using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Compatibility;

// Center was a below-owner layout, not a numeric horizontal alignment. v0.113.1
// replaces that enum member with a separate native operation.
internal static class NativeHoverTipCompatibility
{
    private static readonly HoverTipAlignment? LegacyCenter =
        Enum.TryParse("Center", out HoverTipAlignment center) ? center : null;
    internal static readonly MethodInfo BelowMethod = typeof(NHoverTipSet).GetMethod(
        "SetAlignmentBelow", BindingFlags.Instance | BindingFlags.Public,
        null, new[] { typeof(Vector2) }, null);
    private static readonly Action<NHoverTipSet, Vector2> NativeBelow = BelowMethod == null ? null :
        (Action<NHoverTipSet, Vector2>)BelowMethod.CreateDelegate(typeof(Action<NHoverTipSet, Vector2>));
    private static readonly ConditionalWeakTable<NHoverTipSet, object> BelowTips = new();

    internal static bool IsBelow(NHoverTipSet tips, HoverTipAlignment alignment) =>
        LegacyCenter is { } center && alignment == center ||
        tips != null && BelowTips.TryGetValue(tips, out _);

    internal static void RecordBelow(NHoverTipSet tips)
    { if (tips != null) BelowTips.GetValue(tips, _ => new object()); }

    internal static NHoverTipSet CreateAndShowBelow(Control owner, IEnumerable<IHoverTip> hoverTips)
    {
        if (NativeBelow == null)
            return NHoverTipSet.CreateAndShow(owner, hoverTips, RequireLegacyCenter());
        NHoverTipSet tips = NHoverTipSet.CreateAndShow(owner, hoverTips, HoverTipAlignment.None);
        if (tips != null)
        {
            // Preserve the old Center anchor exactly; owner scale was not part
            // of its native vertical offset. Potion refresh has its own anchor.
            NativeBelow(tips, owner.GlobalPosition + Vector2.Down * owner.Size.Y * 1.5f);
            RecordBelow(tips);
        }
        return tips;
    }

    internal static void PositionBelowAt(NHoverTipSet tips, Vector2 position,
        Control textContainer, NHoverTipCardContainer cards)
    {
        if (NativeBelow != null)
        {
            NativeBelow(tips, position);
            RecordBelow(tips);
            return;
        }
        textContainer.Position = Vector2.Zero;
        tips.GlobalPosition = position;
        if (cards == null) return;
        // Legacy Center only sizes cards. Reset their local origin before the
        // native call so refreshed descriptions never accumulate text height.
        cards.Position = Vector2.Down * textContainer.Size.Y;
        cards.LayoutResizeAndReposition(cards.GlobalPosition, RequireLegacyCenter());
    }

    private static HoverTipAlignment RequireLegacyCenter() => LegacyCenter ??
        throw new MissingMethodException(typeof(NHoverTipSet).FullName, "Center / SetAlignmentBelow(Vector2)");
}
