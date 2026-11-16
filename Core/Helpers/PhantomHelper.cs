using System;
using HBRGOSakiMod.Core.Compatibility;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomHelper
{
    private const string SceneBossReviveMoveId = "RESPAWN_MOVE";
    private sealed record RawSummonedMinionHealth(decimal Maximum);
    private static readonly ConditionalWeakTable<Creature, RawSummonedMinionHealth> RawMinionHealth = new();

    internal static bool IsOwnedMainPhantom(Creature creature, Player owner)
    {
        return owner != null && creature?.PetOwner == owner &&
            creature.GetPower<PhantomPower>() is { IsSummonedMinion: false } &&
            !creature.HasPower<MinionPower>();
    }

    internal static IEnumerable<Creature> GetLivingPhantoms(Player owner)
    {
        return owner?.Creature?.CombatState?.Allies.Where(creature =>
                creature.IsAlive &&
                IsOwnedMainPhantom(creature, owner)) ??
            Enumerable.Empty<Creature>();
    }

    internal static bool IsValidPhantomTarget(Creature target)
    {
        return target != null &&
            target.IsAlive &&
            target.IsHittable;
    }

    internal static bool IsPhantomMinion(Creature creature)
    {
        return creature?.GetPower<PhantomPower>()?.IsSummonedMinion == true ||
            creature?.GetPower<MinionPower>()?.Applier?
            .HasPower<PhantomPower>() == true;
    }

    internal static Creature GetDamageDefender(Player owner)
    {
        if (owner == null)
        {
            return null;
        }

        return GetLivingPhantoms(owner).FirstOrDefault(creature => creature.IsHittable) ??
            owner.Creature?.CombatState?.Allies.FirstOrDefault(creature =>
                creature.PetOwner == owner && IsPhantomMinion(creature) &&
                IsValidPhantomTarget(creature));
    }

    internal static bool IsMinionOf(
        Creature leader,
        Creature creature)
    {
        return leader != null &&
            creature?.GetPower<MinionPower>()?.Applier == leader;
    }

    internal static Creature GetLivingPhantom(Player owner)
    {
        return GetLivingPhantoms(owner).FirstOrDefault();
    }

    internal static decimal ApplyMaximumDpLoss(Creature creature, decimal unadjustedMaximum)
    {
        decimal lost = IsOwnedMainPhantom(creature, creature?.PetOwner)
            ? creature.PetOwner.GetRelic<MomentOfReign>()?.ExactPhantomLostMaxDp ?? 0m
            : 0m;
        return BigCombatNumbers.ClampStat(unadjustedMaximum - lost);
    }

    internal static Task LoseHp(Creature phantom, decimal amount)
    {
        if (phantom?.IsAlive != true || amount <= 0m)
        {
            return Task.CompletedTask;
        }

        // HP costs bypass damage mitigation and damage triggers, but retain HP/death hooks.
        return CreatureCmd.SetCurrentHp(phantom,
            Math.Max(0m, LargeCreatureHealthHelper.GetCurrentHp(phantom) - HbrResourceValueHelper.Fixed(phantom, amount)));
    }

    internal static async Task RestoreDp(
        PlayerChoiceContext choiceContext, Creature phantom, decimal amount, CardModel cardSource)
    {
        amount = YanagiButlerCombatHelper.ScaleRecovery(cardSource, amount);
        if (phantom?.IsAlive != true || amount <= 0)
        {
            return;
        }

        HighDimensionalShellPower shell = phantom.GetPower<HighDimensionalShellPower>();
        decimal before = shell?.CurrentDp ?? 0m;
        if (shell != null)
        {
            shell.Restore(amount);
        }
        else
        {
            // A broken DP shield has been removed and must be reapplied.
            await PowerCmd.Apply<PhantomHighDimensionalShellPower>(choiceContext, phantom,
                Math.Min(amount, PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom)),
                cardSource?.Owner?.Creature ?? phantom.PetOwner?.Creature ?? phantom, cardSource);
        }
        MariaRepentanceCombatHelper.OnDpRestored(phantom,
            (phantom.GetPower<HighDimensionalShellPower>()?.CurrentDp ?? 0m) - before, cardSource);
    }

    internal static int GetActionsPerformed(Player owner)
    {
        return GetLivingPhantom(owner)?
            .GetPower<PhantomPower>()?
            .ActionsPerformed ?? 0;
    }

    internal static bool IsPendingSceneBossRevive(Creature phantom)
    {
        return phantom?.IsDead == true &&
            (phantom.Monster is IHbrgoRevivingStaticMonster
             {
                 ShouldSuppressDeathFade: true
             } || HasNativeRevival(phantom)) &&
            phantom.Monster.NextMove?.Id == SceneBossReviveMoveId;
    }

    internal static bool HasNativeRevival(Creature creature) =>
        creature?.Monster is TestSubject && creature.HasPower<AdaptablePower>();

    internal static bool CountsAsFinalDefeat(Creature creature)
    {
        return creature != null &&
            !HasNativeRevival(creature) &&
            creature.Monster is not IHbrgoRevivingStaticMonster
            {
                HasRemainingRevives: true
            };
    }

    internal static decimal GetDamageBonusMultiplier(Creature phantom)
    {
        if (phantom?.GetPower<PhantomPower>() is not { IsSummonedMinion: false })
        {
            return 1m;
        }

        return phantom.PetOwner?.GetRelic<MomentOfReign>()?
            .GetActiveRosterEntry()?.DamageBonusMultiplier ??
            PhantomFusionEffectHelper.GetDamageBonusMultiplier(1);
    }

    internal static (decimal PermanentPercent, decimal TemporaryPercent)
        GetDamageBonusBreakdown(Creature phantom)
    {
        if (phantom?.PetOwner is not { } player)
        {
            return (0m, 0m);
        }

        PhantomPower phantomPower = phantom.GetPower<PhantomPower>();
        decimal multiplier = GetDamageBonusMultiplier(phantom);
        decimal permanent = phantomPower?.IsSummonedMinion == true
            ? PhantomFusionEffectHelper.GetBaseDamageBonusPercent(1) +
                Math.Max(0, phantomPower.SummonedMinionLevel - 1) *
                PhantomRosterEntry.LevelDamageBonusPercent
            : player.GetRelic<MomentOfReign>()?
                .GetActiveRosterEntry()?
                .DamageBonusPercent ??
                PhantomFusionEffectHelper.GetBaseDamageBonusPercent(1) * multiplier;
        decimal temporary = (phantomPower?.GetTemporaryDamageBonusPercent() ?? 0m) +
            (phantom.GetPower<PhantomTemporaryLevelPower>()?.GetDamageBonusPercent() ?? 0m);
        return (permanent, temporary);
    }

    internal static decimal GetDamageBonusPercent(Creature phantom) =>
        TemporaryDamageBonusHelper.CalculateEffectiveBonusPercent(phantom);

    internal static decimal GetDamageReductionPercent(Creature phantom)
    {
        if (phantom?.PetOwner is not { } player)
        {
            return 0m;
        }

        PhantomPower power = phantom.GetPower<PhantomPower>();
        decimal reduction = power?.IsSummonedMinion == true
            ? Math.Max(0, power.SummonedMinionLevel - 1) *
                PhantomRosterEntry.LevelDamageReductionPercent
            : player.GetRelic<MomentOfReign>()?
                .GetActiveRosterEntry()?.DamageReductionPercent ?? 0m;
        reduction += phantom.GetPower<PhantomTemporaryLevelPower>()?
            .GetDamageReductionPercent() ?? 0m;
        if (power is { IsSummonedMinion: false, PlayerTurnsStarted: > 0 } &&
            power.PlayerTurnsStarted % 2 == 0)
        {
            reduction += PhantomFusionEffectHelper.GetEffect(
                GetFusionEffectData(phantom),
                PhantomFusionEffectType.Rocket,
                20m,
                10m);
        }

        return Math.Clamp(reduction, 0m, PhantomRosterEntry.MaximumDamageReductionPercent);
    }

    internal static int GetSummonerRollBonus(Player player)
    {
        Creature phantom = GetLivingPhantoms(player)
            .FirstOrDefault(creature => IsOwnedMainPhantom(creature, player));
        if (phantom == null)
        {
            return 0;
        }

        decimal bonus = (player.GetRelic<MomentOfReign>()?.GetActiveRosterEntry()?.SummonerRollBonus ?? 0m) +
            (phantom.GetPower<PhantomTemporaryLevelPower>()?.GetSummonerRollBonus() ?? 0m);
        return (int)Math.Clamp(decimal.Floor(bonus), 0m, int.MaxValue);
    }

    internal static async Task PerformCurrentIntent(
        Creature phantom,
        Creature preferredAttackTarget = null,
        int additionalAttackSegments = 0,
        bool isImmediateAction = true)
    {
        ICombatState combatState = phantom?.CombatState;
        MonsterModel monster = phantom?.Monster;
        bool isPendingSceneBossRevive =
            IsPendingSceneBossRevive(phantom);
        if (combatState == null ||
            monster == null ||
            (!phantom.IsAlive && !isPendingSceneBossRevive) ||
            monster.IsPerformingMove)
        {
            return;
        }

        using IDisposable ascensionScope =
            PhantomAscensionHelper.BeginPhantomEvaluation();

        IReadOnlyList<Creature> targets = combatState
            .GetOpponentsOf(phantom)
            .Where(IsValidPhantomTarget)
            .ToArray();
        PhantomQueenHelper.RefreshCurrentMove(phantom);
        MoveState move = monster.NextMove;
        if (move == null)
        {
            return;
        }

        if (isPendingSceneBossRevive)
        {
            using PhantomPlayerBenefitContext.Scope benefitScope =
                PhantomPlayerBenefitContext.Begin(phantom, targets);
            await move.PerformMove(targets);
            await benefitScope.Flush();
            await ThirtyAClassRelicsHelper.AfterAction(phantom);
            monster.MoveStateMachine?.OnMovePerformed(move);
            if (phantom.IsAlive && combatState.ContainsCreature(phantom))
            {
                phantom.PrepareForNextTurn(
                    combatState.GetOpponentsOf(phantom)
                        .Where(IsValidPhantomTarget));
            }

            return;
        }

        if (targets.Count == 0)
        {
            return;
        }

        PhantomPower phantomPower = phantom.GetPower<PhantomPower>();
        (string copiedMoveId, int copiedMoveCount) =
            phantomPower?.ConsumeCopiedIntents() ??
            (string.Empty, 0);
        MoveState copiedMove = ResolveCopiedMove(
            monster,
            copiedMoveId,
            move);
        List<MoveState> movesToPerform = new(
            1 + copiedMoveCount)
        {
            move
        };
        for (int index = 0; index < copiedMoveCount; index++)
        {
            movesToPerform.Add(copiedMove);
        }

        await ApplyPendingFusionEffectsBeforeAction(
            phantom,
            phantomPower);
        bool performedAttack = false;
        foreach (MoveState intentMove in movesToPerform)
        {
            // A self-destructing move (or retaliation) can end this phantom's
            // life before copied intents get their turn.
            if (!phantom.IsAlive || !combatState.ContainsCreature(phantom))
                break;

            (bool wasAttack, bool wasBuff) =
                await PerformPhantomIntent(
                    phantom,
                    targets,
                    intentMove,
                    phantomPower,
                    preferredAttackTarget,
                    additionalAttackSegments);
            performedAttack |= wasAttack;
            phantomPower?.RecordIntent(wasAttack, wasBuff);
            await ApplyFusionEffectsAfterIntent(
                phantom,
                phantomPower,
                wasAttack,
                wasBuff);
        }

        phantomPower?.RecordAction();
        await ApplyFusionEffectsAfterAction(
            phantom,
            phantomPower);
        await NotifyPhantomActionCompleted(
            phantom,
            performedAttack);

        // An extra action advances sleep once, even with copied intents. Normal
        // turn-end actions leave the decrement to the native AsleepPower hooks.
        if (isImmediateAction)
            await PhantomSleepHelper.AdvanceAfterImmediateAction(phantom);

        monster.MoveStateMachine?.OnMovePerformed(move);
        if (phantom.IsAlive && combatState.ContainsCreature(phantom))
        {
            phantom.PrepareForNextTurn(
                combatState.GetOpponentsOf(phantom)
                    .Where(IsValidPhantomTarget));
        }
    }

    private static MoveState ResolveCopiedMove(
        MonsterModel monster,
        string copiedMoveId,
        MoveState fallback)
    {
        return !string.IsNullOrEmpty(copiedMoveId) &&
            monster.MoveStateMachine?.States.TryGetValue(
                copiedMoveId,
                out MonsterState copiedState) == true &&
            copiedState is MoveState copiedMove
                ? copiedMove
                : fallback;
    }

    private static async Task<(bool WasAttack, bool WasBuff)>
        PerformPhantomIntent(
            Creature phantom,
            IReadOnlyList<Creature> targets,
            MoveState move,
            PhantomPower phantomPower,
            Creature preferredAttackTarget,
            int additionalAttackSegments)
    {
        // Every native secondary effect receives opponents, never the
        // summoner or another allied phantom, even during an intent replay.
        targets = targets.Where(target =>
                IsValidPhantomTarget(target) &&
                target.Side != phantom.Side &&
                ReferenceEquals(target.CombatState, phantom.CombatState))
            .Distinct()
            .ToArray();
        using PhantomPlayerBenefitContext.Scope benefitScope =
            PhantomPlayerBenefitContext.Begin(phantom, targets);
        AttackIntent[] attackIntents = move.Intents
            .OfType<AttackIntent>()
            .ToArray();
        bool wasAttack = attackIntents.Length > 0;
        bool wasBuff = move.Intents.OfType<BuffIntent>().Any();
        if (wasAttack && phantomPower?.IsSummonedMinion == false)
        {
            decimal attackIntentDamageBonus =
                PhantomFusionEffectHelper.GetEffect(
                    GetFusionEffectData(phantom),
                    PhantomFusionEffectType.SnappingJaxfruit,
                    2m,
                    1m);
            if (attackIntentDamageBonus > 0m)
            {
                phantomPower.AddCombatDamageBonus(
                    attackIntentDamageBonus);
            }
        }

        if (wasAttack)
        {
            PhantomAttackToBlockPower attackToBlock =
                phantom.GetPower<PhantomAttackToBlockPower>();
            using var comboAttack = attackToBlock == null ? HbrComboHelper.BeginAttack(phantom) : null;
            try
            {
                IReadOnlyList<decimal> segments = BuildAttackSegments(
                    phantom,
                    attackIntents,
                    additionalAttackSegments,
                    applyCombo: attackToBlock == null);
                int poisonPerSegment = 0;
                if (phantomPower?.IsSummonedMinion == false &&
                    (phantomPower.AttackIntentsPerformed + 1) % 3 == 0)
                {
                    poisonPerSegment = GetFusionEffectAmount(
                        GetFusionEffectData(phantom),
                        PhantomFusionEffectType.Myte,
                        3m,
                        1m);
                }

                PhantomSelfDestructHelper.BeforeAttack(phantom, move);
                await PhantomThornsHelper.BeforeAttack(phantom, move);
                PhantomDebuffIntentHelper.BeforeAttack(phantom, move);
                await PhantomThievingHopperHelper.BeforeAttack(phantom, move);
                if (phantom.Monster is RedCrimsonBossMonster rayBoss)
                {
                    await rayBoss.BeforePhantomIntentPerformed(move, targets);
                }
                else if (phantom.Monster is FlatHand preAttackFlatHand)
                {
                    await preAttackFlatHand.BeforePhantomIntentPerformed(move, targets);
                }
                else if (phantom.Monster is SkullFeather preAttackSkullFeather)
                {
                    await preAttackSkullFeather.BeforePhantomIntentPerformed(move, targets);
                }

                IReadOnlyList<Creature> attackedTargets = Array.Empty<Creature>();
                // Which targets took damage past their block, for move-table
                // monsters whose statuses attach only then.
                using HbrEnemyHitTracker.Scope hitScope = HbrEnemyHitTracker.Begin();
                if (attackToBlock != null)
                {
                    await PerformAttackIntentAsBlock(
                        phantom,
                        targets,
                        segments,
                        attackToBlock.BonusBlock);
                    if (phantom.Monster is RedCrimson &&
                        move.Id == "DELTA_SCRAPE_MOVE")
                    {
                        Creature blockTarget = preferredAttackTarget != null &&
                            targets.Contains(preferredAttackTarget)
                                ? preferredAttackTarget
                                : SelectRandomAttackTarget(phantom, targets);
                        attackedTargets = phantom.HasPower<PhantomNextAttackAllEnemiesPower>()
                            ? targets
                            : blockTarget == null
                                ? Array.Empty<Creature>()
                                : new[] { blockTarget };
                    }
                }
                else
                {
                    // A move-table monster's original all-unit skill hits every
                    // enemy; its lowest-DP skill picks the enemy with the least DP.
                    HbrgoMoveTarget? tableTarget = HbrgoMoveTableMonster.MoveTargetOf(phantom.Monster, move.Id);
                    attackedTargets = await PerformAttackIntentOn(
                        phantom,
                        targets,
                        segments,
                        poisonPerSegment,
                        tableTarget == HbrgoMoveTarget.MinimumDp && preferredAttackTarget == null
                            ? LowestDpTarget(targets)
                            : preferredAttackTarget,
                        tableTarget == HbrgoMoveTarget.All);
                    await ResolveKillKissFusions(phantom);
                }

                await PhantomThornsHelper.AfterAttack(phantom, move);
                await PhantomCeremonialBeastHelper.AfterAttack(phantom, move);
                await PhantomKaiserClawHelper.AfterAttack(phantom, move, targets);
                await PhantomDebuffIntentHelper.AfterAttack(phantom, move, targets);
                if (wasBuff)
                {
                    // Damage is replayed from the intent rather than its native
                    // move. Restore the move's accompanying effects once, not
                    // once per hit or by executing the attack a second time.
                    await PhantomMixedIntentHelper.AfterAttack(phantom, move, targets);
                }

                if (phantom.Monster is
                    RedCrimsonBossMonster redCrimson)
                {
                    await redCrimson.AfterPhantomIntentPerformed(move);
                }
                else if (phantom.Monster is TezukaSaki tezuka)
                {
                    await tezuka.AfterPhantomIntentPerformed(move,
                        targets.Where(IsValidPhantomTarget).ToArray());
                }
                else if (phantom.Monster is RedCrimson redCrimsonPet)
                {
                    await redCrimsonPet.AfterPhantomIntentPerformed(
                        move, targets, attackedTargets);
                }
                else if (phantom.Monster is SkullFeather skullFeather)
                {
                    await skullFeather.AfterPhantomIntentPerformed(move, targets);
                }
                else if (phantom.Monster is FlatHand flatHand)
                {
                    await flatHand.AfterPhantomIntentPerformed(move, targets);
                }
                else if (phantom.Monster is NararaShibaBossMonster narara)
                {
                    await narara.AfterPhantomIntentPerformed(move, targets);
                }
                else if (phantom.Monster is IHbrgoPhantomAttackResults attackResults)
                {
                    IReadOnlyList<Creature> hitTargets = attackedTargets
                        .Where(IsValidPhantomTarget).ToArray();
                    await attackResults.AfterPhantomAttack(move, hitTargets,
                        hitTargets.Where(hitScope.Passed).ToArray(), hitTargets.Sum(hitScope.Amount));
                }
                else if (phantom.Monster is IHbrgoPhantomAttackEffects attackEffects)
                {
                    await attackEffects.AfterPhantomAttackIntent(move,
                        targets.Where(IsValidPhantomTarget).ToArray());
                }
            }
            finally
            {
                try { await ConsumeNextAttackSegmentPowers(phantom); }
                finally
                {
                    if (comboAttack != null)
                    {
                        try { await comboAttack.Complete(new ThrowingPlayerChoiceContext()); }
                        finally { comboAttack.Dispose(); }
                    }
                }
            }

            await PhantomSelfDestructHelper.AfterAttack(phantom, move);
        }
        else
        {
            using PhantomMinionSummonContext.Scope summonScope =
                PhantomMinionSummonContext.Begin(phantom);
            if (!await PhantomKnowledgeIntentHelper.TryPerform(phantom, move, targets) &&
                !await PhantomPlayerOnlyIntentHelper.TryPerform(phantom, move, targets))
            {
                await PhantomThornsHelper.PerformBuffMove(phantom, move, targets);
            }
            foreach (Creature minion in summonScope.CreatedCreatures)
            {
                await InitializeSummonedMinion(
                    phantom,
                    minion,
                    1,
                    configureHealth: false);
            }
        }

        await benefitScope.Flush();
        return (wasAttack, wasBuff);
    }

    internal static decimal GetCurrentAttackIntentTotal(
        Creature phantom,
        Creature target)
    {
        if (phantom?.Monster?.NextMove == null || target == null)
        {
            return 0m;
        }

        IReadOnlyList<decimal> segments = BuildAttackSegments(
            phantom,
            phantom.Monster.NextMove.Intents
                .OfType<AttackIntent>()
                .ToArray());
        return segments.Sum(segment =>
            GetModifiedIntentDamage(phantom, target, segment));
    }

    internal static IReadOnlyList<int>
        GetCurrentAttackIntentPreviewSegments(Creature phantom)
    {
        if (phantom?.Monster?.NextMove == null)
        {
            return Array.Empty<int>();
        }

        IReadOnlyList<decimal> segments = BuildAttackSegments(
            phantom,
            phantom.Monster.NextMove.Intents
                .OfType<AttackIntent>()
                .ToArray());
        Creature target = phantom.CombatState?
            .GetOpponentsOf(phantom)
            .FirstOrDefault(IsValidPhantomTarget);
        // Every segment in this synchronous preview has the same dealer,
        // target, props and hook mode. Repeated base damage therefore needs
        // only one hook evaluation; never retain it across previews or hits.
        // Intent refreshes can also be nested inside a real damage notification.
        StrongestShieldDamagePatch.BeginIntentPreview();
        try
        {
            return PhantomIntentDamagePreview.Calculate(segments,
                segment => target == null
                    ? segment
                    : GetModifiedIntentDamage(phantom, target, segment));
        }
        finally
        {
            StrongestShieldDamagePatch.EndIntentPreview();
        }
    }

    internal static bool HasCurrentAttackIntent(Creature phantom)
    {
        return phantom?.Monster?.NextMove?.Intents
            .OfType<AttackIntent>()
            .Any() == true;
    }

    internal static bool IsSummonerOwnedDamageModifier(
        AbstractModel model,
        Creature dealer)
    {
        if (dealer?.PetOwner is not { } summoner ||
            !dealer.HasPower<PhantomPower>())
        {
            return false;
        }

        return model switch
        {
            // OD affects the whole allied side, including the summoner's phantom.
            Models.Relics.Overclock => false,
            PowerModel power => ReferenceEquals(
                power.Owner,
                summoner.Creature),
            RelicModel relic => ReferenceEquals(relic.Owner, summoner),
            CardModel card => ReferenceEquals(card.Owner, summoner),
            _ => false
        };
    }

    private static Creature LowestDpTarget(IReadOnlyList<Creature> targets) =>
        targets.Where(IsValidPhantomTarget)
            .OrderBy(target => target.GetPower<HighDimensionalShellPower>()?.CurrentDp ?? 0m)
            .FirstOrDefault();

    private static Task<IReadOnlyList<Creature>> PerformAttackIntent(
        Creature phantom,
        IReadOnlyList<Creature> targets,
        IReadOnlyList<decimal> segments,
        int poisonPerSegment,
        Creature preferredTarget)
    {
        return PerformAttackIntentOn(
            phantom,
            targets,
            segments,
            poisonPerSegment,
            preferredTarget,
            allUnitMove: false);
    }

    // allUnitMove: a move-table monster's original all-unit skill hits every
    // enemy, as the 30A sweep does.
    private static async Task<IReadOnlyList<Creature>> PerformAttackIntentOn(
        Creature phantom,
        IReadOnlyList<Creature> targets,
        IReadOnlyList<decimal> segments,
        int poisonPerSegment,
        Creature preferredTarget,
        bool allUnitMove)
    {
        List<Creature> attackedTargets = new();
        bool attackAllEnemies = allUnitMove ||
            phantom.HasPower<PhantomNextAttackAllEnemiesPower>();
        Creature target = !attackAllEnemies &&
            preferredTarget != null &&
            targets.Contains(preferredTarget) &&
            IsValidPhantomTarget(preferredTarget)
                ? preferredTarget
                : attackAllEnemies
                    ? null
                    : SelectRandomAttackTarget(phantom, targets);
        VigorPower vigor = phantom.GetPower<VigorPower>();
        int vigorAmountWhenIntentStarted = vigor?.Amount ?? 0;
        bool dealtUnblockedDamage = false;
        using var damageBatch = HighHitDamageVisualHelper.BeginDirectDamageBatch(
            segments.Count, phantom, null, ValueProp.Move);
        using var checkmate = segments.Count > 0 && (attackAllEnemies || target != null)
            ? ThirtyAClassRelicsHelper.BeginAttack(phantom, damageBatch) : null;
        using var destructionSkill = HbrBattleStatsHelper.IsEnabled(phantom)
            ? HbrDestructionGainHelper.BeginSkill(phantom, null, segments.Count) : null;
        try
        {
            await CreatureCmd.TriggerAnim(phantom, "Attack", 0.15f);
            for (int index = 0; index < segments.Count; index++)
            {
                if (attackAllEnemies)
                {
                    Creature[] livingTargets = phantom.CombatState
                        .GetOpponentsOf(phantom)
                        .Where(IsValidPhantomTarget)
                        .ToArray();
                    if (livingTargets.Length == 0)
                    {
                        return attackedTargets;
                    }

                    foreach (Creature currentTarget in livingTargets)
                    {
                        if (!attackedTargets.Contains(currentTarget))
                        {
                            attackedTargets.Add(currentTarget);
                        }
                        IEnumerable<DamageResult> areaResults =
                            await CreatureCommandCompatibility.Damage(
                                new ThrowingPlayerChoiceContext(),
                                currentTarget,
                                segments[index],
                                ValueProp.Move,
                                phantom,
                                null);
                        foreach (DamageResult result in areaResults)
                        {
                            HbrComboHelper.MarkHit(phantom);
                            dealtUnblockedDamage |= result.UnblockedDamage > 0;
                        }

                        if (poisonPerSegment > 0 && currentTarget.IsAlive)
                        {
                            await PowerCmd.Apply<PoisonPower>(
                                new ThrowingPlayerChoiceContext(),
                                currentTarget,
                                poisonPerSegment,
                                phantom,
                                null);
                        }
                    }

                    continue;
                }

                if (target == null)
                {
                    return attackedTargets;
                }

                if (!attackedTargets.Contains(target))
                {
                    attackedTargets.Add(target);
                }
                IEnumerable<DamageResult> results =
                    await CreatureCommandCompatibility.Damage(
                    new ThrowingPlayerChoiceContext(),
                    target,
                    segments[index],
                    ValueProp.Move,
                    phantom,
                    null);
                foreach (DamageResult result in results)
                {
                    HbrComboHelper.MarkHit(phantom);
                    dealtUnblockedDamage |= result.UnblockedDamage > 0;
                }

                if (poisonPerSegment > 0 && target.IsAlive)
                {
                    await PowerCmd.Apply<PoisonPower>(
                        new ThrowingPlayerChoiceContext(),
                        target,
                        poisonPerSegment,
                        phantom,
                        null);
                }

                if (!target.IsAlive && index + 1 < segments.Count)
                {
                    target = SelectRandomAttackTarget(
                        phantom,
                        targets);
                }
            }
        }
        finally
        {
            if (vigorAmountWhenIntentStarted > 0 &&
                ReferenceEquals(
                    phantom.GetPower<VigorPower>(),
                    vigor))
            {
                await PowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(),
                    vigor,
                    -vigorAmountWhenIntentStarted,
                    null,
                    null);
            }
        }

        SuckPower suck = phantom.GetPower<SuckPower>();
        if (dealtUnblockedDamage &&
            suck?.Amount > 0 &&
            phantom.IsAlive)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                phantom,
                suck.Amount,
                phantom,
                null);
        }

        return attackedTargets;
    }

    private static Creature SelectRandomAttackTarget(
        Creature phantom,
        IReadOnlyList<Creature> originalTargets)
    {
        ICombatState combatState = phantom?.CombatState;
        if (combatState?.RunState == null)
        {
            return null;
        }

        Creature[] livingTargets = originalTargets
            .Where(IsValidPhantomTarget)
            .ToArray();
        return livingTargets.Length > 0
            ? combatState.RunState.Rng.CombatTargets
                .NextItem(livingTargets)
            : null;
    }

    private static async Task PerformAttackIntentAsBlock(
        Creature phantom,
        IReadOnlyList<Creature> targets,
        IReadOnlyList<decimal> segments,
        int bonusBlock)
    {
        Creature previewTarget = targets.FirstOrDefault();
        decimal total = previewTarget == null
            ? segments.Sum()
            : segments.Sum(segment =>
                GetModifiedIntentDamage(
                    phantom,
                    previewTarget,
                    segment));
        decimal block = Math.Min(999_999_999m,
            Math.Max(0m, Math.Floor(total / 2m) + HbrSkillBlockHelper.ResolveUnpowered(phantom, Math.Max(0, bonusBlock))));
        if (block <= 0)
        {
            return;
        }

        await CreatureCmd.GainBlock(
            phantom,
            block,
            ValueProp.Unpowered,
            null);
    }

    private static IReadOnlyList<decimal> BuildAttackSegments(
        Creature phantom,
        IReadOnlyList<AttackIntent> attackIntents,
        int additionalAttackSegments = 0,
        bool applyCombo = true)
    {
        const int maximumSegments = 1000;
        List<decimal> segments = new();
        foreach (AttackIntent intent in attackIntents)
        {
            decimal damage = Math.Max(
                0m,
                intent.DamageCalc?.Invoke() ?? 0m);
            int repeats = Math.Clamp(
                Math.Max(1, intent.Repeats),
                1,
                maximumSegments - segments.Count);
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                segments.Add(damage);
            }

            if (segments.Count >= maximumSegments)
            {
                break;
            }
        }

        if (segments.Count == 0)
        {
            return segments;
        }

        PhantomPower phantomPower = phantom.GetPower<PhantomPower>();
        string fusionEffectData = GetFusionEffectData(phantom);
        decimal forgottenScaling =
            PhantomFusionEffectHelper.GetEffect(
                fusionEffectData,
                PhantomFusionEffectType.TheForgotten,
                1m,
                0m);
        decimal ownerDexterity =
            phantom.PetOwner?.Creature?
                .GetPowerAmount<DexterityPower>() ?? 0;
        if (forgottenScaling > 0m && ownerDexterity != 0m)
        {
            decimal bonus = ownerDexterity * forgottenScaling;
            for (int index = 0; index < segments.Count; index++)
            {
                segments[index] = Math.Max(0m, segments[index] + bonus);
            }
        }

        bool isOriginalMultiAttack = segments.Count > 1;
        if (isOriginalMultiAttack)
        {
            AddRepeatedSegments(
                segments,
                PhantomFusionEffectHelper.ToInt(
                    PhantomFusionEffectHelper.GetEffect(
                        fusionEffectData,
                        PhantomFusionEffectType.TrackerRubyRaider,
                        1m,
                        1m)),
                maximumSegments);
        }

        if (phantomPower?.AttackIntentsPerformed == 0)
        {
            AddRepeatedSegments(
                segments,
                PhantomFusionEffectHelper.ToInt(
                    PhantomFusionEffectHelper.GetEffect(
                        fusionEffectData,
                        PhantomFusionEffectType.Mawler,
                        1m,
                        1m) +
                    PhantomFusionEffectHelper.GetEffect(
                        fusionEffectData,
                        PhantomFusionEffectType.WireSlasher,
                        1m,
                        1m) +
                    PhantomFusionEffectHelper.GetEffect(
                        fusionEffectData,
                        PhantomFusionEffectType.TargetWireFrameKiller,
                        1m,
                        1m) +
                    PhantomFusionEffectHelper.GetEffect(
                        fusionEffectData,
                        PhantomFusionEffectType.BarrelWalker,
                        2m,
                        1m) +
                    PhantomFusionEffectHelper.GetEffect(
                        fusionEffectData,
                        PhantomFusionEffectType.TriClaw,
                        1m,
                        1m)),
                maximumSegments);
        }

        AddRepeatedSegments(
            segments,
            PhantomFusionEffectHelper.ToInt(
                PhantomFusionEffectHelper.GetEffect(
                    fusionEffectData,
                    PhantomFusionEffectType.TurretOperator,
                    1m,
                    1m)),
            maximumSegments);

        if (phantomPower != null &&
            (phantomPower.AttackIntentsPerformed + 1) % 3 == 0)
        {
            decimal bonusDamage = PhantomFusionEffectHelper.GetEffect(
                fusionEffectData,
                PhantomFusionEffectType.AbyssKnocker,
                5m,
                3m) +
                PhantomFusionEffectHelper.GetEffect(
                    fusionEffectData,
                    PhantomFusionEffectType.TargetWireFrame,
                    4m,
                    2m) +
                PhantomFusionEffectHelper.GetEffect(
                    fusionEffectData,
                    PhantomFusionEffectType.DarkKnockerKiller,
                    6m,
                    3m) +
                PhantomFusionEffectHelper.GetEffect(
                    fusionEffectData,
                    PhantomFusionEffectType.AbyssKnockerKiller,
                    8m,
                    4m) +
                PhantomFusionEffectHelper.GetEffect(
                    fusionEffectData,
                    PhantomFusionEffectType.MolArms,
                    10m,
                    5m);
            if (bonusDamage > 0m && segments.Count < maximumSegments)
            {
                segments.Add(bonusDamage);
            }
        }

        int bonusSegments = (int)Math.Min(
            maximumSegments - segments.Count,
            (long)Math.Max(
                0,
                phantom.GetPower<
                    PhantomNextAttackBonusSegmentsPower>()?.Amount ?? 0) +
            Math.Max(0, additionalAttackSegments));
        decimal lastDamage = segments[^1];
        for (int index = 0; index < bonusSegments; index++)
        {
            segments.Add(lastDamage);
        }

        PhantomNextAttackDoublePower doubling =
            phantom.GetPower<PhantomNextAttackDoublePower>();
        bool combo = applyCombo && !phantom.HasPower<PhantomAttackToBlockPower>();
        IReadOnlyList<decimal> transformed = combo
            ? HbrComboHelper.TransformSegments(phantom, segments, doublesOnly: false) : segments;
        transformed = doubling?.TransformSegments(transformed) ?? transformed;
        return combo ? HbrComboHelper.TransformSegments(phantom, transformed, doublesOnly: true) : transformed;
    }

    private static async Task ResolveKillKissFusions(Creature phantom)
    {
        KillKissFusionPower[] pendingFusions = phantom
            .GetPowerInstances<KillKissFusionPower>()
            .ToArray();
        if (pendingFusions.Length == 0)
        {
            return;
        }

        MomentOfReign relic = phantom.PetOwner?
            .GetRelic<MomentOfReign>();
        foreach (KillKissFusionPower pending in pendingFusions)
        {
            if (relic != null)
            {
                await relic.FuseMonsterCopyIntoActivePhantom(
                    pending.TargetMonsterId,
                    pending.CopyLevel);
            }

            await PowerCmd.Remove(pending);
        }
    }

    private static void AddRepeatedSegments(
        ICollection<decimal> segments,
        int amount,
        int maximumSegments)
    {
        if (amount <= 0 || segments.Count == 0)
        {
            return;
        }

        decimal lastDamage = segments.Last();
        int count = Math.Min(
            Math.Max(0, maximumSegments - segments.Count),
            amount);
        for (int index = 0; index < count; index++)
        {
            segments.Add(lastDamage);
        }
    }

    private static decimal GetModifiedIntentDamage(
        Creature phantom,
        Creature target,
        decimal amount)
    {
        if (phantom?.CombatState?.RunState == null)
        {
            return Math.Max(0m, amount);
        }

        decimal modified = HookCompatibility.ModifyDamage(
            phantom.CombatState.RunState,
            phantom.CombatState,
            target,
            phantom,
            amount,
            ValueProp.Move,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out _);
        return Math.Max(0m, modified);
    }

    internal static string GetFusionEffectData(Creature phantom)
    {
        PhantomPower phantomPower = phantom?.GetPower<PhantomPower>();
        if (phantomPower?.IsSummonedMinion == true ||
            phantom?.PetOwner is not { } owner)
        {
            return string.Empty;
        }

        return owner.GetRelic<MomentOfReign>()?
            .GetActiveRosterEntry()?
            .FusionEffectData ?? string.Empty;
    }

    internal static decimal GetExtraMaximumDp(Creature phantom)
    {
        return PhantomFusionEffectHelper.GetExtraMaxDp(
            GetFusionEffectData(phantom));
    }

    internal static async Task RunPhantomEntryEffects(Creature phantom)
    {
        if (phantom?.Monster == null)
        {
            return;
        }

        Creature[] entryMinions;
        using (PhantomMinionSummonContext.Scope summonScope =
               PhantomMinionSummonContext.Begin(phantom))
        {
            // Queen's vanilla entry hook requires an enemy-side amalgam.
            if (phantom.Monster is not Queen)
            {
                await phantom.Monster.AfterAddedToRoom();
            }
            phantom.Monster.InvokeExecutionFinished();
            entryMinions = summonScope.CreatedCreatures.ToArray();
        }

        foreach (Creature minion in entryMinions)
        {
            await InitializeSummonedMinion(
                phantom,
                minion,
                1,
                configureHealth: false);
        }
    }

    internal static async Task SyncFlatHandFusionPower(Creature phantom)
    {
        if (phantom?.GetPower<PhantomPower>() is not { IsSummonedMinion: false })
        {
            return;
        }

        int layers = GetFusionEffectAmount(
            GetFusionEffectData(phantom),
            PhantomFusionEffectType.FlatHand,
            1m,
            1m);
        PseudoSpaceWarpPower current = phantom.GetPower<PseudoSpaceWarpPower>();
        if (layers <= 0)
        {
            if (current != null)
            {
                await PowerCmd.Remove(current);
            }
            return;
        }

        int delta = layers - (current?.Amount ?? 0);
        if (delta != 0)
        {
            await PowerCmd.Apply<PseudoSpaceWarpPower>(
                new BlockingPlayerChoiceContext(),
                phantom,
                delta,
                phantom,
                null);
        }
    }

    internal static async Task ApplyOnSummonFusionEffects(
        Creature phantom)
    {
        await SyncFlatHandFusionPower(phantom);
        string effects = GetFusionEffectData(phantom);
        if (string.IsNullOrEmpty(effects))
        {
            return;
        }

        await ApplyPowerIfPositive<DoubleDamagePower>(
            phantom,
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.SoulNexus,
                1m,
                0m),
            phantom);

        await ApplyPowerIfPositive<StrengthPower>(
            phantom,
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.MysteriousKnight,
                2m,
                1m),
            phantom);

        int slippery = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                effects,
                PhantomFusionEffectType.Inklet,
                1m,
                1m) +
            PhantomFusionEffectHelper.GetEffect(
                effects,
                PhantomFusionEffectType.Vantom,
                3m,
                2m));
        await ApplyPowerIfPositive<SlipperyPower>(
            phantom,
            slippery,
            phantom);

        await ApplyPowerIfPositive<CurlUpPower>(
            phantom,
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.LouseProgenitor,
                10m,
                5m),
            phantom);

        if (PhantomFusionEffectHelper.HasEffect(
                effects,
                PhantomFusionEffectType.Exoskeleton))
        {
            decimal hardToKillPercent =
                PhantomFusionEffectHelper.GetEffect(
                    effects,
                    PhantomFusionEffectType.Exoskeleton,
                    30m,
                    -5m);
            int damageCap = Math.Max(
                1,
                PhantomFusionEffectHelper.ToInt(
                    LargeCreatureHealthHelper.GetMaxHp(phantom) * hardToKillPercent / 100m));
            await ApplyPowerIfPositive<HardToKillPower>(
                phantom,
                damageCap,
                phantom);
        }

        int artifact = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.CubexConstruct,
            1m,
            1m);
        await ApplyPowerIfPositive<ArtifactPower>(
            phantom,
            artifact,
            phantom);

        int territorial = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.Byrdonis,
            1m,
            1m);
        await ApplyPowerIfPositive<TerritorialPower>(
            phantom,
            territorial,
            phantom);

        int slowTargets = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.BygoneEffigy,
            1m,
            1m);
        foreach (Creature target in GetLivingOpponents(phantom)
                     .OrderByDescending(LargeCreatureHealthHelper.GetCurrentHp)
                     .Take(slowTargets))
        {
            await PowerCmd.Apply<SlowPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                1m,
                phantom,
                null);
        }

        int ceremonialStrength = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetFixedEffect(
                effects,
                PhantomFusionEffectType.CeremonialBeast,
                1m));
        if (LargeCreatureHealthHelper.GetCurrentHp(phantom) * 2m > LargeCreatureHealthHelper.GetMaxHp(phantom))
        {
            await ApplyPowerIfPositive<StrengthPower>(
                phantom,
                ceremonialStrength,
                phantom);
        }

        int followerCount = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.KinPriest,
                2m,
                0m));
        int followerLevel = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                effects,
                PhantomFusionEffectType.KinPriest,
                3m,
                2m));
        for (int index = 0;
             index < followerCount && followerLevel > 0;
             index++)
        {
            await SummonPhantomMinion<KinFollower>(
                phantom,
                followerLevel);
        }

        int eggCount = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.Ovicopter,
                3m,
                0m));
        int eggLevel = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.Ovicopter,
            1m,
            2m);
        for (int index = 0;
             index < eggCount && eggLevel > 0;
             index++)
        {
            await SummonPhantomMinion<ToughEgg>(
                phantom,
                eggLevel);
        }

        int thorns = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.Toadpole,
                4m,
                2m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.SpinyToad,
                8m,
                4m);
        await ApplyPowerIfPositive<ThornsPower>(
            phantom,
            thorns,
            phantom);

        int ritual = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.DampCultist,
                3m,
                1m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.CalcifiedCultist,
                1m,
                0m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.DevotedSculptor,
                4m,
                0m);
        await ApplyPowerIfPositive<RitualPower>(
            phantom,
            ritual,
            phantom);

        await ApplyPowerIfPositive<SuckPower>(
            phantom,
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.FossilStalker,
                1m,
                0m),
            phantom);

        int plating = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.SewerClam,
                4m,
                2m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.LagavulinMatriarch,
                8m,
                4m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.SlumberingBeetle,
                10m,
                5m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.LivingShield,
                10m,
                0m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.FrogKnight,
                12m,
                6m) +
            PhantomFusionEffectHelper.ToInt(
                PhantomFusionEffectHelper.GetSecondaryEffect(
                    effects,
                    PhantomFusionEffectType.MysteriousKnight,
                    6m,
                    3m));
        await ApplyPowerIfPositive<PlatingPower>(
            phantom,
            plating,
            phantom);

        int additionalArtifact = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.PunchConstruct,
                1m,
                1m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.Chomper,
                2m,
                2m) +
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.MechaKnight,
                3m,
                0m);
        await ApplyPowerIfPositive<ArtifactPower>(
            phantom,
            additionalArtifact,
            phantom);

        await ApplyPowerIfPositive<SkittishPower>(
            phantom,
            GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.PhantasmalGardener,
                6m,
                3m),
            phantom);

        if (PhantomFusionEffectHelper.HasEffect(
                effects,
                PhantomFusionEffectType.SkulkingColony))
        {
            HardenedShellPower existingShell =
                phantom.GetPower<HardenedShellPower>();
            if (existingShell != null)
            {
                await PowerCmd.Remove(existingShell);
            }

            int hardenedShell = PhantomFusionEffectHelper.GetSkulkingColonyShellAmount(
                effects, LargeCreatureHealthHelper.GetMaxHp(phantom));
            await ApplyPowerIfPositive<HardenedShellPower>(
                phantom,
                hardenedShell,
                phantom);
        }

        int obscuraMinionLevel = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.TheObscura,
            1m,
            2m);
        if (obscuraMinionLevel > 0)
        {
            await SummonPhantomMinion<Parafright>(
                phantom,
                obscuraMinionLevel);
        }

        int axebotLevel = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.Axebot,
            1m,
            3m);
        if (axebotLevel > 0)
        {
            Axebot axebot =
                (Axebot)ModelDb.Monster<Axebot>().ToMutable();
            axebot.StockAmount = 0;
            await SummonPhantomMinion(
                phantom,
                axebot,
                axebotLevel);
        }

        int fabricatorLevel = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.Fabricator,
            1m,
            1m);
        int fabricatorCount = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.Fabricator,
                2m,
                0m));
        if (fabricatorLevel > 0 && fabricatorCount > 0)
        {
            await SummonFabricatorRobots(
                phantom,
                fabricatorLevel,
                fabricatorCount);
        }

        int amalgamLevel = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.Queen,
            1m,
            5m);
        if (amalgamLevel > 0)
        {
            await SummonPhantomMinion<TorchHeadAmalgam>(
                phantom,
                amalgamLevel);
        }

        int ironWaveCount = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.BowlbugEgg,
                1m,
                0m));
        int ironWaveBonus = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.BowlbugEgg,
            0m,
            2m);
        for (int index = 0; index < ironWaveCount; index++)
        {
            await AddFusionIronWaveToHand(
                phantom,
                ironWaveBonus);
        }

        int imbalancedTargets = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.BowlbugRock,
            1m,
            1m);
        foreach (Creature target in GetLivingOpponents(phantom)
                     .OrderByDescending(LargeCreatureHealthHelper.GetCurrentHp)
                     .Take(imbalancedTargets))
        {
            await PowerCmd.Apply<ImbalancedPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                1m,
                phantom,
                null);
        }

        if (phantom.PetOwner is { } owner)
        {
            int playerStrengthLoss = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.TheLost,
                4m,
                1m);
            if (playerStrengthLoss > 0)
            {
                await PowerCmd.Apply<StrengthPower>(
                    new ThrowingPlayerChoiceContext(),
                    owner.Creature,
                    -playerStrengthLoss,
                    phantom,
                    null);
            }

            await ApplyPowerIfPositive<StrengthPower>(
                phantom,
                PhantomFusionEffectHelper.ToInt(
                    PhantomFusionEffectHelper.GetSecondaryEffect(
                        effects,
                        PhantomFusionEffectType.TheLost,
                        2m,
                        1m)),
                phantom);

            await ApplyPowerIfPositive<DexterityPower>(
                owner.Creature,
                PhantomFusionEffectHelper.ToInt(
                    PhantomFusionEffectHelper.GetSecondaryEffect(
                        effects,
                        PhantomFusionEffectType.TheForgotten,
                        0m,
                        1m)),
                phantom);

            await SelectAeonglassCards(
                phantom,
                owner,
                GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.Aeonglass,
                    1m,
                    1m));
        }
    }

    private static async Task ApplyPendingFusionEffectsBeforeAction(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower?.HasPendingVineShamblerEffect != true)
        {
            return;
        }

        phantomPower.SetPendingVineShamblerEffect(false);
        int strengthLoss = GetFusionEffectAmount(
            GetFusionEffectData(phantom),
            PhantomFusionEffectType.VineShambler,
            2m,
            2m);
        if (strengthLoss <= 0)
        {
            return;
        }

        await PowerCmd.Apply<PiercingWailPower>(
            new ThrowingPlayerChoiceContext(),
            GetLivingOpponents(phantom),
            strengthLoss,
            phantom,
            null);
    }

    private static async Task ApplyFusionEffectsAfterIntent(
        Creature phantom,
        PhantomPower phantomPower,
        bool wasAttack,
        bool wasBuff)
    {
        if (phantomPower == null || phantomPower.IsSummonedMinion)
        {
            return;
        }

        string effects = GetFusionEffectData(phantom);
        if (string.IsNullOrEmpty(effects))
        {
            return;
        }

        if (wasAttack)
        {
            int nibbitBlock = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.Nibbit,
                HbrSkillPower.BlockUnits(60m),
                HbrSkillPower.BlockUnits(20m));
            if (nibbitBlock > 0)
            {
                await CreatureCmd.GainBlock(
                    phantom,
                    HbrSkillBlockHelper.ResolveUnpowered(phantom, nibbitBlock),
                    ValueProp.Unpowered,
                    null);
            }

            if (phantomPower.AttackIntentsPerformed % 3 == 0)
            {
                int vulnerable = GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.Flyconid,
                    2m,
                    1m);
                Creature target = SelectRandomAttackTarget(
                    phantom,
                    GetLivingOpponents(phantom));
                if (target != null && vulnerable > 0)
                {
                    await HbrPercentDebuffApplication.ApplyDefenseDown(
                        new ThrowingPlayerChoiceContext(),
                        target,
                        vulnerable,
                        phantom,
                        null);
                }

                int gasBombs = GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.LivingFog,
                    1m,
                    1m);
                for (int index = 0; index < gasBombs; index++)
                {
                    await SummonPhantomMinion<GasBomb>(phantom, 1);
                }

                int hauntedWeak = GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.HauntedShip,
                    2m,
                    1m);
                if (hauntedWeak > 0)
                {
                    await HbrPercentDebuffApplication.ApplyAttackDown(
                        new ThrowingPlayerChoiceContext(),
                        GetLivingOpponents(phantom),
                        hauntedWeak,
                        phantom,
                        null);
                }

                int hauntedStrengthLoss =
                    PhantomFusionEffectHelper.ToInt(
                        PhantomFusionEffectHelper.GetSecondaryEffect(
                            effects,
                            PhantomFusionEffectType.HauntedShip,
                            3m,
                            1m));
                Creature highestDamageEnemy = GetLivingOpponents(phantom)
                    .Where(enemy =>
                        GetRawAttackIntentTotal(enemy) > 0m)
                    .OrderByDescending(GetRawAttackIntentTotal)
                    .FirstOrDefault();
                if (highestDamageEnemy != null &&
                    hauntedStrengthLoss > 0)
                {
                    await PowerCmd.Apply<PiercingWailPower>(
                        new ThrowingPlayerChoiceContext(),
                        highestDamageEnemy,
                        hauntedStrengthLoss,
                        phantom,
                        null);
                }

                int silkWeak = GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.BowlbugSilk,
                    1m,
                    1m);
                Creature silkTarget = SelectRandomAttackTarget(
                    phantom,
                    GetLivingOpponents(phantom));
                if (silkTarget != null && silkWeak > 0)
                {
                    await HbrPercentDebuffApplication.ApplyAttackDown(
                        new ThrowingPlayerChoiceContext(),
                        silkTarget,
                        silkWeak,
                        phantom,
                        null);
                }
            }

            decimal escalatingDamage =
                PhantomFusionEffectHelper.GetEffect(
                    effects,
                    PhantomFusionEffectType.CeremonialBeast,
                    5m,
                    3m);
            if (escalatingDamage > 0m)
            {
                phantomPower.AddCombatDamageBonus(escalatingDamage);
            }
        }
        else
        {
            int block = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.CrossbowRubyRaider,
                HbrSkillPower.BlockUnits(100m),
                HbrSkillPower.BlockUnits(40m));
            if (block > 0)
            {
                await CreatureCmd.GainBlock(
                    phantom,
                    HbrSkillBlockHelper.ResolveUnpowered(phantom, block),
                    ValueProp.Unpowered,
                    null);
            }
        }

        if (wasBuff)
        {
            if (phantomPower.BuffIntentsPerformed % 2 == 0)
            {
                await ApplyPowerIfPositive<StrengthPower>(
                    phantom,
                    GetFusionEffectAmount(
                        effects,
                        PhantomFusionEffectType.BruteRubyRaider,
                        1m,
                        1m),
                    phantom);

                await ApplyPowerIfPositive<StrengthPower>(
                    phantom,
                    GetFusionEffectAmount(
                        effects,
                        PhantomFusionEffectType.FlailKnight,
                        2m,
                        0m),
                    phantom);
            }

            int poison = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.PhrogParasite,
                6m,
                3m);
            if (poison > 0)
            {
                await PowerCmd.Apply<PoisonPower>(
                    new ThrowingPlayerChoiceContext(),
                    GetLivingOpponents(phantom),
                    poison,
                    phantom,
                    null);
            }

            int dp = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.HealHopper,
                3m,
                1m);
            phantom.GetPower<PhantomHighDimensionalShellPower>()?
                .Restore(HbrResourceValueHelper.Fixed(phantom, dp));

            int seapunkBlock = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.Seapunk,
                HbrSkillPower.BlockUnits(60m),
                HbrSkillPower.BlockUnits(20m));
            if (seapunkBlock > 0)
            {
                await CreatureCmd.GainBlock(
                    phantom,
                    HbrSkillBlockHelper.ResolveUnpowered(phantom, seapunkBlock),
                    ValueProp.Unpowered,
                    null);
            }
        }
    }

    private static async Task ApplyFusionEffectsAfterAction(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower == null || phantomPower.IsSummonedMinion)
        {
            return;
        }

        string effects = GetFusionEffectData(phantom);
        if (string.IsNullOrEmpty(effects))
        {
            return;
        }

        if (phantomPower.ActionsPerformed == 1)
        {
            int eyeLevel = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.Fogmog,
                1m,
                1m);
            if (eyeLevel > 0)
            {
                await SummonPhantomMinion<EyeWithTeeth>(
                    phantom,
                    eyeLevel);
            }

            int constrict = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.SlitheringStrangler,
                5m,
                2m);
            if (constrict > 0)
            {
                await PowerCmd.Apply<ConstrictPower>(
                    new ThrowingPlayerChoiceContext(),
                    GetLivingOpponents(phantom),
                    constrict,
                    phantom,
                    null);
            }
        }

        if (phantomPower.ActionsPerformed == 2)
        {
            int weak = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.ShrinkerBeetle,
                2m,
                1m);
            if (weak > 0)
            {
                await HbrPercentDebuffApplication.ApplyAttackDown(
                    new ThrowingPlayerChoiceContext(),
                    GetLivingOpponents(phantom),
                    weak,
                    phantom,
                    null);
            }
        }

        if (phantomPower.ActionsPerformed % 4 == 0 &&
            PhantomFusionEffectHelper.GetEffect(
                effects,
                PhantomFusionEffectType.VineShambler,
                2m,
                2m) > 0m)
        {
            phantomPower.SetPendingVineShamblerEffect(true);
        }

        decimal sludgePotency = PhantomFusionEffectHelper.GetEffect(
            effects,
            PhantomFusionEffectType.SludgeSpinner,
            1m,
            1m);
        int sludgeInterval = Math.Max(
            1,
            5 - PhantomFusionEffectHelper.ToInt(sludgePotency));
        if (sludgePotency > 0m &&
            phantomPower.ActionsPerformed % sludgeInterval == 0)
        {
            IReadOnlyList<Creature> opponents =
                GetLivingOpponents(phantom);
            await HbrPercentDebuffApplication.ApplyAttackDown(
                new ThrowingPlayerChoiceContext(),
                opponents,
                1m,
                phantom,
                null);
            await PowerCmd.Apply<FragilePower>(
                new ThrowingPlayerChoiceContext(),
                opponents,
                1m,
                phantom,
                null);
        }

        int hunterKillerReduction = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.HunterKiller,
            1m,
            1m);
        if (hunterKillerReduction > 0 &&
            phantomPower.ActionsThisPlayerTurn > 0 &&
            phantomPower.ActionsThisPlayerTurn % 3 == 0)
        {
            IReadOnlyList<Creature> opponents =
                GetLivingOpponents(phantom);
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                opponents,
                -hunterKillerReduction,
                phantom,
                null);
            await PowerCmd.Apply<DexterityPower>(
                new ThrowingPlayerChoiceContext(),
                opponents,
                -hunterKillerReduction,
                phantom,
                null);
        }

        int steamEruption = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.WaterfallGiant,
            2m,
            1m);
        await ApplyPowerIfPositive<SteamEruptionPower>(
            phantom,
            steamEruption,
            phantom);

        decimal devotedDamage =
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.DevotedSculptor,
                0m,
                3m);
        if (devotedDamage > 0m)
        {
            phantomPower.AddCombatDamageBonus(devotedDamage);
        }

        decimal disintegration = PhantomFusionEffectHelper.GetEffect(
            effects,
            PhantomFusionEffectType.KnowledgeDemon,
            12m,
            0m);
        int knowledgeIntervalReduction =
            PhantomFusionEffectHelper.ToInt(
                PhantomFusionEffectHelper.GetSecondaryEffect(
                    effects,
                    PhantomFusionEffectType.KnowledgeDemon,
                    1m,
                    1m));
        int knowledgeInterval = Math.Max(
            1,
            5 - knowledgeIntervalReduction);
        if (disintegration > 0m &&
            phantomPower.ActionsPerformed % knowledgeInterval == 0)
        {
            await PowerCmd.Apply<DisintegrationPower>(
                new ThrowingPlayerChoiceContext(),
                GetLivingOpponents(phantom),
                disintegration,
                phantom,
                null);
        }
    }

    internal static async Task ApplyFusionEffectsAtPlayerTurnStart(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            !phantom.IsAlive)
        {
            return;
        }

        string effects = GetFusionEffectData(phantom);
        decimal calcifiedDamage =
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.CalcifiedCultist,
                0m,
                2m);
        if (calcifiedDamage > 0m)
        {
            phantomPower.AddCombatDamageBonus(calcifiedDamage);
        }

        if (phantomPower.PlayerTurnsStarted == 2)
        {
            int tunnelerBlock = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.Tunneler,
                HbrSkillPower.BlockUnits(320m),
                HbrSkillPower.BlockUnits(160m));
            if (tunnelerBlock > 0)
            {
                await CreatureCmd.GainBlock(
                    phantom,
                    HbrSkillBlockHelper.ResolveUnpowered(phantom, tunnelerBlock),
                    ValueProp.Unpowered,
                    null);
            }
        }

        if (phantomPower.PlayerTurnsStarted == 6)
        {
            await ApplyPowerIfPositive<StrengthPower>(
                phantom,
                GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.TheInsatiable,
                    8m,
                    0m),
                phantom);
        }

        if (PhantomFusionEffectHelper.HasEffect(
                effects,
                PhantomFusionEffectType.BowlbugNectar))
        {
            int turnReduction = GetFusionEffectAmount(
                effects,
                PhantomFusionEffectType.BowlbugNectar,
                1m,
                1m);
            int triggerTurn = Math.Max(1, 5 - turnReduction);
            if (phantomPower.PlayerTurnsStarted == triggerTurn)
            {
                int strength = PhantomFusionEffectHelper.ToInt(
                    PhantomFusionEffectHelper.GetSecondaryEffect(
                        effects,
                        PhantomFusionEffectType.BowlbugNectar,
                        8m,
                        0m));
                await ApplyPowerIfPositive<StrengthPower>(
                    phantom,
                    strength,
                    phantom);
            }
        }
    }

    internal static async Task ApplyFusionEffectsAfterPlayerTurnEnd(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            phantomPower.HasTriggeredTwoTailedRatFusion ||
            phantomPower.PlayerTurnsStarted <= 0 ||
            !phantom.IsAlive)
        {
            return;
        }

        int ratLevel = GetFusionEffectAmount(
            GetFusionEffectData(phantom),
            PhantomFusionEffectType.TwoTailedRat,
            1m,
            1m);
        if (ratLevel <= 0)
        {
            return;
        }

        phantomPower.MarkTwoTailedRatFusionTriggered();
        await SummonPhantomMinion<TwoTailedRat>(
            phantom,
            ratLevel);
    }

    internal static async Task ApplyFusionEffectsAfterDamageGiven(
        Creature phantom,
        PhantomPower phantomPower,
        Creature dealer,
        DamageResult result,
        Creature target)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            dealer != phantom ||
            target?.Side == phantom.Side ||
            result.UnblockedDamage <= 0)
        {
            return;
        }

        decimal damageBonus =
            PhantomFusionEffectHelper.GetSecondaryEffect(
                GetFusionEffectData(phantom),
                PhantomFusionEffectType.FossilStalker,
                0m,
                3m);
        if (damageBonus > 0m)
        {
            phantomPower.AddCombatDamageBonus(damageBonus);
        }

        int maxHpPerKill = GetFusionEffectAmount(
            GetFusionEffectData(phantom),
            PhantomFusionEffectType.ScrollOfBiting,
            1m,
            0m);
        if (result.WasTargetKilled &&
            CountsAsFinalDefeat(target) &&
            maxHpPerKill > 0)
        {
            await CreatureCmd.GainMaxHp(phantom, HbrResourceValueHelper.Fixed(phantom, maxHpPerKill));
        }
    }

    internal static async Task ApplyFusionEffectsAfterDamageReceived(
        Creature phantom,
        PhantomPower phantomPower,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer)
    {
        if (target != phantom || result.TotalDamage <= 0)
        {
            return;
        }

        if (phantomPower?.IsSummonedMinion == false &&
            dealer != null &&
            dealer.Side != phantom.Side &&
            props.IsPoweredAttack())
        {
            string effects = GetFusionEffectData(phantom);
            await ApplyPowerIfPositive<
                PhantomNextTurnStrengthLossPower>(
                dealer,
                GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.Entomancer,
                    1m,
                    1m),
                phantom);
            await ApplyPowerIfPositive<PhantomPollutionPower>(
                dealer,
                GetFusionEffectAmount(
                    effects,
                    PhantomFusionEffectType.InfestedPrism,
                    2m,
                    1m),
                phantom);
        }

        await ApplyTerrorEelFusionIfNeeded(
            phantom,
            phantomPower);
        await ApplyOwlMagistrateFusionIfNeeded(
            phantom,
            phantomPower);
    }

    private static async Task ApplyTerrorEelFusionIfNeeded(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            phantomPower.HasTriggeredTerrorEelFusion ||
            LargeCreatureHealthHelper.GetCurrentHp(phantom) * 2m >= LargeCreatureHealthHelper.GetMaxHp(phantom) ||
            !phantom.IsAlive)
        {
            return;
        }

        string effects = GetFusionEffectData(phantom);
        if (!PhantomFusionEffectHelper.HasEffect(
                effects,
                PhantomFusionEffectType.TerrorEel))
        {
            return;
        }

        phantomPower.MarkTerrorEelFusionTriggered();
        IReadOnlyList<Creature> opponents = GetLivingOpponents(phantom);
        int vulnerable = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.TerrorEel,
            99m,
            0m);
        if (vulnerable > 0)
        {
            await HbrPercentDebuffApplication.ApplyDefenseDown(
                new ThrowingPlayerChoiceContext(),
                opponents,
                vulnerable,
                phantom,
                null);
        }

        int fragile = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.TerrorEel,
                0m,
                5m));
        if (fragile > 0)
        {
            await PowerCmd.Apply<FragilePower>(
                new ThrowingPlayerChoiceContext(),
                opponents,
                fragile,
                phantom,
                null);
        }
    }

    internal static async Task ApplyFusionEffectsAfterHpLoss(
        Creature phantom,
        PhantomPower phantomPower,
        Creature creature,
        decimal delta)
    {
        if (delta >= 0m || creature != phantom)
        {
            return;
        }

        await ApplyTerrorEelFusionIfNeeded(
            phantom,
            phantomPower);
        await ApplyOwlMagistrateFusionIfNeeded(
            phantom,
            phantomPower);
    }

    private static async Task ApplyOwlMagistrateFusionIfNeeded(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            phantomPower.HasTriggeredOwlMagistrateFusion ||
            !phantom.IsAlive ||
            LargeCreatureHealthHelper.GetCurrentHp(phantom) * 4m >= LargeCreatureHealthHelper.GetMaxHp(phantom) ||
            GetFusionEffectAmount(
                GetFusionEffectData(phantom),
                PhantomFusionEffectType.OwlMagistrate,
                1m,
                0m) <= 0)
        {
            return;
        }

        phantomPower.MarkOwlMagistrateFusionTriggered();
        if (!phantom.HasPower<SoarPower>())
        {
            await PowerCmd.Apply<SoarPower>(
                new ThrowingPlayerChoiceContext(),
                phantom,
                1m,
                phantom,
                null);
        }
    }

    internal static async Task ApplyFusionEffectsAfterMinionDeath(
        Creature phantom,
        PhantomPower phantomPower)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            !phantom.IsAlive)
        {
            return;
        }

        string effects = GetFusionEffectData(phantom);
        int strength = GetFusionEffectAmount(
            effects,
            PhantomFusionEffectType.CorpseSlug,
            1m,
            0m);
        await ApplyPowerIfPositive<StrengthPower>(
            phantom,
            strength,
            phantom);

        decimal damageBonus =
            PhantomFusionEffectHelper.GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.CorpseSlug,
                0m,
                5m);
        if (damageBonus > 0m)
        {
            phantomPower.AddCombatDamageBonus(damageBonus);
        }
    }

    internal static async Task ApplyFusionEffectsAfterUnitDeath(
        Creature phantom,
        PhantomPower phantomPower,
        Creature deadCreature,
        bool wasRemovalPrevented)
    {
        if (phantomPower == null ||
            phantomPower.IsSummonedMinion ||
            wasRemovalPrevented ||
            deadCreature == null ||
            deadCreature == phantom ||
            !phantom.IsAlive)
        {
            return;
        }

        await ApplyPowerIfPositive<StrengthPower>(
            phantom,
            GetFusionEffectAmount(
                GetFusionEffectData(phantom),
                PhantomFusionEffectType.TestSubject,
                1m,
                0m),
            phantom);
    }

    private static IReadOnlyList<Creature> GetLivingOpponents(
        Creature phantom)
    {
        return phantom?.CombatState?.GetOpponentsOf(phantom)
            .Where(IsValidPhantomTarget)
            .ToArray() ?? Array.Empty<Creature>();
    }

    private static decimal GetRawAttackIntentTotal(Creature creature)
    {
        return creature?.Monster?.NextMove?.Intents
            .OfType<AttackIntent>()
            .Sum(intent =>
                Math.Max(0m, intent.DamageCalc?.Invoke() ?? 0m) *
                Math.Max(1, intent.Repeats)) ?? 0m;
    }

    private static int GetFusionEffectAmount(
        string effectData,
        PhantomFusionEffectType type,
        decimal baseValue,
        decimal additionalValue)
    {
        return PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                effectData,
                type,
                baseValue,
                additionalValue));
    }

    private static async Task ApplyPowerIfPositive<T>(
        Creature target,
        int amount,
        Creature applier)
        where T : PowerModel
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<T>(
            new ThrowingPlayerChoiceContext(),
            target,
            amount,
            applier,
            null);
    }

    private static async Task AddFusionIronWaveToHand(
        Creature phantom,
        int bonus)
    {
        if (phantom?.CombatState == null ||
            phantom.PetOwner is not { } owner)
        {
            return;
        }

        IronWave ironWave =
            phantom.CombatState.CreateCard<IronWave>(owner);
        ironWave.DynamicVars.Damage.BaseValue += Math.Max(0, bonus);
        ironWave.DynamicVars.Block.BaseValue += Math.Max(0, bonus);
        ironWave.SetToFreeThisTurn();
        ironWave.AddKeyword(CardKeyword.Exhaust);
        await CardPileCmd.AddGeneratedCardToCombat(
            ironWave,
            PileType.Hand,
            owner);
    }

    private static async Task SelectAeonglassCards(
        Creature phantom,
        Player owner,
        int count)
    {
        if (phantom?.CombatState == null || owner == null || count <= 0)
        {
            return;
        }

        int selectionCount = Math.Min(
            count,
            PileType.Draw.GetPile(owner).Cards.Count);
        if (selectionCount <= 0)
        {
            return;
        }

        IReadOnlyList<CardModel> selected =
            (await CardSelectCmd.FromCombatPile(
                new BlockingPlayerChoiceContext(),
                PileType.Draw.GetPile(owner),
                owner,
                new CardSelectorPrefs(
                    new LocString(
                        "relics",
                        "MOMENT_OF_REIGN.aeonglassSelectionScreenPrompt"),
                    selectionCount)))
            .ToArray();
        if (selected.Count > 0)
        {
            await CardPileCmd.Add(selected, PileType.Hand);
        }
    }

    private static async Task<Creature> SummonPhantomMinion<T>(
        Creature leader,
        int level)
        where T : MonsterModel
    {
        MonsterModel monster =
            (MonsterModel)ModelDb.Monster<T>().ToMutable();
        return await SummonPhantomMinion(
            leader,
            monster,
            level);
    }

    private static async Task<Creature> SummonPhantomMinion(
        Creature leader,
        MonsterModel monster,
        int level)
    {
        if (leader?.CombatState == null ||
            leader.PetOwner is not { } owner ||
            monster == null ||
            !leader.CombatState.IsLiveCombat())
        {
            return null;
        }

        using IDisposable ascensionScope =
            PhantomAscensionHelper.BeginPhantomEvaluation();

        Creature minion = leader.CombatState.CreateCreature(
            monster,
            leader.Side,
            null);
        ConfigureSummonedMinionHealth(minion, level, owner);
        await PlayerCmd.AddPet(minion, owner);
        await InitializeSummonedMinion(
            leader,
            minion,
            level,
            configureHealth: false);
        return minion;
    }

    private static async Task SummonFabricatorRobots(
        Creature leader,
        int level,
        int count)
    {
        if (leader?.CombatState?.RunState == null)
        {
            return;
        }

        List<MonsterModel> options = new()
        {
            ModelDb.Monster<Guardbot>(),
            ModelDb.Monster<Noisebot>(),
            ModelDb.Monster<Stabbot>(),
            ModelDb.Monster<Zapbot>()
        };
        int summonCount = Math.Clamp(count, 0, options.Count);
        for (int index = 0; index < summonCount; index++)
        {
            MonsterModel selected = leader.CombatState.RunState.Rng
                .MonsterAi.NextItem(options);
            options.Remove(selected);
            await SummonPhantomMinion(
                leader,
                (MonsterModel)selected.ToMutable(),
                level);
        }
    }

    internal static async Task InitializeNativeDeathMinions(
        Creature leader, IReadOnlyList<Creature> minions)
    {
        foreach (Creature minion in minions)
        {
            if (minion?.CombatState != null &&
                minion.CombatState == leader?.CombatState &&
                minion.CombatState.ContainsCreature(minion))
            {
                await InitializeSummonedMinion(
                    leader, minion, 1, configureHealth: false);
            }
        }
    }

    private static async Task InitializeSummonedMinion(
        Creature leader,
        Creature minion,
        int level,
        bool configureHealth = true)
    {
        if (leader?.PetOwner is not { } owner ||
            minion?.Monster == null ||
            minion.GetPower<PhantomPower>() != null)
        {
            return;
        }

        int normalizedLevel = Math.Max(1, level);
        if (configureHealth)
        {
            ConfigureSummonedMinionHealth(minion, normalizedLevel, owner);
        }

        await RunPhantomEntryEffects(minion);
        MinionPower minionPower = minion.GetPower<MinionPower>() ??
            await PowerCmd.Apply<MinionPower>(
                new ThrowingPlayerChoiceContext(),
                minion,
                1m,
                leader,
                null);
        if (minionPower != null)
        {
            minionPower.Applier = leader;
        }

        PhantomPower phantomPower = await PowerCmd.Apply<PhantomPower>(
            new ThrowingPlayerChoiceContext(),
            minion,
            1m,
            owner.Creature,
            null);
        phantomPower?.ConfigureAsSummonedMinion(normalizedLevel);
        await PowerCmd.Apply<PhantomHighDimensionalShellPower>(
            new ThrowingPlayerChoiceContext(),
            minion,
            PhantomHighDimensionalShellPower.CalculateInitialAmount(minion),
            owner.Creature,
            null);

        MomentOfReign.ArrangePhantomVisuals(minion);
        minion.HpDisplay = HpDisplay.Normal;
        MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance?
            .SetCreatureIsInteractableCompat(minion, true);
        minion.PrepareForNextTurn(
            minion.CombatState.GetOpponentsOf(minion)
                .Where(IsValidPhantomTarget));
    }

    internal static void ConfigureSummonedMinionHealth(
        Creature minion,
        int level,
        Player owner = null)
    {
        if (minion == null)
        {
            return;
        }

        int normalizedLevel = Math.Max(1, level);
        owner ??= minion.PetOwner;
        decimal originalHp = HbrBattleStatsHelper.IsEnabled(owner)
            ? RawMinionHealth.GetValue(minion,
                creature => new(LargeCreatureHealthHelper.GetMaxHp(creature))).Maximum
            : LargeCreatureHealthHelper.GetMaxHp(minion);
        decimal baseHp = HbrBattleStatsHelper.GetPhantomBaseHp(owner,
            Math.Max(1m, decimal.Floor(originalHp / 2m)));
        decimal bonusHp = HbrResourceValueHelper.Fixed(owner?.Creature,
            Math.Max(0, normalizedLevel - 1) * PhantomRosterEntry.LevelBonusMaxHp);
        decimal maxHp = BigCombatNumbers.ClampStat(baseHp + bonusHp);
        minion.SetMaxHpInternal(maxHp);
        minion.SetCurrentHpInternal(maxHp);
        minion.HpDisplay = HpDisplay.Normal;
    }

    private static async Task ConsumeNextAttackSegmentPowers(
        Creature phantom)
    {
        PhantomNextAttackDoublePower doubling =
            phantom.GetPower<PhantomNextAttackDoublePower>();
        if (doubling != null)
        {
            await PowerCmd.Remove(doubling);
        }

        PhantomNextAttackBonusSegmentsPower bonusSegments =
            phantom.GetPower<PhantomNextAttackBonusSegmentsPower>();
        if (bonusSegments != null)
        {
            await PowerCmd.Remove(bonusSegments);
        }

        PhantomNextAttackAllEnemiesPower allEnemies =
            phantom.GetPower<PhantomNextAttackAllEnemiesPower>();
        if (allEnemies != null)
        {
            await PowerCmd.Remove(allEnemies);
        }
    }

    private static async Task NotifyPhantomActionCompleted(
        Creature phantom,
        bool wasAttack)
    {
        Player owner = phantom?.PetOwner;
        if (owner?.Creature == null)
        {
            return;
        }

        if (IsOwnedMainPhantom(phantom, owner))
        {
            await ThirtyAClassRelicsHelper.AfterAction(phantom);
            if (owner.Creature.GetPower<InMyNamePower>() is { } inMyName)
            {
                await inMyName.AfterPhantomActed(phantom);
            }
            if (!wasAttack && owner.Creature.GetPower<SilentOfferingPower>() is { } offering)
            {
                await offering.AfterPhantomNonAttack(phantom);
            }
        }

        if (!wasAttack)
        {
            EndlessPhantomPower endless =
                owner.Creature.GetPower<EndlessPhantomPower>();
            if (endless != null)
            {
                await endless.AfterPhantomNonAttack(phantom);
            }
        }

        foreach (NowIsTheTimeToReignHbrgoSaki card in
                 owner.PlayerCombatState?.AllCards
                     .OfType<NowIsTheTimeToReignHbrgoSaki>() ??
                 Enumerable.Empty<NowIsTheTimeToReignHbrgoSaki>())
        {
            card.AfterPhantomActed(phantom);
        }

        foreach (WorldThatWillComeHbrgoSaki card in
                 owner.PlayerCombatState?.AllCards.OfType<WorldThatWillComeHbrgoSaki>() ??
                 Enumerable.Empty<WorldThatWillComeHbrgoSaki>())
        {
            card.RefreshActionProgress();
        }

    }
}
