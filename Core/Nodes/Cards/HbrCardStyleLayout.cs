using System;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// The HBR card style from ui-concepts/card-style-20261104. Plain data, so validation/hbr-card-style can check
// paths and geometry without Godot. Rects are in NCard space: origin at the card centre, vanilla Frame (-150, -211)-(150, 211).
internal enum HbrCardTheme { Saki31O, Saki30A, Phantom, Hotaru, Memory }

internal enum HbrCardTier { Common, Uncommon, Rare }

internal enum HbrCardKind { Attack, Skill, Power }

internal readonly record struct HbrCardRect(float Left, float Top, float Right, float Bottom)
{
    internal float Width => Right - Left;
    internal float Height => Bottom - Top;
}


internal static class HbrCardStyleLayout
{
    internal const string TextureDirectory = "res://images/ui/hbr_card_style/";

    internal static readonly HbrCardRect Frame = new(-150, -211, 150, 211);
    // The frame's see-through portrait window. The portrait overshoots it by 2px so no seam can open at the edge.
    internal static readonly HbrCardRect ArtWindow = new(-136, -152, 136, 54);
    internal static readonly HbrCardRect Portrait = new(-138, -154, 138, 56);
    internal static readonly HbrCardRect TitleBand = new(-106, -198, 138, -158);
    internal static readonly HbrCardRect Title = new(-88, -204, 124, -150);
    internal static readonly HbrCardRect Description = new(-128, 62, 128, 184);
    // Phantom choice cards: blue flame outline drawn behind the card, 24px past the frame on every side.
    internal static readonly HbrCardRect PhantomGlow = new(-174, -235, 174, 235);

    // Type plate below the portrait's left edge. Its width follows the label: the coloured bar in front,
    // room for the slant behind. The user asked for no attribute icon (斩/打/无) in front of the type name.
    internal const float PlateLeft = -136;
    internal const float PlateTop = 28;
    internal const float PlateHeight = 30;
    internal const float PlateLabelLeft = 12;
    internal const float PlateLabelRight = 12;
    internal const float PlateSlant = 10;
    internal const float PlateBarWidth = 2;
    internal const string PlateColor = "0a0614e0";

    internal const string DescriptionColor = "f2ebf7";

    internal static string FramePath(HbrCardTheme theme, HbrCardTier tier) =>
        $"{TextureDirectory}frame_{FrameKey(theme)}_{TierKey(tier)}.png";

    internal static string PhantomGlowPath => TextureDirectory + "phantom_glow.png";

    // Phantom cards wear the 30A frame; the flame outline is a separate layer.
    internal static string FrameKey(HbrCardTheme theme) => theme switch
    {
        HbrCardTheme.Saki31O => "31o",
        HbrCardTheme.Saki30A or HbrCardTheme.Phantom => "30a",
        HbrCardTheme.Hotaru => "hotaru",
        HbrCardTheme.Memory => "memory",
        _ => throw new ArgumentOutOfRangeException(nameof(theme))
    };

    internal static string TierKey(HbrCardTier tier) => tier switch
    {
        HbrCardTier.Common => "common",
        HbrCardTier.Uncommon => "uncommon",
        HbrCardTier.Rare => "rare",
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };

    // Left bar of the type plate: attack red, skill blue, power purple.
    internal static string KindAccent(HbrCardKind kind) => kind switch
    {
        HbrCardKind.Attack => "ff7a92",
        HbrCardKind.Skill => "6fc1ff",
        HbrCardKind.Power => "d39bff",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static float PlateWidth(float labelWidth) => PlateLabelLeft + Math.Max(0, labelWidth) + PlateLabelRight;
}
