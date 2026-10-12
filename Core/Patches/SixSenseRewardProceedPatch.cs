using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

internal sealed class SixSenseRewardProceedGate
{
    internal bool Opened { get; private set; }
    internal bool AwaitingClose { get; private set; }
    internal void Begin() { Opened = true; AwaitingClose = true; }
    internal bool Complete()
    {
        if (!AwaitingClose) return false;
        AwaitingClose = false;
        return true;
    }
    internal void Cancel() { Opened = false; AwaitingClose = false; }
}

[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
internal static class SixSenseRewardProceedPatch
{
    private static readonly ConditionalWeakTable<NRewardsScreen, SixSenseRewardProceedGate> Gates = new();

    private static bool Prefix(NRewardsScreen __instance, NButton __0, bool ____isTerminal, IRunState ____runState)
    {
        if (!____isTerminal || ____runState == null || RunManager.Instance?.NetService?.Type == NetGameType.Replay)
            return true;
        var gate = Gates.GetValue(__instance, static _ => new SixSenseRewardProceedGate());
        if (gate.Opened) return !gate.AwaitingClose;
        ElectronicSoldierManual manual = LocalContext.GetMe(____runState)?.GetRelic<ElectronicSoldierManual>();
        if (manual == null) return true;

        gate.Begin();
        if (NSixSenseStatusScreen.TryShow(manual, () =>
        {
            if (!gate.Complete() || !GodotObject.IsInstanceValid(__instance) ||
                __instance.IsQueuedForDeletion() || !__instance.IsInsideTree()) return;
            // Resume vanilla progression so skip tutorials, boss transitions and multiplayer readiness remain intact.
            AccessTools.Method(typeof(NRewardsScreen), "OnProceedButtonPressed").Invoke(__instance, new object[] { __0 });
        })) return false;
        gate.Cancel();
        return true;
    }
}
