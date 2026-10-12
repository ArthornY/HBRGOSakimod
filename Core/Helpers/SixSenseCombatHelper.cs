using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class SixSenseCombatHelper
{
    internal static bool IsEligibleEnemy(Creature creature) =>
        creature?.IsAlive == true && creature.Monster != null &&
        !creature.HasPower<MinionPower>() && !PhantomHelper.IsPhantomMinion(creature) &&
        creature.CombatState?.Enemies.Contains(creature) == true;

    internal static int[] RollResistances(int actNumber, Rng rng, bool isBoss = false)
    {
        int act = Math.Clamp(actNumber, 1, int.MaxValue / (isBoss ? 4 : 2));
        int[] values = Enumerable.Repeat(act, 6).ToArray();
        List<int> indices = Enumerable.Range(0, 6).ToList();
        rng.Shuffle(indices);
        foreach (int index in indices.Take(Math.Min(6, act))) values[index] += act;
        if (isBoss)
            for (int i = 0; i < values.Length; i++) values[i] *= 2;
        return values;
    }

    internal static bool IsEnhanced(Player player, SixSenseAttribute attribute) =>
        player?.Creature?.CombatState?.Enemies.Any(enemy =>
            IsEligibleEnemy(enemy) &&
            enemy.GetPower<SixSenseResistancePower>()?.IsBroken(attribute) == true) == true;

    internal static decimal Level(Player player, SixSenseAttribute attribute) =>
        Math.Max(0m, player?.GetRelic<ElectronicSoldierManual>()?.GetEffectiveSenseAmount(attribute) ?? 0m);

    internal static decimal EffectValue(SixSenseAttribute attribute, decimal level, bool enhanced)
    {
        level = Math.Max(0m, level);
        return attribute switch
        {
            SixSenseAttribute.Madness => (120m + (enhanced ? 1.5m : 1m) * level) / 100m,
            SixSenseAttribute.LeadershipCharm => 10m + (enhanced ? 2m : 1m) * level,
            SixSenseAttribute.Simplicity => 3m * (100m + (enhanced ? 1.5m : 1m) * level) / 100m,
            SixSenseAttribute.MentalStrength => (enhanced ? 0.7m : 0.5m) * level,
            SixSenseAttribute.Talent => enhanced ? 15m : 10m,
            SixSenseAttribute.Gentleness => (enhanced ? 1m : 0.5m) * level,
            _ => 0m
        };
    }

    internal static decimal EffectValue(Player player, SixSenseAttribute attribute) =>
        EffectValue(attribute, Level(player, attribute), IsEnhanced(player, attribute));

    internal static int ResistanceLoss(SixSenseAttribute attribute) =>
        attribute is SixSenseAttribute.Talent or SixSenseAttribute.Gentleness ? 2 : 1;

    internal static int ResistanceLossForPlayer(Player player, SixSenseAttribute attribute) =>
        ResistanceLoss(attribute) + (player?.Creature?.HasPower<GodspeedPower>() == true ? GodspeedPower.ExtraResistanceLoss : 0);

    internal static int ResistanceBreakCount(Player player) =>
        player?.Creature?.GetPower<SixSenseCombatPower>()?.ResistanceBreaks ?? 0;

    internal static int TargetResistanceBreakCount(Creature target) =>
        IsEligibleEnemy(target) ? target.GetPower<SixSenseResistancePower>()?.BrokenCount ?? 0 : 0;

    internal static async Task ResolvePhantomAttack(
        PlayerChoiceContext context, Player player, Creature target, CardModel cardSource)
    {
        if (player?.Creature?.IsAlive != true || !IsEligibleEnemy(target) ||
            target.GetPower<SixSenseResistancePower>() is not { } resistance)
            return;

        SixSenseAttribute[] remaining = ElectronicSoldierManual.AllSixSenseAttributes
            .Where(attribute => resistance.GetResistance(attribute) > 0 &&
                target.GetPower<NararaNullificationPower>()?.IsResistanceLocked(attribute) != true).ToArray();
        if (remaining.Length == 0) return;

        // One resistance per completed hit, independent of card attributes and Godspeed.
        SixSenseAttribute selected = remaining[target.CombatState.RunState.Rng.Niche.NextInt(remaining.Length)];
        int before = resistance.BrokenCount;
        if (resistance.Reduce(selected, 1))
            await RegisterResistanceBreaks(context, player, cardSource, 1);
        MoreThanOneTruthCombatHelper.OnResistanceTransition(player, target, before, resistance.BrokenCount);
        player.PlayerCombatState?.RecalculateCardValues();
    }

    internal static async Task BreakRandomResistances(
        PlayerChoiceContext context, Player player, Creature target, CardModel cardSource, int count)
    {
        if (count <= 0 || player?.Creature?.IsAlive != true || !IsEligibleEnemy(target) ||
            target.GetPower<SixSenseResistancePower>() is not { } resistance)
            return;

        List<SixSenseAttribute> remaining = ElectronicSoldierManual.AllSixSenseAttributes
            .Where(attribute => resistance.GetResistance(attribute) > 0 &&
                target.GetPower<NararaNullificationPower>()?.IsResistanceLocked(attribute) != true).ToList();
        int before = resistance.BrokenCount;
        int breaks = Math.Min(count, remaining.Count);
        int brokenCount = 0;
        for (int i = 0; i < breaks; i++)
        {
            int index = target.CombatState.RunState.Rng.Niche.NextInt(remaining.Count);
            SixSenseAttribute selected = remaining[index];
            remaining.RemoveAt(index);
            // A direct break removes the entire remaining resistance. Each
            // choice is removed from the pool before the next random draw.
            if (resistance.Reduce(selected, resistance.GetResistance(selected))) brokenCount++;
        }
        await RegisterResistanceBreaks(context, player, cardSource, brokenCount);
        MoreThanOneTruthCombatHelper.OnResistanceTransition(player, target, before, resistance.BrokenCount);
        player.PlayerCombatState?.RecalculateCardValues();
    }

    private static async Task RegisterResistanceBreaks(
        PlayerChoiceContext context, Player player, CardModel cardSource, int count)
    {
        if (count <= 0) return;
        SixSenseCombatPower state = player.Creature.GetPower<SixSenseCombatPower>() ??
            await PowerCmd.Apply<SixSenseCombatPower>(context, player.Creature, 1m, player.Creature, null);
        state?.RegisterResistanceBreaks(count);
        if (player.Creature.GetPower<HastePower>() is { } haste)
            await haste.OnResistancesBroken(context, cardSource, count);
        if (player.Creature.GetPower<BreakInsightPower>() is { } insight)
            await insight.OnResistancesBroken(context, count);
    }

    internal static decimal DestructionConversion(int brokenCount)
    {
        decimal coefficient = 0.5m;
        for (int i = 0; i < Math.Clamp(brokenCount, 0, 6); i++) coefficient *= 1.1m;
        return coefficient;
    }

    internal static Creature[] ResistanceTargets(CardPlay play)
    {
        if (RollKeywordRules.IsCopiedCard(play?.Card)) return Array.Empty<Creature>();
        Creature owner = play?.Card?.Owner?.Creature;
        if (owner?.CombatState == null) return Array.Empty<Creature>();
        // A selected enemy remains the sole target even if it died or is a minion.
        if (play.Target != null && play.Target.Side != owner.Side)
            return IsEligibleEnemy(play.Target) ? new[] { play.Target } : Array.Empty<Creature>();
        if (play.Card.TargetType == TargetType.AnyEnemy) return Array.Empty<Creature>();
        return owner.CombatState.Enemies.Where(IsEligibleEnemy).ToArray();
    }

    internal static async Task ResolveCardEffects(
        PlayerChoiceContext context, CardPlay play, IReadOnlyList<SixSenseAttribute> attributes,
        IReadOnlyList<SixSenseAttribute> resistanceAttributes)
    {
        Player player = play.Card.Owner;
        if (player?.Creature?.IsAlive != true || player.Creature.CombatState == null) return;

        // Resolve the played card first; newly broken resistances empower subsequent plays.
        foreach (SixSenseAttribute attribute in attributes.Distinct())
        {
            decimal value = EffectValue(player, attribute);
            switch (attribute)
            {
                case SixSenseAttribute.Madness:
                    await CreatureCommandCompatibility.Damage(context, player.Creature, 1m,
                        ValueProp.Unpowered, player.Creature, null);
                    break;
                case SixSenseAttribute.Simplicity:
                    await CreatureCmd.GainBlock(player.Creature, value, ValueProp.Unpowered, null);
                    break;
                case SixSenseAttribute.Talent:
                    if (value > 0m) await PlayerCmd.GainGold(value, player);
                    break;
                default:
                    if (value > 0m)
                    {
                        SixSenseCombatPower state = player.Creature.GetPower<SixSenseCombatPower>() ??
                            await PowerCmd.Apply<SixSenseCombatPower>(context, player.Creature, 1m, player.Creature, null);
                        state?.AddEffect(attribute, value);
                        if (state != null) await state.SyncEffectPowers(context);
                    }
                    break;
            }
            if (!player.Creature.IsAlive) return;
        }

        int brokenCount = 0;
        foreach (Creature enemy in ResistanceTargets(play))
        {
            SixSenseResistancePower resistance = enemy.GetPower<SixSenseResistancePower>();
            if (resistance == null) continue;
            int before = resistance.BrokenCount;
            foreach (SixSenseAttribute attribute in resistanceAttributes.Distinct())
                if (resistance.Reduce(attribute, ResistanceLossForPlayer(player, attribute))) brokenCount++;
            MoreThanOneTruthCombatHelper.OnResistanceTransition(player, enemy, before, resistance.BrokenCount);
        }
        await RegisterResistanceBreaks(context, player, play.Card, brokenCount);
        player.PlayerCombatState?.RecalculateCardValues();
    }
}
