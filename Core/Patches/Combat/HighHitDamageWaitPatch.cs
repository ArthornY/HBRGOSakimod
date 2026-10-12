using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class HighHitDamageWait
{
    internal static bool ContinueNormally(CancellationToken cancellationToken, ref Task result)
    {
        if (!HighHitDamageVisualHelper.ShouldSkipPresentationWait()) return true;
        // Removing animation time must not turn a cancelled command into a
        // successful one. All damage, choice and combat hooks still execute.
        result = cancellationToken.IsCancellationRequested
            ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Cmd), nameof(Cmd.Wait), new[] { typeof(float), typeof(bool) })]
internal static class HighHitDamageSimpleWaitPatch
{
    private static bool Prefix(ref Task __result) =>
        HighHitDamageWait.ContinueNormally(default, ref __result);
}

[HarmonyPatch(typeof(Cmd), nameof(Cmd.Wait), new[] { typeof(float), typeof(CancellationToken), typeof(bool) })]
internal static class HighHitDamageTokenWaitPatch
{
    private static bool Prefix(CancellationToken cancelToken, ref Task __result) =>
        HighHitDamageWait.ContinueNormally(cancelToken, ref __result);
}

[HarmonyPatch(typeof(Cmd), nameof(Cmd.CustomScaledWait),
    new[] { typeof(float), typeof(float), typeof(bool), typeof(CancellationToken) })]
internal static class HighHitDamageScaledWaitPatch
{
    private static bool Prefix(CancellationToken cancellationToken, ref Task __result) =>
        HighHitDamageWait.ContinueNormally(cancellationToken, ref __result);
}
