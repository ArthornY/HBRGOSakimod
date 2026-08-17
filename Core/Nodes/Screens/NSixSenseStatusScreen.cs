using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NSixSenseStatusScreen : Control, IOverlayScreen
{
    private const string AttributeImagePath = "ui/six_sense_attributes.png";
    private const string QuestionImagePath = "ui/six_sense_question.png";

    private static readonly Vector2 SourceImageSize = new(1351f, 1164f);
    private static NSixSenseStatusScreen _activeScreen;

    private ElectronicSoldierManual _manual;
    private readonly Dictionary<SixSenseAttribute, Label> _valueLabels = new();
    private Control _panel;
    private PanelContainer _helpPanel;
    private RichTextLabel _helpText;
    private Button _closeButton;
    private SixSenseRadarOverlay _radarOverlay;
    private Tween _fadeTween;

    public NetScreenType ScreenType => NetScreenType.Room;

    public bool UseSharedBackstop => true;

    public Control DefaultFocusedControl => (Control)_closeButton ?? this;

    public static void Show(ElectronicSoldierManual manual)
    {
        NOverlayStack overlayStack = NOverlayStack.Instance;
        if (overlayStack == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_activeScreen) &&
            !_activeScreen.IsQueuedForDeletion() &&
            _activeScreen.IsInsideTree())
        {
            _activeScreen.Refresh(manual);
            return;
        }

        NSixSenseStatusScreen screen = new()
        {
            Name = "SixSenseStatusScreen",
            _manual = manual
        };
        _activeScreen = screen;
        overlayStack.Push(screen);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetFullRect(this);
        BuildUi();
        LayoutPanel();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutPanel();
        }
    }

    public void AfterOverlayOpened()
    {
        Modulate = Colors.Transparent;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2);
    }

    public void AfterOverlayClosed()
    {
        _fadeTween?.Kill();
        if (ReferenceEquals(_activeScreen, this))
        {
            _activeScreen = null;
        }

        this.QueueFreeSafely();
    }

    public void AfterOverlayShown()
    {
        Visible = true;
    }

    public void AfterOverlayHidden()
    {
        Visible = false;
    }

    private void BuildUi()
    {
        _panel = new Control
        {
            Name = "SixSensePanel",
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(_panel);

        TextureRect background = new()
        {
            Name = "SixSenseBackground",
            Texture = PreloadManager.Cache.GetTexture2D(ImageHelper.GetImagePath(AttributeImagePath)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(background);
        _panel.AddChild(background);

        _radarOverlay = new SixSenseRadarOverlay
        {
            Name = "SixSenseRadarOverlay",
            Manual = _manual,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_radarOverlay);
        _panel.AddChild(_radarOverlay);

        AddValueLabels();
        AddHelpButton();
        AddHelpPanel();
        AddCloseButton();
    }

    private void AddValueLabels()
    {
        foreach ((SixSenseAttribute attribute, Vector2 anchor) in GetLabelAnchors())
        {
            Label label = CreateValueLabel(_manual.GetSenseAmount(attribute).ToString());
            label.AnchorLeft = anchor.X;
            label.AnchorTop = anchor.Y;
            label.AnchorRight = anchor.X;
            label.AnchorBottom = anchor.Y;
            label.OffsetLeft = -34f;
            label.OffsetTop = -18f;
            label.OffsetRight = 34f;
            label.OffsetBottom = 18f;
            _panel.AddChild(label);
            _valueLabels[attribute] = label;
        }
    }

    private void AddHelpButton()
    {
        Button helpButton = new()
        {
            Name = "SixSenseHelpButton",
            Icon = PreloadManager.Cache.GetTexture2D(ImageHelper.GetImagePath(QuestionImagePath)),
            ExpandIcon = true,
            AnchorLeft = 0.87f,
            AnchorTop = 0.055f,
            AnchorRight = 0.87f,
            AnchorBottom = 0.055f,
            OffsetLeft = -2f,
            OffsetTop = -2f,
            OffsetRight = 58f,
            OffsetBottom = 58f,
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = "查看六感增益"
        };
        helpButton.Pressed += ToggleHelpPanel;
        helpButton.MouseEntered += ShowHelpPanel;
        helpButton.MouseExited += HideHelpPanel;
        _panel.AddChild(helpButton);
    }

    private void AddHelpPanel()
    {
        _helpPanel = new PanelContainer
        {
            AnchorLeft = 0.55f,
            AnchorTop = 0.13f,
            AnchorRight = 0.94f,
            AnchorBottom = 0.68f,
            OffsetLeft = 0f,
            OffsetTop = 0f,
            OffsetRight = 0f,
            OffsetBottom = 0f,
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        _helpPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.08f, 0.08f, 0.11f, 0.94f), new Color(0.92f, 0.78f, 1f, 0.9f)));
        _panel.AddChild(_helpPanel);

        _helpText = new RichTextLabel
        {
            Text = BuildHelpText(),
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 18f,
            OffsetTop = 16f,
            OffsetRight = -18f,
            OffsetBottom = -16f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _helpText.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.NormalFontSize, 20);
        _helpText.AddThemeColorOverride(ThemeConstants.RichTextLabel.DefaultColor, Colors.White);
        _helpPanel.AddChild(_helpText);
    }

    private void AddCloseButton()
    {
        _closeButton = new Button
        {
            Text = "关闭",
            AnchorLeft = 0.5f,
            AnchorTop = 0.91f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.91f,
            OffsetLeft = -72f,
            OffsetTop = 0f,
            OffsetRight = 72f,
            OffsetBottom = 52f,
            FocusMode = FocusModeEnum.All
        };
        _closeButton.AddThemeFontSizeOverride("font_size", 22);
        _closeButton.AddThemeColorOverride("font_color", Colors.Black);
        _closeButton.AddThemeColorOverride("font_hover_color", Colors.Black);
        _closeButton.AddThemeColorOverride("font_pressed_color", Colors.Black);
        _closeButton.AddThemeColorOverride("font_focus_color", Colors.Black);
        _closeButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(Colors.White));
        _closeButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.92f, 0.92f, 0.92f)));
        _closeButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.82f, 0.82f, 0.82f)));
        _closeButton.AddThemeStyleboxOverride("focus", CreateButtonStyle(Colors.White));
        _closeButton.Pressed += Close;
        _panel.AddChild(_closeButton);
    }

    private void LayoutPanel()
    {
        if (_panel == null)
        {
            return;
        }

        Vector2 viewportSize = Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return;
        }

        float scale = Math.Min(viewportSize.X * 0.82f / SourceImageSize.X, viewportSize.Y * 0.86f / SourceImageSize.Y);
        scale = Mathf.Clamp(scale, 0.35f, 0.88f);
        Vector2 panelSize = SourceImageSize * scale;
        _panel.Size = panelSize;
        _panel.Position = new Vector2(
            (viewportSize.X - panelSize.X) * 0.5f,
            viewportSize.Y - panelSize.Y);
    }

    private void ToggleHelpPanel()
    {
        _helpPanel.Visible = !_helpPanel.Visible;
    }

    private void ShowHelpPanel()
    {
        _helpPanel.Visible = true;
    }

    private void HideHelpPanel()
    {
        _helpPanel.Visible = false;
    }

    private void Close()
    {
        NOverlayStack.Instance?.Remove(this);
    }

    private void Refresh(ElectronicSoldierManual manual)
    {
        _manual = manual;
        foreach ((SixSenseAttribute attribute, Label label) in _valueLabels)
        {
            label.Text = _manual.GetSenseAmount(attribute).ToString();
        }

        if (_radarOverlay != null)
        {
            _radarOverlay.Manual = _manual;
            _radarOverlay.QueueRedraw();
        }

        if (_helpText != null)
        {
            _helpText.Text = BuildHelpText();
        }
    }

    private string BuildHelpText()
    {
        return string.Join("\n", ElectronicSoldierManual.AllSixSenseAttributes.Select(attribute =>
            $"{ElectronicSoldierManual.GetSenseTitle(attribute)}：{_manual.GetSenseShortDescriptionForOwner(attribute)}"));
    }

    private static IEnumerable<(SixSenseAttribute Attribute, Vector2 Anchor)> GetLabelAnchors()
    {
        yield return (SixSenseAttribute.LeadershipCharm, new Vector2(0.43f, 0.27f));
        yield return (SixSenseAttribute.Talent, new Vector2(0.61f, 0.395f));
        yield return (SixSenseAttribute.Gentleness, new Vector2(0.61f, 0.62f));
        yield return (SixSenseAttribute.MentalStrength, new Vector2(0.405f, 0.70f));
        yield return (SixSenseAttribute.Simplicity, new Vector2(0.21f, 0.625f));
        yield return (SixSenseAttribute.Madness, new Vector2(0.23f, 0.40f));
    }

    private static Label CreateValueLabel(string text)
    {
        Label label = new()
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 26);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, Colors.White);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, Colors.Black);
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 8);
        return label;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            ContentMarginBottom = 14,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 14,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        return style;
    }

    private static StyleBoxFlat CreateButtonStyle(Color background)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = Colors.Black,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        return style;
    }

    private static void SetFullRect(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }

    private partial class SixSenseRadarOverlay : Control
    {
        private static readonly Vector2 Center = new(0.418f, 0.471f);
        private static readonly Dictionary<SixSenseAttribute, Vector2> MaxPoints = new()
        {
            [SixSenseAttribute.LeadershipCharm] = new Vector2(0.429f, 0.305f),
            [SixSenseAttribute.Talent] = new Vector2(0.565f, 0.375f),
            [SixSenseAttribute.Gentleness] = new Vector2(0.535f, 0.610f),
            [SixSenseAttribute.MentalStrength] = new Vector2(0.410f, 0.640f),
            [SixSenseAttribute.Simplicity] = new Vector2(0.300f, 0.610f),
            [SixSenseAttribute.Madness] = new Vector2(0.295f, 0.380f)
        };

        public ElectronicSoldierManual Manual { get; set; }

        public override void _Draw()
        {
            Vector2[] points = GetPoints(outer: true);
            if (points.Length < 3)
            {
                return;
            }

            DrawColoredPolygon(points, new Color(0.95f, 0.38f, 0.72f, 0.38f));
            DrawPolyline(CloseLoop(points), new Color(1f, 0.62f, 0.86f, 0.95f), 3f, true);
            DrawPolyline(CloseLoop(GetPoints(outer: false)), new Color(0.52f, 0.12f, 0.42f, 0.95f), 2f, true);
        }

        private Vector2[] GetPoints(bool outer)
        {
            if (Manual == null)
            {
                return Array.Empty<Vector2>();
            }

            Vector2 center = Center * Size;
            return ElectronicSoldierManual.AllSixSenseAttributes
                .Select(attribute => GetPoint(attribute, center, outer))
                .ToArray();
        }

        private Vector2 GetPoint(SixSenseAttribute attribute, Vector2 center, bool outer)
        {
            float amount = Mathf.Clamp(Manual.GetSenseAmount(attribute), 0, ElectronicSoldierManual.MaxSixSense);
            Vector2 maxPoint = MaxPoints[attribute] * Size;
            Vector2 point = center + (maxPoint - center) * (amount / ElectronicSoldierManual.MaxSixSense);
            if (outer)
            {
                return point;
            }

            Vector2 direction = center - point;
            return direction.Length() <= 0.001f ? point : point + direction.Normalized() * 2f;
        }

        private static Vector2[] CloseLoop(Vector2[] points)
        {
            return points.Concat(new[] { points[0] }).ToArray();
        }
    }
}
