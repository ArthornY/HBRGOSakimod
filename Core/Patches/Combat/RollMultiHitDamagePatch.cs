using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
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
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

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
