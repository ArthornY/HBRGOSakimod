using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomHighDimensionalShellPower :
    HighDimensionalShellPower
{
    protected override decimal MaximumAmount =>
        CalculateMaximumAmount(Owner);

    internal static decimal CalculateInitialAmount(Creature creature)
    {
        return CalculateMaximumAmount(creature);
    }

    private static decimal CalculateMaximumAmount(Creature creature)
    {
        decimal baseAmount = CalculatePlayerAmount(LargeCreatureHealthHelper.GetMaxHp(creature));
        decimal bonus = BigCombatNumbers.ClampStat(HbrResourceValueHelper.Fixed(creature,
            PhantomHelper.GetExtraMaximumDp(creature)));
        return PhantomHelper.ApplyMaximumDpLoss(creature,
            BigCombatNumbers.ClampStat(baseAmount + bonus));
    }

    internal static bool CanAbsorb(ValueProp props)
    {
        return !props.HasFlag(ValueProp.Unblockable);
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return 1m;
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await base.AfterRemoved(oldOwner);
        if (CurrentDp > 0m ||
            oldOwner?.IsAlive != true ||
            !CombatManager.Instance.IsInProgress ||
            CombatManager.Instance.IsEnding)
        {
            return;
        }

        int strength = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetFixedEffect(
                PhantomHelper.GetFusionEffectData(oldOwner),
                PhantomFusionEffectType.TezukaSaki,
                3m));
        if (strength > 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                oldOwner,
                strength,
                oldOwner,
                null);
        }


        int intangible = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                PhantomHelper.GetFusionEffectData(oldOwner),
                PhantomFusionEffectType.SoulFysh,
                2m,
                1m));
        if (intangible > 0)
        {
            await PowerCmd.Apply<IntangiblePower>(
                new ThrowingPlayerChoiceContext(),
                oldOwner,
                intangible,
                oldOwner,
                null);
        }

        if (PhantomHelper.IsOwnedMainPhantom(oldOwner, oldOwner.PetOwner) &&
            oldOwner.PetOwner.Creature.GetPower<UnbrokenRemnantPower>() is { } unbroken)
        {
            await unbroken.AfterPhantomDpBroken(new ThrowingPlayerChoiceContext(), oldOwner);
        }
    }
}
