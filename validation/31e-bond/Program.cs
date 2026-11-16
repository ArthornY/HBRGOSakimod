using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

public sealed class ThirtyOneEMemberFixture : CardModel, IThirtyOneEMemberCard
{
    public ThirtyOneEMember ThirtyOneEMember { get; set; }
    public override string Title => "31E native fixture " + ThirtyOneEMember;
    public ThirtyOneEMemberFixture() : base(2, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => EffectChecks.EObserve(play);
}
static class ThirtyOneEStandalone { public static void Run(string path) => EffectChecks.RunThirtyOneE(path); }
static partial class EffectChecks
{
    static readonly List<(CardModel Card, bool Auto, int Hits)> EPlays = [];
    static readonly List<(Player Player, int Count)> EExtraTurns = [];
    static bool ERequest(Player owner, int count) { EExtraTurns.Add((owner, count)); return false; }
    static bool EListeners(ref IEnumerable<AbstractModel> __result)
    { __result = Combat.Creatures.SelectMany(c => c.Powers.Cast<AbstractModel>()).Concat(RunState.Players.SelectMany(p => p.Relics.OfType<ThirtyOneECollection>())).ToArray(); return false; }
    internal static Task EObserve(CardPlay play)
    {
        var attack = EAttack(play.Card, play);
        EPlays.Add((play.Card, play.IsAutoPlay, (int)Hook.ModifyAttackHitCount(Combat, attack, 1)));
        return Task.CompletedTask;
    }
    static AttackCommand EAttack(CardModel card, CardPlay? play = null)
    {
        var attack = new AttackCommand(100);
        MethodInfo from = typeof(AttackCommand).GetMethod("FromCard", F)!;
        Invoke(from, attack, from.GetParameters().Length == 1 ? [card] : [card, play]);
        return attack;
    }
    static ThirtyOneEMemberFixture ECard(int member, bool permanent = false, PileType? pile = null)
    {
        var card = (ThirtyOneEMemberFixture)ModelDb.Card<ThirtyOneEMemberFixture>().MutableClone();
        Set(card, "_owner", Player); Set(card, "<Id>k__BackingField", new ModelId("CARD", "31E_FIXTURE_" + member));
        card.ThirtyOneEMember = (ThirtyOneEMember)member;
        if (permanent) Player.Deck.AddInternal(card);
        if (pile.HasValue) pile.Value.GetPile(Player).AddInternal(card, -1, true);
        return card;
    }
    static ThirtyOneECollection EReset(int kinds)
    {
        GardenReset(); EPlays.Clear(); EExtraTurns.Clear();
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1); Set(Player.PlayerCombatState!, "_energy", 100);
        Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        var relic = (ThirtyOneECollection)ModelDb.Relic<ThirtyOneECollection>().MutableClone(); Set(relic, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Append(relic).ToList());
        for (int i = 0; i < kinds; i++) ECard(i, true);
        relic.BeforeCombatStart().GetAwaiter().GetResult(); return relic;
    }
    static async Task EHitCases()
    {
        foreach (int kinds in new[] { 0, 1, 2, 6 })
        {
            var relic = EReset(kinds); var first = ECard(0, pile: PileType.Hand);
            Equal(relic.DisplayAmount, kinds, "64 permanent member counter " + kinds);
            if (kinds > 0) { ECard(0, true); Equal(relic.DisplayAmount, kinds, "64 duplicate permanent member never inflates roster"); }
            int expected = kinds >= 2 ? kinds : 0;
            for (int i = 0; i < 3; i++) Equal((int)Hook.ModifyAttackHitCount(Combat, EAttack(first), 1), 1 + expected,
                "64 pure native hit-count preview does not consume first-attack receipt");
            await ChasePlay(first);
            Equal(EPlays.Last().Hits, 1 + expected, "64 actual native first card wrapper receives all sister-relay segments");
            await ChasePlay(ECard(1, pile: PileType.Hand));
            Equal(EPlays.Last().Hits, 1m, "64 second physical attack in the same own turn has no relay segments");
            Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
            var doubled = ECard(0, pile: PileType.Hand); Power<HbrComboLargePower>(Hero, 1);
            await ChasePlay(doubled);
            Equal(EPlays.Last().Hits, (1m + expected) * 2m, "64 native combo hook adds sister segments before large-combo doubling");
        }
        EReset(2); var plain = ChaseCard<ChaseProbeAttack>();
        object scope = Call("CardPlayContextHelper", "Begin", plain, false)!;
        try
        {
            Equal((int)Call("ThirtyOneEBondHelper", "GetExtraHits", Hero, plain)!, 2m, "64 an ordinary non31E attack receives the same first-card relay");
            Equal((int)Call("ThirtyOneEBondHelper", "GetExtraHits", Enemy, plain)!, 0m, "64 enemy and phantom dealers cannot borrow the owner's card relay");
            ExecutionContext captured = ExecutionContext.Capture()!;
            Call("CardPlayContextHelper", "Close", scope);
            ExecutionContext.Run(captured, _ => Equal((int)Call("ThirtyOneEBondHelper", "GetExtraHits", Hero, plain)!, 0m,
                "64 a captured but closed card context cannot replay the first-card receipt"), null);
        }
        finally { Call("CardPlayContextHelper", "Close", scope); }
    }
    static async Task ECallAndArrivalCases()
    {
        var relic = EReset(4); var first = ECard(0, pile: PileType.Hand);
        var same = ECard(0, pile: PileType.Draw); var second = ECard(1, pile: PileType.Draw); var third = ECard(2, pile: PileType.Draw);
        int energy = Player.PlayerCombatState!.Energy; await ChasePlay(first);
        Check(EPlays.Count == 2 && EPlays[1].Auto && EPlays[1].Card.Id != first.Id, "64 real native AutoPlay calls exactly one different-name draw-pile sibling without chaining");
        Equal(energy - Player.PlayerCombatState.Energy, 2m, "64 native automatic sibling spends zero energy while the manual caller spends its normal two");
        Check(EPlays.All(play => play.Card.Pile == Player.PlayerCombatState.DiscardPile) && same.Pile == Player.PlayerCombatState.DrawPile,
            "64 native wrappers finish both played cards into discard and leave same-name draw card alone");
        Check(relic.ArrivedMembers == (1 | 1 << (int)((IThirtyOneEMemberCard)EPlays[1].Card).ThirtyOneEMember),
            "64 manually and automatically played sisters both enter the real arrival ledger");
        await ChasePlay(ECard(3, pile: PileType.Hand)); Check(EPlays.Count == 3, "64 a later31E play in the same own turn cannot call again");
        relic = EReset(4); first = ECard(0, pile: PileType.Hand); ECard(0, pile: PileType.Draw); await ChasePlay(first);
        Check(EPlays.Count == 1, "64 no eligible differently-named draw card means no call");
        ECard(1, pile: PileType.Draw); await ChasePlay(ECard(2, pile: PileType.Hand));
        Check(EPlays.Count == 2, "64 an empty first call is not retried after later draw-pile changes");
        relic = EReset(6);
        for (int i = 0; i < 6; i++) await ChasePlay(ECard(i, pile: PileType.Hand));
        Check(relic.ArrivedMembers == 63 && relic.AllArrivedRewardGiven && EExtraTurns.SequenceEqual(new[] { (Player, 1) }),
            "64 real six-kind arrival requests exactly one owner-only immediate personal turn");
        await ChasePlay(ECard(0, pile: PileType.Hand)); Check(EExtraTurns.Count == 1, "64 later plays cannot issue a second all-arrived turn in this battle");
        string description = relic.DynamicDescription.GetFormattedText();
        Check(description.Contains("大岛一千子") && description.Contains("大岛六宇亚") && description.Contains("全部到齐"),
            "64 actual relic tooltip names arrived sisters and shows that none are missing");
    }
    static async Task EExtraAndSaveCases()
    {
        var relic = EReset(2); await ChasePlay(ECard(0, pile: PileType.Hand));
        Type personal = Helper("PersonalImmediateTurnCoordinator"); FieldInfo current = personal.GetField("_current", F)!, battle = personal.GetField("_combat", F)!;
        object? old = current.GetValue(null), oldCombat = battle.GetValue(null);
        try
        {
            object session = Activator.CreateInstance(personal.GetNestedType("Session", F)!, true)!;
            Set(session, "Owner", Player); Set(session, "Combat", Combat); current.SetValue(null, session); battle.SetValue(null, Combat);
            await ChasePlay(ECard(1, pile: PileType.Hand)); Equal(EPlays.Last().Hits, 3m, "64 own personal extra turn has an independent first attack");
            current.SetValue(null, null); await ChasePlay(ECard(0, pile: PileType.Hand)); Equal(EPlays.Last().Hits, 1m, "64 returning from personal extra turn does not reset ordinary-turn receipt");
            Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); await ChasePlay(ECard(0, pile: PileType.Hand));
            Equal(EPlays.Last().Hits, 3m, "64 a native extra turn in the same round is isolated by ownerTurnNumber");
        }
        finally { current.SetValue(null, old); battle.SetValue(null, oldCombat); }
        relic = EReset(6); for (int i = 0; i < 6; i++) await ChasePlay(ECard(i, pile: PileType.Hand));
        NativeSaveType(typeof(ThirtyOneECollection));
        var options = new JsonSerializerOptions { IncludeFields = true };
        string payload = JsonSerializer.Serialize(SavedProperties.From(relic), options);
        var restored = (ThirtyOneECollection)ModelDb.Relic<ThirtyOneECollection>().MutableClone();
        JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(restored);
        Check(restored.ArrivedMembers == 63 && restored.AllArrivedRewardGiven, "64 real SavedProperties From/JSON/Fill retains six arrivals and reward receipt");
    }
    static async Task EYotsuhaArrivalCases()
    {
        var relic = EReset(4); var yotsuha = GardenCard<AbsolutelyNoEffortHbrgoSaki>();
        yotsuha.RemoveFromCurrentPile(); Player.PlayerCombatState!.DrawPile.AddInternal(yotsuha, -1, true);
        await ChasePlay(ECard(0, pile: PileType.Hand));
        Check(EPlays.Count == 1 && yotsuha.Pile == Player.PlayerCombatState.DrawPile,
            "69 real Call Name eligibility never selects an Unplayable Yotsuha from the draw pile");
        relic = EReset(0);
        foreach (int member in new[] { 0, 1, 2, 4, 5 })
        {
            ECard(member, true);
            Call("ThirtyOneEBondHelper", "RecordArrival", Player, (ThirtyOneEMember)member);
        }
        yotsuha = GardenCard<AbsolutelyNoEffortHbrgoSaki>();
        var permanent = GardenCard<AbsolutelyNoEffortHbrgoSaki>(); GardenPermanent(permanent);
        Check(relic.DisplayAmount == 6 && EExtraTurns.Count == 0, "69 five actual arrivals wait for the retained sixth sister");
        await yotsuha.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        Check(relic.ArrivedMembers == 63 && relic.AllArrivedRewardGiven && EExtraTurns.SequenceEqual(new[] { (Player, 1) }),
            "69 actual Yotsuha hand-end effect records the sixth arrival and requests the same one-time owner personal turn");
    }
    static void NativeSaveType(Type type)
    {
        Assembly native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } legacy) legacy.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [type]);
        else { Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!; cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]); cache.GetField("_initialized", F)!.SetValue(null, true); }
    }
    static async Task ECases()
    {
        foreach (Type type in new[] { typeof(ThirtyOneECollection), typeof(ThirtyOneEMemberFixture), typeof(HbrComboLargePower), typeof(AbsolutelyNoEffortHbrgoSaki), typeof(HbrAttackUpPower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var h = new Harmony(ProductionId + ".31e"); ulong? local = LocalContext.NetId;
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            LocalContext.NetId = Player.NetId;
            foreach (Type t in new[] { typeof(CombatState), typeof(MegaCrit.Sts2.Core.Runs.RunState) })
            { MethodInfo method = t.GetMethod("IterateHookListeners")!; h.Unpatch(method, HarmonyPatchType.Prefix, "hbr.fragile.native.checks"); Patch(method, nameof(EListeners)); }
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            Patch(Helper("PersonalImmediateTurnCoordinator").GetMethod("Request", F)!, nameof(ERequest));
            foreach (string name in new[] { "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch", "HbrComboVirtualHitPatch", "ThirtyOneEDescriptionPatch" })
            { Type type = Mod.GetType("HBRGOSakiMod.Core.Patches." + name) ?? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!; h.CreateClassProcessor(type).Patch(); }
            await EHitCases(); await ECallAndArrivalCases(); await EExtraAndSaveCases(); await EYotsuhaArrivalCases();
        }
        finally { h.UnpatchAll(h.Id); LocalContext.NetId = local; CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunThirtyOneE(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); ECases().GetAwaiter().GetResult(); ok = true; } catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real CardModel wrappers, native AutoPlay, native Hook hit count/combo ordering, real pile state and energy spending, personal/native extra session keys and SavedProperties; presentation/wait/pile animation sinks isolated, extra-turn request recorded. No Godot renderer or live network session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + "31E checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
