using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;

public sealed class LitaProbeCard : ThirtyOneBGardenCard
{
    protected override int FlashEffectId => -1;
    public static Func<CardPlay, Task>? Body;
    public override string Title => "Lita native probe";
    public LitaProbeCard() : base(1, CardType.Skill, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Body?.Invoke(play) ?? Task.CompletedTask;
}
static class ChieStandalone { public static void Run(string path) => EffectChecks.RunChie(path); }
static partial class EffectChecks
{
    static ThirtyGCollectionRelic ChieRelic = null!;
    static CuteIsInvincibleHbrgoSaki ChieOrigin = null!;
    static readonly List<CardPlay> ChiePlays = [];
    static readonly List<CardModel[]> ChieSelections = [];
    static readonly List<CardModel> ChieNotifies = [];
    static readonly List<bool> ChieComboAtBody = [];
    static CardModel? ChieSelected;
    static int ChieDepth;
    static TaskCompletionSource? ChieCountWait;
    static bool ChieCountWaitTaken;
    static bool ChieCountPause(AbstractModel __instance, ref Task __result)
    {
        if (ChieCountWait == null || ChieCountWaitTaken || !ReferenceEquals(__instance, ChieRelic)) return true;
        ChieCountWaitTaken = true; __result = ChieCountWait.Task; return false;
    }
    static string ChieId(CardModel card) => (string)Call("ThirtyGProposalState", "IdentityOf", card)!;
    static JsonObject ChieData() => JsonNode.Parse(ChieRelic.ProposalData)!.AsObject();
    static int ChieSerial(string field) => ChieData()[field]!.GetValue<int>();
    static void ChieEdit(Action<JsonObject> edit)
    { JsonObject data = ChieData(); edit(data); Native(ChieRelic, "SetProposalData", data.ToJsonString()); }
    static void ChieSet(CardModel card, int lita, bool dressed = false, int heart = 0, bool pride = false, bool combo = false, bool graduated = false)
    {
        string id = ChieId(card);
        ChieEdit(data => data["Cards"]![id] = new JsonObject { ["Lita"] = lita, ["Dressed"] = dressed,
            ["Heart"] = heart, ["DressPride"] = pride, ["ComboPending"] = combo, ["GraduatedRemaining"] = graduated ? 1 : 0 });
    }
    static T ChiePhysical<T>(params int[] upgrades) where T : CardModel, new()
    { var card = GardenCard<T>(upgrades); GardenPermanent(card); return card; }
    static T ChieMirror<T>(T physical, PileType pile = PileType.Hand) where T : CardModel
    {
        var clone = (T)physical.MutableClone(); Set(clone, "_owner", Player); clone.DeckVersion = physical;
        (pile == PileType.Draw ? Player.PlayerCombatState!.DrawPile : Player.PlayerCombatState!.Hand).AddInternal(clone);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat)!).Add(clone); return clone;
    }
    static void ChieReset(params int[] upgrades)
    {
        GardenReset(); ChiePlays.Clear(); ChieSelections.Clear(); ChieComboAtBody.Clear(); ChieSelected = null; ChieDepth = 0;
        Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        Set(Player.PlayerCombatState!, "_energy", 20); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        ChieRelic = (ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().MutableClone(); Set(ChieRelic, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Append(ChieRelic).ToList());
        ChieOrigin = ChiePhysical<CuteIsInvincibleHbrgoSaki>(upgrades);
        Native(ChieRelic, "SetProposalData", "{\"InBattle\":true}");
        Call("ChieLitaState", "OnPermanentDeckChanged", Player);
        LitaProbeCard.Body = play => { ChiePlays.Add(play); ChieComboAtBody.Add(Hero.GetPower<HbrComboMediumPower>() != null); return Task.CompletedTask; };
    }
    static LitaProbeCard ChieTarget(int lita, PileType pile = PileType.Hand)
    {
        var physical = ChiePhysical<LitaProbeCard>(); Call("ChieLitaState", "OnPermanentDeckChanged", Player);
        ChieSet(physical, lita); return ChieMirror(physical, pile);
    }
    static async Task ChieBody(CuteIsInvincibleHbrgoSaki card)
    { await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), Play(card))!; }
    static bool ChieGrid(object[] __args, ref Task<IEnumerable<CardModel>> __result)
    {
        var choices = __args.OfType<IEnumerable<CardModel>>().Single().ToArray(); ChieSelections.Add(choices);
        __result = Task.FromResult<IEnumerable<CardModel>>(new[] { ChieSelected ?? choices.First() }); return false;
    }
    static decimal ChieFaith() => (decimal)(System.Numerics.BigInteger)Call("HbrFaithResource", "Get", Hero)!;
    static void ChieRegisterSave(Type type)
    {
        Assembly native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } legacy) legacy.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [type]);
        else { Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!; cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]); cache.GetField("_initialized", F)!.SetValue(null, true); }
    }
    static SavedProperties ChieSave(AbstractModel model)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        return JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(model), options), options)!;
    }
    static void ChieAssignmentCases()
    {
        ChieReset(); var first = ChieTarget(67); var second = ChieTarget(23);
        Check(ChieLitaState.Get(ChieOrigin).Lita is >= 1 and <= 100, "77 first acquisition assigns the permanent Chie card itself");
        Equal(ChieSerial("LitaSerial"), 3, "77 each physical garden card is independently assigned exactly once");
        int niche = RngCount(RunState.Rng.Niche), targets = RngCount(RunState.Rng.CombatTargets), selection = RngCount(RunState.Rng.CombatCardSelection);
        int serial = ChieSerial("LitaSerial");
        for (int i = 0; i < 20; i++) { ChieLitaState.Get(first); ChieLitaState.Get(second); Call("ChieLitaState", "OnPermanentDeckChanged", Player); }
        Check(ChieLitaState.Get(first).Lita == 67 && ChieLitaState.Get(second).Lita == 23 && ChieSerial("LitaSerial") == serial,
            "77 previews and repeated deck notifications never reroll existing Lita");
        Check(RngCount(RunState.Rng.Niche) == niche && RngCount(RunState.Rng.CombatTargets) == targets && RngCount(RunState.Rng.CombatCardSelection) == selection,
            "77 named Lita assignments leave ordinary combat and selection RNG streams untouched");
        ChieOrigin.Pile!.RemoveInternal(ChieOrigin, true);
        var future = ChiePhysical<LitaProbeCard>(); Call("ChieLitaState", "OnPermanentDeckChanged", Player);
        Check(ChieLitaState.Get(future).Lita is >= 1 and <= 100 && ChieSerial("LitaSerial") == serial + 1,
            "77 permanently enabled assignment continues after all Chie cards are removed");
        Check(RngCount(RunState.Rng.Niche) == niche && RngCount(RunState.Rng.CombatTargets) == targets && RngCount(RunState.Rng.CombatCardSelection) == selection,
            "77 a new assignment also consumes none of the ordinary RNG streams");
        string id = ChieId(first); var physical = first.DeckVersion!;
        var copy = (LitaProbeCard)physical.MutableClone(); Set(copy, "_owner", Player); copy.DeckVersion = physical; ChieSave(physical).Fill(copy); Player.Deck.AddInternal(copy, 0);
        Call("ChieLitaState", "OnPermanentDeckChanged", Player);
        Check(ChieId(physical) == id && ChieId(copy) != id && ChieLitaState.Get(physical).Lita == 67 && ChieSerial("LitaSerial") == serial + 2,
            "77 a real saved permanent copy gets one new assignment without taking the original identity or Lita");
        Check(ChieLitaState.Get(ChieMirror(physical)).Lita == 67, "77 OD-style mirror reads its physical original without a new assignment");
        ChieSet(physical, 67, dressed: true, heart: 2, pride: true, combo: true);
        ChieEdit(data => { data["Cards"]![id]!["LastDressRoll"] = 82; data["Cards"]![id]!["LastDressSuccess"] = true; data["DressRollSerial"] = 9; });
        var restoredRelic = (ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().MutableClone(); Set(restoredRelic, "_owner", Player); ChieSave(ChieRelic).Fill(restoredRelic);
        var restored = (LitaProbeCard)ModelDb.Card<LitaProbeCard>().MutableClone(); Set(restored, "_owner", Player); ChieSave(physical).Fill(restored);
        Set(Player, "_relics", Player.Relics.Where(relic => relic != ChieRelic).Append(restoredRelic).ToList()); ChieRelic = restoredRelic;
        foreach (CardModel card in Player.Deck.Cards.ToArray()) card.Pile!.RemoveInternal(card, true); Player.Deck.AddInternal(restored);
        int before = ChieSerial("LitaSerial"); Call("ChieLitaState", "OnPermanentDeckChanged", Player);
        Check(ChieId(restored) == id && ChieLitaState.Get(restored).Lita == 67 && ChieSerial("LitaSerial") == before,
            "77 actual native SavedProperties JSON Fill preserves assigned Lita and enablement without rerolling after load");
        var state = ChieLitaState.Get(restored);
        Check(state.Dressed && state.LastRoll == 82 && state.LastSuccess && ChieSerial("DressRollSerial") == 9,
            "77 active battle dressing and last judgment survive native save restoration without a fresh roll");
        Call("ThirtyGBondHelper", "EndBattle", Player);
        state = ChieLitaState.Get(restored);
        Check(!state.Dressed && state.LastRoll == 0 && !state.LastSuccess && state.Lita == 67 && ChieData()["LitaEnabled"]!.GetValue<bool>(),
            "77 actual battle end removes only transient dressing and judgment while keeping permanent Lita and assignment enablement");
        var peer = SupportTeammate();
        var otherRelic = (ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().MutableClone(); Set(otherRelic, "_owner", peer); Set(peer, "_relics", peer.Relics.Append(otherRelic).ToList());
        var other = (LitaProbeCard)ModelDb.Card<LitaProbeCard>().MutableClone(); Set(other, "_owner", peer); peer.Deck.AddInternal(other);
        Call("ChieLitaState", "OnPermanentDeckChanged", peer);
        Check(ChieLitaState.Get(other).Lita == 0 && otherRelic.ProposalData == "", "77 a teammate without its own prior Chie acquisition is not enabled by the first owner");
    }
    static async Task ChieDressCases()
    {
        ChieReset(); var one = ChieTarget(60); await ChieBody(ChieMirror(ChieOrigin));
        Check(ChieLitaState.Get(one).Dressed && ChieSelections.Single().SequenceEqual(new[] { one }), "77 base card uses the real hand-only selection and dresses its chosen garden card");
        await ChieBody(ChieMirror(ChieOrigin)); Check(ChieLitaState.Get(one).Dressed, "77 repeated dressing is a single battle marker");
        ChieReset(); one = ChieTarget(60, PileType.Draw); await ChieBody(ChieMirror(ChieOrigin));
        Check(ChieLitaState.Get(one).Dressed && ChieSelections.Single().Contains(one), "77 no eligible hand card falls back to the real draw pile");
        ChieReset(); one = ChieTarget(60); ChieSet(one, 60, graduated: true); await ChieBody(ChieMirror(ChieOrigin));
        Check(!ChieLitaState.Get(one).Dressed && ChieSelections.Count == 0, "77 graduated cards are excluded and an empty candidate set does nothing");
        ChieReset(4); one = ChieTarget(65); await ChieBody(ChieMirror(ChieOrigin)); Equal(ChieLitaState.Get(one).Lita, 70, "77 Still Rising increases before later judgment and caps its own growth at70");
        ChieSet(one, 95); await ChieBody(ChieMirror(ChieOrigin)); Equal(ChieLitaState.Get(one).Lita, 95, "77 Still Rising never lowers a previously higher Lita");
        ChieReset(1, 3, 5); one = ChieTarget(80); var two = ChieTarget(55); var low = ChieTarget(49);
        await ChieBody(ChieMirror(ChieOrigin));
        Check(ChieLitaState.Get(one).Dressed && ChieLitaState.Get(two).Dressed && !ChieLitaState.Get(low).Dressed && ChieSelections.Count == 0,
            "77 Lolita Alliance dresses all hand targets above threshold without opening individual selections");
        Check(Hero.GetPower<HbrAttackUpPower>()?.Amount == 1 && Hero.GetPower<HbrCriticalDamageUpPower>()?.Amount == 1,
            "77 Full Burst applies only one65attack and45critical-damage team pair per cast even when two cards are dressed");
        var attack = Hero.GetPower<HbrAttackUpPower>()!; var critical = Hero.GetPower<HbrCriticalDamageUpPower>()!;
        Equal((decimal)AccessTools.PropertyGetter(attack.GetType(), "EffectPotency").Invoke(attack, null)!, 65m, "77 Full Burst attack basis resolves to65 at baseline intelligence");
        Equal((decimal)AccessTools.PropertyGetter(critical.GetType(), "EffectPotency").Invoke(critical, null)!, 45m, "77 Full Burst critical-damage basis resolves to45 at baseline intelligence");
        Check(Enemy.GetPower<HbrAttackDownPower>()?.Amount == 1 && Enemy.GetPower<HbrAttackDownPower>()!.Amount == 1,
            "77 Lolita God gives one enemy-turn20attack-down application rather than one application per target card");
        ChieReset(2); one = ChieTarget(100); await ChieBody(ChieMirror(ChieOrigin)); await ChasePlay(one);
        Check(ChieComboAtBody.All(value => value) && Hero.GetPower<HbrComboMediumPower>()?.Amount == 3,
            "77 Whimsical Weariness grants one actual three-use medium combo before the next nonattack body and preserves its uses");
        Check(!ChieData()["Cards"]![ChieId(one)]!["ComboPending"]!.GetValue<bool>(), "77 the pending combo receipt is consumed once across the full replay series");
    }
    static async Task ChieWrapperCases()
    {
        foreach (int heart in new[] { 0, 2 })
        {
            ChieReset(); var card = ChieTarget(100); ChieSet(card, 100, dressed: true, heart: heart, pride: true);
            int serial = ChieSerial("DressRollSerial"), energy = Player.PlayerCombatState!.Energy;
            for (int i = 0; i < 20; i++) Equal(ChieRelic.ModifyCardPlayCount(card, Enemy, 1), 1 + heart, "77 preview count reads no manual judgment receipt");
            Check(ChieSerial("DressRollSerial") == serial, "77 repeated unscope count previews do not roll");
            await ChasePlay(card);
            Check(ChiePlays.Count == 2 + heart && ChiePlays.Select(play => play.PlayIndex).SequenceEqual(Enumerable.Range(0, 2 + heart)),
                "77 true native GeneratePlayCount combines exactly one dressed bonus with native Heart replays: " + heart);
            Check(ChieSerial("DressRollSerial") == serial + 1 && ChieLitaState.Get(card).LastSuccess && ChieLitaState.Get(card).LastRoll is >= 1 and <= 100,
                "77 one manual wrapper performs exactly one pure judgment and exposes its final result: " + heart);
            Equal(ChieFaith(), 1, "77 Pride is granted once after the actual final bonus play: " + heart);
            Equal(energy - Player.PlayerCombatState.Energy, 1, "77 manual series pays native energy only once: " + heart);
            Equal((int)Call("ChieLitaState", "ReplayBonus", card)!, 0, "77 completed wrapper closes its manual receipt");
        }
        ChieReset(); var auto = ChieTarget(100); ChieSet(auto, 100, dressed: true, heart: 2, pride: true); await ChasePlay(auto, true);
        Check(ChiePlays.Count == 3 && ChieSerial("DressRollSerial") == 0 && ChieFaith() == 0,
            "77 true native automatic play retains Heart while making no judgment and granting no dressed bonus or Pride");
        ChieReset(); var nested = ChieTarget(100); ChieSet(nested, 100, dressed: true, heart: 2, pride: true);
        bool nestedOnce = false;
        LitaProbeCard.Body = async play =>
        {
            ChiePlays.Add(play);
            if (!nestedOnce) { nestedOnce = true; ChieDepth++; await ChasePlay(play.Card, true); ChieDepth--; }
        };
        await ChasePlay(nested);
        Check(ChiePlays.Count(play => play.IsAutoPlay) == 3 && ChiePlays.Count(play => !play.IsAutoPlay) == 4 && ChieSerial("DressRollSerial") == 1 && ChieFaith() == 1,
            "77 nested automatic play of the same physical card masks the parent receipt, then restores it without rerolling or stealing Pride");
        ChieReset(); var failed = ChieTarget(100); ChieSet(failed, 100, dressed: true, pride: true);
        LitaProbeCard.Body = play => throw new OperationCanceledException("controlled-before-bonus-body");
        bool canceled = false; try { await ChasePlay(failed); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled && ChieSerial("DressRollSerial") == 1 && ChieFaith() == 0 && (int)Call("ChieLitaState", "ReplayBonus", failed)! == 0,
            "77 a successful judgment followed by cancellation before the bonus body grants no Pride and releases its receipt");
        ChieReset(); var killed = ChieTarget(100); ChieSet(killed, 100, dressed: true, pride: true);
        LitaProbeCard.Body = play => { ChiePlays.Add(play); Set(Hero, "_currentHp", 0); return Task.CompletedTask; };
        await ChasePlay(killed);
        Check(ChiePlays.Count == 1 && ChieFaith() == 0, "77 death after the first body cannot grant Faith for an unexecuted dressed bonus");
        Set(Hero, "_currentHp", 5000);
        ChieReset(); var suspended = ChieTarget(100); ChieSet(suspended, 100, dressed: true, pride: true);
        ChieCountWait = new(TaskCreationOptions.RunContinuationsAsynchronously); ChieCountWaitTaken = false;
        Task pending = ChasePlay(suspended);
        Check(!pending.IsCompleted && ChieCountWaitTaken && ChiePlays.Count == 0 && (int)Call("ChieLitaState", "ReplayBonus", suspended)! == 0,
            "77 true native GeneratePlayCount can suspend without leaking its manual receipt to the caller");
        ChieCountWait.SetResult(); await pending; ChieCountWait = null;
        Check(ChiePlays.Count == 2 && ChieFaith() == 1 && ChieSerial("DressRollSerial") == 1,
            "77 suspended native count continuation preserves the final count and grants Pride only after its actual bonus body");
        ChieReset(); var strict = ChieTarget(50);
        var rngMethod = Mod.GetType("HBRGOSakiMod.Core.Compatibility.RngCompatibility", true)!.GetMethod("CreateNamed", F)!;
        int raw = 100, chosenSerial = -1;
        while (raw is 1 or 100)
        { chosenSerial++; var rng = (Rng)rngMethod.Invoke(null, [RunState.Rng, "hbrgo_dress_" + Player.NetId + "_" + chosenSerial])!; raw = rng.NextInt(1, 101); }
        ChieEdit(data => data["DressRollSerial"] = chosenSerial); ChieSet(strict, 100 - raw, dressed: true, pride: true);
        int niche = RngCount(RunState.Rng.Niche); var bonus = FormPower(typeof(NextRollBonusPower), 999);
        var sword = FormPower(typeof(SwordheartPower), 100); var fate = FormPower(typeof(FatePower), 88); var advantage = FormPower(typeof(AdvantageDicePower), 3);
        await ChasePlay(strict);
        Check(ChieLitaState.Get(strict).LastRoll == raw && !ChieLitaState.Get(strict).LastSuccess && ChiePlays.Count == 1 && ChieFaith() == 0,
            "77 judgment uses strict greater-than: a roll exactly100minusLita fails and never triggers a bonus");
        Check(bonus.Amount == 999 && sword.Amount == 100 && fate.Amount == 88 && advantage.Amount == 3 && RngCount(RunState.Rng.Niche) == niche,
            "77 judgment is a pure named1d100 and neither uses nor consumes next-roll, Swordheart, Fate, advantage, or ordinary RNG");
    }
    static async Task ChieCases()
    {
        foreach (Type type in new[] { typeof(CuteIsInvincibleHbrgoSaki), typeof(LitaProbeCard), typeof(ThirtyGCollectionRelic), typeof(HbrFaithPower),
            typeof(HbrAttackUpPower), typeof(HbrCriticalDamageUpPower), typeof(HbrAttackDownPower), typeof(HbrComboMediumPower),
            typeof(NextRollBonusPower), typeof(SwordheartPower), typeof(FatePower), typeof(AdvantageDicePower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        foreach (Type type in new[] { typeof(CuteIsInvincibleHbrgoSaki), typeof(LitaProbeCard), typeof(ThirtyGCollectionRelic) }) ChieRegisterSave(type);
        var h = new Harmony(ProductionId + ".chie77"); ulong? local = LocalContext.NetId;
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            LocalContext.NetId = Player.NetId;
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent", F)!, nameof(VisualTask));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            foreach (MethodInfo grid in typeof(CardSelectCmd).GetMethods(F).Where(method => method.Name == "FromSimpleGrid")) Patch(grid, nameof(ChieGrid));
            Patch(typeof(AbstractModel).GetMethod("AfterModifyingCardPlayCount", F)!, nameof(ChieCountPause));
            foreach (string name in new[] { "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch", "ChieLolitaPlayPatch", "ChieLolitaFinalPlayCountPatch", "ThirtyGCardIdentityWritePatch", "ThirtyGCardIdentityReadPatch" })
            {
                Type? type = Mod.GetType("HBRGOSakiMod.Core.Patches." + name) ?? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name);
                if (type == null && name.Contains("Identity")) continue;
                h.CreateClassProcessor(type!).Patch();
            }
            ChieLitaState.Changed += ChieNotifies.Add;
            ChieAssignmentCases(); await ChieDressCases(); await ChieWrapperCases();
            Check(ChieNotifies.Count > 0, "77 public state-change notifications accompany actual Lita, dressing, and judgment changes");
        }
        finally { ChieLitaState.Changed -= ChieNotifies.Add; LitaProbeCard.Body = null; h.UnpatchAll(h.Id); LocalContext.NetId = local; CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunChie(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); ChieCases().GetAwaiter().GetResult(); ok = true; } catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Production card bodies, named RNG assignment, native SavedProperties JSON Fill, real Native OnPlayWrapper/GeneratePlayCount/Before/AfterCardPlayed callbacks and PowerCmd effects. Card selection input, texture/clock/pile VFX/waits and intent refresh leaves isolated. No Godot rendering or live multiplayer." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Chie checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}

