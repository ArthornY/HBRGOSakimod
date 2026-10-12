using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

internal static class SummerGrassDreamCombatHelper
{
    // BeforeHandDraw runs after both energy-reset phases, including OD's late
    // SetEnergy. Only the permanent deck entry grants energy: shuffled or
    // generated combat copies also hear this hook but must not grant it again.
    internal static Task BeforeHandDraw(SummerGrassDreamHbrgoSaki card, Player player)
    {
        if (card == null || player == null || card.Owner != player ||
            player.Creature?.IsAlive != true || player.PlayerCombatState == null ||
            !card.HasUpgradeEffect(5) ||
            !player.Deck.Cards.Any(owned => ReferenceEquals(owned, card)))
            return Task.CompletedTask;

        return PlayerCmd.GainEnergy(1m, player);
    }

    // The caller snapshots DP before this play and invokes this after all of
    // its attacks. ResourceInfo.EnergySpent (first play in a series only) is
    // actual energy paid: free copies, X modifiers and Swordheart add no refund.
    internal static async Task AfterPlay(PlayerChoiceContext context,
        SummerGrassDreamHbrgoSaki card, bool startedAtZeroDp, int actualEnergySpent)
    {
        if (!startedAtZeroDp || card?.Owner?.Creature is not { IsAlive: true } owner)
            return;

        if (card.HasUpgradeEffect(4))
        {
            card.Owner.GetRelic<Overclock>()?.GainLevelProgress(15m);

            var debuffs = owner.Powers.Where(power => power.Type == PowerType.Debuff).ToArray();
            foreach (var debuff in debuffs)
                if (owner.Powers.Contains(debuff)) await PowerCmd.Remove(debuff);

            int refund = Math.Max(0, actualEnergySpent);
            if (refund > 0 && owner.IsAlive)
                await PlayerCmd.GainEnergy(refund, card.Owner);
        }

        if (card.HasUpgradeEffect(6) && owner.IsAlive)
            await PowerCmd.Apply<SummerGrassGhostPathPower>(context, owner, 1m, owner, card);
    }
}
