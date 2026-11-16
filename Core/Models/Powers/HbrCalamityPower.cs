using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

// Every original Misfortune application is independent (None/EnemyTurnEnd).
// Amount is its fixed stat reduction; duration never changes that reduction.
[CustomID("HBR_CALAMITY_POWER")]
public sealed class HbrCalamityPower : PowerModel, ICustomPower, IHbrEffectInstance
{
    internal const int InitialDuration = 2;

    [SavedProperty]
    public int RemainingTurns { get; private set; } = InitialDuration;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    decimal IHbrEffectInstance.EffectPotency => Amount;
    int IHbrEffectInstance.Remaining => RemainingTurns;
    HbrEffectProfile IHbrEffectInstance.EffectProfile =>
        new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, InitialDuration);

    public static decimal GetTotalReduction(Creature creature) => creature?.Powers
        .OfType<HbrCalamityPower>().Where(power => power.RemainingTurns > 0)
        .Aggregate(0m, (sum, power) => DamageAmplificationLimits.AddSaturated(sum, Math.Max(0, power.Amount))) ?? 0m;
    public string CustomPackedIconPath => "res://images/powers/hbr_calamity_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Amount", IsMutable ? Math.Max(0, Amount) : 0);
            text.Add("RemainingTurns", Math.Max(0, RemainingTurns));
            text.Add("Turns", Math.Max(0, RemainingTurns));
            return text;
        }
    }

    internal void RefreshDuration()
    {
        AssertMutable();
        RemainingTurns = InitialDuration;
    }

    internal void SetRemainingTurns(int remaining)
    {
        AssertMutable();
        RemainingTurns = Math.Max(0, remaining);
        Owner?.InvokePowerModified(this, 0, true);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (!HbrEffectRules.ShouldTick(this, ((IHbrEffectInstance)this).EffectProfile, side)) return;
        RemainingTurns = Math.Max(0, RemainingTurns - 1);
        Flash();
        if (RemainingTurns == 0 || Amount <= 0)
            await PowerCmd.Remove(this);
        else Owner.InvokePowerModified(this, 0, true);
    }
}
