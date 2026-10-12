using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomPortraitSpineSlots
{
    // Keep the upper arm, forearm, claw, and its clipping slots. Bones and IK remain
    // intact: some claw bones do not inherit scale, so hiding their parent is insufficient.
    private static readonly HashSet<string> RightClawSlots = new(StringComparer.Ordinal)
    {
        "arm_r_bottom", "arm_r_top", "claw_hole", "arm_r_clipper", "thruster_attach",
        "claw_r_3", "claw_slime_base", "claw_r_2", "claw_slime_lower", "claw_r_1",
        "claw_slime", "arm_r_clip_target", "claw_hole_edge_mask"
    };

    private static readonly HashSet<string> LeftClawSlots = new(StringComparer.Ordinal)
    {
        "arm_l_bottom", "arm_l_middle", "arm_l_clipper", "arm_l_clip_targ",
        "arm_l_top", "claw_l_2", "claw_shine_lower", "claw_l_1", "claw_shine_upper"
    };

    internal static void KeepKaiserClaw(MegaSprite spine, bool right)
    {
        HashSet<string> keptSlots = right ? RightClawSlots : LeftClawSlots;
        FilterSlots(spine, name => !keptSlots.Contains(name));
    }

    internal static void HideSlots(MegaSprite spine, params string[] names)
    {
        var hiddenSlots = new HashSet<string>(names, StringComparer.Ordinal);
        FilterSlots(spine, hiddenSlots.Contains);
    }

    private static void FilterSlots(MegaSprite spine, Predicate<string> shouldHide)
    {
        MegaSkeleton skeleton = spine.GetSkeleton();
        var binding = new SkeletonSlotsBinding(skeleton.BoundObject);
        var hidden = new List<SlotBinding>();
        foreach (SlotBinding slot in binding.GetSlots())
        {
            if (shouldHide(slot.GetName())) hidden.Add(slot);
        }

        void ClearAttachments()
        {
            foreach (SlotBinding slot in hidden) slot.ClearAttachment();
            // Keep the owner and every native wrapper rooted through the complete native call.
            GC.KeepAlive(hidden);
            GC.KeepAlive(spine);
        }

        // Animation timelines can restore attachments. Clear them after animation application
        // on every update; null attachments also disappear from Spine's geometry bounds.
        spine.ConnectBeforeWorldTransformsChange(Callable.From<GodotObject>(_ => ClearAttachments()));
        ClearAttachments();
        GC.KeepAlive(binding);
        GC.KeepAlive(skeleton);
    }

    private sealed class SkeletonSlotsBinding : MegaSpineBinding
    {
        private static readonly string[] Methods = { "get_slots" };
        protected override string SpineClassName => "SpineSkeleton";
        protected override IEnumerable<string> SpineMethods => Methods;

        internal SkeletonSlotsBinding(GodotObject native) : base(native) { }

        internal List<SlotBinding> GetSlots()
        {
            using Godot.Collections.Array slots = Call("get_slots").AsGodotArray();
            var result = new List<SlotBinding>(slots.Count);
            foreach (Variant slot in slots) result.Add(new SlotBinding(slot));
            GC.KeepAlive(this);
            return result;
        }
    }

    private sealed class SlotBinding : MegaSpineBinding
    {
        private static readonly string[] Methods = { "get_data", "set_attachment" };
        protected override string SpineClassName => "SpineSlot";
        protected override IEnumerable<string> SpineMethods => Methods;

        internal SlotBinding(Variant native) : base(native) { }

        internal string GetName()
        {
            var data = new SlotDataBinding(Call("get_data"));
            string name = data.GetName();
            GC.KeepAlive(data);
            GC.KeepAlive(this);
            return name;
        }

        internal void ClearAttachment()
        {
            Call("set_attachment", default(Variant));
            GC.KeepAlive(this);
        }
    }

    private sealed class SlotDataBinding : MegaSpineBinding
    {
        private static readonly string[] Methods = { "get_name" };
        protected override string SpineClassName => "SpineSlotData";
        protected override IEnumerable<string> SpineMethods => Methods;

        internal SlotDataBinding(Variant native) : base(native) { }

        internal string GetName()
        {
            string name = Call("get_name").AsString();
            GC.KeepAlive(this);
            return name;
        }
    }
}
