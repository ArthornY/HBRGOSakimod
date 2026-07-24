using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class AngryIzumiYukiPower : PowerModel
{
    private const string _forcedAttackMoveId = "ANGRY_IZUMI_YUKI_ATTACK";
    private const int _forcedAttackBaseDamage = 1;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F85C5C");

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is not EhehePeroroHbrgoSaki ||
            cardPlay.Target != Owner ||
            Owner.Monster == null)
        {
            return;
        }

        await PowerCmd.Apply<AngryIzumiYukiTemporaryStrengthPower>(
            choiceContext,
            Owner,
            Amount,
            Owner,
            null);

        MoveState currentMove = Owner.Monster.NextMove;
        MoveState forcedAttackMove = new(
            _forcedAttackMoveId,
            PerformForcedAttack,
            new SingleAttackIntent(_forcedAttackBaseDamage))
        {
            FollowUpState = currentMove.FollowUpState,
            FollowUpStateId = currentMove.FollowUpStateId ?? currentMove.Id,
            MustPerformOnceBeforeTransitioning = true
        };

        Flash();
        Owner.Monster.SetMoveImmediate(forcedAttackMove, forceTransition: true);
    }

    private async Task PerformForcedAttack(IReadOnlyList<Creature> targets)
    {
        if (Owner.Monster == null || Owner.IsDead || targets.Count == 0)
        {
            return;
        }

        await DamageCmd.Attack(_forcedAttackBaseDamage)
            .FromMonster(Owner.Monster)
            .WithAttackerAnim("Attack", 0.15f)
            .Execute(null);
    }
}
