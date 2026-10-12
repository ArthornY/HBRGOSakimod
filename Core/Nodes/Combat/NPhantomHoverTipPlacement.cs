using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NPhantomHoverTipPlacement : Node
{
    private const float Clearance = 12f;
    private const float ScreenMargin = 8f;
    internal NCreature Creature { get; set; }
    private NHoverTipSet _tips;
    private Control _text;
    private Control _cards;
    private Vector2 _nativePosition;
    private Vector2 _nativeScale;

    public override void _Ready()
    {
        _tips = GetParent<NHoverTipSet>();
        _nativePosition = _tips.Position;
        _nativeScale = _tips.Scale;
        _text = _tips.GetNodeOrNull<Control>("textHoverTipContainer");
        _cards = _tips.GetNodeOrNull<Control>("cardHoverTipContainer");
        RenderingServer.FramePreDraw += RefreshPlacement;
        RefreshPlacement();
        // Native flow containers finish wrapping after the initial hover call.
        Callable.From(RefreshPlacement).CallDeferred();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshPlacement;
    }

    private void RefreshPlacement()
    {
        if (!IsUsable(_tips) || !IsUsable(Creature) || !_tips.IsVisibleInTree() ||
            !PhantomHoverTipLayoutHelper.ShouldAvoidHud(Creature.Entity)) return;
        NCreatureStateDisplay state = Creature.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        NHealthBar health = state?.GetNodeOrNull<NHealthBar>("%HealthBar");
        if (!IsUsable(state) || !IsUsable(health)) return;

        Rect2? hud = null;
        AddVisibleBounds(health.HpBarContainer, ref hud);
        AddVisibleBounds(health.GetNodeOrNull<Control>("%BlockContainer"), ref hud);
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(health,
                HBRGOSakiCombatUiIntegration.DeflectorProtectionDpBarAttachmentId,
                out NDeflectorProtectionDpBar dp))
            AddVisibleBounds(dp, ref hud);
        NPowerContainer powers = state.GetNodeOrNull<NPowerContainer>("%PowerContainer");
        if (IsUsable(powers) && powers.IsVisibleInTree())
        {
            // PowerContainer.Size omits icons that overflow or wrap to row two.
            foreach (Node child in powers.GetChildren()) AddVisibleBounds(child, ref hud);
        }
        if (hud == null) return;

        // Always calculate from the native baseline, not last frame's adjusted
        // position/scale, so resizing and status animations cannot accumulate.
        _tips.Position = _nativePosition;
        _tips.Scale = _nativeScale;
        Rect2? content = GetContentBounds();
        if (content == null) return;
        float uiScale = _tips.GetGlobalTransformWithCanvas().Scale.Abs().X;
        Rect2 viewport = _tips.GetViewportRect().Grow(-ScreenMargin * uiScale);
        PhantomHoverTipPlacement placement = PhantomHoverTipLayoutHelper.ComputePlacement(
            content.Value, hud.Value, viewport, Clearance * uiScale);
        _tips.Scale = _nativeScale * placement.Scale;
        // Scaling around the native origin/pivot also moves child containers.
        // Read their new bounds before converting the viewport displacement.
        Rect2? scaled = GetContentBounds();
        if (scaled == null) return;
        Vector2 displacement = placement.Position - scaled.Value.Position;
        Transform2D parentToViewport = _tips.GetGlobalTransformWithCanvas() * _tips.GetTransform().AffineInverse();
        _tips.Position += parentToViewport.AffineInverse().BasisXform(displacement);
    }

    private Rect2? GetContentBounds()
    {
        Rect2? bounds = null;
        if (IsUsable(_text) && _text.GetChildCount() > 0) AddVisibleBounds(_text, ref bounds);
        if (IsUsable(_cards) && _cards.GetChildCount() > 0) AddVisibleBounds(_cards, ref bounds);
        return bounds;
    }

    private static void AddVisibleBounds(Node node, ref Rect2? bounds)
    {
        if (!IsUsable(node)) return;
        // Reserve space during remote-pet fade-in and HP-label fade-out too.
        if (node is CanvasItem item && !item.IsVisibleInTree()) return;
        if (node is Control control && control.Size.X > 0f && control.Size.Y > 0f)
        {
            Rect2 rect = control.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, control.Size);
            if (rect.HasArea() && rect.Position.IsFinite() && rect.Size.IsFinite())
                bounds = bounds is { } current ? current.Merge(rect) : rect;
        }
        foreach (Node child in node.GetChildren()) AddVisibleBounds(child, ref bounds);
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) &&
        !node.IsQueuedForDeletion() && node.IsInsideTree();
}
