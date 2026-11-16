using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
internal static class RisaRecoveryTargetPatch
{
    private static void Postfix(CardModel __instance, Creature __0, ref bool __result)
    {
        if (__result && __instance is AlwaysKeepYourChinUpHbrgoSaki { IsMutable: true, TargetType: TargetType.AnyAlly } card)
            __result = __0 != null && RisaRecoveryCombatHelper.Targets(card.Owner).Contains(__0);
    }
}
