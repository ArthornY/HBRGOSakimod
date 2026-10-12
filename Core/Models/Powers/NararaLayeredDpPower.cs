using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaLayeredDpPower : HighDimensionalShellPower, ICustomPower
{
    private NararaShibaBossMonster Boss => Owner?.Monster as NararaShibaBossMonster;

    internal decimal LayerMaximum => PhantomHelper.ApplyMaximumDpLoss(
        Owner, Math.Max(1m, Boss?.ExactDpLayerMaximum ?? LargeCreatureHealthHelper.GetMaxHp(Owner)));
    internal int RemainingLayers => Boss?.DpLayersRemaining ?? 1;
    protected override decimal MaximumAmount => LayerMaximum;

    internal static decimal CalculateInitialAmount(Creature creature)
    {
        decimal unadjustedMaximum = creature?.Monster is NararaShibaBossMonster
            { ExactDpLayerMaximum: > 0 } boss
            ? boss.ExactDpLayerMaximum
            : CalculateUnadjustedInitialAmount(creature);
        return PhantomHelper.ApplyMaximumDpLoss(creature, unadjustedMaximum);
    }

    internal static decimal CalculateUnadjustedInitialAmount(Creature creature)
    {
        // The boss's intrinsic layer is 100% HP at the default enemy setting,
        // rather than the ordinary shell's 20% HP. Apply setting changes
        // relative to that default, then reuse difficulty and per-act scaling.
        decimal settingMultiplier = EnemyStatScalingHelper.IsEnemy(creature)
            ? Math.Clamp(HBRGOSakiConfig.EnemyHighDimensionalShellPercent,
                0, HBRGOSakiConfig.MaxEnemyHighDimensionalShellPercent) /
                (decimal)HBRGOSakiConfig.DefaultEnemyHighDimensionalShellPercent
            : 1m;
        return BigCombatNumbers.ScaleHealth(
            LargeCreatureHealthHelper.GetMaxHp(creature),
            settingMultiplier * GetEnemyShellMultiplier(creature));
    }

    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/high_dimensional_shell_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Layers", IsMutable ? RemainingLayers : NararaShibaBossMonster.MaximumDpLayers);
            return text;
        }
    }

    internal override LocString CompactDescription => Description;

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        Boss?.InitializeDpLayers();
        ClampToMaximum();
        await base.AfterApplied(applier, cardSource);
    }

    public override bool TryAbsorb(decimal amount)
    {
        _lastAbsorbedDamage = 0;
        if (amount <= 0m || CurrentDp <= 0m || _pendingBreak) return false;
        if (LayerMaximum <= 0)
        {
            ClampToMaximum();
            return false;
        }
        NararaShibaBossMonster boss = Boss;
        if (boss == null) return base.TryAbsorb(amount);

        decimal incoming = Math.Max(0m, decimal.Floor(amount + PendingExtraDpDamage));
        decimal previous = CurrentDp;
        bool changedLayer = false;
        if (!boss.CanBreakDpLayerThisRound)
        {
            decimal spent = Math.Min(Math.Max(0m, CurrentDp - 1m), incoming);
            SetDpAmount(CurrentDp - spent);
            _lastAbsorbedDamage = spent;
        }
        else if (incoming < CurrentDp)
        {
            decimal spent = incoming;
            SetDpAmount(CurrentDp - spent);
            _lastAbsorbedDamage = spent;
        }
        else
        {
            boss.RecordDpLayerBreak();
            changedLayer = true;
            if (boss.DpLayersRemaining > 0)
            {
                // Spill into the next layer, but never clear two layers in one
                // combat round, even with a single huge hit or extra DP damage.
                decimal spill = Math.Min(LayerMaximum - 1m, incoming - previous);
                SetDpAmount(LayerMaximum - spill);
                _lastAbsorbedDamage = previous + spill;
            }
            else
            {
                SetDpAmount(0m);
                _lastAbsorbedDamage = previous;
                _pendingBreak = true;
            }
        }

        CardDamageHistoryHelper.RecordDpDamage(Owner, Math.Max(0m, _lastAbsorbedDamage - amount));
        if (changedLayer) RefreshLayerDisplay();
        if (_lastAbsorbedDamage > 0) Flash();
        // A protected layer still intercepts all incoming damage at 1 DP.
        return true;
    }

    internal override async Task BreakImmediately(PlayerChoiceContext choiceContext, int extraBreak)
    {
        NararaShibaBossMonster boss = Boss;
        if (boss == null)
        {
            await base.BreakImmediately(choiceContext, extraBreak);
            return;
        }
        if (CurrentDp <= 0m || _pendingBreak) return;
        if (LayerMaximum <= 0)
        {
            ClampToMaximum();
            return;
        }

        _lastAbsorbedDamage = 0;
        if (!boss.CanBreakDpLayerThisRound)
        {
            decimal previousDp = CurrentDp;
            SetDpAmount(1m);
            CardDamageHistoryHelper.RecordDpDamage(Owner, Math.Max(0m, previousDp - CurrentDp));
            return;
        }

        boss.RecordDpLayerBreak();
        if (boss.DpLayersRemaining <= 0)
        {
            await base.BreakImmediately(choiceContext, extraBreak);
            return;
        }

        // An explicit break consumes one layer; it has no numeric overflow.
        decimal consumedDp = CurrentDp;
        SetDpAmount(LayerMaximum);
        CardDamageHistoryHelper.RecordDpDamage(Owner, consumedDp);
        RefreshLayerDisplay();
        await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
    }

    internal void RefreshLayerDisplay()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }
}
