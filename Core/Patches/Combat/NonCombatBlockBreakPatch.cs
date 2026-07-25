using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockBroken))]
internal static class NonCombatBlockBreakPatch
{
    private static bool Prefix(ICombatState combatState, ref Task __result)
    {
        if (combatState != null)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
