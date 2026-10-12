using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomSelfDestructHelper
{
    private static readonly Action<GasBomb, bool> SetHasExploded =
        AccessTools.MethodDelegate<Action<GasBomb, bool>>(
            AccessTools.PropertySetter(typeof(GasBomb), "HasExploded"));

    internal static void BeforeAttack(Creature phantom, MoveState move)
    {
        if (phantom?.Monster is GasBomb bomb && move.Id == "EXPLODE_MOVE")
            SetHasExploded(bomb, true);
    }

    internal static async Task AfterAttack(Creature phantom, MoveState move)
    {
        if (phantom?.IsAlive != true || move.Id != "EXPLODE_MOVE" ||
            phantom.Monster is not (GasBomb or WaterfallGiant))
            return;

        // Intent replay has already dealt the damage. Restore only the native
        // move's self-destruction; executing the native move would attack twice.
        await CreatureCmd.Kill(phantom);

        // GasBomb has no follow-up because its native move ends in death. If a
        // death-prevention effect saves a main phantom, keep its sole move valid.
        if (phantom.IsAlive && phantom.Monster is GasBomb &&
            move.FollowUpState == null && move.FollowUpStateId == null)
            move.FollowUpState = move;
    }
}
