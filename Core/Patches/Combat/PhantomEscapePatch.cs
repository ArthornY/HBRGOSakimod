using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(ThievingHopper), "EscapeMove")]
internal static class PhantomEscapeMovePatch
{
    private static bool Prefix(ThievingHopper __instance, ref Task __result)
    {
        if (!PhantomEscapeHelper.DiesInsteadOfEscaping(__instance.Creature))
            return true;

        // Preserve the native move sequence and timer, but resolve expiry as a
        // normal death so phantom level loss, revival and death effects run.
        // Patching this move also leaves deliberate phantom replacement intact.
        __result = CreatureCmd.Kill(__instance.Creature);
        return false;
    }
}

[HarmonyPatch]
internal static class PhantomEscapeArtistDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Native status hover tips use SmartDescription, while glossary and
        // dumb tips use Description. Cover the remote path as well.
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Description));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.SmartDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.RemoteDescription));
    }

    private static void Postfix(PowerModel __instance, ref LocString __result)
    {
        if (__instance is EscapeArtistPower && __instance.IsMutable &&
            PhantomEscapeHelper.DiesInsteadOfEscaping(__instance.Owner))
            __result = new LocString("powers", "HBRGO_PHANTOM_ESCAPE_ARTIST.description");
    }
}

[HarmonyPatch(typeof(AbstractIntent), nameof(AbstractIntent.GetHoverTip))]
internal static class PhantomEscapeIntentHoverTipPatch
{
    private static void Postfix(AbstractIntent __instance, Creature owner, ref HoverTip __result)
    {
        if (__instance is EscapeIntent && PhantomEscapeHelper.DiesInsteadOfEscaping(owner))
            __result = new HoverTip(
                new LocString("powers", "HBRGO_PHANTOM_ESCAPE_INTENT.title"),
                new LocString("powers", "HBRGO_PHANTOM_ESCAPE_INTENT.description"),
                __result.Icon);
    }
}
