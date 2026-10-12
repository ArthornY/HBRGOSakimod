using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomPlayerOnlyIntentHelper
{
    private static readonly Action<TheInsatiable, bool> SetLiquified =
        AccessTools.MethodDelegate<Action<TheInsatiable, bool>>(
            AccessTools.PropertySetter(typeof(TheInsatiable), "HasLiquified"));

    internal static Task<bool> TryPerform(Creature phantom, MoveState move,
        IReadOnlyList<Creature> targets)
    {
        if (phantom?.Monster == null || move == null ||
            PhantomPlayerBenefitContext.CurrentPhantom != phantom)
            return Task.FromResult(false);

        switch (phantom.Monster)
        {
            case Noisebot when move.Id == "NOISE_MOVE":
            case SoulFysh when move.Id == "BECKON_MOVE":
                PhantomPlayerBenefitContext.QueueDraw(2);
                break;
            case TheInsatiable insatiable when move.Id == "LIQUIFY_GROUND_MOVE":
                PhantomPlayerBenefitContext.QueueDraw(6);
                SetLiquified(insatiable, true);
                break;
            case Queen when move.Id == "PUPPET_STRINGS_MOVE":
            case CeremonialBeast when move.Id == "BEAST_CRY_MOVE":
                PhantomPlayerBenefitContext.QueueHandLimit(1);
                break;
            case SpectralKnight when move.Id == "HEX":
                PhantomPlayerBenefitContext.QueuePower<PhantomNoExhaustPower>(1);
                break;
            case MagiKnight when move.Id == "DAMPEN_MOVE":
                PhantomPlayerBenefitContext.QueuePower<PhantomCardUpgradeBenefitPower>(1);
                break;
            case HunterKiller when move.Id == "TENDERIZING_GOOP_MOVE":
                PhantomPlayerBenefitContext.QueuePower<PhantomTenderBenefitPower>(1);
                break;
            default:
                return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }
}
