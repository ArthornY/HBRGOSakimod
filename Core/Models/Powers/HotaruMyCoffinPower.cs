using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_MY_COFFIN_POWER")]
public sealed class HotaruMyCoffinPower : PowerModel, ICustomPower
{
    private const decimal NativeMaximum = 999_999_999m;
    private decimal _sacrificePercent;
    private decimal _redemptionPercent;
    private int _upgradedCopies;
    private bool _resolving;
    private ConditionalWeakTable<CannonHitReceipt, ReceiptState> _receipts = new();

    public decimal SacrificePercent => _sacrificePercent;
    public decimal RedemptionPercent => _redemptionPercent;
    public int UpgradedCopies => _upgradedCopies;
    public int ExtraActivationCount => Form == HotaruCannonRetirementChoice.None && CanResolve() ? Amount : 0;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string CoffinStateData
    {
        get => JsonSerializer.Serialize(new SavedState
        {
            SacrificePercent = _sacrificePercent,
            RedemptionPercent = _redemptionPercent,
            UpgradedCopies = _upgradedCopies
        });
        private set
        {
            SavedState state;
            try { state = JsonSerializer.Deserialize<SavedState>(value ?? "") ?? new SavedState(); }
            catch (JsonException) { state = new SavedState(); }
            _sacrificePercent = Math.Max(0m, state.SacrificePercent);
            _redemptionPercent = Math.Max(0m, state.RedemptionPercent);
            _upgradedCopies = Math.Max(0, state.UpgradedCopies);
        }
    }

    private HotaruCannonRetirementChoice Form => IsMutable
        ? Owner?.Player?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Coffin)
            ?? HotaruCannonRetirementChoice.None
        : HotaruCannonRetirementChoice.None;

    public override LocString Title => new("powers", "HOTARU_MY_COFFIN_POWER." + (Form switch
    {
        HotaruCannonRetirementChoice.Sacrifice => "sacrificeTitle",
        HotaruCannonRetirementChoice.Redemption => "redemptionTitle",
        _ => "title"
    }));

    public override LocString Description
    {
        get
        {
            LocString text = new("powers", "HOTARU_MY_COFFIN_POWER." + (Form switch
            {
                HotaruCannonRetirementChoice.Sacrifice => "sacrificeDescription",
                HotaruCannonRetirementChoice.Redemption => "redemptionDescription",
                _ => "normalDescription"
            }));
            int copies = IsMutable ? Amount : 1;
            int upgraded = IsMutable ? UpgradedCopies : 0;
            text.Add("Amount", copies);
            text.Add("ExtraActivations", copies);
            text.Add("NormalDamagePercent", DamageAmplificationLimits.MultiplySaturated(100m, copies));
            text.Add("UpgradedCopies", upgraded);
            text.Add("NormalDoomPercent", DamageAmplificationLimits.MultiplySaturated(100m, upgraded));
            text.Add("SacrificePercent", IsMutable ? SacrificePercent : 100m);
            text.Add("RedemptionPercent", IsMutable ? RedemptionPercent : 50m);
            return text;
        }
    }

    public static async Task Apply(PlayerChoiceContext context, Creature owner, bool upgraded,
        decimal sacrificePercent, decimal redemptionPercent, CardModel source)
    {
        if (owner?.IsAlive != true || owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding) return;
        sacrificePercent = Math.Max(0m, sacrificePercent);
        redemptionPercent = Math.Max(0m, redemptionPercent);
        HotaruMyCoffinPower power = owner.GetPower<HotaruMyCoffinPower>();
        if (power == null)
        {
            power = (HotaruMyCoffinPower)ModelDb.Power<HotaruMyCoffinPower>().ToMutable();
            power._sacrificePercent = sacrificePercent;
            power._redemptionPercent = redemptionPercent;
            power._upgradedCopies = upgraded ? 1 : 0;
            await PowerCmd.Apply(context, power, owner, 1m, owner, source);
            if (power.Owner == owner && power.Amount > 0)
            {
                power._sacrificePercent = DamageAmplificationLimits.MultiplySaturated(sacrificePercent, power.Amount);
                power._redemptionPercent = DamageAmplificationLimits.MultiplySaturated(redemptionPercent, power.Amount);
                power._upgradedCopies = upgraded ? power.Amount : 0;
                power.Refresh();
            }
            return;
        }

        power.AssertMutable();
        int oldAmount = power.Amount;
        decimal oldSacrifice = power.SacrificePercent;
        decimal oldRedemption = power.RedemptionPercent;
        int oldUpgraded = power.UpgradedCopies;
        power._sacrificePercent = DamageAmplificationLimits.AddSaturated(oldSacrifice, sacrificePercent);
        power._redemptionPercent = DamageAmplificationLimits.AddSaturated(oldRedemption, redemptionPercent);
        power._upgradedCopies = (int)Math.Min(int.MaxValue, (long)oldUpgraded + (upgraded ? 1 : 0));
        try { await PowerCmd.ModifyAmount(context, power, 1m, owner, source); }
        finally
        {
            int gained = Math.Max(0, power.Amount - oldAmount);
            power._sacrificePercent = DamageAmplificationLimits.AddSaturated(oldSacrifice,
                DamageAmplificationLimits.MultiplySaturated(sacrificePercent, gained));
            power._redemptionPercent = DamageAmplificationLimits.AddSaturated(oldRedemption,
                DamageAmplificationLimits.MultiplySaturated(redemptionPercent, gained));
            power._upgradedCopies = (int)Math.Min(int.MaxValue, (long)oldUpgraded + (upgraded ? gained : 0));
            power.Refresh();
        }
    }

    internal async Task OnCoffinBlock(PlayerChoiceContext context, Creature cannon, decimal actualBlock,
        CardModel cardSource = null, CardPlay cardPlay = null)
    {
        if (!CanResolve() || _resolving || actualBlock <= 0m || Form != HotaruCannonRetirementChoice.None ||
            cannon?.Monster is not HotaruCannonModel { Kind: HotaruCannonKind.Coffin } ||
            !Millsage.IsOwnedCannon(cannon, Owner.Player)) return;
        Creature target = HighestHpEnemy();
        if (target == null) return;
        decimal block = decimal.Floor(actualBlock);
        await DamageAndDoom(context, target,
            DamageAmplificationLimits.MultiplySaturated(block, Amount),
            DamageAmplificationLimits.MultiplySaturated(block, UpgradedCopies), cardSource, cannon, cardPlay);
    }

    public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel cardSource)
    {
        if (creature != Owner || amount <= 0m || !CanResolve() || _resolving ||
            Form != HotaruCannonRetirementChoice.Sacrifice) return;
        Creature target = HighestHpEnemy();
        if (target == null) return;
        decimal damage = DamageAmplificationLimits.MultiplySaturated(decimal.Floor(amount), SacrificePercent / 100m);
        await DamageAndDoom(new ThrowingPlayerChoiceContext(), target, damage, damage, cardSource);
    }

    internal async Task OnCannonDamage(PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        if (!CanResolve() || _resolving || Form != HotaruCannonRetirementChoice.Redemption || receipt == null ||
            receipt.ActualDamage <= 0m || !Millsage.IsOwnedCannon(receipt.Cannon, Owner.Player)) return;
        ReceiptState state = _receipts.GetValue(receipt, static _ => new ReceiptState());
        if (state.Resolved) return;
        state.Resolved = true;
        Creature target = receipt.Target;
        _resolving = true;
        try
        {
            decimal block = DamageAmplificationLimits.MultiplySaturated(receipt.ActualDamage, RedemptionPercent / 100m);
            if (block <= 0m) return;
            Flash();
            // This is owner block, not a new cannon block phase. Native block
            // modifiers run once, without cannon half-value/Roll/card enchantment.
            decimal gained = await HotaruMyCoffinBlockContext.Gain(Owner, block);
            if (CanResolve() && Millsage.IsEnemy(target, Owner.Player))
                await ApplyDoom(context, target, gained, receipt.CardSource);
        }
        finally { _resolving = false; }
    }

    private async Task DamageAndDoom(PlayerChoiceContext context, Creature target, decimal dpDamage,
        decimal doom, CardModel source, Creature cannon = null, CardPlay play = null)
    {
        _resolving = true;
        try
        {
            Flash();
            HighDimensionalShellPower shell = target.GetPower<HighDimensionalShellPower>();
            decimal maximum = shell?.MaxAmount ?? 0m;
            decimal spent = shell == null || dpDamage <= 0m ? 0m : await shell.DamageDpOnly(context, dpDamage);
            if (spent > 0m)
                await HotaruCannonStatsHelper.NotifyDirectDpDamage(context, Owner, target, spent, maximum, source, cannon, play);
            // Keep the target chosen at the start, including when it has no DP.
            if (CanResolve() && Millsage.IsEnemy(target, Owner.Player)) await ApplyDoom(context, target, doom, source);
        }
        finally { _resolving = false; }
    }

    private Task ApplyDoom(PlayerChoiceContext context, Creature target, decimal amount, CardModel source)
    {
        amount = decimal.Floor(Math.Clamp(amount, 0m, NativeMaximum));
        return amount > 0m ? PowerCmd.Apply<DoomPower>(context, target, amount, Owner, source) : Task.CompletedTask;
    }

    private Creature HighestHpEnemy() => Owner.CombatState.Enemies
        .Where(enemy => Millsage.IsEnemy(enemy, Owner.Player))
        .OrderByDescending(LargeCreatureHealthHelper.GetCurrentHp).FirstOrDefault();

    private bool CanResolve() => IsMutable && Amount > 0 && Owner?.IsAlive == true &&
        Owner.Player?.PlayerCombatState != null && Owner.CombatState != null &&
        !CombatManager.Instance.IsOverOrEnding && !HotaruCannonStatsHelper.IsDamagePreview &&
        !HotaruCannonBlockHelper.IsBlockPreview;

    private void Refresh()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _resolving = false;
        _receipts = new();
    }

    private sealed class ReceiptState { internal bool Resolved; }
    private sealed class SavedState
    {
        public decimal SacrificePercent { get; set; }
        public decimal RedemptionPercent { get; set; }
        public int UpgradedCopies { get; set; }
    }
}
