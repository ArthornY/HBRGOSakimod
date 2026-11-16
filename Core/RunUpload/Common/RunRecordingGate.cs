using System;
using System.IO;
using System.Text.Json.Nodes;

namespace HBRGOSakiMod.Core.RunUpload;

// Whether the run in progress is recorded (schema-v1.md 13). RunRecorder
// reports the game's lifecycle here and keeps a record only when this allows
// it; the offline checks drive the same class.
//
// A run is recorded from its start or not at all. It is recorded when it
// starts while uploading is on, and continued after a restart only together
// with its own saved record. A run already going when uploading is turned on
// (a saved run continued later included) is never recorded or uploaded;
// recording starts with the next new run.
internal sealed class RunRecordingGate
{
    internal const string ReasonOff = "off";
    internal const string ReasonModifier = "modifier";
    internal const string ReasonConsole = "console";
    internal const string ReasonNoRecord = "no-record";
    internal const string ReasonTurnedOn = "turned-on-during-run";
    internal const string ReasonCleared = "cleared";

    private readonly RunUploadCoordinator _uploads;

    internal RunRecordingGate(RunUploadCoordinator uploads) => _uploads = uploads;

    internal string RunKey { get; private set; }
    internal bool RunActive { get; private set; }

    // Why the run in progress is not recorded; null while it may be.
    internal string SkipReason { get; private set; }

    // A console command changed the run in progress.
    internal bool ConsoleUsed { get; private set; }

    // A new run. True when it is recorded from its start.
    internal bool RunStarted(string runKey, RunIntegrity session)
    {
        Begin(runKey);
        _uploads.DeleteInProgress();
        if (!_uploads.IsEnabled) return Skip(ReasonOff);
        if (session?.HasModifiers == true) return Exclude(ReasonModifier);
        return true;
    }

    // A saved run continued. The record to go on with, or none: the run then
    // stays unrecorded.
    internal (RunRecordBuilder Record, JsonObject Live) RunLoaded(string runKey, RunIntegrity session)
    {
        Begin(runKey);
        if (!_uploads.IsEnabled)
        {
            // Played on while off: a saved record would miss this part.
            _uploads.DeleteInProgress();
            Skip(ReasonOff);
            return default;
        }
        if (session?.HasModifiers == true)
        {
            Exclude(ReasonModifier);
            return default;
        }
        if (_uploads.ExcludedReason(runKey) is { } excluded)
        {
            // Excluded in an earlier session (a modifier or the console).
            Skip(excluded);
            return default;
        }
        (string key, JsonObject run, JsonObject live) = _uploads.LoadInProgress();
        if (key == runKey && RunRecordBuilder.Resume(run) is { } record)
        {
            record.AddSession();
            if (session?.OtherGameplayMods == true) record.MarkOtherGameplayMods();
            return (record, live);
        }
        // No record from its start: the run began before uploading was turned
        // on, or its record was cleared.
        _uploads.DeleteInProgress();
        Skip(ReasonNoRecord);
        return default;
    }

    // Uploading was turned on or off, or local data was cleared, during a run:
    // the rest of that run is not recorded either. True when a run was going.
    internal bool StopCurrentRun(string reason)
    {
        if (!RunActive) return false;
        Skip(reason);
        return true;
    }

    // A console command finished. True when it may have changed the run in
    // progress, which is then excluded: its saved record is replaced by a
    // marker holding only the run's key and the reason.
    internal bool ConsoleCommand(string commandName, bool succeeded)
    {
        if (!succeeded || !RunActive || !RunUploadConsole.ChangesRun(commandName)) return false;
        ConsoleUsed = true;
        Exclude(ReasonConsole);
        return true;
    }

    // A saved run abandoned from the main menu ends without being loaded.
    // Its saved record, unless that run was skipped in this session.
    internal JsonObject SavedRecordOfEndedRun(string runKey)
    {
        if (string.IsNullOrEmpty(runKey) || !_uploads.IsEnabled) return null;
        if (runKey == RunKey && SkipReason != null) return null;
        (string key, JsonObject run, _) = _uploads.LoadInProgress();
        return key == runKey ? run : null;
    }

    internal bool ConsoleUsedIn(string runKey) => ConsoleUsed && runKey == RunKey;

    internal void RunEnded() => RunActive = false;

    private void Begin(string runKey)
    {
        RunKey = runKey;
        RunActive = true;
        SkipReason = null;
        ConsoleUsed = false;
    }

    private bool Skip(string reason)
    {
        SkipReason ??= reason;
        return false;
    }

    // Skipped here at once; the marker (written only while on) keeps the run
    // excluded after a restart and is best effort.
    private bool Exclude(string reason)
    {
        Skip(reason);
        try
        {
            _uploads.MarkRunExcluded(RunKey, reason);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        return false;
    }
}
