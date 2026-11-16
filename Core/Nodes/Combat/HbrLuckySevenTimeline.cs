using System;
using HBRGOSakiMod.Core.Config;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Magic Sword Lucky Seven's finisher (design v3, section 6; user: 「魔剑的终结技能不能做个动画啊（」, then chose the
// full-screen show). Plain numbers and rules so the battle HUD suite can check them without Godot;
// NHbrLuckySevenFinisher draws them and HbrLuckySevenFinisher sequences them around the card's real hits.
internal static class HbrLuckySevenTimeline
{
    // Seconds of the full-screen show at normal speed.
    internal const float DarkEnd = 0.3f;      // 暗场: the battle dims and the interface fades
    internal const float SummonEnd = 0.9f;    // 召剑: rift, the sword falls and sticks, Saki draws it, title slides in
    internal const float SlashEnd = 1.7f;     // 斩击: nominal length; the card's real hits pace the slashes
    internal const float FinishEnd = 2.1f;    // 收刀: white flash, the combined total, the screen returns
    internal const float FinishLength = FinishEnd - SlashEnd;

    // Beats inside the summon.
    internal const float RiftOpenEnd = 0.46f;
    internal const float SwordFallStart = 0.42f;
    internal const float SwordLand = 0.6f;
    internal const float DrawStart = 0.66f;
    internal const float DrawEnd = 0.84f;
    internal const float VeinsPeak = 0.8f;
    internal const float SakiIn = 0.5f;
    internal const float SakiInEnd = 0.7f;
    internal const float TitleStart = 0.5f;
    internal const float TitleEnd = 0.78f;

    // The sword icon (HbrMagicSwordArt.SwordIcon) is a painted square: red and blue flames around a sword that runs
    // from its pommel near the bottom-left to its tip near the top-right. Drawn whole, even additively, the flames
    // show as a glowing square, so the finisher keeps a soft band along the blade and drops the rest. Texture
    // coordinates, y down; a final icon with another pose changes these five numbers.
    internal const float SwordTailU = 0.30f, SwordTailV = 0.98f;
    internal const float SwordTipU = 0.87f, SwordTipV = 0.05f;
    internal const float SwordHiltHalfWidth = 0.13f;   // across the guard and the ring hilt
    internal const float SwordTipHalfWidth = 0.05f;
    internal const float SwordMaskSoftness = 0.05f;
    // While stuck, the sword's centre stays this share of its size above the landing point, so its tip, not the
    // middle of the blade, is in the ground where the shock ring spreads.
    internal const float SwordLandLift = 0.45f;

    internal const float SummonDim = 0.78f;
    internal const float SlashDim = 0.5f;
    internal const float SlashDimBlend = 0.15f;
    internal const float SkipFade = 0.12f;

    internal const int MaxSlashes = 7;
    internal const float SlashLife = 0.34f;
    internal const float BigSlashLife = 0.55f;
    internal const float SlashLength = 440f;
    internal const float BigSlashLength = 980f;
    internal const float SlashWidth = 9f;
    internal const float BigSlashWidth = 22f;

    internal const float FlashPeak = 0.1f;    // seconds into the finish
    internal const float FlashAlpha = 0.85f;
    internal const float TotalPop = 0.16f;
    internal const float TotalPopScale = 1.6f;
    internal const float TotalHold = 1.1f;
    internal const float TotalFade = 0.35f;

    // FastModeType.Fast plays the show this much faster; Instant never waits, so it gets the simplified show.
    internal const float FastSpeed = 1.6f;
    // Upper bounds of the two waits around the card, so no stalled frame or freed node can hold the action.
    internal const float MaxIntroWait = 3f;
    internal const float MaxFinishWait = 2f;

    internal const string CrimsonHex = "E2243F";
    internal const string VioletHex = "A05CFF";
    internal const string RiftHex = "0B0614";
    internal const string Title = "必杀剑·魔剑幸运七";
    internal const string TotalCaption = "总伤害";
    internal const int TitleSize = 64;
    internal const int TotalSize = 96;
    internal const int CaptionSize = 30;

    // Layout as fractions of the screen: the rift high above the sword's landing point, Saki to its left, the
    // title band on the right, the full-screen total in the right half.
    internal const float RiftX = 0.42f, RiftY = 0.2f;
    internal const float GroundX = 0.42f, GroundY = 0.63f;
    internal const float HeldX = 0.35f, HeldY = 0.42f;
    internal const float SwordSize = 0.46f;          // of the screen height
    internal const float SakiLeft = 0.08f, SakiTop = 0.13f, SakiHeight = 0.87f;
    internal const float TitleTop = 0.24f, TitleHeight = 0.13f, TitleLeft = 0.5f;
    // The total's box (caption over number) at 1080p; centred low enough that even popped to TotalPopScale it
    // clears the title band, which is still fading out when the total appears.
    internal const float TotalBoxWidth = 640f, TotalBoxHeight = 190f;
    internal const float TotalX = 0.66f, TotalY = 0.53f;
    internal const float SimplifiedTotalRise = 150f;

    // Which show this machine plays. The setting has 「全屏、简化、关闭」, default full screen. A teammate's
    // finisher never covers this screen, and a machine that cannot wait (tests, headless, instant fast mode)
    // never blocks the card.
    internal static HbrFinisherPresentation Resolve(HbrFinisherPresentation setting, bool ownerIsLocal,
        bool interactive, bool instantFastMode)
    {
        if (!interactive || setting == HbrFinisherPresentation.Off) return HbrFinisherPresentation.Off;
        if (!ownerIsLocal) return HbrFinisherPresentation.Simplified;
        if (instantFastMode && setting == HbrFinisherPresentation.Fullscreen) return HbrFinisherPresentation.Simplified;
        return setting;
    }

    // 「斩光道数和这次花掉的剑心一致，超过 7 段时，后面几段合成一道大斩」: the first six hits start a slash each,
    // the seventh starts one big slash when more hits follow, and the hits after it fold into it.
    internal static int SlashCount(int hits) => Math.Clamp(hits, 0, MaxSlashes);
    internal static bool StartsSlash(int hit) => hit >= 0 && hit < MaxSlashes;
    internal static bool IsBigSlash(int hits, int slash) => hits > MaxSlashes && slash == MaxSlashes - 1;
    internal static string SlashColor(int slash) => slash % 2 == 0 ? CrimsonHex : VioletHex;

    // Alternating directions in degrees so successive cuts cross; the big slash is a long low diagonal.
    private static readonly float[] Angles = { -28f, 24f, -12f, 38f, -42f, 14f, -20f };
    internal static float SlashAngle(int slash, bool big) => big ? -16f : Angles[Math.Clamp(slash, 0, MaxSlashes - 1)];
    // Small offsets around the target so the cuts do not all pass through one point.
    private static readonly (float X, float Y)[] Offsets =
        { (0f, 0f), (-22f, 18f), (26f, -14f), (-10f, -26f), (18f, 24f), (-30f, -6f), (0f, 8f) };
    internal static (float X, float Y) SlashOffset(int slash) => Offsets[Math.Clamp(slash, 0, MaxSlashes - 1)];

    // Darkness over the summon: rises during 暗场 and holds.
    internal static float SummonDarkness(float t) => Ramp(t, 0f, DarkEnd) * SummonDim;

    // White flash of 收刀, u seconds into the finish: up to the peak, then back to nothing by the end.
    internal static float Flash(float u) =>
        u <= 0f ? 0f
        : u < FlashPeak ? FlashAlpha * u / FlashPeak
        : FlashAlpha * (1f - Ramp(u, FlashPeak, FinishLength));

    // The darkness lifts over the finish, so the screen is back when 收刀 ends.
    internal static float FinishDarkness(float u, float from) => from * (1f - Ramp(u, 0f, FinishLength));

    // The blade's direction on the icon in degrees, screen convention (y down): about -58.5, up and to the right.
    internal static float SwordTipAngle =>
        MathF.Atan2(SwordTipV - SwordTailV, SwordTipU - SwordTailU) * 180f / MathF.PI;

    // The rotation that points the tip straight down while the sword falls and stands in the ground; drawn, it
    // returns to 0 and the blade is raised at the icon's own angle.
    internal static float FallRotation => 90f - SwordTipAngle;

    // How much of the icon's colour the finisher keeps at texture coordinate (u, v): 1 on the blade, 0 away
    // from it, soft in between. The band narrows from the hilt to the tip and fades past both ends.
    internal static float SwordMask(float u, float v)
    {
        float dx = SwordTipU - SwordTailU, dy = SwordTipV - SwordTailV;
        float lengthSquared = dx * dx + dy * dy;
        float along = ((u - SwordTailU) * dx + (v - SwordTailV) * dy) / lengthSquared;
        float across = MathF.Abs((u - SwordTailU) * dy - (v - SwordTailV) * dx) / MathF.Sqrt(lengthSquared);
        float half = SwordHiltHalfWidth + (SwordTipHalfWidth - SwordHiltHalfWidth) * Math.Clamp(along, 0f, 1f);
        float side = 1f - Ramp(across, half, half + SwordMaskSoftness);
        float ends = Ramp(along, -0.04f, 0.02f) * (1f - Ramp(along, 1f, 1.05f));
        return side * ends;
    }

    // Sword: falls with gravity, then rises into Saki's hand.
    internal static float SwordFall(float t)
    {
        float p = Ramp(t, SwordFallStart, SwordLand);
        return p * p;
    }

    internal static float SwordDraw(float t) => Smooth(Ramp(t, DrawStart, DrawEnd));

    internal static float Veins(float t) =>
        t < DrawStart ? 0f
        : t < VeinsPeak ? Ramp(t, DrawStart, VeinsPeak)
        : 1f - 0.45f * Ramp(t, VeinsPeak, SummonEnd);

    internal static float RiftOpen(float t) => Smooth(Ramp(t, DarkEnd, RiftOpenEnd));

    internal static float SakiAlpha(float t) => Smooth(Ramp(t, SakiIn, SakiInEnd));

    // Title slides in from the right edge with an ease-out.
    internal static float TitleSlide(float t)
    {
        float p = Ramp(t, TitleStart, TitleEnd);
        return 1f - (1f - p) * (1f - p) * (1f - p);
    }

    // The total pops in large and settles, holds, then fades.
    internal static float TotalScale(float v)
    {
        float p = Ramp(v, 0f, TotalPop);
        return TotalPopScale + (1f - TotalPopScale) * (1f - (1f - p) * (1f - p));
    }

    internal static float TotalAlpha(float v) => 1f - Ramp(v, TotalHold, TotalHold + TotalFade);

    internal static float TotalLife => TotalHold + TotalFade;

    internal static float Ramp(float t, float from, float to) =>
        to <= from ? (t >= to ? 1f : 0f) : Math.Clamp((t - from) / (to - from), 0f, 1f);

    internal static float Smooth(float p) => p * p * (3f - 2f * p);
}
