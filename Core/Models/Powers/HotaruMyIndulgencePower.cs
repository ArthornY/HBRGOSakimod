using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_MY_INDULGENCE_POWER")]
public sealed class HotaruMyIndulgencePower : PowerModel, ICustomPower
{
    private static readonly AsyncLocal<GrowthScope> Applying = new();
    private object _consumedOverclockToken;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int NormalCopies { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int UpgradedCopies { get; private set; }

    [SavedProperty]
    public int ConsumedOrdinaryTurn { get; private set; } = -1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    private HotaruCannonRetirementChoice Form => IsMutable
        ? Owner?.Player?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Absolution)
            ?? HotaruCannonRetirementChoice.None
        : HotaruCannonRetirementChoice.None;

    public bool IsSacrificeActive => IsActive && Form == HotaruCannonRetirementChoice.Sacrifice;
    public bool HasUpgradedSacrifice => IsSacrificeActive && UpgradedCopies > 0;

    public override LocString Title => new("powers", "HOTARU_MY_INDULGENCE_POWER." + (Form switch
    {
        HotaruCannonRetirementChoice.Sacrifice => "sacrificeTitle",
        HotaruCannonRetirementChoice.Redemption => "redemptionTitle",
        _ => "title"
    }));

    public override LocString Description
    {
        get
        {
            string prefix = Form switch
            {
                HotaruCannonRetirementChoice.Sacrifice => "sacrifice",
                HotaruCannonRetirementChoice.Redemption => "redemption",
                _ => "normal"
            };
            LocString text = new("powers", "HOTARU_MY_INDULGENCE_POWER." + prefix +
                (IsMutable && UpgradedCopies > 0 ? "UpgradedDescription" : "Description"));
            text.Add("Amount", IsMutable ? Amount : 1);
            text.Add("Multiplier", HotaruSeeYouAgainHelper.Pow2(IsMutable ? Amount : 1));
            text.Add("UpgradedCopies", UpgradedCopies);
            text.Add("BaseDamageBonus", DamageAmplificationLimits.MultiplySaturated(2m, UpgradedCopies));
            text.Add("BaseDamageMultiplier", HotaruSeeYouAgainHelper.Pow2(UpgradedCopies));
            return text;
        }
    }

    private bool IsActive => IsMutable && Amount > 0 && Owner?.IsAlive == true &&
        Owner.Player?.PlayerCombatState != null && Owner.CombatState != null && !CombatManager.Instance.IsOverOrEnding;

    private static HotaruMyIndulgencePower ForCannon(Creature cannon) =>
        cannon?.Monster is HotaruCannonModel && Millsage.IsOwnedCannon(cannon, cannon.PetOwner)
            ? cannon.PetOwner?.Creature?.GetPower<HotaruMyIndulgencePower>() : null;

    public static int GetHitCount(Creature cannon, int baseHits)
    {
        HotaruMyIndulgencePower power = ForCannon(cannon);
        if (baseHits <= 0 || power?.IsActive != true ||
            power.Form == HotaruCannonRetirementChoice.Sacrifice ||
            power.Form == HotaruCannonRetirementChoice.None &&
            ((HotaruCannonModel)cannon.Monster).Kind != HotaruCannonKind.Absolution) return baseHits;
        return (int)Math.Min(int.MaxValue, DamageAmplificationLimits.MultiplySaturated(
            baseHits, HotaruSeeYouAgainHelper.Pow2(power.Amount)));
    }

    public static decimal ModifyBaseDamage(Creature cannon, decimal baseDamage)
    {
        HotaruMyIndulgencePower power = ForCannon(cannon);
        if (power?.IsActive != true || power.UpgradedCopies <= 0) return baseDamage;
        if (power.Form == HotaruCannonRetirementChoice.Redemption)
            return DamageAmplificationLimits.MultiplySaturated(baseDamage, HotaruSeeYouAgainHelper.Pow2(power.UpgradedCopies));
        return power.Form == HotaruCannonRetirementChoice.None &&
            ((HotaruCannonModel)cannon.Monster).Kind == HotaruCannonKind.Absolution
                ? DamageAmplificationLimits.AddSaturated(baseDamage,
                    DamageAmplificationLimits.MultiplySaturated(2m, power.UpgradedCopies)) : baseDamage;
    }

    public static async Task Apply(PlayerChoiceContext context, Creature owner, bool upgraded, CardModel source)
    {
        if (owner?.IsAlive != true || owner.Player?.PlayerCombatState == null || owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding) return;
        Millsage relic = owner.Player.GetRelic<Millsage>();
        var before = owner.CombatState.Allies.Where(cannon => Millsage.IsOwnedCannon(cannon, owner.Player))
            .Select(cannon => (Model: (HotaruCannonModel)cannon.Monster, Cannon: cannon))
            .Select(entry => (entry.Model.Kind, Damage: ModifyBaseDamage(entry.Cannon,
                entry.Model.CurrentDamage + HotaruUncommonCannonHelper.DamageBonus(entry.Cannon))))
            .GroupBy(entry => entry.Kind).Select(group => group.First()).ToArray();
        GrowthScope previousScope = Applying.Value;
        GrowthScope growthScope = new(owner, previousScope);
        Applying.Value = growthScope;
        try
        {
            HotaruMyIndulgencePower power = owner.GetPower<HotaruMyIndulgencePower>();
            if (power == null)
            {
                power = (HotaruMyIndulgencePower)ModelDb.Power<HotaruMyIndulgencePower>().ToMutable();
                power.AddCopy(upgraded);
                try { await PowerCmd.Apply(context, power, owner, 1m, owner, source); }
                finally { power.AlignCopies(upgraded); }
            }
            else
            {
                power.AddCopy(upgraded);
                try { await PowerCmd.ModifyAmount(context, power, 1m, owner, source); }
                finally { power.AlignCopies(upgraded); }
            }
            if (relic != null && !CombatManager.Instance.IsOverOrEnding)
            {
                // Freeze every receipt before the first callback can change
                // another cannon through an unrelated growth effect.
                var receipts = before.Select(entry =>
                {
                    Creature cannon = relic.GetCannon(entry.Kind);
                    decimal after = cannon?.Monster is HotaruCannonModel model
                        ? ModifyBaseDamage(cannon, model.CurrentDamage +
                            HotaruUncommonCannonHelper.DamageBonus(cannon)) : entry.Damage;
                    decimal growth = after > entry.Damage ? after - entry.Damage : 0m;
                    growth -= Math.Min(growth, growthScope.Notified.GetValueOrDefault(entry.Kind));
                    return (entry.Kind, Cannon: cannon, Growth: growth);
                }).Where(entry => entry.Cannon != null && entry.Growth > 0m).ToArray();
                foreach (var receipt in receipts)
                {
                    // A nested Apply already owns the growth it dispatched.
                    // Credit all matching ancestors before entering reactions.
                    for (GrowthScope ancestor = previousScope; ancestor != null; ancestor = ancestor.Parent)
                        if (ReferenceEquals(ancestor.Owner, owner))
                            ancestor.Notified[receipt.Kind] = DamageAmplificationLimits.AddSaturated(
                                ancestor.Notified.GetValueOrDefault(receipt.Kind), receipt.Growth);
                    await relic.NotifyCannonBaseDamageIncreased(context, receipt.Cannon, receipt.Growth, source);
                }
            }
            power.InvokeDisplayAmountChanged();
            owner.InvokePowerModified(power, 0, true);
            CombatPreviewRefreshHelper.RefreshCombatState(owner, nameof(HotaruMyIndulgencePower));
        }
        finally
        {
            if (ReferenceEquals(Applying.Value, growthScope)) Applying.Value = previousScope;
        }
    }

    private void AddCopy(bool upgraded)
    {
        if (upgraded) UpgradedCopies = Math.Min(999_999_999, UpgradedCopies + 1);
        else NormalCopies = Math.Min(999_999_999, NormalCopies + 1);
    }

    private void AlignCopies(bool upgraded)
    {
        long difference = Math.Max(0, Amount) - ((long)NormalCopies + UpgradedCopies);
        if (difference > 0)
        {
            if (upgraded) UpgradedCopies = (int)Math.Min(999_999_999L, UpgradedCopies + difference);
            else NormalCopies = (int)Math.Min(999_999_999L, NormalCopies + difference);
            return;
        }
        int remove = (int)Math.Min(int.MaxValue, -difference);
        if (upgraded)
        {
            int count = Math.Min(remove, UpgradedCopies);
            UpgradedCopies -= count;
            NormalCopies = Math.Max(0, NormalCopies - (remove - count));
        }
        else
        {
            int count = Math.Min(remove, NormalCopies);
            NormalCopies -= count;
            UpgradedCopies = Math.Max(0, UpgradedCopies - (remove - count));
        }
    }

    private bool CanReplay(CardModel card)
    {
        if (!IsSacrificeActive || card?.Owner?.Creature != Owner ||
            CardPlayContextHelper.IsAutoPlay(card) || !HotaruFirstManualCardHelper.IsEligible(card)) return false;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        return token == null ? ConsumedOrdinaryTurn != Owner.Player.PlayerCombatState.TurnNumber
            : !ReferenceEquals(token, _consumedOverclockToken);
    }

    public bool ShouldMakeFirstManualFree(CardModel card) => HasUpgradedSacrifice && CanReplay(card);

    public override int ModifyCardPlayCount(CardModel card, Creature target, int playCount) =>
        CanReplay(card) ? (int)Math.Min(int.MaxValue, (long)playCount + Amount) : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (!CanReplay(card)) return Task.CompletedTask;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        if (token == null) ConsumedOrdinaryTurn = Owner.Player.PlayerCombatState.TurnNumber;
        else _consumedOverclockToken = token;
        Flash();
        return Task.CompletedTask;
    }

    private sealed class GrowthScope(Creature owner, GrowthScope parent)
    {
        internal Creature Owner { get; } = owner;
        internal GrowthScope Parent { get; } = parent;
        internal Dictionary<HotaruCannonKind, decimal> Notified { get; } = new();
    }
}
