using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(
    typeof(PersonalHivePower),
    nameof(PersonalHivePower.AfterDamageReceived))]
internal static class PhantomCardRecipientPatch
{
    private static bool Prefix(Creature target, Creature dealer, CardModel cardSource, ValueProp props, ref Task __result)
    {
        if (MonsterCardRecipientHelper.IsPlayer(dealer) &&
            !HotaruAutomaticCannonContext.SuppressesPersonalHive(target, dealer, cardSource, props))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
