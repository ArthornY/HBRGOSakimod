using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HotaruCannonHappinessStrengthPatch
{
    private static MethodBase TargetMethod() => typeof(StrengthPower)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => m.Name == nameof(StrengthPower.ModifyDamageAdditive))
        .OrderByDescending(m => m.GetParameters().Length).First();

    private static void Postfix(Creature __0, ValueProp __2, Creature __3, CardModel __4,
        ref decimal __result)
    {
        if (__result > 0m && HotaruCannonStatsHelper.TryGetCurrentCannon(__0, __3, __4, __2,
                out Creature cannon) && HotaruCannonDamageUpgradeHelper.Has(cannon,
                HotaruCannonKind.Happiness, 3))
            __result *= 2m;
    }
}
