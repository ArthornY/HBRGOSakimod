using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    sealed record ChaseObservation(CardModel Card, bool Automatic, int Replay, bool Chase, bool Next, int EnergyCost, int Hits, decimal Damage);
    static readonly List<ChaseObservation> ChaseObservations = [];
    static bool ChaseNestOnce, ChaseThrow;
    static int ChaseDamageRaw = 100;

    sealed class ChaseProbeAttack : CardModel
    {
        public override string Title => "Chase probe attack";
        public ChaseProbeAttack() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
        protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => ObserveChase(play);
    }
    sealed class ChaseProbeSkill : CardModel
    {
        public override string Title => "Chase probe skill";
        public ChaseProbeSkill() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
        protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    }
    static async Task ObserveChase(CardPlay play)
    {
        await Task.Yield();
        CardModel card = play.Card;
        ChaseAssertWrapperScopes(card);
        var attack = new AttackCommand(ChaseDamageRaw);
        MethodInfo fromCard = typeof(AttackCommand).GetMethod("FromCard", F)!;
        Invoke(fromCard, attack, fromCard.GetParameters().Length == 1 ? [card] : [card, play]);
        int hits = (int)Hook.ModifyAttackHitCount(Combat, attack, 1);
        ChaseObservations.Add(new(card, play.IsAutoPlay, play.PlayIndex,
            (bool)Call("DualSwordsChaseHelper", "IsChaseAttack", Hero, card)!,
            Hero.GetPower<ReptureDualSwordsPower>()?.NextAttackHasChase ?? false,
            card.EnergyCost.GetAmountToSpend(), hits, Actual(Enemy, Hero, ChaseDamageRaw, ValueProp.Move, card)));
        if (ChaseThrow) throw new InvalidOperationException("controlled-chase-play-failure");
        if (ChaseNestOnce && !play.IsAutoPlay)
        {
            ChaseNestOnce = false;
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), ChaseCard<ChaseProbeAttack>(), Enemy,
                skipCardPileVisuals: true);
            Check((bool)Call("DualSwordsChaseHelper", "IsChaseAttack", Hero, card)!,
                "a nested automatic card restores the outer physical chase after await");
        }
    }
    static bool ChaseManualPile(CardModel card, ref Task __result)
    {
        card.RemoveFromCurrentPile(); Player.PlayerCombatState!.PlayPile.AddInternal(card, -1, true);
        __result = Task.CompletedTask; return false;
    }
    static void ChaseAssertWrapperScopes(CardModel card)
    {
        Check(Call("CardPlayContextHelper", "GetCurrentPlayToken", card) != null,
            "the native/reverse wrapper preserves the earlier CardPlayContext capture prefix");
        object? truth = AsyncValue("MoreThanOneTruthCombatHelper", "Current");
        Check(truth != null && (bool)truth.GetType().GetField("Active", F)!.GetValue(truth)! &&
            ReferenceEquals(card, truth.GetType().GetField("Card", F)!.GetValue(truth)),
            "the native/reverse wrapper preserves the earlier MoreThanOneTruth capture prefix");
    }
    static bool ChaseRestrictedAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        ChaseAssertWrapperScopes((CardModel)__instance.ModelSource!);
        __result = Task.FromResult(__instance); return false;
    }
    static T ChaseCard<T>(int? energy = null) where T : CardModel
    {
        ThreeCanonical<T>();
        T card = Combat.CreateCard<T>(Player);
        if (energy.HasValue) card.EnergyCost.SetThisCombat(energy.Value);
        Player.PlayerCombatState!.Hand.AddInternal(card, -1, true);
        return card;
    }
    static void ChaseReset(bool hbr = false)
    {
        FormReset(hbr, typeof(ReptureDualSwordsPower));
        Set(Player.PlayerCombatState!, "_energy", 30);
        ChaseObservations.Clear(); ChaseNestOnce = ChaseThrow = false; ChaseDamageRaw = hbr ? 6 : 100;
    }
    static async Task ChasePlay(CardModel card, bool automatic = false)
    {
        if (automatic)
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, Enemy, skipCardPileVisuals: true);
        else
        {
            var (energy, stars) = await card.SpendResources();
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), Enemy, false,
                new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars }, true);
        }
    }
    static void ChaseNext(bool enabled) => Invoke(typeof(ReptureDualSwordsPower)
        .GetMethod("SetNextAttackHasChase", F)!, Hero.GetPower<ReptureDualSwordsPower>(), enabled);

    static async Task DualSwordsChaseCases()
    {
        foreach (Type type in new[] { typeof(ReptureDualSwordsPower), typeof(DualSwordsEnhancementPower),
            typeof(MagicSwordUniversalityPower), typeof(SwordheartPower), typeof(SwordheartClarityPower),
            typeof(ChaseProbeAttack), typeof(ChaseProbeSkill), typeof(DualSwordsEnhancementHbrgoSaki),
            typeof(SwallowReversalHbrgoSaki), typeof(ReptureBowPower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony(ProductionId + ".dual-chase-native");
        var patched = new List<MethodBase>(); ExceptionDispatchInfo? failure = null;
        ulong? local = LocalContext.NetId;
        void Patch(MethodBase method, string prefix)
        { harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(method); }
        void Production(string name, MethodBase target)
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Patches." + name) ?? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            harmony.CreateClassProcessor(type).Patch(); patched.Add(target);
        }
        try
        {
            LocalContext.NetId = Player.NetId;
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            Production("ModeRestrictedCardPlayPatch", typeof(CardModel).GetMethod("OnPlayWrapper", F)!);
            Production("CardPlayContextPatch", typeof(CardModel).GetMethod("OnPlayWrapper", F)!);
            Production("MoreThanOneTruthPlayScopePatch", typeof(CardModel).GetMethod("OnPlayWrapper", F)!);
            Production("CardModelSpendResourcesPatch", typeof(CardModel).GetMethod("SpendResources", F)!);
            Production("PlayerCombatStateHasEnoughResourcesForPatch", typeof(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState).GetMethod("HasEnoughResourcesFor", F)!);
            Production("HbrComboVirtualHitPatch", typeof(Hook).GetMethod("ModifyAttackHitCount", F)!);
            await ChaseAlternatingCases();
            await ChasePaymentCases();
            await ChaseReplayAndAutomaticCases();
            await ChaseDamageCases();
            ChaseCannonAndUnrelatedPreviewCases();
            await ChaseRestrictedWrapperScopeCases(harmony, patched);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase method in patched.Distinct())
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            LocalContext.NetId = local;
            try { CombatInProgress(false); As31O(false); Reset(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }

    static async Task ChaseAlternatingCases()
    {
        ChaseReset();
        for (int index = 0; index < 4; index++)
        {
            CardModel card = ChaseCard<ChaseProbeAttack>();
            bool chase = index % 2 == 1;
            Equal(card.EnergyCost.GetAmountToSpend(), chase ? 1 : 2, "hand and native payment use alternating normal/chase costs");
            int before = Player.PlayerCombatState!.Energy;
            await ChasePlay(card);
            var observation = ChaseObservations.Last();
            Check(observation.Chase == chase && observation.Next == !chase,
                "native BeforeCardPlayed alternates ordinary/chase exactly once per manual card: " + index);
            Equal(observation.Hits, chase ? 5 : 4, "native hit hook adds permanent medium and transient chase small");
            Equal(before - Player.PlayerCombatState.Energy, chase ? 1 : 2, "displayed half cost matches real energy spent");
            Check(card.Pile == Player.PlayerCombatState.DiscardPile, "the complete native wrapper cleans the played card into discard");
        }
        ChaseNext(true);
        await ChasePlay(ChaseCard<ChaseProbeSkill>());
        Check(!Hero.GetPower<ReptureDualSwordsPower>()!.NextAttackHasChase,
            "a manual nonattack breaks the sequence and makes the next attack ordinary");
        Check(AsyncValue("DualSwordsChaseHelper", "Current") == null, "completed native wrappers leave no caller chase scope");
        Check(AsyncValue("CardPlayContextHelper", "Current") == null &&
            AsyncValue("MoreThanOneTruthCombatHelper", "Current") == null,
            "completed replacement wrappers also close both earlier production capture scopes");
    }

    static async Task ChasePaymentCases()
    {
        foreach (int price in new[] { 0, 1, 2, 3, 5 })
        {
            ChaseReset(); ChaseNext(true);
            CardModel card = ChaseCard<ChaseProbeAttack>(price);
            Equal(card.EnergyCost.GetAmountToSpend(), price / 2, "half-energy rounds down for authored cost " + price);
            Check(Player.PlayerCombatState!.HasEnoughResourcesFor(card, out _), "native resource eligibility accepts its displayed chase price");
            int before = Player.PlayerCombatState.Energy;
            var spent = await card.SpendResources();
            Equal(spent.Item1, price / 2, "native SpendResources pays the exact half-price quote");
            Equal(before - Player.PlayerCombatState.Energy, price / 2, "native energy pool loses only the quoted chase energy");
        }
        foreach (bool clarity in new[] { false, true })
        {
            ChaseReset(); ChaseNext(true);
            Power<MagicSwordUniversalityPower>(Hero, 1); Power<SwordheartPower>(Hero, 8);
            if (clarity) Power<SwordheartClarityPower>(Hero, 1);
            Set(Player.PlayerCombatState!, "_energy", 0);
            CardModel card = ChaseCard<ChaseProbeAttack>(3);
            object quote = CostQuote(card)!;
            CostCheckQuote(quote, 1, 0, clarity ? 2 : 1, true);
            Check(Player.PlayerCombatState!.HasEnoughResourcesFor(card, out _), "chase and Swordheart resource validation use one native adjusted quote");
            await card.SpendResources();
            Equal(Hero.GetPowerAmount<SwordheartPower>(), clarity ? 6 : 7,
                "actual Swordheart substitution uses half-energy first, then Clarity exactly once");
        }
        ChaseReset(); ChaseNext(true);
        ThreeCanonical<Whirlwind>();
        var x = Combat.CreateCard<Whirlwind>(Player); Player.PlayerCombatState!.Hand.AddInternal(x, -1, true);
        Set(Player.PlayerCombatState, "_energy", 3);
        Equal(x.EnergyCost.GetAmountToSpend(), 3, "X-cost chase remains three energy rather than halving X");
        var xSpent = await x.SpendResources();
        Check(xSpent.Item1 == 3 && x.EnergyCost.CapturedXValue == 3, "native X capture and payment remain unchanged");
    }

    static async Task ChaseReplayAndAutomaticCases()
    {
        ChaseReset(); ChaseNext(true);
        var replay = ChaseCard<ChaseProbeAttack>(); replay.BaseReplayCount = 1;
        await ChasePlay(replay);
        Check(ChaseObservations.Count == 2 && ChaseObservations.All(row => row.Chase && !row.Next && row.Hits == 5),
            "both real native replays keep the physical card chase and toggle the next state only once");
        ChaseNext(true); ChaseObservations.Clear();
        await ChasePlay(ChaseCard<ChaseProbeAttack>(), automatic: true);
        Check(ChaseObservations.Single() is { Automatic: true, Chase: false, Next: true, Hits: 4 },
            "automatic attacks gain only the form medium, do not use chase and do not alter the pending sequence");
        await ChasePlay(ChaseCard<ChaseProbeSkill>(), automatic: true);
        Check(Hero.GetPower<ReptureDualSwordsPower>()!.NextAttackHasChase,
            "automatic nonattacks do not break a pending manual chase");
        ChaseObservations.Clear(); ChaseNestOnce = true;
        await ChasePlay(ChaseCard<ChaseProbeAttack>());
        Check(ChaseObservations.Count == 2 && ChaseObservations[0] is { Automatic: false, Chase: true } &&
            ChaseObservations[1] is { Automatic: true, Chase: false },
            "nested automatic attack masks rather than inherits the enclosing manual chase");
        Check(!Hero.GetPower<ReptureDualSwordsPower>()!.NextAttackHasChase,
            "nested automatic play leaves the outer chase's alternating result unchanged");
        ChaseNext(true); ChaseThrow = true;
        try { await ChasePlay(ChaseCard<ChaseProbeAttack>()); throw new Exception("expected controlled failure"); }
        catch (InvalidOperationException error) when (error.Message == "controlled-chase-play-failure") { }
        finally { ChaseThrow = false; }
        Check(AsyncValue("DualSwordsChaseHelper", "Current") == null,
            "failed asynchronous card execution closes the chase scope and preserves its original exception");
        Check(AsyncValue("CardPlayContextHelper", "Current") == null &&
            AsyncValue("MoreThanOneTruthCombatHelper", "Current") == null,
            "failed asynchronous replacement wrappers close both earlier production capture scopes");
    }

    static async Task ChaseDamageCases()
    {
        foreach (bool hbr in new[] { false, true })
        {
            ChaseReset(hbr);
            CardModel ordinary = ChaseCard<ChaseProbeAttack>(); await ChasePlay(ordinary);
            decimal baseline = ChaseObservations.Last().Damage;
            CardModel chase = ChaseCard<ChaseProbeAttack>(); await ChasePlay(chase);
            Equal(ChaseObservations.Last().Damage, hbr ? decimal.Floor(baseline * .75m) : baseline * .75m,
                "chase applies one final 75-percent damage factor after the shared damage formula (HBR " + hbr + ")");
            Power<DualSwordsEnhancementPower>(Hero, 1); ChaseNext(true);
            await ChasePlay(ChaseCard<ChaseProbeAttack>());
            Equal(ChaseObservations.Last().Damage, baseline, "Dual Swords Enhancement restores chase to exactly 100 percent");
            Equal(ChaseObservations.Last().Hits, 5, "enhanced chase preserves both form and chase extra hits");
        }
        ChaseReset();
        var enhancement = ChaseCard<DualSwordsEnhancementHbrgoSaki>();
        enhancement.UpgradeInternal(); enhancement.FinalizeUpgradeInternal();
        Equal(enhancement.DynamicVars["DamagePercent"].BaseValue, 100m, "upgraded enhancement no longer invents an obsolete 110-percent blanket form bonus");
        Check(enhancement.Keywords.Contains(CardKeyword.Innate), "upgraded enhancement retains its authored Innate keyword");
        var dual = Hero.GetPower<ReptureDualSwordsPower>()!;
        Equal((decimal)Native(dual, "ModifyBlockMultiplicative", Hero, 10m, ValueProp.Move, enhancement, null)!, 1m,
            "ordinary dual-sword form no longer multiplies block by 150 percent");
        int energy = Player.PlayerCombatState!.Energy;
        await dual.AfterEnergyReset(Player);
        Equal(Player.PlayerCombatState.Energy, energy, "ordinary dual-sword form no longer grants turn-start energy");
    }

    static decimal ChaseCannonCalculation(Creature cannon, CardModel card)
    {
        Type helper = Helper("HotaruCannonStatsHelper");
        object local = helper.GetField("Pending", F)!.GetValue(null)!;
        PropertyInfo value = local.GetType().GetProperty("Value")!;
        object? prior = value.GetValue(local);
        object intent = Activator.CreateInstance(helper.GetNestedType("Intent", F)!, true)!;
        Set(intent, "Cannon", cannon); Set(intent, "Target", Enemy); Set(intent, "Dealer", Hero);
        Set(intent, "Amount", 100m); Set(intent, "Props", ValueProp.Move); Set(intent, "Card", card);
        value.SetValue(local, intent);
        object?[] args = [new[] { Enemy }, 100m, ValueProp.Move, Hero, card, null];
        object scope = Invoke(helper.GetMethod("Begin", F)!, null, args)!;
        try { return Actual(Enemy, Hero, 100m, ValueProp.Move, card); }
        finally { Call("HotaruCannonStatsHelper", "Close", scope); value.SetValue(local, prior); }
    }

    static void ChaseCannonAndUnrelatedPreviewCases()
    {
        ChaseReset();
        CardModel card = ChaseCard<ChaseProbeAttack>();
        var cannon = (Creature)H("Actor", Blank<HotaruContestCannon>(), null, Player)!;
        cannon.CombatState = Combat;
        Set(Combat, "_allies", new List<Creature> { Hero, cannon });
        decimal normalCannon = ChaseCannonCalculation(cannon, card);
        ChaseNext(true);
        using ((IDisposable)Call("DualSwordsChaseHelper", "BeginPlay", card, false)!)
        {
            Equal(Actual(Enemy, Hero, 100m, ValueProp.Move, card), 75m,
                "the physical card enters the exact shared chase damage pipeline");
            Equal(ChaseCannonCalculation(cannon, card), normalCannon,
                "a real cannon command context with the same owner and card source does not inherit chase's 75-percent factor");
            Check(!(bool)Call("DualSwordsChaseHelper", "IsChaseAttack", cannon, card)!,
                "a pet or phantom actor cannot borrow its owner's physical-card chase");
            Equal(Actual(Hero, Hero, 100m, ValueProp.Move, card), 100m,
                "chase does not reduce a same-side self-damage calculation");
            Equal(Actual(Enemy, Hero, 100m, ValueProp.Unpowered, card), 100m,
                "unpowered triggered damage does not become chase attack damage");
            using ((IDisposable)Call("PrecalculatedDamageContext", "Begin", card, Enemy, 100m)!)
                Equal(Actual(Enemy, Hero, 100m, ValueProp.Move, card), 100m,
                    "already resolved damage does not apply chase a second time");
        }

        // A native damage preview can refer to a mutable unattached card while
        // its owner's run piles are not initialized. Non-dual actors must not
        // inspect those unrelated piles just to rule out chase.
        Powers(Hero).Remove(Hero.GetPower<ReptureDualSwordsPower>()!);
        CardModel unattached = ThreeCanonical<ChaseProbeAttack>().ToMutable(); Set(unattached, "_owner", Player);
        CardPile originalDeck = Player.Deck;
        try
        {
            Set(Player, "<Deck>k__BackingField", null); Set(Player, "_runPiles", null);
            Check(!(bool)Call("DualSwordsChaseHelper", "IsChaseAttack", Hero, unattached)!,
                "a non-dual preview rejects chase before querying uninitialized native card piles");
            Equal(Actual(Enemy, Hero, 100m, ValueProp.Move, unattached), 100m,
                "unrelated native damage previews remain valid without the owner's deck-pile cache");
        }
        finally { Set(Player, "<Deck>k__BackingField", originalDeck); Set(Player, "_runPiles", null); }
    }

    static async Task ChaseRestrictedWrapperScopeCases(Harmony harmony, List<MethodBase> patched)
    {
        void Patch(MethodBase method, string prefix)
        { harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(method); }
        Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(ChaseRestrictedAttackLeaf));
        Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" && method.GetParameters().Length == 4), nameof(FormDrawLeaf));
        Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
        foreach (Type current in new[] { typeof(ReptureDualSwordsPower), typeof(ReptureBowPower) })
        {
            FormReset(false, current);
            await ChasePlay(ChaseCard<SwallowReversalHbrgoSaki>());
            Check(Hero.HasPower<ReptureDualSwordsPower>(),
                "the complete native wrapper retains the required dual mode with all capture prefixes installed");
            Check(AsyncValue("CardPlayContextHelper", "Current") == null &&
                AsyncValue("MoreThanOneTruthCombatHelper", "Current") == null &&
                AsyncValue("DualSwordsChaseHelper", "Current") == null,
                "compatible and switching restricted cards close every wrapper scope exactly once");
        }
    }
}
