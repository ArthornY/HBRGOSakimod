using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The roll history button, drawn as the HBR battle "AUTO" capsule (ButtonBattleAutoOnOff*, 80px caps)
// from ui-concepts/battle-hud-practical-20261104: idle, hovered and pressed states, greyed out under
// amnesia. Left click opens the history, a drag over 5px moves it, right click also opens it.
public partial class NRollHistoryWidget : Control
{
    private const float ViewportPadding = 12f;
    private const float DragThreshold = 5f;
    private const string IdleTexture = "ButtonBattleAutoOnOffDefault";
    private const string HoverTexture = "ButtonBattleAutoOnOffActive";
    private const string PressedTexture = "ButtonBattleAutoOnOffTempActive";

    private static readonly Color CountColor = new(HbrBattleHudLayout.RollCountColor);
    private static readonly Color DisabledColor = new(HbrBattleHudLayout.DisabledTextColor);
    private static readonly Color TextOutline = new(0.07f, 0.03f, 0.09f, 0.9f);
    private static Vector2? _savedPositionRatio;

    private SoulOfAnchor _history;
    private NinePatchRect _cap;
    private Label _titleLabel;
    private Label _countLabel;
    private Tween _bumpTween;
    private bool _isPointerDown;
    private bool _hasDragged;
    private bool _isHovered;
    private bool _isHidden;
    private int _shownCount = -1;
    private Vector2 _pointerDownPosition;
    private Vector2 _dragPointerOffset;

    public override void _Ready()
    {
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -HbrBattleHudLayout.RollFromRight;
        OffsetRight = OffsetLeft + HbrBattleHudLayout.RollWidth;
        OffsetTop = HbrBattleHudLayout.RollTop;
        OffsetBottom = OffsetTop + HbrBattleHudLayout.RollHeight;
        CustomMinimumSize = new Vector2(HbrBattleHudLayout.RollWidth, HbrBattleHudLayout.RollHeight);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ZIndex = 30;

        BuildUi();
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        RestoreSavedPosition();
        Refresh();
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
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

            RefreshCapsule();
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
                RefreshCapsule();
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
        _shownCount = -1;
        if (_history != null)
        {
            _history.DisplayAmountChanged += Refresh;
            _history.HistoryChanged += Refresh;
        }

        Refresh();
    }

    private void BuildUi()
    {
        // NinePatchRect keeps its caps at source size, so the 84px-tall capsule is laid out at
        // source scale and shrunk to 64px, like the preview's border-image (80px caps drawn at 61px).
        float scale = HbrBattleHudLayout.RollScale;
        _cap = new NinePatchRect
        {
            Name = "Capsule",
            Size = new Vector2(HbrBattleHudLayout.RollWidth / scale, HbrBattleHudLayout.RollSourceHeight),
            Scale = Vector2.One * scale,
            Texture = HbrBattleHudArt.Texture(IdleTexture),
            PatchMarginLeft = HbrBattleHudLayout.RollSourceCap,
            PatchMarginRight = HbrBattleHudLayout.RollSourceCap,
            TextureFilter = HbrBattleHudArt.MipmapFilter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_cap);

        _titleLabel = HbrBattleHudArt.Label("RollHistoryTitle", HbrBattleHudLayout.RollTitle,
            HbrBattleHudLayout.RollTitleSize, Colors.White, 2);
        HbrBattleHudArt.Outline(_titleLabel, TextOutline, 6);
        AddChild(_titleLabel);
        _countLabel = HbrBattleHudArt.Label("RollHistoryCount", "0", HbrBattleHudLayout.RollCountSize, CountColor);
        HbrBattleHudArt.Outline(_countLabel, TextOutline, 6);
        AddChild(_countLabel);
    }

    private void Refresh()
    {
        Visible = _history != null;
        if (_history == null || _countLabel == null)
        {
            return;
        }

        _isHidden = AmnesiaRollVisibilityHelper.IsRollHidden(_history.Owner);
        if (_isHidden)
        {
            _countLabel.Text = HbrBattleHudLayout.RollHidden;
            TooltipText = "失忆\n无法查看掷骰出目。";
        }
        else
        {
            int count = _history.RollHistory.Count;
            bool grew = _shownCount >= 0 && count > _shownCount;
            _shownCount = count;
            _countLabel.Text = count.ToString();
            TooltipText =
                $"出目历史\n本场共 {count} 次掷骰。\n左键点击查看，拖动可移动；右键也可查看。";
            if (grew) Bump();
        }

        LayoutText();
        RefreshCapsule();
    }

    // Title and count are centred together, 12px apart, 2px above the middle.
    private void LayoutText()
    {
        float width = TextWidth(_titleLabel, HbrBattleHudLayout.RollTitleSize);
        float countWidth = Math.Max(TextWidth(_countLabel, HbrBattleHudLayout.RollCountSize), 14f);
        float left = (HbrBattleHudLayout.RollWidth - width - HbrBattleHudLayout.RollTextGap - countWidth) / 2f;
        float height = HbrBattleHudLayout.RollHeight - 2f;
        _titleLabel.Position = new Vector2(left, 0f);
        _titleLabel.Size = new Vector2(width, height);
        _countLabel.Position = new Vector2(left + width + HbrBattleHudLayout.RollTextGap, 0f);
        _countLabel.Size = new Vector2(countWidth, height);
        _countLabel.PivotOffset = _countLabel.Size / 2f;
    }

    private static float TextWidth(Label label, int size) =>
        label.GetThemeFont("font").GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X + 2f;

    private void RefreshCapsule()
    {
        if (_cap == null) return;
        bool pressed = _isPointerDown && !_hasDragged;
        string texture = pressed ? PressedTexture : _isHovered ? HoverTexture : IdleTexture;
        _cap.Texture = HbrBattleHudArt.Texture(_isHidden ? texture + "_amnesia" : texture);
        Color text = _isHidden ? DisabledColor : Colors.White;
        _titleLabel.AddThemeColorOverride("font_color", text);
        _countLabel.AddThemeColorOverride("font_color", _isHidden ? DisabledColor :
            pressed || _isHovered ? Colors.White : CountColor);
    }

    private void Bump()
    {
        _countLabel.Scale = Vector2.One * HbrBattleHudLayout.CountBumpScale;
        _bumpTween?.Kill();
        _bumpTween = CreateTween();
        _bumpTween.TweenProperty(_countLabel, "scale", Vector2.One, 0.5)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private void OnMouseEntered()
    {
        _isHovered = true;
        RefreshCapsule();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        RefreshCapsule();
    }

    private void OpenHistory()
    {
        Refresh();
        if (_history != null &&
            !AmnesiaRollVisibilityHelper.IsRollHidden(_history.Owner))
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
}
