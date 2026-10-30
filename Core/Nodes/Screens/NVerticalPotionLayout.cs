using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NVerticalPotionLayout : Node
{
    internal const string WidgetName = "HBRGOVerticalPotionLayout";
    private const float PanelWidth = 90f;
    private const float Padding = 8f;
    private const float SlotSize = 60f;
    private const float SlotGap = 2f;
    private const float RelicGap = 8f;
    private const int HoverTipLayerOffset = 10;

    private static readonly FieldInfo ShakeTween = AccessTools.DeclaredField(typeof(NPotionContainer), "_potionsFullTween");
    private static readonly FieldInfo ShakeOrigin = AccessTools.DeclaredField(typeof(NPotionContainer), "_potionHolderInitPos");
    private static readonly MethodInfo NativePotionNavigation = AccessTools.DeclaredMethod(typeof(NPotionContainer), "UpdateNavigation");

    private sealed class HolderState
    {
        internal readonly NPotionHolder Holder;
        internal readonly List<NPotionPopup> Popups = new();
        internal readonly Action ChildrenChanged;
        internal bool PopupsDirty = true;

        internal HolderState(NPotionHolder holder)
        {
            Holder = holder;
            ChildrenChanged = () => PopupsDirty = true;
        }
    }

    private sealed class HoverTipLayerState
    {
        internal int OriginalZ;
        internal bool OriginalRelative;
        internal bool Raised;
        internal Action Exiting;
    }

    private NGlobalUi _ui;
    private NPotionContainer _potions;
    private Control _wrapper;
    private Control _holderContainer;
    private Control _background;
    private Control _errorBackground;
    private Control _shortcut;
    private bool _originalTopLevel;
    private int _originalZIndex;
    private bool _topBarHidden;
    private bool _relicsHidden;
    private bool _exiting;
    private bool _enabled = true;
    private bool _masked;
    private bool _visibleBeforeMask;
    private NRelicInventoryHolder _anchor;
    private NodePath _originalAnchorBottom;
    private NodePath _anchorPotionNeighbor;
    private NRelicInventoryHolder _layoutAnchor;
    private bool _relicStructureDirty = true;
    private bool _holderStructureDirty = true;
    private bool _anchorDirty = true;
    private bool _navigationDirty = true;
    private readonly List<NRelicInventoryHolder> _relics = new();
    private readonly List<NPotionHolder> _holders = new();
    private readonly Dictionary<NPotionHolder, HolderState> _holderStates = new();
    private readonly List<NodePath> _holderPaths = new();
    private readonly Dictionary<NPotionPopup, Vector2> _popupPositions = new();
    private readonly Dictionary<Control, Control.MouseFilterEnum> _mouseFilters = new();
    private readonly Dictionary<Control, NodePath> _relicTopNeighbors = new();
    private readonly Dictionary<NHoverTipSet, HoverTipLayerState> _hoverTipLayers = new();

    internal static NVerticalPotionLayout Current =>
        NRun.Instance?.GlobalUi?.TopBar?.GetNodeOrNull<NVerticalPotionLayout>(WidgetName);

    internal static void Install(NGlobalUi ui)
    {
        if (!Usable(ui?.TopBar)) return;
        if (ui.TopBar.GetNodeOrNull<NVerticalPotionLayout>(WidgetName) == null)
        {
            var layout = new NVerticalPotionLayout { Name = WidgetName };
            layout.Bind(ui);
            ui.TopBar.AddChild(layout);
        }
    }

    private void Bind(NGlobalUi ui)
    {
        _ui = ui;
        _potions = ui.TopBar.PotionContainer;
        _wrapper = _potions?.GetParent() as Control;
        if (!Usable(_wrapper)) return;
        _holderContainer = _potions.GetNodeOrNull<Control>("MarginContainer/PotionHolders");
        _background = _potions.GetNodeOrNull<Control>("PotionBg");
        _errorBackground = _potions.GetNodeOrNull<Control>("PotionErrorBg");
        _shortcut = _potions.GetNodeOrNull<Control>("PotionShortcutButton");
        _originalTopLevel = _wrapper.TopLevel;
        _originalZIndex = _wrapper.ZIndex;
        _topBarHidden = ui.TopBar.FocusBehaviorRecursive == Control.FocusBehaviorRecursiveEnum.Disabled;
        _relicsHidden = ui.RelicInventory.FocusBehaviorRecursive == Control.FocusBehaviorRecursiveEnum.Disabled;
        // A top-level Control is excluded from its HBox without leaving the
        // tree. Reparenting would clear the native potion player's connections.
        _wrapper.TopLevel = true;
        _wrapper.ZIndex = ui.TopBar.ZIndex;
        IgnoreMouse(_wrapper);
        IgnoreMouse(_potions);
        IgnoreMouse(_potions.GetNodeOrNull<Control>("MarginContainer"));
        IgnoreMouse(_background);
        IgnoreMouse(_errorBackground);
        if (_wrapper.GetParent() is Container parent)
        {
            parent.UpdateMinimumSize();
            parent.QueueSort();
        }
    }

    public override void _Ready()
    {
        if (Usable(_ui?.RelicInventory))
        {
            _ui.RelicInventory.RelicsChanged += InvalidateRelicStructure;
            _ui.RelicInventory.ChildOrderChanged += InvalidateRelicStructure;
            _ui.RelicInventory.SortChildren += InvalidateAnchor;
            _ui.RelicInventory.Resized += InvalidateAnchor;
        }
        if (Usable(_holderContainer)) _holderContainer.ChildOrderChanged += InvalidateHolderStructure;
        RenderingServer.FramePreDraw += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        _exiting = true;
        _enabled = false;
        RestoreNativeLayout();
        foreach (var (tips, state) in _hoverTipLayers)
        {
            if (!GodotObject.IsInstanceValid(tips)) continue;
            tips.TreeExiting -= state.Exiting;
            RestoreHoverTipLayer(tips, state);
        }
        _hoverTipLayers.Clear();
        if (Usable(_ui?.RelicInventory))
        {
            _ui.RelicInventory.RelicsChanged -= InvalidateRelicStructure;
            _ui.RelicInventory.ChildOrderChanged -= InvalidateRelicStructure;
            _ui.RelicInventory.SortChildren -= InvalidateAnchor;
            _ui.RelicInventory.Resized -= InvalidateAnchor;
        }
        if (Usable(_holderContainer)) _holderContainer.ChildOrderChanged -= InvalidateHolderStructure;
        DisconnectRelicEvents();
        DisconnectHolderEvents();
    }

    private void RestoreNativeLayout()
    {
        foreach (var (popup, position) in _popupPositions)
            if (Usable(popup)) popup.Position = position;
        _popupPositions.Clear();
        foreach (var (control, filter) in _mouseFilters)
            if (Usable(control)) control.MouseFilter = filter;
        foreach (var (control, path) in _relicTopNeighbors)
            if (Usable(control)) control.FocusNeighborTop = path;
        RestoreAnchorNavigation();
        if (!Usable(_wrapper)) return;
        if (_masked) _wrapper.Visible = _visibleBeforeMask;
        _wrapper.TopLevel = _originalTopLevel;
        _wrapper.ZIndex = _originalZIndex;
        _masked = false;
        _anchor = null;
        _relicTopNeighbors.Clear();
        if (Usable(_holderContainer)) _holderContainer.Call("queue_sort");
        if (Usable(_potions))
        {
            _potions.Call("queue_sort");
            NativePotionNavigation?.Invoke(_potions, null);
        }
        if (_wrapper.GetParent() is Container parent)
        {
            parent.UpdateMinimumSize();
            parent.QueueSort();
        }
    }

    private void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        if (!enabled) { RestoreNativeLayout(); return; }
        InvalidateRelicStructure();
        InvalidateHolderStructure();
        _wrapper.TopLevel = true;
        _wrapper.ZIndex = _ui.TopBar.ZIndex;
        foreach (Control control in _mouseFilters.Keys)
            if (Usable(control)) control.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (_wrapper.GetParent() is Container parent)
        {
            parent.UpdateMinimumSize();
            parent.QueueSort();
        }
    }

    internal void SetNativeHidden(Node source, bool hidden)
    {
        if (ReferenceEquals(source, _ui?.TopBar)) _topBarHidden = hidden;
        else if (ReferenceEquals(source, _ui?.RelicInventory)) _relicsHidden = hidden;
        RefreshLayout();
    }

    internal bool Owns(Control owner) => !_exiting && HBRGOSakiConfig.ModUI && _enabled && Usable(_potions) && Usable(owner) &&
        (ReferenceEquals(owner, _potions) || _potions.IsAncestorOf(owner));

    internal void PlaceHoverTipAboveHud(NHoverTipSet tips)
    {
        if (_exiting || !HBRGOSakiConfig.ModUI || !_enabled || !Usable(tips) || !Usable(_ui?.TopBar)) return;
        if (!_hoverTipLayers.ContainsKey(tips))
        {
            var state = new HoverTipLayerState { OriginalZ = tips.ZIndex, OriginalRelative = tips.ZAsRelative };
            state.Exiting = () => _hoverTipLayers.Remove(tips);
            _hoverTipLayers.Add(tips, state);
            tips.TreeExiting += state.Exiting;
        }
        RefreshHoverTipLayers();
    }

    private void RefreshHoverTipLayers()
    {
        if (_hoverTipLayers.Count == 0) return;
        bool raise = HBRGOSakiConfig.ModUI && !_exiting &&
            NGame.Instance?.Transition?.InTransition != true && NModalContainer.Instance?.OpenModal == null;
        foreach (var (tips, state) in _hoverTipLayers)
        {
            if (state.Raised == raise || !Usable(tips)) continue;
            if (raise)
            {
                // Top-level HUD controls draw above ordinary controls at the
                // same Z. Lift text and card tips together, without moving HUD.
                tips.ZAsRelative = false;
                tips.ZIndex = Math.Max(state.OriginalZ, _ui.TopBar.ZIndex + HoverTipLayerOffset);
                state.Raised = true;
            }
            else RestoreHoverTipLayer(tips, state);
        }
    }

    private static void RestoreHoverTipLayer(NHoverTipSet tips, HoverTipLayerState state)
    {
        tips.ZIndex = state.OriginalZ;
        tips.ZAsRelative = state.OriginalRelative;
        state.Raised = false;
    }

    internal bool TryGetHoverTipBounds(Control owner, out Rect2 bounds)
    {
        bounds = default;
        if (!Owns(owner) || _masked || !Usable(_background) || !_wrapper.IsVisibleInTree()) return false;
        bounds = _background.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, _background.Size);
        return bounds.HasArea() && bounds.Position.IsFinite() && bounds.Size.IsFinite();
    }

    private void IgnoreMouse(Control control)
    {
        if (!Usable(control)) return;
        _mouseFilters.TryAdd(control, control.MouseFilter);
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private void InvalidateRelicStructure()
    {
        _relicStructureDirty = true;
        _anchorDirty = true;
        _navigationDirty = true;
    }

    private void InvalidateHolderStructure()
    {
        _holderStructureDirty = true;
        _navigationDirty = true;
    }

    private void InvalidateAnchor() => _anchorDirty = true;

    internal void InvalidateNativeNavigation(Node source)
    {
        if (_exiting || !ReferenceEquals(source, _potions) &&
            !ReferenceEquals(source, _ui?.RelicInventory)) return;
        _navigationDirty = true;
        if (!_enabled || !HBRGOSakiConfig.ModUI || !ReferenceEquals(source, _ui?.RelicInventory)) return;
        // Native navigation owns the restore baseline, including multiplayer
        // neighbors. Capture its new values before the vertical links return.
        foreach (NRelicInventoryHolder relic in _ui.RelicInventory.RelicNodes)
            if (Usable(relic)) _relicTopNeighbors[relic] = relic.FocusNeighborTop;
        if (Usable(_anchor)) _originalAnchorBottom = _anchor.FocusNeighborBottom;
    }

    private void DisconnectRelicEvents()
    {
        foreach (NRelicInventoryHolder relic in _relics)
        {
            if (!Usable(relic)) continue;
            relic.ItemRectChanged -= InvalidateAnchor;
            relic.VisibilityChanged -= InvalidateAnchor;
        }
        _relics.Clear();
    }

    private void RebuildRelics()
    {
        DisconnectRelicEvents();
        foreach (NRelicInventoryHolder relic in _ui.RelicInventory.RelicNodes)
        {
            if (!Usable(relic) || !relic.IsInsideTree()) continue;
            _relics.Add(relic);
            relic.ItemRectChanged += InvalidateAnchor;
            relic.VisibilityChanged += InvalidateAnchor;
        }
        // Removed holders should not stay rooted by their restore paths.
        var removed = new List<Control>();
        foreach (Control relic in _relicTopNeighbors.Keys)
            if (relic is not NRelicInventoryHolder holder || !_relics.Contains(holder)) removed.Add(relic);
        foreach (Control relic in removed) _relicTopNeighbors.Remove(relic);
        _relicStructureDirty = false;
        _anchorDirty = true;
    }

    private void DisconnectHolderEvents()
    {
        foreach (HolderState state in _holderStates.Values)
            if (Usable(state.Holder)) state.Holder.ChildOrderChanged -= state.ChildrenChanged;
        _holderStates.Clear();
        _holders.Clear();
    }

    private void RebuildHolders()
    {
        RestoreAnchorNavigation();
        DisconnectHolderEvents();
        foreach (Node child in _holderContainer.GetChildren())
        {
            if (child is not NPotionHolder holder || !Usable(holder) || !holder.IsInsideTree()) continue;
            _holders.Add(holder);
            var state = new HolderState(holder);
            _holderStates.Add(holder, state);
            holder.ChildOrderChanged += state.ChildrenChanged;
        }
        _holderStructureDirty = false;
        _navigationDirty = true;
    }

    private void RefreshPopups(HolderState state)
    {
        if (!state.PopupsDirty) return;
        foreach (NPotionPopup popup in state.Popups)
            if (!Usable(popup) || !ReferenceEquals(popup.GetParent(), state.Holder)) _popupPositions.Remove(popup);
        state.Popups.Clear();
        foreach (Node child in state.Holder.GetChildren())
            if (child is NPotionPopup popup && Usable(popup)) state.Popups.Add(popup);
        state.PopupsDirty = false;
    }

    private NRelicInventoryHolder FindAnchor()
    {
        NRelicInventoryHolder anchor = null;
        foreach (NRelicInventoryHolder relic in _relics)
        {
            if (!Usable(relic) || !relic.IsInsideTree() || !relic.Visible) continue;
            if (anchor == null || relic.Position.Y > anchor.Position.Y + 0.5f ||
                Math.Abs(relic.Position.Y - anchor.Position.Y) <= 0.5f && relic.Position.X < anchor.Position.X)
                anchor = relic;
        }
        return anchor;
    }

    internal void RefreshLayout()
    {
        if (_exiting || !Usable(_wrapper) || !Usable(_holderContainer) || !Usable(_ui?.RelicInventory)) return;
        SetEnabled(HBRGOSakiConfig.ModUI);
        // Only track live tips; no scene searches or per-frame child scans.
        // Native modal/transition layers must still cover an existing tooltip.
        RefreshHoverTipLayers();
        if (!_enabled) return;
        bool hidden = _topBarHidden || _relicsHidden || !_ui.RelicInventory.IsVisibleInTree();
        if (hidden && !_masked)
        {
            _visibleBeforeMask = _wrapper.Visible;
            _wrapper.Visible = false;
            _masked = true;
        }
        else if (!hidden && _masked)
        {
            _wrapper.Visible = _visibleBeforeMask;
            _masked = false;
        }
        if (hidden) return;

        if (_relicStructureDirty) RebuildRelics();
        if (_holderStructureDirty) RebuildHolders();
        if (_anchorDirty || _layoutAnchor != null && !Usable(_layoutAnchor))
        {
            NRelicInventoryHolder nextAnchor = FindAnchor();
            if (!ReferenceEquals(nextAnchor, _layoutAnchor)) _navigationDirty = true;
            _layoutAnchor = nextAnchor;
            _anchorDirty = false;
        }
        NRelicInventoryHolder anchor = _layoutAnchor;
        Transform2D transform = _ui.RelicInventory.GetGlobalTransform();
        Vector2 origin = anchor == null ? new Vector2(34f, _ui.RelicInventory.Size.Y)
            : anchor.Position + new Vector2(anchor.Size.X * 0.5f, anchor.Size.Y);
        _wrapper.Scale = transform.Scale;
        _wrapper.Rotation = transform.Rotation;
        _wrapper.GlobalPosition = transform * (origin + new Vector2(-PanelWidth * 0.5f, RelicGap));

        float height = Padding * 2f + Math.Max(1, _holders.Count) * SlotSize +
            Math.Max(0, _holders.Count - 1) * SlotGap;
        Vector2 panelSize = new(PanelWidth, height);
        LayoutPanel(_background, panelSize);
        LayoutPanel(_errorBackground, panelSize);
        LayoutPanel(_shortcut, panelSize);
        Vector2 shake = GetShakeOffset();
        Transform2D holderInverse = _holderContainer.GetGlobalTransform().AffineInverse();
        Transform2D potionTransform = _potions.GetGlobalTransform();
        for (int index = 0; index < _holders.Count; index++)
        {
            NPotionHolder holder = _holders[index];
            if (!Usable(holder) || !holder.IsInsideTree()) { InvalidateHolderStructure(); continue; }
            // The native HBox remains intact. Its parent margins, error shake,
            // original slot identity and contents are preserved; only the final
            // holder position is written after the normal container layout.
            Vector2 point = new((PanelWidth - SlotSize) * 0.5f, Padding + index * (SlotSize + SlotGap));
            holder.Position = holderInverse * (potionTransform * point + shake);
            HolderState state = _holderStates[holder];
            RefreshPopups(state);
            foreach (NPotionPopup popup in state.Popups)
                if (Usable(popup) && !PotionPopupCompatibility.IsMarkedForRemoval(popup)) DockPopup(popup, holder);
        }
        if (_navigationDirty && !_holderStructureDirty) UpdateNavigation(anchor);
    }

    private Vector2 GetShakeOffset()
    {
        if (ShakeTween?.GetValue(_potions) is not Tween tween || !Usable(tween) || !tween.IsRunning() ||
            ShakeOrigin?.GetValue(_potions) is not Vector2 origin) return Vector2.Zero;
        Vector2 offset = _holderContainer.Position - origin;
        Transform2D transform = (_holderContainer.GetParent() as CanvasItem)?.GetGlobalTransform() ?? Transform2D.Identity;
        return transform * offset - transform.Origin;
    }

    private static void LayoutPanel(Control panel, Vector2 size)
    {
        if (!Usable(panel)) return;
        panel.Position = Vector2.Zero;
        panel.Size = size;
    }

    private void DockPopup(NPotionPopup popup, NPotionHolder holder)
    {
        _popupPositions.TryAdd(popup, popup.Position);
        Vector2 point = _potions.GetGlobalTransform() * new Vector2(PanelWidth + Padding, Padding);
        point.Y = holder.GlobalPosition.Y;
        Rect2 viewport = popup.GetViewport().GetVisibleRect();
        Vector2 size = popup.GetGlobalRect().Size;
        point.X = Mathf.Clamp(point.X, viewport.Position.X + Padding,
            Mathf.Max(viewport.Position.X + Padding, viewport.End.X - size.X - Padding));
        point.Y = Mathf.Clamp(point.Y, viewport.Position.Y + Padding,
            Mathf.Max(viewport.Position.Y + Padding, viewport.End.Y - size.Y - Padding));
        popup.GlobalPosition = point;
    }

    private void UpdateNavigation(NRelicInventoryHolder anchor)
    {
        if (_holders.Count == 0)
        {
            RestoreAnchorNavigation();
            _navigationDirty = false;
            return;
        }
        if (!Usable(_ui.TopBar.Gold) || !Usable(_ui.TopBar.RoomIcon) ||
            !Usable(_ui.TopBar.ActiveScreenProxy)) return;
        _holderPaths.Clear();
        foreach (NPotionHolder holder in _holders) _holderPaths.Add(holder.GetPath());
        NodePath first = _holderPaths[0];
        if (!ReferenceEquals(anchor, _anchor))
        {
            RestoreAnchorNavigation();
            _anchor = anchor;
            if (Usable(anchor)) _originalAnchorBottom = anchor.FocusNeighborBottom;
        }
        if (Usable(anchor))
        {
            if (anchor.FocusNeighborBottom != first) _originalAnchorBottom = anchor.FocusNeighborBottom;
            anchor.FocusNeighborBottom = first;
            _anchorPotionNeighbor = first;
        }
        NodePath gold = _ui.TopBar.Gold.GetPath();
        NodePath room = _ui.TopBar.RoomIcon.GetPath();
        NodePath screen = _ui.TopBar.ActiveScreenProxy.GetPath();
        NodePath anchorPath = Usable(anchor) ? anchor.GetPath() : gold;
        foreach (NRelicInventoryHolder relic in _relics)
        {
            if (!Usable(relic)) continue;
            _relicTopNeighbors.TryAdd(relic, relic.FocusNeighborTop);
            relic.FocusNeighborTop = gold;
        }
        for (int index = 0; index < _holders.Count; index++)
        {
            NPotionHolder holder = _holders[index];
            holder.FocusNeighborTop = index > 0 ? _holderPaths[index - 1] : anchorPath;
            holder.FocusNeighborBottom = index + 1 < _holders.Count ? _holderPaths[index + 1] : screen;
            holder.FocusNeighborLeft = gold;
            holder.FocusNeighborRight = room;
        }
        _navigationDirty = false;
    }

    private void RestoreAnchorNavigation()
    {
        if (Usable(_anchor) && _anchor.IsInsideTree() && _anchorPotionNeighbor != null &&
            _anchor.FocusNeighborBottom == _anchorPotionNeighbor)
            _anchor.FocusNeighborBottom = _originalAnchorBottom;
        _anchorPotionNeighbor = null;
    }

    internal static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value) &&
        (value is not Node node || !node.IsQueuedForDeletion());
}
