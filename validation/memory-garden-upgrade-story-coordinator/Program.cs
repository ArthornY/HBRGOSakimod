using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

static class UpgradeStoryStandalone { public static void Run(string path) => EffectChecks.RunUpgradeStories(path); }
static partial class EffectChecks
{
    static readonly Harmony StoryHarmony = new("hbr.upgrade-story.coordinator.native");
    static readonly List<(Player Owner, string Actor, int Level)> StoryPlays = [];
    static readonly List<string> StoryWarnings = [];
    static readonly List<int> StoryEffectIds = [];
    static string StoryDirectory = "", StoryPath = "", StoryResult = "Completed";
    static bool StoryReady = true, StoryHold, StoryAvailable = true, StoryIsLocal = true;
    static object? StoryCompletion;
    static int StoryCaseNumber, StorySyncs;
    static Type StoryCoordinator => Helper("HbrMemoryGardenUpgradeStories");
    static Type StoryPresenter => Helper("HbrMemoryUpgradeStoryPresentation");
    static Type StoryResultType => Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrMemoryUpgradeStoryPlaybackResult", true)!;
    static Type StoryCatalog => Mod.GetType("HBRGOSakiMod.Core.Story.HbrMemoryUpgradeStoryCatalog", true)!;
    static bool StoryLocal(Player player, ref bool __result) { __result = StoryIsLocal && player == Player; return false; }
    static bool StoryReserve(ref uint __result) { __result = (uint)(StorySyncs + 1); return false; }
    static bool StorySync() { StorySyncs++; return false; }
    static bool StoryRemote(ref Task<PlayerChoiceResult> __result) { __result = Task.FromResult(PlayerChoiceResult.FromIndex(StoryEffectIds.Last())); return false; }
    static bool StoryUiReady(ref bool __result) { __result = StoryReady; return false; }
    static bool StoryRead(string path, ref string? __result)
    {
        string actor = Path.GetFileNameWithoutExtension(path);
        __result = StoryAvailable && actor == "isuzu" ? JsonSerializer.Serialize(new
        {
            schemaVersion = 1, characterId = actor,
            presentation = new { background = "res://images/events/hbrgo_saki_mod_event_library_hbrgo_saki.png" },
            segments = Enumerable.Range(1, 7).Select(level => new { level, perspectives = new[] { "31o" }, lines = new[] { new { kind = "narration", text = "TEST ONLY " + level } } })
        }) : null;
        return false;
    }
    static bool StoryPlay(Player owner, string actorId, int level, ref object __result)
    {
        StoryPlays.Add((owner, actorId, level));
        if (StoryHold)
        {
            StoryCompletion = Activator.CreateInstance(typeof(TaskCompletionSource<>).MakeGenericType(StoryResultType), TaskCreationOptions.RunContinuationsAsynchronously)!;
            __result = StoryCompletion.GetType().GetProperty("Task")!.GetValue(StoryCompletion)!;
        }
        else __result = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(StoryResultType)
            .Invoke(null, [Enum.Parse(StoryResultType, StoryResult)])!;
        return false;
    }
    static void StoryObserveOffers(object __result)
    {
        object? first = ((IEnumerable)__result).Cast<object>().FirstOrDefault();
        if (first != null) StoryEffectIds.Add((int)first.GetType().GetProperty("EffectId")!.GetValue(first)!);
    }
    static void StoryWake() => (StoryPresenter.GetField("AvailabilityChanged", F)!.GetValue(null) as Action)?.Invoke();
    static void StoryClear() => Invoke(StoryCoordinator.GetMethod("ClearPending", F)!, null);
    static void StoryStore(string path)
    {
        Type file = Mod.GetType("HBRGOSakiMod.Core.Story.HbrMemoryGardenUpgradeStoryLocalFile", true)!;
        object value = file.GetConstructors(F).Single().Invoke([path, (Action<string>)StoryWarnings.Add]);
        typeof(HbrMemoryGardenUpgradeStoryLocalStore).GetField("_file", F)!.SetValue(null, value);
    }
    static void StoryReset(bool freshFile = true)
    {
        if (Player.PlayerCombatState == null)
        {
            var combat = Blank<PlayerCombatState>(); Set(combat, "<TurnNumber>k__BackingField", 1);
            Set(Player, "<PlayerCombatState>k__BackingField", combat);
        }
        FormReset(); CombatInProgress(false); StoryClear(); StoryPlays.Clear(); StoryWarnings.Clear(); StoryEffectIds.Clear();
        StoryReady = StoryAvailable = StoryIsLocal = true; StoryHold = false; StoryCompletion = null; StoryResult = "Completed"; StorySyncs = 0;
        Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki31O>()); Set(Player, "<NetId>k__BackingField", 71UL);
        Set(Hero, "_currentHp", 5000); Set(RunState, "_allCards", new List<CardModel>());
        Set(RunState, "_currentRooms", new List<AbstractRoom> { Blank<RestSiteRoom>() });
        typeof(RunManager).GetProperty("State", F)!.SetValue(RunManager.Instance, RunState);
        Set(RunManager.Instance, "_startTime", (long)100000 + ++StoryCaseNumber);
        Set(RunManager.Instance, "<NetService>k__BackingField", Blank<NetSingleplayerGameService>());
        Set(RunManager.Instance, "<PlayerChoiceSynchronizer>k__BackingField", Blank<PlayerChoiceSynchronizer>());
        ConstructorInfo rng = typeof(PlayerRngSet).GetConstructors().Single(ctor => ctor.GetParameters().Length == 1);
        Set(Player, "<PlayerRng>k__BackingField", rng.Invoke([Convert.ChangeType(20261116, rng.GetParameters()[0].ParameterType)]));
        Native(Player.GetRelic<ElectronicSoldierManual>()!, "SetMemoryGardenSharedUpgrades", "");
        HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback = HbrMemoryGardenStoryPlayback.All;
        StoryCatalog.GetField("Entries", F)!.GetValue(null)!.GetType().GetMethod("Clear")!.Invoke(StoryCatalog.GetField("Entries", F)!.GetValue(null), null);
        if (freshFile) StoryPath = Path.Combine(StoryDirectory, "local-" + StoryCaseNumber + ".json");
        StoryStore(StoryPath);
    }
    static T StoryCard<T>() where T : CardModel
    {
        T card = (T)ModelDb.Card<T>().ToMutable(); Set(card, "_owner", Player); Player.Deck.AddInternal(card);
        ((List<CardModel>)typeof(RunState).GetField("_allCards", F)!.GetValue(RunState)!).Add(card);
        return card;
    }
    static Task<bool> StoryChoose(CardModel card, string source = "upgradePointNotice", Func<bool>? allowed = null) =>
        (Task<bool>)Call("HbrMemoryGarden", "ChooseUpgrade", card, allowed ?? (() => true), source, null)!;
    static void StoryNotify(CardModel card, int previous) => Invoke(StoryCoordinator.GetMethod("OnUpgradeCommitted", F)!, null, card, previous);
    static bool StoryRunning()
    {
        object states = StoryCoordinator.GetField("States", F)!.GetValue(null)!; object?[] args = [Player, null];
        return (bool)states.GetType().GetMethod("TryGetValue")!.Invoke(states, args)! && (bool)args[1]!.GetType().GetField("Running", F)!.GetValue(args[1])!;
    }
    static async Task StorySettled(int? count = null)
    {
        for (int tick = 0; tick < 2000; tick++)
        {
            if (!StoryRunning() && (count == null || StoryPlays.Count == count)) return;
            await Task.Delay(1);
        }
        throw new Exception("Story queue did not settle; count=" + StoryPlays.Count + " expected=" + count);
    }
    static string StoryRunKey() { Check(HbrMemoryGardenUpgradeStoryLocalStore.TryGetRunKey(Player, out string key), "native stable run identity exists"); return key; }
    static async Task StoryLevelCases()
    {
        StoryReset(); var card = StoryCard<OpenTheLockHbrgoSaki>(); StoryNotify(card, -1); await StorySettled(0);
        Check(!File.Exists(StoryPath), "initial level0 acquisition creates no playback or local receipt");
        for (int level = 1; level <= 7; level++)
        {
            Check(await StoryChoose(card), "actual synchronized common upgrade succeeds at level" + level); await StorySettled(level);
            Check(card.CurrentUpgradeLevel == level && StoryPlays.Last().Level == level, "story uses actual acquired level rather than effect ID: " + level);
        }
        Check(StoryEffectIds.Where((effect, index) => effect != index).Any(), "real native reward RNG offers a different effect order from story segment order");
        var copy = StoryCard<OpenTheLockHbrgoSaki>(); Invoke(Helper("HbrMemoryGardenProgression").GetMethod("Synchronize", F, [typeof(CardModel)])!, null, copy);
        Check(copy.CurrentUpgradeLevel == 7, "real shared progression synchronizes the second physical copy");
        StoryNotify(copy, 6); await StorySettled(7); Check(StoryPlays.Count == 7, "another physical copy cannot replay the same actor and level");
        StoryClear(); StoryStore(StoryPath); StoryNotify(copy, 6); await StorySettled(7);
        Check(StoryPlays.Count == 7 && HbrMemoryGardenUpgradeStoryLocalStore.HasStarted(StoryRunKey(), "31o", "isuzu", 7), "cleared in-memory queue and reloaded local JSON retain run-level playback receipt");
        int before = card.CurrentUpgradeLevel; Check(!await StoryChoose(card), "max-level upgrade decline remains false"); await StorySettled(7);
        Check(card.CurrentUpgradeLevel == before, "story framework never changes a declined upgrade");
    }
    static async Task StoryFilterCases()
    {
        StoryReset(); var card = StoryCard<OpenTheLockHbrgoSaki>(); HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback = HbrMemoryGardenStoryPlayback.Off;
        Check(await StoryChoose(card), "Off leaves real upgrade successful"); await StorySettled(0);
        Check(!File.Exists(StoryPath), "Off creates neither Started nor Seen record");
        HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback = HbrMemoryGardenStoryPlayback.All; StoryNotify(card, 0); await StorySettled(1);
        Check(StoryPlays.Count == 1, "turning back on can show an unstarted same-level milestone");
        string saved = StoryPath; StoryReset(false); HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback = HbrMemoryGardenStoryPlayback.UnseenOnly;
        card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "UnseenOnly leaves new-run upgrade successful"); await StorySettled(0);
        Check(!HbrMemoryGardenUpgradeStoryLocalStore.HasStarted(StoryRunKey(), "31o", "isuzu", 1), "previously seen text is skipped without consuming this run's Started receipt");
        HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback = HbrMemoryGardenStoryPlayback.All; StoryNotify(card, 0); await StorySettled(1);
        Check(StoryPlays.Count == 1 && StoryPath == saved, "All replays text in a new run despite cross-run Seen");
        StoryReset(); StoryAvailable = false; card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "missing-text upgrade still commits"); await StorySettled(0);
        Check(!File.Exists(StoryPath) && StoryWarnings.Count == 0, "missing actor file is a quiet no-op without local receipts");
        StoryReset(); Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki30A>()); card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "other viewpoint upgrade still commits"); await StorySettled(0);
        Check(!File.Exists(StoryPath), "missing 30A viewpoint neither displays 31O text nor claims it");
        StoryReset(); StoryIsLocal = false; card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "remote owner's replayed choice still applies its actual native upgrade"); await StorySettled(0);
        Check(card.CurrentUpgradeLevel == 1 && StorySyncs == 0 && !File.Exists(StoryPath), "another owner causes no local reader, local receipt or additional choice sync");
        StoryReset(); card = StoryCard<OpenTheLockHbrgoSaki>(); Check(!await StoryChoose(card, allowed: () => false), "failed upgrade precondition returns without a presentation"); await StorySettled(0);
        Check(card.CurrentUpgradeLevel == 0 && !File.Exists(StoryPath), "failed upgrade does not change card or claim a chapter");
    }
    static async Task StoryQueueCases()
    {
        StoryReset(); StoryReady = false; var card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "blocked UI does not block a successful native upgrade"); await StorySettled(0);
        Check(!File.Exists(StoryPath), "a queued but not displayed memory is not marked Started");
        StoryReady = true; StoryWake(); await StorySettled(1); Check(StoryPlays.Count == 1, "real availability event resumes an idle local queue");
        StoryReset(); StoryReady = false; card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "UI-blocked upgrade can leave a pending local chapter"); await StorySettled(0);
        Set(RunState, "_currentRooms", new List<AbstractRoom> { Blank<RestSiteRoom>() }); StoryReady = true; StoryWake(); await StorySettled(0);
        Check(!File.Exists(StoryPath), "changing the logical room before playback discards its stale chapter without claiming it");
        StoryReset(); StoryReady = false; card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "pre-load pending chapter leaves upgrade intact"); await StorySettled(0);
        StoryClear(); StoryStore(StoryPath); StoryReady = true; StoryWake(); await StorySettled(0);
        Check(!File.Exists(StoryPath), "run/load queue cleanup does not catch up an unstarted chapter");
        StoryReset(); StoryHold = true; card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "synchronized upgrade returns before its reader finishes");
        for (int tick = 0; StoryPlays.Count == 0 && tick < 2000; tick++) await Task.Delay(1);
        Check(StoryCompletion != null && StoryRunning(), "local reader can remain pending independently of the completed upgrade task");
        string runKey = StoryRunKey(); Check(HbrMemoryGardenUpgradeStoryLocalStore.HasStarted(runKey, "31o", "isuzu", 1) && !HbrMemoryGardenUpgradeStoryLocalStore.HasSeen("31o", "isuzu", 1), "Started persists before playback but Seen waits for completion");
        StoryClear(); StoryCompletion!.GetType().GetMethod("SetResult")!.Invoke(StoryCompletion, [Enum.Parse(StoryResultType, "Completed")]); await Task.Delay(10);
        Check(!HbrMemoryGardenUpgradeStoryLocalStore.HasSeen("31o", "isuzu", 1), "stale completion after run/load cleanup cannot write Seen");
        foreach (string result in new[] { "Skipped", "Abandoned" })
        {
            StoryReset(); StoryResult = result; card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card), "upgrade unaffected by local result " + result); await StorySettled(1);
            Check(HbrMemoryGardenUpgradeStoryLocalStore.HasStarted(StoryRunKey(), "31o", "isuzu", 1), result + " preserves run Started");
            Check(HbrMemoryGardenUpgradeStoryLocalStore.HasSeen("31o", "isuzu", 1) == (result == "Skipped"), "only Completed/Skipped marks cross-run Seen: " + result);
        }
        StoryReset(); var room = Blank<CombatRoom>(); Set(room, "<CombatState>k__BackingField", Combat); Set(RunState, "_currentRooms", new List<AbstractRoom> { room }); CombatInProgressWithTurn();
        card = StoryCard<OpenTheLockHbrgoSaki>(); Check(await StoryChoose(card, "marriageVoteNotice"), "actual upgrade can commit during combat"); await StorySettled(0); StoryNotify(card, 0); await StorySettled(0);
        Check(!File.Exists(StoryPath), "combat defers and in-memory-deduplicates a chapter without claiming playback");
        CombatInProgress(false); (typeof(CombatManager).GetField("CombatEnded", F)!.GetValue(CombatManager.Instance) as Action<CombatRoom>)!(room); await StorySettled(1);
        Check(StoryPlays.Count == 1, "actual registered native combat-ended callback releases the matching battle once");
        card = StoryCard<OpenTheLockHbrgoSaki>(); Invoke(Helper("HbrMemoryGardenProgression").GetMethod("Synchronize", F, [typeof(CardModel)])!, null, card); StoryNotify(card, 0); await StorySettled(1);
        Check(StoryPlays.Count == 1, "post-combat CombatRoom does not create another pending-battle playback");
    }
    static void StoryActorAndSourceCases()
    {
        Array rows = (Array)Helper("HbrMemoryGarden").GetField("Characters", F)!.GetValue(null)!;
        foreach (object row in rows)
        {
            Type type = (Type)row.GetType().GetProperty("CardType")!.GetValue(row)!; string id = (string)row.GetType().GetProperty("Id")!.GetValue(row)!;
            CardModel sample = (CardModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            object?[] args = [sample, null]; Check((bool)Call("HbrMemoryGarden", "TryGetUpgradeStoryActor", args)! && (string)args[1]! == id, "authoritative actor mapping: " + id);
        }
        foreach ((string helper, string method) in new[] { ("HbrMemoryGarden", "ClaimWithUpgradePoint"), ("HbrMemoryGarden", "ClaimRepeatMemoryRewards"), ("ThirtyACollectionUpgrades", "Resolve"), ("ThirtyGBondHelper", "ResolveVote") })
        {
            MethodInfo entry = Helper(helper).GetMethod(method, F)!;
            Type machine = entry.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
            Check(PatchProcessor.GetOriginalInstructions(machine.GetMethod("MoveNext", F)!).Any(instruction => instruction.operand is MethodInfo call && call.Name == "ChooseUpgrade"), "actual source calls the common upgrade protocol: " + helper + "." + method);
        }
    }
    public static void RunUpgradeStories(string path)
    {
        bool ok = false; string? error = null;
        try
        {
            H("Setup"); Localization(); NonCriticalSeed = "UPGRADE-STORY-100"; StoryDirectory = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-local-receipts"); Directory.CreateDirectory(StoryDirectory);
            foreach (Type type in new[] { typeof(OpenTheLockHbrgoSaki), typeof(HerSongIsMyScreamHbrgoSaki) }) if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            foreach (Type type in Mod.GetTypes().Where(type => !type.IsAbstract && typeof(PowerModel).IsAssignableFrom(type)))
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            foreach (MethodInfo method in typeof(PlayerChoiceSynchronizer).GetMethods(F).Where(method => method.Name == "ReserveChoiceId")) StoryHarmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryReserve), F)!));
            foreach (MethodInfo method in typeof(PlayerChoiceSynchronizer).GetMethods(F).Where(method => method.Name == "SyncLocalChoice")) StoryHarmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StorySync), F)!));
            foreach (MethodInfo method in typeof(PlayerChoiceSynchronizer).GetMethods(F).Where(method => method.Name == "WaitForRemoteChoice")) StoryHarmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryRemote), F)!));
            StoryHarmony.Patch(typeof(LocalContext).GetMethod(nameof(LocalContext.IsMe), [typeof(Player)])!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryLocal), F)!));
            StoryHarmony.Patch(StoryCatalog.GetMethod("ReadText", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryRead), F)!));
            StoryHarmony.Patch(StoryPresenter.GetMethod("CanPlayLocal", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryUiReady), F)!));
            StoryHarmony.Patch(StoryPresenter.GetMethod("PlayLocalAsync", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryPlay), F)!));
            StoryHarmony.Patch(Helper("HbrMemoryGardenUpgradeCatalog").GetMethod("RollOffers", F)!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoryObserveOffers), F)!));
            foreach (MethodInfo method in typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!.GetMethods(F).Where(method => method.Name is "GetPrefix" or "GetPath" && method.ReturnType == typeof(string)))
                StoryHarmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Energy), F)!));
            StoryHarmony.CreateClassProcessor(Mod.GetTypes().Single(type => type.Name == "HbrMemoryGardenUpgradeCommandPatch")).Patch();
            StoryActorAndSourceCases(); StoryLevelCases().GetAwaiter().GetResult(); StoryFilterCases().GetAwaiter().GetResult(); StoryQueueCases().GetAwaiter().GetResult(); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real common garden ChooseUpgrade/offer RNG/native Upgrade/shared progression and native CombatEnded subscription; actual local JSON receipts and stable native run key. Choice network terminals, resource-read input, UI readiness and reader result are controlled. Four source routes are checked at their real native callsites, not all four full UI flows. No Godot rendering or live multiplayer." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " upgrade-story coordinator checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
