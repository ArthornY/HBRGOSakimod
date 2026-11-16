using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;
[HarmonyPatch(typeof(PhantomHelper), nameof(PhantomHelper.GetDamageDefender))]
internal static class KozueSpiritDamageDefenderPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __0, ref Creature __result)
    { if (KozueSpiritHelper.GetLivingSpirit(__0) is { } spirit) __result = spirit; }
}
[HarmonyPatch(typeof(PhantomHelper), nameof(PhantomHelper.GetDamageBonusBreakdown))]
internal static class KozueSpiritNoRosterDamagePatch
{
    private static void Postfix(Creature phantom, ref (decimal PermanentPercent, decimal TemporaryPercent) __result)
    { if (phantom?.HasPower<KozueSpiritPower>() == true) __result = (0m, 0m); }
}
[HarmonyPatch(typeof(PhantomHelper), nameof(PhantomHelper.IsPendingSceneBossRevive))]
internal static class KozueSpiritNoPendingRevivePatch
{
    private static void Postfix(Creature phantom, ref bool __result)
    { if (phantom?.HasPower<KozueSpiritPower>() == true) __result = false; }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldCreatureBeRemovedFromCombatAfterDeath))]
internal static class KozueSpiritDeathRemovalPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Creature creature, ref bool __result)
    { if (creature.HasPower<KozueSpiritPower>()) __result = true; }
}
[HarmonyPatch]
internal static class KozueSpiritNoRevivalPowerPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(PowerModel).Assembly, typeof(KozueSpiritPower).Assembly }
        .SelectMany(assembly => assembly.GetTypes()).Where(type => typeof(PowerModel).IsAssignableFrom(type) && !type.IsAbstract &&
            (type == typeof(AdaptablePower) || type.Name.Contains("RevivalPower")))
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(method => method.Name == "AfterDeath" && method.ReturnType == typeof(Task) && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(Creature))).Distinct();
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(PowerModel __instance, object[] __args, ref Task __result)
    { Creature creature = __args.OfType<Creature>().FirstOrDefault();
      if (creature != __instance.Owner || creature?.HasPower<KozueSpiritPower>() != true) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
internal static class KozueSpiritNoHealRevivalPatch
{
    private static bool Prefix(Creature creature, ref Task __result)
    { if (!creature.IsDead || !creature.HasPower<KozueSpiritPower>()) return true; __result = Task.CompletedTask; return false; }
}
