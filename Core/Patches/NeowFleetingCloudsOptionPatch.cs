using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class NeowFleetingCloudsOptionPatch
{
    private const float OptionChance = 0.25f;

    private static readonly MethodInfo DoneMethod = AccessTools.Method(
        typeof(AncientEventModel),
        "Done");

    private static void Postfix(
        Neow __instance,
        ref IReadOnlyList<EventOption> __result)
    {
        if (__instance.Owner?.Character is not HbrgoSaki31O ||
            __instance.Owner.RunState.Modifiers.Count > 0 ||
            __result.Count != 3 ||
            __instance.Rng.NextFloat() >= OptionChance)
        {
            return;
        }

        FleetingClouds relic =
            (FleetingClouds)ModelDb.Relic<FleetingClouds>().ToMutable();
        relic.Owner = __instance.Owner;
        string optionKey =
            $"NEOW.pages.INITIAL.options.{relic.Id.Entry}";
        EventOption option = EventOption.FromRelic(
            relic,
            __instance,
            () => ObtainRelic(__instance, relic),
            optionKey);
        EventOption[] options = __result.ToArray();
        options[^1] = option;
        __result = options;
    }

    private static async Task ObtainRelic(
        Neow neow,
        FleetingClouds relic)
    {
        await RelicCmd.Obtain(relic, neow.Owner);
        DoneMethod.Invoke(neow, null);
    }
}
