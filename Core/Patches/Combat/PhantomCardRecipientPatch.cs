using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(
    typeof(PersonalHivePower),
    nameof(PersonalHivePower.AfterDamageReceived))]
internal static class PhantomCardRecipientPatch
{
    private static bool Prefix(Creature dealer, ref Task __result)
    {
        if (MonsterCardRecipientHelper.IsPlayer(dealer))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
