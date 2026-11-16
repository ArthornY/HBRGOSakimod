using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BeyondProtectionPower : PowerModel, ICustomPower
{
    private const decimal DamagePercentPerDp = 5m / 1000m;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ConvertedDp { get; private set; }

    private decimal? _legacyDamagePercent;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DamagePercentData
    {
        get => _legacyDamagePercent.HasValue ? DamageAmplificationLimits.Format(DamagePercent) : string.Empty;
        private set
        {
            if (string.IsNullOrEmpty(value)) return;
            _legacyDamagePercent = DamageAmplificationLimits.Parse(value);
        }
    }

    // Old saved fields are migration input only. New conversions put their
    // exact percentage in HotaruDamageAmplificationPower, never in this model.
    public decimal DamagePercent => DamageAmplificationLimits.Clamp(_legacyDamagePercent ?? ConvertedDp * DamagePercentPerDp);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_damage_inheritance_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description => Describe(base.Description);

    internal LocString CompactDescription => Describe(
        LocString.GetIfExists("powers", Id.Entry + ".compactDescription") ?? base.Description);

    private LocString Describe(LocString text)
    {
        DeflectorProtection protection = IsMutable ? Owner?.Player?.GetRelic<DeflectorProtection>() : null;
        text.Add("TemporaryMaxHp", (decimal)(protection?.BeyondProtectionPlayerMaxHp ?? 0) +
            (protection?.BeyondProtectionPhantomMaxHp ?? 0));
        text.Add("DpToReturn", protection?.BeyondProtectionDpToReturn ?? 0);
        return text;
    }

    internal async Task AddConvertedDp(PlayerChoiceContext context, int removedDp, CardModel cardSource)
    {
        AssertMutable();
        await MigrateLegacyDamage(context);
        int converted = Math.Max(0, removedDp);
        if (converted > 0)
            await GrantResolvedDamageBonus(context, converted * DamagePercentPerDp);
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }

    private Task GrantResolvedDamageBonus(PlayerChoiceContext context, decimal percent) =>
        // The lost DP already resolved this value. A card source would let
        // Roll multiply it again; the ability still belongs to this holder.
        PowerCmd.Apply<HotaruDamageAmplificationPower>(context, Owner, percent, Owner, null);

    internal async Task MigrateLegacyDamage(PlayerChoiceContext context)
    {
        if (!IsMutable || Owner == null) return;
        decimal percent = DamagePercent;
        int converted = ConvertedDp;
        decimal? exact = _legacyDamagePercent;
        // Clear before callbacks can play another card and re-enter migration.
        ConvertedDp = 0;
        _legacyDamagePercent = null;
        if (percent <= 0m) return;
        try
        {
            await GrantResolvedDamageBonus(context, percent);
        }
        catch
        {
            ConvertedDp = converted;
            _legacyDamagePercent = exact;
            throw;
        }
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource) =>
        MigrateLegacyDamage(new ThrowingPlayerChoiceContext());

    public override Task BeforeCombatStart() =>
        MigrateLegacyDamage(new ThrowingPlayerChoiceContext());

    public override Task BeforeCardPlayed(CardPlay cardPlay) =>
        cardPlay?.Card?.Owner?.Creature == Owner
            ? MigrateLegacyDamage(new ThrowingPlayerChoiceContext())
            : Task.CompletedTask;

    // Keep the old callable boundary harmless for compatibility. Only the
    // unified damage-amplification ability now participates in damage hooks.
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) => 1m;

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ConvertedDp = 0;
        _legacyDamagePercent = null;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
