using System;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Defaults are the modal native effect rows in the frozen cn/skills snapshot.
// Override only a verified homologous part: a mod-authored Fragile attached to
// a skill whose original part is DefenseDown does not silently change kind.
internal static class HbrEffectProfileResolver
{
    internal static HbrEffectProfile Resolve(PowerModel power, CardModel source = null)
    {
        if (power is HbrEffectPower effect) return effect.EffectProfile;
        return ForType(power.GetType(), source);
    }

    internal static HbrEffectProfile ForType(Type effectType, CardModel source = null)
    {
        if (source is HotaruBuoy or HotaruGentleTouch &&
            (effectType == typeof(HbrAttackDownPower) || effectType == typeof(HbrDefenseDownPower) ||
             effectType == typeof(FragilePower)))
            return new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2);
        if (effectType == typeof(HbrEnemyChargePower)) return new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1);
        if (effectType == typeof(HbrStunPower)) return new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnStart, 1);
        if (effectType == typeof(HbrContinuousDpDamagePower) || effectType == typeof(HbrContinuousDamagePower) ||
            effectType == typeof(HbrEnergyDrainPower)) return new(HbrEffectLimit.None, HbrEffectExit.OwnerTurnStart, 4);
        if (effectType == typeof(HbrImprisonPower) || effectType == typeof(HbrBindPower) ||
            effectType == typeof(HbrVirusPower) || effectType == typeof(HbrIllusionPower))
            return new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, 2);
        if (effectType == typeof(HbrConfusionPower)) return new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, 3);
        // MKiryuSkill53, both DefenseDown parts: Default / Eternal.
        if (effectType == typeof(HbrDefenseDownPower) && source is MimicIceFlowerThousandArrowsHbrgoSaki)
            return new(HbrEffectLimit.Default, HbrEffectExit.Eternal, 1);
        if (effectType == typeof(HbrAttackUpPower)) return new(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
        if (effectType == typeof(MindsEyePower)) return new(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
        if (effectType == typeof(HbrSpiritDownPower) || effectType == typeof(HbrCalamityPower))
            return new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, 2);
        if (effectType == typeof(HbrWisdomDownPower)) return new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, 3);
        if (effectType == typeof(HbrCriticalRateUpPower) || effectType == typeof(HbrCriticalDamageUpPower))
            return new(HbrEffectLimit.Only, HbrEffectExit.PlayerTurnEnd, 3);
        if (effectType == typeof(HbrComboLargePower)) return new(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
        if (effectType == typeof(HbrComboMediumPower)) return new(HbrEffectLimit.Default, HbrEffectExit.Count, 3);
        if (effectType == typeof(HbrComboSmallPower)) return new(HbrEffectLimit.Default, HbrEffectExit.Count, 5);
        return new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
    }

    internal static string Duration(HbrEffectProfile profile, int? remaining = null)
    {
        string duration = profile.Exit switch
        {
            HbrEffectExit.Eternal => "本场战斗",
            HbrEffectExit.Count => (remaining ?? profile.DefaultRemaining) + "次攻击",
            HbrEffectExit.OwnerTurnStart or HbrEffectExit.EnemyTurnStart => (remaining ?? profile.DefaultRemaining) + "次",
            _ => (remaining ?? profile.DefaultRemaining) + "回合"
        };
        return profile.Limit == HbrEffectLimit.None ? duration + " · 不计入上限" : duration;
    }

    internal static string LifetimeRule(HbrEffectProfile profile, int remaining) => profile.Exit switch
    {
        HbrEffectExit.Eternal => "持续整场战斗，不会自然减少。",
        HbrEffectExit.Count => $"剩余{remaining}次攻击，每次完整攻击命中后减少1次。",
        HbrEffectExit.PlayerTurnEnd => $"剩余{remaining}回合，持有者的我方回合结束时减少1。",
        HbrEffectExit.EnemyTurnStart => $"剩余{remaining}次，敌方回合开始时减少1。",
        HbrEffectExit.OwnerTurnStart => $"剩余{remaining}次，持有者回合开始结算后减少1。",
        HbrEffectExit.OwnerTurnEnd => $"剩余{remaining}回合，持有者回合结束时减少1。",
        _ => $"剩余{remaining}回合，敌方回合结束时减少1。"
    };

    internal static string SlotRule(HbrEffectProfile profile) => profile.Limit == HbrEffectLimit.None
        ? "本份不计入份数上限，不会被后续普通施加替换。"
        : "通常最多并存2份；满额时，新效果不低于较弱的一份才替换它并重置时间，同强度优先替换剩余最少的一份。";
}
