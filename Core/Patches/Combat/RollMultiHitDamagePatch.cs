using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class RollMultiHitDamagePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
            .Where(method => method.Name == nameof(CreatureCmd.Damage))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length is 6 or 7 &&
                    parameters[0].ParameterType == typeof(
                        MegaCrit.Sts2.Core.GameActions.Multiplayer
                            .PlayerChoiceContext) &&
                    parameters[1].ParameterType ==
                        typeof(IEnumerable<Creature>) &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == typeof(ValueProp) &&
                    parameters[4].ParameterType == typeof(Creature) &&
                    parameters[5].ParameterType == typeof(CardModel);
            })
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName,
                nameof(CreatureCmd.Damage));
    }

    private static void Prefix(
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        cardSource?
            .Capability<RollGlossaryCapability>()?
            .PrepareDamageSegmentRoll(cardSource, props, dealer);
    }
}
