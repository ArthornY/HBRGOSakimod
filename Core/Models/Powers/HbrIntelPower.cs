using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HbrIntelPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount
    {
        get
        {
            int kinds = IsMutable ? ThirtyOneDKeywordRules.GetPermanentDistinctCount(Owner?.Player) : 0;
            return kinds >= 2 ? Math.Clamp(Amount, 0, kinds) : 0;
        }
    }
    public string CustomPackedIconPath => "res://images/ui/thirty_one_d/scout_badge.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public override LocString Description
    {
        get
        {
            LocString text = new("powers", Id.Entry + (IsMutable && Owner != null ? ".description" : ".referenceDescription"));
            int count = IsMutable ? ThirtyOneDKeywordRules.GetPermanentDistinctCount(Owner?.Player) : 0;
            int cap = count >= 2 ? count : 0;
            text.Add("IntelCap", cap);
            text.Add("Intel", IsMutable ? Math.Clamp(Amount, 0, cap) : 0);
            text.Add("DamagePercent", IsMutable ? Math.Clamp(Amount, 0, cap) * 5 : 0);
            return text;
        }
    }
    public override Task AfterApplied(Creature applier, CardModel cardSource) { Refresh(); return Task.CompletedTask; }
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel cardSource)
    { if (ReferenceEquals(power, this)) Refresh(); return Task.CompletedTask; }
    public override Task AfterRemoved(Creature oldOwner)
    { CombatPreviewRefreshHelper.RefreshCombatState(oldOwner, nameof(HbrIntelPower)); return Task.CompletedTask; }
    internal void RefreshRoster() { InvokeDisplayAmountChanged(); Refresh(); }
    private void Refresh() => CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(HbrIntelPower));
}
