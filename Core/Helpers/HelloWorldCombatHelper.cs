using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HelloWorldCombatHelper
{
    internal const int DawnCapIncrease = 300;
    internal const int CodeShadowCapPerFragile = 20;

    // Read the permanent deck, never its shuffled combat copies. Each owned,
    // upgraded copy contributes once, including to enemies summoned later.
    internal static int GetInitialBreakBonus(Creature target)
    {
        if (target?.Side != CombatSide.Enemy || target.CombatState == null) return 0;

        return target.CombatState.Players
            .Where(player => player.Creature.Side != target.Side)
            .Sum(player => player.Deck.Cards.OfType<HelloWorldHbrgoSaki>()
                .Count(card => card.HasUpgradeEffect(2)));
    }

    internal static int CalculateCapIncrease(bool hasBreak, int fragile,
        bool dawn, bool codeShadow) => (int)Math.Min(int.MaxValue - 1L,
        (dawn && hasBreak ? DawnCapIncrease : 0L) +
        (codeShadow ? Math.Max(0, fragile) * (long)CodeShadowCapPerFragile : 0L));

    // Called after the base Fragile application. Store only the resulting cap
    // increase so later Fragile gains/losses cannot change this play's snapshot.
    internal static async Task ApplyUpgradeEffects(PlayerChoiceContext context,
        HelloWorldHbrgoSaki card, IEnumerable<Creature> enemies)
    {
        Creature owner = card.Owner.Creature;
        foreach (Creature enemy in enemies.Where(enemy => enemy.IsAlive).ToArray())
        {
            int capIncrease = CalculateCapIncrease(enemy.HasPower<BreakPower>(),
                enemy.GetPowerAmount<FragilePower>(), card.HasUpgradeEffect(0), card.HasUpgradeEffect(3));
            if (capIncrease > 0)
                await PowerCmd.Apply<DestructionRateCapIncreasePower>(context, enemy,
                    capIncrease, owner, card);

            if (card.HasUpgradeEffect(1)) await RemoveRandomBuffs(card, enemy, 2);
        }

        if (card.HasUpgradeEffect(4))
        {
            int startingBlock = owner.Block;
            if (startingBlock > 0)
            {
                await CreatureCommandCompatibility.LoseBlock(context, owner, startingBlock, owner);
                int lostBlock = Math.Max(0, startingBlock - owner.Block);
                if (lostBlock > 0)
                    await PowerCmd.Apply<PhantomDamageInheritancePower>(context, owner, lostBlock, owner, card);
            }
        }

        if (card.HasUpgradeEffect(6))
            await PowerCmd.Apply<MyLegendNextAttackPower>(context, owner, 2m, owner, card);
    }

    private static async Task RemoveRandomBuffs(HelloWorldHbrgoSaki card, Creature enemy, int count)
    {
        // Hidden powers implement combat mechanics, including boss revival and
        // phase transitions. They are not dispellable status buffs.
        // Native revival, replacement and minion identity can use visible Buffs.
        // Native Strength and Dexterity remain Buff models at negative amounts.
        // Removing those would help the enemy rather than strip an actual buff.
        List<PowerModel> candidates = enemy.Powers.Where(power =>
            power.IsVisible && power.Type == PowerType.Buff &&
            power is not (AdaptablePower or SteamEruptionPower or ReattachPower or
                InfestedPower or StockPower or SurprisePower or IllusionPower or MinionPower or
                SandpitPower) &&
            (power is not (StrengthPower or DexterityPower) || power.Amount > 0)).ToList();
        List<PowerModel> selected = new();
        while (selected.Count < count && candidates.Count > 0)
        {
            int index = card.Owner.RunState.Rng.CombatCardGeneration.NextInt(0, candidates.Count);
            selected.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        foreach (PowerModel power in selected)
            if (enemy.Powers.Contains(power)) await PowerCmd.Remove(power);
    }
}
