using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Godot;
using HBRGOSakiMod.Core.Config;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Modding;

namespace HBRGOSakiMod.Core.RunUpload;

// Game-side entry for run uploading: paths under user://, the first-launch
// prompt and the settings rows, the loaded-mod check, and handing finished
// runs to the queue.
internal static class RunUploadService
{
    private const string ModId = "HBRGOSakiMod";
    private static RunUploadCoordinator _coordinator;
    private static RunRecordingGate _gate;
    private static RunIntegrity _sessionIntegrity;
    private static string _modVersion;
    private static bool _initialized;
    // The consent dialog on screen, if any. A dialog removed without an answer
    // (a scene change clearing the modal layer) no longer blocks a new one.
    private static NRunUploadDialog _consentDialog;

    internal static RunUploadCoordinator Coordinator => _coordinator ??= new RunUploadCoordinator(
        ProjectSettings.GlobalizePath("user://HBRGOSakiMod/run-upload"),
        () => new HttpRunUploadTransport(),
        () => DateTimeOffset.UtcNow,
        Log,
        RunUploadConstants.EndpointUri,
        "HBRGOSakiMod/" + RunUploadEnvelope.CleanModVersion(ModVersion));

    // Which run is recorded: from its start or not at all.
    internal static RunRecordingGate Gate => _gate ??= new RunRecordingGate(Coordinator);

    internal static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            RunRecorder.Register();
            Coordinator.Prune();
            // Startup retry; does nothing (and creates no connection) while the switch is off.
            Coordinator.SendInBackground();
        }
        catch (Exception exception)
        {
            Log("Run upload setup failed: " + exception);
        }
    }

    internal static bool IsEnabled
    {
        get
        {
            try
            {
                return Coordinator.IsEnabled;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    internal static bool NeedsFirstPrompt
    {
        get
        {
            try
            {
                return Coordinator.NeedsPrompt;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    internal static string InstallId
    {
        get
        {
            try
            {
                return Coordinator.InstallId;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

    // Cached only once the manifest has been found.
    internal static string ModVersion => _modVersion ?? ReadModVersion();

    private static string ReadModVersion()
    {
        try
        {
            string version = ModManager.Mods?.FirstOrDefault(mod => mod?.manifest?.id == ModId)?.manifest?.version;
            if (!string.IsNullOrWhiteSpace(version)) return _modVersion = version;
        }
        catch (Exception)
        {
        }
        return "unknown";
    }

    private static string GameVersion(string fallback)
    {
        try
        {
            string version = ReleaseInfoManager.Instance?.ReleaseInfo?.Version;
            if (!string.IsNullOrWhiteSpace(version)) return version;
        }
        catch (Exception)
        {
        }
        return string.IsNullOrWhiteSpace(fallback) ? "unknown" : fallback;
    }

    // Known modifiers and "another gameplay mod is on", from the mods loaded in
    // this session. Mods cannot change while the game runs.
    internal static RunIntegrity SessionIntegrity => _sessionIntegrity ??= ReadSessionIntegrity();

    private static RunIntegrity ReadSessionIntegrity()
    {
        try
        {
            var mods = new List<RunModInfo>();
            foreach (Mod mod in ModManager.Mods ?? Array.Empty<Mod>())
            {
                if (mod?.manifest == null || mod.state is not (ModLoadState.Loaded or ModLoadState.AddedAtRuntime)) continue;
                mods.Add(new RunModInfo(mod.manifest.id, mod.manifest.affectsGameplay));
            }
            return RunIntegrity.FromMods(mods);
        }
        catch (Exception exception)
        {
            Log("Reading the loaded mods failed: " + exception.Message);
            return RunIntegrity.Clean;
        }
    }

    // ---- the prompt and the settings ----------------------------------------------------

    // The first-launch prompt, shown once on the main menu (NRunUploadFirstPrompt).
    internal static bool ShowFirstPrompt(Control returnFocus) => ShowConsentDialog(returnFocus);

    // The settings toggle. Off is immediate; on shows the same explanation.
    // Values BaseLib applies while loading or restoring its own config file
    // are ignored: the consent file in user:// decides.
    internal static void RequestEnabled(bool value)
    {
        if (IsConfigLoad()) return;
        if (!value)
        {
            if (IsEnabled) Disable();
            return;
        }
        if (IsEnabled) return;
        Callable.From(() =>
        {
            if (IsEnabled || ShowConsentDialog(null)) return;
            Log("The consent dialog could not open; uploading stays off.");
            HBRGOSakiConfig.RefreshRunUploadSettings();
        }).CallDeferred();
    }

    private static bool ShowConsentDialog(Control returnFocus)
    {
        if (_consentDialog != null && _consentDialog.IsOpen) return true;
        _consentDialog = NRunUploadDialog.Open(RunUploadText.ConsentTitle, RunUploadText.ConsentBody(),
            RunUploadText.Allow, RunUploadText.Deny, () => Answer(true), () => Answer(false), returnFocus);
        return _consentDialog != null;
    }

    // "允许", or "拒绝" / closing the dialog. Both answers are remembered.
    // After "允许" recording starts with the next new run: a run already going
    // (a saved one continued later included) is not uploaded.
    private static void Answer(bool allow)
    {
        _consentDialog = null;
        Coordinator.AnswerPrompt(allow);
        if (allow)
        {
            RunRecorder.SkipCurrentRun(RunRecordingGate.ReasonTurnedOn);
            Coordinator.SendInBackground();
        }
        else
        {
            RunRecorder.Discard();
        }
        HBRGOSakiConfig.SaveRunUploadSettings();
    }

    private static void Disable()
    {
        Coordinator.Disable();
        RunRecorder.SkipCurrentRun(RunRecordingGate.ReasonOff);
        DeleteInProgress();
        HBRGOSakiConfig.RefreshRunUploadSettings();
    }

    internal static void CopyInstallId()
    {
        string id = InstallId;
        if (string.IsNullOrEmpty(id))
        {
            NRunUploadDialog.Show(RunUploadText.InstallIdTitle, RunUploadText.NoInstallId, RunUploadText.Ok, null, null, null);
            return;
        }
        DisplayServer.ClipboardSet(id);
        NRunUploadDialog.Show(RunUploadText.InstallIdTitle, RunUploadText.Copied(id), RunUploadText.Ok, null, null, null);
    }

    internal static void ConfirmRegenerateInstallId()
    {
        string current = InstallId;
        if (string.IsNullOrEmpty(current))
        {
            NRunUploadDialog.Show(RunUploadText.RegenerateTitle, RunUploadText.NoInstallId, RunUploadText.Ok, null, null, null);
            return;
        }
        NRunUploadDialog.Show(RunUploadText.RegenerateTitle, RunUploadText.RegenerateConfirm(current),
            RunUploadText.RegenerateAction, RunUploadText.Cancel,
            () =>
            {
                (string old, string next) = Coordinator.RegenerateInstallId();
                if (next == null) return;
                DisplayServer.ClipboardSet(old);
                HBRGOSakiConfig.SaveRunUploadSettings();
                Callable.From(() => NRunUploadDialog.Show(RunUploadText.RegenerateTitle, RunUploadText.Regenerated(old, next),
                    RunUploadText.Ok, null, null, null)).CallDeferred();
            },
            null);
    }

    internal static void ConfirmClearLocalData()
    {
        int queued = Coordinator.Queue.Count;
        bool recording = RunRecorder.IsRecording;
        NRunUploadDialog.Show(RunUploadText.ClearTitle, RunUploadText.ClearConfirm(queued, recording),
            RunUploadText.ClearAction, RunUploadText.Cancel,
            () =>
            {
                int removed = Coordinator.ClearLocalData();
                RunRecorder.SkipCurrentRun(RunRecordingGate.ReasonCleared);
                Callable.From(() => NRunUploadDialog.Show(RunUploadText.ClearTitle, RunUploadText.Cleared(removed),
                    RunUploadText.Ok, null, null, null)).CallDeferred();
            },
            null);
    }

    // ---- records ----------------------------------------------------------------------

    internal static void SubmitFinishedRun(JsonObject run, string historyBuildId)
    {
        RunSubmitResult result = Coordinator.SubmitFinishedRun(run, ModVersion, GameVersion(historyBuildId));
        if (result.Queued) Coordinator.SendInBackground();
        else Log("The finished run is not uploaded: " + result.Reason);
    }

    // The record of the run in progress, so closing the game does not lose it.
    internal static void SaveInProgress(string key, JsonObject run, JsonObject live)
    {
        try
        {
            Coordinator.SaveInProgress(key, run, live);
        }
        catch (Exception exception)
        {
            Log("Saving the run record failed: " + exception.Message);
        }
    }

    internal static void DeleteInProgress()
    {
        try
        {
            Coordinator.DeleteInProgress();
        }
        catch (Exception)
        {
        }
    }

    internal static void Log(string message)
    {
        try
        {
            MegaCrit.Sts2.Core.Logging.Log.Info("[HBRGOSakiMod run upload] " + message);
        }
        catch (Exception)
        {
        }
    }

    // BaseLib applies saved values through these methods; a toggle click never
    // passes through them.
    private static bool IsConfigLoad()
    {
        try
        {
            foreach (StackFrame frame in new StackTrace(1, false).GetFrames())
            {
                MethodBase method = frame?.GetMethod();
                string type = method?.DeclaringType?.FullName;
                if (type == null || !type.StartsWith("BaseLib.Config.", StringComparison.Ordinal)) continue;
                string name = method.Name;
                if (name.StartsWith("Load", StringComparison.Ordinal) || name == "TryApplyPropertyValue" ||
                    name == "Init" || name.StartsWith("RestoreDefaults", StringComparison.Ordinal))
                    return true;
            }
        }
        catch (Exception)
        {
        }
        return false;
    }
}
