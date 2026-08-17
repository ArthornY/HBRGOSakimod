using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HangingShotPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasDpDamageBonus { get; private set; }

    public override LocString Description => HasDpDamageBonus
        ? new LocString(
            "powers",
            "HANGING_SHOT_POWER.dpBonusDescription")
        : base.Description;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    public override Color AmountLabelColor => new("E88954");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        if (cardSource is HangingShotHbrgoSaki)
        {
            AssertMutable();
            HasDpDamageBonus = true;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Applier == null ||
            side != Applier.Side ||
            !Owner.IsHittable ||
            Amount <= 0)
        {
            return;
        }

        Flash();
        decimal damage = Amount;
        if (HasDpDamageBonus &&
            Owner.GetPower<HighDimensionalShellPower>()
                is { Amount: > 0 })
        {
            damage *= 1.25m;
        }

        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            damage,
            ValueProp.Move,
            Applier);
        await PowerCmd.Remove(this);
    }
}
