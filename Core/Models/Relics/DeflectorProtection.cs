using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class DeflectorProtection : RelicModel
{
    public const int DpPerMaxHp = 2;

    public static readonly Color DpColor = new("6ED7FF");

    private int _dp;
    private int _bonusMaxDp;
    private int _cancerMaxDp;
    private int _lostMaxDp;
    private bool _hasInitializedDp;
    private bool _hasUsedMagicSwordBreakProtection;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => IsMutable;

    public override int DisplayAmount => GetDisplayDp();

    public int MaxDp => GetMaxDp();

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/deflector_protection.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    [SavedProperty]
    public int Dp
    {
        get => _dp;
        private set
        {
            AssertMutable();
            int nextValue = Math.Max(0, value);
            if (_dp == nextValue)
            {
                return;
            }

            _dp = nextValue;
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BonusMaxDp
    {
        get => _bonusMaxDp;
        private set
        {
            AssertMutable();
            _bonusMaxDp = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CancerMaxDp
    {
        get => _cancerMaxDp;
        private set
        {
            AssertMutable();
            _cancerMaxDp = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LostMaxDp
    {
        get => _lostMaxDp;
        private set
        {
            AssertMutable();
            _lostMaxDp = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasInitializedDp
    {
        get => _hasInitializedDp;
        private set
        {
            AssertMutable();
            _hasInitializedDp = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasUsedMagicSwordBreakProtection
    {
        get => _hasUsedMagicSwordBreakProtection;
        private set
        {
            AssertMutable();
            _hasUsedMagicSwordBreakProtection = value;
        }
    }

    public override Task AfterObtained()
    {
        if (!HasInitializedDp)
        {
            InitializeDpToMax();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureDpInitialized();
        HasLostDpThisCombat = false;
        return Task.CompletedTask;
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (isMimicked || !IsOwnerPlayer(player))
        {
            return Task.CompletedTask;
        }

        if (Owner.Character is HbrgoHotaru) GainMaxDp(10);
        RestoreDpFromHealAmount(HealRestSiteOption.GetHealAmount(player));

        return Task.CompletedTask;
    }

    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(Player player, IReadOnlyList<LocString> currentExtraText)
    {
        if (!IsOwnerPlayer(player))
        {
            return currentExtraText;
        }

        LocString extraText = Owner.Character is HbrgoHotaru
            ? new LocString("relics", "DEFLECTOR_PROTECTION.hotaruAdditionalRestSiteHealText")
            : AdditionalRestSiteHealText;
        if (extraText == null)
        {
            return currentExtraText;
        }

        extraText.Add("DP", GetRestSiteDpRestore(player));
        return currentExtraText.Concat(new[] { extraText }).ToArray();
    }

    public void EnsureDpInitialized()
    {
        if (!IsMutable)
        {
            return;
        }

        int maxDp = MaxDp;
        if (maxDp <= 0)
        {
            SetDpClamped(0);
            return;
        }

        if (!HasInitializedDp)
        {
            HasInitializedDp = true;
            SetDpClamped(maxDp);
            return;
        }

        if (Dp > maxDp)
        {
            SetDpClamped(maxDp);
        }
    }

    public decimal AbsorbDp(decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        EnsureDpInitialized();
        if (Dp <= 0)
        {
            return 0m;
        }

        int dpToSpend = Math.Min(Dp, (int)amount);
        if (dpToSpend <= 0)
        {
            return 0m;
        }

        decimal absorbed = Math.Min(amount, dpToSpend);
        SetDpClamped(Dp - dpToSpend);
        RegisterMentalStrengthDpLoss(dpToSpend);
        Flash();
        return absorbed;
    }

    public bool IsDpBelowPercent(decimal percent)
    {
        EnsureDpInitialized();
        int maxDp = MaxDp;
        return maxDp > 0 && Dp < maxDp * percent;
    }

    public int RestoreDpByMaxPercent(decimal percent) => RestoreDpByMaxPercent(percent, out _);

    internal int RestoreDpByMaxPercent(decimal percent, out int overflow)
    {
        overflow = 0;
        EnsureDpInitialized();
        int maxDp = MaxDp;
        if (maxDp <= 0 ||
            percent <= 0m ||
            Owner.Creature.HasPower<LastStandPower>())
        {
            return 0;
        }

        int previousDp = Dp;
        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(Owner.Creature) * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(Owner);
        int restoredDp = (int)Math.Clamp(maxDp * percent * recoveryMultiplier, 0m, int.MaxValue);
        int acceptedDp = Math.Min(restoredDp, Math.Max(0, maxDp - previousDp));
        overflow = restoredDp - acceptedDp;
        SetDpClamped(previousDp + acceptedDp);
        int actualRestoredDp = Dp - previousDp;
        if (actualRestoredDp > 0)
        {
            Flash();
        }
        HotaruCannonSupportUpgradeHelper.OnDpRecoveryOverflow(this, overflow);
        return actualRestoredDp;
    }

    internal int RestoreDpByMissingPercent(decimal percent)
    {
        Player owner = GetOwnerOrNull();
        if (owner == null || percent <= 0m)
        {
            return 0;
        }

        EnsureDpInitialized();
        int missingDp = Math.Max(0, MaxDp - Dp);
        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(owner.Creature) * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(owner);
        int restoredDp = (int)Math.Clamp(
            missingDp * percent * recoveryMultiplier,
            0m,
            missingDp);
        return RestoreDp(restoredDp);
    }

    public int RestoreDpToAtLeastMaxPercent(decimal percent)
    {
        EnsureDpInitialized();
        int maxDp = MaxDp;
        if (maxDp <= 0 ||
            percent <= 0m ||
            Owner.Creature.HasPower<LastStandPower>())
        {
            return 0;
        }

        int previousDp = Dp;
        int targetDp = (int)(maxDp * percent);
        if (Dp < targetDp)
        {
            SetDpClamped(targetDp);
        }

        int actualRestoredDp = Dp - previousDp;
        if (actualRestoredDp > 0)
        {
            Flash();
        }

        return actualRestoredDp;
    }

    public int RestoreDpFromHealAmount(decimal healAmount, decimal recoveryFactor = 1m)
    {
        Player owner = GetOwnerOrNull();
        if (owner == null)
        {
            return 0;
        }

        EnsureDpInitialized();
        if (Owner.Creature.HasPower<LastStandPower>())
        {
            return 0;
        }

        int restoredDp = GetDpRestoreForHealAmount(owner, healAmount, recoveryFactor);
        return RestoreDp(restoredDp);
    }

    public int RestoreDp(int amount)
    {
        EnsureDpInitialized();
        if (amount <= 0 ||
            Owner.Creature.HasPower<LastStandPower>())
        {
            return 0;
        }

        int previousDp = Dp;
        int acceptedDp = Math.Min(amount, Math.Max(0, MaxDp - previousDp));
        int overflow = amount - acceptedDp;
        SetDpClamped(previousDp + acceptedDp);
        int actualRestoredDp = Dp - previousDp;
        if (actualRestoredDp > 0)
        {
            Flash();
        }
        HotaruCannonSupportUpgradeHelper.OnDpRecoveryOverflow(this, overflow);
        return actualRestoredDp;
    }

    public int GainMaxDp(int amount)
    {
        if (!IsMutable || amount <= 0)
        {
            return 0;
        }

        EnsureDpInitialized();
        int previousMaxDp = MaxDp;
        var gain = GetMaxDpGainPlan(amount);
        LostMaxDp = gain.Loss;
        if (CancerMaxDp > 0)
        {
            CancerMaxDp = gain.StoredMaximum;
        }
        else
        {
            BonusMaxDp = gain.StoredMaximum;
        }
        int gainedMaxDp = MaxDp - previousMaxDp;
        if (gainedMaxDp <= 0)
        {
            return 0;
        }

        if (!Owner.Creature.HasPower<LastStandPower>())
        {
            SetDpClamped(Dp + gainedMaxDp);
        }
        else
        {
            InvokeDisplayAmountChanged();
        }

        Flash();
        return gainedMaxDp;
    }

    public void ActivateCancerMaxDp(int maxDp)
    {
        if (!IsMutable || maxDp <= 0)
        {
            return;
        }

        EnsureDpInitialized();
        CancerMaxDp = maxDp;
        SetDpClamped(Dp);
        InvokeDisplayAmountChanged();
        Flash();
    }

    public int LoseMaxDp(int amount)
    {
        if (!IsMutable || amount <= 0) return 0;

        EnsureDpInitialized();
        int previousMaximum = MaxDp;
        int lost = Math.Min(previousMaximum, amount);
        if (lost <= 0) return 0;

        // Keep the penalty separate from bonuses so that it survives battle
        // transitions and alternate DP formulas, without creating negative DP.
        LostMaxDp = (int)Math.Min(int.MaxValue, (long)LostMaxDp + lost);
        int previousDp = Dp;
        SetDpClamped(Dp);
        if (Dp < previousDp) RegisterMentalStrengthDpLoss(previousDp - Dp);
        InvokeDisplayAmountChanged();
        Flash();
        return previousMaximum - MaxDp;
    }

    public int LoseDpByCurrentPercent(decimal percent)
    {
        EnsureDpInitialized();
        if (Dp <= 0 || percent <= 0m)
        {
            return 0;
        }

        int lostDp = Math.Min(Dp, (int)(Dp * percent));
        if (lostDp <= 0)
        {
            return 0;
        }

        SetDpClamped(Dp - lostDp);
        CardDamageHistoryHelper.RecordDpDamage(GetOwnerOrNull()?.Creature, lostDp);
        RegisterMentalStrengthDpLoss(lostDp);
        Flash();

        return lostDp;
    }

    public int LoseDp(int amount)
    {
        EnsureDpInitialized();
        if (Dp <= 0 || amount <= 0)
        {
            return 0;
        }

        int lostDp = Math.Min(Dp, amount);
        SetDpClamped(Dp - lostDp);
        CardDamageHistoryHelper.RecordDpDamage(GetOwnerOrNull()?.Creature, lostDp);
        RegisterMentalStrengthDpLoss(lostDp);
        Flash();
        return lostDp;
    }

    public async Task ResolveMentalStrengthDpLossRewards(
        PlayerChoiceContext choiceContext,
        CardModel cardSource = null)
    {
        await ResolveMatchaDpLossReturn();
        ElectronicSoldierManual manual =
            Owner.GetRelic<ElectronicSoldierManual>();
        await (manual?.ResolveMentalStrengthDpLossRewards(
                choiceContext,
                cardSource) ??
            Task.CompletedTask);
    }

    public bool WouldDpBeDepletedBy(decimal amount)
    {
        if (amount <= 0m)
        {
            return false;
        }

        EnsureDpInitialized();
        return Dp > 0 && (int)amount >= Dp;
    }

    public bool TryUseMagicSwordBreakProtection()
    {
        if (!IsMutable || HasUsedMagicSwordBreakProtection)
        {
            return false;
        }

        HasUsedMagicSwordBreakProtection = true;
        return true;
    }

    public void SyncAfterMaxHpChanged(int oldMaxHp, int newMaxHp)
    {
        if (!IsMutable || newMaxHp <= 0)
        {
            return;
        }

        if (TemporaryMaxHpChangeDepth.Value > 0)
        {
            SetDpClamped(Dp);
            InvokeDisplayAmountChanged();
            return;
        }

        if (CancerMaxDp > 0)
        {
            SetDpClamped(Dp);
            InvokeDisplayAmountChanged();
            return;
        }

        if (!HasInitializedDp)
        {
            EnsureDpInitialized();
            return;
        }

        int oldMaxDp = GetNormalMaxDp(oldMaxHp);
        int newMaxDp = GetNormalMaxDp(newMaxHp);
        if (newMaxDp > oldMaxDp &&
            !Owner.Creature.HasPower<LastStandPower>())
        {
            SetDpClamped(Dp + newMaxDp - oldMaxDp);
            return;
        }

        if (Dp > newMaxDp)
        {
            SetDpClamped(newMaxDp);
            return;
        }

        InvokeDisplayAmountChanged();
    }

    private void InitializeDpToMax()
    {
        int maxDp = MaxDp;
        if (maxDp <= 0)
        {
            return;
        }

        HasInitializedDp = true;
        SetDpClamped(maxDp);
    }

    private void SetDpClamped(int value)
    {
        int maxDp = MaxDp;
        Dp = Math.Clamp(value, 0, maxDp);
    }

    private void RegisterMentalStrengthDpLoss(int amount)
    {
        RegisterMatchaDpLoss(amount);
        Owner.GetRelic<ElectronicSoldierManual>()?
            .RegisterMentalStrengthDpLoss(amount);
    }

    private int GetDisplayDp()
    {
        if (!IsMutable)
        {
            return 0;
        }

        int maxDp = MaxDp;
        return Math.Min(Dp, maxDp);
    }

    private int GetMaxDp()
    {
        Player owner = GetOwnerOrNull();
        if (owner == null)
        {
            return 0;
        }

        if (CancerMaxDp > 0)
        {
            return Math.Max(0, CancerMaxDp - LostMaxDp);
        }

        return GetNormalMaxDp(owner.Creature.MaxHp);
    }

    private int GetNormalMaxDp(int maxHp) =>
        Math.Max(0, GetNormalBaseMaxDp(maxHp, BonusMaxDp) - LostMaxDp);

    private static int GetNormalBaseMaxDp(int maxHp, int bonus) =>
        (int)Math.Clamp((long)maxHp * DpPerMaxHp + bonus, 0L, int.MaxValue);

    private (int StoredMaximum, int Loss) GetMaxDpGainPlan(int amount)
    {
        int baseMaximum = CancerMaxDp > 0
            ? CancerMaxDp : GetNormalBaseMaxDp(Owner.Creature.MaxHp, BonusMaxDp);
        // The effective cap bottoms out at zero. Clear any hidden overflow
        // debt, then recover loss before adding bonuses above the old cap.
        int loss = Math.Min(LostMaxDp, baseMaximum);
        int recoveredLoss = Math.Min(loss, amount);
        int stored = CancerMaxDp > 0 ? CancerMaxDp : BonusMaxDp;
        return (AddClamped(stored, amount - recoveredLoss), loss - recoveredLoss);
    }

    private int GetRestSiteDpRestore(Player player)
    {
        int maxDpAfterRest = MaxDp;
        if (player.Character is HbrgoHotaru)
        {
            var gain = GetMaxDpGainPlan(10);
            int baseAfterGain = CancerMaxDp > 0
                ? gain.StoredMaximum : GetNormalBaseMaxDp(player.Creature.MaxHp, gain.StoredMaximum);
            maxDpAfterRest = Math.Max(0, baseAfterGain - gain.Loss);
        }
        return GetDpRestoreForHealAmount(player, HealRestSiteOption.GetHealAmount(player), 1m, maxDpAfterRest);
    }

    private int GetDpRestoreForHealAmount(Player player, decimal healAmount, decimal recoveryFactor, int? maxDpOverride = null)
    {
        if (player.Creature.MaxHp <= 0 || healAmount <= 0m || recoveryFactor <= 0m)
        {
            return 0;
        }

        decimal healPercent = healAmount / player.Creature.MaxHp;
        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(player.Creature) * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(player);
        decimal dpRestore = (maxDpOverride ?? MaxDp) * healPercent * recoveryFactor * recoveryMultiplier;
        return Math.Max(0, (int)dpRestore);
    }

    private bool IsOwnerCreature(Creature creature)
    {
        return GetOwnerOrNull()?.Creature == creature;
    }

    private bool IsOwnerPlayer(Player player)
    {
        return GetOwnerOrNull() == player;
    }

    private Player GetOwnerOrNull()
    {
        if (!IsMutable)
        {
            return null;
        }

        return Owner;
    }
}
