using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Production grouped powers, reservations, card-roll selection and shield selection.
// Fixed raw-die leaves make percentage arithmetic and dice-count assertions exact.
// Card-only tests isolate their native damage/block/generated-card command leaves.
static partial class EffectChecks
{
    static Type UnifiedBonusType => Mod.GetType("HBRGOSakiMod.Core.Models.Powers.RollBonusPower", true)!;
    static Type UnifiedAdvantageType => Mod.GetType("HBRGOSakiMod.Core.Models.Powers.AdvantageDicePower", true)!;
    static readonly Queue<int> UnifiedRawDice = new();
    static int UnifiedRawReads;
    static Exception? UnifiedRawFailure;
    static bool UnifiedCleanupFailure;
    static CardModel? UnifiedKeywordCard;

    static void UnifiedReset()
    {
        Reset(); As31O(true); Fate(FateJson(bosses: 0)); CombatInProgress(false);
        UnifiedRawDice.Clear(); UnifiedRawReads = 0;
        UnifiedRawFailure = null; UnifiedCleanupFailure = false; UnifiedKeywordCard = null;
    }

    static async Task UnifiedApply(bool advantage, int value, int uses, CardModel? source = null)
    {
        Type type = advantage ? UnifiedAdvantageType : UnifiedBonusType;
        if (!ModelDb.Contains(type))
        {
            ModelDb.Inject(type);
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        MethodInfo method = type.GetMethods(F).Single(method => method.Name == "Apply" && method.GetParameters().Length == 7);
        await (Task)Invoke(method, null, new ThrowingPlayerChoiceContext(), Hero, value, uses, Hero, source, true)!;
    }

    static PowerModel[] UnifiedPowers(bool advantage = false) => Hero.Powers
        .Where(power => (advantage ? UnifiedAdvantageType : UnifiedBonusType).IsInstanceOfType(power)).ToArray();

    static int UnifiedUses(PowerModel power) => (int)power.GetType().GetProperty("RemainingUses", F)!.GetValue(power)!;
    static int UnifiedAvailable(bool advantage = false) => (int)Call("HbrRollPowerHelper",
        advantage ? "GetAvailableExtraDice" : "GetAvailableBonus", Hero)!;
    static object UnifiedReserve() => Call("HbrRollPowerHelper", "Reserve", Hero)!;
    static int UnifiedReserved(object reservation, string property) =>
        (int)reservation.GetType().GetProperty(property, F)!.GetValue(reservation)!;
    static async Task UnifiedConsume(object reservation) =>
        await (Task)Call("HbrRollPowerHelper", "ConsumeReservation", new ThrowingPlayerChoiceContext(), reservation, null)!;

    static void UnifiedGroups(bool advantage, params (int Value, int Uses)[] expected)
    {
        var actual = UnifiedPowers(advantage).Select(power => (power.Amount, UnifiedUses(power)))
            .OrderBy(row => row.Amount).ToArray();
        var wanted = expected.OrderBy(row => row.Value).ToArray();
        Check(actual.SequenceEqual(wanted), $"{(advantage ? "advantage" : "roll bonus")} groups are " +
            string.Join(", ", wanted.Select(row => $"{row.Value}/{row.Uses}")) + "; actual " +
            string.Join(", ", actual.Select(row => $"{row.Amount}/{row.Item2}")));
    }

    static bool UnifiedFixedRaw(ref int __result)
    {
        if (UnifiedRawFailure != null) throw UnifiedRawFailure;
        if (UnifiedRawDice.Count == 0) throw new Exception("Production requested an unexpected extra raw die");
        UnifiedRawReads++;
        __result = UnifiedRawDice.Dequeue();
        return false;
    }

    static void UnifiedDice(params int[] dice)
    {
        UnifiedRawDice.Clear(); UnifiedRawReads = 0;
        foreach (int die in dice) UnifiedRawDice.Enqueue(die);
    }

    static object UnifiedCardRoll(RollGlossaryCapability pipeline, CardModel card, params int[] dice)
    {
        UnifiedDice(dice);
        return Invoke(typeof(RollGlossaryCapability).GetMethod("RollD100", F)!, pipeline,
            false, card, card.Type == CardType.Attack, false, false, null)!;
    }

    static SoulRollBreakdown UnifiedWinning(object result) =>
        (SoulRollBreakdown)result.GetType().GetProperty("WinningRoll", F)!.GetValue(result)!;
    static int UnifiedDiscarded(object result) =>
        ((IEnumerable<SoulRollBreakdown>)result.GetType().GetProperty("DiscardedRolls", F)!.GetValue(result)!).Count();
    static async Task UnifiedFinishCardRoll(RollGlossaryCapability pipeline, CardModel card) =>
        await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("ConsumeReservedRollModifierPowers", F)!, pipeline,
            new ThrowingPlayerChoiceContext(), card)!;

    static async Task UnifiedRollModifierCases()
    {
        var harmony = new Harmony(ProductionId + ".unified-roll-raw-leaves");
        MethodInfo cardRaw = typeof(RollGlossaryCapability).GetMethod("NextRawRoll", F)!;
        MethodInfo shieldRaw = typeof(StrongestShieldPower).GetMethod("RollDeterministicD100", F)!;
        MethodInfo prefix = typeof(EffectChecks).GetMethod(nameof(UnifiedFixedRaw), F)!;
        MethodInfo[] waits = typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") &&
            method.ReturnType == typeof(Task)).ToArray();
        MethodInfo waitPrefix = typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!;
        try
        {
            IsolateCenterVisuals();
            foreach (MethodInfo wait in waits) harmony.Patch(wait, prefix: new HarmonyMethod(waitPrefix));
            harmony.Patch(cardRaw, prefix: new HarmonyMethod(prefix));
            harmony.Patch(shieldRaw, prefix: new HarmonyMethod(prefix));
            await UnifiedGroupCases();
            await UnifiedDescriptionCases();
            await UnifiedCardRollCases();
            await UnifiedForcedRollCases();
            await UnifiedShieldCases();
            await UnifiedFailureCases();
            await UnifiedCardGrantCases();
        }
        finally
        {
            harmony.Unpatch(cardRaw, prefix); harmony.Unpatch(shieldRaw, prefix);
            foreach (MethodInfo wait in waits) harmony.Unpatch(wait, waitPrefix);
            UnifiedRawDice.Clear(); CombatInProgress(false); As31O(false); Reset();
        }
    }

    static async Task UnifiedGroupCases()
    {
        UnifiedReset();
        await UnifiedApply(false, 10, 2);
        await UnifiedApply(false, 10, 3);
        UnifiedGroups(false, (10, 5));
        await UnifiedApply(false, 20, 2);
        UnifiedGroups(false, (10, 5), (20, 2));
        Equal(UnifiedAvailable(), 30m, "different roll-bonus values remain separate but add to +30 percent");
        var card = Owned<DefendIronclad>();
        for (int index = 0; index < 3; index++)
        {
            var preview = ((int FlatBonus, int PercentBonus))StaticCall(typeof(RollGlossaryCapability),
                "GetProjectedAttackRollModifiers", Player, card)!;
            Check(preview.PercentBonus == 30, "repeated production roll preview sees the additive +30 percent");
            UnifiedGroups(false, (10, 5), (20, 2));
        }
        await UnifiedApply(false, -10, 2);
        UnifiedGroups(false, (-10, 2), (10, 5), (20, 2));
        object first = UnifiedReserve();
        Equal(UnifiedReserved(first, "BonusPercent"), 20m, "a reservation sums positive and negative roll-bonus groups");
        await UnifiedConsume(first);
        UnifiedGroups(false, (-10, 1), (10, 4), (20, 1));
        await UnifiedConsume(first);
        UnifiedGroups(false, (-10, 1), (10, 4), (20, 1));
        object second = UnifiedReserve();
        await UnifiedConsume(second);
        UnifiedGroups(false, (10, 3));
        Equal(UnifiedAvailable(), 10m, "only exhausted groups disappear; longer-running groups continue");

        UnifiedReset();
        await UnifiedApply(true, 1, 2); await UnifiedApply(true, 1, 3); await UnifiedApply(true, 2, 2);
        UnifiedGroups(true, (1, 5), (2, 2));
        Equal(UnifiedAvailable(true), 3m, "independent advantage groups add their extra dice, not their durations");
        object advantage = UnifiedReserve();
        Equal(UnifiedReserved(advantage, "ExtraDice"), 3m, "one roll reserves all three extra dice");
        await UnifiedConsume(advantage);
        UnifiedGroups(true, (1, 4), (2, 1));

        // Doubling changes V or K while leaving N unchanged.
        UnifiedReset();
        await UnifiedApply(false, 10, 2); await UnifiedApply(false, 20, 3); await UnifiedApply(false, -10, 1);
        await UnifiedApply(true, 1, 3); await UnifiedApply(true, 2, 2);
        await (Task)Call("HbrRollPowerHelper", "DoubleAll", new ThrowingPlayerChoiceContext(), Hero, Hero, null)!;
        UnifiedGroups(false, (-20, 1), (20, 2), (40, 3));
        UnifiedGroups(true, (2, 3), (4, 2));
        Equal(UnifiedAvailable(), 40m, "doubling signed roll values doubles their sum without doubling uses");

        // Deliberately create a duplicate-value instance as can occur during
        // modification callbacks, then execute the real merge operation.
        UnifiedReset();
        await UnifiedApply(false, 10, 2); await UnifiedApply(false, 20, 3);
        PowerModel changed = UnifiedPowers().Single(power => power.Amount == 10);
        Set(changed, "_amount", 20);
        await (Task)Call("HbrRollPowerHelper", "MergeSameValues", new ThrowingPlayerChoiceContext(), Hero)!;
        UnifiedGroups(false, (20, 5));

        UnifiedReset();
        await UnifiedApply(false, 10, 2); await UnifiedApply(false, 20, 1);
        object beforeMerge = UnifiedReserve();
        Set(UnifiedPowers().Single(power => power.Amount == 10), "_amount", 20);
        await (Task)Call("HbrRollPowerHelper", "MergeSameValues", new ThrowingPlayerChoiceContext(), Hero)!;
        await UnifiedConsume(beforeMerge);
        UnifiedGroups(false, (20, 1));
        await UnifiedConsume(beforeMerge);
        UnifiedGroups(false, (20, 1));

        UnifiedReset();
        await UnifiedApply(false, 10, 2); await UnifiedApply(false, 20, 3); await UnifiedApply(true, 1, 4);
        await UnifiedPlayCard(Owned<DoublePowersHbrgoSaki>(), false);
        UnifiedGroups(false, (20, 2), (40, 3)); UnifiedGroups(true, (2, 4));
    }

    static async Task UnifiedCardRollCases()
    {
        UnifiedReset();
        await UnifiedApply(false, 10, 3); await UnifiedApply(false, 20, 2);
        CardModel card = Owned<DefendIronclad>();
        var pipeline = RollPipeline(card);
        object roll = UnifiedCardRoll(pipeline, card, 50);
        Equal(UnifiedWinning(roll).BaseRoll, 50m, "production card pipeline keeps the raw die unchanged");
        Equal(UnifiedWinning(roll).FinalRoll, 65m, "raw 50 with +10 and +20 produces 65 by addition, not 66 by compounding");
        Check(UnifiedRawReads == 1 && UnifiedDiscarded(roll) == 0, "roll bonuses do not silently add advantage dice");
        UnifiedGroups(false, (10, 3), (20, 2));
        await UnifiedFinishCardRoll(pipeline, card);
        UnifiedGroups(false, (10, 2), (20, 1));

        UnifiedReset();
        await UnifiedApply(false, -10, 2);
        card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
        roll = UnifiedCardRoll(pipeline, card, 50);
        Equal(UnifiedWinning(roll).FinalRoll, 45m, "negative roll bonus reduces raw 50 to 45");
        await UnifiedFinishCardRoll(pipeline, card); UnifiedGroups(false, (-10, 1));

        UnifiedReset();
        await UnifiedApply(true, 1, 3); await UnifiedApply(true, 2, 2);
        Equip(ProtagonistPrivilege);
        card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
        roll = UnifiedCardRoll(pipeline, card, 50, 5, 80, 40, 75);
        Check(UnifiedRawReads == 5 && UnifiedDiscarded(roll) == 4,
            "base die plus three grouped advantage dice plus the protagonist fragment roll five dice");
        Equal(UnifiedWinning(roll).BaseRoll, 80m, "without a critical success the highest advantage raw die wins");
        Equal(UnifiedWinning(roll).FinalRoll, 72m, "the protagonist fragment's minus ten percent still applies to the winning die");
        await UnifiedFinishCardRoll(pipeline, card); UnifiedGroups(true, (1, 2), (2, 1));

        UnifiedReset();
        await UnifiedApply(true, 2, 3); Equip(AllIn, ProtagonistPrivilege);
        card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
        roll = UnifiedCardRoll(pipeline, card, 50);
        Check(UnifiedRawReads == 1 && UnifiedDiscarded(roll) == 0,
            "All In blocks grouped advantage and the protagonist fragment together");
        Equal(UnifiedWinning(roll).FinalRoll, 52m, "All In plus protagonist modifiers add to five percent");
        await UnifiedFinishCardRoll(pipeline, card); UnifiedGroups(true, (2, 2));

        UnifiedReset();
        await UnifiedApply(true, 1, 1); Equip(LuckyNumbers); Power<ReptureDualSwordsPower>(Hero, 1);
        card = Owned<StrikeIronclad>(); pipeline = RollPipeline(card);
        roll = UnifiedCardRoll(pipeline, card, 95, 7);
        Equal(UnifiedWinning(roll).BaseRoll, 7m,
            "an advantage natural critical seven wins over a higher noncritical ninety-five");
        await UnifiedFinishCardRoll(pipeline, card); UnifiedGroups(true);
    }

    static async Task UnifiedDescriptionCases()
    {
        Localization(); UnifiedReset();
        await UnifiedApply(false, -10, 2); await UnifiedApply(true, 2, 3);
        foreach (PowerModel power in UnifiedPowers().Concat(UnifiedPowers(true)))
        {
            Check(power.InstanceType.ToString() == "Instanced", "each counted roll value uses a native instanced power");
            PropertyInfo uses = power.GetType().GetProperty("RemainingUses", F)!;
            Check(uses.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "SavedPropertyAttribute"),
                "remaining uses are serialized independently of the visible power amount");
            Check(power.AllowNegative, "counted roll powers retain signed visible values");
        }
        PowerModel bonus = UnifiedPowers().Single(), advantage = UnifiedPowers(true).Single();
        string bonusText = bonus.Description.GetFormattedText(), advantageText = advantage.Description.GetFormattedText();
        Check(bonusText.Contains("接下来2次") && bonusText.Contains("-10%"),
            "owned roll-bonus description states the actual signed value and remaining uses");
        Check(advantageText.Contains("接下来3次") && advantageText.Contains("额外投2枚"),
            "owned advantage description states the actual extra dice and remaining uses");
        object reservation = UnifiedReserve();
        Check(bonus.Description.GetFormattedText().Contains("接下来1次") &&
            advantage.Description.GetFormattedText().Contains("接下来2次"),
            "owned descriptions update their available uses when a roll is reserved");
        await UnifiedConsume(reservation);
        Check(bonus.Description.GetFormattedText().Contains("接下来1次") &&
            advantage.Description.GetFormattedText().Contains("接下来2次"),
            "cleanup spends the reservation without subtracting the displayed uses a second time");
        foreach (Type type in new[] { UnifiedBonusType, UnifiedAdvantageType })
        {
            string text = ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).Description.GetFormattedText();
            Check(!text.Contains("0次") && !text.Contains("0%") && text.Contains("右上角"),
                "canonical card-hover reference describes the rule without a fake zero value or duration");
        }
        string display = Source("Core/Patches/CountedRollPowerDisplayPatch.cs");
        Check(display.Contains("HarmonyPatch(typeof(NPower), \"RefreshAmount\")") &&
            display.Contains("new Vector2(7f, -7f)") && display.Contains("power.AvailableUses") &&
            !display.Contains("_Process"),
            "the uses overlay is refreshed by the native amount event, reads available uses and is positioned at the upper right");
        Check(Source("Core/Models/Powers/CountedRollPower.cs").Contains("FF7070"),
            "negative counted roll values have a red amount-label color");
    }

    static async Task UnifiedForcedRollCases()
    {
        UnifiedReset();
        await UnifiedApply(false, 10, 2); await UnifiedApply(false, 20, 2); await UnifiedApply(true, 2, 2);
        var critical = Power<NextCriticalSuccessPower>(Hero, 1);
        var beat = Power<HotaruOnlyThisBeatPower>(Hero, 20);
        typeof(HotaruTurnLedgerPower).GetProperty("OrdinaryGrants", F)!.SetValue(beat,
            "[{\"Value\":20,\"Remaining\":3,\"Tag\":0}]");
        typeof(HotaruTurnLedgerPower).GetProperty("OrdinaryTurnNumber", F)!.SetValue(beat,
            Player.PlayerCombatState.TurnNumber);
        var card = Owned<DefendIronclad>(); var pipeline = RollPipeline(card);
        // OnlyThisBeat reads whether combat is ending. A live beta turn state
        // owns engine services absent from this host; isolate this single getter.
        var endingIsolation = new Harmony(ProductionId + ".unified-forced-roll-ending-leaf");
        MethodInfo ending = AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding");
        MethodInfo notEnding = typeof(EffectChecks).GetMethod(nameof(UnifiedNotEnding), F)!;
        endingIsolation.Patch(ending, prefix: new HarmonyMethod(notEnding));
        try
        {
            Equal((int)Call("HotaruRareRollHelper", "GetRollPercent", Player)!, 20m,
                "the forced-roll fixture has a real active twenty-percent Only This Beat ledger");
            object result = UnifiedCardRoll(pipeline, card);
            Equal(UnifiedWinning(result).FinalRoll, 150m,
                "forced critical roll adds thirty-percent counted bonuses and twenty-percent Only This Beat on one basis");
            Check(UnifiedRawReads == 0 && UnifiedDiscarded(result) == 0,
                "a forced critical result bypasses raw dice without generating pointless advantage dice");
            await UnifiedFinishCardRoll(pipeline, card);
            UnifiedGroups(false, (10, 1), (20, 1)); UnifiedGroups(true, (2, 1));
            Check(critical.Amount == 0 || !Hero.Powers.Contains(critical),
                "the forced critical judgement consumes its own critical guarantee as well as the counted uses");
        }
        finally { endingIsolation.Unpatch(ending, notEnding); CombatInProgress(false); }

        UnifiedReset(); await UnifiedApply(false, 20, 2); await UnifiedApply(true, 2, 2);
        card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
        pipeline.SetRollsFixedToOneHundred(true);
        try
        {
            object result = UnifiedCardRoll(pipeline, card);
            Equal(UnifiedWinning(result).FinalRoll, 100m,
                "the developer fixed-one-hundred roll remains fixed despite a counted percentage bonus");
            Check(UnifiedRawReads == 0, "the fixed-one-hundred control does not draw native dice");
            await UnifiedFinishCardRoll(pipeline, card);
            UnifiedGroups(false, (20, 1)); UnifiedGroups(true, (2, 1));
        }
        finally { pipeline.SetRollsFixedToOneHundred(false); UnifiedReset(); }
    }

    static bool UnifiedNotEnding(ref bool __result) { __result = false; return false; }

    static async Task UnifiedShieldCases()
    {
        UnifiedReset();
        await UnifiedApply(false, 10, 2); await UnifiedApply(false, 20, 1); await UnifiedApply(true, 2, 1);
        var shield = Power<StrongestShieldPower>(Hero, 1);
        UnifiedDice(50, 40, 30);
        object?[] args = { Hero, Enemy, 6m, ValueProp.Move, null, 0, null };
        var winning = (SoulRollBreakdown)Invoke(typeof(StrongestShieldPower).GetMethod("RollD100", F)!, shield, args)!;
        Equal(winning.BaseRoll, 50m, "shield judgement uses its own deterministic raw roll");
        Equal(winning.FinalRoll, 65m, "shield judgement receives both additive roll-bonus groups");
        Check(UnifiedRawReads == 3 && ((IEnumerable<SoulRollBreakdown>)args[6]!).Count() == 2,
            "shield judgement receives the two grouped advantage dice");
        await (Task)Invoke(typeof(StrongestShieldPower).GetMethod("CompleteIncomingRoll", F)!, shield,
            new ThrowingPlayerChoiceContext(), null)!;
        UnifiedGroups(false, (10, 1)); UnifiedGroups(true);
        await (Task)Invoke(typeof(StrongestShieldPower).GetMethod("CompleteIncomingRoll", F)!, shield,
            new ThrowingPlayerChoiceContext(), null)!;
        UnifiedGroups(false, (10, 1));
    }

    static object UnifiedMarker(RollGlossaryCapability pipeline) =>
        Invoke(typeof(RollGlossaryCapability).GetMethod("CaptureRollReservationMarker", F)!, pipeline)!;
    static async Task UnifiedCleanup(RollGlossaryCapability pipeline, object marker) =>
        await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("CleanupFailedRollReservations", F)!, pipeline, marker)!;
    static int UnifiedPendingUses() => UnifiedPowers().Sum(power =>
        (int)power.GetType().BaseType!.GetProperty("ReservedRollCount", F)!.GetValue(power)!);

    static bool UnifiedEligibleCard(CardModel card, ref bool __result)
    {
        if (!ReferenceEquals(card, UnifiedKeywordCard)) return true;
        __result = true;
        return false;
    }

    static bool UnifiedCannonRegistryLeaf(Player player, CardType? type, ref RollGlossaryCapability __result)
    {
        __result = RollPipeline(Owned<DefendIronclad>());
        Set(__result, "_standalonePlayer", player);
        Set(__result, "_standaloneType", type);
        return false;
    }

    static void UnifiedThrowCleanup()
    {
        if (UnifiedCleanupFailure) throw new ApplicationException("controlled unified-roll cleanup failure");
    }

    static async Task<Exception?> UnifiedCatch(Func<Task> operation)
    {
        try { await operation(); return null; }
        catch (Exception exception) { return exception; }
    }

    static async Task UnifiedCannonRoll()
    {
        await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("RollForCannon", F)!, null,
            new ThrowingPlayerChoiceContext(), Player, CardType.Skill, false, null)!;
    }

    static async Task UnifiedFailureCases()
    {
        UnifiedReset(); await UnifiedApply(false, 10, 5);
        var card = Owned<DefendIronclad>(); var pipeline = RollPipeline(card);
        object initial = UnifiedMarker(pipeline);
        Invoke(typeof(RollGlossaryCapability).GetMethod("BeginCardRollState", F)!, pipeline);
        UnifiedCardRoll(pipeline, card, 50);
        object outer = UnifiedMarker(pipeline);
        Invoke(typeof(RollGlossaryCapability).GetMethod("BeginCardRollState", F)!, pipeline);
        UnifiedCardRoll(pipeline, card, 50);
        Check(UnifiedPendingUses() == 2, "nested card rolls reserve their own independent uses");
        await UnifiedCleanup(pipeline, outer);
        UnifiedGroups(false, (10, 4));
        Check(UnifiedPendingUses() == 1 && ReferenceEquals(UnifiedMarker(pipeline), outer),
            "inner failed-roll cleanup spends its roll and restores the untouched outer reservation");
        await UnifiedCleanup(pipeline, outer);
        UnifiedGroups(false, (10, 4));
        Check(UnifiedPendingUses() == 1, "repeated inner cleanup cannot spend the outer roll");
        await UnifiedCleanup(pipeline, initial);
        UnifiedGroups(false, (10, 3));
        Check(UnifiedPendingUses() == 0, "outer failed-roll cleanup spends only its own started roll");

        UnifiedReset(); await UnifiedApply(false, 10, 5);
        card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
        initial = UnifiedMarker(pipeline);
        Invoke(typeof(RollGlossaryCapability).GetMethod("BeginCardRollState", F)!, pipeline);
        UnifiedCardRoll(pipeline, card, 50); outer = UnifiedMarker(pipeline);
        Invoke(typeof(RollGlossaryCapability).GetMethod("BeginCardRollState", F)!, pipeline);
        UnifiedCardRoll(pipeline, card, 50);
        await UnifiedFinishCardRoll(pipeline, card);
        Invoke(typeof(RollGlossaryCapability).GetMethod("FinishCardRollState", F)!, pipeline);
        await UnifiedCleanup(pipeline, outer);
        UnifiedGroups(false, (10, 4));
        Check(UnifiedPendingUses() == 1 && ReferenceEquals(UnifiedMarker(pipeline), outer),
            "a failure after inner after-play already restored its state cannot consume the outer card");
        await UnifiedCleanup(pipeline, initial);
        UnifiedGroups(false, (10, 3));

        var harmony = new Harmony(ProductionId + ".unified-roll-failure-leaves");
        MethodInfo factory = typeof(RollGlossaryCapability).GetMethod("CreateCannonPipeline", F)!;
        MethodInfo factoryPrefix = typeof(EffectChecks).GetMethod(nameof(UnifiedCannonRegistryLeaf), F)!;
        MethodInfo eligibility = typeof(RollGlossaryCapability).GetMethod("HasEffectiveRollKeyword", F)!;
        MethodInfo eligibilityPrefix = typeof(EffectChecks).GetMethod(nameof(UnifiedEligibleCard), F)!;
        MethodInfo consume = Helper("HbrRollPowerHelper").GetMethod("ConsumeReservation", F)!;
        MethodInfo consumePrefix = typeof(EffectChecks).GetMethod(nameof(UnifiedThrowCleanup), F)!;
        try
        {
            harmony.Patch(factory, prefix: new HarmonyMethod(factoryPrefix));
            harmony.Patch(eligibility, prefix: new HarmonyMethod(eligibilityPrefix));
            harmony.Patch(consume, prefix: new HarmonyMethod(consumePrefix));
            UnifiedReset(); await UnifiedApply(false, 10, 3);
            Exception expected = new InvalidOperationException("controlled cannon raw-roll failure");
            UnifiedRawFailure = expected;
            Exception? actual = await UnifiedCatch(UnifiedCannonRoll);
            Check(ReferenceEquals(actual, expected), "cannon raw-roll exceptions retain their original identity");
            UnifiedGroups(false, (10, 2));
            Check(UnifiedPendingUses() == 0 && UnifiedAvailable() == 10,
                "failed cannon roll consumes exactly one use and clears its reservation");

            UnifiedReset(); await UnifiedApply(false, 10, 4);
            card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
            UnifiedCardRoll(pipeline, card, 50);
            expected = new InvalidOperationException("controlled extra raw-roll failure");
            UnifiedRawFailure = expected; UnifiedKeywordCard = card;
            actual = await UnifiedCatch(async () => { await pipeline.RollExtraD100(new ThrowingPlayerChoiceContext(), card); });
            Check(ReferenceEquals(actual, expected), "extra-roll raw exceptions retain their original identity");
            UnifiedGroups(false, (10, 3));
            Check(UnifiedPendingUses() == 1 && UnifiedAvailable() == 10,
                "failed extra roll spends only its new reservation and leaves the outer card's reserved use");
            UnifiedRawFailure = null;
            await UnifiedFinishCardRoll(pipeline, card);
            UnifiedGroups(false, (10, 2));
            Check(UnifiedPendingUses() == 0, "outer card can finish normally after the extra roll fails");

            UnifiedReset(); await UnifiedApply(false, 10, 3);
            expected = new InvalidOperationException("original cannon failure survives cleanup failure");
            UnifiedRawFailure = expected; UnifiedCleanupFailure = true;
            actual = await UnifiedCatch(UnifiedCannonRoll);
            Check(ReferenceEquals(actual, expected), "an original cannon exception is not replaced by a cleanup exception");
            Check(expected.Data["HBRGOSakiMod.RollCleanupFailure"] is ApplicationException,
                "cannon cleanup failure remains available as diagnostics on the original exception");

            UnifiedReset(); await UnifiedApply(false, 10, 3);
            card = Owned<DefendIronclad>(); pipeline = RollPipeline(card);
            expected = new InvalidOperationException("original extra-roll failure survives cleanup failure");
            UnifiedRawFailure = expected; UnifiedCleanupFailure = true; UnifiedKeywordCard = card;
            actual = await UnifiedCatch(async () => { await pipeline.RollExtraD100(new ThrowingPlayerChoiceContext(), card); });
            Check(ReferenceEquals(actual, expected) && expected.Data["HBRGOSakiMod.RollCleanupFailure"] is ApplicationException,
                "extra-roll cleanup preserves the original exception and attaches its cleanup diagnostic");
        }
        finally
        {
            harmony.Unpatch(factory, factoryPrefix); harmony.Unpatch(eligibility, eligibilityPrefix);
            harmony.Unpatch(consume, consumePrefix); UnifiedReset();
        }
    }

    static bool UnifiedSkipAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        __result = Task.FromResult(__instance);
        return false;
    }

    static bool UnifiedSkipGenerated(CardModel card, PileType newPileType, ref Task<CardPileAddResult> __result)
    {
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card });
        return false;
    }

    static async Task UnifiedPlayCard(CardModel card, bool upgraded)
    {
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        // CardModel.CombatState is available only from a combat pile (or a
        // combat upgrade preview), not merely from an assigned Owner.
        if (card.Pile?.Type != PileType.Play)
            Player.PlayerCombatState.PlayPile.AddInternal(card, -1, true);
        var play = Play(card);
        Set(play, "<Target>k__BackingField", Enemy);
        await (Task)Invoke(card.GetType().GetMethod("OnPlay", F)!, card,
            new ThrowingPlayerChoiceContext(), play)!;
    }

    static async Task UnifiedCardGrantCases()
    {
        var harmony = new Harmony(ProductionId + ".unified-roll-card-command-leaves");
        MethodInfo execute = typeof(AttackCommand).GetMethod("Execute", F)!;
        MethodInfo skipAttack = typeof(EffectChecks).GetMethod(nameof(UnifiedSkipAttack), F)!;
        MethodInfo skipTask = typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!;
        MethodInfo skipGenerated = typeof(EffectChecks).GetMethod(nameof(UnifiedSkipGenerated), F)!;
        MethodInfo[] blocks = typeof(CreatureCmd).GetMethods(F).Where(method => method.Name == "GainBlock" && method.ReturnType == typeof(Task)).ToArray();
        MethodInfo[] generated = typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "AddGeneratedCardToCombat").ToArray();
        try
        {
            harmony.Patch(execute, prefix: new HarmonyMethod(skipAttack));
            foreach (MethodInfo block in blocks) harmony.Patch(block, prefix: new HarmonyMethod(skipTask));
            foreach (MethodInfo method in generated) harmony.Patch(method, prefix: new HarmonyMethod(skipGenerated));
            foreach (Type type in new[] { typeof(MaidCultivationPower), typeof(BufferPower), typeof(StrongestShieldPower),
                         typeof(SwordheartClarityPower), typeof(SwordheartPower), typeof(MeditationHbrgoSaki) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            Set(Combat, "_allCards", new List<CardModel>());
            foreach (bool upgraded in new[] { false, true })
            {
                UnifiedReset();
                var strengthen = Owned<StrengthenHbrgoSaki>();
                await UnifiedPlayCard(strengthen, upgraded);
                UnifiedGroups(false, (upgraded ? 20 : 10, upgraded ? 4 : 3));
                UnifiedGroups(true, (1, upgraded ? 4 : 3));
                Check(Hero.GetPower<StrengthenPower>() == null, "Strengthen no longer creates the retired combined power");

                UnifiedReset(); Power<ReptureDualSwordsPower>(Hero, 1);
                var strike = Owned<RollBoostStrikeHbrgoSaki>();
                await UnifiedPlayCard(strike, upgraded);
                UnifiedGroups(false, (upgraded ? 20 : 10, 1));
                Check(Hero.GetPower<NextRollBonusPower>() == null, "Roll Boost Strike no longer creates the card-only next-roll power");

                UnifiedReset();
                var dodge = Owned<ExtremeDodgeHbrgoSaki>();
                await UnifiedPlayCard(dodge, upgraded);
                UnifiedGroups(false, (-10, 2));
                Check(Hero.GetPower<ExtremeDodgeRollPenaltyPower>() == null, "Extreme Dodge no longer creates its private penalty power");

                UnifiedReset();
                var maid = Owned<MimicMaidCultivationHbrgoSaki>();
                await UnifiedPlayCard(maid, upgraded);
                UnifiedGroups(false, (20, 2));
                var maidPower = Hero.GetPower<MaidCultivationPower>() ??
                    throw new Exception("Maid Cultivation did not create its attack-enhancement power");
                Check(maidPower.Amount == 2, "Maid Cultivation retains two attack-enhancement uses");
                var attack = Owned<StrikeIronclad>();
                Equal((decimal)Invoke(typeof(MaidCultivationPower).GetMethod("GetAvailableOverclockMultiplier", F)!, maidPower)!,
                    upgraded ? 2m : 1.5m, "Maid Cultivation retains its original or upgraded OD multiplier");
                Equal(maidPower.ModifyDamageMultiplicative(Enemy, 6m, ValueProp.Move, Hero, attack), 1.5m,
                    "Maid Cultivation retains its attack damage multiplier");
                Equal(UnifiedAvailable(), 20m, "Maid Cultivation's roll percentage comes from the independent unified power only");

                UnifiedReset();
                var anoko = Owned<AnokoHbrgoSaki>();
                await UnifiedPlayCard(anoko, upgraded);
                Check(Hero.GetPower<ProtagonistPrivilegePower>() == null,
                    "Anoko's actual play no longer grants the retired protagonist power");
                Check(!anoko.DynamicVars.TryGetValue("ProtagonistPrivilegePower", out _),
                    "Anoko's canonical variables no longer contain the protagonist power");
                Check(Hero.GetPowerAmount<BufferPower>() == 2 && Hero.GetPowerAmount<StrongestShieldPower>() == 1,
                    "Anoko retains exactly two Buffer and one Strongest Shield");
                Check(Hero.GetPower<SwordheartClarityPower>() == null && Hero.GetPower<SwordheartPower>() == null &&
                    !anoko.DynamicVars.TryGetValue("SwordheartClarityPower", out _),
                    "Anoko no longer grants Swordheart Clarity or fills swordheart");
            }
        }
        finally
        {
            harmony.Unpatch(execute, skipAttack);
            foreach (MethodInfo block in blocks) harmony.Unpatch(block, skipTask);
            foreach (MethodInfo method in generated) harmony.Unpatch(method, skipGenerated);
            UnifiedReset();
        }
    }
}
