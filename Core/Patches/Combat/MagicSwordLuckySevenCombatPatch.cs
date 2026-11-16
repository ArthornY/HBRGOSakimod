using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
internal static class MagicSwordLuckySevenPopulateCombatStatePatch
{
    private static void Postfix(Player __instance)
    {
        if (__instance.PlayerCombatState == null)
        {
            return;
        }

        foreach (var card in __instance.PlayerCombatState.DrawPile.Cards
            .Where(MagicSwordCombatTemplateHelper.IsTemplate)
            .ToList())
        {
            card.RemoveFromState();
        }
    }
}
