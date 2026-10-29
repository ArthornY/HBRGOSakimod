using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FragilePower : PowerModel
{
    private decimal? _hbrEffectPercent;

    // Amount is remaining turns; original modifier strength is stored separately.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrModifierCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrEffectData
    {
        get => _hbrEffectPercent.HasValue
            ? _hbrEffectPercent.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        private set => _hbrEffectPercent = decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out decimal parsed) ? Math.Max(0m, parsed) : null;
    }

    internal int ModifierAmount => HbrModifierCount > 0 ? HbrModifierCount : Math.Max(0, Amount);
    internal decimal HbrEffectPercent => _hbrEffectPercent ??
        HbrFragilePowerHelper.ResolvePercent(ModifierAmount, Applier, Owner);

    internal void ConfigureHbr(decimal percent, int modifiers)
    {
        AssertMutable();
        _hbrEffectPercent = Math.Max(0m, percent);
        HbrModifierCount = Math.Max(1, modifiers);
    }

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override Color AmountLabelColor => new("DDA0DD");

    public override LocString Description
    {
        get
        {
            if (!IsMutable || Owner == null)
                return new LocString("powers", Id.Entry + ".hbrReferenceDescription");
            if (!HbrBattleStatsHelper.IsEnabled(Owner)) return base.Description;
            LocString text = new("powers", Id.Entry + ".hbrDescription");
            text.Add("FragilePercent", DamageAmplificationLimits.AddSaturated(HbrEffectPercent,
                HotaruUnfadingScarsPower.AdditionalEffect(Owner) * 100m)
                .ToString("0.#", CultureInfo.InvariantCulture));
            text.Add("Turns", Math.Max(0, Amount));
            text.Add("ModifierCount", ModifierAmount);
            return text;
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0 ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(this)) return;
        if (HbrBattleStatsHelper.IsEnabled(Owner) && !_hbrEffectPercent.HasValue)
        {
            // Legacy counters migrate during lifecycle work, never preview reads.
            ConfigureHbr(HbrEffectPercent, ModifierAmount);
            SetAmount(Math.Min(2, Amount), silent: true);
        }
        Flash();
        await PowerCmd.TickDownDuration(this);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || amount <= 0m || !Owner.HasPower<BreakPower>()) return 1m;
        bool hbr = HbrBattleStatsHelper.IsEnabled(Owner);
        if (hbr && Amount <= 0) return 1m;
        decimal multiplier = (hbr ? 1m + HbrEffectPercent / 100m : 1.5m) +
            HotaruUnfadingScarsPower.AdditionalEffect(Owner);
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }
}
