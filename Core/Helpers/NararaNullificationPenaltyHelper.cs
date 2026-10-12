using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Reads the owner's current selection without changing cards or consuming RNG.
/// The boss power owns the per-player selection and its lifetime.
/// </summary>
internal static class NararaNullificationPenaltyHelper
{
    internal const int RollPenalty = -50;

    internal static bool IsPenalized(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature is not { IsAlive: true } owner)
            return false;

        // Dual-swords copies register their X value before entering a pile.
        // Deck/reward piles must still keep their normal, out-of-combat costs.
        var combat = card.CombatState ?? (card.Pile == null ? owner.CombatState : null);
        return combat != null && ReferenceEquals(owner.CombatState, combat) &&
            combat.GetOpponentsOf(owner).Any(opponent =>
                opponent.IsAlive &&
                opponent.GetPower<NararaNullificationPower>()?.IsPenalized(card) == true);
    }

    internal static int GetRollPenalty(CardModel card) =>
        IsPenalized(card) ? RollPenalty : 0;

    internal static int GetRollPenalty(CardModel card, Player roller) =>
        card?.IsMutable == true && ReferenceEquals(card.Owner, roller)
            ? GetRollPenalty(card) : 0;
}
