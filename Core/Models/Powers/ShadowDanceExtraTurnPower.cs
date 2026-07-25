using Godot;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ShadowDanceExtraTurnPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasQueuedExtraTurn { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("9B9DFF");

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner.Player &&
            Owner.IsAlive &&
            HasQueuedExtraTurn;
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side &&
            participants.Contains(Owner) &&
            !CombatManager.Instance.PlayersTakingExtraTurn.Contains(Owner.Player))
        {
            AssertMutable();
            HasQueuedExtraTurn = true;
            Flash();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        AssertMutable();
        HasQueuedExtraTurn = false;
        await PowerCmd.Remove(this);
    }
}
