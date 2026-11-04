using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// Only overflowing hand tips become a scrolling column. Keep the actual native
// entries and never cover the hovered card's transformed body/description.
public partial class NHandCardHoverTips : Node
{
    private static readonly AccessTools.FieldRef<NHoverTipSet, bool> FollowOwner =
        AccessTools.FieldRefAccess<NHoverTipSet, bool>("_followOwner");
    private static readonly AccessTools.FieldRef<NCardHolder, bool> IsHovered =
        AccessTools.FieldRefAccess<NCardHolder, bool>("_isHovered");
    private static readonly AccessTools.FieldRef<NPlayerHand, int> DraggedHolderIndex =
        AccessTools.FieldRefAccess<NPlayerHand, int>("_draggedHolderIndex");
    internal NHandCardHolder Holder { get; set; }
    private NHoverTipSet _tips;
    private Control _text;
    private Control _cards;
    private Viewport _viewport;
    private readonly List<Control> _entries = new();
    private ScrollContainer _scroll;
    private VBoxContainer _column;
    private float _nativeWidth;
    private float _width;
    private bool _dirty = true;
    private Rect2 _lastViewport;
    private Rect2 _lastCard;

    public override void _Ready()
    {
        _tips = GetParent<NHoverTipSet>();
        _text = _tips.GetNodeOrNull<Control>("textHoverTipContainer");
        _cards = _tips.GetNodeOrNull<Control>("cardHoverTipContainer");
        _viewport = GetViewport();
        _viewport.SizeChanged += Invalidate;
        if (Usable(_text)) _text.Resized += Invalidate;
        if (Usable(_cards)) _cards.Resized += Invalidate;
        // Hand animations change position, scale and rotation without Resized.
        // Compare geometry before drawing; only changes trigger actual layout.
        RenderingServer.FramePreDraw += RefreshLayout;
        Callable.From(RefreshLayout).CallDeferred();
    }

    private void Invalidate() => _dirty = true;

    private void RefreshLayout()
    {
        if (!Usable(_tips) || !Usable(Holder) || !_tips.IsVisibleInTree()) return;
        NCard card = Holder.CardNode;
        if (!Usable(card) || !card.IsVisibleInTree()) return;
        if (_scroll != null) FollowOwner(_tips) = false;
        Transform2D inverse = _tips.GetGlobalTransformWithCanvas().AffineInverse();
        Rect2 viewport = inverse * _tips.GetViewportRect();
        Rect2 reserved = Bounds(card, inverse);
        if (Usable(card.Body)) reserved = reserved.Merge(Bounds(card.Body, inverse));
        if (!reserved.HasArea() || !_dirty && viewport == _lastViewport && reserved == _lastCard) return;
        _dirty = false;
        _lastViewport = viewport;
        _lastCard = reserved;

        if (_scroll == null)
        {
            _entries.Clear();
            AddEntries(_text);
            AddEntries(_cards);
            if (_entries.Count == 0) { _dirty = true; return; }
            if (!NeedsSidebar(viewport, reserved, inverse)) return;
            _nativeWidth = 360f;
            foreach (Control entry in _entries)
                _nativeWidth = Math.Max(_nativeWidth, Math.Max(entry.Size.X, entry.GetCombinedMinimumSize().X));
        }
        // Labels and card previews can finish measuring after native creation.
        // Only minimum sizes remain intrinsic after VBox has assigned a width;
        // feeding assigned Size back here would grow the column every frame.
        float width = _nativeWidth;
        foreach (Control entry in _entries)
            if (Usable(entry)) width = Math.Max(width, entry.GetCombinedMinimumSize().X);
        if (_column != null && width != _width) _column.CustomMinimumSize = new Vector2(width, 0f);
        _width = width;
        HandCardHoverTipPlacement placement = HandCardHoverTipLayoutHelper.ComputePlacement(viewport, reserved, _width + 20f);
        if (!placement.Bounds.HasArea() || placement.Scale <= 0f) { _dirty = true; return; }
        if (_scroll == null) CreateSidebar();
        FollowOwner(_tips) = false;
        _scroll.Position = placement.Bounds.Position;
        _scroll.Scale = Vector2.One * placement.Scale;
        _scroll.Size = placement.Bounds.Size / placement.Scale;
    }

    private void AddEntries(Control parent)
    {
        if (!Usable(parent)) return;
        foreach (Node child in parent.GetChildren())
            if (child is Control entry && Usable(entry) && entry.Visible) _entries.Add(entry);
    }

    private bool NeedsSidebar(Rect2 viewport, Rect2 reserved, Transform2D inverse)
    {
        Rect2 safe = viewport.Grow(-16f);
        foreach (Control entry in _entries)
        {
            Rect2 bounds = Bounds(entry, inverse);
            if (bounds.HasArea() && (bounds.Intersects(reserved.Grow(12f)) || !safe.Encloses(bounds))) return true;
        }
        return false;
    }

    private void CreateSidebar()
    {
        _scroll = new ScrollContainer
        {
            Name = "HandCardHoverTipScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        };
        _column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(_width, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _column.AddThemeConstantOverride("separation", 12);
        _tips.AddChild(_scroll);
        _scroll.AddChild(_column);
        VScrollBar bar = _scroll.GetVScrollBar();
        bar.FocusMode = Control.FocusModeEnum.None;
        bar.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (Control entry in _entries)
            if (Usable(entry)) entry.Reparent(_column, false);
        if (Usable(_text)) _text.Hide();
        if (Usable(_cards)) _cards.Hide();
        _column.MinimumSizeChanged += Invalidate;
    }

    public override void _Input(InputEvent input)
    {
        if (_scroll == null || !Usable(_tips) || !Usable(Holder) || !_tips.IsVisibleInTree() ||
            input is not InputEventMouseButton { Pressed: true } mouse ||
            mouse.AltPressed || mouse.CtrlPressed || mouse.ShiftPressed || mouse.MetaPressed ||
            !IsHovered(Holder) && !Holder.HasFocus()) return;
        NCombatRoom room = NCombatRoom.Instance;
        if (!Usable(room) || ActiveScreenContext.Instance?.IsCurrent(room) != true) return;
        int direction = mouse.ButtonIndex switch
        {
            MouseButton.WheelUp => -1,
            MouseButton.WheelDown => 1,
            _ => 0
        };
        if (direction == 0 || !Usable(Holder.CardNode)) return;
        for (Node parent = Holder.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is NPlayerHand hand && (hand.InCardPlay || DraggedHolderIndex(hand) >= 0)) return;
        // Leaving the hand card invokes native unhover and removes the tips.
        // Route only its wheel gesture; never intercept clicks or dragging.
        Control body = Usable(Holder.CardNode.Body) ? Holder.CardNode.Body : Holder.CardNode;
        Vector2 localMouse = body.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
        if (!new Rect2(Vector2.Zero, body.Size).HasPoint(localMouse)) return;
        VScrollBar bar = _scroll.GetVScrollBar();
        if (bar.MaxValue - bar.Page <= bar.MinValue + 1d) return;
        int step = (int)Math.Clamp(80d * Math.Abs(mouse.Factor), 16d, 240d);
        _scroll.ScrollVertical += direction * step;
        GetViewport().SetInputAsHandled();
    }

    private static Rect2 Bounds(Control control, Transform2D inverse) =>
        inverse * (control.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, control.Size));

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        if (GodotObject.IsInstanceValid(_viewport)) _viewport.SizeChanged -= Invalidate;
        if (GodotObject.IsInstanceValid(_text)) _text.Resized -= Invalidate;
        if (GodotObject.IsInstanceValid(_cards)) _cards.Resized -= Invalidate;
        if (GodotObject.IsInstanceValid(_column)) _column.MinimumSizeChanged -= Invalidate;
        _entries.Clear();
    }
}
