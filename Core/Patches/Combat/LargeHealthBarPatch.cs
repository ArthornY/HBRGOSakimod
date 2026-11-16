using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
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
        if (NararaLayeredHpHelper.IsEnabled(____creature))
        {
            ____hpLabel.SetTextAutoSize(
                NararaLayeredHpHelper.GetActiveCurrentHp(____creature).ToString("0", CultureInfo.InvariantCulture) +
                "/" + NararaLayeredHpHelper.GetActiveMaximumHp(____creature).ToString("0", CultureInfo.InvariantCulture) +
                " ×" + NararaLayeredHpHelper.GetRemainingLayers(____creature).ToString(CultureInfo.InvariantCulture));
            return;
        }
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
        if (NararaLayeredHpHelper.IsEnabled(____creature))
        {
            // Ordinary HP loss can clear only the active bar. A lethal tint
            // must mean actual death, rather than exposing the next reserve.
            __result = poisonDamage > 0 && ____creature.HasPower<PoisonPower>() &&
                NararaLayeredHpHelper.GetRemainingLayers(____creature) <= 1 &&
                !____creature.HpDisplay.IsInfinite() &&
                poisonDamage >= LargeCreatureHealthHelper.GetCurrentHp(____creature);
            return false;
        }
        if (____creature?.Monster == null ||
            LargeCreatureHealthHelper.GetMaxHp(____creature) <= BigCombatNumbers.NativeStatCap)
            return true;
        __result = !____creature.HpDisplay.IsInfinite() &&
            poisonDamage >= LargeCreatureHealthHelper.GetCurrentHp(____creature);
        return false;
    }
}

// Doom is an absolute total-HP threshold, whereas poison previews pass the
// current native proxy minus a damage delta. Keep those two inputs distinct.
internal static class NararaHealthBarDoomPreview
{
    private static readonly AccessTools.FieldRef<NHealthBar, Creature> Creature =
        AccessTools.FieldRefAccess<NHealthBar, Creature>("_creature");
    private static readonly Func<NHealthBar, float> MaximumWidth =
        AccessTools.MethodDelegate<Func<NHealthBar, float>>(AccessTools.PropertyGetter(typeof(NHealthBar), "MaxFgWidth"));
    private static readonly Func<NHealthBar, int, float> NativeWidth =
        AccessTools.MethodDelegate<Func<NHealthBar, int, float>>(
            AccessTools.Method(typeof(NHealthBar), "GetFgWidth", new[] { typeof(int) }));

    internal static decimal ReserveHp(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) =>
        LargeCreatureHealthHelper.GetCurrentHp(creature) - NararaLayeredHpHelper.GetActiveCurrentHp(creature);

    internal static float GetWidth(NHealthBar bar, int amount)
    {
        var creature = Creature(bar);
        if (!NararaLayeredHpHelper.IsEnabled(creature)) return NativeWidth(bar, amount);
        decimal maximum = NararaLayeredHpHelper.GetActiveMaximumHp(creature);
        decimal projected = Math.Clamp((decimal)amount - ReserveHp(creature), 0m, maximum);
        return maximum <= 0m || projected <= 0m ? 0f : Math.Max(
            (float)(projected / maximum) * MaximumWidth(bar), 12f);
    }
}

[HarmonyPatch(typeof(NHealthBar), "IsDoomLethal")]
internal static class NararaHealthBarDoomLethalPatch
{
    private static bool Prefix(Creature ____creature, int doomAmount, int poisonDamage, ref bool __result)
    {
        if (!NararaLayeredHpHelper.IsEnabled(____creature)) return true;
        decimal poisonLoss = ____creature.HasPower<PoisonPower>()
            ? Math.Min(Math.Max(0m, poisonDamage), NararaLayeredHpHelper.GetActiveCurrentHp(____creature)) : 0m;
        // Explicit Doom death remains native even with reserves, but poison
        // cannot manufacture a future total-HP threshold across several bars.
        __result = doomAmount > 0 && ____creature.HasPower<DoomPower>() &&
            doomAmount >= LargeCreatureHealthHelper.GetCurrentHp(____creature) - poisonLoss;
        return false;
    }
}

[HarmonyPatch]
internal static class NararaHealthBarDoomWidthPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo width = AccessTools.Method(typeof(NHealthBar), "GetFgWidth", new[] { typeof(int) });
        MethodInfo lethal = AccessTools.Method(typeof(NHealthBar), "IsDoomLethal");
        foreach (string name in new[] { "RefreshDoom", "RefreshForeground" })
        {
            MethodInfo method = AccessTools.Method(typeof(NHealthBar), name);
            if (method == null) continue;
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(method);
            if (instructions.Any(instruction => instruction.Calls(width)) &&
                instructions.Any(instruction => instruction.Calls(lethal))) return method;
        }
        throw new InvalidOperationException("Native Doom preview has no supported width/threshold call pair.");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> result = instructions.ToList();
        MethodInfo width = AccessTools.Method(typeof(NHealthBar), "GetFgWidth", new[] { typeof(int) });
        MethodInfo lethal = AccessTools.Method(typeof(NHealthBar), "IsDoomLethal");
        int lastWidth = result.FindLastIndex(instruction => instruction.Calls(width));
        if (lastWidth < 0 || !result.Skip(lastWidth + 1).Any(instruction => instruction.Calls(lethal)))
            throw new InvalidOperationException("Native Doom threshold width is not followed by its lethal predicate.");
        result[lastWidth].opcode = OpCodes.Call;
        result[lastWidth].operand = AccessTools.Method(typeof(NararaHealthBarDoomPreview), nameof(NararaHealthBarDoomPreview.GetWidth));
        return result;
    }

    private static void Postfix(Creature ____creature, Control ____doomForeground)
    {
        if (____doomForeground != null && NararaLayeredHpHelper.IsEnabled(____creature) &&
            ____creature.GetPowerAmount<DoomPower>() <= NararaHealthBarDoomPreview.ReserveHp(____creature))
            ____doomForeground.Visible = false;
    }
}

[HarmonyPatch(typeof(NHealthBar), "GetFgWidth", new[] { typeof(int), typeof(float) })]
internal static class LargeHealthBarFillPatch
{
    private static bool Prefix(Creature ____creature, int amount, float maxFgWidth, ref float __result)
    {
        if (____creature?.Monster == null) return true;
        if (NararaLayeredHpHelper.IsEnabled(____creature))
        {
            decimal layerMaximum = NararaLayeredHpHelper.GetActiveMaximumHp(____creature);
            decimal layerCurrent = NararaLayeredHpHelper.GetActiveCurrentHp(____creature);
            // Current HP and poison-preview callers use the native proxy with
            // an absolute delta. Only the active pool's ratio reaches float.
            decimal layerRemaining = Math.Clamp(layerCurrent - ((decimal)____creature.CurrentHp - amount),
                0m, layerMaximum);
            __result = layerMaximum <= 0m ? 0f : Math.Max(
                (float)(layerRemaining / layerMaximum) * maxFgWidth, layerCurrent > 0m ? 12f : 0f);
            return false;
        }
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
