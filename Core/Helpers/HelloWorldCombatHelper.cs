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
                .Count(card => !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(2)));
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
                HbrFragilePowerHelper.GetModifierAmount(enemy), card.HasUpgradeEffect(0), card.HasUpgradeEffect(3));
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
                    await PowerCmd.Apply<HotaruDamageAmplificationPower>(context, owner,
                        HbrBattleStatsHelper.IsEnabled(owner) ? lostBlock / 100m : lostBlock, owner, card);
            }
        }

        if (card.HasUpgradeEffect(6))
            await PowerCmd.Apply<MyLegendNextAttackPower>(context, owner, 2m, owner, card);
    }

    private static Task RemoveRandomBuffs(HelloWorldHbrgoSaki card, Creature enemy, int count) =>
        PowerDispelRules.RandomRemoveBuffs(card.Owner, enemy, count);
}
