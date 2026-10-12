using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

internal static class BoostUpPhantomHelper
{
    internal static void ApplyDamageBonus(Player player, int percent)
    {
        if (player?.Character is not HbrgoSaki30A || percent <= 0) return;
        foreach (Creature phantom in PhantomHelper.GetLivingPhantoms(player).ToArray())
            phantom.GetPower<PhantomPower>()?.AddCombatDamageBonus(percent);
    }

    internal static bool HasThunderInPermanentDeck(Player player) =>
        player?.Character is HbrgoSaki30A &&
        player.Deck.Cards.OfType<BoostUpHbrgoSaki>()
            .Any(card => card.HasUpgradeEffect(6));

    internal static Task PerformThunderStartAction(Creature phantom)
    {
        // A permanent Thunder grants one extra start action; normal turn-end
        // actions remain intact. Multiple copies do not add more actions.
        if (phantom?.IsAlive != true ||
            phantom.CombatState?.ContainsCreature(phantom) != true ||
            CombatManager.Instance.IsOverOrEnding)
            return Task.CompletedTask;

        return PhantomHelper.PerformCurrentIntent(phantom);
    }
}
