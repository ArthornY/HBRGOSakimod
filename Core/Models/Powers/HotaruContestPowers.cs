using System;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public abstract class HotaruContestPowerBase : PowerModel, ICustomPower
{
    protected abstract decimal PercentPerStack { get; }
    protected abstract HotaruCannonRetirementChoice Form { get; }

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("DDA0DD");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("DamagePercent", PercentPerStack * (IsMutable ? Math.Max(0, Amount) : 1));
            return text;
        }
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (Amount <= 0 || Owner == null || amount <= 0m || dealer == null)
            return 1m;

        // Outgoing risk applies to every source of the marked creature's
        // damage. Only the incoming benefit is restricted by the chosen form.
        bool outgoing = ReferenceEquals(dealer, Owner);
        bool incoming = ReferenceEquals(target, Owner) && MatchesIncomingSource(
            target, dealer, props, cardSource);
        if (!outgoing && !incoming) return 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(1m + Amount * PercentPerStack / 100m, this);
    }

    private bool MatchesIncomingSource(Creature target, Creature dealer,
        ValueProp props, CardModel cardSource)
    {
        // Cannons use the player as their native dealer. Consult the same
        // explicit context in previews and real hits before classifying it as
        // character damage; a null card source does not mean character damage.
        if (!HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props,
                out Creature cannon) && dealer.Monster is HotaruCannonModel)
            cannon = dealer;
        if (cannon != null)
        {
            if (!Millsage.IsOwnedCannon(cannon, cannon.PetOwner)) return false;
            return Form == HotaruCannonRetirementChoice.Redemption ||
                Form == HotaruCannonRetirementChoice.None &&
                cannon.Monster is HotaruContestCannon;
        }
        return Form == HotaruCannonRetirementChoice.Sacrifice && dealer.Player != null;
    }
}

[CustomID("HOTARU_CONTEST_POWER")]
public sealed class HotaruContestPower : HotaruContestPowerBase
{
    protected override decimal PercentPerStack => 100m;
    protected override HotaruCannonRetirementChoice Form => HotaruCannonRetirementChoice.None;
}

[CustomID("HOTARU_CONTEST_SACRIFICE_POWER")]
public sealed class HotaruContestSacrificePower : HotaruContestPowerBase
{
    protected override decimal PercentPerStack => 125m;
    protected override HotaruCannonRetirementChoice Form => HotaruCannonRetirementChoice.Sacrifice;
}

[CustomID("HOTARU_CONTEST_REDEMPTION_POWER")]
public sealed class HotaruContestRedemptionPower : HotaruContestPowerBase
{
    protected override decimal PercentPerStack => 100m;
    protected override HotaruCannonRetirementChoice Form => HotaruCannonRetirementChoice.Redemption;
}
