using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class MagicSwordEnhancementDamagePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
            .Single(method =>
            {
                if (method.Name != nameof(CreatureCmd.Damage))
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 6 &&
                       parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
                       parameters[2].ParameterType == typeof(decimal) &&
                       parameters[3].ParameterType == typeof(ValueProp) &&
                       parameters[4].ParameterType == typeof(Creature) &&
                       parameters[5].ParameterType == typeof(CardModel);
            });
    }

    private static void Prefix(IEnumerable<Creature> __1, ref ValueProp __3, Creature __4)
    {
        if (__4 == null ||
            __1 == null ||
            !__1.Any(target => target != null && target.Side != __4.Side) ||
            !__4.HasPower<MagicSwordModePower>() ||
            !__4.HasPower<MagicSwordEnhancementPower>())
        {
            return;
        }

        __3 |= ValueProp.Unblockable;
    }
}
