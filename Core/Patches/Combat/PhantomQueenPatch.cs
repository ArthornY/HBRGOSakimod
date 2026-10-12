using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Queen), "HasAmalgamDied", MethodType.Getter)]
internal static class PhantomQueenAmalgamStatePatch
{
    private static bool Prefix(Queen __instance, ref bool __result)
    {
        if (!PhantomQueenHelper.IsPhantom(__instance.Creature))
            return true;

        // A standalone phantom has no native encounter partner. Only its own
        // living summoned amalgam can keep it in the puppet support phase.
        __result = !PhantomQueenHelper.HasLivingAmalgam(__instance.Creature);
        return false;
    }
}

[HarmonyPatch(typeof(Queen), nameof(Queen.AfterDeath))]
internal static class PhantomQueenAfterDeathPatch
{
    private static bool Prefix(Queen __instance, Creature creature,
        bool wasRemovalPrevented, ref Task __result)
    {
        if (!PhantomQueenHelper.IsPhantom(__instance.Creature))
            return true;

        // Native Queen reacts to every amalgam death and changes boss music.
        // A phantom must react only to its own puppet; normal phantom death
        // cleanup is handled by PhantomPower rather than the native boss hook.
        PhantomQueenHelper.AfterDeath(__instance.Creature, creature,
            wasRemovalPrevented);
        __result = Task.CompletedTask;
        return false;
    }
}
