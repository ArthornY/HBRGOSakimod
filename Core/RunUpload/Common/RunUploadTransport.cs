using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HBRGOSakiMod.Core.RunUpload;

// StatusCode 0 means no HTTP answer at all (offline, DNS, TLS or timeout).
internal readonly record struct RunUploadResponse(int StatusCode, TimeSpan? RetryAfter, bool Duplicate,
    string ErrorCode, string Error);

internal enum RunUploadOutcome
{
    Delivered,
    Rejected,
    Retry
}

internal interface IRunUploadTransport
{
    Task<RunUploadResponse> PostAsync(Uri endpoint, byte[] gzipBody, string userAgent, CancellationToken token);
}

internal static class RunUploadResponsePolicy
{
    // endpoint.md 4: 201, or 200 for a duplicate, is done; 400/409/413/415
    // can never succeed, nor 422 (the site's own filter: modified_run,
    // early_end). Everything else is temporary, 404 included, so records made
    // before the site goes live are not deleted.
    internal static RunUploadOutcome Classify(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => RunUploadOutcome.Delivered,
        400 or 409 or 413 or 415 or 422 => RunUploadOutcome.Rejected,
        _ => RunUploadOutcome.Retry
    };

    internal static TimeSpan Backoff(int attempt)
    {
        double minutes = RunUploadConstants.FirstRetryDelay.TotalMinutes * Math.Pow(2, Math.Max(0, attempt - 1));
        return minutes >= RunUploadConstants.MaxRetryDelay.TotalMinutes
            ? RunUploadConstants.MaxRetryDelay
            : TimeSpan.FromMinutes(minutes);
    }
}

// Plain HTTPS POST: no cookies, no credentials, no redirects (a redirect could
// carry the record somewhere the consent text did not name).
internal sealed class HttpRunUploadTransport : IRunUploadTransport, IDisposable
{
    private const int MaxResponseBytes = 64 * 1024;
    private readonly HttpClient _client;

    internal HttpRunUploadTransport()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = RunUploadConstants.ConnectTimeout,
            UseCookies = false,
            AllowAutoRedirect = false,
            UseProxy = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = RunUploadConstants.RequestTimeout };
    }

    public async Task<RunUploadResponse> PostAsync(Uri endpoint, byte[] gzipBody, string userAgent,
        CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            var content = new ByteArrayContent(gzipBody);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            content.Headers.ContentEncoding.Add("gzip");
            request.Content = content;
            if (!string.IsNullOrEmpty(userAgent)) request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            using HttpResponseMessage response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            string body = await ReadSmallBody(response, token).ConfigureAwait(false);
            (bool duplicate, string code) = ParseBody(body);
            return new RunUploadResponse((int)response.StatusCode, RetryAfter(response), duplicate, code, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or IOException or InvalidOperationException)
        {
            return new RunUploadResponse(0, null, false, null, exception.GetType().Name + ": " + exception.Message);
        }
    }

    public void Dispose() => _client.Dispose();

    private static async Task<string> ReadSmallBody(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content == null) return string.Empty;
        await using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        byte[] buffer = new byte[MaxResponseBytes];
        int total = 0, read;
        while (total < buffer.Length &&
               (read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), token).ConfigureAwait(false)) > 0)
            total += read;
        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue header = response.Headers.RetryAfter;
        if (header?.Delta is TimeSpan delta) return delta;
        if (header?.Date is DateTimeOffset date)
        {
            TimeSpan wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
        return null;
    }

    internal static (bool Duplicate, string Code) ParseBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (false, null);
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (false, null);
            bool duplicate = root.TryGetProperty("duplicate", out JsonElement value) && value.ValueKind == JsonValueKind.True;
            string code = root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object &&
                          error.TryGetProperty("code", out JsonElement codeValue) && codeValue.ValueKind == JsonValueKind.String
                ? codeValue.GetString()
                : null;
            return (duplicate, code);
        }
        catch (JsonException)
        {
            return (false, null);
        }
    }
}
