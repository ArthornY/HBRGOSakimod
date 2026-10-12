using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Native potion menus close before targeting. Keep a separate, non-interactive
// tooltip below the selected potion for the lifetime of that targeting task.
public partial class NPotionTargetDamagePreview : Control
{
    internal NPotionHolder Holder { get; set; }
    internal PotionModel Potion { get; set; }
    internal NTargetManager TargetManager { get; private set; }
    private bool _stopped;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = Holder.Size;
        TargetManager = NRun.Instance?.GlobalUi.TargetManager;
        PotionDamagePreviewUiHelper.Register(this);
        // Temporarily bypass the native targeting guard only for this dedicated
        // preview. Preserve the guard for every other tooltip and later calls.
        bool blocked = NHoverTipSet.shouldBlockHoverTips;
        try
        {
            NHoverTipSet.shouldBlockHoverTips = false;
            NHoverTipSet.CreateAndShow(this, Potion.HoverTips, HoverTipAlignment.Center);
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = blocked;
        }
    }

    public override void _Process(double delta)
    {
        if (!PotionDamagePreviewUiHelper.IsUsable(TargetManager) ||
            !TargetManager.IsInSelection || !PotionDamagePreviewHelper.CanPreview(Potion)) Stop();
    }

    public override void _ExitTree()
    {
        PotionDamagePreviewUiHelper.Unregister(this);
        NHoverTipSet.Remove(this);
    }

    internal void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        PotionDamagePreviewUiHelper.Unregister(this);
        NHoverTipSet.Remove(this);
        this.QueueFreeSafely();
    }
}
