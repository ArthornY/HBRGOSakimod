using HBRGOSakiMod.Core.Nodes.Combat;

int checks = 0;
int scenarios = 0;

void Expect(SakiSelectionPortraitFade fade, double time, double expected, string description)
{
    float actual = fade.OpacityAt(time);
    checks++;
    if (!float.IsFinite(actual) || actual < 0 || actual > 1 || Math.Abs(actual - expected) > 0.00001)
        throw new InvalidOperationException($"{description}: t={time:R}, expected {expected:R}, got {actual:R}");
}

void Scenario(string name, Action<SakiSelectionPortraitFade> body)
{
    body(new SakiSelectionPortraitFade());
    scenarios++;
    Console.WriteLine($"PASS {name}");
}

Scenario("Initially hidden; releases without a selection do nothing", fade =>
{
    Expect(fade, 10, 0, "Initial state");
    fade.Release(11, played: true);
    Expect(fade, 11.2, 0, "Unmatched played release");
    fade.Release(12, played: false);
    Expect(fade, 100, 0, "Unmatched cancellation");
});

Scenario("Full fade-in takes half a second", fade =>
{
    fade.Select(10);
    Expect(fade, 10, 0, "Selection is continuous");
    Expect(fade, 10.125, 0.25, "Quarter opacity");
    Expect(fade, 10.25, 0.5, "Half opacity");
    Expect(fade, 10.5, 1, "Fade-in completes");
    Expect(fade, 30, 1, "Selected illustration stays visible");
});

Scenario("Cancelled selection starts fading immediately", fade =>
{
    fade.Select(10);
    fade.Release(11, played: false);
    Expect(fade, 11, 1, "Cancellation is continuous");
    Expect(fade, 11.125, 0.75, "No hold after cancellation");
    Expect(fade, 11.25, 0.5, "Halfway out");
    Expect(fade, 11.5, 0, "Full fade-out completes after half a second");
    Expect(fade, 20, 0, "No negative opacity");
});

Scenario("Cancelling partial fade-in preserves opacity and rate", fade =>
{
    fade.Select(10);
    fade.Release(10.2, played: false);
    Expect(fade, 10.2, 0.4, "No jump on cancellation");
    Expect(fade, 10.25, 0.3, "Partial fade-out keeps the same rate");
    Expect(fade, 10.4, 0, "Partial opacity needs only its remaining duration");
});

Scenario("Played card holds for one second then fades out", fade =>
{
    fade.Select(10);
    fade.Release(11, played: true);
    Expect(fade, 11, 1, "Played release is continuous");
    Expect(fade, 11.999, 1, "Still opaque just before hold ends");
    Expect(fade, 12, 1, "Hold boundary");
    Expect(fade, 12.125, 0.75, "Fade-out starts after hold");
    Expect(fade, 12.25, 0.5, "Halfway out");
    Expect(fade, 12.5, 0, "Hold plus half-second fade");
});

Scenario("Fade-in continues during post-play hold", fade =>
{
    fade.Select(10);
    fade.Release(10.1, played: true);
    Expect(fade, 10.1, 0.2, "Early play keeps current opacity");
    Expect(fade, 10.25, 0.5, "Fade-in continues at original rate");
    Expect(fade, 10.5, 1, "Original fade-in completion time");
    Expect(fade, 11.1, 1, "Full second counted from play");
    Expect(fade, 11.35, 0.5, "Fade-out follows the hold");
    Expect(fade, 11.6, 0, "Fade-out complete");
});

Scenario("Next selection within hold cancels the pending fade-out", fade =>
{
    fade.Select(10);
    fade.Release(11, played: true);
    fade.Select(11.999);
    Expect(fade, 11.999, 1, "Reselection during hold stays opaque");
    Expect(fade, 12.25, 1, "Former fade-out time is cancelled");
    Expect(fade, 20, 1, "Remains visible while next card is selected");
    fade.Release(20, played: true);
    Expect(fade, 21, 1, "Next play gets its own hold");
    Expect(fade, 21.25, 0.5, "Next play fades on its own timeline");
});

Scenario("Reselection during hold also continues an unfinished fade-in", fade =>
{
    fade.Select(10);
    fade.Release(10.1, played: true);
    fade.Select(10.2);
    Expect(fade, 10.2, 0.4, "Partial opacity is retained");
    Expect(fade, 10.35, 0.7, "Progress continues without restarting");
    Expect(fade, 10.5, 1, "Still completes at original time");
    Expect(fade, 11.6, 1, "Old hold and fade-out were cancelled");
});

Scenario("Reselecting during fade-out reverses from current opacity", fade =>
{
    fade.Select(10);
    fade.Release(11, played: true);
    fade.Select(12.2);
    Expect(fade, 12.2, 0.6, "Interrupted fade-out does not jump");
    Expect(fade, 12.3, 0.8, "Reversal uses the same opacity rate");
    Expect(fade, 12.4, 1, "Only (1-alpha)*0.5 seconds needed to return");
    Expect(fade, 60, 1, "Old fade-out cannot resume later");
});

Scenario("Repeated reversals preserve continuity", fade =>
{
    fade.Select(10);
    fade.Release(10.2, played: false);
    fade.Select(10.3);
    Expect(fade, 10.3, 0.2, "First reversal");
    fade.Release(10.4, played: false);
    Expect(fade, 10.4, 0.4, "Second cancellation");
    fade.Select(10.45);
    Expect(fade, 10.45, 0.3, "Second reversal");
    Expect(fade, 10.6, 0.6, "Continues fading in");
    fade.Release(10.65, played: false);
    Expect(fade, 10.65, 0.7, "Third cancellation");
    fade.Select(10.85);
    Expect(fade, 10.85, 0.3, "Third reversal");
    Expect(fade, 11.2, 1, "Completes remaining fade-in");
});

Scenario("Duplicate Select and Release calls cannot restart timelines", fade =>
{
    fade.Select(10);
    fade.Select(10.1);
    Expect(fade, 10.25, 0.5, "Repeated Select does not delay fade-in");
    Expect(fade, 10.5, 1, "Original completion time retained");
    fade.Select(10.75);
    fade.Release(11, played: true);
    fade.Release(11.5, played: false);
    Expect(fade, 11.75, 1, "Unmatched cancellation cannot shorten hold");
    fade.Release(11.9, played: true);
    Expect(fade, 12.25, 0.5, "Unmatched play cannot restart hold");
    Expect(fade, 12.5, 0, "Original fade-out completion retained");
});

foreach (string phase in new[] { "fade-in", "hold", "fade-out" })
{
    Scenario($"Reset during {phase} hides immediately and never revives", fade =>
    {
        fade.Select(10);
        double resetAt = 10.2;
        if (phase != "fade-in")
        {
            fade.Release(11, played: true);
            resetAt = phase == "hold" ? 11.2 : 12.2;
        }
        fade.Reset();
        Expect(fade, resetAt, 0, "Reset is an immediate hide");
        fade.Release(resetAt + 0.1, played: true);
        Expect(fade, resetAt + 0.2, 0, "Old release cannot revive reset state");
        Expect(fade, 100, 0, "No pending animation survives Reset");
        fade.Select(101);
        Expect(fade, 101, 0, "A new selection starts cleanly");
        Expect(fade, 101.25, 0.5, "New fade-in still works after Reset");
    });
}

Scenario("Long frames crossing hold boundary use actual elapsed time", fade =>
{
    fade.Select(10);
    fade.Release(11, played: true);
    Expect(fade, 11.1, 1, "Last frame before a long stall");
    Expect(fade, 12.3, 0.4, "Crossing hold boundary spends only excess time fading");
    Expect(fade, 20, 0, "Long stall past both phases clamps to hidden");
    fade.Select(21);
    fade.Release(21.1, played: true);
    Expect(fade, 22.35, 0.5, "Unfinished fade-in and hold resolve correctly without intermediate frames");
});

Scenario("Selection exactly at hold end cancels fade; selection after expiry starts fresh", fade =>
{
    fade.Select(10);
    fade.Release(11, played: true);
    fade.Select(12);
    Expect(fade, 12, 1, "Exact hold-end selection is continuous");
    Expect(fade, 13, 1, "No fade-out after boundary selection");
    fade.Release(14, played: true);
    fade.Select(20);
    Expect(fade, 20, 0, "Fully expired illustration starts from transparent");
    Expect(fade, 20.25, 0.5, "Fresh fade-in after expiry");
    Expect(fade, 20.5, 1, "Fresh fade-in finishes after half a second");
});

Console.WriteLine($"PASS: {checks} opacity checks across {scenarios} scenarios; no Godot or game runtime required.");
