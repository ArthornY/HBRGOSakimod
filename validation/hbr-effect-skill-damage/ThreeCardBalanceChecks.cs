using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using ModOverclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

// Native AutoPlay/OnPlayWrapper chooses Yo's result pile and executes native
// Exhaust. Pile presentation preserves actual managed membership. Arrow uses
// the real pool filter, random distinct factory and Upgrade; only the pool's
// test data and the choice-screen/attack terminal are substituted.
static partial class EffectChecks
{
    static CardModel[] ThreeCardPool = [], ThreeArrowChoices = [];
    static AttackCommand? ThreeArrowAttack;
    static int ThreeExhausts;

    static void ThreeExhausted(CardModel card)
    {
        if (card is YoHbrgoSaki) ThreeExhausts++;
    }
    static bool ThreePool(ref IEnumerable<CardModel> __result) { __result = ThreeCardPool; return false; }
    static bool ThreeChoose(IReadOnlyList<CardModel> cards, ref Task<CardModel?> __result)
    {
        ThreeArrowChoices = cards.ToArray(); __result = Task.FromResult<CardModel?>(cards[0]); return false;
    }
    static bool ThreeAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        ThreeArrowAttack = __instance; __result = Task.FromResult(__instance); return false;
    }
    static T ThreeCanonical<T>() where T : CardModel
    {
        if (!ModelDb.Contains(typeof(T))) ModelDb.Inject(typeof(T));
        T card = ModelDb.Card<T>(); Set(card, "<Id>k__BackingField", ModelDb.GetId(typeof(T))); return card;
    }
    static T ThreeCard<T>(bool upgraded = false) where T : CardModel
    {
        T card = Combat.CreateCard<T>(Player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        Player.PlayerCombatState!.Hand.AddInternal(card, -1, true);
        return card;
    }
    static void ThreeReset()
    {
        MoonlightReset(); Fate(FateJson(bosses: 0));
        Set(Combat, "_allCards", new List<CardModel>());
        Set(Player.PlayerCombatState!, "<ExhaustPile>k__BackingField", new CardPile(PileType.Exhaust));
        Set(Player.PlayerCombatState!, "_piles", null);
        ThreeExhausts = 0; ThreeArrowAttack = null; ThreeArrowChoices = [];
    }

    static async Task ThreeCardBalanceCases()
    {
        var harmony = new Harmony(ProductionId + ".three-card-native-resolution");
        var patched = new List<MethodBase>(); ExceptionDispatchInfo? failure = null;
        ulong? local = LocalContext.NetId;
        object? allCards = typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat);
        void Patch(MethodBase method, string prefix)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(method);
        }
        try
        {
            LocalContext.NetId = Player.NetId;
            ThreeCanonical<YoHbrgoSaki>(); ThreeCanonical<DestinedArrowHbrgoSaki>(); ThreeCanonical<OverclockUpgradeHbrgoSaki>();
            if (!ModelDb.Contains(typeof(MidsummerHellCursePower))) ModelDb.Inject(typeof(MidsummerHellCursePower));
            Set(ModelDb.Power<MidsummerHellCursePower>(), "<Id>k__BackingField", ModelDb.GetId(typeof(MidsummerHellCursePower)));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "AddGeneratedCardToCombat")) Patch(add, nameof(CrackGenerated));
            MethodInfo exhausted = CombatManager.Instance.History.GetType().GetMethod("CardExhausted", F)!;
            harmony.Patch(exhausted, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ThreeExhausted), F)!)); patched.Add(exhausted);
            await ThreeYoCases();
            await ThreeArrowCases(harmony, patched);
            await ThreeOverclockUpgradeCases();
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase method in patched.Distinct())
            {
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            }
            LocalContext.NetId = local; Set(Combat, "_allCards", allCards);
            try { CombatInProgress(false); As31O(false); Reset(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }

    static async Task ThreeYoCases()
    {
        ThreeReset();
        foreach (bool upgraded in new[] { false, true })
        {
            var yo = ThreeCard<YoHbrgoSaki>(upgraded);
            Equal(yo.EnergyCost.GetAmountToSpend(), upgraded ? 0m : 1m, "Yo retains its existing base and upgraded energy prices");
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), yo, Enemy, skipCardPileVisuals: true);
            Check(yo.Pile == Player.PlayerCombatState!.ExhaustPile && !Player.PlayerCombatState.Hand.Cards.Contains(yo),
                "native OnPlayWrapper resolves Yo into the exhaust pile after its real effect (upgraded " + upgraded + ")");
        }
        Check(ThreeExhausts == 2 && Enemy.GetPowerAmount<MidsummerHellCursePower>() == 2,
            "native Exhaust executes once for each Yo while two real plays stack the existing curse");
        Power<StrengthPower>(Hero, 999);
        var strike = (StrikeIronclad)ThreeCanonical<StrikeIronclad>().ToMutable();
        Set(strike, "_owner", Player);
        Player.PlayerCombatState!.PlayPile.AddInternal(strike, -1, true);
        Hits.Clear();
        for (int index = 0; index < 6; index++)
            await (Task)Invoke(typeof(Hook).GetMethod("AfterCardPlayed", F)!, null, Combat, new ThrowingPlayerChoiceContext(), Play(strike))!;
        decimal expected = Convert.ToDecimal(Call("LargeCreatureHealthHelper", "GetMaxHp", Enemy)) * .2m;
        Check(Hits.Count == 2 && Hits.All(hit => hit.Raw == expected && hit.Final == expected &&
            hit.Props.HasFlag(MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable) && hit.Props.HasFlag(MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered)),
            "six same-type native after-play dispatches still trigger the two-stack curse twice for literal unblockable max-HP damage");
        Check(Enemy.GetPower<MidsummerHellCursePower>()!.ConsecutiveCardCount == 0,
            "the curse resets its sequence after each third card without gaining a once-per-turn lock");
    }

    static async Task ThreeArrowCases(Harmony harmony, List<MethodBase> patched)
    {
        ThreeCardPool = [ThreeCanonical<SlideStepHbrgoSaki>(), ThreeCanonical<StrongShotHbrgoSaki>(),
            ThreeCanonical<DragonArrowHbrgoSaki>(), ThreeCanonical<DestinedArrowHbrgoSaki>(), ThreeCanonical<YoHbrgoSaki>()];
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Models.CardPools.HBRGOSaki31OCardPool", true)!;
        if (!ModelDb.Contains(pool)) ModelDb.Inject(pool);
        Set(ModelDb.GetById<CardPoolModel>(ModelDb.GetId(pool)), "<Id>k__BackingField", ModelDb.GetId(pool));
        void Patch(MethodInfo method, string prefix) { harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(method); }
        Patch(typeof(CardPoolModel).GetMethod("GetUnlockedCards", F)!, nameof(ThreePool));
        Patch(typeof(CardSelectCmd).GetMethod("FromChooseACardScreen", F)!, nameof(ThreeChoose));
        Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(ThreeAttack));
        foreach (bool upgraded in new[] { false, true })
        {
            ThreeReset(); var arrow = ThreeCard<DestinedArrowHbrgoSaki>(upgraded);
            Equal(arrow.EnergyCost.GetAmountToSpend(), 1m, "Destined Arrow costs one energy in both upgrade states");
            Check(arrow.Keywords.Contains(CardKeyword.Exhaust), "Destined Arrow keeps its existing Exhaust keyword");
            await UnifiedPlayCard(arrow, false);
            Check(ThreeArrowChoices.Length == 3 && ThreeArrowChoices.Select(card => card.GetType()).Distinct().Count() == 3 &&
                ThreeArrowChoices.All(card => card is not DestinedArrowHbrgoSaki && card is not YoHbrgoSaki && card.IsUpgraded == upgraded),
                "real distinct-card factory retains three bow-only candidates and upgrades every candidate only for the upgraded arrow");
            Check(ThreeArrowChoices[0].Pile == Player.PlayerCombatState!.Hand,
                "the actual arrow effect adds the selected candidate to the hand");
            Check(ThreeArrowAttack is { IsMultiTargeted: true, IsRandomlyTargeted: false } && ReferenceEquals(ThreeArrowAttack.ModelSource, arrow),
                "the actual arrow effect still constructs one all-opponent attack from that card");
            Equal((decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(ThreeArrowAttack)!, 7m,
                "Destined Arrow retains its original seven native skill units (7000 base HBR damage)");
            Equal(arrow.DynamicVars.Damage.BaseValue, 7m, "upgrading Destined Arrow does not change its authored damage");
        }
    }

    static async Task ThreeOverclockUpgradeCases()
    {
        ThreeReset();
        if (!ModelDb.Contains(typeof(ModOverclock))) ModelDb.Inject(typeof(ModOverclock));
        Set(ModelDb.Relic<ModOverclock>(), "<Id>k__BackingField", ModelDb.GetId(typeof(ModOverclock)));
        var od = (ModOverclock)ModelDb.Relic<ModOverclock>().ToMutable(); Set(od, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { FateManual, od });
        string key = (string)typeof(ModOverclock).GetProperty("CurrentFloorUsageKey", F)!.GetValue(od)!;
        foreach (bool upgraded in new[] { false, true })
        {
            Set(od, "_charge", 0); Set(od, "_chargeFractionHundredths", 0);
            Invoke(typeof(ModOverclock).GetMethod("SetFloorUsage", F)!, od, key, 7);
            var card = ThreeCard<OverclockUpgradeHbrgoSaki>(upgraded);
            Equal(card.EnergyCost.GetAmountToSpend(), upgraded ? 1m : 2m, "OD Upgrade uses its new two/one energy prices");
            await UnifiedPlayCard(card, false);
            Check(od.Level == 1 && od.Charge == od.ChargePerLevel && od.UsedFloorLevels == 6 && od.RemainingFloorLevels == 6,
                "the actual OD Upgrade card still grants one full OD level and restores exactly one act allowance");
        }
        Invoke(typeof(ModOverclock).GetMethod("SetFloorUsage", F)!, od, key, 0);
        await UnifiedPlayCard(ThreeCard<OverclockUpgradeHbrgoSaki>(), false);
        Check(od.RemainingFloorLevels == ModOverclock.FloorLevelLimit,
            "OD Upgrade's allowance restoration still stops at the existing twelve-level act limit");
    }
}
