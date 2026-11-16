using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HBRGOSakiMod.Core.RunUpload;

internal sealed record RunUploadQueueItem(string UploadId, DateTimeOffset CreatedAt, int Attempts,
    DateTimeOffset NextAttemptAt, string LastError);

internal sealed class RunUploadQueueMeta
{
    public string UploadId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string NextAttemptAt { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
}

// Finished runs that have not reached the site yet. One gzip file per run plus
// a small sidecar for retry bookkeeping. Bounded by count and age; the oldest
// runs go first. The main thread enqueues while the background sender reads,
// so every operation holds the same lock.
internal sealed class RunUploadQueue
{
    private const string DataSuffix = ".json.gz";
    private const string MetaSuffix = ".meta.json";
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly string _directory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly int _maxRuns;
    private readonly TimeSpan _maxAge;

    internal RunUploadQueue(string directory, Func<DateTimeOffset> clock,
        int maxRuns = RunUploadConstants.MaxQueuedRuns, TimeSpan? maxAge = null)
    {
        _directory = directory;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _maxRuns = Math.Max(1, maxRuns);
        _maxAge = maxAge ?? RunUploadConstants.MaxQueueAge;
    }

    internal string Directory => _directory;

    internal int Count
    {
        get
        {
            lock (Gate) return ReadAll().Count;
        }
    }

    // Returns false (with a reason) when the record breaks the site's size limits.
    internal bool Enqueue(string uploadId, byte[] envelopeJson, out string error)
    {
        error = null;
        if (!RunUploadIds.IsValidUuid(uploadId))
        {
            error = "invalid upload id";
            return false;
        }
        if (envelopeJson == null || envelopeJson.Length == 0)
        {
            error = "empty record";
            return false;
        }
        if (envelopeJson.LongLength > RunUploadConstants.MaxUncompressedBytes)
        {
            error = $"record is {envelopeJson.LongLength} bytes, above the {RunUploadConstants.MaxUncompressedBytes} byte limit";
            return false;
        }
        byte[] compressed = RunUploadEnvelope.Gzip(envelopeJson);
        if (compressed.LongLength > RunUploadConstants.MaxCompressedBytes)
        {
            error = $"compressed record is {compressed.LongLength} bytes, above the {RunUploadConstants.MaxCompressedBytes} byte limit";
            return false;
        }
        lock (Gate)
        {
            DateTimeOffset now = _clock();
            RunUploadFiles.WriteAtomic(DataPath(uploadId), compressed);
            WriteMeta(new RunUploadQueueMeta
            {
                UploadId = uploadId,
                CreatedAt = RunUploadEnvelope.FormatTime(now),
                NextAttemptAt = RunUploadEnvelope.FormatTime(now)
            });
            PruneLocked();
        }
        return true;
    }

    internal IReadOnlyList<RunUploadQueueItem> List()
    {
        lock (Gate) return ReadAll();
    }

    // Oldest first, only those whose waiting time has passed.
    internal IReadOnlyList<RunUploadQueueItem> Due()
    {
        lock (Gate)
        {
            DateTimeOffset now = _clock();
            return ReadAll().Where(item => item.NextAttemptAt <= now).ToList();
        }
    }

    internal byte[] ReadEnvelope(string uploadId)
    {
        lock (Gate)
        {
            string path = DataPath(uploadId);
            return File.Exists(path)
                ? RunUploadEnvelope.Gunzip(File.ReadAllBytes(path), RunUploadConstants.MaxUncompressedBytes)
                : null;
        }
    }

    internal void Remove(string uploadId)
    {
        lock (Gate) RemoveLocked(uploadId);
    }

    internal void MarkFailed(string uploadId, TimeSpan wait, string error)
    {
        lock (Gate)
        {
            RunUploadQueueMeta meta = ReadMeta(MetaPath(uploadId));
            if (meta == null) return;
            meta.Attempts++;
            meta.NextAttemptAt = RunUploadEnvelope.FormatTime(_clock() + (wait < TimeSpan.Zero ? TimeSpan.Zero : wait));
            meta.LastError = Trim(error);
            WriteMeta(meta);
        }
    }

    // Age first, then count. Returns how many runs were dropped.
    internal int Prune()
    {
        lock (Gate) return PruneLocked();
    }

    internal int Clear()
    {
        lock (Gate)
        {
            int removed = 0;
            if (!System.IO.Directory.Exists(_directory)) return 0;
            foreach (string path in System.IO.Directory.GetFiles(_directory))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(MetaSuffix, StringComparison.Ordinal)) removed++;
                RunUploadFiles.TryDelete(path);
            }
            return removed;
        }
    }

    private int PruneLocked()
    {
        List<RunUploadQueueItem> items = ReadAll();
        DateTimeOffset cutoff = _clock() - _maxAge;
        int removed = 0;
        foreach (RunUploadQueueItem item in items.Where(item => item.CreatedAt < cutoff).ToList())
        {
            RemoveLocked(item.UploadId);
            items.Remove(item);
            removed++;
        }
        while (items.Count > _maxRuns)
        {
            RemoveLocked(items[0].UploadId);
            items.RemoveAt(0);
            removed++;
        }
        return removed;
    }

    private List<RunUploadQueueItem> ReadAll()
    {
        var items = new List<RunUploadQueueItem>();
        if (!System.IO.Directory.Exists(_directory)) return items;
        foreach (string path in System.IO.Directory.GetFiles(_directory, "*" + MetaSuffix))
        {
            RunUploadQueueMeta meta = ReadMeta(path);
            if (meta == null || !RunUploadIds.IsValidUuid(meta.UploadId) || !File.Exists(DataPath(meta.UploadId)))
            {
                // A sidecar without its record (or a damaged one) cannot be sent.
                RunUploadFiles.TryDelete(path);
                if (meta != null && RunUploadIds.IsValidUuid(meta.UploadId)) RunUploadFiles.TryDelete(DataPath(meta.UploadId));
                continue;
            }
            DateTimeOffset created = ParseTime(meta.CreatedAt) ?? DateTimeOffset.MinValue;
            items.Add(new RunUploadQueueItem(meta.UploadId, created, meta.Attempts,
                ParseTime(meta.NextAttemptAt) ?? created, meta.LastError ?? string.Empty));
        }
        return items.OrderBy(item => item.CreatedAt).ThenBy(item => item.UploadId, StringComparer.Ordinal).ToList();
    }

    private void RemoveLocked(string uploadId)
    {
        RunUploadFiles.TryDelete(DataPath(uploadId));
        RunUploadFiles.TryDelete(MetaPath(uploadId));
    }

    private void WriteMeta(RunUploadQueueMeta meta) =>
        RunUploadFiles.WriteAtomic(MetaPath(meta.UploadId), JsonSerializer.SerializeToUtf8Bytes(meta, Options));

    private static RunUploadQueueMeta ReadMeta(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<RunUploadQueueMeta>(File.ReadAllBytes(path), Options)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ParseTime(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset value)
            ? value
            : null;

    private static string Trim(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= 300 ? text : text.Substring(0, 300);

    private string DataPath(string uploadId) => Path.Combine(_directory, uploadId + DataSuffix);

    private string MetaPath(string uploadId) => Path.Combine(_directory, uploadId + MetaSuffix);
}
