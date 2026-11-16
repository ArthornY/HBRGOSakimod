using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace HBRGOSakiMod.Core.RunUpload;

internal sealed record RunSubmitResult(bool Queued, string Reason, string UploadId);

// Owns the switch, the local queue and sending. The HTTP client is created
// only when the switch is on and there is something due, so a player who never
// agreed causes no connection, DNS lookup or request at all.
internal sealed class RunUploadCoordinator
{
    internal const string InProgressFileName = "current-run.json";

    private readonly object _gate = new();
    private readonly RunUploadSettingsStore _settings;
    private readonly RunUploadQueue _queue;
    private readonly Func<IRunUploadTransport> _transportFactory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<string> _log;
    private readonly Uri _endpoint;
    private readonly string _userAgent;
    private IRunUploadTransport _transport;
    private RunUploadSender _sender;
    private CancellationTokenSource _cancel = new();

    internal RunUploadCoordinator(string root, Func<IRunUploadTransport> transportFactory, Func<DateTimeOffset> clock,
        Action<string> log, Uri endpoint, string userAgent)
    {
        Root = root;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _log = log ?? (_ => { });
        _settings = new RunUploadSettingsStore(root);
        _queue = new RunUploadQueue(Path.Combine(root, "queue"), _clock);
        _transportFactory = transportFactory;
        _endpoint = endpoint;
        _userAgent = userAgent;
    }

    internal string Root { get; }
    internal string InProgressPath => Path.Combine(Root, InProgressFileName);
    internal bool IsEnabled => _settings.IsEnabled;
    internal bool NeedsPrompt => _settings.NeedsPrompt;
    internal string InstallId => _settings.InstallId;
    internal RunUploadQueue Queue => _queue;
    internal bool TransportCreated => _transport != null;

    // The answer to the first-launch prompt, or to the same explanation shown
    // when the settings toggle goes from off to on.
    internal string AnswerPrompt(bool allow)
    {
        string installId = _settings.AnswerPrompt(allow, _clock());
        lock (_gate)
        {
            if (!allow) _cancel.Cancel();
            else if (_cancel.IsCancellationRequested) _cancel = new CancellationTokenSource();
        }
        return installId;
    }

    internal string Enable() => AnswerPrompt(true);

    internal (string Old, string New) RegenerateInstallId() => _settings.RegenerateInstallId();

    // Stops at once: a request in flight is cancelled. Queued runs stay but
    // are not sent until the switch is on again.
    internal void Disable()
    {
        _settings.Disable();
        lock (_gate) _cancel.Cancel();
    }

    // "清空本地未上传数据": the queue and the record of the run in progress.
    internal int ClearLocalData()
    {
        lock (_gate)
        {
            _cancel.Cancel();
            _cancel = new CancellationTokenSource();
        }
        int removed = _queue.Clear();
        // An exclusion marker holds no run data; it stays so an excluded run
        // is not picked up again after a restart.
        if (ExcludedMarkerPresent()) return removed;
        RunUploadFiles.TryDelete(InProgressPath);
        RunUploadFiles.TryDelete(InProgressPath + ".tmp");
        return removed;
    }

    internal bool Enqueue(string uploadId, byte[] envelopeJson, out string error)
    {
        if (!IsEnabled)
        {
            error = "uploading is off";
            return false;
        }
        return _queue.Enqueue(uploadId, envelopeJson, out error);
    }

    // A finished run: its in-progress copy goes in every case; it is queued
    // only when the switch is on and the filter of schema-v1.md 3.2 lets it
    // through. A dropped run leaves nothing behind.
    internal RunSubmitResult SubmitFinishedRun(JsonObject run, string modVersion, string gameVersion)
    {
        RunUploadFiles.TryDelete(InProgressPath);
        if (run == null) return new RunSubmitResult(false, "missing", null);
        if (!IsEnabled) return new RunSubmitResult(false, "uploading is off", null);
        string reason = RunUploadFilter.RejectReason(run);
        if (reason != null) return new RunSubmitResult(false, reason, null);
        string uploadId = RunUploadIds.NewUuid();
        JsonObject envelope = RunUploadEnvelope.Create(uploadId, InstallId, modVersion, gameVersion, _clock(), run);
        return _queue.Enqueue(uploadId, RunUploadJson.ToUtf8(envelope), out string error)
            ? new RunSubmitResult(true, null, uploadId)
            : new RunSubmitResult(false, error, null);
    }

    // The record of the run in progress, so closing the game does not lose it.
    // Written only while the switch is on. The key (seed and ascension) never
    // leaves this file.
    internal void SaveInProgress(string key, JsonObject run, JsonObject live)
    {
        if (run == null || !IsEnabled) return;
        // Written as text: the run object stays with its builder.
        var text = new StringBuilder();
        text.Append("{\"key\":").Append(JsonValue.Create(key ?? string.Empty).ToJsonString())
            .Append(",\"live\":").Append(live?.ToJsonString(RunUploadJson.Options) ?? "null")
            .Append(",\"run\":").Append(run.ToJsonString(RunUploadJson.Options)).Append('}');
        RunUploadFiles.WriteAtomic(InProgressPath, Encoding.UTF8.GetBytes(text.ToString()));
    }

    // The saved record, detached so a builder can take it over. Nothing when
    // the file is missing, unreadable or an exclusion marker.
    internal (string Key, JsonObject Run, JsonObject Live) LoadInProgress()
    {
        try
        {
            if (!File.Exists(InProgressPath) || RunUploadJson.ParseObject(File.ReadAllBytes(InProgressPath)) is not { } document)
                return (null, null, null);
            string key = document["key"] is JsonValue value && value.TryGetValue(out string text) ? text : null;
            JsonObject run = document["run"] as JsonObject;
            JsonObject live = document["live"] as JsonObject;
            document.Remove("run");
            document.Remove("live");
            return (key, run, live);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, null, null);
        }
    }

    internal void DeleteInProgress() => RunUploadFiles.TryDelete(InProgressPath);

    // The run in progress must not be recorded (a modifier or the console was
    // used). The file then holds only the run's key and the reason, no run data,
    // so a continued run stays excluded after the game restarts.
    internal void MarkRunExcluded(string runKey, string reason)
    {
        if (!IsEnabled || string.IsNullOrEmpty(runKey)) return;
        var marker = new JsonObject { ["key"] = runKey, ["excluded"] = reason ?? "excluded" };
        RunUploadFiles.WriteAtomic(InProgressPath, RunUploadJson.ToUtf8(marker));
    }

    private bool ExcludedMarkerPresent()
    {
        try
        {
            return File.Exists(InProgressPath) &&
                   RunUploadJson.ParseObject(File.ReadAllBytes(InProgressPath)) is { } document &&
                   document.ContainsKey("excluded") && !document.ContainsKey("run");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    internal string ExcludedReason(string runKey)
    {
        try
        {
            if (string.IsNullOrEmpty(runKey) || !File.Exists(InProgressPath)) return null;
            JsonObject document = RunUploadJson.ParseObject(File.ReadAllBytes(InProgressPath));
            return document?["key"]?.GetValue<string>() == runKey ? document["excluded"]?.GetValue<string>() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
                                              or InvalidOperationException)
        {
            return null;
        }
    }

    internal int Prune() => _queue.Prune();

    internal async Task<RunUploadPassResult> SendDueAsync()
    {
        if (!IsEnabled) return RunUploadPassResult.Nothing("disabled");
        if (_queue.Due().Count == 0) return RunUploadPassResult.Nothing("nothing due");
        RunUploadSender sender;
        CancellationToken token;
        lock (_gate)
        {
            if (_cancel.IsCancellationRequested) return RunUploadPassResult.Nothing("cancelled");
            _transport ??= _transportFactory();
            _sender ??= new RunUploadSender(_queue, _transport, () => _settings.IsEnabled, _clock, _endpoint,
                _userAgent, _log);
            sender = _sender;
            token = _cancel.Token;
        }
        return await sender.SendDueAsync(token).ConfigureAwait(false);
    }

    // Fire and forget from the game thread; the work runs on the thread pool.
    internal void SendInBackground()
    {
        if (!IsEnabled) return;
        Task.Run(async () =>
        {
            try
            {
                RunUploadPassResult result = await SendDueAsync().ConfigureAwait(false);
                if (result.Delivered + result.Rejected + result.Deferred > 0)
                    _log($"Run upload pass: {result.Delivered} sent, {result.Rejected} refused, {result.Deferred} kept for later" +
                         (result.StopReason != null ? $" ({result.StopReason})" : "") + ".");
            }
            catch (Exception exception)
            {
                _log("Run upload pass failed: " + exception.Message);
            }
        });
    }
}
