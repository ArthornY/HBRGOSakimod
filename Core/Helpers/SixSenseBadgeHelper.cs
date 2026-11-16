using System;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// The six sense emblems, now used only by the manual's glossary entries. The card
/// badges and the enemy resistance dots they once drew were removed on 2026-11-09.
/// </summary>
internal static class SixSenseBadgeHelper
{
    internal const string AtlasPath = "res://images/ui/six_sense_badges.png";

    internal static Rect2 AtlasRegion(SixSenseAttribute attribute)
    {
        int index = (int)attribute;
        if (index < 0 || index >= 6) throw new ArgumentOutOfRangeException(nameof(attribute));
        // The region stays inside the emblem's silver rim.
        return new Rect2(160 + index % 3 * 448, 104 + index / 3 * 450, 316, 362);
    }
}
