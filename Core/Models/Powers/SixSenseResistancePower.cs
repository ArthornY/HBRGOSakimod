using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SixSenseResistancePower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] Resistances { get; private set; } = Array.Empty<int>();

    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
    public override Color AmountLabelColor => new("EF87AD");
    public override int DisplayAmount => Math.Max(0, 6 - BrokenCount);

    public int BrokenCount => Resistances?.Length == 6 ? Resistances.Count(value => value == 0) : 0;
    public decimal DamageConversion => SixSenseCombatHelper.DestructionConversion(BrokenCount);

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("resistances", string.Join("\n", SixSenseBadgeHelper.DisplayOrder.Select(attribute =>
                $"{ElectronicSoldierManual.GetSenseTitle(attribute)}：{GetResistance(attribute)}")));
            text.Add("conversion", DamageConversion * 100m);
            return text;
        }
    }

    internal int GetResistance(SixSenseAttribute attribute) =>
        Resistances?.Length == 6 ? Resistances[(int)attribute] : 0;

    internal bool IsBroken(SixSenseAttribute attribute) =>
        Resistances?.Length == 6 && GetResistance(attribute) == 0;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath()
    {
        if (!IsMutable || Owner?.CombatState == null) return true;
        // Keep the same resistance state while death is only a phase transition.
        return Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(Owner.CombatState, Owner);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (Resistances?.Length != 6 && SixSenseCombatHelper.IsEligibleEnemy(Owner))
        {
            AssertMutable();
            Resistances = SixSenseCombatHelper.RollResistances(
                Owner.CombatState.RunState.CurrentActIndex + 1,
                Owner.CombatState.RunState.Rng.Niche,
                (Owner.CombatState.Encounter?.RoomType ??
                    Owner.CombatState.RunState.CurrentRoom?.RoomType) == RoomType.Boss);
        }
        return Task.CompletedTask;
    }

    internal bool Reduce(SixSenseAttribute attribute, int amount)
    {
        if (Resistances?.Length != 6 || amount <= 0 || GetResistance(attribute) == 0) return false;
        if (Owner?.GetPower<NararaNullificationPower>()?.IsResistanceLocked(attribute) == true) return false;
        AssertMutable();
        Resistances[(int)attribute] = Math.Max(0, GetResistance(attribute) - amount);
        InvokeDisplayAmountChanged();
        return IsBroken(attribute);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context,
        PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (power is MinionPower && power.Owner == Owner) await PowerCmd.Remove(this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        Resistances = Resistances?.ToArray() ?? Array.Empty<int>();
    }
}
