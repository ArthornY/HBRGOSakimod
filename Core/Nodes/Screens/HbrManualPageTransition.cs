namespace HBRGOSakiMod.Core.Nodes.Screens;

// One of the manual's two screen layers as a page switch moves it: home fades and slips back to
// HbrManualPageTransition.HomeShrink, a page fades and slides PageSlide px in from the right. X is the
// page's offset and Scale home's; the other layer leaves each at its rest value.
internal readonly record struct HbrManualLayerPose(bool Visible, float Alpha, float X, float Scale);

// How the phone manual's home screen and its pages trade places (ui-concepts/manual-phone-20261104):
// home fades and slips back to 0.97 while a page fades in from 40px right, and returning reverses it; a
// switch between two pages only flashes the screen. Pure (no Godot types): NSixSenseStatusScreen.ShowPage
// follows it, and validation/hbr-manual-phone replays runs of switches, however fast, against it.
internal static class HbrManualPageTransition
{
    internal const float PageSlide = 40f;
    internal const float HomeShrink = 0.97f;
    internal const float FadeSeconds = 0.2f;   // alpha
    internal const float MoveSeconds = 0.22f;  // the page's slide and home's scale, easing out (quart)

    internal static readonly HbrManualLayerPose HomeShown = new(true, 1f, 0f, 1f);
    internal static readonly HbrManualLayerPose HomeAway = new(false, 0f, 0f, HomeShrink);
    internal static readonly HbrManualLayerPose PageShown = new(true, 1f, 0f, 1f);
    internal static readonly HbrManualLayerPose PageAway = new(false, 0f, PageSlide, 1f);

    internal enum Step
    {
        // Both layers straight to rest: no animation asked for, or a switch between pages with nothing running.
        Snap,
        // A switch between pages while the last transition still runs: that transition already ends where
        // this page rests, so it carries on.
        Keep,
        // Into or out of home: tween from wherever the layers are now.
        Animate
    }

    // At rest the side on show is shown and the other side is hidden in its away pose, ready to come back.
    internal static (HbrManualLayerPose Home, HbrManualLayerPose Page) Rest(bool home) =>
        home ? (HomeShown, PageAway) : (HomeAway, PageShown);

    // Until 2026-11-12 a switch between pages killed the running transition and returned, so Ctrl+F during
    // the 0.22s slide left the page half faded and offset with home still showing until home was reopened
    // (Codex takeover review F5).
    internal static Step Decide(bool animate, bool fromHome, bool toHome, bool transitionRunning) =>
        !animate ? Step.Snap
        : fromHome != toHome ? Step.Animate
        : transitionRunning ? Step.Keep
        : Step.Snap;

    internal static HbrManualLayerPose Shown(bool homeLayer) => homeLayer ? HomeShown : PageShown;

    internal static HbrManualLayerPose Away(bool homeLayer) => homeLayer ? HomeAway : PageAway;

    // Animate, for the layer of the side being opened: it comes in from its away pose if it was hidden, or
    // from where it stands if it was still leaving, so a reversal half way carries on smoothly.
    internal static HbrManualLayerPose EnterFrom(HbrManualLayerPose current, bool homeLayer) =>
        current.Visible ? current : Away(homeLayer) with { Visible = true };

    // Animate, at the end: the layer that left is hidden unless the manual is on its side again. (A switch
    // back would have replaced the transition before it ended; this mirrors the guard the tween always had.)
    internal static bool VisibleAfterLeaving(bool homeLayer, bool nowHome) => homeLayer == nowHome;
}
