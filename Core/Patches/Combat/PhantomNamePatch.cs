using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), "get_Name")]
internal static class PhantomNamePatch
{
    private static void Postfix(Creature __instance, ref string __result)
    {
        if (__instance?.PetOwner is not { } owner ||
            !__instance.HasPower<PhantomPower>())
        {
            return;
        }

        string summoner = owner.Character.Title.GetFormattedText();
        if (!RunManager.Instance.IsSingleplayerOrFakeMultiplayer)
        {
            string playerName = PlatformUtil.GetPlayerNameRaw(
                RunManager.Instance.NetService.Platform,
                owner.NetId);
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                summoner += $"（{playerName}）";
            }
        }

        __result = $"幻影：由{summoner}召唤出的{__result}";
    }
}
