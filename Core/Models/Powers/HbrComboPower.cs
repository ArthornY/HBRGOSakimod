using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>Each stack pays for one complete attack, regardless of its targets or hits.</summary>
public abstract class HbrComboPower : MyLegendNextAttackBasePower
{
    public abstract int ChargesPerApplication { get; }
    internal override int ApplicationsPerAttack => Amount > 0 ? 1 : 0;
    internal override bool RequiresActualHit => true;

    /// <summary>Grant applications as charges; ordinary power amount changes still count individual charges.</summary>
    public static Task<T> Grant<T>(PlayerChoiceContext context, Creature target,
        Creature source, CardModel card, int applications = 1) where T : HbrComboPower =>
        applications <= 0 || target == null ? Task.FromResult<T>(null)
            : PowerCmd.Apply<T>(context, target,
                Math.Min(int.MaxValue, (long)ModelDb.Power<T>().ChargesPerApplication * applications), source, card);

    protected override bool IsOwnerAttack(AttackCommand command) =>
        base.IsOwnerAttack(command) && !command.DamageProps.HasFlag(ValueProp.Unpowered);

    public override Task AfterApplied(Creature applier, CardModel cardSource) =>
        CombatPreviewRefreshHelper.RefreshCreatureIntent(Owner);

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext,
        PowerModel power, decimal amount, Creature applier, CardModel cardSource) =>
        ReferenceEquals(power, this)
            ? CombatPreviewRefreshHelper.RefreshCreatureIntent(Owner)
            : Task.CompletedTask;

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command) =>
        CompleteReservation(choiceContext, command, command.Results.Any(results =>
            results.Any(result => result.Props.HasFlag(ValueProp.Move) &&
                !result.Props.HasFlag(ValueProp.Unpowered))));

    internal override int TransformHitCount(int hitCount, int applications)
    {
        if (applications <= 0 || hitCount <= 0) return hitCount;
        return (int)Math.Min(int.MaxValue, DoublesHitCount
            ? (long)hitCount * 2 : (long)hitCount + ExtraHits);
    }

    protected virtual int ExtraHits => 0;
}

[CustomID("HBR_COMBO_LARGE_POWER")]
public sealed class HbrComboLargePower : HbrComboPower
{
    public override int ChargesPerApplication => 1;
    internal override bool DoublesHitCount => true;
    public override string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_next_attack_double_power.tres";
}

[CustomID("HBR_COMBO_MEDIUM_POWER")]
public sealed class HbrComboMediumPower : HbrComboPower
{
    public override int ChargesPerApplication => 3;
    protected override int ExtraHits => 3;
    public override string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_next_attack_bonus_segments_power.tres";
}

[CustomID("HBR_COMBO_SMALL_POWER")]
public sealed class HbrComboSmallPower : HbrComboPower
{
    public override int ChargesPerApplication => 5;
    protected override int ExtraHits => 1;
    public override string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_next_attack_bonus_segments_power.tres";
}
