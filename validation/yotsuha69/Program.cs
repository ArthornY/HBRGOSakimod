using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class YotsuhaStandalone { public static void Run(string path) => EffectChecks.RunYotsuha(path); }
static partial class EffectChecks
{
    sealed record YotsuhaHit(CardModel Card, decimal Units, int Hits, bool All, decimal Destruction, int Relay);
    static readonly List<YotsuhaHit> YotsuhaHits = [];
    static readonly List<string> YotsuhaOrder = [];
    static AttackCommand? YotsuhaLeafCommand;
    static bool YotsuhaDirectLeaf(AttackCommand command, ref bool __result)
    { __result = ReferenceEquals(command, YotsuhaLeafCommand); return false; }
    static void YotsuhaPowerApplied(PowerModel __instance) => YotsuhaOrder.Add(__instance.GetType().Name);
    static bool YotsuhaAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        YotsuhaLeafCommand = __instance;
        CardModel card = (CardModel)__instance.ModelSource!;
        decimal destruction = (decimal)Call("YotsuhaNoEffortCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!;
        int relay = (int)Call("ThirtyOneEBondHelper", "GetExtraHits", Hero, card)!;
        YotsuhaHits.Add(new(card, (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!,
            (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(__instance)!, __instance.IsMultiTargeted, destruction, relay));
        YotsuhaOrder.Add("Attack");
        YotsuhaLeafCommand = null;
        Equal((decimal)Call("YotsuhaNoEffortCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!, 1m,
            "69 nested unrelated damage inside the same helper scope does not borrow its original destruction coefficient");
        __result = Task.FromResult(__instance); return false;
    }
    static AbsolutelyNoEffortHbrgoSaki YotsuhaCard(params int[] ids)
    {
        GardenReset(); YotsuhaHits.Clear(); YotsuhaOrder.Clear();
        Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        return GardenCard<AbsolutelyNoEffortHbrgoSaki>(ids);
    }
    static async Task YotsuhaEnd(AbsolutelyNoEffortHbrgoSaki card, int turn)
    {
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", turn);
        await card.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
    }
    static decimal YotsuhaPotency(PowerModel power) => Convert.ToDecimal(Native(power, "get_EffectPotency"));
    static async Task YotsuhaEndsCases()
    {
        var basic = YotsuhaCard();
        Check(basic.MaxUpgradeLevel == 7 && basic.CanonicalKeywords.Contains(CardKeyword.Unplayable) && basic.CanonicalKeywords.Contains(CardKeyword.Retain) &&
            !basic.ShouldPlay(basic, AutoPlayType.Default), "69 rare garden skill is genuinely Unplayable+Retain with seven upgrades");
        await YotsuhaEnd(basic, 1);
        Equal(YotsuhaPotency(Hero.GetPower<HbrAttackUpPower>()!), 50m, "69 actual hand-end body grants framework attack-up basis50");
        Check(basic.Pile == Player.PlayerCombatState!.Hand && Hero.Powers.OfType<HbrAttackUpPower>().Count() == 1,
            "69 hand-end body leaves the retained card in hand and applies exactly one counted attack-up");
        await YotsuhaEnd(basic, 1); Check(Hero.Powers.OfType<HbrAttackUpPower>().Count() == 1, "69 repeated same-turn hand-end dispatch cannot replay effects");
        await basic.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, new[] { Enemy });
        Check(Hero.Powers.OfType<HbrAttackUpPower>().Count() == 1, "69 enemy turn-end never triggers the owner's hand passive");
        basic.RemoveFromCurrentPile(); Player.PlayerCombatState.DiscardPile.AddInternal(basic, -1, true);
        await YotsuhaEnd(basic, 2); Check(Hero.Powers.OfType<HbrAttackUpPower>().Count() == 1, "69 a discarded Yotsuha does not trigger hand effects");

        var all = YotsuhaCard(0, 1, 2, 3, 5, 6);
        Check(all.Keywords.Contains(CardKeyword.Innate), "69 Homebody uses the actual native Innate keyword for initial hand placement");
        await YotsuhaEnd(all, 1);
        Equal(YotsuhaPotency(Hero.Powers.OfType<HbrAttackUpPower>().Last()), 80m, "69 first sleeping/decisive hand end applies75plus5 through normal formula");
        Check(Hero.GetPower<HbrComboSmallPower>() is { Amount: 5 }, "69 Bubble Burst gives one real small-combo application with five uses");
        Check(YotsuhaPotency(Hero.GetPower<HbrCriticalRateUpPower>()!) == 85m && YotsuhaPotency(Hero.GetPower<HbrCriticalDamageUpPower>()!) == 45m,
            "69 Dream Awakening applies actual separate85/45critical effects");
        Check(Native(Hero.GetPower<HbrCriticalRateUpPower>()!, "get_CurrentEffectProfile")!.ToString()!.Contains("Count") && Hero.GetPower<HbrCriticalRateUpPower>()!.Amount == 1,
            "69 Dream critical grant is one complete attack instead of turn duration");
        Check(YotsuhaOrder.IndexOf(nameof(HbrAttackUpPower)) < YotsuhaOrder.IndexOf(nameof(HbrCriticalRateUpPower)) &&
            YotsuhaOrder.IndexOf(nameof(HbrCriticalDamageUpPower)) < YotsuhaOrder.IndexOf(nameof(HbrComboSmallPower)) && YotsuhaOrder.Last() == "Attack",
            "69 real mutation order is attack-up, dream criticals, small combo, then Candy Burst");
        var candy = YotsuhaHits.Single();
        Check(candy.All && candy.Hits == 2 && candy.Units == 95m * 6m / 100m && candy.Card == all && candy.Relay == 0,
            "69 Candy Burst issues one owned native95percent two-hit all-enemy command without sister first-card relay");
        Equal(candy.Destruction, 1.4m / 3m, "69 Candy Burst keeps original total destruction1.4 rather than multiplying default3 again");
        int applications = Hero.Powers.OfType<HbrAttackUpPower>().Count();
        await YotsuhaEnd(all, 2);
        Check(Hero.Powers.OfType<HbrAttackUpPower>().Count() == applications && YotsuhaHits.Count == 2,
            "69 decisive skip affects only attack-up; other end effects still execute on the skipped turn");
        await YotsuhaEnd(all, 3);
        Equal((decimal)Call("YotsuhaNoEffortCombatHelper", "AttackUpBasis", all)!, 90m, "69 three hand ends accumulate fifteen sleeping points even across the decisive skip");
        all.RemoveFromCurrentPile(); Player.PlayerCombatState!.DiscardPile.AddInternal(all, -1, true);
        all.RemoveFromCurrentPile(); Player.PlayerCombatState.Hand.AddInternal(all, -1, true);
        Equal((decimal)Call("YotsuhaNoEffortCombatHelper", "AttackUpBasis", all)!, 90m, "69 leaving and returning to hand retains this battle's sleeping growth");
        for (int turn = 4; turn <= 14; turn++) await YotsuhaEnd(all, turn);
        Equal((decimal)Call("YotsuhaNoEffortCombatHelper", "AttackUpBasis", all)!, 125m, "69 sleeping growth caps at fifty points over decisive75");
        var fresh = YotsuhaCard(5); Equal((decimal)Call("YotsuhaNoEffortCombatHelper", "AttackUpBasis", fresh)!, 50m, "69 a new battle physical card starts with zero sleeping accumulation");
    }
    static CardPlay YotsuhaPlay(CardModel source, int paid, Creature? target = null)
    {
        var play = Play(source); Set(play, "<Resources>k__BackingField", new ResourceInfo { EnergySpent = paid, EnergyValue = paid, StarsSpent = 0, StarValue = 0 });
        Set(play, "<Target>k__BackingField", target ?? Enemy); return play;
    }
    static async Task YotsuhaShotCases()
    {
        var card = YotsuhaCard(4); var source = ChaseCard<ChaseProbeAttack>();
        source.EnergyCost.SetThisCombat(9); await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 1));
        var shot = YotsuhaHits.Single();
        Check(!shot.All && shot.Hits == 5 && shot.Units == 42m * 6m / 100m && shot.Relay == 0,
            "69 One More Shot uses five42percent hits and ignores the source card's later mutated live cost");
        Equal(shot.Destruction, 1.625m / 3m, "69 One More Shot keeps original destruction1.625 in the direct action scope");
        await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 0));
        Check(YotsuhaHits.Count == 1, "69 another cheap attack in the same own turn cannot fire a second shot");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
        await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 2));
        Check(YotsuhaHits.Count == 1, "69 two energy actually spent is ineligible even when live cost can later fall");
        await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 0));
        Check(YotsuhaHits.Count == 2, "69 a zero-paid nonX attack is eligible on a new own turn");
        var x = GardenCard<MyLegendBeginsHbrgoSaki>(4); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 3);
        await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(x, 0));
        Check(YotsuhaHits.Count == 2, "69 X-cost attack is excluded even when it actually spent zero");
        Type personal = Helper("PersonalImmediateTurnCoordinator"); var field = personal.GetField("_current", F)!; var combat = personal.GetField("_combat", F)!;
        object? previous = field.GetValue(null), oldCombat = combat.GetValue(null);
        try
        {
            Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
            object session = Activator.CreateInstance(personal.GetNestedType("Session", F)!, true)!;
            Set(session, "Owner", Player); Set(session, "Combat", Combat); field.SetValue(null, session); combat.SetValue(null, Combat);
            await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 0));
            Check(YotsuhaHits.Count == 3, "69 personal extra turn receives its own once-per-turn shot receipt");
            field.SetValue(null, null); await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 0));
            Check(YotsuhaHits.Count == 3, "69 returning to the ordinary turn preserves its already used shot receipt");
        }
        finally { field.SetValue(null, previous); combat.SetValue(null, oldCombat); }
        Player peer = SupportTeammate(); Set(source, "_owner", peer); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 4);
        await card.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), YotsuhaPlay(source, 0));
        Check(YotsuhaHits.Count == 3, "69 a teammate's cheap attack cannot fire the holder's Yotsuha");
    }
    static void YotsuhaListeners(ref IEnumerable<AbstractModel> __result) => __result =
        Combat.Creatures.SelectMany(c => c.Powers.Cast<AbstractModel>()).Concat(Player.Relics)
            .Concat(Player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards)).Distinct().ToArray();
    static async Task YotsuhaWrapperCases(Harmony h)
    {
        ulong? local = LocalContext.NetId;
        try
        {
            LocalContext.NetId = Player.NetId;
            foreach (Type t in new[] { typeof(CombatState), typeof(MegaCrit.Sts2.Core.Runs.RunState) })
                h.Patch(t.GetMethod("IterateHookListeners")!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(YotsuhaListeners), F)!));
            void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            foreach (string name in new[] { "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + name, false) ?? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch();
            var card = YotsuhaCard(4); Set(Player.PlayerCombatState!, "_energy", 20);
            await ChasePlay(ChaseCard<ChaseProbeAttack>(1));
            Check(YotsuhaHits.Count == 1 && Player.PlayerCombatState!.Energy == 19,
                "69 actual native card wrapper dispatches the retained observer and uses one energy really paid");
            Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
            await ChasePlay(ChaseCard<ChaseProbeAttack>(2));
            Check(YotsuhaHits.Count == 1 && Player.PlayerCombatState!.Energy == 17,
                "69 actual native wrapper rejects a two-energy payment without consuming the next cheap-shot chance");
            await ChasePlay(ChaseCard<ChaseProbeAttack>(0));
            Check(YotsuhaHits.Count == 2 && Player.PlayerCombatState!.Energy == 17 && card.Pile == Player.PlayerCombatState.Hand,
                "69 actual zero-paid card wrapper fires the shot and never plays or moves the retained Yotsuha");
        }
        finally { LocalContext.NetId = local; }
    }
    static async Task YotsuhaCases()
    {
        foreach (Type type in new[] { typeof(AbsolutelyNoEffortHbrgoSaki), typeof(HbrAttackUpPower), typeof(HbrCriticalRateUpPower), typeof(HbrCriticalDamageUpPower), typeof(HbrComboSmallPower), typeof(MyLegendBeginsHbrgoSaki), typeof(ThirtyOneECollection) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var h = new Harmony(ProductionId + ".yotsuha69");
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(YotsuhaAttackLeaf));
            Patch(Helper("HighHitDamageVisualHelper").GetMethod("IsDirectAttackDamage", F)!, nameof(YotsuhaDirectLeaf));
            h.Patch(typeof(PowerModel).GetMethod("ApplyInternal", F)!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(YotsuhaPowerApplied), F)!));
            await YotsuhaEndsCases(); await YotsuhaShotCases(); await YotsuhaWrapperCases(h);
        }
        finally { h.UnpatchAll(h.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunYotsuha(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); YotsuhaCases().GetAwaiter().GetResult(); ok = true; } catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual hand-end/card-play callbacks, native framework buff mutations and structural keywords; actual attack requests recorded at Execute leaf with direct-action identity guard. Personal/native-turn receipts and spent-resource snapshots. No Godot or live network session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + "Yotsuha checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
