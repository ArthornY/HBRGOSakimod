using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The phone itself: blurred backdrop, the 1920x1080 rig with the exit plate, DenchoFrame, the clipped
// screen holding home and the pages, the screen flash and the hand. Page chrome: back button, title,
// info and rule above an 836x912 body.
public partial class NSixSenseStatusScreen
{
    private enum ExitState { Normal, Active, Disabled }

    private const string BackdropShader = "res://shaders/hbr_manual_backdrop.gdshader";

    private Control _rig;
    private NManualPhoneHome _home;
    private Control _pageRoot;
    private Control _body;
    private NManualSensesPage _sensesPage;
    private Button _back;
    private Label _title;
    private RichTextLabel _info;
    private NinePatchRect _flash;
    private Button _exit;
    private TextureRect[] _exitPlates;
    private TextureRect[] _exitIcons;
    private Label _exitLabel;
    private bool _exitHovered;
    private Tween _pageTween;
    private Tween _flashTween;

    private void BuildUi()
    {
        // Behind the phone: the scene blurred, darkened and slightly desaturated.
        Shader shader = ResourceLoader.Load<Shader>(BackdropShader);
        ColorRect backdrop = new()
        {
            Name = "Backdrop", Color = new Color(0.02f, 0.03f, 0.05f, 0.6f), MouseFilter = MouseFilterEnum.Ignore,
            Material = shader == null ? null : new ShaderMaterial { Shader = shader }
        };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Gradient dim = new()
        {
            Offsets = new[] { 0f, 1f },
            Colors = new[] { new Color(0.04f, 0.063f, 0.12f, 0.2f), new Color(0.016f, 0.024f, 0.047f, 0.6f) }
        };
        TextureRect vignette = new()
        {
            Name = "Dim", MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = new GradientTexture2D
            {
                Gradient = dim, Width = 256, Height = 144, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.6f, 0.45f), FillTo = new Vector2(1.2f, 0.45f)
            }
        };
        AddChild(vignette);
        vignette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _rig = new Control
        {
            Name = "Rig", MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(HbrManualPhoneLayout.CanvasWidth, HbrManualPhoneLayout.CanvasHeight)
        };
        AddChild(_rig);
        BuildExit();
        _rig.AddChild(HbrManualPhoneArt.Sprite("Frame", "DenchoFrame", HbrManualPhoneLayout.Frame));

        HbrPhoneRect screen = HbrManualPhoneLayout.Screen;
        Control clip = new() { Name = "Screen", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        HbrManualPhoneArt.Place(clip, screen);
        _rig.AddChild(clip);
        // Home and pages use canvas coordinates, like the preview's .cv layer.
        Control canvas = new()
        {
            Name = "Canvas", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(-screen.X, -screen.Y),
            Size = new Vector2(HbrManualPhoneLayout.CanvasWidth, HbrManualPhoneLayout.CanvasHeight)
        };
        clip.AddChild(canvas);

        _home = new NManualPhoneHome
        {
            Name = "Home",
            OpenButton = button =>
            {
                // The 六感 button, points capsule, overview radar and reward entry all open the sense view.
                if (button == HbrManualHomeButton.Senses) _statsView = false;
                OpenHomeButton(button);
            },
            OpenSenses = () => { _statsView = false; SelectPage(ManualPage.Senses); },
            OpenStat = index =>
            {
                _statHighlight = index;
                SelectPage(ManualPage.BattleStats);
            }
        };
        canvas.AddChild(_home);

        _pageRoot = new Control
        {
            Name = "Page", MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(HbrManualPhoneLayout.CanvasWidth, HbrManualPhoneLayout.CanvasHeight)
        };
        // Home is on show first; the page waits hidden in its away pose (HbrManualPageTransition.Rest).
        SetLayerPose(_pageRoot, homeLayer: false, HbrManualPageTransition.PageAway);
        canvas.AddChild(_pageRoot);
        BuildPageChrome();

        HbrPhoneRect flash = HbrManualPhoneLayout.Flash;
        _flash = HbrManualPhoneArt.Patch("Flash", "DenchoFrameFlash", flash, HbrManualPhoneLayout.FlashCap);
        _flash.Modulate = new Color(1f, 1f, 1f, 0f);
        _flash.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _rig.AddChild(_flash);
        _rig.AddChild(HbrManualPhoneArt.Sprite("Hand", "DenchoRightHand", HbrManualPhoneLayout.Hand));
    }

    private void OpenHomeButton(HbrManualHomeButton button) =>
        SelectPage(button switch
            {
                HbrManualHomeButton.Senses => ManualPage.Senses,
                HbrManualHomeButton.Equipment => ManualPage.Equipment,
                HbrManualHomeButton.Shop => ManualPage.Shop,
                HbrManualHomeButton.Cannon => ManualPage.CannonUpgrades,
                HbrManualHomeButton.FateDice => ManualPage.FateDice,
                _ => ManualPage.Glossary
            });

    private void BuildExit()
    {
        HbrPhoneRect rect = HbrManualPhoneLayout.Exit;
        _exit = new Button { Name = "Exit", TooltipText = "关闭手册（Esc）" };
        HbrManualPhoneArt.Bare(_exit);
        HbrManualPhoneArt.Place(_exit, rect);
        _rig.AddChild(_exit);
        HbrPhoneRect full = new(0f, 0f, rect.Width, rect.Height);
        _exitPlates = new[]
        {
            HbrManualPhoneArt.Sprite("Normal", "ButtonDungeonExitDefault", full),
            HbrManualPhoneArt.Sprite("Active", "ButtonDungeonExitAvtive", full),
            HbrManualPhoneArt.Sprite("Disabled", "ButtonDungeonExitDisable", full)
        };
        foreach (TextureRect plate in _exitPlates) _exit.AddChild(plate);
        _exitIcons = new[]
        {
            HbrManualPhoneArt.Sprite("Icon", "IconExitDefault", HbrManualPhoneLayout.ExitIcon),
            HbrManualPhoneArt.Sprite("IconActive", "IconExitActive", HbrManualPhoneLayout.ExitIcon)
        };
        foreach (TextureRect icon in _exitIcons)
        {
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _exit.AddChild(icon);
        }
        _exitLabel = HbrManualPhoneArt.Label("Label", HbrManualPhoneLayout.ExitText, HbrManualPhoneLayout.ExitLabelSize,
            new Color("f4e9f6"), align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(_exitLabel, HbrManualPhoneLayout.ExitLabel);
        _exit.AddChild(_exitLabel);
        _exit.MouseEntered += () => { _exitHovered = true; RefreshExit(); };
        _exit.MouseExited += () => { _exitHovered = false; RefreshExit(); };
        _exit.FocusEntered += () => { _exitHovered = true; RefreshExit(); };
        _exit.FocusExited += () => { _exitHovered = false; RefreshExit(); };
        _exit.Pressed += Close;
        SetExitState(ExitState.Normal);
    }

    private void RefreshExit() =>
        SetExitState(_manual?.IsAllocationPending == true ? ExitState.Disabled : _exitHovered ? ExitState.Active : ExitState.Normal);

    // While a confirmation is in flight the plate greys out; closing then waits for the result.
    private void SetExitState(ExitState state)
    {
        if (_exitPlates == null) return;
        for (int i = 0; i < _exitPlates.Length; i++) _exitPlates[i].Visible = i == (int)state;
        _exitIcons[0].Visible = state != ExitState.Active;
        _exitIcons[1].Visible = state == ExitState.Active;
        float alpha = state == ExitState.Disabled ? 0.45f : 1f;
        _exitIcons[0].Modulate = new Color(1f, 1f, 1f, alpha);
        _exitLabel.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    private void BuildPageChrome()
    {
        _back = new Button { Name = "Back", TooltipText = "返回（Esc）" };
        HbrManualPhoneArt.Bare(_back);
        HbrManualPhoneArt.Place(_back, HbrManualPhoneLayout.Back);
        NManualFrost frost = new() { Name = "Frost", Chevron = true, Size = new Vector2(HbrManualPhoneLayout.Back.Width, HbrManualPhoneLayout.Back.Height) };
        _back.AddChild(frost);
        _back.MouseEntered += () => frost.Hovered = true;
        _back.MouseExited += () => frost.Hovered = false;
        _back.FocusEntered += () => frost.Hovered = true;
        _back.FocusExited += () => frost.Hovered = false;
        _back.Pressed += Back;
        _pageRoot.AddChild(_back);

        _title = HbrManualPhoneArt.Label("Title", "", 40, Colors.White, glow: true);
        HbrManualPhoneArt.Place(_title, HbrManualPhoneLayout.PageTitle);
        _pageRoot.AddChild(_title);

        _info = new RichTextLabel
        {
            Name = "Info", BbcodeEnabled = true, FitContent = false, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off, MouseFilter = MouseFilterEnum.Ignore
        };
        _info.AddThemeFontOverride("normal_font", HbrManualPhoneArt.Font);
        _info.AddThemeFontSizeOverride("normal_font_size", 24);
        _info.AddThemeColorOverride("default_color", HbrManualPhoneArt.Muted);
        HbrManualPhoneArt.Place(_info, new HbrPhoneRect(HbrManualPhoneLayout.PageInfoRight - 420f,
            HbrManualPhoneLayout.PageInfoTop - 4f, 420f, 44f));
        _pageRoot.AddChild(_info);

        HbrPhoneRect line = HbrManualPhoneLayout.PageLine;
        Gradient rule = new()
        {
            Offsets = new[] { 0f, 0.12f, 0.88f, 1f },
            Colors = new[]
            {
                new Color(0.52f, 0.91f, 0.94f, 0f), new Color(0.52f, 0.91f, 0.94f, 0.85f),
                new Color(0.52f, 0.91f, 0.94f, 0.85f), new Color(0.52f, 0.91f, 0.94f, 0f)
            }
        };
        TextureRect ruleRect = new()
        {
            Name = "Rule", MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = new GradientTexture2D { Gradient = rule, Width = 256, Height = 2 }
        };
        HbrManualPhoneArt.Place(ruleRect, line);
        _pageRoot.AddChild(ruleRect);

        _body = new Control { Name = "Body", MouseFilter = MouseFilterEnum.Ignore };
        HbrManualPhoneArt.Place(_body, HbrManualPhoneLayout.Body);
        _pageRoot.AddChild(_body);
        _sensesPage = new NManualSensesPage
        {
            Name = "Senses",
            Adjust = AdjustPoint,
            Select = sense => { _selected = sense; Refresh(_manual); },
            Confirm = ConfirmAllocation,
            SetStatsView = stats => { _statsView = stats; Refresh(_manual); },
            // From the battle stat view, the selected sense's stat is lit on the stats page.
            OpenStats = () =>
            {
                _statHighlight = HbrManualPhoneData.StatIndex(_selected);
                SelectPage(ManualPage.BattleStats);
            }
        };
        _body.AddChild(_sensesPage);
    }

    private Vector2 RigOrigin()
    {
        (_, float x, float y) = HbrManualPhoneLayout.RigPlacement(Size.X, Size.Y);
        return new Vector2(x, y);
    }

    // The rig scales as a whole to fit below the run's top bar, which draws above the manual.
    private void LayoutRig()
    {
        if (_rig == null) return;
        (float scale, float x, float y) = HbrManualPhoneLayout.RigPlacement(Size.X, Size.Y);
        _rig.Scale = Vector2.One * scale;
        _rig.Position = new Vector2(x, y);
    }

    // Home slips back (fade, .97) as a page slides in from 40px right; returning reverses it.
    // Each switch flashes the phone screen. However fast the switches come (Ctrl+F, Esc, the back button,
    // a controller), the layers end at rest for the page shown (HbrManualPageTransition).
    private void ShowPage(ManualPage previous, ManualPage page, bool animate)
    {
        bool home = page == ManualPage.Home;
        _title.Text = page switch
        {
            ManualPage.BattleStats => HbrManualPhoneLayout.PortraitTitle,
            ManualPage.Glossary => HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.Glossary),
            ManualPage.CannonUpgrades => HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.Cannon),
            ManualPage.Equipment => HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.Equipment),
            ManualPage.Shop => HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.Shop),
            ManualPage.FateDice => HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.FateDice),
            _ => string.Empty
        };
        _home.PivotOffset = _home.Size / 2f;
        bool running = GodotObject.IsInstanceValid(_pageTween) && _pageTween.IsValid() && _pageTween.IsRunning();
        HbrManualPageTransition.Step step = HbrManualPageTransition.Decide(animate, previous == ManualPage.Home, home, running);
        if (animate) Flash();
        // Between two pages the running transition already ends at this page's rest; killing it would
        // freeze the page half faded and offset with home still showing (Codex takeover review F5).
        if (step == HbrManualPageTransition.Step.Keep) return;
        _pageTween?.Kill();
        _pageTween = null;
        if (step == HbrManualPageTransition.Step.Snap)
        {
            (HbrManualLayerPose homeRest, HbrManualLayerPose pageRest) = HbrManualPageTransition.Rest(home);
            SetLayerPose(_home, homeLayer: true, homeRest);
            SetLayerPose(_pageRoot, homeLayer: false, pageRest);
            return;
        }
        // Into or out of home, from wherever the layers stand: the side being opened comes in, the other
        // leaves and is hidden at the end.
        _pageTween = CreateTween().SetParallel();
        TweenLayer(_home, homeLayer: true, entering: home);
        TweenLayer(_pageRoot, homeLayer: false, entering: !home);
        Control leaving = home ? _pageRoot : _home;
        _pageTween.Chain().TweenCallback(Callable.From(() =>
            leaving.Visible = HbrManualPageTransition.VisibleAfterLeaving(homeLayer: !home, nowHome: _page == ManualPage.Home)));
    }

    private void TweenLayer(Control layer, bool homeLayer, bool entering)
    {
        if (entering) SetLayerPose(layer, homeLayer, HbrManualPageTransition.EnterFrom(LayerPose(layer, homeLayer), homeLayer));
        HbrManualLayerPose target = entering ? HbrManualPageTransition.Shown(homeLayer) : HbrManualPageTransition.Away(homeLayer);
        _pageTween.TweenProperty(layer, "modulate:a", target.Alpha, HbrManualPageTransition.FadeSeconds);
        if (homeLayer)
            _pageTween.TweenProperty(layer, "scale", Vector2.One * target.Scale, HbrManualPageTransition.MoveSeconds)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        else
            _pageTween.TweenProperty(layer, "position", new Vector2(target.X, 0f), HbrManualPageTransition.MoveSeconds)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
    }

    private static HbrManualLayerPose LayerPose(Control layer, bool homeLayer) =>
        new(layer.Visible, layer.Modulate.A, homeLayer ? 0f : layer.Position.X, homeLayer ? layer.Scale.X : 1f);

    // Home only ever scales and the page only ever slides; both fade.
    private static void SetLayerPose(Control layer, bool homeLayer, HbrManualLayerPose pose)
    {
        layer.Visible = pose.Visible;
        layer.Modulate = new Color(1f, 1f, 1f, pose.Alpha);
        if (homeLayer) layer.Scale = Vector2.One * pose.Scale;
        else layer.Position = new Vector2(pose.X, 0f);
    }

    private void Flash()
    {
        _flashTween?.Kill();
        _flash.Modulate = new Color(1f, 1f, 1f, 0f);
        _flashTween = CreateTween();
        float duration = HbrManualPhoneLayout.FlashDuration;
        _flashTween.TweenProperty(_flash, "modulate:a", HbrManualPhoneLayout.FlashPeak, duration * 0.3f);
        _flashTween.TweenProperty(_flash, "modulate:a", 0f, duration * 0.7f).SetEase(Tween.EaseType.Out);
    }

    private void RefreshHeader()
    {
        if (_info == null || _manual == null) return;
        _title.Visible = _page != ManualPage.Senses;
        string info = string.Empty;
        if (_page == ManualPage.Senses)
        {
            string pink = "#" + HbrManualPhoneLayout.Pink;
            int remaining = _draft.Remaining;
            info = $"{HbrManualPhoneLayout.PointsText} [font_size=32]" +
                (remaining > 0 ? $"[color={pink}]{remaining}[/color]" : $"[color=#ffffff]{remaining}[/color]") + "[/font_size]";
            if (_draft.Total > 0)
                info += $"  [color={pink}][font_size=22]{(_manual.IsAllocationPending ? HbrManualPhoneLayout.SendingText : HbrManualPhoneLayout.PendingText)} +{_draft.Total}[/font_size][/color]";
        }
        else if (_page == ManualPage.BattleStats)
        {
            info = HbrManualPhoneLayout.CharacterName(HbrManualPhoneData.IsHotaru(_manual), HbrManualPhoneData.IsThirtyA(_manual));
        }
        else if (_page == ManualPage.Glossary && _glossaryPage != null)
        {
            info = _glossaryPage.CountText;
        }
        _info.Text = $"[right]{info}[/right]";
    }
}
