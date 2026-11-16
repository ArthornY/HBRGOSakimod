using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrProvokeTargetingHelper
{
    private static bool IsRestrictedCard(CardModel card) => card?.Owner?.Creature?.IsPlayer == true &&
        HbrConfusionPower.IsSingleTargetAttack(card);
    private static bool IsProvoking(Creature enemy) => enemy?.IsAlive == true && EnemyStatScalingHelper.IsEnemy(enemy) &&
        HbrBattleStatsHelper.IsEnabled(enemy) && enemy.Powers.OfType<HbrProvokePower>().Any(power => power.Amount > 0);
    internal static bool IsAllowedTarget(CardModel card, Creature target)
    {
        if (!IsRestrictedCard(card) || target == null || card.Owner.Creature.CombatState is not { } combat) return true;
        return !combat.HittableEnemies.Any(IsProvoking) || IsProvoking(target);
    }
    internal static Creature ResolveTarget(CardModel card, Creature target)
    {
        if (!IsRestrictedCard(card) || target == null || target.Side == card.Owner.Creature.Side ||
            card.Owner.Creature.CombatState is not { } combat) return target;
        if (IsProvoking(target)) return target;
        return combat.HittableEnemies.FirstOrDefault(IsProvoking) ?? target;
    }
    internal static Creature[] ConfusionCandidates(CardModel card)
    {
        var enemies = card?.Owner?.Creature?.CombatState?.HittableEnemies.Where(enemy => enemy.IsAlive).ToArray()
            ?? Array.Empty<Creature>();
        Creature[] provoking = enemies.Where(IsProvoking).ToArray();
        return provoking.Length > 0 ? provoking : enemies;
    }
}
