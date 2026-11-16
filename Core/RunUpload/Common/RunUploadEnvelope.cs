using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.RunUpload;

internal static class RunUploadJson
{
    // Chinese text stays readable and compact; the body is JSON, never HTML.
    internal static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    internal static byte[] ToUtf8(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString(Options));

    internal static JsonObject ParseObject(byte[] utf8) => JsonNode.Parse(utf8) as JsonObject;
}

internal static class RunUploadIds
{
    internal static string NewUuid() => Guid.NewGuid().ToString("D");

    internal static bool IsValidUuid(string value) =>
        !string.IsNullOrEmpty(value) && Guid.TryParseExact(value, "D", out Guid parsed) &&
        parsed != Guid.Empty && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);
}

internal static class RunUploadEnvelope
{
    private static readonly Regex ModVersionPattern = new("^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$", RegexOptions.CultureInvariant);

    internal static string FormatTime(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // endpoint.md 3: 1–32 characters, alphanumeric first, then [A-Za-z0-9._+-].
    internal static string CleanModVersion(string value) =>
        value != null && ModVersionPattern.IsMatch(value) ? value : "unknown";

    // endpoint.md 3: 1–64 printable ASCII characters.
    internal static string CleanGameVersion(string value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";
        var builder = new StringBuilder(Math.Min(value.Length, 64));
        foreach (char c in value)
        {
            if (builder.Length == 64) break;
            if (c >= 0x20 && c <= 0x7E) builder.Append(c);
        }
        string cleaned = builder.ToString().Trim();
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }

    // The fixed outer envelope (schema-v1.md 1). Exactly these seven fields.
    internal static JsonObject Create(string uploadId, string installId, string modVersion, string gameVersion,
        DateTimeOffset uploadedAt, JsonObject run) => new()
    {
        ["schema"] = RunUploadConstants.Schema,
        ["uploadId"] = uploadId,
        ["installId"] = installId,
        ["modVersion"] = CleanModVersion(modVersion),
        ["gameVersion"] = CleanGameVersion(gameVersion),
        ["uploadedAt"] = FormatTime(uploadedAt),
        ["run"] = run
    };

    // uploadedAt is the time of this attempt, so every retry restamps it.
    internal static byte[] Restamp(byte[] envelopeJson, DateTimeOffset now)
    {
        JsonObject envelope = RunUploadJson.ParseObject(envelopeJson)
            ?? throw new InvalidDataException("The queued record is not a JSON object.");
        envelope["uploadedAt"] = FormatTime(now);
        return RunUploadJson.ToUtf8(envelope);
    }

    // One gzip member, as endpoint.md 2 requires.
    internal static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(data, 0, data.Length);
        return output.ToArray();
    }

    internal static byte[] Gunzip(byte[] data, long maxBytes)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > maxBytes)
                throw new InvalidDataException("The queued record is larger than the upload limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}

internal static class RunUploadFiles
{
    // A crash mid-write leaves the old file or the new one, never half of one.
    internal static void WriteAtomic(string path, byte[] data)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, data);
        File.Move(temp, path, overwrite: true);
    }

    internal static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
