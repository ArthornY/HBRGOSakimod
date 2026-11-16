using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

// 30G 「卒」 / 「心动」 card faces follow every native visual refresh of a card node; pooled nodes are
// cleared on the way in and out. Prepare also hooks the proposal performance to Codex's bridge, so the
// shared initializer stays untouched.
[HarmonyPatch]
internal static class ThirtyGCardDecorPatch
{
    private static MethodBase Target => AccessTools.FirstMethod(typeof(NCard), method => method.Name == nameof(NCard.UpdateVisuals));

    private static bool Prepare()
    {
        ThirtyGProposalPresenter.Register();
        return Target != null;
    }

    private static MethodBase TargetMethod() => Target;
    private static void Postfix(NCard __instance) => NThirtyGCardDecor.Update(__instance);
}

[HarmonyPatch]
internal static class ThirtyGCardDecorPoolPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCard), nameof(NCard.OnFreedToPool));
        yield return AccessTools.Method(typeof(NCard), nameof(NCard.OnReturnedFromPool));
    }

    private static void Prefix(NCard __instance) => NThirtyGCardDecor.Reset(__instance);
}
