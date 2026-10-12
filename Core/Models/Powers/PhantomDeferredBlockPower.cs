using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomDeferredBlockPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingBlock { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target == Owner &&
            dealer != null &&
            dealer.Side != Owner.Side &&
            props.IsPoweredAttack() &&
            result.TotalDamage > 0)
        {
            AssertMutable();
            long gained = (long)result.TotalDamage *
                Math.Max(1, Amount);
            PendingBlock = (int)Math.Min(
                int.MaxValue,
                (long)PendingBlock + gained);
            Flash();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.PetOwner)
        {
            return;
        }

        if (Owner.IsAlive && PendingBlock > 0)
        {
            await CreatureCmd.GainBlock(
                Owner,
                PendingBlock,
                ValueProp.Unpowered,
                null);
        }

        await PowerCmd.Remove(this);
    }
}
