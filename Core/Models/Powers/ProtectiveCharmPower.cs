using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

// An owned, readable Artifact-like counter. Prefer native Artifact when both
// exist so a single incoming debuff never consumes one charge from each.
public sealed class ProtectiveCharmPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/powers/hbr_defense_up_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower,
        Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || Amount <= 0 || amount == 0m || canonicalPower == null ||
            canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff || !canonicalPower.IsVisible ||
            Owner.Powers.Any(power => power is ArtifactPower && power.Amount > 0)) return false;
        modifiedAmount = 0m;
        return true;
    }

    public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        if (Amount > 0 && Owner?.Powers.Contains(this) == true)
            await PowerCmd.Decrement(this);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        Refresh(Owner);
        return Task.CompletedTask;
    }
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this)) Refresh(Owner);
        return Task.CompletedTask;
    }
    public override Task AfterRemoved(Creature oldOwner)
    {
        Refresh(oldOwner);
        return Task.CompletedTask;
    }
    private static void Refresh(Creature creature)
    {
        if (creature?.CombatState != null)
            CombatPreviewRefreshHelper.RefreshCombatState(creature, nameof(ProtectiveCharmPower));
    }
}
