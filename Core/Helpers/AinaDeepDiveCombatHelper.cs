using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class AinaDeepDiveCombatHelper
{
    private sealed class Battle { internal readonly HashSet<CardModel> Played = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    internal static int DebuffKinds(CardModel card) => card?.IsMutable == true && card.Owner?.Creature?.CombatState is { } combat
        ? combat.GetOpponentsOf(card.Owner.Creature).Where(enemy => enemy.IsAlive).SelectMany(enemy => enemy.Powers)
            .Where(power => power.Amount != 0 && power.TypeForCurrentAmount == PowerType.Debuff).Select(power => power.GetType()).Distinct().Count() : 0;
    internal static bool HasBrokenEnemy(CardModel card) => card?.IsMutable == true && card.Owner?.Creature?.CombatState is { } combat &&
        combat.GetOpponentsOf(card.Owner.Creature).Any(enemy => enemy.IsAlive && enemy.HasPower<BreakPower>());
    internal static decimal ModifiedCost(DiveAsDeepAsIWantHbrgoSaki card, decimal original) =>
        card.HasUpgradeEffect(DiveAsDeepAsIWantHbrgoSaki.RainbowMiracle) && HasBrokenEnemy(card)
            ? 0m : Math.Max(0m, original - DebuffKinds(card));
    internal static bool MarkPlayed(CardModel card)
    {
        if (card.Owner?.Creature?.CombatState is not { } combat) return false;
        CardModel key = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        return Battles.GetOrCreateValue(combat).Played.Add(key);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (source is not DiveAsDeepAsIWantHbrgoSaki card || !source.IsMutable || dealer == null || source.Owner?.Creature != dealer ||
            !props.IsPoweredAttack() || target?.Side == dealer.Side ||
            HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _)) return 1m;
        decimal coefficient = card.HasUpgradeEffect(DiveAsDeepAsIWantHbrgoSaki.WideOcean) ? 1.6m : 1.2m;
        return coefficient / HbrDestructionGainHelper.DefaultSkillCoefficient *
            (card.HasUpgradeEffect(DiveAsDeepAsIWantHbrgoSaki.RainbowMiracle) && HasBrokenEnemy(card) ? 5m : 1m);
    }
}
