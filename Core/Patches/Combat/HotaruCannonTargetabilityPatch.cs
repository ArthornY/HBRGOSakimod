using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowHitting),
    new[] { typeof(ICombatState), typeof(Creature) })]
internal static class HotaruCannonTargetabilityPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Creature creature, ref bool __result)
    {
        // Cannons remain alive combat entities for formation and our custom
        // card targets, but are never receivers of attacks or power effects.
        // Ritsu's owned-cannon predicates do not depend on IsHittable.
        if (creature?.Monster is HotaruCannonModel)
            __result = false;
    }
}
