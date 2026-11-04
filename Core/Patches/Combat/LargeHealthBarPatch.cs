using System;
using System.Globalization;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.UpdateWidthRelativeToReferenceValue))]
internal static class HbrPlayerHealthBarWidthPatch
{
    private static void Prefix(Creature ____creature, ref float refMaxHp)
    {
        if (____creature?.Player is { } player && HbrBattleStatsHelper.IsEnabled(player))
        {
            // Native multiplayer rows grow linearly with maximum HP. Keep the
            // pre-conversion slope while displaying the real enlarged values.
            refMaxHp *= HbrBattleStatsHelper.PlayerHealthScale;
        }
    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshText")]
internal static class LargeHealthBarTextPatch
{
    private static void Postfix(Creature ____creature, MegaLabel ____hpLabel)
    {
        if (____creature?.Monster == null || !____creature.IsAlive ||
            !____creature.HpDisplay.ShowsNumbers() || ____hpLabel == null) return;
        decimal current = LargeCreatureHealthHelper.GetCurrentHp(____creature);
        decimal maximum = LargeCreatureHealthHelper.GetMaxHp(____creature);
        ____hpLabel.SetTextAutoSize(current.ToString("0", CultureInfo.InvariantCulture) +
            "/" + maximum.ToString("0", CultureInfo.InvariantCulture));
    }
}

[HarmonyPatch(typeof(NHealthBar), "IsPoisonLethal")]
internal static class LargeHealthBarPoisonPatch
{
    private static bool Prefix(Creature ____creature, int poisonDamage, ref bool __result)
    {
        if (____creature?.Monster == null ||
            LargeCreatureHealthHelper.GetMaxHp(____creature) <= BigCombatNumbers.NativeStatCap)
            return true;
        __result = !____creature.HpDisplay.IsInfinite() &&
            poisonDamage >= LargeCreatureHealthHelper.GetCurrentHp(____creature);
        return false;
    }
}

[HarmonyPatch(typeof(NHealthBar), "GetFgWidth", new[] { typeof(int), typeof(float) })]
internal static class LargeHealthBarFillPatch
{
    private static bool Prefix(Creature ____creature, int amount, float maxFgWidth, ref float __result)
    {
        if (____creature?.Monster == null) return true;
        decimal maximum = LargeCreatureHealthHelper.GetMaxHp(____creature);
        if (maximum <= BigCombatNumbers.NativeStatCap) return true;
        // Native callers pass the current proxy HP, optionally minus an
        // absolute poison amount. Only the final display ratio becomes float.
        decimal current = LargeCreatureHealthHelper.GetCurrentHp(____creature);
        decimal remaining = Math.Clamp(current - ((decimal)____creature.CurrentHp - amount), 0m, maximum);
        __result = Math.Max((float)(remaining / maximum) * maxFgWidth,
            current > 0m ? 12f : 0f);
        return false;
    }
}
