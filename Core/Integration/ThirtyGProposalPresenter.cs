using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Nodes.Cards;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Integration;

// Answers Codex's ThirtyGProposalPresentationBridge with the picture side of requirement 72. The proposal
// is already resolved and saved when this runs; it only shows it. The first proposal of a run plays the
// full 5 s version, later ones the 1.9 s quick one (a click skips either to the card flying back). The
// on/off switch stays Codex's HBRGOSakiConfig.ThirtyGProposalPresentation, checked by the bridge itself.
internal static class ThirtyGProposalPresenter
{
    private static readonly object Seen = new();
    private static readonly ConditionalWeakTable<object, object> FullShown = new();
    private static bool _registered;

    // Called from ThirtyGCardDecorPatch.Prepare, so no shared initializer needs a line for it.
    internal static void Register()
    {
        if (_registered) return;
        _registered = true;
        ThirtyGProposalPresentationBridge.Requested += Present;
    }

    private static async Task Present(ThirtyGProposalPresentation proposal)
    {
        if (TestMode.IsOn || proposal?.TargetCard == null) return;
        if (NCombatRoom.Instance?.Ui is not { } ui || !GodotObject.IsInstanceValid(ui) || !ui.IsInsideTree())
        {
            ThirtyGProposalPresentationBridge.Fallback(proposal);
            return;
        }
        object run = proposal.Player?.RunState;
        bool full = run == null || !FullShown.TryGetValue(run, out _);
        if (run != null) FullShown.AddOrUpdate(run, Seen);
        NThirtyGCardDecor.RememberProposal(proposal);
        var overlay = new NThirtyGProposalOverlay();
        ui.AddChild(overlay);
        Task play;
        try { play = overlay.Play(proposal, full); }
        catch
        {
            // The bridge logs it and shows its one-line fallback; the half-built overlay must not stay.
            overlay.Finish();
            throw;
        }
        // The combat start waits for this task, so it must end even if a frame never comes.
        if (await Task.WhenAny(play, Task.Delay(TimeSpan.FromSeconds(full ? 15 : 8))) != play && GodotObject.IsInstanceValid(overlay))
            overlay.Finish();
    }
}
