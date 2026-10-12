using System;
using System.Globalization;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DestructionRatePower : PowerModel
{
    public const int BaselineAmount = 1;
    public const int NormalMaxDisplayAmount = 50;
    public const int EliteMaxDisplayAmount = 150;
    public const int BossMaxDisplayAmount = 300;

    private const decimal DamageConversionMultiplier = 0.5m;
    internal decimal DamageIncreasePerStack =>
        GetDamageIncreasePerStack(IsMutable ? Owner : null);

    internal static decimal GetDamageIncreasePerStack(Creature creature) =>
        GetFullDamageIncreasePerStack(creature) * HotaruAbsolutionUpgradeHelper.GetDestructionRateEffectScale(creature);

    private static decimal GetFullDamageIncreasePerStack(Creature creature) =>
        (creature?.GetPower<SixSenseResistancePower>()?.DamageConversion ?? 0.5m) / 100m;

    // Shared by the damage hook and HUD; querying it must not enter or modify
    // the additive damage context used while resolving an actual hit.
    internal static decimal GetDamageMultiplier(Creature creature, int displayAmount) =>
        1m + GetBoundedDisplayAmount(creature, displayAmount) * GetDamageIncreasePerStack(creature);

    internal static decimal GetDamageMultiplier(Creature creature, int displayAmount, Creature dealer) =>
        1m + GetBoundedDisplayAmount(creature, displayAmount) * GetFullDamageIncreasePerStack(creature) *
            HotaruAbsolutionUpgradeHelper.GetDestructionRateEffectScale(creature, dealer);

    // The stored cap remains available after DP breaks. Its readout must not
    // shrink to 100% just because destruction is currently inactive or halved.
    internal static decimal GetMaximumDamageMultiplier(Creature creature) =>
        1m + GetMaxDisplayAmount(creature) * GetFullDamageIncreasePerStack(creature);

    internal static void GetDisplayMultipliers(Creature creature, int powerAmount,
        out decimal current, out decimal maximum)
    {
        // Both caption values describe the same instant. Share its cap and
        // conversion reads without keeping gameplay values across refreshes.
        int cap = GetMaxDisplayAmount(creature);
        int displayAmount = (int)Math.Clamp((long)powerAmount - BaselineAmount, 0L, cap);
        decimal fullIncrease = GetFullDamageIncreasePerStack(creature);
        decimal activeIncrease = fullIncrease * HotaruAbsolutionUpgradeHelper.GetDestructionRateEffectScale(creature);
        current = 1m + displayAmount * activeIncrease;
        maximum = 1m + cap * fullIncrease;
    }

    private static int GetBoundedDisplayAmount(Creature creature, int displayAmount) =>
        Math.Min(GetMaxDisplayAmount(creature), Math.Max(0, displayAmount));

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("damagePerStack", DamageIncreasePerStack * 100m);
            return text;
        }
    }

    internal LocString CompactDescription
    {
        get
        {
            LocString text = new("powers", Id.Entry + ".compactDescription");
            text.Add("damagePerStack", (DamageIncreasePerStack * 100m)
                .ToString("0.000", CultureInfo.InvariantCulture));
            return text;
        }
    }

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override int DisplayAmount =>
        GetBoundedDisplayAmount(IsMutable ? Owner : null,
            (int)Math.Max(0L, (long)Amount - BaselineAmount));

    public override Color AmountLabelColor => new("FF9C6E");

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner ||
            canonicalPower is not DestructionRatePower ||
            amount <= 0m)
        {
            return false;
        }

        int maximumAmount =
            BaselineAmount + GetMaxDisplayAmount(target);
        decimal cappedAmount = Math.Min(
            amount,
            Math.Max(0L, (long)maximumAmount - Amount));
        if (cappedAmount == amount)
        {
            return false;
        }

        modifiedAmount = cappedAmount;
        return true;
    }

    public static int GetMaxDisplayAmount(Creature creature)
    {
        RoomType roomType =
            creature?.CombatState?.Encounter?.RoomType ??
            creature?.CombatState?.RunState.CurrentRoom?.RoomType ??
            RoomType.Monster;

        int baseMaximum = roomType switch
        {
            RoomType.Boss => BossMaxDisplayAmount,
            RoomType.Elite => EliteMaxDisplayAmount,
            _ => NormalMaxDisplayAmount,
        };
        bool coffinTier6 = false;
        bool happinessTier5 = false;
        bool happinessTier6 = false;
        if (creature?.CombatState is { } combatState)
        {
            // All three upgrades share the same opposing owners. Read that
            // set once, while retaining the union across multiple players.
            foreach (var player in combatState.Players)
            {
                if (player.Creature.Side == creature.Side || player.GetRelic<Millsage>() is not { } relic)
                    continue;
                coffinTier6 |= relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 6);
                happinessTier5 |= relic.HasCannonUpgrade(HotaruCannonKind.Happiness, 5);
                happinessTier6 |= relic.HasCannonUpgrade(HotaruCannonKind.Happiness, 6);
                if (coffinTier6 && happinessTier5 && happinessTier6) break;
            }
        }
        return CalculateMaxDisplayAmount(baseMaximum,
            creature?.GetPowerAmount<DestructionRateCapIncreasePower>() ?? 0,
            coffinTier6
                ? creature.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.DoomPower>() : 0,
            happinessTier5,
            happinessTier6);
    }

    internal static int CalculateMaxDisplayAmount(int baseMaximum, int capIncrease, int doomCapBonus,
        bool happinessTier5, bool happinessTier6)
    {
        long maximum = (long)Math.Max(0, baseMaximum) + Math.Max(0, capIncrease) +
            Math.Max(0, doomCapBonus) + (happinessTier5 ? 200 : 0);
        if (happinessTier6) maximum *= 2;
        return (int)Math.Min(int.MaxValue - (long)BaselineAmount, maximum);
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        decimal multiplier = target == Owner && amount > 0m
            // GetDamageMultiplier already bounds the counter against the
            // current cap. Avoid a second cap query through DisplayAmount.
            ? GetDamageMultiplier(Owner, (int)Math.Max(0L, (long)Amount - BaselineAmount), dealer)
            : 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner ||
            result.UnblockedDamage <= 0 ||
            !(Owner.HasPower<BreakPower>() ||
                HotaruAbsolutionUpgradeHelper.CanAccumulateDestructionWithDp(Owner, dealer)))
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<DestructionRatePower>(
            choiceContext,
            Owner,
            result.UnblockedDamage *
            DamageConversionMultiplier *
            MimicEvilEyeTidalSlashHbrgoSaki
                .GetDestructionRateMultiplier(cardSource),
            dealer ?? Owner,
            cardSource);
    }
}
