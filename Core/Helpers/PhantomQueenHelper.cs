using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomQueenHelper
{
    internal static bool IsPhantom(Creature creature) =>
        creature?.Monster is Queen && creature.Side == CombatSide.Player &&
        creature.PetOwner != null && creature.HasPower<PhantomPower>();

    internal static bool HasLivingAmalgam(Creature queen) =>
        queen?.CombatState?.GetTeammatesOf(queen).Any(creature =>
            creature.IsAlive && creature.Monster is TorchHeadAmalgam &&
            creature.CombatState == queen.CombatState &&
            creature.Side == queen.Side && creature.PetOwner == queen.PetOwner &&
            PhantomHelper.IsMinionOf(queen, creature)) == true;

    internal static void RefreshCurrentMove(Creature queen)
    {
        if (!IsPhantom(queen) || !queen.IsAlive ||
            queen.Monster.NextMove?.Id != "BURN_BRIGHT_FOR_ME_MOVE" ||
            HasLivingAmalgam(queen))
            return;

        // Also repair a Queen already waiting for a nonexistent puppet in an
        // ongoing combat. Use the native reaction when her puppet is lost.
        if (queen.Monster.MoveStateMachine?.States.TryGetValue(
                "ENRAGE_MOVE", out MonsterState state) == true &&
            state is MoveState enrage)
            queen.Monster.SetMoveImmediate(enrage);
    }

    internal static void AfterDeath(Creature queen, Creature creature,
        bool wasRemovalPrevented)
    {
        if (!IsPhantom(queen) || wasRemovalPrevented ||
            creature?.Monster is not TorchHeadAmalgam ||
            creature.CombatState != queen.CombatState ||
            creature.Side != queen.Side || creature.PetOwner != queen.PetOwner ||
            !PhantomHelper.IsMinionOf(queen, creature))
            return;

        RefreshCurrentMove(queen);
    }
}
