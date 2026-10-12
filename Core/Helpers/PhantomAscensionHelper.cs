using System;
using System.Linq;
using System.Threading;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomAscensionHelper
{
    private static readonly AsyncLocal<int> PhantomEvaluationDepth = new();

    internal static IDisposable BeginPhantomEvaluation()
    {
        PhantomEvaluationDepth.Value++;
        return new EvaluationScope();
    }

    internal static bool ShouldSuppressEnemyAscension(
        AscensionLevel level)
    {
        if (PhantomEvaluationDepth.Value > 0)
        {
            return false;
        }

        int tier = GetSuppressionTier();
        return level switch
        {
            AscensionLevel.DeadlyEnemies => tier >= 2,
            AscensionLevel.ToughEnemies => tier >= 3,
            _ => false
        };
    }

    internal static bool ShouldSuppressEnemyShellEnhancement(
        Creature creature)
    {
        return creature?.Side == CombatSide.Enemy &&
            GetSuppressionTier() >= 1;
    }

    internal static void RefreshEnemyAscensionEffects(
        ICombatState combatState)
    {
        int tier = GetSuppressionTier();
        if (combatState == null || tier < 2)
        {
            return;
        }

        int ascension = combatState.RunState.AscensionLevel;
        bool suppressDeadly = tier >= 2 && ascension >= 9;
        bool suppressTough = tier >= 3 && ascension >= 8;
        if (!suppressDeadly && !suppressTough)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies
                     .Where(enemy => enemy.IsAlive && enemy.Monster != null)
                     .ToArray())
        {
            if (suppressTough)
            {
                ApplyReducedMaximumHp(enemy, combatState);
            }

            string moveId = enemy.Monster.NextMove?.Id ?? string.Empty;
            enemy.Monster.ResetStateMachine();
            enemy.Monster.SetUpForCombat();
            if (!string.IsNullOrEmpty(moveId) &&
                enemy.Monster.MoveStateMachine?.States.TryGetValue(
                    moveId,
                    out MonsterState replacementState) == true &&
                replacementState is MoveState replacementMove)
            {
                enemy.Monster.SetMoveImmediate(
                    replacementMove,
                    forceTransition: true);
            }
            else
            {
                enemy.PrepareForNextTurn(
                    combatState.GetOpponentsOf(enemy));
            }
        }
    }

    private static int GetSuppressionTier()
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return 0;
        }

        return RunManager.Instance.DebugOnlyGetState()?.Players
            .Select(player => player.GetRelic<MomentOfReign>())
            .Where(relic => relic != null)
            .Select(relic => relic.GetActiveRosterEntry())
            .Where(entry => entry.HasValue)
            .Select(entry => PhantomFusionEffectHelper.ToInt(
                PhantomFusionEffectHelper.GetEffect(
                    entry.Value.FusionEffectData,
                    PhantomFusionEffectType.MagiKnight,
                    1m,
                    1m)))
            .DefaultIfEmpty(0)
            .Max() ?? 0;
    }

    private static void ApplyReducedMaximumHp(
        Creature enemy,
        ICombatState combatState)
    {
        int reducedMin = enemy.Monster.MinInitialHp;
        int reducedMax = enemy.Monster.MaxInitialHp;
        int ascendedMin;
        int ascendedMax;
        using (BeginPhantomEvaluation())
        {
            ascendedMin = enemy.Monster.MinInitialHp;
            ascendedMax = enemy.Monster.MaxInitialHp;
        }

        if (reducedMin == ascendedMin && reducedMax == ascendedMax)
        {
            return;
        }

        int originalBaseHp = Math.Clamp(
            enemy.MonsterMaxHpBeforeModification ?? ascendedMax,
            ascendedMin,
            ascendedMax);
        decimal position = ascendedMax == ascendedMin
            ? 1m
            : (decimal)(originalBaseHp - ascendedMin) /
                (ascendedMax - ascendedMin);
        int reducedBaseHp = reducedMin + (int)Math.Round(
            position * (reducedMax - reducedMin),
            MidpointRounding.AwayFromZero);
        decimal reducedScaledHp = EnemyHealthScalingHelper.ScaleReplacementHealth(Creature.ScaleHpForMultiplayer(
            reducedBaseHp,
            combatState.Encounter,
            combatState.Players.Count,
            combatState.RunState.CurrentActIndex), enemy);
        decimal hpRatio = LargeCreatureHealthHelper.GetMaxHp(enemy) <= 0
            ? 1m
            : LargeCreatureHealthHelper.GetCurrentHp(enemy) / LargeCreatureHealthHelper.GetMaxHp(enemy);
        enemy.SetMaxHpInternal(reducedScaledHp);
        enemy.SetCurrentHpInternal(Math.Max(
            1m,
            Math.Floor(reducedScaledHp * hpRatio)));
    }

    private sealed class EvaluationScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            PhantomEvaluationDepth.Value = Math.Max(
                0,
                PhantomEvaluationDepth.Value - 1);
        }
    }
}
