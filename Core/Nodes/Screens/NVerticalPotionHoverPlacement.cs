using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Keep the native tooltip contents and lifetime, replacing only the final
// placement for the potion bar when ModUI lays it out vertically.
public partial class NVerticalPotionHoverPlacement : Node
{
    private const float Clearance = 12f;
    private const float ScreenMargin = 8f;
    internal Control OwnerControl { get; set; }
    private NHoverTipSet _tips;
    private Control _text;
    private Control _cards;
    private Vector2 _nativeOffset;
    private Vector2 _nativeScale;
    private bool _placed;

    public override void _Ready()
    {
        _tips = GetParent<NHoverTipSet>();
        _text = _tips.GetNodeOrNull<Control>("textHoverTipContainer");
        _cards = _tips.GetNodeOrNull<Control>("cardHoverTipContainer");
        _nativeOffset = _tips.GlobalPosition - OwnerControl.GlobalPosition;
        _nativeScale = _tips.Scale;
        RenderingServer.FramePreDraw += RefreshPlacement;
        RefreshPlacement();
    }

    public override void _ExitTree() => RenderingServer.FramePreDraw -= RefreshPlacement;

    private void RefreshPlacement()
    {
        if (!Usable(_tips) || !Usable(OwnerControl) || !_tips.IsVisibleInTree()) return;
        NVerticalPotionLayout layout = NVerticalPotionLayout.Current;
        if (layout == null || !layout.TryGetHoverTipBounds(OwnerControl, out Rect2 bar))
        {
            if (_placed)
            {
                _tips.Scale = _nativeScale;
                _tips.GlobalPosition = OwnerControl.GlobalPosition + _nativeOffset;
                _tips.SetAlignment(OwnerControl, MegaCrit.Sts2.Core.HoverTips.HoverTipAlignment.None);
                _placed = false;
            }
            return;
        }

        // Restore the baseline scale before measuring. Container reflow and
        // damage-preview refreshes may have moved text or card tips meanwhile.
        _tips.Scale = _nativeScale;
        Rect2? content = GetContentBounds();
        if (content == null) return;
        float uiScale = _tips.GetGlobalTransformWithCanvas().Scale.Abs().X;
        Rect2 ownerBounds = OwnerControl.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, OwnerControl.Size);
        // Popup buttons can extend past the bar too; keep their own hover
        // descriptions clear of the button as well as the full potion column.
        float right = Mathf.Max(bar.End.X, ownerBounds.End.X) + Clearance * uiScale;
        Rect2 preferred = new(new Vector2(right, ownerBounds.Position.Y), content.Value.Size);
        Rect2 viewport = _tips.GetViewportRect().Grow(-ScreenMargin * uiScale);
        // This shared geometry routine fits the full text/card group into the
        // viewport while avoiding the reserved HUD rectangle.
        PhantomHoverTipPlacement placement = PhantomHoverTipLayoutHelper.ComputePlacement(
            preferred, bar, viewport, Clearance * uiScale);
        _tips.Scale = _nativeScale * placement.Scale;
        Rect2? scaled = GetContentBounds();
        if (scaled == null) return;
        Vector2 displacement = placement.Position - scaled.Value.Position;
        Transform2D parentToViewport = _tips.GetGlobalTransformWithCanvas() * _tips.GetTransform().AffineInverse();
        _tips.Position += parentToViewport.AffineInverse().BasisXform(displacement);
        _placed = true;
    }

    private Rect2? GetContentBounds()
    {
        Rect2? bounds = null;
        AddChildrenBounds(_text, ref bounds);
        AddChildrenBounds(_cards, ref bounds);
        return bounds;
    }

    private static void AddChildrenBounds(Node parent, ref Rect2? bounds)
    {
        if (!Usable(parent)) return;
        foreach (Node child in parent.GetChildren())
        {
            if (!Usable(child) || child is CanvasItem item && !item.IsVisibleInTree()) continue;
            if (child is Control control && control.Size.X > 0f && control.Size.Y > 0f)
            {
                Rect2 rect = control.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, control.Size);
                if (rect.HasArea() && rect.Position.IsFinite() && rect.Size.IsFinite())
                    bounds = bounds is { } current ? current.Merge(rect) : rect;
            }
            AddChildrenBounds(child, ref bounds);
        }
    }

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        !node.IsQueuedForDeletion() && node.IsInsideTree();
}
