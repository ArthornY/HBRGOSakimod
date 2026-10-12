using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
internal static class PhantomHudAnchorPatch
{
    private static void Postfix(List<NCreature> creatureNodes)
    {
        NPhantomCombatLayout.RememberPlayerAnchors(creatureNodes);
    }
}
