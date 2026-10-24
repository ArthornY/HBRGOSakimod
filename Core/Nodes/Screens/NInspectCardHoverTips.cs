using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The native inspect flow adds columns when tips are tall. Reserve the card and
// its navigation controls; overflowing sets keep their native tips in a sidebar.
public partial class NInspectCardHoverTips : Node
{
    private static readonly AccessTools.FieldRef<NInspectCardScreen, float> LeftButtonX =
        AccessTools.FieldRefAccess<NInspectCardScreen, float>("_leftButtonX");
    private static readonly AccessTools.FieldRef<NInspectCardScreen, float> RightButtonX =
        AccessTools.FieldRefAccess<NInspectCardScreen, float>("_rightButtonX");
    private static readonly AccessTools.FieldRef<NInspectCardScreen, Tween> CardTween =
        AccessTools.FieldRefAccess<NInspectCardScreen, Tween>("_cardTween");
    private static readonly AccessTools.FieldRef<NInspectCardScreen, Tween> OpenTween =
        AccessTools.FieldRefAccess<NInspectCardScreen, Tween>("_openTween");
    internal NInspectCardScreen Inspect { get; set; }
    internal NCard Card { get; set; }
    internal Control CardAnchor { get; set; }
    private NHoverTipSet _tips;
    private Control _text;
    private Control _cards;
    private Viewport _viewport;
    private readonly List<Control> _entries = new();
    private ScrollContainer _scroll;
    private VBoxContainer _column;
    private Tween _cardTween;
    private Tween _openTween;
    private bool _queued;

    public override void _Ready()
    {
        _tips = GetParent<NHoverTipSet>();
        _text = _tips.GetNodeOrNull<Control>("textHoverTipContainer");
        _cards = _tips.GetNodeOrNull<Control>("cardHoverTipContainer");
        _viewport = GetViewport();
        _viewport.SizeChanged += QueueLayout;
        if (Usable(Inspect)) Inspect.Resized += QueueLayout;
        if (Usable(Card)) Card.Resized += QueueLayout;
        if (Usable(_text)) _text.Resized += QueueLayout;
        if (Usable(_cards)) _cards.Resized += QueueLayout;
        QueueLayout();
    }

    private void QueueLayout()
    {
        if (_queued || !IsInsideTree() || IsQueuedForDeletion()) return;
        _queued = true;
        Callable.From(RefreshLayout).CallDeferred();
    }

    private void RefreshLayout()
    {
        _queued = false;
        if (!Usable(_tips) || !Usable(Card) || !Usable(Inspect) || !_tips.IsVisibleInTree()) return;
        TrackOpeningAnimations();
        if (_scroll == null)
        {
            _entries.Clear();
            AddEntries(_text);
            AddEntries(_cards);
            if (_entries.Count == 0) return;
        }

        Transform2D inverse = _tips.GetGlobalTransformWithCanvas().AffineInverse();
        Rect2 viewport = inverse * _tips.GetViewportRect();
        Rect2 card = Bounds(Card, inverse);
        if (!card.HasArea()) return;
        Rect2 reserved = card;
        // Open animates the card's scale and arrows' positions without Resized.
        // Reserve the stable card anchor and both arrows' final positions now.
        Reserve(CardAnchor, inverse, ref reserved);
        ReserveArrow(Inspect.GetNodeOrNull<Control>("%LeftArrow"), LeftButtonX(Inspect), inverse, ref reserved);
        ReserveArrow(Inspect.GetNodeOrNull<Control>("%RightArrow"), RightButtonX(Inspect), inverse, ref reserved);
        Reserve(Inspect.GetNodeOrNull<Control>("%Upgrade"), inverse, ref reserved);
        Reserve(Inspect.GetNodeOrNull<Control>("MemoryGardenCompendiumSkillsButton"), inverse, ref reserved);
        if (_scroll == null && !NeedsSidebar(viewport, reserved, inverse)) return;

        float width = 360f;
        foreach (Control entry in _entries)
            if (Usable(entry)) width = Math.Max(width, entry.GetCombinedMinimumSize().X);
        // Keep normal tips below the top bar and leave room for the scrollbar,
        // so its appearance cannot clip the native text's minimum width.
        float top = Math.Max(viewport.Position.Y, reserved.Position.Y);
        Rect2 readingArea = new(new Vector2(viewport.Position.X, top),
            new Vector2(viewport.Size.X, viewport.End.Y - top));
        InspectCardHoverTipPlacement placement = InspectCardHoverTipLayoutHelper.ComputePlacement(
            readingArea, reserved, width + 20f);
        if (!placement.Bounds.HasArea()) return;
        if (_scroll == null) CreateSidebar(width);
        _scroll.Position = placement.Bounds.Position;
        _scroll.Scale = Vector2.One * placement.Scale;
        _scroll.Size = placement.Bounds.Size / placement.Scale;
    }

    private void AddEntries(Control parent)
    {
        if (!Usable(parent)) return;
        foreach (Node node in parent.GetChildren())
            if (node is Control entry && Usable(entry) && entry.Visible) _entries.Add(entry);
    }

    private bool NeedsSidebar(Rect2 viewport, Rect2 reserved, Transform2D inverse)
    {
        foreach (Control entry in _entries)
        {
            Rect2 bounds = Bounds(entry, inverse);
            if (bounds.HasArea() && (bounds.Intersects(reserved.Grow(12f)) || !viewport.Encloses(bounds)))
                return true;
        }
        return false;
    }

    private void CreateSidebar(float width)
    {
        _scroll = new ScrollContainer
        {
            Name = "InspectCardHoverTipScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = Control.MouseFilterEnum.Pass,
            MouseForcePassScrollEvents = false,
            FocusMode = Control.FocusModeEnum.None
        };
        _column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(width, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _column.AddThemeConstantOverride("separation", 12);
        _tips.AddChild(_scroll);
        _scroll.AddChild(_column);
        _scroll.GetVScrollBar().FocusMode = Control.FocusModeEnum.None;
        foreach (Control entry in _entries)
            if (Usable(entry)) entry.Reparent(_column, false);
        if (Usable(_text)) _text.Hide();
        if (Usable(_cards)) _cards.Hide();
        // Native MarginContainer text tips propagate fit-content height changes
        // themselves. Only resize events schedule placement; no per-frame scan.
        _column.MinimumSizeChanged += QueueLayout;
    }

    private static void Reserve(Control control, Transform2D inverse, ref Rect2 bounds)
    {
        if (!Usable(control) || !control.IsVisibleInTree()) return;
        Rect2 rect = Bounds(control, inverse);
        if (rect.HasArea()) bounds = bounds.Merge(rect);
    }

    private static void ReserveArrow(Control arrow, float finalX, Transform2D inverse, ref Rect2 bounds)
    {
        if (!Usable(arrow) || !arrow.IsVisibleInTree()) return;
        Rect2 current = Bounds(arrow, inverse);
        Transform2D parentToViewport = arrow.GetGlobalTransformWithCanvas() * arrow.GetTransform().AffineInverse();
        Vector2 displacement = inverse.BasisXform(parentToViewport.BasisXform(new Vector2(finalX - arrow.Position.X, 0f)));
        Rect2 final = new(current.Position + displacement, current.Size);
        bounds = bounds.Merge(current).Merge(final);
    }

    private void TrackOpeningAnimations()
    {
        // The native Open method creates these after UpdateCardDisplay. This
        // deferred callback sees them, and retains the exact references even
        // when Close later replaces the screen's tween fields.
        if (_cardTween == null && CardTween(Inspect) is { } card && GodotObject.IsInstanceValid(card))
        {
            _cardTween = card;
            card.Finished += QueueLayout;
        }
        if (_openTween == null && OpenTween(Inspect) is { } opening && GodotObject.IsInstanceValid(opening))
        {
            _openTween = opening;
            opening.Finished += QueueLayout;
        }
    }

    private static Rect2 Bounds(Control control, Transform2D inverse) =>
        inverse * (control.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, control.Size));

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_viewport)) _viewport.SizeChanged -= QueueLayout;
        if (GodotObject.IsInstanceValid(Inspect)) Inspect.Resized -= QueueLayout;
        if (GodotObject.IsInstanceValid(Card)) Card.Resized -= QueueLayout;
        if (GodotObject.IsInstanceValid(_text)) _text.Resized -= QueueLayout;
        if (GodotObject.IsInstanceValid(_cards)) _cards.Resized -= QueueLayout;
        if (GodotObject.IsInstanceValid(_column)) _column.MinimumSizeChanged -= QueueLayout;
        if (GodotObject.IsInstanceValid(_cardTween)) _cardTween.Finished -= QueueLayout;
        if (GodotObject.IsInstanceValid(_openTween)) _openTween.Finished -= QueueLayout;
        _entries.Clear();
    }
}
