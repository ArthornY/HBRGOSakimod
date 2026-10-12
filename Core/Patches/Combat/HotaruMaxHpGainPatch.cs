using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp))]
[HarmonyPriority(Priority.First)]
internal static class HotaruMaxHpGainPatch
{
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        if (creature.Player?.Character is not HbrgoHotaru || amount <= 0m ||
            DeflectorProtection.IsMaxHpGainConversionBypassed)
            return true;

        // Keep native maximum-HP gains. DeflectorProtection then synchronizes
        // its maximum and current DP through the existing max-HP change hook.
        __result = DeflectorProtection.GainMaxHpWithoutConversion(creature, amount);
        return false;
    }
}
