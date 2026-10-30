using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class GhostStatePower : PowerModel
{
    private const decimal IncomingDamageMultiplier = 1.5m;
    private const int StrengthPerTurn = 2;
    private const int DamagePerVulnerable = 2;
    internal const decimal DefenseDownBonusPermille = 6m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("D75D9A");

    public override LocString Description
    {
        get
        {
            if (!IsMutable || Owner == null || !HbrBattleStatsHelper.IsEnabled(Owner)) return base.Description;
            LocString text = new("powers", "GHOST_STATE_POWER.hbrDescription");
            text.Add("BonusPermille", DefenseDownBonusPermille.ToString("0.#", CultureInfo.InvariantCulture));
            return text;
        }
    }

    // A skill-coefficient bonus, resolved before either the enemy or phantom
    // attribute curve. It is never an additive world-space damage amount.
    internal decimal GetBonusUnits(Creature target, ValueProp props)
    {
        if (!IsMutable || Owner == null || target == null || !props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(Owner) ||
            Owner.Monster?.NextMove?.Id != TezukaSaki.FocusedBreakthroughMoveId) return 0m;
        decimal percent = 0m;
        bool hasReal = false;
        foreach (PowerModel power in target.Powers)
        {
            if (power is not HbrDefenseDownPower) continue;
            hasReal = true;
            if (power.Amount > 0)
                percent = DamageAmplificationLimits.AddSaturated(percent,
                    HbrPercentDebuffHelper.GetEffectPercent(power));
        }
        if (!hasReal && HotaruContestVulnerabilityHelper.GetDefenseDown(target) is { Amount: > 0 } view)
            percent = HbrPercentDebuffHelper.GetEffectPercent(view);
        // Snapshots already contain any defense-down base boost. Original game
        // Weak/Vulnerable do not enter this coefficient; Scars enhances it once.
        percent = HotaruUnfadingScarsPower.EnhanceEffect(target, percent);
        return DamageAmplificationLimits.MultiplySaturated(percent,
            HbrSkillPower.LegacyDamageAt100Percent * DefenseDownBonusPermille / 1000m);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side ||
            !participants.Contains(Owner) ||
            !Owner.IsAlive)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            StrengthPerTurn,
            Owner,
            null);
    }

    public decimal ModifyDamageAdditive(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (dealer != Owner ||
            target == null ||
            (props & ValueProp.Move) == 0 ||
            Owner.Monster?.NextMove?.Id !=
                TezukaSaki.FocusedBreakthroughMoveId)
        {
            return 0m;
        }

        // HBR adds GetBonusUnits at the base-skill boundary. Applying the old
        // flat bonus here would add it a second time after stat conversion.
        if (HbrBattleStatsHelper.IsEnabled(Owner)) return 0m;
        int vulnerable = HotaruContestVulnerabilityHelper.GetVulnerableAmount(target);
        return (decimal)vulnerable * DamagePerVulnerable;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return target == Owner && amount > 0m
            ? IncomingDamageMultiplier
            : 1m;
    }
}
