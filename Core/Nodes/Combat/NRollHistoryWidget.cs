using System;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NRollHistoryWidget : Control
{
    private const float WidgetWidth = 250f;
    private const float WidgetHeight = 68f;
    private const float ViewportPadding = 12f;
    private const float DragThreshold = 5f;
    private const string IconPath =
        "res://images/relics/soul_of_anchor.png";
    private const string FontPath =
        "res://themes/kreon_bold_glyph_space_one.tres";

    private static Vector2? _savedPositionRatio;

    private SoulOfAnchor _history;
    private Label _countLabel;
    private bool _isPointerDown;
    private bool _hasDragged;
    private Vector2 _pointerDownPosition;
    private Vector2 _dragPointerOffset;

    public override void _Ready()
    {
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -300f;
        OffsetRight = -50f;
        OffsetTop = 350f;
        OffsetBottom = 418f;
        CustomMinimumSize = new Vector2(WidgetWidth, WidgetHeight);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ZIndex = 30;

        BuildUi();
        RestoreSavedPosition();
        Refresh();
    }

    public override void _ExitTree()
    {
        BindHistory(null);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left
            } leftButton)
        {
            if (leftButton.Pressed)
            {
                _isPointerDown = true;
                _hasDragged = false;
                _pointerDownPosition = GetGlobalMousePosition();
                _dragPointerOffset = _pointerDownPosition - GlobalPosition;
            }
            else if (_isPointerDown)
            {
                _isPointerDown = false;
                if (_hasDragged)
                {
                    MoveToClampedPosition(
                        GetGlobalMousePosition() - _dragPointerOffset);
                    SavePosition();
                }
                else
                {
                    OpenHistory();
                }

                _hasDragged = false;
                MouseDefaultCursorShape = CursorShape.PointingHand;
            }

            AcceptEvent();
            return;
        }

        if (inputEvent is InputEventMouseMotion && _isPointerDown)
        {
            if (!_hasDragged &&
                GetGlobalMousePosition().DistanceTo(_pointerDownPosition) >=
                DragThreshold)
            {
                _hasDragged = true;
                MouseDefaultCursorShape = CursorShape.Move;
            }

            if (_hasDragged)
            {
                MoveToClampedPosition(
                    GetGlobalMousePosition() - _dragPointerOffset);
            }

            AcceptEvent();
            return;
        }

        if (inputEvent is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right,
                Pressed: false
            })
        {
            OpenHistory();
            AcceptEvent();
        }
    }

    public void BindHistory(SoulOfAnchor history)
    {
        if (ReferenceEquals(_history, history))
        {
            Refresh();
            return;
        }

        if (_history != null)
        {
            _history.DisplayAmountChanged -= Refresh;
            _history.HistoryChanged -= Refresh;
        }

        _history = history;
        if (_history != null)
        {
            _history.DisplayAmountChanged += Refresh;
            _history.HistoryChanged += Refresh;
        }

        Refresh();
    }

    private void BuildUi()
    {
        PanelContainer panel = new()
        {
            Name = "RollHistoryPanel",
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxFlat
            {
                BgColor = new Color(0.035f, 0.055f, 0.085f, 0.94f),
                BorderColor = new Color(0.38f, 0.72f, 0.92f, 0.95f),
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                ContentMarginBottom = 8,
                ContentMarginLeft = 10,
                ContentMarginRight = 12,
                ContentMarginTop = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8
            });
        SetFullRect(panel);
        AddChild(panel);

        HBoxContainer row = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride(
            ThemeConstants.BoxContainer.Separation,
            10);
        panel.AddChild(row);

        TextureRect icon = new()
        {
            Name = "RollHistoryIcon",
            CustomMinimumSize = new Vector2(50f, 50f),
            Texture = ResourceLoader.Load<Texture2D>(IconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(icon);

        VBoxContainer textColumn = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textColumn.AddThemeConstantOverride(
            ThemeConstants.BoxContainer.Separation,
            0);
        row.AddChild(textColumn);

        Label titleLabel = CreateLabel(
            "出目历史",
            21,
            new Color("F4F7FFFF"));
        titleLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        textColumn.AddChild(titleLabel);

        _countLabel = CreateLabel(
            "本场记录：0",
            16,
            new Color("9EDCF7FF"));
        _countLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        textColumn.AddChild(_countLabel);
    }

    private static Label CreateLabel(
        string text,
        int fontSize,
        Color color)
    {
        Label label = new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride(
            ThemeConstants.Label.Font,
            ResourceLoader.Load<Font>(FontPath));
        label.AddThemeFontSizeOverride(
            ThemeConstants.Label.FontSize,
            fontSize);
        label.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            color);
        label.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            new Color("0C1724E8"));
        label.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            5);
        return label;
    }

    private void Refresh()
    {
        Visible = _history != null;
        if (_history == null || _countLabel == null)
        {
            return;
        }

        int count = _history.RollHistory.Count;
        _countLabel.Text = $"本场记录：{count}";
        TooltipText =
            $"出目历史\n本场共 {count} 次掷骰。\n左键点击查看，拖动可移动；右键也可查看。";
    }

    private void OpenHistory()
    {
        if (_history != null)
        {
            NSoulRollHistoryScreen.Show(_history);
        }
    }

    private void RestoreSavedPosition()
    {
        if (!_savedPositionRatio.HasValue)
        {
            return;
        }

        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        Vector2 range = maximum - minimum;
        Vector2 ratio =
            _savedPositionRatio.Value.Clamp(Vector2.Zero, Vector2.One);
        MoveToClampedPosition(
            minimum + new Vector2(range.X * ratio.X, range.Y * ratio.Y));
    }

    private void SavePosition()
    {
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        Vector2 range = maximum - minimum;
        _savedPositionRatio = new Vector2(
            range.X <= 0f
                ? 0f
                : (GlobalPosition.X - minimum.X) / range.X,
            range.Y <= 0f
                ? 0f
                : (GlobalPosition.Y - minimum.Y) / range.Y);
    }

    private void MoveToClampedPosition(Vector2 position)
    {
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        GlobalPosition = position.Clamp(minimum, maximum);
    }

    private void GetPositionBounds(
        out Vector2 minimum,
        out Vector2 maximum)
    {
        Vector2 viewportSize = GetViewportRect().Size;
        minimum = new Vector2(ViewportPadding, ViewportPadding);
        maximum = new Vector2(
            Math.Max(
                minimum.X,
                viewportSize.X - Size.X - ViewportPadding),
            Math.Max(
                minimum.Y,
                viewportSize.Y - Size.Y - ViewportPadding));
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
