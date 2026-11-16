using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HBRGOSakiMod.Core.Nodes.Screens;

// Checks for the phone manual (ui-concepts/manual-phone-20261104). Usage: Checks <repo>
string repo = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../.."));
string assets = Path.Combine(repo, "images", "ui", "hbr_manual_phone");
int checks = 0;

void Expect(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
}

bool Inside(HbrPhoneRect inner, HbrPhoneRect outer) =>
    inner.X >= outer.X - 0.01f && inner.Y >= outer.Y - 0.01f && inner.Right <= outer.Right + 0.01f && inner.Bottom <= outer.Bottom + 0.01f;

bool Overlaps(HbrPhoneRect a, HbrPhoneRect b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

// ---------------------------------------------------------------- phone geometry
HbrPhoneRect screen = HbrManualPhoneLayout.Screen;
HbrPhoneRect visible = screen with { Y = 0f, Height = screen.Bottom };
HbrPhoneRect frame = HbrManualPhoneLayout.Frame;
// DenchoFrame's screen is x 49..933, y 183..1363 of the 1270x1564 sprite.
Expect(Math.Abs(screen.X - (frame.X + 49f)) < 0.01f && Math.Abs(screen.Y - (frame.Y + 183f)) < 0.01f &&
       Math.Abs(screen.Width - 884f) < 0.01f && Math.Abs(screen.Height - 1180f) < 0.01f, "screen sits in DenchoFrame's window");
Expect(Inside(HbrManualPhoneLayout.Body, visible), "page body is inside the visible screen");
Expect(Inside(HbrManualPhoneLayout.Dock, visible), "stat dock is inside the visible screen");
Expect(Inside(HbrManualPhoneLayout.Portrait, visible), "portrait window is inside the visible screen");
Expect(Inside(HbrManualPhoneLayout.MiniRadar, visible), "overview radar is inside the visible screen");
Expect(HbrManualPhoneLayout.Exit.Right <= frame.X + 60f, "exit plate stays left of the phone body");
Expect(Math.Abs(HbrManualPhoneLayout.Body.Width - 836f) < 0.01f && Math.Abs(HbrManualPhoneLayout.Body.Height - 912f) < 0.01f,
    "page body is 836x912");
Expect(Math.Abs(HbrManualPhoneLayout.MiniRadar.CentreX - HbrManualPhoneLayout.ScreenCentreX) < 0.01f, "overview radar is centred");

// Terminal buttons: 2-5 in one row, inside the screen, apart, clear of the portrait and the radar labels.
// 浮游炮 is Hotaru's and 命运骰子 31O's, so the two never show together.
foreach (bool equipment in new[] { false, true })
foreach (bool cannon in new[] { false, true })
foreach (bool fateDice in new[] { false, true })
{
    if (cannon && fateDice) continue;
    var buttons = HbrManualPhoneLayout.HomeButtons(equipment, cannon, fateDice);
    Expect(buttons[0] == HbrManualHomeButton.Senses, "六感 comes first");
    Expect(buttons.Contains(HbrManualHomeButton.Shop) == equipment && buttons.Contains(HbrManualHomeButton.Equipment) == equipment,
        "equipment and shop appear with the HBR numerics layer");
    Expect(buttons.Contains(HbrManualHomeButton.Cannon) == cannon, "浮游炮 appears for Hotaru only");
    Expect(buttons.Contains(HbrManualHomeButton.FateDice) == fateDice, "命运骰子 appears for 31O only");
    Expect(!fateDice || buttons[^1] == HbrManualHomeButton.FateDice, "命运骰子 comes last");
    Expect(buttons.Count == 2 + (equipment ? 2 : 0) + (cannon ? 1 : 0) + (fateDice ? 1 : 0), "button count");
    float[] centres = HbrManualPhoneLayout.RingButtonCentres(buttons.Count);
    float size = HbrManualPhoneLayout.RingButtonSize * HbrManualPhoneLayout.RingButtonScale(buttons.Count);
    var rects = centres.Select(c => new HbrPhoneRect(c - size / 2f, HbrManualPhoneLayout.RingButtonTop - size / 2f, size, size)).ToList();
    Expect(Math.Abs(centres.Average() - HbrManualPhoneLayout.ScreenCentreX) < 0.01f, $"{buttons.Count} buttons centred");
    foreach (var rect in rects)
    {
        Expect(Inside(rect, visible), $"{buttons.Count} buttons: {rect} inside the screen");
        Expect(!Overlaps(rect, HbrManualPhoneLayout.Portrait), $"{buttons.Count} buttons: {rect} clears the portrait");
        Expect(rect.Bottom <= HbrManualPhoneLayout.MiniRadar.CentreY - HbrManualPhoneLayout.MiniLabelRadius - 12f,
            $"{buttons.Count} buttons: {rect} clears the radar's top label");
    }
    for (int i = 1; i < rects.Count; i++) Expect(rects[i].X >= rects[i - 1].Right - 0.01f, $"{buttons.Count} buttons do not overlap");
}

// Stat dock: six items centred in DenchoHeader.
for (int i = 0; i < 6; i++)
{
    float centre = HbrManualPhoneLayout.DockItemCentre(i);
    var item = new HbrPhoneRect(centre - HbrManualPhoneLayout.DockItemWidth / 2f, HbrManualPhoneLayout.DockItemTop,
        HbrManualPhoneLayout.DockItemWidth, HbrManualPhoneLayout.DockItemHeight);
    Expect(Inside(item, HbrManualPhoneLayout.Dock), $"dock item {i} inside the dock");
    if (i > 0) Expect(item.X >= HbrManualPhoneLayout.DockItemCentre(i - 1) + HbrManualPhoneLayout.DockItemWidth / 2f, $"dock items {i - 1}/{i} apart");
}

// Dock values: the part before the decimal point, and a clear gap to the neighbouring value in the
// manual face (digits advance 0.573 em in hbr_manual_ui.otf, measured with fontTools).
float DockTextWidth(string text) => text.Length * HbrManualPhoneLayout.DockDigitAdvance * HbrManualPhoneLayout.DockValueFontSize(text);
Expect(HbrManualPhoneLayout.DockValueText(267.7m) == "267" && HbrManualPhoneLayout.DockValueText(313.2m) == "313" &&
       HbrManualPhoneLayout.DockValueText(216m) == "216" && HbrManualPhoneLayout.DockValueText(0.9m) == "0" &&
       HbrManualPhoneLayout.DockValueText(-3m) == "0", "dock values keep the part before the decimal point");
foreach (decimal value in new[] { 7m, 267.7m, 1999.9m, 12345m, 999999m })
{
    string text = HbrManualPhoneLayout.DockValueText(value);
    Expect(HbrManualPhoneLayout.DockItemSpacing - DockTextWidth(text) >= HbrManualPhoneLayout.DockValueGap - 0.01f,
        $"dock value {text} leaves a gap to its neighbour");
}
Expect(HbrManualPhoneLayout.DockValueFontSize("267") == HbrManualPhoneLayout.DockValueSize, "three digits keep the 52px face");
Expect((4 * HbrManualPhoneLayout.DockDigitAdvance + 0.303f) * HbrManualPhoneLayout.DockValueSize > HbrManualPhoneLayout.DockItemSpacing,
    "a value like 267.7 at 52px was wider than the item spacing");

// Six senses page: vertices with their ± buttons stay inside the body and clear of each other.
var body = new HbrPhoneRect(0f, 0f, HbrManualPhoneLayout.Body.Width, HbrManualPhoneLayout.Body.Height);
var vertices = Enumerable.Range(0, 6).Select(i =>
{
    var (x, y) = HbrManualPhoneLayout.Point(HbrManualPhoneLayout.RadarCentreX, HbrManualPhoneLayout.RadarCentreY, i,
        HbrManualPhoneLayout.RadarLabelRadius);
    return new HbrPhoneRect(x - HbrManualPhoneLayout.VertexWidth / 2f, y - HbrManualPhoneLayout.VertexHeight / 2f,
        HbrManualPhoneLayout.VertexWidth, HbrManualPhoneLayout.VertexHeight);
}).ToList();
for (int i = 0; i < 6; i++)
{
    Expect(Inside(vertices[i], body), $"vertex {i} inside the body");
    // The bottom vertex's hit box reaches 5px into the legend's 32px line box, as in the preview;
    // the legend's glyphs start below that.
    Expect(vertices[i].Bottom <= HbrManualPhoneLayout.LegendTop + 8f, $"vertex {i} above the legend text");
    for (int j = i + 1; j < 6; j++) Expect(!Overlaps(vertices[i], vertices[j]), $"vertices {i}/{j} apart");
}
Expect(Math.Abs(HbrManualPhoneLayout.Angle(0) + MathF.PI / 2f) < 1e-5f, "领袖魅力 points up");
var (x3, y3) = HbrManualPhoneLayout.Point(0f, 0f, 3, 10f);
Expect(Math.Abs(x3) < 1e-4f && Math.Abs(y3 - 10f) < 1e-4f, "精神力 points down");
Expect(Inside(HbrManualPhoneLayout.SixDetail, body) && Inside(HbrManualPhoneLayout.Confirm, body) &&
       !Overlaps(HbrManualPhoneLayout.SixDetail, HbrManualPhoneLayout.Confirm), "detail box and confirm button fit apart");
Expect(HbrManualPhoneLayout.Toggle.Bottom <= 0f && HbrManualPhoneLayout.Body.Y + HbrManualPhoneLayout.Toggle.Y >= 0f,
    "the 六感/战斗属性 toggle sits in the page header");

// Radar scaling and the battle stat view's rings.
Expect(HbrManualPhoneLayout.RadiusOf(99f, 99f, 236f) == 236f && HbrManualPhoneLayout.RadiusOf(-5f, 99f, 236f) == 0f &&
       HbrManualPhoneLayout.RadiusOf(500f, 99f, 236f) == 236f, "radar radius clamps to the outer ring");
Expect(HbrManualPhoneLayout.StatRadarMax(144f, 2f) == 350f, "stat radar: base 144 + 99x2 rounds up to 350");
Expect(HbrManualPhoneLayout.StatRadarMax(144f, 2f, 421f) == 450f, "stat radar grows with larger live stats");
Expect(HbrManualPhoneLayout.StatRings(350f).SequenceEqual(new[] { 70f, 140f, 210f, 280f, 350f }), "stat radar rings at fifths");
Expect(HbrManualPhoneLayout.SenseRings.Last() == HbrManualPhoneLayout.MaxLevel, "sense radar outer ring is 99");

// Six-sense tiers: black Saki 领袖魅力/才能/疯狂, white 温柔/单纯, grey 精神力.
Expect(HbrManualPhoneLayout.Tiers.Length == 6 && HbrManualPhoneLayout.TierColours.Length == 6 && HbrManualPhoneLayout.Leanings.Length == 6,
    "six tiers, colours and leanings");
Expect(HbrManualPhoneLayout.Tiers.Count(t => t == HbrSenseTier.Black) == 3 && HbrManualPhoneLayout.Tiers[3] == HbrSenseTier.Grey &&
       HbrManualPhoneLayout.Tiers[2] == HbrSenseTier.White && HbrManualPhoneLayout.Tiers[4] == HbrSenseTier.White, "tier assignment");
for (int i = 0; i < 6; i++)
    Expect(HbrManualPhoneLayout.Leanings[i].StartsWith(HbrManualPhoneLayout.Tiers[i] switch
    {
        HbrSenseTier.Black => "黑祥侧", HbrSenseTier.White => "白祥侧", _ => "灰"
    }), $"leaning {i} matches its tier");
Expect(HbrManualPhoneLayout.MiniLabelSide(0) == 0 && HbrManualPhoneLayout.MiniLabelSide(3) == 0 &&
       HbrManualPhoneLayout.MiniLabelSide(1) == 1 && HbrManualPhoneLayout.MiniLabelSide(5) == -1, "overview label sides");

// The seeded burst: the preview's 200 spokes within their ranges, a third of them violet.
var spokes = HbrManualPhoneLayout.BurstSpokes().ToList();
Expect(spokes.Count == HbrManualPhoneLayout.BurstLines, "200 burst spokes");
Expect(spokes.All(s => s.From >= 60f && s.From <= 130f && s.To >= s.From + 40f && s.To <= s.From + 280f &&
                       s.Alpha >= 0.12f && s.Alpha <= 0.5f && (s.Width == 1f || s.Width == 2f)), "burst spokes stay in range");
Expect(spokes.Count(s => s.Violet) == 67, "every third spoke is violet");
Expect(spokes.Select(s => s.Angle).SequenceEqual(HbrManualPhoneLayout.BurstSpokes().Select(s => s.Angle)), "burst is deterministic");
// Park-Miller from seed 7: the first draw is 7 * 16807 / (2^31 - 1).
Expect(Math.Abs(spokes[0].Angle - 7f * 16807f / 2147483647f * 0.02f) < 1e-6f, "burst uses the preview's generator");

Expect(HbrManualPhoneLayout.Chant(false, false) == "「千早同学？」" && HbrManualPhoneLayout.Chant(false, true) == "「此刻、即是复权之刻。」" &&
       HbrManualPhoneLayout.Chant(true, false) == "只记得，要去塔顶。", "chants per character");
Expect(HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.Shop) == "商店" &&
       HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.Equipment) == "装备", "shop and equipment buttons");

// ---------------------------------------------------------------- assets
var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "sources.json"))).RootElement;
var records = manifest.GetProperty("files").EnumerateArray().ToDictionary(r => r.GetProperty("file").GetString()!);
string[] used =
{
    "DenchoFrame", "DenchoFrameFlash", "DenchoRightHand", "ButtonDungeonExitDefault", "ButtonDungeonExitAvtive",
    "ButtonDungeonExitDisable", "IconExitDefault", "IconExitActive", "LineSlash", "DenchoHeader", "BtnDenchoshadow",
    "BtnDenchoStatusNormal", "BtnDenchoStatusActive", "IconBatchNumber", "DenchoBannerActive", "BtnDenchoLotteryAdditive",
    "Lotteryshadow", "DenchoGachaTextBox", "btnPlusNormal", "btnPlusActive", "btnPlusDisable", "IconHc",
    "minusNormal", "minusActive", "minusDisable", "icon_glossary", "icon_equipment", "icon_shop", "icon_cannon",
    "face_31o_normal", "face_31o_dual", "face_31o_bow", "face_30a", "face_hotaru"
};
foreach (string name in used)
{
    string file = Path.Combine(assets, name + ".png");
    Expect(File.Exists(file), $"{name}.png exists");
    Expect(records.TryGetValue(name + ".png", out JsonElement record), $"{name}.png is in sources.json");
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
    Expect(record.GetProperty("sha256").GetString() == hash, $"{name}.png matches its SHA-256");
    var (w, h) = PngSize(file);
    Expect(record.GetProperty("size")[0].GetInt32() == w && record.GetProperty("size")[1].GetInt32() == h, $"{name}.png size recorded");
    string import = file + ".import";
    Expect(File.Exists(import) && File.ReadAllText(import).Contains("mipmaps/generate=true") &&
           File.ReadAllText(import).Contains($"source_file=\"res://images/ui/hbr_manual_phone/{name}.png\""),
        $"{name}.png imports with mipmaps");
}
Expect(PngSize(Path.Combine(assets, "DenchoFrame.png")) == (1270, 1564), "DenchoFrame drawn 1:1");
Expect(PngSize(Path.Combine(assets, "DenchoHeader.png")) == (820, 202), "DenchoHeader drawn 1:1 as the dock");
Expect(PngSize(Path.Combine(assets, "DenchoFrameFlash.png")) == (384, 384), "flash nine-patch source");
foreach (string face in used.Where(n => n.StartsWith("face_", StringComparison.Ordinal)))
    Expect(PngSize(Path.Combine(assets, face + ".png")).Width == 360, $"{face} is a 360px head crop");
foreach (string minus in new[] { "minusNormal", "minusActive", "minusDisable" })
    Expect(PngSize(Path.Combine(assets, minus + ".png")) == (88, 88), $"{minus} drawn at 2x");
Expect(File.Exists(Path.Combine(repo, "images", "ui", "memory_garden.png")), "garden capsule reuses images/ui/memory_garden.png");
Expect(File.Exists(Path.Combine(repo, "shaders", "hbr_manual_backdrop.gdshader")), "backdrop shader present");

// Font: every fixed string drawn in the HBR face is in the subset. 命运骰子 is drawn through the
// face's locale fallback until the subset is regenerated with 命 and 骰.
Expect(HbrManualPhoneLayout.HomeButtonTitle(HbrManualHomeButton.FateDice) == "命运骰子" &&
       HbrManualPhoneLayout.UsesLocaleFont(HbrManualHomeButton.FateDice) &&
       !HbrManualPhoneLayout.AllText().Contains("命运骰子") &&
       Enum.GetValues<HbrManualHomeButton>().Count(HbrManualPhoneLayout.UsesLocaleFont) == 1, "only 命运骰子 leans on the locale fallback");
string fontFile = Path.Combine(assets, "font", "hbr_manual_ui.otf");
HashSet<int> cmap = ReadCmap(fontFile);
foreach (string text in HbrManualPhoneLayout.AllText())
foreach (char ch in text)
    Expect(cmap.Contains(ch), $"manual font draws '{ch}' (U+{(int)ch:X4}) from \"{text}\"");
for (int ch = 32; ch < 127; ch++) Expect(cmap.Contains(ch), $"manual font covers ASCII U+{ch:X4}");
Expect(records["font/hbr_manual_ui.otf"].GetProperty("sha256").GetString() ==
       Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fontFile))).ToLowerInvariant(), "font subset matches its SHA-256");

// ---------------------------------------------------------------- page style
// Every page inside the phone uses the shared page style, not the old manual's pink/charcoal palette
// or the purple exchange-shop look.
string screens = Path.Combine(repo, "Core", "Nodes", "Screens");
string[] oldPalette = { "#ed72aa", "#25282e", "#3c303b", "#22252b", "#191b20", "#f2dbe6", "#7c3058", "HbrEquipmentArt.Lilac", "HbrEquipmentArt.Magenta" };
foreach (string page in new[] { "NManualBattleStatsPanel.cs", "NManualGlossaryPanel.cs", "NManualCannonUpgradesPanel.cs",
             "NManualEquipmentPanel.cs", "NManualShopPanel.cs", "NManualFateDicePanel.cs" })
{
    string source = File.ReadAllText(Path.Combine(screens, page));
    Expect(source.Contains("HbrManualPhoneStyle.", StringComparison.Ordinal), $"{page} uses the phone page style");
    foreach (string colour in oldPalette)
        Expect(!source.Contains(colour, StringComparison.OrdinalIgnoreCase), $"{page} no longer uses {colour}");
}
string equipmentArt = File.ReadAllText(Path.Combine(screens, "HbrEquipmentArt.cs"));
Expect(equipmentArt.Contains("HbrManualPhoneStyle.Primary", StringComparison.Ordinal) &&
       !equipmentArt.Contains("Capsule(selected ? ListActive : ListNormal", StringComparison.Ordinal),
    "equipment and shop rows and buttons follow the phone style");
// hbr-status-value reads these phrases from the stats page; the restyle keeps them.
string stats = File.ReadAllText(Path.Combine(screens, "NManualBattleStatsPanel.cs"));
Expect(stats.Contains("力量、灵巧、智慧、体力、精神、运气", StringComparison.Ordinal) && stats.Contains("防御参照属性", StringComparison.Ordinal) &&
       stats.Contains("状态值", StringComparison.Ordinal), "stats page keeps the terminology checked by hbr-status-value");

// ---------------------------------------------------------------- fit below the top bar
// The run's top bar draws above overlays; its torn edge reaches y 89 over the phone (top_bar.tres).
Expect(HbrManualPhoneLayout.TopBarInset >= 89f, "top bar inset clears the torn edge");
float[] tops =
{
    HbrManualPhoneLayout.Humans.Y, HbrManualPhoneLayout.RankNumber.Y, HbrManualPhoneLayout.RankLabel.Y,
    HbrManualPhoneLayout.Portrait.Y + HbrManualPhoneLayout.PortraitRing.Y, HbrManualPhoneLayout.Back.Y,
    HbrManualPhoneLayout.PageTitle.Y, HbrManualPhoneLayout.PageInfoTop - 4f,
    HbrManualPhoneLayout.Body.Y + HbrManualPhoneLayout.Toggle.Y
};
Expect(tops.All(top => top >= HbrManualPhoneLayout.ContentTop), "content band starts above every top-row element");
float[] bottoms = { HbrManualPhoneLayout.Body.Bottom, HbrManualPhoneLayout.Dock.Bottom, HbrManualPhoneLayout.Screen.Bottom };
Expect(bottoms.All(bottom => bottom <= HbrManualPhoneLayout.ContentBottom), "content band ends below the screen");
foreach ((float width, float height) in new[] { (1920f, 1080f), (1920f, 1099f), (1920f, 1200f), (2560f, 1080f), (1920f, 1440f), (1728f, 1080f) })
{
    var (scale, x, y) = HbrManualPhoneLayout.RigPlacement(width, height);
    string at = $"{width}x{height}";
    Expect(scale > 0f && scale <= 1f, $"{at}: rig never grows past 1:1");
    Expect(y + HbrManualPhoneLayout.ContentTop * scale >= HbrManualPhoneLayout.TopBarInset - 0.01f, $"{at}: content starts below the top bar");
    Expect(y + HbrManualPhoneLayout.ContentBottom * scale <= height + 0.01f, $"{at}: content ends on screen");
    Expect(x >= -0.01f && x + HbrManualPhoneLayout.CanvasWidth * scale <= width + 0.01f, $"{at}: rig fits the width");
    Expect(Math.Abs(x + HbrManualPhoneLayout.CanvasWidth * scale / 2f - width / 2f) < 0.01f, $"{at}: rig centred");
}
var (fit1080, _, top1080) = HbrManualPhoneLayout.RigPlacement(1920f, 1080f);
Expect(Math.Abs(top1080 + HbrManualPhoneLayout.ContentTop * fit1080 - HbrManualPhoneLayout.TopBarInset) < 0.01f &&
       Math.Abs(top1080 + HbrManualPhoneLayout.ContentBottom * fit1080 - 1080f) < 0.01f, "1080p: content fills the room below the top bar");
Expect(HbrManualPhoneLayout.RigPlacement(1920f, 1440f).Scale == 1f, "tall screens keep the rig 1:1");
string rigSource = File.ReadAllText(Path.Combine(screens, "NSixSenseStatusScreen.Pages.cs"));
Expect(rigSource.Contains("HbrManualPhoneLayout.RigPlacement(Size.X, Size.Y)", StringComparison.Ordinal) &&
       rigSource.Contains("_rig.Scale = Vector2.One * scale", StringComparison.Ordinal), "the rig is placed and scaled by RigPlacement");
// The relic row also draws above overlays; the manual slides it away while open and brings it back.
string screenSource = File.ReadAllText(Path.Combine(screens, "NSixSenseStatusScreen.cs"));
Expect(screenSource.Contains("relics.AnimHide()", StringComparison.Ordinal) &&
       screenSource.Contains("RelicInventory?.AnimShow()", StringComparison.Ordinal) &&
       screenSource.Contains("public override void _ExitTree() => RestoreRelics();", StringComparison.Ordinal) &&
       System.Text.RegularExpressions.Regex.Matches(screenSource, @"RestoreRelics\(\);").Count >= 3 &&
       System.Text.RegularExpressions.Regex.Matches(screenSource, @"HideRelics\(\);").Count >= 2,
    "relic row hidden while the manual is open and restored on close, hide and exit");

// ---------------------------------------------------------------- shop and equipment page fixes
// The clerk's bubble is sized to its text: the autowrapping label gets its width up front, and the
// bubble shrinks back whenever the label's height settles (a container only ever grows).
string shop = File.ReadAllText(Path.Combine(screens, "NManualShopPanel.cs"));
Expect(shop.Contains("CustomMinimumSize = new Vector2(BubbleMaxWidth - 2f * BubblePadding, 0f)", StringComparison.Ordinal) &&
       shop.Contains("_bubble.MinimumSizeChanged += FitBubble", StringComparison.Ordinal) &&
       shop.Contains("_bubble.Size = new Vector2(_bubbleWidth, 0f)", StringComparison.Ordinal), "shop bubble fits its text");
// Gold uses the game's own coin (the top bar's), with HBR's GP icon only as the fallback.
Expect(equipmentArt.Contains("\"res://images/atlases/ui_atlas.sprites/top_bar/top_bar_gold.tres\"", StringComparison.Ordinal) &&
       equipmentArt.Contains("CoinTexture => Load(GoldCoin) ?? Load(Coin)", StringComparison.Ordinal), "gold coin with GP fallback");
Expect(!shop.Contains("HbrEquipmentArt.Coin)", StringComparison.Ordinal) && !shop.Contains("HbrEquipmentArt.Coin,", StringComparison.Ordinal) &&
       !equipmentArt.Contains("Picture(Coin,", StringComparison.Ordinal), "wallet, prices and the sell tab use the gold coin");
// Chip rows follow the booster: as many as its chip slots, fitted/slots in the header, 0/0 without one.
string equipmentPage = File.ReadAllText(Path.Combine(screens, "NManualEquipmentPanel.cs"));
Expect(equipmentPage.Contains("AddSection($\"芯片　{chipsFitted}/{chipSlots}\")", StringComparison.Ordinal) &&
       equipmentPage.Contains("for (int i = 0; i < chipSlots; i++)", StringComparison.Ordinal) &&
       !equipmentPage.Contains("i < HbrEquipmentCatalog.MaxChipSlots", StringComparison.Ordinal), "chip rows follow the booster's slots");

// ---------------------------------------------------------------- lists fit the page
// A control given a Size keeps the minimum height it reports at that moment, and an autowrapping
// label reports one glyph per line before it has a width: the shop list kept that height and its
// last rows sat below the phone screen. Panes are anchored to the page instead.
string style = File.ReadAllText(Path.Combine(screens, "HbrManualPhoneStyle.cs"));
Expect(style.Contains("control.SetAnchorsPreset(Control.LayoutPreset.FullRect)", StringComparison.Ordinal) &&
       style.Contains("control.OffsetBottom = y + height - parent.Y", StringComparison.Ordinal), "Pin anchors a pane to the page's edges");
Expect(shop.Contains("HbrManualPhoneStyle.Pin(_listPanel, Size,", StringComparison.Ordinal) &&
       !shop.Contains("_listPanel.Size =", StringComparison.Ordinal) && !shop.Contains("_listPanel.Position =", StringComparison.Ordinal),
    "shop list is anchored to the page");
string glossary = File.ReadAllText(Path.Combine(screens, "NManualGlossaryPanel.cs"));
foreach (string pane in new[] { "_searchRow", "_listPane", "_detailPane" })
    Expect(glossary.Contains($"HbrManualPhoneStyle.Pin({pane}, Size,", StringComparison.Ordinal) &&
           !glossary.Contains($"{pane}.Size =", StringComparison.Ordinal), $"glossary {pane} is anchored to the page");
Expect(equipmentPage.Contains("HbrManualPhoneStyle.Pin(_picker, Size,", StringComparison.Ordinal) &&
       !equipmentPage.Contains("_picker.Size =", StringComparison.Ordinal), "equipment picker is anchored to the page");
Expect(equipmentPage.Contains("statsScroll.AddChild(stats)", StringComparison.Ordinal) &&
       equipmentPage.Contains("body.AddChild(statsScroll)", StringComparison.Ordinal), "equipment stat column scrolls instead of growing the page");

// ---------------------------------------------------------------- home
string home = File.ReadAllText(Path.Combine(screens, "NManualPhoneHome.cs"));
Expect(HbrManualPhoneLayout.RankText == "等级" && home.Contains("manual.HbrBattleLevel.ToString()", StringComparison.Ordinal) &&
       !home.Contains("levels.Sum()", StringComparison.Ordinal), "home shows the battle level, not the sum of the six senses");
Expect(home.Contains("HbrManualPhoneLayout.NoRank", StringComparison.Ordinal), "runs without HBR numerics show no level");
Expect(!home.Contains("PortraitTitle", StringComparison.Ordinal) && !home.Contains("_levelTag", StringComparison.Ordinal) &&
       home.Contains("TooltipText = \"查看战斗属性\"", StringComparison.Ordinal), "the portrait carries no title or level and still opens battle stats");
Expect(home.Contains("HbrManualPhoneLayout.DockValueText(", StringComparison.Ordinal) &&
       home.Contains("HbrManualPhoneLayout.DockValueFontSize(value.Text)", StringComparison.Ordinal), "dock values use the whole number at a fitted size");

// ---------------------------------------------------------------- page switches
// Codex takeover review F5: a switch during the 0.22s transition froze the page part way. Any run of
// switches must leave the layers at rest for the page shown (PageTransitionChecks).
string transitions = PageTransitionChecks.Run(Expect, repo);

Console.WriteLine(transitions);
Console.WriteLine($"hbr-manual-phone: {checks} checks passed");
return 0;

static (int Width, int Height) PngSize(string path)
{
    byte[] head = new byte[24];
    using (var stream = File.OpenRead(path)) stream.ReadExactly(head);
    return (BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(20)));
}

// Code points of an OpenType font's cmap (formats 4 and 12).
static HashSet<int> ReadCmap(string path)
{
    byte[] font = File.ReadAllBytes(path);
    int tables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
    int cmap = -1;
    for (int i = 0; i < tables; i++)
    {
        int record = 12 + i * 16;
        if (Encoding.ASCII.GetString(font, record, 4) == "cmap") cmap = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
    }
    if (cmap < 0) throw new InvalidDataException("no cmap table");
    var points = new HashSet<int>();
    int subtables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(cmap + 2));
    for (int i = 0; i < subtables; i++)
    {
        int offset = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(cmap + 4 + i * 8 + 4));
        int format = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(offset));
        if (format == 4)
        {
            int segments = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(offset + 6)) / 2;
            int ends = offset + 14, starts = ends + segments * 2 + 2, deltas = starts + segments * 2, ranges = deltas + segments * 2;
            for (int s = 0; s < segments; s++)
            {
                int end = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(ends + s * 2));
                int start = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(starts + s * 2));
                short delta = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(deltas + s * 2));
                int rangeOffset = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(ranges + s * 2));
                for (int code = start; code <= end && code != 0xFFFF; code++)
                {
                    int glyph;
                    if (rangeOffset == 0) glyph = (code + delta) & 0xFFFF;
                    else
                    {
                        int at = ranges + s * 2 + rangeOffset + (code - start) * 2;
                        glyph = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    if (glyph != 0) points.Add(code);
                }
            }
        }
        else if (format == 12)
        {
            int groups = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(offset + 12));
            for (int g = 0; g < groups; g++)
            {
                int group = offset + 16 + g * 12;
                int start = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(group));
                int end = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(group + 4));
                for (int code = start; code <= end; code++) points.Add(code);
            }
        }
    }
    return points;
}
