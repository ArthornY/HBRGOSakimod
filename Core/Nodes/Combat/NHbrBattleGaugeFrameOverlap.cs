using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NHbrBattleGaugeFrameOverlap : Node
{
    internal NHealthBar HealthBar { get; set; }
    private readonly List<NinePatchRect> _hpFrames = new(5);
    private readonly List<NinePatchRect> _dpFrames = new(3);
    private readonly List<Node> _watchedNodes = new(12);
    private NHealthBar _boundHealthBar;
    private Control _boundHp;
    private NDeflectorProtectionDpBar _boundDp;
    private bool _bindingsDirty = true;

    public override void _Ready() => RenderingServer.FramePreDraw += RefreshOverlap;

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshOverlap;
        ClearBindings();
        _boundHealthBar = null;
        _boundHp = null;
        _boundDp = null;
        _bindingsDirty = true;
    }

    internal void RefreshOverlap()
    {
        if (!HbrBattleGaugeFrameOverlapHelper.IsUsable(HealthBar) ||
            !HbrBattleGaugeFrameOverlapHelper.IsUsable(HealthBar.HpBarContainer)) return;

        Control hp = HealthBar.HpBarContainer;
        HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(HealthBar,
            HBRGOSakiCombatUiIntegration.DeflectorProtectionDpBarAttachmentId,
            out NDeflectorProtectionDpBar dp);
        if (!HbrBattleGaugeFrameOverlapHelper.IsUsable(dp)) dp = null;
        RefreshBindings(hp, dp);

        // Geometry and visibility still follow every native layout/DP update;
        // only the stable node-path bindings are cached between tree changes.
        foreach (NinePatchRect frame in _hpFrames)
            HbrBattleGaugeFrameOverlapHelper.SetOccluders(frame, _dpFrames);
        foreach (NinePatchRect frame in _dpFrames)
            HbrBattleGaugeFrameOverlapHelper.SetOccluders(frame, _hpFrames);
    }

    private void RefreshBindings(Control hp, NDeflectorProtectionDpBar dp)
    {
        if (!ReferenceEquals(_boundHealthBar, HealthBar) || !ReferenceEquals(_boundHp, hp) ||
            !ReferenceEquals(_boundDp, dp) || !FramesUsable(_hpFrames) || !FramesUsable(_dpFrames))
            _bindingsDirty = true;
        if (!_bindingsDirty) return;
        ClearBindings();
        _boundHealthBar = HealthBar;
        _boundHp = hp;
        _boundDp = dp;
        _bindingsDirty = false;
        Watch(HealthBar);
        Watch(hp);
        Add(_hpFrames, hp.GetNodeOrNull<NinePatchRect>("HbrHpFrame"));
        Add(_hpFrames, HealthBar.GetNodeOrNull<NinePatchRect>("%BlockOutline"));
        Node hpReserves = hp.GetNodeOrNull<Node>("HbrHpReserveLayers");
        Watch(hpReserves);
        AddReserve(_hpFrames, hpReserves, "ReserveHp1", "Frame");
        AddReserve(_hpFrames, hpReserves, "ReserveHp2", "Frame");
        AddReserve(_hpFrames, hpReserves, "ReserveHp3", "Frame");
        if (dp == null) return;
        Watch(dp);
        Add(_dpFrames, dp.GetNodeOrNull<NinePatchRect>("HbrDpFrame"));
        Node dpReserves = dp.GetNodeOrNull<Node>("DpLayeredForeground");
        Watch(dpReserves);
        AddReserve(_dpFrames, dpReserves, "DpReserve1", "HbrDpReserveFrame");
        AddReserve(_dpFrames, dpReserves, "DpReserve2", "HbrDpReserveFrame");
    }

    private void AddReserve(List<NinePatchRect> frames, Node root, string layerName, string frameName)
    {
        Node layer = root?.GetNodeOrNull<Node>(layerName);
        Watch(layer);
        Add(frames, layer?.GetNodeOrNull<NinePatchRect>(frameName));
    }

    private void Watch(Node node)
    {
        if (!HbrBattleGaugeFrameOverlapHelper.IsUsable(node)) return;
        node.ChildOrderChanged += InvalidateBindings;
        _watchedNodes.Add(node);
    }

    private void InvalidateBindings() => _bindingsDirty = true;

    private void ClearBindings()
    {
        foreach (Node node in _watchedNodes)
            if (GodotObject.IsInstanceValid(node)) node.ChildOrderChanged -= InvalidateBindings;
        _watchedNodes.Clear();
        _hpFrames.Clear();
        _dpFrames.Clear();
    }

    private static bool FramesUsable(List<NinePatchRect> frames)
    {
        foreach (NinePatchRect frame in frames)
            if (!HbrBattleGaugeFrameOverlapHelper.IsUsable(frame)) return false;
        return true;
    }

    private static void Add(List<NinePatchRect> frames, NinePatchRect frame)
    {
        if (HbrBattleGaugeFrameOverlapHelper.IsUsable(frame)) frames.Add(frame);
    }
}
