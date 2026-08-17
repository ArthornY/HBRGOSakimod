using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DamageTransferPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("E64F5E");

    internal Creature GetLivingRedCrimson()
    {
        return FindLivingRedCrimson(Owner);
    }

    internal static Creature FindLivingRedCrimson(Creature owner)
    {
        return owner?.CombatState?.Allies
            .FirstOrDefault(creature =>
                creature.IsAlive &&
                creature.PetOwner == owner.Player &&
                creature.Monster is RedCrimson);
    }

    public override Creature ModifyUnblockedDamageTarget(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer)
    {
        if (target != Owner || amount <= 0m)
        {
            return target;
        }

        return GetLivingRedCrimson() ?? target;
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return target == Owner &&
               amount > 0m &&
               GetLivingRedCrimson() != null
            ? 0m
            : amount;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
