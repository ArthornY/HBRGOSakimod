using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HBRGOSakiMod.Core.RunUpload;

internal sealed record RunUploadPassResult(int Delivered, int Rejected, int Deferred, string StopReason)
{
    internal static RunUploadPassResult Nothing(string reason) => new(0, 0, 0, reason);
}

// Sends the due queue entries one at a time. The switch is read again before
// every request, so turning it off stops the pass without another byte sent.
internal sealed class RunUploadSender
{
    private readonly RunUploadQueue _queue;
    private readonly IRunUploadTransport _transport;
    private readonly Func<bool> _isEnabled;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Uri _endpoint;
    private readonly string _userAgent;
    private readonly Action<string> _log;
    private int _running;

    internal RunUploadSender(RunUploadQueue queue, IRunUploadTransport transport, Func<bool> isEnabled,
        Func<DateTimeOffset> clock, Uri endpoint, string userAgent, Action<string> log)
    {
        _queue = queue;
        _transport = transport;
        _isEnabled = isEnabled;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _endpoint = endpoint;
        _userAgent = userAgent;
        _log = log ?? (_ => { });
    }

    internal bool IsRunning => Volatile.Read(ref _running) == 1;

    internal async Task<RunUploadPassResult> SendDueAsync(CancellationToken token)
    {
        if (!_isEnabled()) return RunUploadPassResult.Nothing("disabled");
        if (Interlocked.Exchange(ref _running, 1) == 1) return RunUploadPassResult.Nothing("busy");
        int delivered = 0, rejected = 0, deferred = 0;
        string stop = null;
        try
        {
            foreach (RunUploadQueueItem item in _queue.Due())
            {
                if (token.IsCancellationRequested)
                {
                    stop = "cancelled";
                    break;
                }
                if (!_isEnabled())
                {
                    stop = "disabled";
                    break;
                }

                byte[] body;
                try
                {
                    byte[] envelope = _queue.ReadEnvelope(item.UploadId);
                    if (envelope == null) continue;
                    body = RunUploadEnvelope.Gzip(RunUploadEnvelope.Restamp(envelope, _clock()));
                }
                catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException)
                {
                    _queue.Remove(item.UploadId);
                    rejected++;
                    _log($"Dropped unreadable queued run {item.UploadId}: {exception.Message}");
                    continue;
                }
                if (body.LongLength > RunUploadConstants.MaxCompressedBytes)
                {
                    _queue.Remove(item.UploadId);
                    rejected++;
                    _log($"Dropped queued run {item.UploadId}: {body.LongLength} compressed bytes.");
                    continue;
                }

                RunUploadResponse response;
                try
                {
                    response = await _transport.PostAsync(_endpoint, body, _userAgent, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // An interrupted attempt is not counted; the run stays queued as it was.
                    stop = "cancelled";
                    break;
                }

                switch (RunUploadResponsePolicy.Classify(response.StatusCode))
                {
                    case RunUploadOutcome.Delivered:
                        _queue.Remove(item.UploadId);
                        delivered++;
                        break;
                    case RunUploadOutcome.Rejected:
                        _queue.Remove(item.UploadId);
                        rejected++;
                        _log($"The site refused run {item.UploadId}: HTTP {response.StatusCode} {response.ErrorCode}");
                        break;
                    default:
                        TimeSpan wait = RunUploadResponsePolicy.Backoff(item.Attempts + 1);
                        if (response.RetryAfter is TimeSpan retryAfter && retryAfter > wait) wait = retryAfter;
                        _queue.MarkFailed(item.UploadId, wait,
                            response.StatusCode == 0 ? response.Error : $"HTTP {response.StatusCode} {response.ErrorCode}");
                        deferred++;
                        break;
                }

                // Offline, rate limited or the site is down: the rest would fail the same way.
                if (RunUploadResponsePolicy.Classify(response.StatusCode) == RunUploadOutcome.Retry)
                {
                    stop = response.StatusCode == 0 ? "unreachable" : $"http-{response.StatusCode}";
                    break;
                }
            }
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
        return new RunUploadPassResult(delivered, rejected, deferred, stop);
    }
}
