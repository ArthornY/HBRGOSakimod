using System;
using Godot;
using HBRGOSakiMod.Core.Config;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NRightTopBarInfoLayout : Node
{
    internal const string WidgetName = "HBRGORightTopBarInfoLayout";
    private const float GroupGap = 14f;
    private const float PortraitGap = 8f;
    private NTopBar _bar;
    private Control _gold;
    private Control _rooms;
    private Control _timer;
    private Control _goldPopup;
    private Container _right;
    private Control _space;
    private bool _enabled = true;
    private float _originalPopupX;
    private bool _goldTopLevel;
    private bool _roomsTopLevel;
    private int _goldZIndex;
    private int _roomsZIndex;
    private bool _timerTopLevel;
    private int _timerZIndex;
    private Vector2 _timerScale;
    private float _timerRotation;
    private Vector2 _timerPosition;

    internal static void Install(NGlobalUi ui)
    {
        if (!Usable(ui?.TopBar) || ui.TopBar.GetNodeOrNull<NRightTopBarInfoLayout>(WidgetName) != null) return;
        var layout = new NRightTopBarInfoLayout { Name = WidgetName };
        if (!layout.Bind(ui.TopBar)) return;
        ui.TopBar.AddChild(layout);
    }

    private bool Bind(NTopBar bar)
    {
        _bar = bar;
        _gold = bar.Gold;
        _rooms = bar.GetNodeOrNull<Control>("LeftAlignedStuff/RoomIcons");
        _timer = bar.Timer;
        _right = bar.Map?.GetParent() as Container;
        _goldPopup = bar.GetNodeOrNull<Control>("%GoldPopup");
        if (!Usable(_gold) || !Usable(_rooms) || !Usable(_right)) return false;
        _originalPopupX = Usable(_goldPopup) ? _goldPopup.Position.X : 0f;
        _goldTopLevel = _gold.TopLevel;
        _roomsTopLevel = _rooms.TopLevel;
        _goldZIndex = _gold.ZIndex;
        _roomsZIndex = _rooms.ZIndex;
        if (Usable(_timer))
        {
            _timerTopLevel = _timer.TopLevel;
            _timerZIndex = _timer.ZIndex;
            _timerScale = _timer.Scale;
            _timerRotation = _timer.Rotation;
            _timerPosition = _timer.Position;
            _timer.TopLevel = true;
            _timer.ZIndex = bar.ZIndex;
        }
        // Preserve the native event subscriptions and room icon descendants.
        // A real slot in the right HBox reserves this group's changing width.
        _gold.TopLevel = true;
        _rooms.TopLevel = true;
        _gold.ZIndex = bar.ZIndex;
        _rooms.ZIndex = bar.ZIndex;
        _space = new Control
        {
            Name = "HBRGORightTopBarInfoSpace",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0f, 80f)
        };
        _right.AddChild(_space);
        Control portrait = _right.GetNodeOrNull<Control>("CharacterStatusButton") ?? bar.Map;
        _right.MoveChild(_space, portrait.GetIndex());
        if (_gold.GetParent() is Container parent)
        {
            parent.UpdateMinimumSize();
            parent.QueueSort();
        }
        return true;
    }

    public override void _Ready()
    {
        RenderingServer.FramePreDraw += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        if (Usable(_gold)) { _gold.TopLevel = _goldTopLevel; _gold.ZIndex = _goldZIndex; }
        if (Usable(_rooms)) { _rooms.TopLevel = _roomsTopLevel; _rooms.ZIndex = _roomsZIndex; }
        RestoreTimer();
        if (Usable(_space)) _space.QueueFree();
        if (Usable(_right))
        {
            _right.UpdateMinimumSize();
            _right.QueueSort();
        }
        if (Usable(_gold) && _gold.GetParent() is Container parent)
        {
            parent.UpdateMinimumSize();
            parent.QueueSort();
        }
    }

    private void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        _space.Visible = enabled;
        _gold.TopLevel = enabled || _goldTopLevel;
        _rooms.TopLevel = enabled || _roomsTopLevel;
        _gold.ZIndex = enabled ? _bar.ZIndex : _goldZIndex;
        _rooms.ZIndex = enabled ? _bar.ZIndex : _roomsZIndex;
        if (enabled && Usable(_timer))
        {
            _timer.TopLevel = true;
            _timer.ZIndex = _bar.ZIndex;
        }
        else if (!enabled) RestoreTimer();
        if (!enabled && Usable(_goldPopup))
            _goldPopup.Position = new Vector2(_originalPopupX, _goldPopup.Position.Y);
        if (_gold.GetParent() is Container parent)
        {
            parent.UpdateMinimumSize();
            parent.QueueSort();
        }
        _right.UpdateMinimumSize();
        _right.QueueSort();
    }

    private void RefreshLayout()
    {
        if (!Usable(_gold) || !Usable(_rooms) || !Usable(_space) || !_space.IsInsideTree()) return;
        SetEnabled(HBRGOSakiConfig.ModUI);
        if (!_enabled) return;
        Vector2 goldSize = _gold.GetCombinedMinimumSize();
        Vector2 roomSize = _rooms.GetCombinedMinimumSize();
        Vector2 timerSize = GetTimerSize();
        float timerWidth = timerSize.X > 0f ? timerSize.X + GroupGap : 0f;
        float width = GetGroupWidth(goldSize, roomSize, timerSize);
        Vector2 spaceSize = new(width, Math.Max(80f, Math.Max(timerSize.Y, Math.Max(goldSize.Y, roomSize.Y))));
        if (_space.CustomMinimumSize != spaceSize)
        {
            _space.CustomMinimumSize = spaceSize;
            _right.UpdateMinimumSize();
            _right.QueueSort();
        }
        // Anchor directly to Tama rather than a viewport width or stale slot
        // position. This also works in the first frame before deferred sorting.
        Control portrait = _right.GetNodeOrNull<Control>("CharacterStatusButton") ?? _bar.Map;
        if (!Usable(portrait)) return;
        Transform2D transform = _right.GetGlobalTransform();
        Vector2 start = portrait.Position - new Vector2(width, 0f);
        if (timerWidth > 0f)
            Place(_timer, transform, start + new Vector2(0f, Math.Max(0f, (80f - timerSize.Y) * 0.5f)), timerSize);
        Place(_gold, transform, start + new Vector2(timerWidth, Math.Max(0f, (80f - goldSize.Y) * 0.5f)), goldSize);
        Place(_rooms, transform, start + new Vector2(timerWidth + goldSize.X + GroupGap,
            Math.Max(0f, (80f - roomSize.Y) * 0.5f)), roomSize);
        if (Usable(_goldPopup))
        {
            Vector2 edge = _bar.GetGlobalTransform().AffineInverse() *
                (_gold.GetGlobalTransform() * new Vector2(goldSize.X, 0f));
            // This label is right-aligned. Keep its right edge with the gold,
            // while preserving the native vertical gain/loss animation.
            _goldPopup.Position = new Vector2(edge.X - _goldPopup.Size.X, _goldPopup.Position.Y);
        }
    }

    internal float? GetGoldLeftEdge(Control coordinateSpace)
    {
        if (!_enabled || !Usable(coordinateSpace) || !Usable(_right)) return null;
        Control portrait = _right.GetNodeOrNull<Control>("CharacterStatusButton") ?? _bar.Map;
        if (!Usable(portrait)) return null;
        // The abilities must stop before the timer too, whenever it is shown.
        float width = GetGroupWidth(_gold.GetCombinedMinimumSize(), _rooms.GetCombinedMinimumSize(), GetTimerSize());
        Vector2 start = _right.GetGlobalTransform() * (portrait.Position - new Vector2(width, 0f));
        return (coordinateSpace.GetGlobalTransform().AffineInverse() * start).X;
    }

    private Vector2 GetTimerSize() => Usable(_timer) && _timer.Visible ? _timer.GetCombinedMinimumSize() : Vector2.Zero;

    private void RestoreTimer()
    {
        if (!Usable(_timer)) return;
        _timer.TopLevel = _timerTopLevel;
        _timer.ZIndex = _timerZIndex;
        _timer.Scale = _timerScale;
        _timer.Rotation = _timerRotation;
        if (_timerTopLevel) _timer.Position = _timerPosition;
    }

    private float GetGroupWidth(Vector2 goldSize, Vector2 roomSize, Vector2 timerSize)
    {
        float overflow = Usable(_bar.BossIcon) && _bar.BossIcon.IsVisibleInTree()
            ? Math.Max(0f, VisibleRightEdge(_bar.BossIcon) - _bar.BossIcon.Size.X) : 0f;
        return (timerSize.X > 0f ? timerSize.X + GroupGap : 0f) +
            goldSize.X + GroupGap + roomSize.X + overflow + PortraitGap;
    }
    private static float VisibleRightEdge(Control root)
    {
        float right = root.Size.X;
        MeasureRightEdge(root, root.GetGlobalTransform().AffineInverse(), ref right);
        return right;
    }

    private static void MeasureRightEdge(Node root, Transform2D inverse, ref float right)
    {
        foreach (Node child in root.GetChildren())
        {
            if (!Usable(child) || child is CanvasItem item && !item.IsVisibleInTree()) continue;
            if (child is Control control)
            {
                Transform2D transform = inverse * control.GetGlobalTransform();
                right = Math.Max(right, Math.Max((transform * Vector2.Zero).X,
                    Math.Max((transform * new Vector2(control.Size.X, 0f)).X,
                    Math.Max((transform * control.Size).X, (transform * new Vector2(0f, control.Size.Y)).X))));
            }
            MeasureRightEdge(child, inverse, ref right);
        }
    }

    private static void Place(Control control, Transform2D transform, Vector2 position, Vector2 size)
    {
        control.Scale = transform.Scale;
        control.Rotation = transform.Rotation;
        control.Size = size;
        control.GlobalPosition = transform * position;
    }

    private static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value) &&
        (value is not Node node || !node.IsQueuedForDeletion());
}
