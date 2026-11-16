using System.Globalization;
using HBRGOSakiMod.Core.Nodes.Screens;

// Page switches in the phone manual, replayed without Godot (Codex takeover review F5: Ctrl+F during the
// 0.22s slide froze the page half faded and offset, with home still showing). A small model of the two
// layers and of Godot's Tween replays runs of switches at any spacing, the way ShowPage uses
// HbrManualPageTransition, and checks that whenever no transition runs the layers rest exactly where the
// page shown says. The model keeps the Tween rules that matter here: parallel property tracks start from
// the value when the tween is made and end exactly at their target after their own duration, a chained
// callback runs after the longest track, and Kill leaves every property where it stands. Whether Godot's
// own Tween behaves so when a switch interrupts it can only be seen in the game.
internal static class PageTransitionChecks
{
    private static readonly float[] Gaps = { 0f, 1f / 60f, 0.05f, 0.1f, 0.19f, 0.2f, 0.21f, 0.219f, 0.22f, 0.25f, 0.5f };

    internal static string Run(Action<bool, string> expect, string repo)
    {
        RuleChecks(expect);
        StuckScenario(expect);
        (int runs, int switches, int oldRuns, int oldOff) = ExhaustiveRuns(expect);
        (int randomRuns, int randomSwitches) = RandomRuns(expect);
        int samples = NormalSpeedLook(expect);
        SourceChecks(expect, repo);
        return string.Create(CultureInfo.InvariantCulture,
            $"page transitions: {runs + randomRuns} runs, {switches + randomSwitches} switches replayed, {samples} frames compared " +
            $"with the old ShowPage; the old ShowPage left {oldOff} of {oldRuns} short runs off rest");
    }

    // The preview's timing and poses, the decision for every kind of switch, and the two pose rules.
    private static void RuleChecks(Action<bool, string> expect)
    {
        expect(HbrManualPageTransition.PageSlide == 40f && HbrManualPageTransition.HomeShrink == 0.97f &&
               HbrManualPageTransition.FadeSeconds == 0.2f && HbrManualPageTransition.MoveSeconds == 0.22f,
            "page switches keep the preview's 40px slide, 0.97 home and 0.2s fade / 0.22s move");
        expect(HbrManualPageTransition.Rest(true) == (HbrManualPageTransition.HomeShown, HbrManualPageTransition.PageAway) &&
               HbrManualPageTransition.Rest(false) == (HbrManualPageTransition.HomeAway, HbrManualPageTransition.PageShown),
            "at rest the side on show is shown and the other hidden in its away pose");
        expect(HbrManualPageTransition.HomeShown == new HbrManualLayerPose(true, 1f, 0f, 1f) &&
               HbrManualPageTransition.PageShown == new HbrManualLayerPose(true, 1f, 0f, 1f) &&
               HbrManualPageTransition.HomeAway == new HbrManualLayerPose(false, 0f, 0f, 0.97f) &&
               HbrManualPageTransition.PageAway == new HbrManualLayerPose(false, 0f, 40f, 1f),
            "shown is opaque and in place; away is hidden, transparent, home at 0.97 and the page 40px right");

        foreach (bool animate in new[] { false, true })
        foreach (bool fromHome in new[] { false, true })
        foreach (bool toHome in new[] { false, true })
        foreach (bool running in new[] { false, true })
        {
            HbrManualPageTransition.Step expected = !animate ? HbrManualPageTransition.Step.Snap
                : fromHome != toHome ? HbrManualPageTransition.Step.Animate
                : running ? HbrManualPageTransition.Step.Keep
                : HbrManualPageTransition.Step.Snap;
            expect(HbrManualPageTransition.Decide(animate, fromHome, toHome, running) == expected,
                $"switch animate={animate} from {(fromHome ? "home" : "a page")} to {(toHome ? "home" : "a page")} " +
                $"{(running ? "during" : "after")} a transition: {expected}");
        }

        foreach (bool homeLayer in new[] { false, true })
        {
            HbrManualLayerPose away = HbrManualPageTransition.Away(homeLayer);
            HbrManualLayerPose half = new(true, 0.5f, homeLayer ? 0f : 20f, homeLayer ? 0.985f : 1f);
            string layer = homeLayer ? "home" : "the page";
            expect(HbrManualPageTransition.EnterFrom(away, homeLayer) == away with { Visible = true } &&
                   HbrManualPageTransition.EnterFrom(away with { Alpha = 1f, X = 0f, Scale = 1f }, homeLayer) == away with { Visible = true },
                $"{layer} comes in from its away pose when it was hidden, whatever it was left at");
            expect(HbrManualPageTransition.EnterFrom(half, homeLayer) == half, $"{layer} comes back from where it stands when still leaving");
            expect(HbrManualPageTransition.VisibleAfterLeaving(homeLayer, nowHome: homeLayer) &&
                   !HbrManualPageTransition.VisibleAfterLeaving(homeLayer, nowHome: !homeLayer),
                $"{layer} is hidden when its exit ends, unless the manual is back on its side");
        }
    }

    // The review's case: home → 六感, Ctrl+F 0.1s later.
    private static void StuckScenario(Action<bool, string> expect)
    {
        SimScreen before = new(old: true), now = new(old: false);
        foreach (SimScreen screen in new[] { before, now })
        {
            screen.Show(toHome: false);
            screen.Advance(0.1f);
            screen.Show(toHome: false);
        }
        float alpha = now.Page.Alpha;
        now.Advance(0.05f);
        expect(now.Running && now.Page.Alpha > alpha + 0.2f && now.Home.Alpha < now.Page.Alpha,
            "after Ctrl+F at 0.1s the slide carries on instead of stopping");
        before.Advance(1f);
        now.Advance(1f);
        expect(before.Home.Visible && before.Home.Alpha is > 0.4f and < 0.6f && before.Page.Alpha is > 0.4f and < 0.6f && before.Page.X > 1f,
            string.Create(CultureInfo.InvariantCulture,
                $"the old ShowPage froze Ctrl+F at 0.1s: page alpha {before.Page.Alpha:0.00} {before.Page.X:0.0}px right, " +
                $"home still drawn at alpha {before.Home.Alpha:0.00} and {before.Home.Scale:0.000}x"));
        expect(now.AtRest() && !now.Home.Visible && now.Page.Alpha == 1f && now.Page.X == 0f,
            "Ctrl+F at 0.1s now lands on the glossary at rest: home hidden, the page opaque and in place");

        // Esc half way back to home, then into a page again: each reversal carries on from where the layers
        // stand (before, a page that came back jumped to transparent and 40px right).
        SimScreen reverse = new(old: false);
        reverse.Show(toHome: false);
        reverse.Advance(0.1f);
        float halfIn = reverse.Page.Alpha;
        reverse.Show(toHome: true);
        expect(Math.Abs(reverse.Page.Alpha - halfIn) < 1e-6f && reverse.Page.Visible, "Esc half way: the page fades out from where it was");
        reverse.Advance(0.05f);
        float halfOut = reverse.Page.Alpha;
        reverse.Show(toHome: false);
        expect(Math.Abs(reverse.Page.Alpha - halfOut) < 1e-6f && halfOut > 0f, "opening a page again half way picks the page up where it was");
        reverse.Advance(1f);
        expect(reverse.AtRest(), "the reversals end at rest");
    }

    // Every run of up to four switches between home and pages at the listed spacings, from a closed manual
    // opened on home. A switch from a page to a page stands for Ctrl+F, the 战斗属性 link, a page
    // reached from another page and back from it.
    private static (int Runs, int Switches, int OldRuns, int OldOff) ExhaustiveRuns(Action<bool, string> expect)
    {
        int runs = 0, switches = 0, oldRuns = 0, oldOff = 0;
        for (int length = 1; length <= 4; length++)
        {
            int count = 0, failures = 0, lengthOldOff = 0;
            string? first = null;
            int gapCombos = (int)Math.Pow(Gaps.Length, length);
            for (int targets = 0; targets < 1 << length; targets++)
            for (int combo = 0; combo < gapCombos; combo++)
            {
                bool[] toHome = new bool[length];
                float[] gap = new float[length];
                for (int i = 0, rest = combo; i < length; i++, rest /= Gaps.Length)
                {
                    toHome[i] = (targets >> i & 1) == 1;
                    gap[i] = Gaps[rest % Gaps.Length];
                }
                count++;
                switches += length;
                if (!Replay(new SimScreen(old: false), toHome, gap, animate: null, strict: true))
                {
                    failures++;
                    first ??= Describe(toHome, gap);
                }
                oldRuns++;
                if (!Replay(new SimScreen(old: true), toHome, gap, animate: null, strict: false)) lengthOldOff++;
            }
            runs += count;
            oldOff += lengthOldOff;
            expect(failures == 0, $"all {count} runs of {length} switches rest whenever nothing runs and end at rest" +
                (first == null ? string.Empty : $"; first failure: {first}"));
            if (length >= 2)
                expect(lengthOldOff > 0, $"the check catches the old ShowPage: {lengthOldOff} runs of {length} switches end off rest");
        }
        return (runs, switches, oldRuns, oldOff);
    }

    // Long seeded runs at any spacing, now and then without animation (the six-sense reward opens the
    // manual straight on its page).
    private static (int Runs, int Switches) RandomRuns(Action<bool, string> expect)
    {
        var random = new Random(20261112);
        int runs = 3000, switches = 0, failures = 0;
        string? first = null;
        for (int run = 0; run < runs; run++)
        {
            int length = random.Next(5, 31);
            bool[] toHome = new bool[length];
            float[] gap = new float[length];
            bool[] animate = new bool[length];
            for (int i = 0; i < length; i++)
            {
                toHome[i] = random.Next(3) == 0;
                gap[i] = random.Next(4) == 0 ? Gaps[random.Next(Gaps.Length)] : (float)random.NextDouble() * 0.3f;
                animate[i] = random.Next(20) != 0;
            }
            switches += length;
            if (Replay(new SimScreen(old: false), toHome, gap, animate, strict: true)) continue;
            failures++;
            first ??= Describe(toHome, gap);
        }
        expect(failures == 0, $"{runs} long random runs rest whenever nothing runs and end at rest" +
            (first == null ? string.Empty : $"; first failure: {first}"));
        return (runs, switches);
    }

    // At normal speed (each switch after the last transition ended) the screen looks as it did before the
    // fix, frame for frame. The one difference: after the six-sense reward opens the manual straight on its
    // page, going back now fades home in like every other return, where it used to pop in at full opacity.
    private static int NormalSpeedLook(Action<bool, string> expect)
    {
        float[] offsets = { 0f, 0.01f, 0.03f, 0.06f, 0.1f, 0.15f, 0.199f, 0.2f, 0.21f, 0.219f, 0.22f, 0.23f, 0.3f };
        var random = new Random(1104);
        int samples = 0, mismatches = 0;
        string? first = null;
        for (int run = 0; run < 400; run++)
        {
            SimScreen before = new(old: true), now = new(old: false);
            int length = random.Next(2, 13);
            for (int i = 0; i < length; i++)
            {
                bool toHome = random.Next(2) == 0;
                before.Show(toHome);
                now.Show(toHome);
                float at = 0f;
                foreach (float offset in offsets)
                {
                    before.Advance(offset - at);
                    now.Advance(offset - at);
                    at = offset;
                    samples++;
                    if (SimScreen.SameLook(before, now)) continue;
                    mismatches++;
                    first ??= string.Create(CultureInfo.InvariantCulture, $"run {run}, switch {i}, +{offset:0.000}s");
                }
            }
        }
        expect(mismatches == 0, $"normal-speed switches look the same as before, {samples} frames compared" +
            (first == null ? string.Empty : $"; first difference at {first}"));

        SimScreen rewardBefore = new(old: true), rewardNow = new(old: false);
        foreach (SimScreen screen in new[] { rewardBefore, rewardNow })
        {
            screen.Show(toHome: false, animate: false);
            screen.Advance(0.3f);
            screen.Show(toHome: true);
        }
        expect(rewardNow.Home.Visible && rewardNow.Home.Alpha == 0f && rewardNow.Home.Scale == 0.97f &&
               rewardBefore.Home.Alpha == 1f && rewardBefore.Home.Scale == 1f,
            "back from the reward's page: home now fades in from 0.97 like other returns (it used to pop in at full opacity)");
        rewardNow.Advance(1f);
        expect(rewardNow.AtRest(), "and it ends at rest on home");
        return samples;
    }

    // ShowPage follows HbrManualPageTransition, and every way into a page goes through ShowPage.
    private static void SourceChecks(Action<bool, string> expect, string repo)
    {
        string screens = Path.Combine(repo, "Core", "Nodes", "Screens");
        string pages = File.ReadAllText(Path.Combine(screens, "NSixSenseStatusScreen.Pages.cs"));
        int start = pages.IndexOf("private void ShowPage(", StringComparison.Ordinal);
        int end = pages.IndexOf("private void Flash()", StringComparison.Ordinal);
        expect(start >= 0 && end > start, "ShowPage is found before Flash");
        string show = pages[start..end];
        int keep = show.IndexOf("if (step == HbrManualPageTransition.Step.Keep) return;", StringComparison.Ordinal);
        int kill = show.IndexOf("_pageTween?.Kill();", StringComparison.Ordinal);
        expect(show.Contains("_pageTween.IsValid() && _pageTween.IsRunning()", StringComparison.Ordinal) &&
               show.Contains("HbrManualPageTransition.Decide(animate, previous == ManualPage.Home, home, running)", StringComparison.Ordinal),
            "ShowPage decides with HbrManualPageTransition from the tween actually running");
        expect(keep >= 0 && kill > keep, "a switch between pages leaves the running transition alone (it returns before Kill)");
        expect(!show.Contains("home == fromHome", StringComparison.Ordinal), "the old kill-and-return between pages is gone");
        expect(show.Contains("HbrManualPageTransition.Rest(home)", StringComparison.Ordinal) &&
               show.Contains("HbrManualPageTransition.EnterFrom(", StringComparison.Ordinal) &&
               show.Contains("HbrManualPageTransition.VisibleAfterLeaving(", StringComparison.Ordinal) &&
               show.Contains("HbrManualPageTransition.FadeSeconds", StringComparison.Ordinal) &&
               show.Contains("HbrManualPageTransition.MoveSeconds", StringComparison.Ordinal),
            "ShowPage snaps, enters, hides and times the layers by HbrManualPageTransition");
        expect(!show.Contains("40f", StringComparison.Ordinal) && !show.Contains("0.97f", StringComparison.Ordinal) &&
               !show.Contains("0.22", StringComparison.Ordinal) && !show.Contains(", 0.2)", StringComparison.Ordinal),
            "ShowPage has no slide, scale or timing of its own");
        expect(pages.Contains("SetLayerPose(_pageRoot, homeLayer: false, HbrManualPageTransition.PageAway);", StringComparison.Ordinal),
            "the page layer starts hidden in its away pose");
        string screen = File.ReadAllText(Path.Combine(screens, "NSixSenseStatusScreen.cs"));
        expect(screen.Contains("if (_page != ManualPage.Glossary) SelectPage(ManualPage.Glossary);", StringComparison.Ordinal) &&
               screen.Contains("ShowPage(previous, page, animate);", StringComparison.Ordinal) &&
               screen.Contains("SelectPage(start, animate: false);", StringComparison.Ordinal) &&
               System.Text.RegularExpressions.Regex.Matches(screen + pages, @"\bShowPage\(").Count == 2,
            "Ctrl+F, back, the home buttons and the reward's page all switch through SelectPage → ShowPage");
    }

    // Runs the switches and reports whether the screen rested whenever nothing ran and ended at rest.
    // strict: exact poses (this build); otherwise only what is drawn (the old build left hidden layers at full alpha).
    private static bool Replay(SimScreen screen, bool[] toHome, float[] gap, bool[]? animate, bool strict)
    {
        bool Settled() => screen.Running || (strict ? screen.AtRest() : screen.LooksAtRest());
        bool ok = true;
        for (int i = 0; i < toHome.Length; i++)
        {
            screen.Show(toHome[i], animate?[i] ?? true);
            ok &= Settled();
            screen.Advance(gap[i]);
            ok &= Settled();
        }
        screen.Advance(1f);
        return ok && !screen.Running && (strict ? screen.AtRest() : screen.LooksAtRest());
    }

    private static string Describe(bool[] toHome, float[] gap) => string.Join(" ", toHome.Select((home, i) =>
        string.Create(CultureInfo.InvariantCulture, $"{(home ? "home" : "page")}+{gap[i]:0.000}s")));
}

// The manual's home layer or page layer.
internal sealed class SimLayer
{
    internal bool Visible;
    internal float Alpha;
    internal float X;
    internal float Scale;

    internal HbrManualLayerPose Pose => new(Visible, Alpha, X, Scale);

    internal void Set(HbrManualLayerPose pose)
    {
        Visible = pose.Visible;
        Alpha = pose.Alpha;
        X = pose.X;
        Scale = pose.Scale;
    }
}

internal enum SimProperty { Alpha, X, Scale }

// Godot's Tween as ShowPage uses it: parallel tracks, then one chained callback.
internal sealed class SimTween
{
    private readonly List<(SimLayer Layer, SimProperty Property, float From, float To, float Duration, bool EaseOut)> _tracks = new();
    private float _elapsed;

    internal Action? End;
    internal bool Alive { get; private set; } = true;

    internal void Track(SimLayer layer, SimProperty property, float to, float duration, bool easeOut) =>
        _tracks.Add((layer, property, Read(layer, property), to, duration, easeOut));

    internal void Kill() => Alive = false;

    internal void Advance(float seconds)
    {
        if (!Alive) return;
        _elapsed += seconds;
        float length = 0f;
        foreach (var track in _tracks)
        {
            length = Math.Max(length, track.Duration);
            float t = Math.Clamp(_elapsed / track.Duration, 0f, 1f);
            // TransitionType.Quart with EaseType.Out for the moves, linear for the fades.
            float eased = track.EaseOut ? 1f - MathF.Pow(1f - t, 4f) : t;
            Write(track.Layer, track.Property, t >= 1f ? track.To : track.From + (track.To - track.From) * eased);
        }
        if (_elapsed + 1e-6f < length) return;
        Alive = false;
        End?.Invoke();
    }

    private static float Read(SimLayer layer, SimProperty property) => property switch
    {
        SimProperty.Alpha => layer.Alpha,
        SimProperty.X => layer.X,
        _ => layer.Scale
    };

    private static void Write(SimLayer layer, SimProperty property, float value)
    {
        switch (property)
        {
            case SimProperty.Alpha: layer.Alpha = value; break;
            case SimProperty.X: layer.X = value; break;
            default: layer.Scale = value; break;
        }
    }
}

// NSixSenseStatusScreen's two layers and ShowPage: as of this build, or as before 2026-11-12 (old).
internal sealed class SimScreen
{
    private readonly bool _old;
    private SimTween? _tween;

    internal readonly SimLayer Home = new();
    internal readonly SimLayer Page = new();
    internal bool OnHome = true;

    internal SimScreen(bool old)
    {
        _old = old;
        Home.Set(HbrManualPageTransition.HomeShown);
        // BuildUi: the page layer now starts in its away pose; it used to start hidden at full alpha.
        Page.Set(old ? new HbrManualLayerPose(false, 1f, 0f, 1f) : HbrManualPageTransition.PageAway);
    }

    internal bool Running => _tween is { Alive: true };

    internal void Advance(float seconds) => _tween?.Advance(seconds);

    internal void Show(bool toHome, bool animate = true)
    {
        bool fromHome = OnHome;
        OnHome = toHome;
        if (_old) ShowBefore(fromHome, toHome, animate);
        else ShowNow(fromHome, toHome, animate);
    }

    internal bool AtRest()
    {
        (HbrManualLayerPose home, HbrManualLayerPose page) = HbrManualPageTransition.Rest(OnHome);
        return Same(Home.Pose, home) && Same(Page.Pose, page);
    }

    // What is drawn: which layers show, and how the shown ones stand.
    internal bool LooksAtRest()
    {
        (HbrManualLayerPose home, HbrManualLayerPose page) = HbrManualPageTransition.Rest(OnHome);
        return SameLook(Home.Pose, home) && SameLook(Page.Pose, page);
    }

    internal static bool SameLook(SimScreen a, SimScreen b) => SameLook(a.Home.Pose, b.Home.Pose) && SameLook(a.Page.Pose, b.Page.Pose);

    private static bool SameLook(HbrManualLayerPose a, HbrManualLayerPose b) =>
        a.Visible == b.Visible && (!a.Visible || Near(a.Alpha, b.Alpha) && Near(a.X, b.X) && Near(a.Scale, b.Scale));

    private static bool Same(HbrManualLayerPose a, HbrManualLayerPose b) =>
        a.Visible == b.Visible && Near(a.Alpha, b.Alpha) && Near(a.X, b.X) && Near(a.Scale, b.Scale);

    private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

    // ShowPage as of 2026-11-12.
    private void ShowNow(bool fromHome, bool toHome, bool animate)
    {
        HbrManualPageTransition.Step step = HbrManualPageTransition.Decide(animate, fromHome, toHome, Running);
        if (step == HbrManualPageTransition.Step.Keep) return;
        _tween?.Kill();
        _tween = null;
        if (step == HbrManualPageTransition.Step.Snap)
        {
            (HbrManualLayerPose home, HbrManualLayerPose page) = HbrManualPageTransition.Rest(toHome);
            Home.Set(home);
            Page.Set(page);
            return;
        }
        SimTween tween = _tween = new SimTween();
        TweenLayer(tween, Home, homeLayer: true, entering: toHome);
        TweenLayer(tween, Page, homeLayer: false, entering: !toHome);
        SimLayer leaving = toHome ? Page : Home;
        tween.End = () => leaving.Visible = HbrManualPageTransition.VisibleAfterLeaving(homeLayer: !toHome, nowHome: OnHome);
    }

    private static void TweenLayer(SimTween tween, SimLayer layer, bool homeLayer, bool entering)
    {
        if (entering) layer.Set(HbrManualPageTransition.EnterFrom(layer.Pose, homeLayer));
        HbrManualLayerPose target = entering ? HbrManualPageTransition.Shown(homeLayer) : HbrManualPageTransition.Away(homeLayer);
        tween.Track(layer, SimProperty.Alpha, target.Alpha, HbrManualPageTransition.FadeSeconds, easeOut: false);
        if (homeLayer) tween.Track(layer, SimProperty.Scale, target.Scale, HbrManualPageTransition.MoveSeconds, easeOut: true);
        else tween.Track(layer, SimProperty.X, target.X, HbrManualPageTransition.MoveSeconds, easeOut: true);
    }

    // ShowPage before 2026-11-12, kept for the reverse checks: it killed the running tween first and, for a
    // switch between pages, returned without finishing it.
    private void ShowBefore(bool fromHome, bool toHome, bool animate)
    {
        _tween?.Kill();
        if (!animate)
        {
            Home.Set(new HbrManualLayerPose(toHome, 1f, 0f, 1f));
            Page.Set(new HbrManualLayerPose(!toHome, 1f, 0f, 1f));
            return;
        }
        if (toHome == fromHome) return;
        SimTween tween = _tween = new SimTween();
        if (!toHome)
        {
            Page.Set(new HbrManualLayerPose(true, 0f, 40f, Page.Scale));
            tween.Track(Page, SimProperty.Alpha, 1f, 0.2f, easeOut: false);
            tween.Track(Page, SimProperty.X, 0f, 0.22f, easeOut: true);
            tween.Track(Home, SimProperty.Alpha, 0f, 0.2f, easeOut: false);
            tween.Track(Home, SimProperty.Scale, 0.97f, 0.22f, easeOut: true);
            tween.End = () => Home.Visible = OnHome;
        }
        else
        {
            Home.Visible = true;
            tween.Track(Home, SimProperty.Alpha, 1f, 0.2f, easeOut: false);
            tween.Track(Home, SimProperty.Scale, 1f, 0.22f, easeOut: true);
            tween.Track(Page, SimProperty.Alpha, 0f, 0.2f, easeOut: false);
            tween.Track(Page, SimProperty.X, 40f, 0.22f, easeOut: true);
            tween.End = () => Page.Visible = !OnHome;
        }
    }
}
