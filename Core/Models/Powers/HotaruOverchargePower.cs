using System;
using System.Globalization;
using System.Linq;
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
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_OVERCHARGE_POWER")]
public sealed class HotaruOverchargePower : PowerModel, ICustomPower
{
    private decimal _damagePercent;
    private bool _resolvingGrowth;

    // Amount counts played copies. A separate scalar preserves mixtures of
    // ordinary (100%) and upgraded (150%) copies, regardless of save load order.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DamagePercentData
    {
        get => _damagePercent.ToString(CultureInfo.InvariantCulture);
        private set => _damagePercent = decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out decimal parsed) ? Math.Max(0m, parsed) : 0m;
    }

    public decimal DamagePercent => _damagePercent;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("DamagePercent", IsMutable ? DamagePercent : 100m);
            text.Add("Amount", IsMutable ? Amount : 1);
            return text;
        }
    }

    public static async Task Apply(PlayerChoiceContext context, Creature owner,
        int damagePercent, CardModel source)
    {
        if (damagePercent <= 0 || owner?.IsAlive != true || owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding) return;

        HotaruOverchargePower power = owner.GetPower<HotaruOverchargePower>();
        if (power == null)
        {
            power = (HotaruOverchargePower)ModelDb.Power<HotaruOverchargePower>().ToMutable();
            power._damagePercent = damagePercent;
            await PowerCmd.Apply(context, power, owner, 1m, owner, source);
            if (power.Owner == owner && power.Amount > 0)
            {
                power._damagePercent = DamageAmplificationLimits.MultiplySaturated(damagePercent, power.Amount);
                owner.InvokePowerModified(power, 0, true);
            }
            return;
        }

        power.AssertMutable();
        int previousAmount = power.Amount;
        decimal previousPercent = power.DamagePercent;
        // Configure the payload before native application dispatches hooks.
        power._damagePercent = DamageAmplificationLimits.AddSaturated(previousPercent, damagePercent);
        try
        {
            await PowerCmd.ModifyAmount(context, power, 1m, owner, source);
        }
        finally
        {
            int gainedCopies = Math.Max(0, power.Amount - previousAmount);
            power._damagePercent = DamageAmplificationLimits.AddSaturated(previousPercent,
                DamageAmplificationLimits.MultiplySaturated(damagePercent, gainedCopies));
            owner.InvokePowerModified(power, 0, true);
        }
    }

    internal async Task OnCannonBaseDamageGrowth(PlayerChoiceContext context, Millsage relic,
        Creature cannon, decimal actualIncrease, CardModel growthSource = null, CardPlay growthPlay = null)
    {
        if (actualIncrease <= 0m || _resolvingGrowth || HotaruCannonStatsHelper.IsDamagePreview ||
            relic?.Owner != Owner?.Player || cannon?.Monster is not HotaruCannonModel ||
            !CanResolve(cannon)) return;

        int chargeAmount = Amount;
        decimal baseDamage = DamageAmplificationLimits.MultiplySaturated(actualIncrease, DamagePercent / 100m);
        if (baseDamage <= 0m) return;
        Creature[] targets = Owner.CombatState.Enemies
            .Where(target => Millsage.IsEnemy(target, relic.Owner)).ToArray();

        _resolvingGrowth = true;
        try
        {
            Flash();
            foreach (Creature target in targets)
            {
                if (!CanResolve(cannon)) break;
                if (!Millsage.IsEnemy(target, relic.Owner)) continue;
                await HotaruCannonStatsHelper.ReactionDamage(context, cannon, target,
                    baseDamage, growthSource, growthPlay, hpOnly: false);
            }
            if (CanResolve(cannon))
                await PowerCmd.Apply<ChargePower>(context, Owner, chargeAmount, Owner, growthSource);
        }
        finally
        {
            _resolvingGrowth = false;
        }
    }

    private bool CanResolve(Creature cannon) => Amount > 0 && Owner?.IsAlive == true &&
        Owner.Player?.PlayerCombatState != null && Owner.CombatState != null &&
        Millsage.IsOwnedCannon(cannon, Owner.Player) && !CombatManager.Instance.IsOverOrEnding;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _resolvingGrowth = false;
    }
}
