using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PotionDamagePreviewUiHelper
{
    private static readonly FieldInfo PopupHolder = AccessTools.Field(typeof(NPotionPopup), "_holder");
    private static readonly Func<NTargetManager, Node> GetHoveredNode =
        AccessTools.MethodDelegate<Func<NTargetManager, Node>>(
            AccessTools.PropertyGetter(typeof(NTargetManager), "HoveredNode"));
    private static WeakReference<NPotionTargetDamagePreview> _targetPreview;

    internal static Creature GetSelectedTarget(PotionModel potion)
    {
        if (_targetPreview?.TryGetTarget(out NPotionTargetDamagePreview preview) != true ||
            !IsUsable(preview) || !ReferenceEquals(preview.Potion, potion)) return null;
        NTargetManager manager = preview.TargetManager;
        return IsUsable(manager) && manager.IsInSelection &&
            GetHoveredNode(manager) is NCreature creature
                ? creature.Entity
                : null;
    }

    internal static void Register(NPotionTargetDamagePreview preview) =>
        _targetPreview = new(preview);

    internal static void Unregister(NPotionTargetDamagePreview preview)
    {
        if (_targetPreview?.TryGetTarget(out NPotionTargetDamagePreview active) == true &&
            ReferenceEquals(active, preview)) _targetPreview = null;
    }

    internal static Task TrackTargeting(NPotionHolder holder, Task task)
    {
        PotionModel potion = holder.Potion?.Model;
        if (TestMode.IsOn || !IsUsable(holder) ||
            !PotionDamagePreviewHelper.CanPreview(potion) || !LocalContext.IsMe(potion.Owner))
            return task;

        NPotionTargetDamagePreview preview = null;
        try
        {
            preview = new() { Holder = holder, Potion = potion };
            holder.AddChildSafely(preview);
        }
        catch (Exception error)
        {
            StopPreview(preview);
            Log.Warn($"[HBRGOSakiMod] Potion targeting preview failed: {error.Message}");
        }
        return preview == null ? task : FinishTargeting(task, preview);
    }

    private static async Task FinishTargeting(Task task, NPotionTargetDamagePreview preview)
    {
        try
        {
            await task;
        }
        finally
        {
            StopPreview(preview);
        }
    }

    private static void StopPreview(NPotionTargetDamagePreview preview)
    {
        try
        {
            if (preview != null && GodotObject.IsInstanceValid(preview)) preview.Stop();
        }
        catch (Exception error)
        {
            // Optional UI cleanup must not replace the native use/cancel task result.
            Log.Warn($"[HBRGOSakiMod] Potion preview cleanup failed: {error.Message}");
        }
    }

    internal static void TrackHoverTips(
        NHoverTipSet tips, Control owner, MegaCrit.Sts2.Core.HoverTips.HoverTipAlignment alignment)
    {
        if (TestMode.IsOn || !IsUsable(tips)) return;
        PotionModel potion = null;
        for (Node node = owner; node != null; node = node.GetParent())
        {
            if (node is NPotionTargetDamagePreview targetPreview)
                potion = targetPreview.Potion;
            else if (node is NPotionHolder holder)
                potion = holder.Potion?.Model;
            else if (node is NPotionPopup popup)
                potion = (PopupHolder?.GetValue(popup) as NPotionHolder)?.Potion?.Model;
            if (potion != null) break;
        }
        if (!PotionDamagePreviewHelper.HasDamage(potion) || !potion.IsMutable ||
            potion.Owner == null || !LocalContext.IsMe(potion.Owner)) return;

        tips.AddChildSafely(new NPotionDamagePreview
        {
            Potion = potion,
            OwnerControl = owner,
            Alignment = alignment
        });
    }

    internal static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) &&
        !node.IsQueuedForDeletion() && node.IsInsideTree();
}
