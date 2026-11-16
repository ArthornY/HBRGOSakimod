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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DestructionRatePower : PowerModel
{
    public const int BaselineAmount = 1;
    public const int NormalMaxDisplayAmount = 50;
    public const int EliteMaxDisplayAmount = 150;
    public const int BossMaxDisplayAmount = 300;

    private const decimal DamageConversionMultiplier = 0.5m;
    private decimal _hbrFractionalGain;

    // Native powers retain their integer layer count. The fractional part is
    // saved separately so a skill split into e.g. three hits loses no growth.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrFractionalGainData
    {
        get => _hbrFractionalGain == 0m ? string.Empty :
            _hbrFractionalGain.ToString(CultureInfo.InvariantCulture);
        private set => _hbrFractionalGain = decimal.TryParse(value,
            NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
                ? Math.Clamp(parsed, 0m, 1m) : 0m;
    }
    internal decimal DamageIncreasePerStack =>
        GetDamageIncreasePerStack(IsMutable ? Owner : null);

    internal static decimal GetDamageIncreasePerStack(Creature creature) =>
        GetFullDamageIncreasePerStack(creature) * HotaruAbsolutionUpgradeHelper.GetDestructionRateEffectScale(creature);

    // Native numerics: a flat 0.5% per stack (the six-sense resistance bonus
    // that once multiplied it is gone).
    private static decimal GetFullDamageIncreasePerStack(Creature creature) =>
        HbrBattleStatsHelper.IsEnabled(creature) ? 0.01m : 0.005m;

    // Shared by the damage hook and HUD; querying it must not enter or modify
    // the additive damage context used while resolving an actual hit.
    internal static decimal GetDamageMultiplier(Creature creature, int displayAmount) =>
        1m + GetEffectiveDisplayAmount(creature, displayAmount) * GetDamageIncreasePerStack(creature);

    internal static decimal GetDamageMultiplier(Creature creature, int displayAmount, Creature dealer) =>
        1m + GetEffectiveDisplayAmount(creature, displayAmount) * GetFullDamageIncreasePerStack(creature) *
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
        current = 1m + GetEffectiveDisplayAmount(creature, displayAmount) * activeIncrease;
        maximum = 1m + cap * fullIncrease;
    }

    private static int GetBoundedDisplayAmount(Creature creature, int displayAmount) =>
        Math.Min(GetMaxDisplayAmount(creature), Math.Max(0, displayAmount));

    private static decimal GetEffectiveDisplayAmount(Creature creature, int displayAmount)
    {
        int bounded = GetBoundedDisplayAmount(creature, displayAmount);
        return HbrBattleStatsHelper.IsEnabled(creature)
            ? Math.Min(GetMaxDisplayAmount(creature), bounded +
                (creature?.GetPower<DestructionRatePower>()?._hbrFractionalGain ?? 0m))
            : bounded;
    }

    public override LocString Description
    {
        get
        {
            bool hbr = IsMutable && HbrBattleStatsHelper.IsEnabled(Owner);
            LocString text = hbr ? new LocString("powers", Id.Entry + ".hbrDescription") : base.Description;
            text.Add("damagePerStack", DamageIncreasePerStack * 100m);
            if (hbr) AddHbrDescriptionValues(text);
            return text;
        }
    }

    internal LocString CompactDescription
    {
        get
        {
            bool hbr = IsMutable && HbrBattleStatsHelper.IsEnabled(Owner);
            LocString text = new("powers", Id.Entry + (hbr ? ".hbrCompactDescription" : ".compactDescription"));
            text.Add("damagePerStack", (DamageIncreasePerStack * 100m)
                .ToString("0.000", CultureInfo.InvariantCulture));
            if (hbr) AddHbrDescriptionValues(text);
            return text;
        }
    }

    private void AddHbrDescriptionValues(LocString text)
    {
        GetDisplayMultipliers(Owner, Amount, out decimal current, out decimal maximum);
        text.Add("CurrentDestruction", (current * 100m).ToString("0.##", CultureInfo.InvariantCulture));
        text.Add("MaximumDestruction", (maximum * 100m).ToString("0.##", CultureInfo.InvariantCulture));
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

        bool hbrNumerics = HbrBattleStatsHelper.IsEnabled(creature);
        int baseMaximum = roomType switch
        {
            RoomType.Boss when hbrNumerics => 200,
            RoomType.Elite when hbrNumerics => 200,
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
        int capIncrease = creature?.GetPowerAmount<DestructionRateCapIncreasePower>() ?? 0;
        int doomBonus = coffinTier6
            ? creature.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.DoomPower>() : 0;
        return hbrNumerics
            ? CalculateHbrMaxDisplayAmount(baseMaximum + 100, capIncrease, doomBonus,
                happinessTier5, happinessTier6)
            : CalculateMaxDisplayAmount(baseMaximum, capIncrease, doomBonus,
                happinessTier5, happinessTier6);
    }

    internal static int CalculateHbrMaxDisplayAmount(int baseMaximumPercent, int capIncrease,
        int doomCapBonus, bool happinessTier5, bool happinessTier6)
    {
        long maximum = (long)Math.Max(100, baseMaximumPercent) + Math.Max(0, capIncrease) +
            Math.Max(0, doomCapBonus) + (happinessTier5 ? 200 : 0);
        if (happinessTier6) maximum *= 2;
        return (int)Math.Min(int.MaxValue - (long)BaselineAmount, maximum - 100);
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
        // HBR destruction is a separate final factor, not an attack-buff offset.
        // The shared accumulator recognizes this model without invoking it twice.
        return HbrBattleStatsHelper.IsEnabled(Owner) ? multiplier :
            HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target == Owner) HbrDestructionGainHelper.ObserveHit(target);
        if (target != Owner ||
            result.UnblockedDamage <= 0 ||
            !(Owner.HasPower<BreakPower>() ||
                HotaruAbsolutionUpgradeHelper.CanAccumulateDestructionWithDp(Owner, dealer)))
        {
            return;
        }

        if (HbrBattleStatsHelper.IsEnabled(Owner))
        {
            AddHbrHitGain(dealer, cardSource, props);
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

    internal void AddHbrHitGain(Creature dealer, CardModel cardSource, ValueProp props)
    {
        if (!HbrBattleStatsHelper.IsEnabled(Owner)) return;
        decimal gain = HbrDestructionGainHelper.GetGain(Owner, dealer, cardSource, props) *
            MimicEvilEyeTidalSlashHbrgoSaki.GetDestructionRateMultiplier(cardSource);
        if (gain <= 0m) return;
        decimal room = Math.Max(0m, GetMaxDisplayAmount(Owner) - DisplayAmount);
        decimal total = Math.Min(room, _hbrFractionalGain + gain);
        decimal whole = decimal.Floor(total);
        _hbrFractionalGain = total - whole;
        // This is the already-resolved fixed destruction coefficient. Applying
        // it as another card-given Power would let Roll multiply it a second time.
        if (whole > 0m) SetAmount((int)Math.Min(int.MaxValue, (decimal)Amount + whole));
        else InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }
}
