using System;
using System.IO;
using System.Text.Json;

namespace HBRGOSakiMod.Core.RunUpload;

internal sealed class RunUploadSettingsData
{
    public bool Enabled { get; set; }
    public int ConsentVersion { get; set; }
    public string InstallId { get; set; } = string.Empty;
    public string ConsentedAt { get; set; } = string.Empty;
    // The first-launch prompt this player has answered, either way.
    public int PromptVersion { get; set; }
    public string PromptAnswer { get; set; } = string.Empty;
    public string PromptAnsweredAt { get; set; } = string.Empty;
}

// The switch, the answer to the first-launch prompt, the accepted consent and
// the anonymous install ID live in the mod's own user:// file. Uploading counts
// as enabled only with all of them current, so a missing or damaged file
// always means "off", and a newer ConsentVersion means "ask again".
internal sealed class RunUploadSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;
    private RunUploadSettingsData _data;

    internal RunUploadSettingsStore(string directory)
    {
        _path = Path.Combine(directory, "settings.json");
    }

    internal string FilePath => _path;

    internal bool IsEnabled
    {
        get
        {
            lock (_gate) return IsEffective(Data);
        }
    }

    // True until the player answers the current prompt version.
    internal bool NeedsPrompt
    {
        get
        {
            lock (_gate) return Data.PromptVersion < RunUploadConstants.ConsentVersion;
        }
    }

    // Kept after the switch is turned off: the player needs it to ask for deletion.
    internal string InstallId
    {
        get
        {
            lock (_gate) return RunUploadIds.IsValidUuid(Data.InstallId) ? Data.InstallId : string.Empty;
        }
    }

    // "允许" turns uploading on (creating the install ID if needed); "拒绝",
    // or closing the prompt, keeps it off. Either answer is remembered.
    internal string AnswerPrompt(bool allow, DateTimeOffset now)
    {
        lock (_gate)
        {
            RunUploadSettingsData data = Data;
            data.PromptVersion = RunUploadConstants.ConsentVersion;
            data.PromptAnswer = allow ? "allow" : "decline";
            data.PromptAnsweredAt = RunUploadEnvelope.FormatTime(now);
            if (allow)
            {
                if (!RunUploadIds.IsValidUuid(data.InstallId)) data.InstallId = RunUploadIds.NewUuid();
                data.Enabled = true;
                data.ConsentVersion = RunUploadConstants.ConsentVersion;
                data.ConsentedAt = data.PromptAnsweredAt;
            }
            else
            {
                data.Enabled = false;
            }
            Save(data);
            return allow ? data.InstallId : string.Empty;
        }
    }

    // The settings toggle shows the same explanation; agreeing there is the same answer.
    internal string Enable(DateTimeOffset now) => AnswerPrompt(true, now);

    internal void Disable()
    {
        lock (_gate)
        {
            RunUploadSettingsData data = Data;
            data.Enabled = false;
            Save(data);
        }
    }

    // Uploads after this carry a new ID and cannot be linked to earlier ones.
    // Returns (null, null) when there is no ID yet.
    internal (string Old, string New) RegenerateInstallId()
    {
        lock (_gate)
        {
            RunUploadSettingsData data = Data;
            if (!RunUploadIds.IsValidUuid(data.InstallId)) return (null, null);
            string old = data.InstallId;
            string next;
            do next = RunUploadIds.NewUuid();
            while (next == old);
            data.InstallId = next;
            Save(data);
            return (old, next);
        }
    }

    private RunUploadSettingsData Data => _data ??= Load();

    private static bool IsEffective(RunUploadSettingsData data) =>
        data.Enabled && data.ConsentVersion >= RunUploadConstants.ConsentVersion &&
        data.PromptVersion >= RunUploadConstants.ConsentVersion && RunUploadIds.IsValidUuid(data.InstallId);

    private RunUploadSettingsData Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                RunUploadSettingsData data =
                    JsonSerializer.Deserialize<RunUploadSettingsData>(File.ReadAllBytes(_path), Options);
                if (data != null)
                {
                    data.InstallId ??= string.Empty;
                    data.ConsentedAt ??= string.Empty;
                    data.PromptAnswer ??= string.Empty;
                    data.PromptAnsweredAt ??= string.Empty;
                    return data;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new RunUploadSettingsData();
    }

    private void Save(RunUploadSettingsData data) =>
        RunUploadFiles.WriteAtomic(_path, JsonSerializer.SerializeToUtf8Bytes(data, Options));
}
