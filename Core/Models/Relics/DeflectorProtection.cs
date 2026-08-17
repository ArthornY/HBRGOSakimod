using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class DeflectorProtection : RelicModel
{
    public const int DpPerMaxHp = 2;

    public static readonly Color DpColor = new("6ED7FF");

    private int _dp;
    private int _bonusMaxDp;
    private bool _hasInitializedDp;

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
    public bool HasInitializedDp
    {
        get => _hasInitializedDp;
        private set
        {
            AssertMutable();
            _hasInitializedDp = value;
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
        return Task.CompletedTask;
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (isMimicked || !IsOwnerPlayer(player))
        {
            return Task.CompletedTask;
        }

        RestoreDpFromHealAmount(HealRestSiteOption.GetHealAmount(player));

        return Task.CompletedTask;
    }

    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(Player player, IReadOnlyList<LocString> currentExtraText)
    {
        if (!IsOwnerPlayer(player))
        {
            return currentExtraText;
        }

        LocString extraText = AdditionalRestSiteHealText;
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
        Flash();
        return absorbed;
    }

    public bool IsDpBelowPercent(decimal percent)
    {
        EnsureDpInitialized();
        int maxDp = MaxDp;
        return maxDp > 0 && Dp < maxDp * percent;
    }

    public int RestoreDpByMaxPercent(decimal percent)
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
        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(Owner.Creature) * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(Owner);
        int restoredDp = (int)(maxDp * percent * recoveryMultiplier);
        SetDpClamped(Dp + restoredDp);
        int actualRestoredDp = Dp - previousDp;
        if (actualRestoredDp > 0)
        {
            Flash();
        }

        return actualRestoredDp;
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
        if (restoredDp <= 0 || Dp >= MaxDp)
        {
            return 0;
        }

        int previousDp = Dp;
        SetDpClamped(Dp + restoredDp);
        int actualRestoredDp = Dp - previousDp;
        if (actualRestoredDp > 0)
        {
            Flash();
        }

        return actualRestoredDp;
    }

    public int RestoreDp(int amount)
    {
        EnsureDpInitialized();
        if (amount <= 0 ||
            Dp >= MaxDp ||
            Owner.Creature.HasPower<LastStandPower>())
        {
            return 0;
        }

        int previousDp = Dp;
        SetDpClamped(Dp + amount);
        int actualRestoredDp = Dp - previousDp;
        if (actualRestoredDp > 0)
        {
            Flash();
        }

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
        BonusMaxDp += amount;
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
        Flash();
        return lostDp;
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

    public void SyncAfterMaxHpChanged(int oldMaxHp, int newMaxHp)
    {
        if (!IsMutable || newMaxHp <= 0)
        {
            return;
        }

        if (!HasInitializedDp)
        {
            EnsureDpInitialized();
            return;
        }

        int oldMaxDp = Math.Max(
            0,
            oldMaxHp * DpPerMaxHp + BonusMaxDp);
        int newMaxDp = Math.Max(
            0,
            newMaxHp * DpPerMaxHp + BonusMaxDp);
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
        Dp = maxDp > 0 ? Math.Min(value, maxDp) : value;
    }

    private int GetDisplayDp()
    {
        if (!IsMutable)
        {
            return 0;
        }

        int maxDp = MaxDp;
        return maxDp > 0 ? Math.Min(Dp, maxDp) : Dp;
    }

    private int GetMaxDp()
    {
        Player owner = GetOwnerOrNull();
        return owner == null
            ? 0
            : Math.Max(
                0,
                owner.Creature.MaxHp * DpPerMaxHp +
                BonusMaxDp);
    }

    private int GetRestSiteDpRestore(Player player)
    {
        return GetDpRestoreForHealAmount(player, HealRestSiteOption.GetHealAmount(player), 1m);
    }

    private int GetDpRestoreForHealAmount(Player player, decimal healAmount, decimal recoveryFactor)
    {
        if (player.Creature.MaxHp <= 0 || healAmount <= 0m || recoveryFactor <= 0m)
        {
            return 0;
        }

        decimal healPercent = healAmount / player.Creature.MaxHp;
        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(player.Creature) * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(player);
        decimal dpRestore = MaxDp * healPercent * recoveryFactor * recoveryMultiplier;
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
