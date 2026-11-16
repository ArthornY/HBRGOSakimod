using System;
using System.Globalization;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// What the phone manual shows, read from the manual relic and the pending allocation draft.
internal static class HbrManualPhoneData
{
    // The stat dock and the battle stat radar: 力量 灵巧 智慧 体力 精神 运气, each fed by one sense.
    internal static readonly SixSenseAttribute[] StatSenses =
    {
        SixSenseAttribute.Madness, SixSenseAttribute.LeadershipCharm, SixSenseAttribute.Talent,
        SixSenseAttribute.Gentleness, SixSenseAttribute.MentalStrength, SixSenseAttribute.Simplicity
    };

    internal static readonly string[] StatUses =
        { "HP 攻击的主要参照", "DP 攻击的主要参照", "格挡倍率的参照", "防御参照属性", "防御参照属性", "自然暴击概率的参照" };

    internal static int SenseIndex(SixSenseAttribute sense) =>
        Array.IndexOf(ElectronicSoldierManual.AllSixSenseAttributes.ToArray(), sense);

    internal static int StatIndex(SixSenseAttribute sense) => Array.IndexOf(StatSenses, sense);

    internal static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static bool IsHotaru(ElectronicSoldierManual manual) => manual?.Owner?.Character is HbrgoHotaru;

    internal static bool IsThirtyA(ElectronicSoldierManual manual) => manual?.Owner?.Character is HbrgoSaki30A;

    // Head crop for the portrait window, following the form the energy counter shows.
    internal static string FaceTexture(ElectronicSoldierManual manual)
    {
        if (IsHotaru(manual)) return "face_hotaru";
        if (IsThirtyA(manual)) return "face_30a";
        return HbrgoSakiCombatFormHelper.GetForm(manual?.Owner?.Creature) switch
        {
            HbrgoSakiCombatForm.DualSwords => "face_31o_dual",
            HbrgoSakiCombatForm.Bow => "face_31o_bow",
            HbrgoSakiCombatForm.Scythe or HbrgoSakiCombatForm.ThirtyA => "face_30a",
            _ => "face_31o_normal"
        };
    }

    // Live stats in combat (powers included), otherwise the manual's own battle level and senses.
    internal static HbrBattleStats Stats(ElectronicSoldierManual manual)
    {
        if (manual?.IsHbrNumericsEnabled != true) return default;
        return HbrBattleStatsHelper.TryGetStats(manual.Owner?.Creature, out HbrBattleStats stats)
            ? stats
            : manual.GetHbrBattleStats();
    }

    internal static decimal StatValue(HbrBattleStats stats, int statIndex) => statIndex switch
    {
        0 => stats.Strength,
        1 => stats.Dexterity,
        2 => stats.Intelligence,
        3 => stats.Vitality,
        4 => stats.Spirit,
        _ => stats.Luck
    };

    internal static decimal BaseStat(ElectronicSoldierManual manual) =>
        HbrBattleStatsHelper.InitialStat +
        Math.Max(0m, manual.HbrBattleLevel - 1m) * HbrBattleStatsHelper.StatGainPerLevel;

    internal static decimal SenseBonus(ElectronicSoldierManual manual, SixSenseAttribute sense) =>
        manual.GetEffectiveSenseAmount(sense) * HbrBattleStatsHelper.SenseStatGainPerLevel;

    // What confirming the draft would add to the sense's stat; the stat itself waits for confirmation.
    internal static decimal PendingGain(ElectronicSoldierManual manual, SixSenseAllocationDraft draft, SixSenseAttribute sense)
    {
        if (draft == null || draft.Added(sense) <= 0) return 0m;
        return (manual.GetEffectiveSenseAmount(sense, draft.Level(sense)) - manual.GetEffectiveSenseAmount(sense)) *
            HbrBattleStatsHelper.SenseStatGainPerLevel;
    }
}
