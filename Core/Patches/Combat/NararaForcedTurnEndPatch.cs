using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class NararaForcedTurnEndPatch
{
    // Capture before the mode-switch prefix can replace the original wrapper.
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out ICombatState __state) =>
        __state = NararaForcedTurnEndHelper.BeginCardPlay(__instance);

    // Wrap the tasks returned by the existing mode/hand-play cleanup patches.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ICombatState __state, ref Task __result)
    {
        if (__state != null) __result = FinishPlay(__result, __state);
    }

    private static async Task FinishPlay(Task original, ICombatState combat)
    {
        bool successful = false;
        try
        {
            await original;
            successful = true;
        }
        finally { NararaForcedTurnEndHelper.EndCardPlay(combat, successful); }
    }
}
