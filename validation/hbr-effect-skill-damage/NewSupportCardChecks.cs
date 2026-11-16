using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static readonly Type[] NewSupportCards = [typeof(ArpeggioHbrgoSaki), typeof(MimicShootingStarSongHbrgoSaki),
        typeof(MimicHeartFlutterSlideHbrgoSaki), typeof(MimicDivineCraftHbrgoSaki), typeof(MimicWillActivationHbrgoSaki)];
    static readonly List<(CardModel Card, decimal Raw, bool All)> SupportAttacks = [];
    static readonly List<decimal> SupportDraws = [];
    static bool SupportTypePredicate(string helper, string method, Type type) =>
        (bool)Invoke(Helper(helper).GetMethod(method, F, null, [typeof(Type)], null)!, null, type)!;
    static decimal SupportPotency(PowerModel power) => decimal.Parse(
        (string)power.GetType().GetProperty("HbrPotencyData", F)!.GetValue(power)!, CultureInfo.InvariantCulture);
    static bool SupportAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        SupportAttacks.Add(((CardModel)__instance.ModelSource!,
            (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!, __instance.IsMultiTargeted));
        __result = Task.FromResult(__instance); return false;
    }
    static bool SupportDrawLeaf(decimal count, ref Task<IEnumerable<CardModel>> __result)
    { SupportDraws.Add(count); __result = Task.FromResult(Enumerable.Empty<CardModel>()); return false; }
    static void SupportReset()
    {
        FormReset(); Set(Player, "_gold", 0); SupportAttacks.Clear(); SupportDraws.Clear();
    }
    static Task SupportGrant<T>(decimal basis, int? turns = null, Creature? target = null, bool withCaster = true) where T : PowerModel
    {
        MethodInfo method = Helper("HbrEffectApplication").GetMethods(F).Single(method => method.Name == "Apply" && method.IsGenericMethod);
        return (Task)Invoke(method.MakeGenericMethod(typeof(T)), null, new ThrowingPlayerChoiceContext(),
            target ?? Hero, basis, withCaster ? Hero : null, null, turns, null, false, null)!;
    }
    static async Task SupportTick(CombatSide side)
    {
        foreach (PowerModel power in Hero.Powers.ToArray())
            await power.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), side, [Hero]);
    }
    static object SupportStats(Creature holder)
    {
        object?[] args = [holder, null];
        Check((bool)Invoke(Helper("HbrBattleStatsHelper").GetMethod("TryGetStats", F)!, null, args)!, "a supported ally exposes live six-dimensional stats");
        return args[1]!;
    }
    static decimal SupportStat(object stats, string name) => (decimal)stats.GetType().GetProperty(name, F)!.GetValue(stats)!;

    static async Task NewSupportCardCases()
    {
        Type[] powers = [typeof(ChargePower), typeof(HbrAttackUpPower), typeof(HbrCriticalRateUpPower),
            typeof(HbrCriticalDamageUpPower), typeof(HbrMoralePower), typeof(HbrComboSmallPower),
            typeof(HbrComboLargePower), typeof(HbrCalamityPower)];
        foreach (Type type in NewSupportCards.Concat(powers).Append(typeof(LiveHbrgoSaki)))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony(ProductionId + ".new-support-native");
        var patched = new List<MethodBase>(); ExceptionDispatchInfo? failure = null;
        void Patch(MethodBase method, string prefix)
        { harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(method); }
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(SupportAttackLeaf));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" && method.GetParameters().Length == 4), nameof(SupportDrawLeaf));
            Type effectPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrEffectApplicationPatch", true)!;
            harmony.CreateClassProcessor(effectPatch).Patch();
            patched.Add(typeof(PowerCmd).GetMethods(F).Single(method => method.Name == "Apply" && !method.IsGenericMethod && method.GetParameters().Length == 7 && method.GetParameters()[1].ParameterType == typeof(PowerModel)));
            await SupportCardBodyCases();
            await SupportLiveAndMoraleCases();
            await SupportCriticalInstanceCases();
            await SupportCriticalCalculationCases();
            await SupportCannonStatDebuffCases();
            SupportPoolCases();
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase method in patched.Distinct())
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            try { CombatInProgress(false); As31O(false); Reset(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }

    static async Task SupportCardBodyCases()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            SupportReset();
            var arpeggio = FormCard<ArpeggioHbrgoSaki>(upgraded);
            Equal(arpeggio.DynamicVars.Damage.BaseValue, upgraded ? 7 : 5, "Arpeggio retains exact five/seven native skill units");
            await UnifiedPlayCard(arpeggio, false);
            Check(SupportAttacks.Single() is { All: false } attack && attack.Card == arpeggio && attack.Raw == (upgraded ? 7 : 5),
                "Arpeggio's actual body requests one single-target authored skill attack");
            Check(Hero.GetPower<HbrComboSmallPower>() is { Amount: 5 }, "Arpeggio grants the shared five-use small combo after attacking");
            Check(arpeggio.Rarity == CardRarity.Common && arpeggio.Type == CardType.Attack && arpeggio.EnergyCost.Canonical == 1,
                "Arpeggio is the new one-energy common attack");

            SupportReset(); var divine = FormCard<MimicDivineCraftHbrgoSaki>(upgraded);
            Equal(divine.DynamicVars.Damage.BaseValue, upgraded ? 11 : 8, "Divine Craft retains exact eight/eleven native skill units");
            await UnifiedPlayCard(divine, false);
            Check(SupportAttacks.Single() is { All: true } divineAttack && divineAttack.Card == divine && divineAttack.Raw == (upgraded ? 11 : 8),
                "Divine Craft's actual body constructs one all-opponent skill attack");
            Equal(HbrCalamityPower.GetTotalReduction(Enemy), upgraded ? 30 : 20, "Divine Craft applies its fixed twenty/thirty Calamity reduction");
            Check((bool)Call("ModeTransitionHelper", "IsBowRestrictedCard", divine)!, "Divine Craft participates in the existing bow-form switch route");

            SupportReset(); var will = FormCard<MimicWillActivationHbrgoSaki>(upgraded);
            Equal(will.EnergyCost.GetAmountToSpend(), upgraded ? 0 : 1, "Will Activation upgrades its energy price from one to zero");
            await UnifiedPlayCard(will, false);
            Check(Hero.GetPower<HbrComboLargePower>() is { Amount: 1 } && SupportDraws.SequenceEqual(new[] { 1m }),
                "Will Activation's actual body grants one-use large combo and draws one");
            Check((bool)Call("ModeTransitionHelper", "IsDualSwordsRestrictedCard", will)!, "Will Activation participates in the existing dual-sword switch route");

            SupportReset(); var stars = FormCard<MimicShootingStarSongHbrgoSaki>(upgraded);
            await UnifiedPlayCard(stars, false);
            Check(Hero.GetPower<HbrCriticalRateUpPower>() is { } rate && rate.Amount == (upgraded ? 4 : 3) && SupportPotency(rate) == 50m,
                "Shooting Star Song grants Wisdom-scaled fifty-percent critical rate for three/four player ends");
            Check(Hero.GetPower<HbrCriticalDamageUpPower>() is { } damage && damage.Amount == (upgraded ? 4 : 3) && SupportPotency(damage) == 50m,
                "Shooting Star Song separately grants fixed fifty-percent critical damage for the same duration");
        }
        foreach (Type type in NewSupportCards)
        {
            var card = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            Check(card.PortraitPath.EndsWith("memory_corridor_hbrgo_saki.png", StringComparison.Ordinal), "the new card uses the authorized default portrait: " + type.Name);
            Check(SupportTypePredicate("HbrgoSaki31OCardFilter", "IsThirtyOneOExclusive", type), "the new card is exclusive to the 31O pool: " + type.Name);
        }
    }

    static Player SupportTeammate()
    {
        var player = Blank<Player>(); Set(player, "_runState", RunState);
        Set(player, "<NetId>k__BackingField", Player.NetId + 1);
        Set(player, "<Character>k__BackingField", Player.Character);
        var state = Blank<PlayerCombatState>(); Set(state, "_player", player); Set(state, "<TurnNumber>k__BackingField", 1);
        foreach (var row in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            Set(state, "<" + row.Item1 + ">k__BackingField", new CardPile(row.Item2));
        Set(state, "_pets", new List<Creature>());
        Set(player, "<PlayerCombatState>k__BackingField", state);
        Set(player, "<Deck>k__BackingField", new CardPile(PileType.Deck));
        var creature = (Creature)H("Actor", null, player, null)!;
        Set(player, "<Creature>k__BackingField", creature);
        var manual = Blank<ElectronicSoldierManual>(); Set(manual, "<IsMutable>k__BackingField", true); Set(manual, "_owner", player);
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!.SetValue(manual,
            "{\"Schema\":1,\"Enabled\":true,\"HealthScaleApplied\":true,\"Level\":1}");
        Set(player, "_relics", new List<RelicModel> { manual });
        return player;
    }

    static async Task SupportLiveAndMoraleCases()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            SupportReset(); var live = FormCard<LiveHbrgoSaki>(upgraded);
            Player teammate = SupportTeammate();
            var pet = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
            Set(Combat, "_allies", new List<Creature> { Hero, teammate.Creature, pet });
            Set(RunState, "_players", new List<Player> { Player, teammate });
            try
            {
                await UnifiedPlayCard(live, false);
                Equal(Hero.GetPowerAmount<ChargePower>(), upgraded ? 3 : 2, "Live retains its existing two/three Charge upgrade");
                Equal(Player.Gold, upgraded ? 7 : 5, "Live retains its existing five/seven gold mutation");
                foreach (Creature ally in new[] { Hero, teammate.Creature, pet })
                    Check(ally.GetPower<HbrAttackUpPower>() is { Amount: 2 } power && SupportPotency(power) == 30m,
                        "Live grants thirty-percent attack up to every ally under the existing team helper");
            }
            finally { Set(RunState, "_players", new List<Player> { Player }); }
        }
        SupportReset(); Power<DexterityPower>(Hero, 50);
        await UnifiedPlayCard(FormCard<LiveHbrgoSaki>(), false);
        Equal(SupportPotency(Hero.GetPower<HbrAttackUpPower>()!), 30.6m, "Live snapshots INT200 as 30×(1+100×0.0002), without an upper endpoint");
        Powers(Hero).Remove(Hero.GetPower<DexterityPower>()!);
        Equal(SupportPotency(Hero.GetPower<HbrAttackUpPower>()!), 30.6m, "later attribute changes do not rewrite an already applied Live potency");
        await SupportTick(CombatSide.Enemy);
        Equal(Hero.GetPower<HbrAttackUpPower>()!.Amount, 2, "Live's player-end profile ignores an enemy-side end");
        await SupportTick(CombatSide.Player); Equal(Hero.GetPower<HbrAttackUpPower>()!.Amount, 1, "Live survives the first player-side end");
        await SupportTick(CombatSide.Player); Check(!Hero.HasPower<HbrAttackUpPower>(), "Live expires at the next player-side end");

        foreach (bool upgraded in new[] { false, true })
        {
            SupportReset(); var morale = FormCard<MimicHeartFlutterSlideHbrgoSaki>(upgraded);
            await UnifiedPlayCard(morale, false);
            Equal(Hero.GetPowerAmount<HbrMoralePower>(), upgraded ? 3 : 2, "Heart Flutter Slide grants two/three permanent Morale levels");
            Check(!morale.Keywords.Contains(CardKeyword.Exhaust), "Heart Flutter Slide remains reusable rather than Exhaust");
            foreach (string stat in new[] { "Strength", "Dexterity", "Intelligence", "Vitality", "Spirit", "Luck" })
                Equal(SupportStat(SupportStats(Hero), stat), upgraded ? 115 : 110, "Morale adds five raw attribute points per level: " + stat);
        }
        await PowerCmd.Apply<HbrMoralePower>(new ThrowingPlayerChoiceContext(), Hero, 20m, Hero, null);
        Equal(Hero.GetPowerAmount<HbrMoralePower>(), 10, "repeated Morale grants stop at level ten");
        await SupportTick(CombatSide.Player); await SupportTick(CombatSide.Enemy);
        Equal(Hero.GetPowerAmount<HbrMoralePower>(), 10, "Morale remains Eternal across both side ends");
        Power<StrengthPower>(Hero, 10); Power<DexterityPower>(Hero, 10);
        var stats = SupportStats(Hero);
        Equal(SupportStat(stats, "Strength"), 180, "Morale's +50 enters before the existing twenty-percent Strength bonus");
        Equal(SupportStat(stats, "Intelligence"), 180, "Morale's +50 enters before the existing twenty-percent Dexterity bonus");
        Power<CrimsonBladeFlowPower>(Hero, 1); stats = SupportStats(Hero);
        Equal(SupportStat(stats, "Strength"), 360, "the final Crimson stance doubles the Morale-adjusted attack stat");
        Equal(SupportStat(stats, "Vitality"), 0, "Morale cannot restore Vitality through final Crimson zeroing");
        Equal(SupportStat(stats, "Spirit"), 0, "Morale cannot restore Spirit through final Crimson zeroing");
    }

    static async Task SupportCriticalInstanceCases()
    {
        SupportReset();
        await SupportGrant<HbrCriticalRateUpPower>(50m, 3); await SupportGrant<HbrCriticalRateUpPower>(50m, 4);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Count() == 2 && Hero.Powers.OfType<HbrCriticalRateUpPower>().Select(power => power.Amount).Order().SequenceEqual(new[] { 3, 4 }),
            "identical critical-rate grants occupy two independent instances rather than merging turns");
        await SupportGrant<HbrCriticalRateUpPower>(40m, 9);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Count() == 2 && Hero.Powers.OfType<HbrCriticalRateUpPower>().All(power => SupportPotency(power) == 50),
            "a third weaker critical-rate grant cannot replace a stronger active instance");
        await SupportGrant<HbrCriticalRateUpPower>(60m, 2);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Select(SupportPotency).Order().SequenceEqual(new[] { 50m, 60m }) &&
            Hero.Powers.OfType<HbrCriticalRateUpPower>().Select(power => power.Amount).Order().SequenceEqual(new[] { 2, 4 }),
            "a stronger third grant replaces the weakest/shortest instance and resets its new duration");
        await SupportGrant<HbrCriticalDamageUpPower>(50m, 3); await SupportGrant<HbrCriticalDamageUpPower>(50m, 3);
        Equal((decimal)Invoke(typeof(HbrCriticalRateUpPower).GetMethod("GetTotalPercent", F)!, null, Hero)!, 110m, "both critical-rate percentages add independently");
        Equal((decimal)Invoke(typeof(HbrCriticalDamageUpPower).GetMethod("GetTotalPercent", F)!, null, Hero)!, 100m, "both fixed critical-damage percentages add independently");
        await SupportTick(CombatSide.Enemy);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Select(power => power.Amount).Order().SequenceEqual(new[] { 2, 4 }), "critical buffs do not count an enemy-side end");
        await SupportTick(CombatSide.Player);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Select(power => power.Amount).Order().SequenceEqual(new[] { 1, 3 }), "critical buffs count each player-side end once");
        Power<DexterityPower>(Hero, 50);
        await SupportGrant<HbrCriticalRateUpPower>(50m, 3);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Any(power => SupportPotency(power) == 51m), "critical rate uses the caster's INT200 to scale fifty to fifty-one");
        await SupportGrant<HbrCriticalDamageUpPower>(50m, 4);
        Check(Hero.Powers.OfType<HbrCriticalDamageUpPower>().All(power => SupportPotency(power) == 50m), "critical damage remains fixed when Intelligence changes");
    }

    static async Task SupportCriticalCalculationCases()
    {
        SupportReset(); await SupportGrant<HbrCriticalDamageUpPower>(50m);
        object scope = Call("HbrCriticalDamageContext", "Begin", Hero, null, ValueProp.Move)!;
        try
        {
            Set(scope, "Sample", 9999);
            int counter = RngCount(RunState.Rng.Niche);
            Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), decimal.Floor(Base(6m)), "critical-damage up does not change a noncritical original HBR hit");
            Equal((decimal)Call("HbrCriticalDamageContext", "GetCriticalDamageMultiplier", Enemy, Hero, null, ValueProp.Move)!, 1m,
                "the final critical stage reads a noncritical sample rather than inventing a die critical success");
            Check(RngCount(RunState.Rng.Niche) == counter, "reading a pre-sampled critical result does not roll again");
            Set(scope, "Sample", 0);
            Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), 12000m, "an original critical retains E-minus50 and uses the original 1.5-plus-fifty-percent critical factor");
            Equal(Actual(Enemy, Hero, .000001m, ValueProp.Move, null), 2m,
                "the exact 1.5-to-two critical boundary does not lose a point through a rounded four-thirds ratio");
            Equal(Preview(Enemy, Hero, 6m, ValueProp.Move, null), decimal.Floor(Base(6m)), "a preview never borrows the enclosing actual critical result");
            await SupportGrant<HbrCriticalDamageUpPower>(50m);
            Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), 15000m, "two critical-damage instances sum to the original 1.5-plus-one critical factor rather than compounding");
        }
        finally { Call("HbrCriticalDamageContext", "Close", scope); }
        SupportReset(); await SupportGrant<HbrCriticalRateUpPower>(50m);
        scope = Call("HbrCriticalDamageContext", "Begin", Hero, null, ValueProp.Move)!;
        try
        {
            Set(scope, "Sample", 4000);
            Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), 9000m, "critical-rate up joins equipment/natural probability and makes the same forty-percent sample critical");
        }
        finally { Call("HbrCriticalDamageContext", "Close", scope); }
        SupportReset(); await SupportGrant<HbrCriticalRateUpPower>(100m); await SupportGrant<HbrCriticalDamageUpPower>(50m);
        await Declared(6m);
        Equal(Hits.Single().Final, 12000m, "declared effect skills share critical-rate up and the same original critical-damage factor");
        Clean("new critical effect skill");
        Hits.Clear(); await Declared(.000001m);
        Equal(Hits.Single().Final, 2m, "declared effect criticals also preserve the exact two-point final floor boundary");
        Clean("tiny critical effect skill");
    }

    static void SupportPoolCases()
    {
        Type content = Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack", true)!;
        var retired = (IReadOnlySet<Type>)content.GetField("RetiredCardTypes", F)!.GetValue(null)!;
        var types = (Type[])content.GetField("CardTypes", F)!.GetValue(null)!;
        var models = new List<CardModel>();
        foreach (Type type in types.Where(type => !retired.Contains(type) &&
            !SupportTypePredicate("HbrgoSaki31OCardFilter", "IsExcluded", type) &&
            !SupportTypePredicate("HbrMemoryGardenCardCatalog", "IsGardenCard", type)))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            CardModel model = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            Set(model, "<Id>k__BackingField", ModelDb.GetId(type));
            models.Add(model);
        }
        object pool = Blank<HBRGOSaki31OCardPool>();
        var filtered = ((IEnumerable<CardModel>)Invoke(typeof(HBRGOSaki31OCardPool).GetMethod("FilterThroughEpochs", F)!, pool, null, models)!)
            .Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly).ToArray();
        foreach (var pair in new[] { (CardRarity.Common, 20), (CardRarity.Uncommon, 30), (CardRarity.Rare, 25), (CardRarity.Ancient, 2) })
            Equal(filtered.Count(card => card.Rarity == pair.Item1), pair.Item2, "the source registration and actual 31O epoch filter expose the agreed pool count: " + pair.Item1);
    }

    static async Task SupportCannonStatDebuffCases()
    {
        SupportReset();
        var millsage = new Millsage();
        Set(millsage, "<IsMutable>k__BackingField", true); Set(millsage, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual"), millsage });
        foreach (Type type in new[] { typeof(HbrWisdomDownPower), typeof(HbrSpiritDownPower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var cannon = (Creature)H("Actor", Blank<HotaruCoffinCannon>(), null, Player)!;
        var other = (Creature)H("Actor", Blank<HotaruContestCannon>(), null, Player)!;
        var phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Power<PhantomPower>(phantom, 1);
        Set(Combat, "_allies", new List<Creature> { Hero, cannon, other, phantom });
        await SupportGrant<HbrWisdomDownPower>(50m, 2, Hero, withCaster: false);
        await SupportGrant<HbrSpiritDownPower>(50m, 2, Hero, withCaster: false);
        Equal(SupportPotency(Hero.GetPower<HbrWisdomDownPower>()!), 50m, "an owner Wisdom Down application has the fixed fifty-percent basis without a caster");
        Equal(SupportStat(SupportStats(cannon), "Intelligence"), 50m, "cannons inherit the owner's Wisdom Down exactly once");
        Equal(SupportStat(SupportStats(cannon), "Spirit"), 50m, "cannons inherit the owner's Spirit Down exactly once");
        await SupportGrant<HbrWisdomDownPower>(50m, 2, cannon, withCaster: false);
        await SupportGrant<HbrSpiritDownPower>(50m, 2, cannon, withCaster: false);
        Equal(SupportStat(SupportStats(cannon), "Intelligence"), 0m, "owner and local fifty-percent Wisdom reductions add to one hundred rather than multiply to twenty-five");
        Equal(SupportStat(SupportStats(cannon), "Spirit"), 0m, "owner and local fifty-percent Spirit reductions add once each");
        Equal(SupportStat(SupportStats(other), "Intelligence"), 50m, "another cannon does not inherit its sibling's local Wisdom Down");
        Equal((decimal)Call("HotaruCannonBlockHelper", "Preview", Hero, 4m, null, cannon)!, 0m,
            "the real cannon block preview uses combined owner/local Wisdom reductions");
        Equal((decimal)Call("HotaruCannonBlockHelper", "Preview", Hero, 4m, null, other)!, 100m,
            "another cannon's block preview receives the owner reduction exactly once");
        Check(ReferenceEquals(Call("HotaruCannonBlockHelper", "GetStatSource", Hero), Hero),
            "cannon block previews restore the original owner's stat-source context");
        Equal(SupportStat(SupportStats(Hero), "Spirit"), 50m, "a cannon's local Spirit Down does not leak back to its owner");
        Equal(SupportStat(SupportStats(phantom), "Intelligence"), 100m, "an independently buffed phantom does not inherit its summoner's Wisdom Down");
        await SupportGrant<HbrWisdomDownPower>(50m, 2, phantom, withCaster: false);
        Equal(SupportStat(SupportStats(phantom), "Intelligence"), 50m, "the phantom applies only its own local Wisdom Down");
    }
}
