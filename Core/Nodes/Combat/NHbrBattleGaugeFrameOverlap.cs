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

    public override void _Ready() => RenderingServer.FramePreDraw += RefreshOverlap;

    public override void _ExitTree() => RenderingServer.FramePreDraw -= RefreshOverlap;

    internal void RefreshOverlap()
    {
        if (!HbrBattleGaugeFrameOverlapHelper.IsUsable(HealthBar) ||
            !HbrBattleGaugeFrameOverlapHelper.IsUsable(HealthBar.HpBarContainer)) return;

        // Run after native layout, DP refreshes and layer visibility updates,
        // so resizing or breaking a layer cannot leave last frame's cutout.
        _hpFrames.Clear();
        _dpFrames.Clear();
        Control hp = HealthBar.HpBarContainer;
        Add(_hpFrames, hp.GetNodeOrNull<NinePatchRect>("HbrHpFrame"));
        Add(_hpFrames, HealthBar.GetNodeOrNull<NinePatchRect>("%BlockOutline"));
        for (int i = 1; i <= 3; i++)
            Add(_hpFrames, hp.GetNodeOrNull<NinePatchRect>($"HbrHpReserveLayers/ReserveHp{i}/Frame"));

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(HealthBar,
            HBRGOSakiCombatUiIntegration.DeflectorProtectionDpBarAttachmentId,
            out NDeflectorProtectionDpBar dp) && HbrBattleGaugeFrameOverlapHelper.IsUsable(dp))
        {
            Add(_dpFrames, dp.GetNodeOrNull<NinePatchRect>("HbrDpFrame"));
            for (int i = 1; i <= 2; i++)
                Add(_dpFrames, dp.GetNodeOrNull<NinePatchRect>($"DpLayeredForeground/DpReserve{i}/HbrDpReserveFrame"));
        }

        foreach (NinePatchRect frame in _hpFrames)
            HbrBattleGaugeFrameOverlapHelper.SetOccluders(frame, _dpFrames);
        foreach (NinePatchRect frame in _dpFrames)
            HbrBattleGaugeFrameOverlapHelper.SetOccluders(frame, _hpFrames);
    }

    private static void Add(List<NinePatchRect> frames, NinePatchRect frame)
    {
        if (HbrBattleGaugeFrameOverlapHelper.IsUsable(frame)) frames.Add(frame);
    }
}
