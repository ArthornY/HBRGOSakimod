using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomKaiserClawHelper
{
    private static readonly Func<Rocket, int> RocketStrength =
        AccessTools.MethodDelegate<Func<Rocket, int>>(
            AccessTools.PropertyGetter(typeof(Rocket), "ChargeUpStrengthGain"));
    private static readonly Func<Crusher, int> CrusherStrength =
        AccessTools.MethodDelegate<Func<Crusher, int>>(
            AccessTools.PropertyGetter(typeof(Crusher), "AdaptStrengthGain"));

    internal static bool IsIndependentClaw(Creature creature) =>
        creature?.PetOwner != null && creature.Monster is Rocket or Crusher;

    internal static Task ApplyEntryPowers(Creature claw) =>
        // A pet is beside its summoner, not part of the boss's two-sided pincer.
        // Keep its combat power without applying the encounter's facing markers.
        PowerCmd.Apply<CrabRagePower>(new ThrowingPlayerChoiceContext(),
            claw, 1m, claw, null);

    internal static Task GainStrength(Creature claw)
    {
        using IDisposable scope = PhantomAscensionHelper.BeginPhantomEvaluation();
        int amount = claw.Monster switch
        {
            Rocket rocket => RocketStrength(rocket),
            Crusher crusher => CrusherStrength(crusher),
            _ => 0
        };
        // The native charge loop belongs to the boss background. Starting it
        // here would leave it playing when the phantom replays its laser intent.
        return PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),
            claw, amount, claw, null);
    }

    internal static async Task AfterAttack(Creature phantom, MoveState move,
        IReadOnlyList<Creature> targets)
    {
        if (!IsIndependentClaw(phantom) || !phantom.IsAlive ||
            phantom.Monster is not Crusher)
        {
            return;
        }

        // Phantom attacks replay damage from the intent. These native moves
        // also have effects, but neither advertises a BuffIntent.
        if (move.Id == "BUG_STING_MOVE")
        {
            Creature[] livingTargets = targets.Where(target => target.IsAlive).ToArray();
            var context = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<WeakPower>(context, livingTargets, 2m, phantom, null);
            await PowerCmd.Apply<FrailPower>(context, livingTargets, 2m, phantom, null);
        }
        else if (move.Id == "GUARDED_STRIKE_MOVE")
        {
            await CreatureCmd.GainBlock(phantom, 18m, ValueProp.Move, null);
        }
    }
}
