using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class NoSelfInDreamDamagePatch
{
    private static void Postfix(
        Creature target,
        Creature dealer,
        ModifyDamageHookType modifyDamageHookType,
        ref decimal __result,
        ref IEnumerable<AbstractModel> modifiers)
    {
        if (__result <= 0m ||
            dealer == null ||
            !modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative) ||
            target?.Side == dealer.Side)
        {
            return;
        }

        NoSelfInDreamPower power = dealer.GetPower<NoSelfInDreamPower>();
        if (power == null)
        {
            return;
        }

        __result *= NoSelfInDreamPower.FinalDamageMultiplier;
        modifiers = (modifiers ?? Enumerable.Empty<AbstractModel>())
            .Append(power)
            .Distinct();
    }
}
