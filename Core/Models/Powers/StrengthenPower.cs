using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class StrengthenPower : PowerModel
{
    public const int BaseRollBonus = 10;
    public const int UpgradedRollBonus = 20;

    private int _reservedRolls;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasEnhancedRollBonus { get; private set; }

    public int RollBonus => HasEnhancedRollBonus
        ? UpgradedRollBonus
        : BaseRollBonus;

    public override LocString Description => HasEnhancedRollBonus
        ? new LocString(
            "powers",
            "STRENGTHEN_POWER.upgradedDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        if (cardSource is StrengthenHbrgoSaki
            {
                HasEnhancedRollBonus: true
            })
        {
            AssertMutable();
            HasEnhancedRollBonus = true;
        }

        return Task.CompletedTask;
    }

    internal bool TryReserveRoll()
    {
        if (Amount - _reservedRolls <= 0)
        {
            return false;
        }

        AssertMutable();
        _reservedRolls++;
        Flash();
        return true;
    }

    internal async Task ConsumeReservedRolls(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        int amount = Math.Min(Amount, _reservedRolls);
        AssertMutable();
        _reservedRolls = 0;
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -amount,
            Owner,
            cardSource);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedRolls = 0;
    }
}
