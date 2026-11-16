using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

static class RepeatStandalone { public static void Run(string path) => StoryChecks.RunRepeatEligibility(path); }
static partial class StoryChecks
{
    static void RepeatHistory(bool previous = true)
    {
        MapPointHistoryEntry Entry() => new(default, RunState) { Rooms = new List<MapPointRoomHistoryEntry> { new() { RoomType = RoomType.Event, ModelId = Canonical.Id } } };
        Set(RunState, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { previous ? new() { Entry(), Entry() } : new() { Entry() } });
    }
    static void RepeatBegin(string knownA = "", string knownB = "", bool previous = true)
    {
        Reset(); RepeatHistory(previous);
        Call(ManualA, "SetMemoryGardenEncounters", knownA); Call(ManualB, "SetMemoryGardenEncounters", knownB);
        Call(ManualA, "SetMemoryGardenRepeatRewardClaims", ""); Call(ManualB, "SetMemoryGardenRepeatRewardClaims", "");
        Set(A, "<Deck>k__BackingField", new CardPile(PileType.Deck)); Set(B, "<Deck>k__BackingField", new CardPile(PileType.Deck));
        RestartRepeat();
    }
    static void RestartRepeat()
    {
        StoryA = (HbrStoryEvent)Canonical.ToMutable(); StoryB = (HbrStoryEvent)Canonical.ToMutable();
        Begin(StoryA, A); Begin(StoryB, B); Set(Synchronizer, "_events", new List<EventModel> { StoryA, StoryB });
    }
    static bool Pending(Player player, HbrStoryEvent story, params string[] ids) => (bool)Helper("HasRepeatMemoryRewards", player, story, ids)!;
    static void CompleteTaki(Player player)
    {
        Receive(player, 0); Receive(player, 0, awaitPending: false);
        // Native AwaitPendingOptionTasks intentionally catches faults. Observe
        // the actual tasks before its cleanup so fixture faults cannot pass as
        // a completed reward/page transition.
        foreach (Task task in (List<Task>)Field(SyncType, "_pendingOptionTasks").GetValue(Synchronizer)!) task.GetAwaiter().GetResult();
        Await();
    }
    static void RepeatNativeCases()
    {
        RepeatBegin(); string snapshot = ManualA.MemoryGardenRepeatRewardClaims;
        Check(snapshot.EndsWith("@met:", StringComparison.Ordinal), "empty entry knowledge is an explicit saved receipt, not a missing snapshot");
        CompleteTaki(A);
        Check(ManualA.MemoryGardenMetCharacters.Split(',').Contains("taki"), "real native personal choice records newly encountered Taki");
        Check(StoryA.IsFinished && StoryA.CurrentStoryPageId == "SCENE", "first encounter on a repeated GYM finishes without repeat-card step: " + JsonSerializer.Serialize(new { StoryA.IsFinished, StoryA.CurrentStoryPageId, options = StoryA.CurrentOptions.Count, pending = Pending(A, StoryA, "taki"), snapshot, current = ManualA.MemoryGardenRepeatRewardClaims, visit = Helper("RepeatVisitKey", A, StoryA) }));
        Check(!Pending(A, StoryA, "taki"), "newly encountered character is ineligible despite now being in encounter storage");
        Equal(Offers.Count, 1, "first encounter still retains the original three-card reward offer");
        Check(ManualA.MemoryGardenRepeatRewardClaims == snapshot && ManualB.MemoryGardenMetCharacters == "", "page transitions preserve entry snapshot and independent teammate knowledge");
        Equal(ManualA.LeadershipCharm, 8, "first encounter still applies its original sense-eight reward");

        RepeatBegin("taki", "karen"); CompleteTaki(A); CompleteTaki(B);
        Check(!StoryA.IsFinished && StoryA.CurrentOptions.Count == 1, "previously known Taki receives one repeat step through the actual native event route");
        Check(A.Deck.Cards.Count == 0 && Pending(A, StoryA, "taki"), "previously encountered but never purchased character remains eligible for free acquisition");
        Check(StoryB.IsFinished && !Pending(B, StoryB, "taki"), "other owner's unrelated encounter does not authorize newly encountered Taki");
        Check(Offers.Count == 2 && Offers.All(offer => offer.Rewards.Length == 1), "repeat eligibility does not duplicate either original card-choice reward");
        string entry = ManualA.MemoryGardenRepeatRewardClaims;
        Helper("CaptureRepeatMemoryEligibility", A, StoryA); Helper("CaptureRepeatMemoryEligibility", A, StoryA);
        Check(entry == ManualA.MemoryGardenRepeatRewardClaims, "repeated capture is idempotent after later pages");
        string visit = (string)Helper("RepeatVisitKey", A, StoryA)!;
        Call(ManualA, "SetMemoryGardenRepeatRewardClaims", entry + "|" + visit + "taki");
        Check(!Pending(A, StoryA, "taki") && !Pending(B, StoryB, "taki"), "successful visit receipt suppresses only its owner's repeated grant");

        RepeatBegin("taki", previous: false); CompleteTaki(A);
        Check(StoryA.IsFinished && !Pending(A, StoryA, "taki"), "current history entry alone never counts as a previous event visit");
    }
    static void RepeatSaveCases()
    {
        RepeatBegin("karen"); CompleteTaki(A);
        string entry = ManualA.MemoryGardenRepeatRewardClaims;
        var options = new JsonSerializerOptions { IncludeFields = true };
        string save = JsonSerializer.Serialize(SavedProperties.From(ManualA), options);
        var restored = new ElectronicSoldierManual(); Set(restored, "<IsMutable>k__BackingField", true); Set(restored, "_owner", A); Set(restored, "<Id>k__BackingField", ModelDb.GetId(typeof(ElectronicSoldierManual)));
        JsonSerializer.Deserialize<SavedProperties>(save, options)!.Fill(restored);
        Check(restored.MemoryGardenRepeatRewardClaims == entry && restored.MemoryGardenMetCharacters == ManualA.MemoryGardenMetCharacters, "actual native SavedProperties JSON round-trip retains entry knowledge and newly met characters separately");
        ManualA = restored; Set(A, "_relics", new List<RelicModel> { ManualA }); RestartRepeat(); CompleteTaki(A);
        Check(StoryA.IsFinished && !Pending(A, StoryA, "taki"), "same-node reload does not promote its first encounter to a repeat-card reward");
        Check(ManualA.MemoryGardenRepeatRewardClaims == entry, "reload does not overwrite saved entry snapshot with post-reward knowledge");
        Set(RunState, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new() { new(default, RunState) { Rooms = new List<MapPointRoomHistoryEntry> { new() { RoomType = RoomType.Event, ModelId = Canonical.Id } } }, new(default, RunState), new(default, RunState) { Rooms = new List<MapPointRoomHistoryEntry> { new() { RoomType = RoomType.Event, ModelId = Canonical.Id } } } } });
        RestartRepeat(); CompleteTaki(A);
        Check(!StoryA.IsFinished && Pending(A, StoryA, "taki"), "later distinct node correctly recognizes a character encountered in the earlier node");
    }
    static void RepeatSharedCases()
    {
        // The six current memory JSONs are personal. Exercise the production
        // shared predicate with a valid extra combat choice that is never taken.
        string path = "res://story/events/gym_hbrgo_saki.json";
        object store = Mod.GetType("HBRGOSakiMod.Core.Story.HbrStoryDefinitionStore", true)!.GetField("Definitions", F)!.GetValue(null)!;
        PropertyInfo item = store.GetType().GetProperty("Item")!; object original = item.GetValue(store, [path])!;
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "story/events/gym_hbrgo_saki.json")))!;
        json["pages"]![0]!["choices"]![2]!["combat"] = "nararaShibaBoss";
        var shared = HbrStoryDefinition.Parse(json.ToJsonString(), path);
        try
        {
            item.SetValue(store, shared, [path]); RepeatBegin("taki", "karen");
            Check(StoryA.IsShared && StoryB.IsShared, "actual combat-choice predicate enables shared fixture without patching IsShared");
            Check(ManualA.MemoryGardenRepeatRewardClaims.Contains("@met:taki") && ManualB.MemoryGardenRepeatRewardClaims.Contains("@met:karen"), "shared entry snapshots each participant's own pre-node knowledge");
            Helper("RecordEncounters", A, new[] { "taki" }); Helper("RecordEncounters", B, new[] { "taki" });
            Check(Pending(A, StoryA, "taki") && !Pending(B, StoryB, "taki"), "shared encounter only authorizes the owner who already knew this character");
            Check((bool)Helper("NeedsRepeatMemoryRewardStep", B, StoryB, new[] { "taki" })!, "shared options remain synchronized while another owner has an eligible reward");
            string visit = (string)Helper("RepeatVisitKey", A, StoryA)!;
            Call(ManualA, "SetMemoryGardenRepeatRewardClaims", ManualA.MemoryGardenRepeatRewardClaims + "|" + visit + "taki");
            Check(!(bool)Helper("NeedsRepeatMemoryRewardStep", B, StoryB, new[] { "taki" })!, "shared step closes when all actually eligible owner receipts are complete");
        }
        finally { item.SetValue(store, original, [path]); }
    }
    static bool RepeatGoldListeners(ref IEnumerable<AbstractModel> __result) { __result = new AbstractModel[] { ManualA, ManualB }; return false; }
    static void RepeatGoldCases()
    {
        RepeatBegin(); var harmony = new Harmony("hbr.memory-repeat.gold-listener-fixture");
        MethodInfo listener = RunState.GetType().GetMethod("IterateHookListeners", F)!;
        harmony.Patch(listener, prefix: new HarmonyMethod(typeof(StoryChecks).GetMethod(nameof(RepeatGoldListeners), F)!));
        try
        {
            MethodInfo gold = typeof(Hook).GetMethods(F).Single(method => method.Name == "ModifyGoldGained");
            decimal Gold(Player player) => (decimal)Invoke(gold, null, RunState, null, 100m, player, null)!;
            typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(ManualA, 0);
            Check(Gold(A) == 100m, "real native gold hook with Talent zero keeps100");
            typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(ManualA, 99);
            Check(Gold(A) == 100m, "real native gold hook with Talent99 still keeps100");
            var supplier = (EldestOshimaIsHereHbrgoSaki)ModelDb.Card<EldestOshimaIsHereHbrgoSaki>().ToMutable(); Set(supplier, "_owner", A);
            typeof(ThirtyOneBGardenCard).GetProperty("UpgradeEffectOrder", F)!.SetValue(supplier, new[] { 5 }); Set(supplier, "_currentUpgradeLevel", 1); A.Deck.AddInternal(supplier);
            Check(Gold(A) == 115m, "real native gold hook adds military pay15 once without Talent amplification");
            Check(Gold(B) == 100m && ManualA.ModifyGoldGained(B, 100m) == 100m, "permanent military pay and manual gold hook remain owner-isolated");
            A.Deck.RemoveInternal(supplier, true);
            Check(Gold(A) == 100m, "real permanent deck removal invalidates military pay without a stale bonus");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
    public static void RunRepeatEligibility(string path)
    {
        bool ok = false; string? error = null;
        try
        {
            Install();
            foreach (Type type in new[] { typeof(StrikeHbrgoSaki), typeof(DefendHbrgoSaki), typeof(MagicSwordLuckySevenHbrgoSaki), typeof(ChihayaClassmateHbrgoSaki), typeof(EldestOshimaIsHereHbrgoSaki) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            if (Native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } legacy)
                legacy.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [typeof(ElectronicSoldierManual)]);
            else
            {
                Type cache = Native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
                cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [typeof(ElectronicSoldierManual)]);
                cache.GetField("_initialized", F)!.SetValue(null, true);
            }
            RepeatNativeCases(); RepeatSaveCases(); RepeatSharedCases(); RepeatGoldCases(); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Native.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual native EventModel.ToMutable/BeginEvent/received personal-choice handler/Chosen and pending task drain, production parsed memory JSON/pages/reward qualifications, real SavedProperties.From/JSON/Fill, native gold hook/manual/permanent deck mutation. Shared fixture adds one valid untaken combat choice to the definition cache to exercise its real IsShared predicate. OfferCustom UI captures original three-card rewards; repeat-card claiming/selecting/add-to-deck UI and complete run save, transport, Godot renderer are not executed. No RNG, source, delivered-file or installed-mod changes." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " memory repeat eligibility checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
