using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomThornsHelper
{
    internal static async Task PerformBuffMove(Creature phantom, MoveState move,
        IReadOnlyList<Creature> targets)
    {
        bool grantsTemporaryThorns = phantom.Monster is Toadpole && move.Id == "SPIKEN_MOVE" ||
            phantom.Monster is SpinyToad && move.Id == "PROTRUDING_SPIKES_MOVE";
        int previousAmount = phantom.GetPower<ThornsPower>()?.Amount ?? 0;
        try
        {
            await move.PerformMove(targets);
        }
        finally
        {
            if (grantsTemporaryThorns)
                phantom.GetPower<PhantomPower>()?.RecordNativeThornsGain(
                    (phantom.GetPower<ThornsPower>()?.Amount ?? 0) - previousAmount);
        }
    }

    internal static Task BeforeAttack(Creature phantom, MoveState move)
    {
        if (phantom.Monster is Toadpole && move.Id == "SPIKE_SPIT_MOVE")
            return RemoveTemporaryThorns(phantom);
        if (phantom.Monster is SpinyToad toad && move.Id == "SPIKE_EXPLOSION_MOVE")
            toad.IsSpiny = false;
        return Task.CompletedTask;
    }

    internal static Task AfterAttack(Creature phantom, MoveState move) =>
        phantom.Monster is SpinyToad && move.Id == "SPIKE_EXPLOSION_MOVE"
            ? RemoveTemporaryThorns(phantom)
            : Task.CompletedTask;

    private static async Task RemoveTemporaryThorns(Creature phantom)
    {
        // Attack intents use the phantom damage path instead of the native move,
        // so restore its thorns cleanup here. Track only the move's gain to keep
        // permanent fusion thorns intact, including when an attack is copied.
        int pending = phantom.GetPower<PhantomPower>()?.ConsumeNativeThorns() ?? 0;
        ThornsPower thorns = phantom.GetPower<ThornsPower>();
        if (pending <= 0 || thorns == null) return;
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), thorns,
            -Math.Min(pending, Math.Max(0, thorns.Amount)), phantom, null);
    }
}
