using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

// Original Morale is an Eternal level counter, not a timed percentage.
// None means unrestricted repeated grants; their aggregate level caps at ten.
[CustomID("HBR_MORALE_POWER")]
public sealed class HbrMoralePower : PowerModel, ICustomPower
{
    internal const int MaximumLevel = 10;
    internal const int AttributePointsPerLevel = 5;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => IsMutable ? Math.Clamp(Amount, 0, MaximumLevel) : 0;
    public string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal static int GetAttributeBonus(Creature holder) =>
        Math.Clamp(holder?.GetPowerAmount<HbrMoralePower>() ?? 0, 0, MaximumLevel) * AttributePointsPerLevel;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower,
        Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || canonicalPower is not HbrMoralePower || amount <= 0m) return false;
        modifiedAmount = Math.Min(amount, Math.Max(0, MaximumLevel - Amount));
        return modifiedAmount != amount;
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        Refresh();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source)
    {
        if (ReferenceEquals(power, this)) Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        int bounded = Math.Clamp(Amount, 0, MaximumLevel);
        if (Amount != bounded) SetAmount(bounded);
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(HbrMoralePower));
    }
}
