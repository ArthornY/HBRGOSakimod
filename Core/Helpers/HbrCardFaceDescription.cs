using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Card faces show the same resolved percentage as the application command.
// Only tokens present in the current template are evaluated. No live power,
// reservation, RNG stream or native dynamic variable is ever mutated here.
internal static class HbrCardFaceDescription
{
    private sealed record PercentToken(string Name, string Kind, decimal Basis, bool Self);
    private static readonly Regex Token = new(@"\{(?<name>Face(?<self>Self)?(?<kind>AttackUp|DefenseUp|CriticalRate|CriticalDamage|AttackDown|DefenseDown|DpDefenseDown|Fragile)(?<basis>\d+(?:_\d+)?))\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly ConcurrentDictionary<string, PercentToken[]> Tokens = new(StringComparer.Ordinal);
    private static readonly Regex FixedHits = new(@"\{FaceHits(?<hits>\d+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static void AddParameters(CardModel card, LocString text)
    {
        if (card?.GetType().Assembly != typeof(HbrCardFaceDescription).Assembly || text == null || !text.Exists()) return;
        string raw = text.GetRawText();
        if (!raw.Contains("{Face", StringComparison.Ordinal)) return;
        if (raw.Contains("{FaceHits}", StringComparison.Ordinal))
        {
            int hits = card switch
            {
                TengenBrilliantMoveHbrgoSaki => 3, WorldIsMyLaboratoryHbrgoSaki yamawaki => yamawaki.BaseHitCount,
                HellfireBurnHbrgoSaki ichigo => ichigo.BaseHitCount, FrozenMistMeowHbrgoSaki sumomo => sumomo.BaseHitCount,
                ItsNinjaShowtimeHbrgoSaki ninja => ninja.BaseHitCount, MakeAChangeKillYourselfHbrgoSaki fubuki => fubuki.BaseHits,
                AcePlayerIsNotMeHbrgoSaki akari => akari.BaseHits, DiveAsDeepAsIWantHbrgoSaki => 10,
                YourCombatDeliveryIsHereHbrgoSaki minori => minori.BaseHits,
                ByakkoRoarHbrgoSaki byakko => byakko.HitCount,
                HarvestBountifulGrainsHbrgoSaki => KuraSatomiCombatHelper.GetHitCount(card, HbrCardDescriptionContext.GetTarget(card)),
                NarcissismIsFineHbrgoSaki => 4, _ => card.DynamicVars.TryGetValue("Repeat", out var repeat) ? Math.Max(0, (int)repeat.PreviewValue) : 1
            };
            text.Add("FaceHits", DisplayHits(card, hits));
        }
        foreach (Match hit in FixedHits.Matches(raw))
            text.Add("FaceHits" + hit.Groups["hits"].Value, DisplayHits(card, int.Parse(hit.Groups["hits"].Value, CultureInfo.InvariantCulture)));
        if (card is ChargeBurstHbrgoSaki && raw.Contains("{FaceChargeDamageDisplay}", StringComparison.Ordinal))
        {
            Creature source = card.IsMutable ? card.Owner?.Creature : null;
            decimal gain = card.IsUpgraded ? 0m : card.DynamicVars["ChargePower"].BaseValue;
            if (source != null && card.CombatState != null && gain > 0m)
            {
                gain = Hook.ModifyPowerAmountGiven(card.CombatState, ModelDb.Power<ChargePower>(), source, gain, source, card, out _);
                gain = Hook.ModifyPowerAmountReceived(card.CombatState, ModelDb.Power<ChargePower>(), source, gain, source, out _);
            }
            decimal units = ((source?.GetPowerAmount<ChargePower>() ?? 0m) + Math.Max(0m, gain)) * card.DynamicVars["DamagePerCharge"].BaseValue;
            HbrSkillCardDescription.AddDamageParameters(card, text, "FaceChargeDamage", units);
        }
        if (card is SoulFinisherHbrgoSaki && raw.Contains("{FaceSoulDamageDisplay}", StringComparison.Ordinal))
        {
            decimal units = card.IsMutable ? card.Owner?.GetRelic<RestorationMoment>()?.SpentThisCombat ?? 0m : 0m;
            HbrSkillCardDescription.AddDamageParameters(card, text, "FaceSoulDamage", ModeTransitionHelper.GetAdjustedEffectAmount(card, units));
        }
        if (card is MimicFullBodyShotHbrgoSaki fullBody && raw.Contains("{FaceFragileBreakDamageDisplay}", StringComparison.Ordinal))
        {
            using var projection = HbrCardEffectPreview.FullBodyShot(fullBody, HbrCardDescriptionContext.GetTarget(card), out decimal units, out decimal gain);
            HbrSkillCardDescription.AddDamageParameters(card, text, "FaceFragileBreakDamage", units);
            if (units <= 0m) text.Add("FaceFragileBreakDamageDisplay", "0");
            text.Add("FaceFragileBreakGain", gain.ToString("0", CultureInfo.InvariantCulture));
        }
        if (card is DpSoulStrikeHbrgoSaki && raw.Contains("{FaceDpSoulDamageDisplay}", StringComparison.Ordinal))
        {
            Creature owner = card.IsMutable ? card.Owner?.Creature : null;
            int available = owner?.Player?.GetRelic<DeflectorProtection>()?.Dp ?? 0;
            decimal lost = Math.Min(available, HbrResourceValueHelper.Fixed(owner, card.DynamicVars["DpLoss"].BaseValue));
            decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(card, lost * card.DynamicVars["DamagePercent"].BaseValue / 100m);
            HbrSkillCardDescription.AddAbsoluteDamageParameters(card, text, "FaceDpSoulDamage", damage, card.DynamicVars["DamagePercent"].BaseValue);
        }
        if (card is FreshDeathHbrgoSaki fresh)
        {
            decimal basis = fresh.HasUpgradeEffect(FreshDeathHbrgoSaki.NearDeathExperiment) && fresh.IsMutable && fresh.Owner?.Creature != null &&
                LargeCreatureHealthHelper.GetCurrentHp(fresh.Owner.Creature) < LargeCreatureHealthHelper.GetMaxHp(fresh.Owner.Creature) * .5m ? 120m : 60m;
            text.Add("FaceFreshAttackUp", Percent(card, "AttackUp", basis));
            text.Add("FaceFreshRecipients", fresh.HasUpgradeEffect(FreshDeathHbrgoSaki.SaturatedEnhancement) ? "我方全体获得" : "选择1名玩家，使其获得");
        }
        foreach (PercentToken token in Tokens.GetOrAdd(raw, key => Token.Matches(key).Cast<Match>().Select(match =>
            new PercentToken(match.Groups["name"].Value, match.Groups["kind"].Value,
                decimal.Parse(match.Groups["basis"].Value.Replace('_', '.'), CultureInfo.InvariantCulture), match.Groups["self"].Success)).Distinct().ToArray()))
            text.Add(token.Name, Percent(card, token.Kind, token.Basis, token.Self));
    }
    private static string DisplayHits(CardModel card, int hits)
    {
        int final = hits;
        if (card.IsMutable && card.Owner?.Creature != null && card.CombatState != null && !HbrCardDescriptionContext.IsUnpowered(card))
            final = (int)Hook.ModifyAttackHitCount(card.CombatState,
                new MegaCrit.Sts2.Core.Commands.Builders.AttackCommand(0m).FromCardCompat(card), hits);
        return final > hits ? "[green]" + final + "[/green]" : final < hits ? "[red]" + final + "[/red]" : final.ToString(CultureInfo.InvariantCulture);
    }
    internal static string Percent(CardModel card, string kind, decimal basis, bool self = false)
    {
        Creature source = card.IsMutable ? card.Owner?.Creature : null;
        Creature target = self ? source : HbrCardDescriptionContext.GetTarget(card);
        PowerModel power = kind switch
        {
            "AttackUp" => ModelDb.Power<HbrAttackUpPower>(), "DefenseUp" => ModelDb.Power<HbrDefenseUpPower>(),
            "CriticalRate" => ModelDb.Power<HbrCriticalRateUpPower>(), "CriticalDamage" => ModelDb.Power<HbrCriticalDamageUpPower>(),
            "AttackDown" => ModelDb.Power<HbrAttackDownPower>(), "DefenseDown" => ModelDb.Power<HbrDefenseDownPower>(),
            "DpDefenseDown" => ModelDb.Power<HbrDpDefenseDownPower>(),
            _ => ModelDb.Power<FragilePower>()
        };
        bool debuff = HbrPercentDebuffHelper.Supports(power);
        if (debuff && !self && source != null && target?.Side == source.Side) target = null;
        if (!debuff && (target == null || source != null && target.Side != source.Side)) target = source;
        decimal result = basis;
        if (source != null && HbrSkillCardDescription.IsEnabled(card) && !HbrCardDescriptionContext.IsUnpowered(card))
        {
            if (debuff)
            {
                if (target != null) result = HbrPercentDebuffHelper.ResolveBasis(power, basis, source, target);
                else if (power is HbrDefenseDownPower or HbrDpDefenseDownPower) result = HbrPercentDebuffHelper.ApplyDefenseDownSourceBoost(source, basis);
            }
            else
            {
                if (card.CombatState != null)
                {
                    result = Hook.ModifyPowerAmountGiven(card.CombatState, power, source, basis, target, card, out _);
                    result = Hook.ModifyPowerAmountReceived(card.CombatState, power, target, result, source, out _);
                }
                result = ((HbrEffectPower)power).ResolvePotency(result, source, target);
            }
        }
        string number = result.ToString("0.#", CultureInfo.InvariantCulture);
        return result > basis ? "[green]" + number + "[/green]" : result < basis ? "[red]" + number + "[/red]" : number;
    }
    internal static string Duration(HbrEffectProfile profile) => profile.Exit switch
    {
        HbrEffectExit.Eternal => "本场战斗", HbrEffectExit.Count => profile.DefaultRemaining + "次攻击",
        _ => profile.DefaultRemaining + "回合"
    };
}
