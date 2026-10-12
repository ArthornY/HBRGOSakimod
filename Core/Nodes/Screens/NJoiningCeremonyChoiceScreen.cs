using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NJoiningCeremonyChoiceScreen : Control, IOverlayScreen
{
    private const string ChoicePillImagePath =
        "ui/joining_ceremony/choice_pill.png";
    private const string SelectedChoicePillImagePath =
        "ui/joining_ceremony/choice_pill_selected.png";
    private const float ChoiceWidth = 344f;
    private const float ChoiceHeight = 150f;
    private const float ChoiceTextOffsetY = 5f;
    private const float LeftChoiceAnchor = 0.25f;
    private const float RightChoiceAnchor = 0.75f;
    private const float HeaderRuleAnchor = 0.275f;

    private static readonly float[] ChoiceRowAnchors =
    {
        0.36f,
        0.512f,
        0.664f
    };

    private static readonly string[] Subjects =
    {
        "我们",
        "我",
        "妹妹"
    };

    private static readonly string[] Predicates =
    {
        "的记忆被封印了",
        "被绑架了",
        "回去了"
    };

    private static readonly string[,] Rewards =
    {
        { "才能+2", "单纯+2", "获得2瓶随机药水" },
        { "领袖魅力+2", "获得一次稀有卡奖励", "疯狂+2" },
        { "删除一张牌", "精神力+2", "温柔+2" }
    };

    private readonly TaskCompletionSource<int> _completionSource = new();
    private readonly List<Button> _subjectButtons = new();
    private readonly List<Button> _predicateButtons = new();
    private readonly List<Control> _subjectChoiceRoots = new();
    private readonly List<Control> _predicateChoiceRoots = new();
    private readonly List<TextureRect> _subjectBackgrounds = new();
    private readonly List<TextureRect> _predicateBackgrounds = new();
    private readonly List<Control> _subjectConnectors = new();
    private readonly List<Control> _predicateConnectors = new();
    private readonly List<Line2D> _guideLines = new();

    private Control _canvas;
    private Line2D _selectionGlowLine;
    private Line2D _selectionLine;
    private Label _sentenceLabel;
    private Label _rewardLabel;
    private Button _confirmButton;
    private Texture2D _choicePillTexture;
    private Texture2D _selectedChoicePillTexture;
    private int _selectedSubject = -1;
    private int _selectedPredicate = -1;
    private Tween _fadeTween;

    public NetScreenType ScreenType => NetScreenType.Room;

    // HBR leaves the current scene visible under its thought-link overlay.
    public bool UseSharedBackstop => false;

    public Control DefaultFocusedControl =>
        (Control)_subjectButtons.FirstOrDefault() ?? this;

    public static NJoiningCeremonyChoiceScreen ShowScreen()
    {
        NJoiningCeremonyChoiceScreen screen = new()
        {
            Name = "JoiningCeremonyChoiceScreen"
        };
        NOverlayStack.Instance.Push(screen);
        return screen;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetFullRect(this);
        BuildUi();
        CallDeferred(nameof(UpdateLines));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && _canvas != null)
        {
            CallDeferred(nameof(UpdateLines));
        }
    }

    public override void _ExitTree()
    {
        _fadeTween?.Kill();
        if (!_completionSource.Task.IsCompleted)
        {
            _completionSource.SetCanceled();
        }
    }

    public async Task<int> ChoiceSelected()
    {
        int result = await _completionSource.Task;
        NOverlayStack.Instance.Remove(this);
        return result;
    }

    public void AfterOverlayOpened()
    {
        Modulate = Colors.Transparent;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.25)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    public void AfterOverlayClosed()
    {
        _fadeTween?.Kill();
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
        _choicePillTexture = PreloadManager.Cache.GetTexture2D(
            ImageHelper.GetImagePath(ChoicePillImagePath));
        _selectedChoicePillTexture = PreloadManager.Cache.GetTexture2D(
            ImageHelper.GetImagePath(SelectedChoicePillImagePath));

        AddBackdrop();

        _canvas = new Control
        {
            Name = "HbrThoughtConnectionCanvas",
            MouseFilter = MouseFilterEnum.Stop
        };
        SetFullRect(_canvas);
        AddChild(_canvas);

        AddHeader();
        AddConnectionLines();
        AddChoices();
        AddResultArea();
        ConfigureFocusNavigation();
    }

    private void AddBackdrop()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.012f, 0.005f, 0.03f, 0.58f),
            MouseFilter = MouseFilterEnum.Stop
        };
        SetFullRect(backdrop);
        AddChild(backdrop);

        ColorRect headerShade = new()
        {
            AnchorTop = 0.122f,
            AnchorRight = 1f,
            AnchorBottom = 0.232f,
            Color = new Color(0.025f, 0.008f, 0.04f, 0.24f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(headerShade);

        ColorRect bottomShade = new()
        {
            AnchorTop = 0.735f,
            AnchorRight = 1f,
            AnchorBottom = 0.965f,
            Color = new Color(0.018f, 0.004f, 0.028f, 0.18f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(bottomShade);
    }

    private void AddHeader()
    {
        Label title = CreateLabel(
            "入队仪式",
            22,
            new Color(0.95f, 0.79f, 1f));
        SetCenteredRect(title, 0.145f, 420f, 38f);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _canvas.AddChild(title);

        Label question = CreateLabel("难道说...", 40, Colors.White);
        SetCenteredRect(question, 0.192f, 760f, 58f);
        question.HorizontalAlignment = HorizontalAlignment.Center;
        _canvas.AddChild(question);

        ColorRect leftRule = CreateHeaderRule();
        leftRule.AnchorLeft = 0.5f;
        leftRule.AnchorTop = HeaderRuleAnchor;
        leftRule.AnchorRight = 0.5f;
        leftRule.AnchorBottom = HeaderRuleAnchor;
        leftRule.OffsetLeft = -322f;
        leftRule.OffsetTop = -1f;
        leftRule.OffsetRight = -42f;
        leftRule.OffsetBottom = 1f;
        _canvas.AddChild(leftRule);

        ColorRect rightRule = CreateHeaderRule();
        rightRule.AnchorLeft = 0.5f;
        rightRule.AnchorTop = HeaderRuleAnchor;
        rightRule.AnchorRight = 0.5f;
        rightRule.AnchorBottom = HeaderRuleAnchor;
        rightRule.OffsetLeft = 42f;
        rightRule.OffsetTop = -1f;
        rightRule.OffsetRight = 322f;
        rightRule.OffsetBottom = 1f;
        _canvas.AddChild(rightRule);

        Control centerMark = CreateThoughtConnector();
        centerMark.AnchorLeft = 0.5f;
        centerMark.AnchorTop = HeaderRuleAnchor;
        centerMark.AnchorRight = 0.5f;
        centerMark.AnchorBottom = HeaderRuleAnchor;
        centerMark.OffsetLeft = -8f;
        centerMark.OffsetTop = -8f;
        centerMark.OffsetRight = 8f;
        centerMark.OffsetBottom = 8f;
        _canvas.AddChild(centerMark);
    }

    private void AddConnectionLines()
    {
        for (int subject = 0; subject < Subjects.Length; subject++)
        {
            for (int predicate = 0; predicate < Predicates.Length; predicate++)
            {
                Line2D line = new()
                {
                    Width = 1f,
                    DefaultColor = new Color(0.93f, 0.63f, 1f, 0.075f),
                    Antialiased = true
                };
                _guideLines.Add(line);
                _canvas.AddChild(line);
            }
        }

        _selectionGlowLine = new Line2D
        {
            Width = 10f,
            DefaultColor = new Color(1f, 0.28f, 0.94f, 0.24f),
            Antialiased = true,
            Visible = false
        };
        _canvas.AddChild(_selectionGlowLine);

        _selectionLine = new Line2D
        {
            Width = 3f,
            DefaultColor = new Color(1f, 0.79f, 1f, 0.96f),
            Antialiased = true,
            Visible = false
        };
        _canvas.AddChild(_selectionLine);
    }

    private void AddChoices()
    {
        for (int i = 0; i < Subjects.Length; i++)
        {
            int index = i;
            AddChoice(
                Subjects[i],
                LeftChoiceAnchor,
                ChoiceRowAnchors[i],
                true,
                () => SelectSubject(index),
                _subjectButtons,
                _subjectChoiceRoots,
                _subjectBackgrounds,
                _subjectConnectors);
        }

        for (int i = 0; i < Predicates.Length; i++)
        {
            int index = i;
            AddChoice(
                Predicates[i],
                RightChoiceAnchor,
                ChoiceRowAnchors[i],
                false,
                () => SelectPredicate(index),
                _predicateButtons,
                _predicateChoiceRoots,
                _predicateBackgrounds,
                _predicateConnectors);
        }
    }

    private void AddChoice(
        string text,
        float anchorX,
        float anchorY,
        bool connectorOnRight,
        System.Action pressed,
        List<Button> buttons,
        List<Control> roots,
        List<TextureRect> backgrounds,
        List<Control> connectors)
    {
        Control root = new()
        {
            AnchorLeft = anchorX,
            AnchorTop = anchorY,
            AnchorRight = anchorX,
            AnchorBottom = anchorY,
            OffsetLeft = -ChoiceWidth / 2f,
            OffsetTop = -ChoiceHeight / 2f,
            OffsetRight = ChoiceWidth / 2f,
            OffsetBottom = ChoiceHeight / 2f,
            PivotOffset = new Vector2(ChoiceWidth / 2f, ChoiceHeight / 2f),
            MouseFilter = MouseFilterEnum.Pass
        };
        roots.Add(root);
        _canvas.AddChild(root);

        TextureRect background = new()
        {
            Texture = _choicePillTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = Colors.White
        };
        SetFullRect(background);
        backgrounds.Add(background);
        root.AddChild(background);

        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            MouseFilter = MouseFilterEnum.Stop
        };
        SetFullRect(button);
        button.OffsetTop += ChoiceTextOffsetY;
        button.OffsetBottom += ChoiceTextOffsetY;
        AddTransparentButtonStyles(button);
        button.AddThemeFontSizeOverride("font_size", 25);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.01f, 0.12f));
        button.AddThemeConstantOverride("outline_size", 5);
        button.Pressed += pressed;
        button.MouseEntered += () => SetChoiceHover(background, true);
        button.MouseExited += () => SetChoiceHover(background, false);
        button.FocusEntered += () => SetChoiceHover(background, true);
        button.FocusExited += () => SetChoiceHover(background, false);
        buttons.Add(button);
        root.AddChild(button);

        Control connector = CreateThoughtConnector();
        float connectorAnchor = connectorOnRight ? 0.955f : 0.045f;
        connector.AnchorLeft = connectorAnchor;
        connector.AnchorTop = 0.5f;
        connector.AnchorRight = connectorAnchor;
        connector.AnchorBottom = 0.5f;
        connector.OffsetLeft = -10f;
        connector.OffsetTop = -10f;
        connector.OffsetRight = 10f;
        connector.OffsetBottom = 10f;
        connectors.Add(connector);
        root.AddChild(connector);
    }

    private void AddResultArea()
    {
        _sentenceLabel = CreateLabel(
            "连接左右两段思绪",
            27,
            new Color(0.8f, 0.75f, 0.88f));
        SetCenteredRect(_sentenceLabel, 0.775f, 860f, 46f);
        _sentenceLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _canvas.AddChild(_sentenceLabel);

        _rewardLabel = CreateLabel(string.Empty, 21, new Color(1f, 0.79f, 0.96f));
        SetCenteredRect(_rewardLabel, 0.822f, 760f, 38f);
        _rewardLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _canvas.AddChild(_rewardLabel);

        _confirmButton = new Button
        {
            Text = "连接两个选项",
            Disabled = true,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            AnchorLeft = 0.5f,
            AnchorTop = 0.89f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.89f,
            OffsetLeft = -322f,
            OffsetTop = -36f,
            OffsetRight = 322f,
            OffsetBottom = 36f
        };
        AddConfirmButtonStyles(_confirmButton);
        _confirmButton.AddThemeFontSizeOverride("font_size", 24);
        _confirmButton.AddThemeColorOverride("font_color", Colors.White);
        _confirmButton.AddThemeColorOverride("font_hover_color", Colors.White);
        _confirmButton.AddThemeColorOverride("font_pressed_color", Colors.White);
        _confirmButton.AddThemeColorOverride("font_disabled_color", new Color(0.62f, 0.58f, 0.68f));
        _confirmButton.Pressed += Confirm;
        _canvas.AddChild(_confirmButton);
    }

    private void ConfigureFocusNavigation()
    {
        for (int i = 0; i < Subjects.Length; i++)
        {
            int previous = (i + Subjects.Length - 1) % Subjects.Length;
            int next = (i + 1) % Subjects.Length;

            _subjectButtons[i].FocusNeighborTop =
                _subjectButtons[previous].GetPath();
            _subjectButtons[i].FocusNeighborBottom =
                _subjectButtons[next].GetPath();
            _subjectButtons[i].FocusNeighborRight =
                _predicateButtons[i].GetPath();

            _predicateButtons[i].FocusNeighborTop =
                _predicateButtons[previous].GetPath();
            _predicateButtons[i].FocusNeighborBottom =
                _predicateButtons[next].GetPath();
            _predicateButtons[i].FocusNeighborLeft =
                _subjectButtons[i].GetPath();
        }
    }

    private void SelectSubject(int index)
    {
        _selectedSubject = index;
        UpdateSelectionAppearance();
    }

    private void SelectPredicate(int index)
    {
        _selectedPredicate = index;
        UpdateSelectionAppearance();
    }

    private void UpdateSelectionAppearance()
    {
        for (int i = 0; i < _subjectBackgrounds.Count; i++)
        {
            bool selected = i == _selectedSubject;
            _subjectBackgrounds[i].Texture = selected
                ? _selectedChoicePillTexture
                : _choicePillTexture;
            _subjectBackgrounds[i].Modulate = Colors.White;
        }

        for (int i = 0; i < _predicateBackgrounds.Count; i++)
        {
            bool selected = i == _selectedPredicate;
            _predicateBackgrounds[i].Texture = selected
                ? _selectedChoicePillTexture
                : _choicePillTexture;
            _predicateBackgrounds[i].Modulate = Colors.White;
        }

        bool hasCompleteChoice =
            _selectedSubject >= 0 && _selectedPredicate >= 0;
        _confirmButton.Disabled = !hasCompleteChoice;

        if (hasCompleteChoice)
        {
            string reward = Rewards[_selectedSubject, _selectedPredicate];
            _sentenceLabel.Text =
                $"{Subjects[_selectedSubject]}{Predicates[_selectedPredicate]}";
            _sentenceLabel.AddThemeColorOverride(
                ThemeConstants.Label.FontColor,
                Colors.White);
            _rewardLabel.Text = reward;
            _confirmButton.Text = $"确认  ·  {reward}";
            _selectionGlowLine.Modulate = Colors.White;
        }
        else
        {
            _sentenceLabel.Text = "连接左右两段思绪";
            _sentenceLabel.AddThemeColorOverride(
                ThemeConstants.Label.FontColor,
                new Color(0.8f, 0.75f, 0.88f));
            _rewardLabel.Text = string.Empty;
            _confirmButton.Text = "连接两个选项";
            _selectionGlowLine.Modulate = Colors.White;
        }

        CallDeferred(nameof(UpdateLines));
    }

    private void UpdateLines()
    {
        if (_canvas == null ||
            _subjectConnectors.Count != Subjects.Length ||
            _predicateConnectors.Count != Predicates.Length)
        {
            return;
        }

        Vector2 canvasOrigin = _canvas.GlobalPosition;
        int lineIndex = 0;
        for (int subject = 0; subject < Subjects.Length; subject++)
        {
            Vector2 start =
                _subjectConnectors[subject].GetGlobalRect().GetCenter() -
                canvasOrigin;
            for (int predicate = 0; predicate < Predicates.Length; predicate++)
            {
                Vector2 end =
                    _predicateConnectors[predicate].GetGlobalRect().GetCenter() -
                    canvasOrigin;
                _guideLines[lineIndex++].Points = new[] { start, end };
            }
        }

        bool hasCompleteChoice =
            _selectedSubject >= 0 && _selectedPredicate >= 0;
        _selectionGlowLine.Visible = hasCompleteChoice;
        _selectionLine.Visible = hasCompleteChoice;
        if (!hasCompleteChoice)
        {
            return;
        }

        Vector2 selectedStart =
            _subjectConnectors[_selectedSubject].GetGlobalRect().GetCenter() -
            canvasOrigin;
        Vector2 selectedEnd =
            _predicateConnectors[_selectedPredicate].GetGlobalRect().GetCenter() -
            canvasOrigin;
        Vector2[] selectedPoints = { selectedStart, selectedEnd };
        _selectionGlowLine.Points = selectedPoints;
        _selectionLine.Points = selectedPoints;
    }

    private void SetChoiceHover(TextureRect background, bool hovered)
    {
        int subjectIndex = _subjectBackgrounds.IndexOf(background);
        int predicateIndex = _predicateBackgrounds.IndexOf(background);
        bool selected =
            (subjectIndex >= 0 && subjectIndex == _selectedSubject) ||
            (predicateIndex >= 0 && predicateIndex == _selectedPredicate);
        background.Modulate = selected || !hovered
            ? Colors.White
            : new Color(1.08f, 1.04f, 1.08f);
    }

    private void Confirm()
    {
        if (_completionSource.Task.IsCompleted ||
            _selectedSubject < 0 ||
            _selectedPredicate < 0)
        {
            return;
        }

        _completionSource.SetResult(
            _selectedSubject * Predicates.Length + _selectedPredicate);
    }

    private static Control CreateThoughtConnector()
    {
        Control connector = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };

        PanelContainer glow = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(glow);
        glow.AddThemeStyleboxOverride(
            "panel",
            CreateRoundStyle(new Color(1f, 0.25f, 0.95f, 0.3f), 10));
        connector.AddChild(glow);

        PanelContainer core = new()
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -5f,
            OffsetTop = -5f,
            OffsetRight = 5f,
            OffsetBottom = 5f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        StyleBoxFlat coreStyle = CreateRoundStyle(
            new Color(1f, 0.9f, 1f),
            5);
        coreStyle.BorderColor = new Color(1f, 0.35f, 0.92f);
        coreStyle.BorderWidthBottom = 1;
        coreStyle.BorderWidthLeft = 1;
        coreStyle.BorderWidthRight = 1;
        coreStyle.BorderWidthTop = 1;
        core.AddThemeStyleboxOverride("panel", coreStyle);
        connector.AddChild(core);

        return connector;
    }

    private static ColorRect CreateHeaderRule()
    {
        return new ColorRect
        {
            Color = new Color(0.93f, 0.53f, 1f, 0.56f),
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    private static void AddTransparentButtonStyles(Button button)
    {
        StyleBoxFlat transparent = new()
        {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent
        };
        button.AddThemeStyleboxOverride("normal", transparent);
        button.AddThemeStyleboxOverride("hover", transparent);
        button.AddThemeStyleboxOverride("pressed", transparent);
        button.AddThemeStyleboxOverride("focus", transparent);
        button.AddThemeStyleboxOverride("disabled", transparent);
    }

    private static void AddConfirmButtonStyles(Button button)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            CreateCapsuleStyle(
                new Color(0.14f, 0.055f, 0.2f, 0.94f),
                new Color(0.84f, 0.55f, 0.96f, 0.9f)));
        button.AddThemeStyleboxOverride(
            "hover",
            CreateCapsuleStyle(
                new Color(0.48f, 0.08f, 0.39f, 0.98f),
                new Color(1f, 0.82f, 1f)));
        button.AddThemeStyleboxOverride(
            "pressed",
            CreateCapsuleStyle(
                new Color(0.7f, 0.1f, 0.5f, 0.98f),
                Colors.White));
        button.AddThemeStyleboxOverride(
            "focus",
            CreateCapsuleStyle(
                new Color(0.22f, 0.06f, 0.27f, 0.98f),
                Colors.White));
        button.AddThemeStyleboxOverride(
            "disabled",
            CreateCapsuleStyle(
                new Color(0.08f, 0.055f, 0.1f, 0.8f),
                new Color(0.34f, 0.28f, 0.4f, 0.7f)));
    }

    private static StyleBoxFlat CreateCapsuleStyle(
        Color background,
        Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 34,
            CornerRadiusBottomRight = 34,
            CornerRadiusTopLeft = 34,
            CornerRadiusTopRight = 34,
            ContentMarginBottom = 12f,
            ContentMarginLeft = 26f,
            ContentMarginRight = 26f,
            ContentMarginTop = 12f
        };
    }

    private static StyleBoxFlat CreateRoundStyle(Color color, int radius)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius
        };
    }

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        Label label = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, fontSize);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, color);
        label.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            new Color(0.025f, 0.008f, 0.05f));
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 8);
        return label;
    }

    private static void SetCenteredRect(
        Control control,
        float anchorY,
        float width,
        float height)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorTop = anchorY;
        control.AnchorRight = 0.5f;
        control.AnchorBottom = anchorY;
        control.OffsetLeft = -width / 2f;
        control.OffsetTop = -height / 2f;
        control.OffsetRight = width / 2f;
        control.OffsetBottom = height / 2f;
    }

    private static void SetFullRect(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }
}
