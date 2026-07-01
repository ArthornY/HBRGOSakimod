using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class DeflectorProtection : RelicModel
{
    public const int DpPerMaxHp = 2;

    public static readonly Color DpColor = new("6ED7FF");

    private decimal _pendingDodgeBlock;
    private decimal _pendingDodgeBaseBlock;
    private ValueProp _pendingDodgeProps;
    private bool _hasPendingDodgeBlock;
    private bool _isCalculatingDodgeBlock;
    private int _dp;
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
        InitializeDpToMax();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureDpInitialized();
        return Task.CompletedTask;
    }

    public override Task BeforeBlockGained(Creature creature, decimal amount, ValueProp props, CardModel cardSource)
    {
        if (!IsOwnerCreature(creature) || amount <= 0m)
        {
            return Task.CompletedTask;
        }

        _pendingDodgeBaseBlock = amount;
        _pendingDodgeProps = props;
        _hasPendingDodgeBlock = true;
        return Task.CompletedTask;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel cardSource, CardPlay cardPlay)
    {
        if (_isCalculatingDodgeBlock)
        {
            return 1m;
        }

        if (!IsOwnerCreature(target) || block <= 0m)
        {
            return 1m;
        }

        if (cardSource != null && cardPlay == null)
        {
            return 1m;
        }

        _pendingDodgeBlock = block;
        return 0m;
    }

    public override async Task AfterModifyingBlockAmount(decimal modifiedAmount, CardModel cardSource, CardPlay cardPlay)
    {
        decimal blockToConvert = GetBlockToConvert(cardSource, cardPlay);
        ClearPendingDodgeBlock();

        if (blockToConvert <= 0m || GetOwnerOrNull()?.Creature.CombatState == null)
        {
            return;
        }

        int dodgeAmount = (int)Math.Ceiling(blockToConvert / 2m);
        if (dodgeAmount <= 0)
        {
            return;
        }

        Flash();
        Creature ownerCreature = Owner.Creature;
        // The card already affected the block value; the converted Dodge should not receive card power bonuses again.
        await PowerCmd.Apply<DodgePower>(new ThrowingPlayerChoiceContext(), ownerCreature, dodgeAmount, ownerCreature, null);
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

        int dpToSpend = Math.Min(Dp, (int)Math.Ceiling(amount));
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
        if (maxDp <= 0 || percent <= 0m)
        {
            return 0;
        }

        int previousDp = Dp;
        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(Owner.Creature) * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(Owner);
        int restoredDp = (int)Math.Ceiling(maxDp * percent * recoveryMultiplier);
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
        if (maxDp <= 0 || percent <= 0m)
        {
            return 0;
        }

        int previousDp = Dp;
        int targetDp = (int)Math.Ceiling(maxDp * percent);
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

    public bool WouldDpBeDepletedBy(decimal amount)
    {
        if (amount <= 0m)
        {
            return false;
        }

        EnsureDpInitialized();
        return Dp > 0 && (int)Math.Ceiling(amount) >= Dp;
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

        int oldMaxDp = Math.Max(0, oldMaxHp * DpPerMaxHp);
        int newMaxDp = Math.Max(0, newMaxHp * DpPerMaxHp);
        if (newMaxDp > oldMaxDp)
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

    protected override void AfterCloned()
    {
        base.AfterCloned();
        ClearPendingDodgeBlock();
    }

    private decimal GetBlockToConvert(CardModel cardSource, CardPlay cardPlay)
    {
        Creature ownerCreature = GetOwnerOrNull()?.Creature;
        if (!_hasPendingDodgeBlock || ownerCreature?.CombatState == null)
        {
            return _pendingDodgeBlock;
        }

        _isCalculatingDodgeBlock = true;
        try
        {
            return Hook.ModifyBlock(ownerCreature.CombatState, ownerCreature, _pendingDodgeBaseBlock, _pendingDodgeProps, cardSource, cardPlay, out _);
        }
        finally
        {
            _isCalculatingDodgeBlock = false;
        }
    }

    private void ClearPendingDodgeBlock()
    {
        _pendingDodgeBlock = 0m;
        _pendingDodgeBaseBlock = 0m;
        _pendingDodgeProps = default;
        _hasPendingDodgeBlock = false;
        _isCalculatingDodgeBlock = false;
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
        return owner?.Creature.MaxHp * DpPerMaxHp ?? 0;
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
        return Math.Max(0, (int)Math.Floor(dpRestore));
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
