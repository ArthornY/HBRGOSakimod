using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomTemporaryLevelPower : PowerModel
{
    private const int MaximumDoublings = 20;
    private const decimal MaximumCreatureHp = BigCombatNumbers.MaximumCreatureStat;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AppliedBonusMaxHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AppliedBonusMaxHpData { get; private set; } = string.Empty;

    internal decimal ExactAppliedBonusMaxHp
    {
        get => BigCombatNumbers.ReadSigned(AppliedBonusMaxHpData, AppliedBonusMaxHp);
        private set
        {
            decimal amount = BigCombatNumbers.ClampStat(value);
            AppliedBonusMaxHpData = BigCombatNumbers.Save(amount);
            AppliedBonusMaxHp = BigCombatNumbers.ToNativeSigned(amount);
        }
    }

    private bool _expired;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    public override Task AfterApplied(Creature applier, CardModel cardSource) =>
        RefreshStats();

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext,
        PowerModel power, decimal amount, Creature applier, CardModel cardSource) =>
        ReferenceEquals(power, this) ? RefreshStats() : Task.CompletedTask;

    public override Task AfterRemoved(Creature oldOwner) => Expire(oldOwner);

    public override Task AfterCombatEnd(CombatRoom room) => Expire(Owner);

    internal decimal GetEffectiveLevel(int baseLevel) =>
        Math.Max(0, baseLevel) + GetAdditionalLevels(baseLevel);

    internal async Task RefreshStats()
    {
        if (_expired || Owner?.IsAlive != true ||
            !PhantomHelper.IsOwnedMainPhantom(Owner, Owner.PetOwner)) return;

        decimal baseMaxHp = Math.Max(1m, LargeCreatureHealthHelper.GetMaxHp(Owner) - ExactAppliedBonusMaxHp);
        decimal bonus = Math.Min(Math.Max(0m, MaximumCreatureHp - baseMaxHp),
            HbrResourceValueHelper.Fixed(Owner,
                GetAdditionalLevels(GetBaseLevel()) * PhantomRosterEntry.LevelBonusMaxHp));
        decimal delta = bonus - ExactAppliedBonusMaxHp;
        // Both initial application and stack changes can notify us; apply only the difference.
        ExactAppliedBonusMaxHp = bonus;
        if (delta > 0)
        {
            decimal previousMaxDp = PhantomHighDimensionalShellPower.CalculateInitialAmount(Owner);
            await CreatureCmd.GainMaxHp(Owner, delta);
            decimal gainedMaxDp = PhantomHighDimensionalShellPower.CalculateInitialAmount(Owner) - previousMaxDp;
            // Maximum DP follows max HP automatically; fill only its actual increase,
            // preserving existing DP damage and handling integer rounding at both ends.
            await PhantomHelper.RestoreDp(new ThrowingPlayerChoiceContext(), Owner, gainedMaxDp, null);
        }
        else if (delta < 0)
        {
            await CreatureCmd.SetMaxHp(Owner, Math.Max(1m, LargeCreatureHealthHelper.GetMaxHp(Owner) + delta));
            Owner.GetPower<PhantomHighDimensionalShellPower>()?.ClampToMaximum();
        }

        await CombatPreviewRefreshHelper.RefreshCreatureIntent(Owner);
        await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
    }

    private async Task Expire(Creature creature)
    {
        _expired = true;
        decimal bonus = ExactAppliedBonusMaxHp;
        ExactAppliedBonusMaxHp = 0m;
        if (creature != null && bonus > 0)
        {
            await CreatureCmd.SetMaxHp(creature, Math.Max(1m, LargeCreatureHealthHelper.GetMaxHp(creature) - bonus));
            creature.GetPower<PhantomHighDimensionalShellPower>()?.ClampToMaximum();
        }
        await CombatPreviewRefreshHelper.RefreshCreatureIntent(creature);
        await CombatPreviewRefreshHelper.RefreshOpponentIntents(creature);
    }

    private int GetBaseLevel() => PhantomHelper.IsOwnedMainPhantom(Owner, Owner?.PetOwner)
        ? Owner.PetOwner.GetRelic<MomentOfReign>()?.GetActivePhantomBaseLevel() ?? 1
        : 0;

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (props.HasFlag(ValueProp.Unpowered) || dealer != Owner)
        {
            return 1m;
        }

        decimal multiplier = 1m + GetDamageBonusPercent() / 100m;

        // PhantomPower applies all phantom reduction together, subject to its shared cap.
        return HbrgoSakiDamageMultiplierHelper.Add(
            Math.Max(0m, multiplier),
            this);
    }

    internal decimal GetDamageBonusPercent()
    {
        // Submit the classified request to the common temporary budget. A local
        // clamp here would hide competition with the combat pool and other powers.
        // Effective levels and their HP/reduction/roll effects remain independent.
        return GetAdditionalLevels(GetBaseLevel()) *
            PhantomRosterEntry.LevelDamageBonusPercent *
            PhantomHelper.GetDamageBonusMultiplier(Owner);
    }

    internal decimal GetDamageReductionPercent()
    {
        return GetAdditionalLevels(GetBaseLevel()) *
            PhantomRosterEntry.LevelDamageReductionPercent;
    }

    internal decimal GetSummonerRollBonus()
    {
        return GetAdditionalLevels(GetBaseLevel()) *
            PhantomRosterEntry.LevelSummonerRollBonus;
    }

    private decimal GetAdditionalLevels(int level)
    {
        int doublings = Math.Clamp(
            _expired ? 0 : Amount,
            0,
            MaximumDoublings);
        decimal levelMultiplier = 1L << doublings;
        return Math.Max(0, level) * (levelMultiplier - 1m);
    }
}
