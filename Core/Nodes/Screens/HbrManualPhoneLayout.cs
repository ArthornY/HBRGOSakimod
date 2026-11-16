using System;
using System.Collections.Generic;
using System.Globalization;

namespace HBRGOSakiMod.Core.Nodes.Screens;

internal readonly record struct HbrPhoneRect(float X, float Y, float Width, float Height)
{
    internal float Right => X + Width;
    internal float Bottom => Y + Height;
    internal float CentreX => X + Width / 2f;
    internal float CentreY => Y + Height / 2f;
}

internal enum HbrManualHomeButton
{
    Senses,
    Equipment,
    Shop,
    Glossary,
    Cannon,
    FateDice
}

internal enum HbrSenseTier
{
    Black,
    Grey,
    White
}

// The Electronic Soldier Manual as HBR's terminal phone (ui-concepts/manual-phone-20261104), on the
// preview's 1920x1080 canvas. Home and page content use canvas coordinates; the phone screen clips them.
// Pure data, linked by validation/hbr-manual-phone.
internal static class HbrManualPhoneLayout
{
    internal const float CanvasWidth = 1920f;
    internal const float CanvasHeight = 1080f;

    // DenchoFrame's screen spans x 49..933, y 183..1363 of the sprite, drawn at 1270x1564.
    internal static readonly HbrPhoneRect Frame = new(467f, -320f, 1270f, 1564f);
    internal static readonly HbrPhoneRect Screen = new(516f, -137f, 884f, 1180f);
    internal static readonly HbrPhoneRect Hand = new(1414f, 600f, 620f, 744f);
    internal static readonly HbrPhoneRect Flash = new(497f, -157f, 922f, 1220f);
    internal const int FlashCap = 128;
    internal const float FlashPeak = 0.55f;
    internal const float FlashDuration = 0.34f;
    internal const float RigRise = 70f;
    internal const float RigRiseDuration = 0.25f;

    // The run's top bar draws above overlays. Its texture (top_bar.tres, 2046x80, stretched to 100 tall
    // and scaled 1.01) has a torn bottom edge that reaches y 89 over the phone. The rig is scaled so
    // that everything from the portrait's ring (y -5) down to the bezel under the screen (y 1043 + 13)
    // stays below that edge.
    internal const float TopBarInset = 92f;
    internal const float ContentTop = -6f;
    internal const float ContentBottom = 1056f;

    // Scale and top-left of the 1920x1080 rig on a screen of the given size. The rig never grows past
    // 1:1, and when there is room to spare the content is centred in the area below the top bar.
    internal static (float Scale, float X, float Y) RigPlacement(float width, float height)
    {
        float band = ContentBottom - ContentTop;
        float room = height - TopBarInset;
        float scale = Math.Clamp(MathF.Min(width / CanvasWidth, room / band), 0.25f, 1f);
        float x = (width - CanvasWidth * scale) / 2f;
        float y = TopBarInset - ContentTop * scale + MathF.Max(0f, (room - band * scale) / 2f);
        return (scale, x, y);
    }

    // Left-hand "帰還する" plate: ButtonDungeonExit* with IconExit* and the caption.
    internal static readonly HbrPhoneRect Exit = new(140f, 258f, 380f, 172f);
    internal static readonly HbrPhoneRect ExitIcon = new(45f, 41f, 90f, 90f);
    internal static readonly HbrPhoneRect ExitLabel = new(150f, 64f, 160f, 44f);
    internal const int ExitLabelSize = 34;

    // Home.
    internal static readonly HbrPhoneRect Burst = new(628f, 350f, 660f, 660f);
    internal const float BurstPeriod = 160f;
    internal const int BurstLines = 200;
    internal static readonly (float Radius, float Alpha, float Dash, float Gap)[] BurstRings =
        { (118f, 0.5f, 3f, 9f), (196f, 0.4f, 2f, 12f), (282f, 0.3f, 2f, 14f), (318f, 0.18f, 1f, 8f) };
    internal static readonly HbrPhoneRect Omega = new(708f, 330f, 500f, 500f);
    internal const float OmegaAlpha = 0.2f;
    internal static readonly HbrPhoneRect Humans = new(560f, 12f, 320f, 26f);
    internal const string HumansCount = "008065300";
    internal static readonly HbrPhoneRect RankLabel = new(640f, 66f, 90f, 40f);
    internal static readonly HbrPhoneRect Slash = new(722f, 52f, 104f, 68f);
    internal static readonly HbrPhoneRect RankNumber = new(812f, 40f, 200f, 84f);
    internal static readonly HbrPhoneRect UnlockRow = new(648f, 148f, 318f, 36f);
    internal static readonly HbrPhoneRect Gauge = new(648f, 190f, 318f, 16f);
    internal const float GaugeShadow = 14f;
    internal const float CapsuleLeft = 640f;
    internal const float CapsuleWidth = 294f;
    internal const float CapsuleHeight = 52f;
    internal const float CapsuleShadow = 17f;
    internal const float GardenTop = 216f;
    internal const float PointsTop = 284f;
    internal static readonly HbrPhoneRect Portrait = new(1012f, 19f, 340f, 340f);
    internal static readonly HbrPhoneRect PortraitShadow = new(35f, 80f, 315f, 276f);
    internal static readonly HbrPhoneRect PortraitRing = new(-24f, -24f, 388f, 388f);
    internal static readonly HbrPhoneRect PortraitCaption = new(20f, 266f, 300f, 44f);
    internal const int CaptionCap = 22;
    internal const float RingBreathPeriod = 3.2f;

    internal const float RingButtonSize = 178f;
    internal const float RingButtonTop = 450f;
    internal const float RingButtonSpacing = 200f;
    internal static readonly HbrPhoneRect MiniRadar = new(808f, 530f, 300f, 300f);
    internal const float MiniRadius = 90f;
    internal const float MiniLabelRadius = 116f;
    internal const float MiniHalfRadius = 124f;
    internal static readonly HbrPhoneRect Dock = new(548f, 811f, 820f, 202f);
    internal const float DockItemWidth = 124f;
    internal const float DockItemHeight = 170f;
    internal const float DockItemTop = 826f;
    internal const float DockItemSpacing = 132f;
    internal const float ScreenCentreX = 958f;

    // Pages: back button, title or toggle, info on the right, a rule, then an 836x912 body.
    internal static readonly HbrPhoneRect Back = new(540f, 32f, 64f, 64f);
    internal static readonly HbrPhoneRect PageTitle = new(624f, 38f, 560f, 56f);
    internal const float PageInfoRight = 1376f;
    internal const float PageInfoTop = 46f;
    internal static readonly HbrPhoneRect PageLine = new(540f, 112f, 836f, 2f);
    internal static readonly HbrPhoneRect Body = new(540f, 128f, 836f, 912f);

    // Six senses page, in body coordinates.
    internal static readonly HbrPhoneRect Toggle = new(84f, -94f, 280f, 60f);
    internal const float RadarCentreX = 418f;
    internal const float RadarCentreY = 328f;
    internal const float RadarRadius = 236f;
    internal const float RadarLabelRadius = 290f;
    internal const float VertexWidth = 200f;
    internal const float VertexHeight = 74f;
    internal const float PointButtonSize = 44f;
    internal const float LegendTop = 650f;
    internal static readonly HbrPhoneRect SixTitle = new(0f, 690f, 600f, 44f);
    internal static readonly HbrPhoneRect SixDetail = new(0f, 738f, 600f, 166f);
    internal static readonly HbrPhoneRect SixMore = new(444f, 746f, 156f, 36f);
    internal static readonly HbrPhoneRect Confirm = new(624f, 832f, 212f, 72f);

    // Colours.
    internal const string Cyan = "c9f8fa";
    internal const string Cyan2 = "84e9f0";
    internal const string Muted = "9fc3c8";
    internal const string Pink = "f43aa8";
    internal const string Ink = "07101c";

    // Black Saki raises 领袖魅力/才能/疯狂, white Saki 温柔/单纯; 精神力 leans grey.
    // Order follows ElectronicSoldierManual.AllSixSenseAttributes.
    internal static readonly HbrSenseTier[] Tiers =
        { HbrSenseTier.Black, HbrSenseTier.Black, HbrSenseTier.White, HbrSenseTier.Grey, HbrSenseTier.White, HbrSenseTier.Black };
    internal static readonly string[] TierColours = { "d4a020", "b48cff", "ffc9de", "b9bdcf", "bfe6ff", "ff5a6e" };
    internal static readonly string[] Leanings =
    {
        "黑祥侧：分离时 +3（原作称其本身不染色）", "黑祥侧：分离时 +5（原作称其本身不染色）", "白祥侧：分离时 +3",
        "灰：越高越不愿依赖他人", "白祥侧：分离时 +3", "黑祥侧：分离时 +3（源自世界观的“污染”）"
    };

    internal const int Threshold = 30;
    internal const int MaxLevel = 99;
    internal static readonly int[] SenseRings = { 20, 40, 60, 80, 99 };
    internal static readonly int[] MiniRings = { 50, 99 };

    // Axis i points (-90 + 60i) degrees from +x: 领袖魅力 at the top, then clockwise.
    internal static float Angle(int index) => (-90f + index * 60f) * MathF.PI / 180f;

    internal static (float X, float Y) Point(float centreX, float centreY, int index, float radius) =>
        (centreX + MathF.Cos(Angle(index)) * radius, centreY + MathF.Sin(Angle(index)) * radius);

    internal static float RadiusOf(float value, float max, float radius) => radius * Math.Clamp(value, 0f, max) / max;

    // The battle stat view keeps every stat on its sense's axis; its outer ring rounds the largest
    // reachable value (or a larger live one, with equipment and powers) up to 50.
    internal static float StatRadarMax(float baseStat, float gainPerLevel, float largest = 0f) =>
        MathF.Ceiling(MathF.Max(baseStat + MaxLevel * gainPerLevel, largest) / 50f) * 50f;

    internal static float[] StatRings(float max) => new[] { max / 5f, max * 2f / 5f, max * 3f / 5f, max * 4f / 5f, max };

    // Ring buttons sit in one row centred on the screen; five (Hotaru with equipment) shrink to fit.
    internal static float RingButtonScale(int count) => count >= 5 ? 0.88f : 1f;

    internal static float[] RingButtonCentres(int count)
    {
        float spacing = count >= 5 ? 172f : RingButtonSpacing;
        var centres = new float[count];
        for (int i = 0; i < count; i++) centres[i] = ScreenCentreX + (i - (count - 1) / 2f) * spacing;
        return centres;
    }

    internal static float DockItemCentre(int index) => ScreenCentreX + (index - 2.5f) * DockItemSpacing;

    // Dock values show the whole part only: "267.7" in the 52px face is wider than the 132px between
    // items and ran into its neighbour. Digits in hbr_manual_ui.otf advance 0.573 em; four or more
    // digits step the size down so each value keeps a clear gap.
    internal const int DockValueSize = 52;
    internal const float DockDigitAdvance = 0.573f;
    internal const float DockValueGap = 24f;

    internal static string DockValueText(decimal value) =>
        decimal.Truncate(Math.Max(0m, value)).ToString("0", CultureInfo.InvariantCulture);

    internal static int DockValueFontSize(string text)
    {
        float room = DockItemSpacing - DockValueGap;
        float width = Math.Max(1, text.Length) * DockDigitAdvance;
        return (int)Math.Min(DockValueSize, MathF.Floor(room / width));
    }

    // Mini radar labels: top and bottom centred, right side left-aligned, left side right-aligned.
    internal static int MiniLabelSide(int index) => index is 0 or 3 ? 0 : index < 3 ? 1 : -1;

    // Deterministic burst, the preview's Park-Miller generator with seed 7.
    internal static IEnumerable<(float Angle, float From, float To, bool Violet, float Alpha, float Width)> BurstSpokes()
    {
        long seed = 7;
        float Next() => (seed = seed * 16807 % 2147483647) / 2147483647f;
        for (int i = 0; i < BurstLines; i++)
        {
            float angle = i / (float)BurstLines * MathF.PI * 2f + Next() * 0.02f;
            float from = 60f + Next() * 70f;
            float to = from + 40f + Next() * Next() * 240f;
            float alpha = 0.12f + Next() * 0.38f;
            float width = Next() < 0.15f ? 2f : 1f;
            yield return (angle, from, to, i % 3 == 0, alpha, width);
        }
    }

    internal static string Chant(bool hotaru, bool thirtyA) =>
        hotaru ? "只记得，要去塔顶。" : thirtyA ? "「此刻、即是复权之刻。」" : "「千早同学？」";

    internal static string CharacterName(bool hotaru, bool thirtyA) =>
        hotaru ? "萤" : thirtyA ? "丰川祥子（30A）" : "丰川祥子（31O）";

    internal static string HomeButtonTitle(HbrManualHomeButton button) => button switch
    {
        HbrManualHomeButton.Senses => "六感",
        HbrManualHomeButton.Equipment => "装备",
        HbrManualHomeButton.Shop => "商店",
        HbrManualHomeButton.Cannon => "浮游炮",
        HbrManualHomeButton.FateDice => "命运骰子",
        _ => "词条全解"
    };

    internal static string HomeButtonTip(HbrManualHomeButton button) => button switch
    {
        HbrManualHomeButton.Senses => "分配六感点数，查看各项效果",
        HbrManualHomeButton.Equipment => "装备增幅器、芯片和饰品",
        HbrManualHomeButton.Shop => "商店、炼金与出售",
        HbrManualHomeButton.Cannon => "五门浮游炮的升级与通用规则",
        HbrManualHomeButton.FateDice => "装卸命运碎片，查看裂痕和剑心",
        _ => "词条全解（Ctrl+F 搜索）"
    };

    // Ring buttons on home, left to right; equipment and shop belong to the HBR numerics layer,
    // the fate dice to 31O (both modes), the cannons to Hotaru.
    internal static IReadOnlyList<HbrManualHomeButton> HomeButtons(bool equipment, bool cannon, bool fateDice = false)
    {
        var buttons = new List<HbrManualHomeButton> { HbrManualHomeButton.Senses };
        if (equipment)
        {
            buttons.Add(HbrManualHomeButton.Equipment);
            buttons.Add(HbrManualHomeButton.Shop);
        }
        buttons.Add(HbrManualHomeButton.Glossary);
        if (cannon) buttons.Add(HbrManualHomeButton.Cannon);
        if (fateDice) buttons.Add(HbrManualHomeButton.FateDice);
        return buttons;
    }

    // Titles the HBR face subset cannot draw yet: 命运骰子's 命 and 骰 fall back to the locale
    // font until the subset is regenerated, so it stays out of the coverage list below.
    internal static bool UsesLocaleFont(HbrManualHomeButton button) => button == HbrManualHomeButton.FateDice;

    internal const string HumansLabel = "剩余人类计量";
    // The battle level; the sum of the six senses is no longer shown on home.
    internal const string RankText = "等级";
    internal const string NoRank = "-";
    internal const string UnlockText = "兼具";
    internal const string UnlockFrom = "30级起";
    internal const string GardenText = "庭园升级点";
    internal const string PointsText = "六感点数";
    internal const string PortraitTitle = "战斗属性";
    internal const string ExitText = "关闭手册";
    internal const string SensesView = "六感";
    internal const string StatsView = "战斗属性";
    internal const string ConfirmText = "确定";
    internal const string ConfirmingText = "确认中…";
    internal const string PendingText = "待确认";
    internal const string SendingText = "正在确认";
    internal const string MoreText = "战斗属性详情 ›";
    internal const string NoNumerics = "本局未启用 HBR 数值测试";
    internal const string SenseLegend = "虚线：30级兼具　外圈：99级上限　长按 ＋/－ 连续调整";

    internal static string StatLegend(int battleLevel, string baseStat, string max) =>
        $"虚线：战斗等级 Lv.{battleLevel} 基础值 {baseStat}　外圈：{max}　＋/－ 调整对应的六感";

    // Fixed strings drawn in the HBR UI face; long descriptions keep the game's own font.
    internal const string GlyphCoverage =
        "领袖魅力才能温柔精神力单纯疯狂力量灵巧智慧体力精神运气萤丰川祥子（）待确认正在战斗等级基础值外圈虚线：＋－调整对应的六感条" +
        "页面属性详情商店装备词条全解浮游炮返回关闭手册本局未启用数值测试HBR战斗等级" + "0123456789+-./ Lv";

    // Every fixed string the manual chrome draws, for the font subset and its coverage check.
    internal static IEnumerable<string> AllText()
    {
        foreach (string text in new[]
                 {
                     HumansLabel, HumansCount, RankText, NoRank, UnlockText, UnlockFrom, GardenText, PointsText, PortraitTitle,
                     ExitText, SensesView, StatsView, ConfirmText, ConfirmingText, PendingText, SendingText, MoreText,
                     NoNumerics, SenseLegend, StatLegend(12, "144", "350"), GlyphCoverage,
                     Chant(false, false), Chant(false, true), Chant(true, false),
                     CharacterName(false, false), CharacterName(false, true), CharacterName(true, false)
                 })
            yield return text;
        foreach (HbrManualHomeButton button in Enum.GetValues<HbrManualHomeButton>())
            if (!UsesLocaleFont(button))
                yield return HomeButtonTitle(button);
    }
}
