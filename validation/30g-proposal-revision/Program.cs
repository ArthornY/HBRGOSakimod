using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

public sealed class ThirtyGRevisionProbe : ThirtyOneBGardenCard
{
    protected override int FlashEffectId => -1;
    public override string Title => "30G next-play probe";
    public static readonly List<CardPlay> Plays = [];
    public static Func<CardPlay, Task>? Body;
    public ThirtyGRevisionProbe() : base(2, CardType.Skill, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { Plays.Add(play); return Body?.Invoke(play) ?? Task.CompletedTask; }
}
static class ThirtyGProposalRevisionStandalone
{ public static void Run(string path) => EffectChecks.RunThirtyGProposalRevision(path); }
static partial class EffectChecks
{
    static object? RToken;
    static int RExternalRepeats;
    static bool RTurnToken(ref object? __result) { __result = RToken; return false; }
    static void RMorePlays(ref int __result) => __result += RExternalRepeats;
    static Task RTurn() => GRelic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player);
    static async Task RInserted() => await (Task)Call("HbrInsertedTurnStartHelper", "Start", new ThrowingPlayerChoiceContext(), Player)!;
    static async Task RPlay(CardModel card, bool automatic = false)
    {
        if (automatic) await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, skipCardPileVisuals: true);
        else
        {
            var (energy, stars) = await card.SpendResources();
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, false, new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars }, true);
        }
    }
    static async Task<CardModel> RProposed(bool miya = false)
    {
        RToken = null; RExternalRepeats = 0; ThirtyGRevisionProbe.Plays.Clear(); ThirtyGRevisionProbe.Body = null;
        GReset(2); CardModel origin = miya ? GDeck[1] : GOrigin<ThirtyGRevisionProbe>();
        var mirror = GMirror(origin, true); GSeedFor(2, san: 25); await GPrepare();
        Check(GPresentations.Count == 0 && !GGraduated(mirror), "combat preparation itself does not propose before the first turn");
        await RTurn();
        Check(GGraduated(mirror) && GPresentations.Count == 1, "first owner turn proposes to the sole eligible physical card");
        return mirror;
    }
    static async Task RNextPlayCases()
    {
        CardModel mirror = await RProposed();
        Equal(mirror.EnergyCost.GetWithModifiers(CostModifiers.All), 1, "native cost hook reduces the next manual play from two energy to one");
        Check(!GRelic.TryModifyEnergyCostInCombat(mirror, 0, out decimal zero) && zero == 0, "the next-play discount never reduces a zero cost below zero");
        Check(mirror.CanPlay(out _, out _), "nullified card remains manually playable through the native ShouldPlay path");
        Check(!Combat.IterateHookListeners().Contains(mirror), "pending nullification suppresses the card's passive listeners");
        int energy = Player.PlayerCombatState!.Energy;
        RExternalRepeats = 2; await RPlay(mirror);
        Check(ThirtyGRevisionProbe.Plays.Count == 0 && mirror.Pile?.Type == PileType.Discard, "native nullified wrapper skips every external replay and still completes normal result-pile handling");
        Equal(Player.PlayerCombatState.Energy, energy - 1, "native manual resource payment spends the discounted amount");
        Check(!GGraduated(mirror) && GHeart(mirror) == 1, "completed nullified wrapper advances to one pending heartfelt play");
        Check(Combat.IterateHookListeners().Contains(mirror), "after nullification the same physical card resumes passive listeners");
        Equal(mirror.EnergyCost.GetWithModifiers(CostModifiers.All), 2, "the one-play discount ends before the heartfelt play");
        await RPlay(mirror);
        Check(ThirtyGRevisionProbe.Plays.Count == 4 && ThirtyGRevisionProbe.Plays.Select(play => play.PlayIndex).SequenceEqual(Enumerable.Range(0, 4)), "next native wrapper adds exactly one heartfelt replay to its base plus two external replays");
        Equal(GHeart(mirror), 0, "one-shot heartfelt bonus is consumed once after the entire multi-play wrapper");
        ThirtyGRevisionProbe.Plays.Clear(); RExternalRepeats = 0; await RPlay(mirror, true);
        Equal(ThirtyGRevisionProbe.Plays.Count, 1, "later native automatic play has returned to ordinary behavior");

        mirror = await RProposed(); await RPlay(mirror, true);
        Check(ThirtyGRevisionProbe.Plays.Count == 0 && GHeart(mirror) == 1, "native automatic play also consumes nullification and advances exactly once");
        await RPlay(mirror, true);
        Check(ThirtyGRevisionProbe.Plays.Count == 2 && ThirtyGRevisionProbe.Plays.All(play => play.IsAutoPlay) && GHeart(mirror) == 0, "next native automatic play receives and consumes exactly one extra play");

        mirror = await RProposed(); await RPlay(mirror, true); RExternalRepeats = 0;
        int depth = 0; ThirtyGRevisionProbe.Body = async play => { if (depth++ == 0) await RPlay(mirror, true); };
        await RPlay(mirror, true); ThirtyGRevisionProbe.Body = null;
        Check(ThirtyGRevisionProbe.Plays.Count == 3 && GHeart(mirror) == 0, "nested automatic play neither borrows nor prematurely clears its outer physical card's one-shot heartfelt bonus");

        mirror = await RProposed(); await RPlay(mirror, true);
        ThirtyGRevisionProbe.Body = _ => throw new InvalidOperationException("controlled-30g-play-failure");
        bool failed = false; try { await RPlay(mirror, true); } catch (InvalidOperationException error) when (error.Message == "controlled-30g-play-failure") { failed = true; }
        ThirtyGRevisionProbe.Body = null;
        Check(failed && GHeart(mirror) == 1, "a failed native effect wrapper retains its unconsumed next-play heartfelt state");
    }
    static async Task RTurnAndIdentityCases()
    {
        CardModel first = await RProposed(); int serial = GPresentations.Count;
        var second = GMirror(GOrigin<ThirtyGRevisionProbe>(), true);
        await RTurn(); Equal(GPresentations.Count, serial, "duplicate standard turn callback cannot propose twice");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); await RTurn();
        Check(GGraduated(second) && GPresentations.Count == 2, "second owner turn proposes again and excludes the still-nullified first card");
        Native(GRelic, "SetSan", 100); var third = GMirror(GOrigin<ThirtyGRevisionProbe>(), true); RToken = new(); await RInserted();
        Check(GGraduated(third) && GPresentations.Count == 3, "shared inserted-turn entry accepts a distinct OD token at the same native turn number");
        await RInserted(); Equal(GPresentations.Count, 3, "the same inserted-turn token is idempotent");
        Native(GRelic, "SetSan", 100); var fourth = GMirror(GOrigin<ThirtyGRevisionProbe>(), true); RToken = new(); await RInserted();
        Check(GGraduated(fourth) && GPresentations.Count == 4, "a distinct personal-turn token independently proposes at the same native turn number");
        RToken = null; await RPlay(first, true); Native(GRelic, "SetSan", 100);
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 3); await RTurn();
        Check(GHeart(first) == 1 && GPresentations.Count == 4, "every nullified or currently heartfelt card is excluded from proposal selection");
        await RPlay(first, true); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 4); await RTurn();
        Check(GGraduated(first) && GPresentations.Count == 5, "a fully consumed ordinary card becomes eligible for a later turn's proposal");

        var physical = first.DeckVersion!; string id = GId(physical);
        var options = new JsonSerializerOptions { IncludeFields = true };
        string saved = JsonSerializer.Serialize(SavedProperties.From(physical), options);
        var copy = (CardModel)physical.MutableClone(); Set(copy, "_owner", Player); copy.DeckVersion = physical;
        JsonSerializer.Deserialize<SavedProperties>(saved, options)!.Fill(copy); Player.Deck.AddInternal(copy, 0);
        Call("ThirtyGProposalState", "EnsureIdentities", Player);
        Check(GId(physical) == id && GId(copy) != id && !GGraduated(copy), "a genuine permanent saved copy receives its own physical ledger without stealing the proposed original's state");
        MethodInfo dupeFactory = typeof(CardModel).GetMethods(F).Single(method => method.Name == nameof(CardModel.CreateDupe));
        var combatCopy = (CardModel)Invoke(dupeFactory, first, dupeFactory.GetParameters().Length == 0 ? [] : [Player])!;
        Set(combatCopy, "_owner", Player); combatCopy.DeckVersion = physical;
        Check(!GGraduated(combatCopy) && GHeart(combatCopy) == 0, "a generated duplicate cannot inherit the original's nullification or heartfelt ledger");
        var odMirror = GMirror(physical);
        Check(GGraduated(odMirror), "a normal OD combat mirror follows the same physical nullification ledger");
    }
    static async Task RPersistentAndMiyaCases()
    {
        CardModel mirror = await RProposed(); CardModel physical = mirror.DeckVersion!;
        Call("ThirtyGBondHelper", "EndBattle", Player); GNextBattle(); mirror = GMirror(physical, true); await GPrepare();
        Check(GGraduated(mirror) && mirror.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "unused next-play nullification and discount persist to the next combat");
        await RPlay(mirror, true); Call("ThirtyGBondHelper", "EndBattle", Player); GNextBattle(); mirror = GMirror(physical); await GPrepare();
        Check(GHeart(mirror) == 1 && mirror.Pile?.Type == PileType.Draw, "unused next-play heartfelt bonus persists without adding the card to hand");
        string saved = JsonSerializer.Serialize(SavedProperties.From(GRelic), new JsonSerializerOptions { IncludeFields = true });
        var restored = (ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable(); Set(restored, "_owner", Player);
        JsonSerializer.Deserialize<SavedProperties>(saved, new JsonSerializerOptions { IncludeFields = true })!.Fill(restored);
        Set(Player, "_relics", Player.Relics.Select(relic => relic == GRelic ? restored : relic).ToList()); GRelic = restored;
        Check(GHeart(mirror) == 1, "native relic saved-property JSON preserves the pending next-play state");

        mirror = await RProposed(true); physical = mirror.DeckVersion!;
        decimal modified; Check(!GRelic.TryModifyEnergyCostInCombat(mirror, 2, out modified), "Miya's whole-combat exception does not acquire the ordinary one-play discount");
        RExternalRepeats = 3; await RPlay(mirror, true); await RPlay(mirror, true);
        Check(GGraduated(mirror) && GHeart(mirror) == 0 && MarriageGardenFixture.Events.All(e => !e.EndsWith(":play")), "Miya stays nullified across multiple native automatic wrappers and all external repeats this combat");
        Call("ThirtyGBondHelper", "EndBattle", Player);
        Check(GHeart(physical) == 1 && (bool)GSnapshot(physical).GetType().GetProperty("PermanentHeartfelt")!.GetValue(GSnapshot(physical))!, "Miya's permanent extra play remains visible on the physical deck card after combat");
        GNextBattle(); mirror = GMirror(physical, true); await GPrepare(); RExternalRepeats = 0;
        Check(!GGraduated(mirror) && GHeart(mirror) == 1, "Miya's battle end replaces full-combat nullification with a permanent one-extra-play bonus");
        MarriageGardenFixture.Events.Clear(); await RPlay(mirror, true); await RPlay(mirror, true);
        Check(MarriageGardenFixture.Events.Count(e => e.EndsWith(":play")) == 4 && GHeart(mirror) == 1, "Miya's permanent bonus applies to every subsequent native automatic wrapper");
        Native(GRelic, "SetSan", 100); await RTurn(); Equal(GPresentations.Count, 1, "permanently strengthened Miya is never proposed to again");
        Call("ThirtyGBondHelper", "EndBattle", Player); GNextBattle(); mirror = GMirror(physical); await GPrepare();
        Check(GHeart(mirror) == 1 && GRelic.ModifyCardPlayCount(mirror, null!, 1) == 2, "Miya's permanent one-extra-play ledger survives another combat boundary");
    }
    static async Task RReactionAndSanCases()
    {
        foreach (int reaction in new[] { 1, 2, 3 })
        {
            RToken = null; RExternalRepeats = 0; ThirtyGRevisionProbe.Plays.Clear();
            GReset(6); GDischarges.Clear(); var mirror = GMirror(GOrigin<ThirtyGRevisionProbe>(), true); GSeedFor(6, reaction, san: 25);
            await GPrepare(); await RTurn();
            Check(GGraduated(mirror) && ((HBRGOSakiMod.Core.Integration.ThirtyGProposalPresentation)GPresentations.Single()).Reaction == reaction,
                "six-member reaction preserves its presentation category without replacing next-play nullification: " + reaction);
            Equal(((ThirtyGCollectionRelic)GRelic).San, 75, "turn proposal retains the independent exact SAN deduction: " + reaction);
            Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); await RTurn();
            Check(GGraduated(mirror), "a later turn never bypasses the pending nullified play, including old early-release reaction: " + reaction);
            await RPlay(mirror, true); Equal(GHeart(mirror), 1, "every reaction advances to exactly one temporary extra play: " + reaction);
            await RPlay(mirror, true); Equal(ThirtyGRevisionProbe.Plays.Count, 2, "old doubled-heart reaction cannot add a second extra replay: " + reaction);
            if (reaction == 2) Check(GDischarges.Single() == (3.06m, 3, .4m), "discharge retains its actual all-enemy three-hit command and destruction coefficient");
            else Equal(GDischarges.Count, 0, "cosmetic reaction does not discharge: " + reaction);
        }
        var first = await RProposed(); Native(GRelic, "SetSan", 0);
        var second = GMirror(GOrigin<ThirtyGRevisionProbe>(), true); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); await RTurn();
        Check(!GGraduated(second) && GPresentations.Count == 1, "broken SAN prevents a later turn from beginning a new proposal");
        GResetSan(); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 3); await RTurn();
        Check(GGraduated(second) && GGraduated(first), "SAN recovery resumes later turn proposals without erasing existing card state");

        GReset(4); var target = GOrigin<NarcissismIsFineHbrgoSaki>(); GMirror(target); GSeedFor(4, san: 25);
        for (int i = 0; i < 9; i++) Native(GRelic, "GainPopularity");
        await GPrepare(); await RTurn();
        Check(target.CurrentUpgradeLevel == 1 && ((ThirtyGCollectionRelic)GRelic).Popularity == 1,
            "the actual tenth turn proposal still resolves the permanent upgrade vote and next-round popularity");
    }
    static async Task RLegacyMigrationCases()
    {
        CardModel mirror = await RProposed(); string id = GId(mirror.DeckVersion!);
        void Legacy(bool released) => GUpdateData(data =>
        {
            data["RulesVersion"] = 0;
            data["Cards"]![id] = new JsonObject { ["GraduatedRemaining"] = 1, ["PendingHeart"] = 2, ["Released"] = released };
        });
        Legacy(false); Check(GGraduated(mirror) && GHeart(mirror) == 0, "old ordinary disabled-encounter save migrates into next-play nullification");
        Legacy(true); Check(!GGraduated(mirror) && GHeart(mirror) == 1, "old early-released save migrates into one pending extra play without re-disabling or doubling");
        mirror = await RProposed(true); id = GId(mirror.DeckVersion!);
        GUpdateData(data => { data["RulesVersion"] = 0; data["Cards"]![id] = new JsonObject { ["GraduatedRemaining"] = 2, ["PendingHeart"] = 2 }; });
        Check(GGraduated(mirror) && (bool)GSnapshot(mirror).GetType().GetProperty("WholeBattleNullified")!.GetValue(GSnapshot(mirror))!, "old Miya two-encounter schedule migrates into the new whole-combat exception");
        Call("ThirtyGBondHelper", "EndBattle", Player); Equal(GHeart(mirror.DeckVersion!), 1, "migrated Miya receives exactly one permanent extra play after that combat");
    }
    public static void RunThirtyGProposalRevision(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try
        {
            Install(); Localization(); var h = new Harmony(ProductionId + ".thirtyGRevision");
            foreach (Type type in GMembers.Concat(new[] { typeof(ThirtyGRevisionProbe), typeof(NarcissismIsFineHbrgoSaki), typeof(AbsolutelyNoEffortHbrgoSaki), typeof(AllOrdersConfirmedHbrgoSaki), typeof(HBRGOSakiMod.Core.Models.Powers.NiinaMakeupPower) }))
            { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
            GSaveType(typeof(ThirtyGRevisionProbe));
            foreach (Type type in new[] { RunState.GetType(), typeof(CombatState) })
                h.Patch(type.GetMethod("IterateHookListeners", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners), F)!) { priority = Priority.First + 100 });
            GInstall(h);
            void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent", F)!, nameof(VisualTask));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (var wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (var add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            Patch(Helper("OverclockImmediateTurnCoordinator").GetMethod("GetTurnToken", F)!, nameof(RTurnToken));
            h.Patch(typeof(Hook).GetMethod(nameof(Hook.ModifyCardPlayCount))!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RMorePlays), F)!));
            RInstallPowerExitChecks(h);
            RNextPlayCases().GetAwaiter().GetResult(); RTurnAndIdentityCases().GetAwaiter().GetResult(); RPersistentAndMiyaCases().GetAwaiter().GetResult(); RReactionAndSanCases().GetAwaiter().GetResult(); RLegacyMigrationCases().GetAwaiter().GetResult();
            RPowerExitCases().GetAwaiter().GetResult(); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CardModel).Assembly.Location))),
            sourceNativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            powerExit = new { expectOldDefect = RExpectOldPowerExit, helperPresent = RPowerExitHelper != null, helperInitialization = Environment.GetEnvironmentVariable("HBR_POWER_EXIT_HELPER_INIT"), cases = RPowerExitResults, guardIl = RPowerExitGuardAudit },
            boundary = "Real proposal helper, native cost/payment/CanPlay/AutoPlay/OnPlayWrapper/GeneratePlayCount, Power RemoveFromCombat and native SavedProperties. For power exit, GetUnclaimedTableCard is an explicit whole UI leaf returning uninitialized NCard tokens; private native flight is a counting/completion leaf. Actual helper guard IL and production fast prefix are inspected/executed separately; no actual scene, tween, renderer or network is exercised. Clock, textures, scene pile movements, waits and shared inserted-turn token leaves controlled. No Godot launch, rendering, in-game session, network or packaging." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " 30G next-play checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
