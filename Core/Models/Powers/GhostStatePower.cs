using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class GhostStatePower : PowerModel
{
    private const decimal IncomingDamageMultiplier = 1.5m;
    private const int StrengthPerTurn = 2;
    private const int DamagePerVulnerable = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("D75D9A");

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side ||
            !participants.Contains(Owner) ||
            !Owner.IsAlive)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            StrengthPerTurn,
            Owner,
            null);
    }

    public decimal ModifyDamageAdditive(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (dealer != Owner ||
            target == null ||
            (props & ValueProp.Move) == 0 ||
            Owner.Monster?.NextMove?.Id !=
                TezukaSaki.FocusedBreakthroughMoveId)
        {
            return 0m;
        }

        int vulnerable = Math.Max(
            0,
            target.GetPower<VulnerablePower>()?.Amount ?? 0);
        return vulnerable * DamagePerVulnerable;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return target == Owner && amount > 0m
            ? IncomingDamageMultiplier
            : 1m;
    }
}
