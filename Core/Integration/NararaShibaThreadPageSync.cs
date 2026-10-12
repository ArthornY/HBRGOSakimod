using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Logging;

namespace HBRGOSakiMod.Core.Integration;

internal enum NararaShibaThreadPageStatus
{
    Idle,
    Loading,
    Ready,
    Stale,
    Unavailable
}

internal sealed record NararaShibaThreadPost(
    long PostId,
    int AuthorPostIndex,
    string PostedAt,
    string PlainText,
    IReadOnlyList<string> ImageUrls);

internal sealed record NararaShibaThreadPageSnapshot(
    long Revision,
    NararaShibaThreadPageStatus Status,
    string ThreadTitle,
    string AuthorName,
    int AuthorId,
    int PageNumber,
    int TotalPages,
    string PlainText,
    IReadOnlyList<NararaShibaThreadPost> Posts,
    DateTimeOffset? UpdatedAtUtc,
    string ErrorMessage)
{
    internal static NararaShibaThreadPageSnapshot Empty { get; } = new(
        0, NararaShibaThreadPageStatus.Idle,
        "丰川祥子、为了人类的未来而战……真的能战吗？", "夜見翔",
        NararaShibaThreadPageSync.AuthorId, 0, 0, "",
        Array.Empty<NararaShibaThreadPost>(), null, "");
}

/// <summary>
/// A shared feed refreshed once at game startup for the Overwork portrait.
/// Reading Current never starts network activity or invokes a Godot object
/// from a worker thread.
/// </summary>
internal static class NararaShibaThreadPageSync
{
    internal const int ThreadId = 41989465;
    internal const int AuthorId = 62906407;
    internal const string ThreadUrl =
        "https://ngabbs.com/read.php?tid=41989465&authorid=62906407&page=e";

    private static readonly NararaShibaThreadPageFeed Feed = new(
        CreateClient(), TimeProvider.System,
        message => Log.Warn(message));

    internal static NararaShibaThreadPageSnapshot Current => Feed.Current;

    internal static void StartSessionRefresh(string cachePath = null) =>
        _ = Feed.StartSessionRefreshAsync(cachePath);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            // This public feed does not use or retain forum login/guest cookies.
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(5),
            MaxResponseContentBufferSize = 2 * 1024 * 1024
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-User-Agent", "NGA_skull/7.3.1");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "NGA/7.3.1");
        return client;
    }
}

// Kept separate from the static owner so parsing, session deduplication, and failure
// retention can be checked without Godot, a game process, or a live endpoint.
internal sealed class NararaShibaThreadPageFeed
{
    private const string Endpoint =
        "https://ngabbs.com/app_api.php?__lib=post&__act=list";

    private readonly object _sync = new();
    private readonly HttpClient _client;
    private readonly TimeProvider _time;
    private readonly Action<string> _warn;
    private NararaShibaThreadPageSnapshot _current =
        NararaShibaThreadPageSnapshot.Empty;
    private Task _inFlight = Task.CompletedTask;
    private bool _sessionStarted;

    internal NararaShibaThreadPageFeed(
        HttpClient client, TimeProvider time, Action<string> warn)
    {
        _client = client;
        _time = time;
        _warn = warn;
    }

    internal NararaShibaThreadPageSnapshot Current => Volatile.Read(ref _current);

    internal Task StartSessionRefreshAsync(string cachePath = null)
    {
        lock (_sync)
        {
            if (_sessionStarted)
                return _inFlight;
            _sessionStarted = true;
            Publish(_current with
            {
                Revision = _current.Revision + 1,
                Status = NararaShibaThreadPageStatus.Loading,
                ErrorMessage = ""
            });
            // Parsing and request setup stay off the game/render thread too.
            _inFlight = Task.Run(() => RefreshAsync(cachePath));
            return _inFlight;
        }
    }

    private async Task RefreshAsync(string cachePath)
    {
        NararaShibaThreadPageSnapshot cached =
            NararaShibaThreadPageCache.TryLoad(cachePath);
        if (cached != null)
        {
            lock (_sync)
            {
                Publish(cached with
                {
                    Revision = _current.Revision + 1,
                    Status = NararaShibaThreadPageStatus.Loading
                });
            }
        }
        try
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["tid"] = NararaShibaThreadPageSync.ThreadId.ToString(
                        CultureInfo.InvariantCulture),
                    ["authorid"] = NararaShibaThreadPageSync.AuthorId.ToString(
                        CultureInfo.InvariantCulture),
                    ["page"] = "e"
                });
            using HttpResponseMessage response =
                await _client.PostAsync(Endpoint, content).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);
            NararaShibaThreadPageSnapshot next =
                NararaShibaThreadPageParser.Parse(json, _time.GetUtcNow());
            lock (_sync)
            {
                Publish(next with { Revision = _current.Revision + 1 });
            }
            NararaShibaThreadPageCache.TrySave(cachePath, json, next);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or
                JsonException or RegexMatchTimeoutException)
        {
            lock (_sync)
            {
                Publish(_current with
                {
                    Revision = _current.Revision + 1,
                    Status = _current.UpdatedAtUtc.HasValue
                        ? NararaShibaThreadPageStatus.Stale
                        : NararaShibaThreadPageStatus.Unavailable,
                    ErrorMessage = _current.UpdatedAtUtc.HasValue
                        ? "本次更新失败，显示上次缓存"
                        : "本次更新失败，下次进入游戏重试"
                });
            }
            // At most one network warning per game session, never once per frame.
            _warn("Failed to refresh the Narara Shiba author-only page; " +
                "keeping the last available portrait content: " +
                exception.GetType().Name);
        }
    }

    private void Publish(NararaShibaThreadPageSnapshot snapshot) =>
        Volatile.Write(ref _current, snapshot);
}

internal static class NararaShibaThreadPageCache
{
    private const int MaxCacheBytes = 2 * 1024 * 1024;

    internal static NararaShibaThreadPageSnapshot TryLoad(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxCacheBytes)
                return null;
            using JsonDocument cache = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = cache.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("FormatVersion", out JsonElement version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out int formatVersion) || formatVersion != 1 ||
                !root.TryGetProperty("UpdatedAtUtc", out JsonElement time) ||
                time.ValueKind != JsonValueKind.String ||
                !time.TryGetDateTimeOffset(out DateTimeOffset updatedAtUtc) ||
                !root.TryGetProperty("Page", out JsonElement page))
                return null;
            return NararaShibaThreadPageParser.Parse(
                    page.GetRawText(), updatedAtUtc, plainTextContent: true)
                with { Status = NararaShibaThreadPageStatus.Stale };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                JsonException or RegexMatchTimeoutException or ArgumentException)
        {
            return null;
        }
    }

    internal static void TrySave(
        string path, string json, NararaShibaThreadPageSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            var page = new Dictionary<string, object>();
            foreach (string name in new[]
            {
                "code", "totalPage", "currentPage", "perPage", "vrows", "tauthorid"
            })
                page[name] = root.GetProperty(name);
            page["tauthor"] = snapshot.AuthorName;
            page["tsubject"] = snapshot.ThreadTitle;
            var posts = new List<Dictionary<string, object>>();
            foreach (NararaShibaThreadPost item in snapshot.Posts)
            {
                var post = new Dictionary<string, object>
                {
                    ["pid"] = item.PostId,
                    ["tid"] = NararaShibaThreadPageSync.ThreadId,
                    ["lou"] = item.AuthorPostIndex,
                    ["postdate"] = item.PostedAt,
                    ["content"] = item.PlainText,
                    ["images"] = item.ImageUrls
                };
                post["author"] = new Dictionary<string, int>
                {
                    ["uid"] = NararaShibaThreadPageSync.AuthorId
                };
                posts.Add(post);
            }
            page["result"] = posts;
            // Persist only public display fields, never guest_token, cookies,
            // user profile objects, or the site's executable HTML extras.
            string cache = JsonSerializer.Serialize(new
            {
                FormatVersion = 1, snapshot.UpdatedAtUtc, Page = page
            });
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path + ".tmp", cache, Encoding.UTF8);
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                JsonException or ArgumentException or KeyNotFoundException)
        {
            // A read-only cache directory must not hide a successful live page.
        }
    }
}

internal static class NararaShibaThreadPageParser
{
    private const int MaxPostCharacters = 64 * 1024;
    private const int MaxPageCharacters = 256 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex Images = Pattern(
        @"\[img(?:=[^\]]*)?\](.*?)\[/img\]");
    private static readonly Regex ActiveContent = Pattern(
        @"<(script|style|iframe)\b[^>]*>.*?</\1\s*>");
    private static readonly Regex LineBreaks = Pattern(
        @"<br\s*/?>|</?(?:p|div|h[1-6]|li|tr|blockquote)\b[^>]*>");
    private static readonly Regex HtmlTags = Pattern(@"<[^>]*>");
    private static readonly Regex BlockMarkup = Pattern(
        @"\[/?(?:quote|collapse|list|\*)(?:[=\s][^\]]*)?\]");
    private static readonly Regex InlineMarkup = Pattern(
        @"\[/?(?:b|i|u|s|del|color|size|font|align|url|uid|pid|h|flash|video|audio|t|markdown)(?:[=\s][^\]]*)?\]");
    private static readonly Regex Emotes = Pattern(@"\[s:[^:\]]+:([^\]]+)\]");
    private static readonly Regex HorizontalSpace = Pattern(@"[^\S\n]+");
    private static readonly Regex ExtraBreaks = Pattern(@"\n{3,}");

    internal static NararaShibaThreadPageSnapshot Parse(
        string json, DateTimeOffset updatedAtUtc, bool plainTextContent = false)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || ReadInt(root, "code") != 0)
            throw new JsonException("NGA did not return a successful page.");

        int authorId = ReadInt(root, "tauthorid");
        int page = ReadInt(root, "currentPage");
        int totalPages = ReadInt(root, "totalPage");
        int perPage = ReadInt(root, "perPage");
        int rows = ReadInt(root, "vrows");
        if (authorId != NararaShibaThreadPageSync.AuthorId ||
            page < 1 || page != totalPages || perPage < 1 || perPage > 100 ||
            rows < 1 || (rows - 1) / perPage + 1 != totalPages ||
            !root.TryGetProperty("result", out JsonElement results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0 || results.GetArrayLength() > perPage ||
            results.GetArrayLength() != rows - (page - 1) * perPage)
        {
            throw new JsonException("NGA did not return the author-only final page.");
        }

        var posts = new List<NararaShibaThreadPost>();
        var pageText = new StringBuilder();
        foreach (JsonElement item in results.EnumerateArray())
        {
            // Reject a silently ignored author filter rather than presenting the
            // full thread's last page as the last page of the author's posts.
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("author", out JsonElement author) ||
                ReadInt(author, "uid") != authorId ||
                ReadInt(item, "tid") != NararaShibaThreadPageSync.ThreadId)
                throw new JsonException("The page contained another author's post.");

            string raw = ReadString(item, "content");
            if (raw.Length > MaxPostCharacters ||
                pageText.Length + raw.Length > MaxPageCharacters)
                throw new JsonException("NGA page content exceeded the display limit.");

            var imageUrls = new List<string>();
            string plain = plainTextContent ? raw : ToPlainText(raw, imageUrls);
            if (plainTextContent && item.TryGetProperty("images", out JsonElement images) &&
                images.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement image in images.EnumerateArray())
                {
                    if (imageUrls.Count >= 4)
                        break;
                    if (image.ValueKind == JsonValueKind.String)
                    {
                        string url = NormalizeImageUrl(image.GetString() ?? "");
                        if (url.Length > 0)
                            imageUrls.Add(url);
                    }
                }
            }
            string postedAt = plainTextContent ? ReadString(item, "postdate")
                : ToPlainText(ReadString(item, "postdate"));
            // NGA renumbers lou after author filtering. This is deliberately
            // called AuthorPostIndex instead of a floor in the whole thread.
            int index = ReadInt(item, "lou");
            long id = ReadLong(item, "pid");
            if (index != (page - 1) * perPage + posts.Count || id < 0)
                throw new JsonException("NGA returned an invalid post identifier.");

            posts.Add(new NararaShibaThreadPost(
                id, index, postedAt, plain, imageUrls.AsReadOnly()));
            if (pageText.Length > 0)
                pageText.Append("\n\n");
            pageText.Append(postedAt).Append('\n').Append(plain);
        }

        return new NararaShibaThreadPageSnapshot(
            0, NararaShibaThreadPageStatus.Ready,
            plainTextContent ? ReadString(root, "tsubject") : ToPlainText(ReadString(root, "tsubject")),
            plainTextContent ? ReadString(root, "tauthor") : ToPlainText(ReadString(root, "tauthor")),
            authorId, page, totalPages, pageText.ToString(), posts.AsReadOnly(),
            updatedAtUtc, "");
    }

    internal static string ToPlainText(
        string value, List<string> imageUrls = null)
    {
        // NGA mixes HTML and BBCode. Do not pass either directly to RichTextLabel.
        string text = ActiveContent.Replace(value, "");
        text = Images.Replace(text, match =>
        {
            string image = NormalizeImageUrl(match.Groups[1].Value);
            if (image.Length > 0 && imageUrls != null && imageUrls.Count < 4 &&
                !imageUrls.Contains(image))
                imageUrls.Add(image);
            return "\n［图片］\n";
        });
        text = LineBreaks.Replace(text, "\n");
        text = HtmlTags.Replace(text, "");
        text = BlockMarkup.Replace(text, "\n");
        text = InlineMarkup.Replace(text, "");
        text = Emotes.Replace(text, "（$1）");
        text = WebUtility.HtmlDecode(text).Replace('\u00a0', ' ')
            .Replace("\r\n", "\n").Replace('\r', '\n');

        var printable = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsControl(character) || character is '\n' or '\t')
                printable.Append(character);
        }
        text = HorizontalSpace.Replace(printable.ToString(), " ");
        text = ExtraBreaks.Replace(text, "\n\n");
        return text.Trim();
    }

    private static string NormalizeImageUrl(string value)
    {
        string url = WebUtility.HtmlDecode(value).Trim();
        if (url.StartsWith("./mon_", StringComparison.Ordinal))
            url = "https://img.nga.cn/attachments/" + url[2..];
        if (url.StartsWith("//", StringComparison.Ordinal))
            url = "https:" + url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            uri.UserInfo.Length > 0 || !uri.IsDefaultPort ||
            !uri.Host.EndsWith(".nga.cn", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.StartsWith("img", StringComparison.OrdinalIgnoreCase))
            return "";
        return new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
    }

    private static Regex Pattern(string expression) => new(
        expression, RegexOptions.IgnoreCase | RegexOptions.Singleline |
            RegexOptions.CultureInvariant, RegexTimeout);

    private static int ReadInt(JsonElement value, string name)
    {
        long number = ReadLong(value, name);
        return number >= int.MinValue && number <= int.MaxValue ? (int)number : -1;
    }

    private static long ReadLong(JsonElement value, string name)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(name, out JsonElement element))
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out long number))
                return number;
            if (element.ValueKind == JsonValueKind.String &&
                long.TryParse(element.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out number))
                return number;
        }
        return -1;
    }

    private static string ReadString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out JsonElement element) &&
        element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : "";
}
