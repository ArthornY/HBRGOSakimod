using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NSixSenseChoiceScreen : Control, IOverlayScreen
{
    private readonly TaskCompletionSource<int> _completionSource = new();
    private readonly List<Button> _buttons = new();
    private ElectronicSoldierManual _manual;
    private IReadOnlyList<SixSenseAttribute> _options;
    private Tween _fadeTween;

    public NetScreenType ScreenType => NetScreenType.Rewards;

    public bool UseSharedBackstop => true;

    public Control DefaultFocusedControl => (Control)_buttons.FirstOrDefault() ?? this;

    public static NSixSenseChoiceScreen ShowScreen(ElectronicSoldierManual manual, IReadOnlyList<SixSenseAttribute> options)
    {
        NSixSenseChoiceScreen screen = new()
        {
            Name = "SixSenseChoiceScreen",
            _manual = manual,
            _options = options
        };
        NOverlayStack.Instance.Push(screen);
        return screen;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetFullRect(this);
        BuildUi();
    }

    public override void _ExitTree()
    {
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
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2);
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
        PanelContainer panel = new()
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -300f,
            OffsetTop = -170f,
            OffsetRight = 300f,
            OffsetBottom = 170f,
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.08f, 0.1f, 0.14f, 0.92f), new Color(0.52f, 0.72f, 0.95f, 0.85f)));
        AddChild(panel);

        VBoxContainer layout = new()
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 28f,
            OffsetTop = 24f,
            OffsetRight = -28f,
            OffsetBottom = -24f
        };
        layout.AddThemeConstantOverride("separation", 14);
        panel.AddChild(layout);

        Label title = CreateLabel("选择提升一项六感", 30, Colors.White);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(title);

        foreach (SixSenseAttribute option in _options)
        {
            int optionIndex = _buttons.Count;
            int current = _manual.GetSenseAmount(option);
            string text = $"{ElectronicSoldierManual.GetSenseTitle(option)}  {current} -> {Mathf.Min(current + 1, ElectronicSoldierManual.MaxSixSense)}\n{ElectronicSoldierManual.GetSenseShortDescription(option)}";
            Button button = new()
            {
                Text = text,
                CustomMinimumSize = new Vector2(0f, 72f),
                FocusMode = FocusModeEnum.All
            };
            button.AddThemeFontSizeOverride("font_size", 22);
            button.Pressed += () => Select(optionIndex);
            _buttons.Add(button);
            layout.AddChild(button);
        }
    }

    private void Select(int index)
    {
        if (_completionSource.Task.IsCompleted)
        {
            return;
        }

        _completionSource.SetResult(index);
    }

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        Label label = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, fontSize);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, color);
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
            ContentMarginBottom = 18,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 18,
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
}
