using System;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Nodes.Combat;

internal enum HbrPileKind
{
    Draw,
    Discard,
    Exhaust
}

internal enum HbrTurnBannerKind
{
    Player,
    Enemy,
    Extra
}

internal readonly record struct HbrHudRect(float X, float Y, float Width, float Height)
{
    internal float Right => X + Width;
    internal float Bottom => Y + Height;

    internal bool Overlaps(HbrHudRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

// The HBR battle HUD (ui-concepts/battle-hud-practical-20261104) on the 1920x1080
// canvas. Positions keep the preview's anchors: right-side controls hang from the
// right edge, the rest from the left, and everything low on screen from the bottom.
// Pure data, so validation/hbr-battle-hud can link it without Godot.
internal static class HbrBattleHudLayout
{
    internal const float ReferenceWidth = 1920f;
    internal const float ReferenceHeight = 1080f;

    // Bottom fade behind the HUD: transparent at the top, .36 at 45%, .72 at the bottom.
    internal const float GradientHeight = 300f;
    internal const string GradientColor = "08050e";
    internal static readonly (float Offset, float Alpha)[] GradientStops = { (0f, 0f), (0.45f, 0.36f), (1f, 0.72f) };

    // End turn disc: UIBattleAttack "Start Action", BtnCmdStart* (352px) at 0.82 in a 230x230 hit box.
    internal const float DiscSize = 230f;
    internal const float DiscFromRight = 305f;
    internal const float DiscFromBottom = 280f;
    internal const float DiscTextureSize = 289f;
    internal const float DiscAuraSize = 242f;
    internal const float DiscHideDrop = 70f;
    internal const float DiscHiddenScale = 0.9f;
    internal const float DiscHoverScale = 1.04f;
    internal const float DiscPressScale = 0.95f;
    internal const float DiscAuraPeriod = 1.6f;
    internal const float DiscAuraFromScale = 0.9f;
    internal const float DiscAuraToScale = 1.34f;
    internal const float DiscAuraFromAlpha = 0.9f;
    internal const float DiscVotesTop = -24f;
    // Long press: a ring inset 8px, from 91% to 99% of its radius.
    internal const float RingInset = 8f;
    internal const float RingInner = 0.91f;
    internal const float RingOuter = 0.99f;
    internal const string RingTrackColor = "ffffff24";
    internal const string RingGlowColor = "ff6fd8";
    // The native NEndTurnLongPressBar grows to this width over its press duration.
    internal const float NativeLongPressWidth = 204f;

    internal const int DiscLabelSize = 31;
    internal const int DiscSubSize = 15;
    internal const int DiscWarnSize = 16;
    internal const float DiscSubGap = 8f;
    internal const string DiscLabelColor = "ffffff";
    internal const string DiscLabelOutline = "3a0d3ad9";
    internal const string DiscGlowColor = "ff6fd8cc";
    internal const string DiscSubColor = "fff0faeb";
    internal const string DiscWarnColor = "ffc24a";
    internal const string DisabledTextColor = "9d9d9d";

    // Piles: HBR tool circles with TerminalIcon glyphs and the remaining-count badge.
    internal const float PileSize = 80f;
    internal const float PileHoverScale = 1.08f;
    internal const float PilePressScale = 0.94f;
    internal const float PileBumpScale = 1.2f;
    internal const float CountBumpScale = 1.35f;
    internal const float ExhaustHideShift = 70f;
    internal const float EmptyIconAlpha = 0.55f;
    internal static readonly HbrHudRect PileGlow = new(-22f, -22f, 124f, 124f);
    internal static readonly HbrHudRect PileDisc = new(11f, 11f, 58f, 58f);
    internal static readonly HbrHudRect PileRing = new(10f, 10f, 60f, 60f);
    internal static readonly HbrHudRect PileCount = new(50f, -6f, 34f, 34f);
    internal const float CountDiscInset = 1f;
    internal const int CountTextSize = 19;
    internal const string PileGlowColor = "ff6fd8";
    internal const float PileGlowHoverAlpha = 0.75f;
    internal const string PileDiscColor = "221830e6";
    internal const string PileDiscPressedColor = "c94aa6";
    internal const string PileRingColor = "d8d8da";
    internal const string PileRingHoverColor = "ffd0f0";
    // UIBattleAttack remainingCount: circle1 tinted (0.647, 0.275, 0.839), gaugeCircleOff01 (0.859, 0.867, 0.871).
    internal const string CountDiscColor = "a546d6";
    internal const string ExhaustCountDiscColor = "cd4096";
    internal const string EmptyCountDiscColor = "57525c";
    internal const string CountRingColor = "dbddde";
    internal const string EmptyCountTextColor = "d6d0d8";

    // TURN indicator: UIBattleTurn "TURN n" over MordaTitleLineEdge with a dot10 end, tinted 0.83 grey.
    internal const float TurnLeft = 70f;
    internal const float TurnFromBottom = 470f;
    internal const float TurnWidth = 260f;
    internal const float TurnHeight = 124f;
    internal const float TurnRowHeight = 60f;
    internal const float TurnRowTop = 64f;
    internal const float TurnTextLeft = 25f;
    internal const float TurnTextTop = 2f;
    internal const int TurnWordSize = 25;
    internal const int TurnNumberSize = 40;
    internal const float TurnNumberGap = 6f;
    internal const float TurnLineTop = 44f;
    internal const float TurnLineWidth = 220f;
    internal const float TurnLineHeight = 10f;
    internal const int TurnLineLeftCap = 2;
    internal const int TurnLineRightCap = 55;
    internal const float TurnLineBrightness = 0.83f;
    internal const string TurnDotColor = "d4d4d4e3";
    internal const string ExtraTextColor = "ffd98a";

    // Roll history: the HBR AUTO capsule, ButtonBattleAutoOnOff* (192x84, 80px caps) drawn 64px tall.
    internal const float RollWidth = 222f;
    internal const float RollHeight = 64f;
    internal const float RollFromRight = 272f;
    internal const float RollTop = 352f;
    internal const float RollSourceHeight = 84f;
    internal const int RollSourceCap = 80;
    internal const float RollTextGap = 12f;
    internal const int RollTitleSize = 20;
    internal const int RollCountSize = 26;
    internal const string RollCountColor = "c4f0ff";
    internal const float RollAmnesiaBrightness = 0.72f;

    // Turn banners: UIDuelTurnChange at 0.55, centred 46% down the screen.
    internal const float BannerHeight = 300f;
    internal const float BannerCentre = 0.46f;
    internal const float BandTop = 41f;
    internal const float BandHeight = 218f;
    internal static readonly HbrHudRect BannerDuel = new(-386f, -10f, 304f, 238f);
    // DuelModalBG's own left fade is short and steep; at 0.55 it read as a hard edge. The block's
    // opacity now rises with smoothstep from its left edge and is untouched from this fraction on.
    internal const float BannerDuelFadeEnd = 0.6f;
    internal static readonly HbrHudRect BannerHalftone = new(-142f, 0f, 574f, 218f);
    internal const float BannerFadeLeft = 430f;
    internal const float BannerFadeStop = 0.6f;
    internal const float BannerShade = 0.72f;
    internal const float BannerLineLeft = -468f;
    internal const float BannerLineWidth = 935f;
    internal const float BannerLineHeight = 28f;
    internal const float BannerLineTopY = 27f;
    internal const float BannerLineBottomY = 245f;
    internal const float BannerLineEndWidth = 150f;
    internal const int BannerLineCap = 5;
    internal const float BannerTitleTop = 98f;
    internal const float BannerTitleHeight = 90f;
    internal const int BannerTitleSize = 74;
    internal const float BannerNumberTop = 190f;
    internal const int BannerNumberWordSize = 22;
    internal const int BannerNumberSize = 30;
    internal const string EnemyTitleColor = "ff4e68";

    // Native durations: player 1.5s entrance, 0.4s hold, 0.3s fade (2.2s); enemy 1.3s, then 1.0s tint and fade (2.3s).
    internal const float BannerFadeIn = 0.25f;
    internal const float BannerWipe = 0.5f;
    internal const float BannerLines = 0.6f;
    internal const float PlayerTitleSlide = 1.0f;
    internal const float PlayerTitleShift = 60f;
    internal const float PlayerNumberSlide = 1.5f;
    internal const float PlayerNumberShift = -14f;
    internal const float PlayerHold = 0.4f;
    internal const float PlayerFadeOut = 0.3f;
    internal const float EnemyTitleIn = 0.75f;
    internal const float EnemyTitleFromScale = 1.6f;
    internal const float EnemyHold = 0.55f;
    internal const float EnemyFadeOut = 1.0f;

    internal static float PlayerBannerDuration => PlayerNumberSlide + PlayerHold + PlayerFadeOut;
    internal static float EnemyBannerDuration => EnemyTitleIn + EnemyHold + EnemyFadeOut;

    // 31O's magic sword switch (design v3, section 5): a round medal right of the energy orb, its swordheart
    // pill below. Clear of the orb, the draw pile, the turn indicator and a resting ten-card hand.
    internal const float MagicSwordLeft = 212f;
    internal const float MagicSwordFromBottom = 300f;
    internal const float MagicSwordMedal = 96f;
    internal const float MagicSwordPillHeight = 28f;
    internal const float MagicSwordPillGap = 4f;
    internal const float MagicSwordRingWidth = 5f;

    internal static HbrHudRect MagicSwordButton(float height) =>
        new(MagicSwordLeft, height - MagicSwordFromBottom, MagicSwordMedal,
            MagicSwordMedal + MagicSwordPillGap + MagicSwordPillHeight);

    // A separate slot for 31C, including when a 31O player owns both bonds.
    internal const float MachinaLeft = 326f;
    internal const float MachinaFromBottom = 300f;
    internal const float MachinaSize = 110f;
    internal const float MachinaFooterHeight = 28f;
    internal const float MachinaFooterGap = 4f;
    internal static HbrHudRect MachinaButton(float height) =>
        new(MachinaLeft, height - MachinaFromBottom, MachinaSize,
            MachinaSize + MachinaFooterGap + MachinaFooterHeight);

    // Context widgets this layout must stay clear of (unchanged by the HUD).
    internal static HbrHudRect OverdriveGauge(float width) => new(width - 430f, 236f, 380f, 102f);
    internal static HbrHudRect EnergyCounter(float height) => new(25f, height - 327f, 150f, 150f);
    internal static HbrHudRect DamageHistory(float width) =>
        new(RollHistory(width).Right - 330f, RollHistory(width).Bottom + 10f, 330f, 104f);

    internal static HbrHudRect EndTurnDisc(float width, float height) =>
        new(width - DiscFromRight, height - DiscFromBottom, DiscSize, DiscSize);

    internal static HbrHudRect Pile(HbrPileKind kind, float width, float height) => kind switch
    {
        HbrPileKind.Draw => new(35f, height - 150f, PileSize, PileSize),
        HbrPileKind.Discard => new(width - 405f, height - 150f, PileSize, PileSize),
        _ => new(width - 405f, height - 280f, PileSize, PileSize)
    };

    // Anchored offsets of each pile inside the full-rect pile container: draw hangs from the
    // bottom-left corner, discard and exhaust from the bottom-right.
    internal static HbrHudRect PileOffsets(HbrPileKind kind) => kind switch
    {
        HbrPileKind.Draw => new(35f, -150f, PileSize, PileSize),
        HbrPileKind.Discard => new(-405f, -150f, PileSize, PileSize),
        _ => new(-405f, -280f, PileSize, PileSize)
    };

    // Where each pile slides from: draw and discard keep the native diagonal, but the discard
    // pile now sits beside the disc, so it drops below the screen instead of sliding under it.
    internal static (float X, float Y) PileHideShift(HbrPileKind kind) => kind switch
    {
        HbrPileKind.Draw => (-150f, 100f),
        HbrPileKind.Discard => (0f, 160f),
        _ => (ExhaustHideShift, 0f)
    };

    internal static HbrHudRect TurnIndicator(float height) =>
        new(TurnLeft, height - TurnFromBottom, TurnWidth, TurnHeight);

    internal static HbrHudRect RollHistory(float width) =>
        new(width - RollFromRight, RollTop, RollWidth, RollHeight);

    internal static float BannerTop(float height) => height * BannerCentre - BannerHeight / 2f;

    // NinePatchRect keeps its caps at source size, so the capsule is laid out at source scale.
    internal static float RollScale => RollHeight / RollSourceHeight;

    internal static float PileIconSize(HbrPileKind kind) => kind switch
    {
        HbrPileKind.Discard => 36f,
        HbrPileKind.Exhaust => 46f,
        _ => 40f
    };

    internal static string PileIconPath(HbrPileKind kind, bool hovered, bool empty) => kind switch
    {
        HbrPileKind.Draw => hovered ? "IconCardGameActive" : empty ? "IconCardGameDisable" : "IconCardGameDefault",
        HbrPileKind.Discard => hovered ? "IconExitActive" : empty ? "IconExitDisable" : "IconExitDefault",
        _ => "IconCemetery"
    };

    internal static string CountDisc(HbrPileKind kind, bool empty) =>
        empty ? EmptyCountDiscColor : kind == HbrPileKind.Exhaust ? ExhaustCountDiscColor : CountDiscColor;

    internal static float PileIconAlpha(bool hovered, bool empty) => empty && !hovered ? EmptyIconAlpha : 1f;

    // A conic ring drawn from 12 o'clock, clockwise, as the native bar fills.
    internal static float LongPressProgress(float nativeBarWidth) =>
        Math.Clamp(nativeBarWidth / NativeLongPressWidth, 0f, 1f);

    internal static string BannerTexture(string name, HbrTurnBannerKind kind) => kind switch
    {
        HbrTurnBannerKind.Enemy => name + "_enemy",
        HbrTurnBannerKind.Extra => name + "_extra",
        _ => name
    };

    // The HUD keeps the preview's Chinese wording, like the mod's other combat widgets.
    internal const string EndTurnLabel = "结束回合";
    internal const string EndTurnSub = "END TURN";
    internal const string EndTurnWarn = "仍有可打出的牌";
    internal const string UndoLabel = "取消";
    internal const string RollTitle = "出目历史";
    internal const string RollHidden = "？";
    internal const string TurnWord = "TURN";
    internal const string ExtraWord = "EXTRA";
    internal const string EnemyTurnSub = "ENEMY TURN";

    internal static string WaitingText(int ready, int total) => $"等待队友 {ready}/{total}";

    internal static string BannerTitle(HbrTurnBannerKind kind) => kind switch
    {
        HbrTurnBannerKind.Enemy => "敌方回合",
        HbrTurnBannerKind.Extra => "额外回合",
        _ => "玩家回合"
    };

    // Every string the HUD can draw, for the font subset and its coverage check.
    internal static IEnumerable<string> AllText()
    {
        yield return EndTurnLabel;
        yield return EndTurnSub;
        yield return EndTurnWarn;
        yield return UndoLabel;
        yield return WaitingText(0, 0);
        yield return RollTitle;
        yield return RollHidden;
        yield return TurnWord;
        yield return ExtraWord;
        yield return EnemyTurnSub;
        foreach (HbrTurnBannerKind kind in Enum.GetValues<HbrTurnBannerKind>())
            yield return BannerTitle(kind);
        yield return "0123456789/";
    }
}
