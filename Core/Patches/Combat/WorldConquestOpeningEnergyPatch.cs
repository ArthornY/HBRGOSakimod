using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Permanent deck cards need not be included among live combat hook listeners.
// Keep the native reset and callbacks first; the shared receipt prevents the
// permanent card, combat copy and this fallback from awarding the same use.
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergyReset))]
internal static class WorldConquestOpeningEnergyPatch
{
    private static void Postfix(Player player, ref Task __result)
    {
        if (WorldConquestCombatHelper.NeedsOpeningEnergy(player)) __result = Finish(__result, player);
    }
    private static async Task Finish(Task native, Player player)
    { await native; await WorldConquestCombatHelper.OnEnergyReset(player); }
}
