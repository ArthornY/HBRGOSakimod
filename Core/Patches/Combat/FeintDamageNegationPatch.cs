using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class FeintDamageNegationPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Creature target,
        Creature dealer,
        ValueProp props,
        ModifyDamageHookType modifyDamageHookType,
        CardPreviewMode previewMode,
        ref decimal __result,
        ref IEnumerable<AbstractModel> modifiers)
    {
        if (__result <= 0m ||
            target == null ||
            dealer == null ||
            previewMode != CardPreviewMode.None ||
            !modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative) ||
            target.Side == dealer.Side)
        {
            return;
        }

        FeintMarkPower feint = target.GetPower<FeintMarkPower>();
        if (feint == null)
        {
            foreach (Creature playerCreature in target.CombatState.PlayerCreatures)
            {
                FeintMarkPower candidate = playerCreature.GetPower<FeintMarkPower>();
                if (candidate != null &&
                    playerCreature.HasPower<FeintDamageRedirectPower>())
                {
                    feint = candidate;
                    break;
                }
            }
        }

        if (feint == null ||
            !feint.TryNegateIncomingDamage(target, __result, props, dealer))
        {
            return;
        }

        __result = 0m;
        modifiers = (modifiers ?? Enumerable.Empty<AbstractModel>())
            .Append(feint)
            .Distinct();
    }
}
