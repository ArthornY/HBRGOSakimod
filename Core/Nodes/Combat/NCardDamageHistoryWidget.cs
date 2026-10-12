using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NCardDamageHistoryWidget : Control
{
    private const float WidgetWidth = 330f;
    private const float WidgetHeight = 104f;
    private const float ViewportPadding = 12f;
    private const float DragThreshold = 5f;
    private static Vector2? _savedPositionRatio;
    private readonly CardDamageDisplayState _displayState = new();

    private Player _player;
    private CardDamageHistoryEntry _latest;
    private Label _damageLabel;
    private Label _cardLabel;
    private Label _stateLabel;
    private decimal _displayDamage = -1m;
    private bool _displayComplete;
    private bool _isPointerDown;
    private bool _hasDragged;
    private Vector2 _pointerDownPosition;
    private Vector2 _dragPointerOffset;

    public override void _Ready()
    {
        AnchorLeft = AnchorRight = 1f;
        AnchorTop = AnchorBottom = 0f;
        OffsetLeft = -380f;
        OffsetRight = -50f;
        OffsetTop = 430f;
        OffsetBottom = OffsetTop + WidgetHeight;
        CustomMinimumSize = new Vector2(WidgetWidth, WidgetHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Modulate = new Color(1f, 1f, 1f, 0f);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ZIndex = 30;
        BuildUi();
        RestoreSavedPosition();
        CardDamageHistoryHelper.HistoryChanged += OnHistoryChanged;
        CardDamageHistoryHelper.DamageUpdated += OnDamageUpdated;
        Refresh();
    }

    public override void _ExitTree()
    {
        CardDamageHistoryHelper.HistoryChanged -= OnHistoryChanged;
        CardDamageHistoryHelper.DamageUpdated -= OnDamageUpdated;
        _player = null;
        _latest = null;
        _displayState.BindBaseline(null);
        ResetPointer();
    }

    public void BindPlayer(Player player)
    {
        if (ReferenceEquals(_player, player))
        {
            Refresh();
            return;
        }
        _player = player;
        var entries = CardDamageHistoryHelper.GetEntries(player);
        _latest = entries.Count == 0 ? null : entries[entries.Count - 1];
        _displayState.BindBaseline(_latest?.IsComplete == false ? null : _latest);
        ResetPointer();
        Refresh();
    }

    // Default placement follows the existing history's right edge. A position
    // chosen by dragging takes precedence, including in subsequent combats.
    public void PlaceBelow(Control historyWidget)
    {
        if (_savedPositionRatio.HasValue || !GodotObject.IsInstanceValid(historyWidget) ||
            !IsInsideTree() || !historyWidget.IsInsideTree())
            return;
        MoveToClampedPosition(new Vector2(
            historyWidget.GlobalPosition.X + historyWidget.Size.X - Size.X,
            historyWidget.GlobalPosition.Y + historyWidget.Size.Y + 10f));
    }

    public override void _Process(double delta)
    {
        if (_player == null || _damageLabel == null) return;
        // The ledger is indexed: this reads only Count and the last entry.
        // Multi-hit chains never rebuild or sum the historical list.
        var entries = CardDamageHistoryHelper.GetEntries(_player);
        CardDamageHistoryEntry latest = entries.Count > 0 ? entries[entries.Count - 1] : null;
        if (!ReferenceEquals(latest, _latest)) Refresh();
        else if (_latest != null &&
            (_latest.TotalDamage != _displayDamage || _latest.IsComplete != _displayComplete))
            UpdateLatest();
        _displayState.Tick(delta);
        ApplyVisibility();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left } left)
        {
            if (left.Pressed)
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
                    MoveToClampedPosition(GetGlobalMousePosition() - _dragPointerOffset);
                    SavePosition();
                }
                else OpenHistory();
                _hasDragged = false;
                MouseDefaultCursorShape = CursorShape.PointingHand;
            }
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseMotion && _isPointerDown)
        {
            if (!_hasDragged && GetGlobalMousePosition().DistanceTo(_pointerDownPosition) >= DragThreshold)
            {
                _hasDragged = true;
                MouseDefaultCursorShape = CursorShape.Move;
            }
            if (_hasDragged) MoveToClampedPosition(GetGlobalMousePosition() - _dragPointerOffset);
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        {
            OpenHistory();
            AcceptEvent();
        }
    }

    private void BuildUi()
    {
        // Source UIBattleDamageTotalLabel: 92x96 dot, 32px effect line,
        // pink TOTAL (32px), white amount (68px) with its pink underlay.
        // Retain those proportions at half scale for the combat sidebar.
        AddChild(HbrCardDamageHistoryVisuals.Sprite("DamageDot", "DotTotalDamage", new(8f, 25f), new(46f, 48f)));
        AddChild(HbrCardDamageHistoryVisuals.Sprite("DamageLine", "EffectLineTotalDamage", new(42f, 41f), new(WidgetWidth - 52f, 16f)));
        Label total = HbrCardDamageHistoryVisuals.Label("TOTAL", 16, Colors.White, HbrCardDamageHistoryVisuals.CaptionFont);
        total.Position = new Vector2(24f, 0f);
        total.Size = new Vector2(100f, 24f);
        AddChild(total);

        _stateLabel = HbrCardDamageHistoryVisuals.Label(string.Empty, 14, new Color("FFCCF5"));
        _stateLabel.Position = new Vector2(104f, 2f);
        _stateLabel.Size = new Vector2(WidgetWidth - 116f, 22f);
        _stateLabel.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_stateLabel);

        _damageLabel = HbrCardDamageHistoryVisuals.Label("0", 34, Colors.White, HbrCardDamageHistoryVisuals.NumberFont);
        _damageLabel.Position = new Vector2(24f, 23f);
        _damageLabel.Size = new Vector2(WidgetWidth - 38f, 52f);
        AddChild(_damageLabel);
        _cardLabel = HbrCardDamageHistoryVisuals.Label(string.Empty, 16, new Color("FFF1FA"));
        _cardLabel.Position = new Vector2(24f, 78f);
        _cardLabel.Size = new Vector2(WidgetWidth - 38f, 24f);
        _cardLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        AddChild(_cardLabel);
    }

    private void OnHistoryChanged(Player player)
    {
        if (ReferenceEquals(player, _player)) Refresh();
    }

    private void OnDamageUpdated(Player player, CardDamageHistoryEntry entry)
    {
        if (!ReferenceEquals(player, _player) || _damageLabel == null) return;
        var entries = CardDamageHistoryHelper.GetEntries(_player);
        if (entries.Count == 0 || !ReferenceEquals(entry, entries[entries.Count - 1])) return;
        // A late receipt from an older root cannot replace the newer card.
        _displayState.UpdateDamage(entry);
        if (!ReferenceEquals(entry, _latest)) Refresh();
        else UpdateLatest();
    }

    private void Refresh()
    {
        if (_damageLabel == null) return;
        var entries = CardDamageHistoryHelper.GetEntries(_player);
        CardDamageHistoryEntry latest = entries.Count == 0 ? null : entries[entries.Count - 1];
        if (!ReferenceEquals(_latest, latest)) ResetPointer();
        _latest = latest;
        _displayDamage = -1m;
        UpdateLatest();
    }

    private void UpdateLatest()
    {
        _displayDamage = _latest?.TotalDamage ?? 0m;
        _displayComplete = _latest?.IsComplete ?? true;
        HbrCardDamageHistoryVisuals.SetDamageText(_damageLabel, FormatDamage(_displayDamage), 34);
        _cardLabel.Text = _latest?.CardName ?? "本场尚未打出卡牌";
        _stateLabel.Text = _latest == null ? "每张牌总伤害" :
            _displayComplete ? $"第 {_latest.Sequence} 次打牌" : "结算中";
        TooltipText = _latest == null
            ? "每张牌总伤害\n左键查看完整历史，拖动可移动。"
            : $"{_latest.CardName}\n总伤害：{FormatDamage(_displayDamage)}{(_displayComplete ? string.Empty : "（结算中）")}\n左键查看完整历史，拖动可移动。";
        _displayState.Observe(_latest, _displayComplete);
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        bool show = _player != null && _displayState.Visible;
        if (!show) ResetPointer();
        Modulate = new Color(1f, 1f, 1f, show ? _displayState.Alpha : 0f);
        MouseFilter = show ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Visible = show;
    }

    private void ResetPointer()
    {
        _isPointerDown = false;
        _hasDragged = false;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    private void OpenHistory()
    {
        if (_player != null) NCardDamageHistoryScreen.Show(_player);
    }

    internal static string FormatDamage(decimal damage) =>
        CardDamageNumberFormatter.Format(damage);

    private void RestoreSavedPosition()
    {
        if (!_savedPositionRatio.HasValue) return;
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        Vector2 range = maximum - minimum;
        Vector2 ratio = _savedPositionRatio.Value.Clamp(Vector2.Zero, Vector2.One);
        MoveToClampedPosition(minimum + new Vector2(range.X * ratio.X, range.Y * ratio.Y));
    }

    private void SavePosition()
    {
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        Vector2 range = maximum - minimum;
        _savedPositionRatio = new Vector2(
            range.X <= 0f ? 0f : (GlobalPosition.X - minimum.X) / range.X,
            range.Y <= 0f ? 0f : (GlobalPosition.Y - minimum.Y) / range.Y);
    }

    private void MoveToClampedPosition(Vector2 position)
    {
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        GlobalPosition = position.Clamp(minimum, maximum);
    }

    private void GetPositionBounds(out Vector2 minimum, out Vector2 maximum)
    {
        Vector2 viewport = GetViewportRect().Size;
        minimum = new Vector2(ViewportPadding, ViewportPadding);
        maximum = new Vector2(Math.Max(minimum.X, viewport.X - Size.X - ViewportPadding),
            Math.Max(minimum.Y, viewport.Y - Size.Y - ViewportPadding));
    }
}

internal static class HbrCardDamageHistoryVisuals
{
    private const string Assets = "res://images/ui/card_damage_history/original/";
    private static FontFile _numbers;
    private static FontFile _caption;
    internal static Font NumberFont => _numbers ??= LoadFont("hbr_total_damage_numbers");
    internal static Font CaptionFont => _caption ??= LoadFont("hbr_total_damage_caption");

    private static FontFile LoadFont(string name)
    {
        FontFile font = ResourceLoader.Load<FontFile>(Assets + name + ".fnt");
        font.FixedSizeScaleMode = TextServer.FixedSizeScaleMode.Enabled;
        font.ModulateColorGlyphs = true;
        return font;
    }

    internal static TextureRect Sprite(string name, string asset, Vector2 position, Vector2 size) => new()
    {
        Name = name,
        Position = position,
        Size = size,
        Texture = ResourceLoader.Load<Texture2D>(Assets + asset + ".png"),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    internal static Label Label(string text, int fontSize, Color color, Font font = null)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font ?? ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeConstantOverride("outline_size", font == null ? 2 : 0);
        label.AddThemeColorOverride("font_outline_color", new Color("241325"));
        return label;
    }

    internal static void SetDamageText(Label label, string text, int preferredFontSize)
    {
        Font font = label.GetThemeFont("font");
        float availableWidth = Math.Max(1f, label.Size.X - 6f);
        float measuredWidth = font.GetStringSize(text, fontSize: preferredFontSize).X;
        int fontSize = measuredWidth > availableWidth
            ? Math.Max(1, (int)Math.Floor(preferredFontSize * availableWidth / measuredWidth))
            : preferredFontSize;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.Text = text;
    }
}
