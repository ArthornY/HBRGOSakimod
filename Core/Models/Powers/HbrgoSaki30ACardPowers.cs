using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FallingContinuallyPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("D897F2");

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner ||
            result.TotalDamage <= 0 ||
            !props.HasFlag(ValueProp.Move) ||
            dealer?.HasPower<PhantomPower>() != true ||
            dealer.PetOwner?.Creature == null)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<DoomPower>(
            choiceContext,
            target,
            Amount,
            dealer,
            null);
    }
}

public sealed class ImmaculateDressPower : PowerModel
{
    private ArtifactPower _reservedArtifact;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner ||
            canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff ||
            !canonicalPower.IsVisible ||
            Owner.Player is not { } player)
        {
            return false;
        }

        ArtifactPower artifact = PhantomHelper.GetLivingPhantoms(player)
            .Select(phantom => phantom.GetPower<ArtifactPower>())
            .FirstOrDefault(power => power?.Amount > 0);
        if (artifact == null)
        {
            return false;
        }

        AssertMutable();
        _reservedArtifact = artifact;
        modifiedAmount = 0m;
        return true;
    }

    public override async Task AfterModifyingPowerAmountReceived(
        PowerModel power)
    {
        ArtifactPower artifact = _reservedArtifact;
        AssertMutable();
        _reservedArtifact = null;
        if (artifact?.Owner?.IsAlive == true && artifact.Amount > 0)
        {
            Flash();
            await PowerCmd.Decrement(artifact);
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedArtifact = null;
    }
}

public sealed class PainOfHellfirePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card?.Owner?.Creature != Owner || Owner.Player == null)
        {
            return;
        }

        Flash();
        foreach (Creature phantom in PhantomHelper
                     .GetLivingPhantoms(Owner.Player)
                     .ToArray())
        {
            await PowerCmd.Apply<VigorPower>(
                choiceContext,
                phantom,
                Amount,
                Owner,
                null);
        }
    }
}

public sealed class EcstaticCorridorPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature applier, CardModel cardSource)
    {
        if (!ReferenceEquals(power, this) || amount <= 0m ||
            Owner?.Player?.GetRelic<MomentOfReign>() is not { } relic)
        {
            return;
        }

        int turns = (int)System.Math.Min(int.MaxValue, decimal.Floor(amount));
        await relic.AdvancePhantomRevive(turns);
    }
}
