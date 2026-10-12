using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class PhantomBlockUi
{
    private sealed class Layout
    {
        internal float VerticalOffset;
    }

    private static readonly ConditionalWeakTable<NHealthBar, Layout> Layouts = new();

    internal static bool AppliesTo(Creature creature) =>
        creature?.PetOwner != null &&
        (creature.HasPower<PhantomPower>() || PhantomHelper.IsPhantomMinion(creature));

    internal static void RememberLayout(NHealthBar healthBar, Control block)
    {
        // Capture the scene's vertical offset before either the native layout
        // or a scene-boss layout moves the HP bar independently of its root.
        Layouts.GetValue(healthBar, _ => new Layout
        {
            VerticalOffset = block.Position.Y - healthBar.HpBarContainer.Position.Y
        });
    }

    internal static bool Prepare(NHealthBar healthBar, Creature creature,
        Control block, ref Vector2 originalPosition, ref Tween blockTween)
    {
        if (!AppliesTo(creature)) return false;
        Control hpBar = healthBar.HpBarContainer;
        if (block == null || hpBar == null) return false;

        RememberLayout(healthBar, block);
        float padding = 24f - (creature.Monster?.HpBarSizeReduction ?? 0f);
        originalPosition = new Vector2(
            padding * 0.5f - block.Size.X * 0.5f,
            Layouts.GetValue(healthBar, _ => new Layout()).VerticalOffset);

        if (block.GetParent() != hpBar)
        {
            // Ritsu and native layout both relocate HP/block globally, whereas
            // the gain tween uses local coordinates. Give them one parent so
            // the fixed phantom HUD cannot leave the icon at an old origin.
            blockTween?.Kill();
            blockTween = null;
            block.Reparent(hpBar, false);
            block.Position = originalPosition;
            block.Modulate = Colors.White;
        }
        return true;
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar._Ready))]
internal static class PhantomBlockUiReadyPatch
{
    private static void Postfix(NHealthBar __instance, Control ____blockContainer)
    {
        PhantomBlockUi.RememberLayout(__instance, ____blockContainer);
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.AnimateInBlock))]
internal static class PhantomBlockUiGainPatch
{
    private static void Prefix(NHealthBar __instance, Creature ____creature,
        Control ____blockContainer, MegaLabel ____blockLabel,
        ref Vector2 ____originalBlockPosition, ref Tween ____blockTween,
        int oldBlock, int blockGain)
    {
        if (!PhantomBlockUi.Prepare(__instance, ____creature, ____blockContainer,
                ref ____originalBlockPosition, ref ____blockTween)) return;

        // BlockChanged runs before the deferred combat UI refresh. Populate
        // the number now, including the first gain and instant-animation mode.
        if (____creature.Block > 0)
            ____blockLabel.SetTextAutoSize(____creature.Block.ToString());
        if (oldBlock == 0 && blockGain != 0)
        {
            ____blockContainer.Position = ____originalBlockPosition;
            ____blockContainer.Modulate = Colors.White;
        }
    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshBlockUi")]
internal static class PhantomBlockUiRefreshPatch
{
    private static void Prefix(NHealthBar __instance, Creature ____creature,
        Control ____blockContainer, ref Vector2 ____originalBlockPosition,
        ref Tween ____blockTween, out bool __state)
    {
        __state = PhantomBlockUi.Prepare(__instance, ____creature,
            ____blockContainer, ref ____originalBlockPosition, ref ____blockTween)
            && ____blockContainer.Visible;
    }

    private static void Postfix(NHealthBar __instance, Control ____blockContainer,
        ref Tween ____blockTween, bool __state)
    {
        if (!__state || ____blockContainer.Visible) return;

        // Native RefreshBlockUi creates the broken shield under NHealthBar.
        // Keep its origin beside HP through subsequent native/Ritsu relayouts,
        // preserving the original fragment animation and automatic cleanup.
        foreach (Node child in __instance.GetChildren())
        {
            if (child is not NBlockBrokenVfx broken) continue;
            broken.Reparent(__instance.HpBarContainer, false);
            broken.Position = ____blockContainer.GetTransform() *
                (____blockContainer.Size * 0.5f);
        }
        ____blockTween?.Kill();
        ____blockTween = null;
    }
}
