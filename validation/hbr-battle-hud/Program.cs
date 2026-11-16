using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Nodes.Combat;
using HBRGOSakiMod.Core.Nodes.Merchant;
using L7 = HBRGOSakiMod.Core.Nodes.Combat.HbrLuckySevenTimeline;
using Shelf = HBRGOSakiMod.Core.Nodes.Merchant.HbrFateFragmentShelfLayout;

// Checks for the HBR battle HUD (ui-concepts/battle-hud-practical-20261104), 31O's magic sword switch and
// Lucky Seven finisher, and the merchant's fate fragment stand (ui-concepts/magic-sword-finisher-20261109).
// Usage: Checks <repo> [stable sts2.dll] [beta sts2.dll] [recovered vanilla scenes folder]
string repo = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../.."));
string stableDll = args.Length > 1 ? args[1] : Path.Combine(repo, "libs", "sts2.dll");
string betaDll = args.Length > 2 ? args[2]
    : @"D:\stsmod\worktrees\HBRGOSakiMod-beta-compat\outputs\beta-compat\profiles\beta-latest\sts2.dll";
string scenes = args.Length > 3 ? args[3] : @"D:\stsmod\sts\scenes";
string assets = Path.Combine(repo, "images", "ui", "hbr_battle_hud");
int checks = 0;

void Expect(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
}

void Near(double actual, double expected, double tolerance, string description) =>
    Expect(Math.Abs(actual - expected) <= tolerance, $"{description}: {actual} vs {expected}");

// ---------------------------------------------------------------- layout
var aspects = new (string Name, float W, float H)[]
{
    ("16:9", 1920f, 1080f), ("16:10", 1920f, 1200f), ("21:9", 2520f, 1080f), ("4:3", 1920f, 1440f)
};
foreach (var (name, w, h) in aspects)
{
    var boxes = new List<(string Name, HbrHudRect Rect)>
    {
        ("end turn", HbrBattleHudLayout.EndTurnDisc(w, h)),
        ("draw", HbrBattleHudLayout.Pile(HbrPileKind.Draw, w, h)),
        ("discard", HbrBattleHudLayout.Pile(HbrPileKind.Discard, w, h)),
        ("exhaust", HbrBattleHudLayout.Pile(HbrPileKind.Exhaust, w, h)),
        ("turn", HbrBattleHudLayout.TurnIndicator(h)),
        ("roll history", HbrBattleHudLayout.RollHistory(w)),
        ("od gauge", HbrBattleHudLayout.OverdriveGauge(w)),
        ("energy", HbrBattleHudLayout.EnergyCounter(h)),
        ("damage history", HbrBattleHudLayout.DamageHistory(w)),
        ("magic sword", HbrBattleHudLayout.MagicSwordButton(h)),
        ("machina", HbrBattleHudLayout.MachinaButton(h))
    };
    foreach (var (box, rect) in boxes)
        Expect(rect.X >= 0f && rect.Y >= 0f && rect.Right <= w && rect.Bottom <= h, $"{name}: {box} {rect} is on screen");
    for (int i = 0; i < boxes.Count; i++)
    for (int j = i + 1; j < boxes.Count; j++)
        Expect(!boxes[i].Rect.Overlaps(boxes[j].Rect), $"{name}: {boxes[i].Name} clears {boxes[j].Name}");

    // Anchored pile offsets land on the same rects as the absolute layout.
    foreach (HbrPileKind kind in Enum.GetValues<HbrPileKind>())
    {
        HbrHudRect offsets = HbrBattleHudLayout.PileOffsets(kind);
        float anchorX = kind == HbrPileKind.Draw ? 0f : w;
        HbrHudRect absolute = HbrBattleHudLayout.Pile(kind, w, h);
        Expect(Math.Abs(anchorX + offsets.X - absolute.X) < 0.01f && Math.Abs(h + offsets.Y - absolute.Y) < 0.01f,
            $"{name}: {kind} offsets resolve to {absolute}");
        Expect(offsets.Width == HbrBattleHudLayout.PileSize && offsets.Height == HbrBattleHudLayout.PileSize,
            $"{kind} keeps the 80x80 native hit box");
    }

    // Slide-out slots leave the screen, except the exhaust pile's short HBR slide beside the disc.
    var drawHide = HbrBattleHudLayout.Pile(HbrPileKind.Draw, w, h);
    var shift = HbrBattleHudLayout.PileHideShift(HbrPileKind.Draw);
    Expect(drawHide.Right + shift.X <= 0f, $"{name}: draw pile slides off the left edge");
    var discard = HbrBattleHudLayout.Pile(HbrPileKind.Discard, w, h);
    shift = HbrBattleHudLayout.PileHideShift(HbrPileKind.Discard);
    Expect(discard.Y + shift.Y >= h, $"{name}: discard pile drops below the screen");
    var disc = HbrBattleHudLayout.EndTurnDisc(w, h);
    Expect(discard.Right + shift.X <= disc.X, $"{name}: the discard pile does not slide under the disc");
    Near(disc.X + disc.Width / 2f, w - 190f, 0.01, $"{name}: disc centre x (W-190)");
    Near(disc.Y + disc.Height / 2f, h - 165f, 0.01, $"{name}: disc centre y (H-165)");
    float bannerTop = HbrBattleHudLayout.BannerTop(h);
    Expect(bannerTop >= 0f && bannerTop + HbrBattleHudLayout.BannerHeight <= h, $"{name}: banner fits vertically");
    float lineLeft = w / 2f + HbrBattleHudLayout.BannerLineLeft - HbrBattleHudLayout.BannerLineEndWidth;
    Expect(lineLeft >= 0f, $"{name}: banner lines fit horizontally");

    // The magic sword switch sits above the resting hand: a full ten-card hand's cards start 76px above the
    // bottom edge (y 1004 at 1080p, measured on NPlayerHand's resting layout), so the button ends above them.
    HbrHudRect sword = HbrBattleHudLayout.MagicSwordButton(h);
    Expect(sword.Bottom <= h - 76f, $"{name}: magic sword button {sword} clears the resting hand");
    Expect(sword.X >= HbrBattleHudLayout.EnergyCounter(h).Right, $"{name}: magic sword button sits right of the energy orb");
    HbrHudRect machina = HbrBattleHudLayout.MachinaButton(h);
    Expect(machina.Bottom <= h - 76f, $"{name}: Machina and its servant counter clear the resting hand");
    Expect(machina.X - sword.Right >= 18f, $"{name}: both combat buttons retain a separate focus and pointer hit area");
}
Expect(HbrBattleHudLayout.MagicSwordButton(1080f) == new HbrHudRect(212f, 780f, 96f, 128f),
    "magic sword button at (212, 780), medal 96 over a 28px swordheart pill");
Expect(HbrBattleHudLayout.PileHideShift(HbrPileKind.Exhaust) == (HbrBattleHudLayout.ExhaustHideShift, 0f), "exhaust slides 70px");
Near(HbrBattleHudLayout.RollScale, 64.0 / 84.0, 1e-6, "capsule drawn 64px tall from 84px");
Near(HbrBattleHudLayout.RollSourceCap * HbrBattleHudLayout.RollScale, 61.0, 0.5, "capsule caps drawn 61px");
Near(HbrBattleHudLayout.DiscTextureSize / 352.0, 0.82, 0.002, "BtnCmdStart drawn at 0.82");
Near(HbrBattleHudLayout.BannerDuel.Width / 552.0, 0.55, 0.002, "DuelModalBG drawn at 0.55");
// DuelModalBG's own left fade is about 50 of 552 columns; at 0.55 that is a 13px near-hard edge. The
// banner fades the block in over BannerDuelFadeEnd of its width instead (user: "左边这里看起来有点突兀").
Expect(HbrBattleHudLayout.BannerDuelFadeEnd > 0.2f && HbrBattleHudLayout.BannerDuelFadeEnd < 1f &&
       HbrBattleHudLayout.BannerDuel.Width * HbrBattleHudLayout.BannerDuelFadeEnd >= 120f, "duel block fades in over at least 120px at 1080p");
string turnBanner = File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Combat", "NHbrTurnBanner.cs"));
Expect(turnBanner.Contains("COLOR.a *= smoothstep(0.0, fade_end, UV.x);", StringComparison.Ordinal) &&
       turnBanner.Contains("duel.Material = DuelFade();", StringComparison.Ordinal) &&
       turnBanner.Contains("SetShaderParameter(\"fade_end\", HbrBattleHudLayout.BannerDuelFadeEnd)", StringComparison.Ordinal),
    "the duel block's left edge fades in on every banner kind");
Near(HbrBattleHudLayout.BannerHalftone.Width / 1044.0, 0.55, 0.002, "HalftoneBase drawn at 0.55");
Near(HbrBattleHudLayout.LongPressProgress(102f), 0.5, 1e-6, "ring follows the native bar width");
Expect(HbrBattleHudLayout.LongPressProgress(-5f) == 0f && HbrBattleHudLayout.LongPressProgress(400f) == 1f, "ring progress clamps");

// Native banner lifetimes: the HBR banner animates inside them.
Near(HbrBattleHudLayout.PlayerBannerDuration, 1.5 + 0.4 + 0.3, 1e-6, "player banner 2.2s (native)");
Near(HbrBattleHudLayout.EnemyBannerDuration, 1.3 + 1.0, 1e-6, "enemy banner 2.3s (native)");

// UIBattleAttack remainingCount tints.
string Hex(double r, double g, double b) =>
    $"{(int)Math.Round(r * 255):x2}{(int)Math.Round(g * 255):x2}{(int)Math.Round(b * 255):x2}";
Expect(HbrBattleHudLayout.CountDiscColor == Hex(0.647, 0.275, 0.839), "count disc is circle1 (0.647, 0.275, 0.839)");
Expect(HbrBattleHudLayout.CountRingColor == Hex(0.859, 0.867, 0.871), "count ring is gaugeCircleOff01 (0.859, 0.867, 0.871)");
Expect(HbrBattleHudLayout.CountDisc(HbrPileKind.Exhaust, empty: true) == HbrBattleHudLayout.EmptyCountDiscColor &&
       HbrBattleHudLayout.CountDisc(HbrPileKind.Exhaust, empty: false) == HbrBattleHudLayout.ExhaustCountDiscColor &&
       HbrBattleHudLayout.CountDisc(HbrPileKind.Draw, empty: false) == HbrBattleHudLayout.CountDiscColor, "count disc colours by state");
Expect(HbrBattleHudLayout.PileIconAlpha(hovered: false, empty: true) == HbrBattleHudLayout.EmptyIconAlpha &&
       HbrBattleHudLayout.PileIconAlpha(hovered: true, empty: true) == 1f &&
       HbrBattleHudLayout.PileIconAlpha(hovered: false, empty: false) == 1f, "empty icons dim unless hovered");
Expect(HbrBattleHudLayout.WaitingText(1, 2) == "等待队友 1/2", "multiplayer waiting text");

// ---------------------------------------------------------------- textures
var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "sources.json"))).RootElement;
var records = manifest.GetProperty("files").EnumerateArray().ToDictionary(r => r.GetProperty("file").GetString()!);
var used = new HashSet<string>
{
    "BtnCmdStartNormal", "BtnCmdStartActive", "BtnCmdStartDisable", "auraCircle", "circle1", "gaugeCircleOff01",
    "StatusEffect", "MordaTitleLineEdge", "dot10", "HalftoneBase",
    "ButtonBattleAutoOnOffDefault", "ButtonBattleAutoOnOffActive", "ButtonBattleAutoOnOffTempActive"
};
foreach (string capsule in used.Where(n => n.StartsWith("ButtonBattleAutoOnOff", StringComparison.Ordinal)).ToList())
    used.Add(capsule + "_amnesia");
foreach (HbrPileKind kind in Enum.GetValues<HbrPileKind>())
foreach (bool hovered in new[] { false, true })
foreach (bool empty in new[] { false, true })
    used.Add(HbrBattleHudLayout.PileIconPath(kind, hovered, empty));
foreach (HbrTurnBannerKind kind in Enum.GetValues<HbrTurnBannerKind>())
foreach (string banner in new[] { "DuelModalBG", "vsFaceBaseLinePlayer01", "vsFaceBaseLinePlayer02" })
    used.Add(HbrBattleHudLayout.BannerTexture(banner, kind));

var sizes = new Dictionary<string, (int W, int H)>
{
    ["BtnCmdStartNormal"] = (352, 352), ["auraCircle"] = (242, 242), ["circle1"] = (48, 48),
    ["gaugeCircleOff01"] = (124, 124), ["StatusEffect"] = (256, 256), ["MordaTitleLineEdge"] = (60, 10),
    ["dot10"] = (10, 10), ["HalftoneBase"] = (1044, 396), ["DuelModalBG"] = (552, 432),
    ["vsFaceBaseLinePlayer01"] = (12, 28), ["vsFaceBaseLinePlayer02"] = (272, 28),
    ["ButtonBattleAutoOnOffDefault"] = (192, 84), ["IconCardGameDefault"] = (104, 104), ["IconExitDefault"] = (90, 90),
    ["IconCemetery"] = (104, 104)
};
foreach (string name in used)
{
    string file = Path.Combine(assets, name + ".png");
    Expect(File.Exists(file), $"{name}.png exists");
    Expect(records.TryGetValue(name + ".png", out JsonElement record), $"{name}.png is recorded in sources.json");
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
    Expect(record.GetProperty("sha256").GetString() == hash, $"{name}.png matches its recorded SHA-256");
    var (pw, ph) = PngSize(file);
    var size = record.GetProperty("size");
    Expect(size[0].GetInt32() == pw && size[1].GetInt32() == ph, $"{name}.png size matches sources.json");
    string baseName = name.Split('_')[0];
    if (sizes.TryGetValue(baseName, out var expected))
        Expect((pw, ph) == expected, $"{name}.png is {expected.W}x{expected.H}");
}
// Every HUD texture imports with mipmaps (drawn at 0.4-0.8x), and the import points at its own file.
foreach (string png in Directory.GetFiles(assets, "*.png"))
{
    string import = png + ".import";
    Expect(File.Exists(import), $"{Path.GetFileName(png)} has an import file");
    string text = File.ReadAllText(import);
    Expect(text.Contains("importer=\"texture\"") && text.Contains("mipmaps/generate=true"),
        $"{Path.GetFileName(png)} imports as a texture with mipmaps");
    Expect(text.Contains($"source_file=\"res://images/ui/hbr_battle_hud/{Path.GetFileName(png)}\""),
        $"{Path.GetFileName(png)}.import names its source");
}
// Nine-patch caps come from the sprites' own borders.
JsonElement Border(string sprite) => records[sprite + ".png"].GetProperty("border");
Expect(Border("ButtonBattleAutoOnOffDefault").GetProperty("x").GetDouble() == HbrBattleHudLayout.RollSourceCap &&
       Border("ButtonBattleAutoOnOffDefault").GetProperty("z").GetDouble() == HbrBattleHudLayout.RollSourceCap, "capsule caps are 80/80");
Expect(Border("MordaTitleLineEdge").GetProperty("x").GetDouble() == HbrBattleHudLayout.TurnLineLeftCap &&
       Border("MordaTitleLineEdge").GetProperty("z").GetDouble() == HbrBattleHudLayout.TurnLineRightCap, "turn line caps are 2/55");
Expect(HbrBattleHudLayout.BannerLineCap < Border("vsFaceBaseLinePlayer01").GetProperty("x").GetDouble(),
    "banner line slice leaves a centre column");
foreach (var (file, record) in records)
{
    string origin = record.GetProperty("origin").GetString()!;
    Expect(origin is "hbr-sprite" or "hbr-texture" or "derived" or "hbr-font-subset", $"{file} has a known origin");
}

// Recolours: the preview's CSS filters, recomputed here per pixel.
double[][] HueRotate(double degrees)
{
    double a = degrees * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
    return new[]
    {
        new[] { 0.213 + c * 0.787 - s * 0.213, 0.715 - c * 0.715 - s * 0.715, 0.072 - c * 0.072 + s * 0.928 },
        new[] { 0.213 - c * 0.213 + s * 0.143, 0.715 + c * 0.285 + s * 0.140, 0.072 - c * 0.072 - s * 0.283 },
        new[] { 0.213 - c * 0.213 - s * 0.787, 0.715 - c * 0.715 + s * 0.715, 0.072 + c * 0.928 + s * 0.072 }
    };
}
double[][] Saturate(double v) => new[]
{
    new[] { 0.213 + 0.787 * v, 0.715 - 0.715 * v, 0.072 - 0.072 * v },
    new[] { 0.213 - 0.213 * v, 0.715 + 0.285 * v, 0.072 - 0.072 * v },
    new[] { 0.213 - 0.213 * v, 0.715 - 0.715 * v, 0.072 + 0.928 * v }
};
double[][] Grayscale = { new[] { 0.2126, 0.7152, 0.0722 }, new[] { 0.2126, 0.7152, 0.0722 }, new[] { 0.2126, 0.7152, 0.0722 } };
double[] Apply(double[][] m, double[] rgb) =>
    m.Select(row => Math.Clamp(row[0] * rgb[0] + row[1] * rgb[1] + row[2] * rgb[2], 0, 1)).ToArray();

void ExpectFiltered(string source, string target, Func<double[], double[]> filter)
{
    var a = ReadPng(Path.Combine(assets, source + ".png"));
    var b = ReadPng(Path.Combine(assets, target + ".png"));
    Expect(a.Width == b.Width && a.Height == b.Height, $"{target} keeps the size of {source}");
    int worst = 0, alphaChanged = 0, compared = 0;
    for (int i = 0; i < a.Rgba.Length; i += 4)
    {
        if (a.Rgba[i + 3] != b.Rgba[i + 3]) alphaChanged++;
        if (a.Rgba[i + 3] == 0) continue;
        compared++;
        double[] rgb = filter(new[] { a.Rgba[i] / 255.0, a.Rgba[i + 1] / 255.0, a.Rgba[i + 2] / 255.0 });
        for (int c = 0; c < 3; c++) worst = Math.Max(worst, Math.Abs((int)Math.Round(rgb[c] * 255) - b.Rgba[i + c]));
    }
    Expect(alphaChanged == 0, $"{target} keeps every alpha value of {source}");
    Expect(compared > 0 && worst <= 1, $"{target} is {source} through its CSS filter ({compared} pixels, max error {worst})");
}
var recolours = new (HbrTurnBannerKind Kind, double Hue, double DuelSat, double LineSat)[]
{
    (HbrTurnBannerKind.Enemy, 50, 1.2, 1.3), (HbrTurnBannerKind.Extra, -45, 0.95, 1.1)
};
foreach (var (kind, hue, duelSat, lineSat) in recolours)
{
    ExpectFiltered("DuelModalBG", HbrBattleHudLayout.BannerTexture("DuelModalBG", kind),
        rgb => Apply(Saturate(duelSat), Apply(HueRotate(hue), rgb)));
    foreach (string line in new[] { "vsFaceBaseLinePlayer01", "vsFaceBaseLinePlayer02" })
        ExpectFiltered(line, HbrBattleHudLayout.BannerTexture(line, kind), rgb => Apply(Saturate(lineSat), Apply(HueRotate(hue), rgb)));
}
foreach (string capsule in new[] { "ButtonBattleAutoOnOffDefault", "ButtonBattleAutoOnOffActive", "ButtonBattleAutoOnOffTempActive" })
    ExpectFiltered(capsule, capsule + "_amnesia",
        rgb => Apply(Grayscale, rgb).Select(v => Math.Min(1, v * HbrBattleHudLayout.RollAmnesiaBrightness)).ToArray());

// ---------------------------------------------------------------- font
string fontFile = Path.Combine(assets, "font", "hbr_hud_ui.otf");
HashSet<int> cmap = ReadCmap(fontFile);
string fontCharacters = records["font/hbr_hud_ui.otf"].GetProperty("characters").GetString()!;
foreach (string text in HbrBattleHudLayout.AllText())
foreach (char ch in text)
{
    Expect(cmap.Contains(ch), $"the HUD font draws '{ch}' (U+{(int)ch:X4}) from \"{text}\"");
    Expect(fontCharacters.Contains(ch), $"sources.json lists '{ch}'");
}
string fontHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fontFile))).ToLowerInvariant();
Expect(records["font/hbr_hud_ui.otf"].GetProperty("sha256").GetString() == fontHash, "font subset matches its SHA-256");
for (int ch = 32; ch < 127; ch++) Expect(cmap.Contains(ch), $"the HUD font covers ASCII U+{ch:X4}");

// ---------------------------------------------------------------- native members
var required = new Dictionary<string, string[]>
{
    ["MegaCrit.Sts2.Core.Nodes.Combat.NCombatUi"] = new[] { "m:Activate", "m:AnimOut" },
    ["MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton"] = new[]
    {
        "m:Initialize", "m:AnimIn", "m:AnimOut", "m:OnEnable", "m:OnDisable", "m:StartOrStopPulseVfx",
        "m:AfterPlayerEndedTurn", "m:AfterPlayerUnendedTurn", "m:OnTurnStarted", "m:OnFocus", "m:OnUnfocus",
        "m:OnPress", "m:OnRelease", "p:ShowPos", "p:HidePos", "f:_combatState", "f:_isShiny", "f:_state"
    },
    ["MegaCrit.Sts2.Core.Nodes.Combat.NCombatCardPile"] = new[]
    {
        "m:Initialize", "m:AddCard", "m:RemoveCard", "m:OnFocus", "m:OnUnfocus", "m:OnPress", "m:OnRelease",
        "m:AnimIn", "m:AnimOut", "f:_countLabel", "f:_showPosition", "f:_hidePosition"
    },
    ["MegaCrit.Sts2.Core.Nodes.Combat.NExhaustPileButton"] = new[] { "m:SetAnimInOutPositions", "f:_posOffset" },
    ["MegaCrit.Sts2.Core.Nodes.Combat.NDrawPileButton"] = Array.Empty<string>(),
    ["MegaCrit.Sts2.Core.Nodes.Combat.NDiscardPileButton"] = Array.Empty<string>(),
    ["MegaCrit.Sts2.Core.Nodes.Combat.NPlayerTurnBanner"] = new[] { "m:_Ready" },
    ["MegaCrit.Sts2.Core.Nodes.Combat.NEnemyTurnBanner"] = new[] { "m:_Ready" },
    ["MegaCrit.Sts2.Core.Combat.CombatState"] = new[] { "p:RoundNumber", "p:Players", "p:CurrentSide" },
    ["MegaCrit.Sts2.Core.Combat.CombatManager"] = new[] { "e:TurnStarted", "p:PlayersTakingExtraTurn", "m:IsPlayerReadyToEndTurn" },
    ["MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState"] = new[] { "p:TurnNumber", "m:HasCardsToPlay" }
};
// Harmony binds __0 and the getters' results by type.
var signatures = new (string Type, string Method, string Signature)[]
{
    ("MegaCrit.Sts2.Core.Nodes.Combat.NCombatUi", "Activate", "Void(MegaCrit.Sts2.Core.Combat.CombatState)"),
    ("MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton", "Initialize", "Void(MegaCrit.Sts2.Core.Combat.CombatState)"),
    ("MegaCrit.Sts2.Core.Nodes.Combat.NCombatCardPile", "Initialize", "Void(MegaCrit.Sts2.Core.Entities.Players.Player)"),
    ("MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton", "get_ShowPos", "Godot.Vector2()"),
    ("MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton", "get_HidePos", "Godot.Vector2()")
};
foreach (var (label, dll) in new[] { ("stable", stableDll), ("beta", betaDll) })
{
    if (!File.Exists(dll))
    {
        Console.WriteLine($"skip {label}: {dll} not found");
        continue;
    }
    var types = ReadTypes(dll, required.Keys);
    foreach (var (type, members) in required)
    {
        Expect(types.ContainsKey(type), $"{label}: {type} exists");
        foreach (string member in members)
            Expect(types[type].Contains(member), $"{label}: {type} has {member}");
    }
    foreach (var (type, method, signature) in signatures)
        Expect(types[type].Contains("s:" + method + ":" + signature), $"{label}: {type}.{method} is {signature}");
}

// ---------------------------------------------------------------- native scene nodes
var nodes = new Dictionary<string, string[]>
{
    ["combat/end_turn_button.tscn"] = new[] { "Visuals", "Visuals/Image", "Visuals/Glow", "Visuals/GlowVfx", "Visuals/Label",
        "Visuals/ControllerIcon", "PlayerIconContainer", "Bar" },
    ["combat/draw_pile.tscn"] = new[] { "Icon", "CountContainer", "CountContainer/Count", "ControllerIcon" },
    ["combat/discard_pile.tscn"] = new[] { "Icon", "CountContainer", "CountContainer/Count", "ControllerIcon" },
    ["combat/exhaust_pile.tscn"] = new[] { "Icon", "CountContainer", "CountContainer/Count", "ControllerIcon" },
    ["combat/player_turn_banner.tscn"] = new[] { "Label", "TurnNumber" },
    ["combat/enemy_turn_banner.tscn"] = new[] { "Label" }
};
if (Directory.Exists(scenes))
{
    foreach (var (scene, paths) in nodes)
    {
        var present = ScenePaths(Path.Combine(scenes, scene));
        foreach (string path in paths) Expect(present.Contains(path), $"{scene} has node {path}");
    }
    // The pile container anchors draw bottom-left and the other two bottom-right, as PileOffsets assumes.
    string container = File.ReadAllText(Path.Combine(scenes, "combat", "combat_piles_container.tscn"));
    Expect(Regex.IsMatch(container, "name=\"DrawPile\"[^\\[]*anchor_top = 1\\.0") &&
           !Regex.IsMatch(container, "name=\"DrawPile\"[^\\[]*anchor_left = 1\\.0"), "draw pile hangs from the bottom-left");
    foreach (string pile in new[] { "DiscardPile", "ExhaustPile" })
        Expect(Regex.IsMatch(container, $"name=\"{pile}\"[^\\[]*anchor_left = 1\\.0[^\\[]*anchor_top = 1\\.0"),
            $"{pile} hangs from the bottom-right");
}
else
{
    Console.WriteLine($"skip scenes: {scenes} not found");
}

// ---------------------------------------------------------------- Lucky Seven finisher
// Design v3, section 6: about two seconds in four beats (暗场 0-0.3, 召剑 0.3-0.9, 斩击 0.9-1.7, 收刀 1.7-2.1),
// skippable; the setting 终结技演出 has 全屏/简化/关闭 with 全屏 the default; a teammate's finisher is simplified at
// most; and a machine that cannot wait never holds the card.
Expect(0f < L7.DarkEnd && L7.DarkEnd < L7.SummonEnd && L7.SummonEnd < L7.SlashEnd && L7.SlashEnd < L7.FinishEnd,
    "finisher beats run 暗场, 召剑, 斩击, 收刀 in order");
Near(L7.DarkEnd, 0.3, 1e-6, "暗场 ends at 0.3s");
Near(L7.SummonEnd, 0.9, 1e-6, "召剑 ends at 0.9s");
Near(L7.SlashEnd, 1.7, 1e-6, "斩击 ends at 1.7s");
Near(L7.FinishEnd, 2.1, 1e-6, "收刀 ends at 2.1s");
foreach (float beat in new[] { L7.RiftOpenEnd, L7.SwordFallStart, L7.SwordLand, L7.DrawStart, L7.DrawEnd, L7.VeinsPeak,
             L7.SakiIn, L7.SakiInEnd, L7.TitleStart, L7.TitleEnd })
    Expect(beat > L7.DarkEnd && beat <= L7.SummonEnd, $"summon beat {beat}s falls inside 召剑");
Expect(L7.SwordFallStart < L7.SwordLand && L7.SwordLand < L7.DrawStart && L7.DrawStart < L7.DrawEnd,
    "the sword falls from the rift, sticks in the ground, then Saki draws it");
Expect(L7.MaxIntroWait >= L7.SummonEnd && L7.MaxFinishWait >= L7.FinishLength,
    "the safety caps on the two waits never cut the show short at normal speed");
Expect(L7.Title == "必杀剑·魔剑幸运七" && L7.TotalCaption == "总伤害", "title and total caption");
// The total pops in while the title band is still fading out (both scale with the screen height).
float bandBottom = (L7.TitleTop + L7.TitleHeight) * 1080f;
float poppedHalf = L7.TotalBoxHeight / 2f * L7.TotalPopScale;
Expect(L7.TotalY * 1080f - poppedHalf >= bandBottom, "the popped total clears the fading title band");
Expect(L7.TotalY * 1080f + poppedHalf <= 1080f, "the popped total stays on screen");
Expect(File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Combat", "NHbrLuckySevenFinisher.cs"))
        .Contains("HbrLuckySevenTimeline.TotalBoxWidth, height = HbrLuckySevenTimeline.TotalBoxHeight", StringComparison.Ordinal),
    "the total box takes its size from the timeline");

var full = HbrFinisherPresentation.Fullscreen;
var simple = HbrFinisherPresentation.Simplified;
var off = HbrFinisherPresentation.Off;
foreach (HbrFinisherPresentation setting in Enum.GetValues<HbrFinisherPresentation>())
{
    Expect(L7.Resolve(setting, true, false, false) == off && L7.Resolve(setting, false, false, true) == off,
        $"{setting}: tests and headless runs never wait");
    Expect(L7.Resolve(setting, false, true, false) == (setting == off ? off : simple),
        $"{setting}: a teammate's finisher shows simplified at most");
}
Expect(L7.Resolve(full, true, true, false) == full, "the player who played it sees the full show");
Expect(L7.Resolve(simple, true, true, false) == simple && L7.Resolve(off, true, true, false) == off, "the setting picks the show");
Expect(L7.Resolve(full, true, true, true) == simple, "instant fast mode plays the simplified show");

Expect(L7.SlashCount(0) == 0 && L7.SlashCount(5) == 5 && L7.SlashCount(7) == 7 && L7.SlashCount(30) == 7,
    "one slash per hit, seven at most");
Expect(Enumerable.Range(0, 7).All(L7.StartsSlash) && !L7.StartsSlash(7) && !L7.StartsSlash(-1),
    "hits one to seven start slashes, later hits fold in");
Expect(!L7.IsBigSlash(7, 6) && L7.IsBigSlash(8, 6) && !L7.IsBigSlash(12, 5),
    "the seventh slash becomes the big one only past seven hits");
Expect(Enumerable.Range(0, 7).All(i => L7.SlashColor(i) == (i % 2 == 0 ? L7.CrimsonHex : L7.VioletHex)),
    "slashes alternate red and violet");
Near(L7.SummonDarkness(0f), 0, 1e-6, "the battle starts undimmed");
Near(L7.SummonDarkness(L7.DarkEnd), L7.SummonDim, 1e-6, "暗场 reaches its dim");
Near(L7.Flash(0f), 0, 1e-6, "no flash before 收刀");
Near(L7.Flash(L7.FlashPeak), L7.FlashAlpha, 1e-6, "收刀's white flash peaks");
Near(L7.Flash(L7.FinishLength), 0, 1e-6, "the flash is gone when 收刀 ends");
Near(L7.FinishDarkness(L7.FinishLength, L7.SlashDim), 0, 1e-6, "the screen is restored when 收刀 ends");
Near(L7.SwordFall(L7.SwordLand), 1, 1e-6, "the sword lands");
Near(L7.SwordDraw(L7.DrawEnd), 1, 1e-6, "the sword is fully drawn");
Near(L7.TitleSlide(L7.TitleEnd), 1, 1e-6, "the title has slid in by the end of 召剑");
Near(L7.TotalScale(L7.TotalPop), 1, 1e-6, "the total settles at full size");
Near(L7.TotalAlpha(L7.TotalLife), 0, 1e-6, "the total fades out");

// The sword icon is a painted square with flames around the blade; the finisher keeps a band along the blade
// (measured on magic_sword_mode_power.png: pommel near (0.30, 0.98), tip near (0.87, 0.05)), turns the tip
// straight down while it falls, and stands it in the ground by its tip.
Near(L7.SwordTipAngle, -58.5, 0.5, "the icon's blade points up and right at about -58.5 degrees");
Near(L7.FallRotation + L7.SwordTipAngle, 90, 1e-4, "while falling, the tip points straight down");
foreach (var (u, v, what) in new[] { (L7.SwordTipU, L7.SwordTipV, "tip"), (0.37f, 0.84f, "ring hilt"), (0.585f, 0.515f, "mid-blade") })
    Expect(L7.SwordMask(u, v) > 0.99f, $"the sword mask keeps the {what}");
foreach (var (u, v) in new[] { (0.03f, 0.03f), (0.97f, 0.97f), (0.03f, 0.6f), (0.97f, 0.4f), (0.15f, 0.15f), (0.6f, 0.95f) })
    Expect(L7.SwordMask(u, v) == 0f, $"the sword mask drops the painted ground at ({u}, {v})");
Expect(L7.SwordLandLift > 0.3f && L7.SwordLandLift < 0.55f, "the stuck sword stands on its tip, not its middle");
Expect(finisherSource().Contains("MaskedSword(ResourceLoader.Load<Texture2D>(HbrMagicSwordArt.SwordIcon))", StringComparison.Ordinal) &&
       finisherSource().Contains("HbrLuckySevenTimeline.FallRotation", StringComparison.Ordinal) &&
       finisherSource().Contains("HbrLuckySevenTimeline.SwordLandLift", StringComparison.Ordinal),
    "the finisher draws the masked sword, tip down, standing on its tip");
string finisherSource() => File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Combat", "NHbrLuckySevenFinisher.cs"));

// Presentation only: the show waits around Lucky Seven's own autoplay, which still runs once with the same
// arguments; the hit observer is a read-only prefix; no finisher file draws random numbers, and the screen
// shake passes its own angle so the game's chaotic generator is not used either.
string power = File.ReadAllText(Path.Combine(repo, "Core", "Models", "Powers", "MagicSwordModePower.cs"));
int[] order =
{
    power.IndexOf("using HbrLuckySevenFinisher show = HbrLuckySevenFinisher.Begin(owner, luckySeven, hitCount);", StringComparison.Ordinal),
    power.IndexOf("await show.BeforeHits();", StringComparison.Ordinal),
    power.IndexOf("await CardCmd.AutoPlay(choiceContext, luckySeven, null);", StringComparison.Ordinal),
    power.IndexOf("await show.AfterHits();", StringComparison.Ordinal),
    power.IndexOf("await CombatFlowHelper.FinishAutoPlayedCard(luckySeven);", StringComparison.Ordinal)
};
Expect(order.All(i => i >= 0) && order.Zip(order.Skip(1)).All(pair => pair.First < pair.Second),
    "the show only waits before and after Lucky Seven's autoplay");
Expect(Regex.Matches(power, Regex.Escape("CardCmd.AutoPlay(choiceContext, luckySeven, null)")).Count == 1,
    "Lucky Seven is still auto-played once");
string hitPatch = File.ReadAllText(Path.Combine(repo, "Core", "Patches", "Combat", "HbrLuckySevenFinisherHitPatch.cs"));
Expect(hitPatch.Contains("private static void Prefix(Creature target, DamageResult result, CardModel cardSource) =>",
           StringComparison.Ordinal) && !hitPatch.Contains("ref ", StringComparison.Ordinal) &&
       !hitPatch.Contains("__result", StringComparison.Ordinal), "the hit observer is a read-only void prefix");
string[] finisherFiles =
{
    "Core/Nodes/Combat/NHbrLuckySevenFinisher.cs", "Core/Nodes/Combat/NHbrLuckySevenSlash.cs",
    "Core/Nodes/Combat/NHbrLuckySevenRift.cs", "Core/Helpers/HbrLuckySevenFinisher.cs",
    "Core/Nodes/Combat/HbrLuckySevenTimeline.cs", "Core/Nodes/Screens/HbrFateDiceCracks.cs"
};
foreach (string file in finisherFiles)
{
    string source = File.ReadAllText(Path.Combine(repo, file));
    Expect(!Regex.IsMatch(source, @"\bRng\.|GD\.Rand|new Random\(|Random\.Shared"), $"{file} draws no random numbers");
}
string finisherView = File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Combat", "NHbrLuckySevenFinisher.cs"));
MatchCollection shakes = Regex.Matches(finisherView, @"ScreenShake\(([^;]*)\);");
Expect(shakes.Count > 0 && shakes.All(m => m.Groups[1].Value.Split(',').Length == 3 && m.Groups[1].Value.Contains("angle")),
    "every screen shake passes an explicit angle");

string config = File.ReadAllText(Path.Combine(repo, "Core", "Config", "HBRGOSakiConfig.cs"));
Expect(Regex.IsMatch(config, @"HbrFinisherPresentation FinisherPresentation \{ get; set; \} =\s*HbrFinisherPresentation\.Fullscreen;"),
    "终结技演出 defaults to 全屏");
foreach (string lang in new[] { "zhs", "eng" })
{
    var labels = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "HBRGOSakiMod", "localization", lang, "settings_ui.json"))).RootElement;
    foreach (string key in new[] { "title", "hover.desc" }.Concat(Enum.GetNames<HbrFinisherPresentation>()))
        Expect(labels.TryGetProperty("HBRGOSAKIMOD-FINISHER_PRESENTATION." + key, out _), $"{lang} names FINISHER_PRESENTATION.{key}");
}
var zhsLabels = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "HBRGOSakiMod", "localization", "zhs", "settings_ui.json"))).RootElement;
Expect(zhsLabels.GetProperty("HBRGOSAKIMOD-FINISHER_PRESENTATION.title").GetString() == "终结技演出" &&
       zhsLabels.GetProperty("HBRGOSAKIMOD-FINISHER_PRESENTATION.Fullscreen").GetString() == "全屏" &&
       zhsLabels.GetProperty("HBRGOSAKIMOD-FINISHER_PRESENTATION.Simplified").GetString() == "简化" &&
       zhsLabels.GetProperty("HBRGOSAKIMOD-FINISHER_PRESENTATION.Off").GetString() == "关闭", "setting reads 终结技演出: 全屏/简化/关闭");

// ---------------------------------------------------------------- magic sword and fate dice art
// Every image path of the interface sits in HbrMagicSwordArt and exists; the fate dice placeholders are the
// files sources.json records (drawn by make_placeholders.py) and import with mipmaps like the manual's icons.
foreach (string path in new[] { HbrMagicSwordArt.SwordIcon, HbrMagicSwordArt.SakiPortrait, HbrMagicSwordArt.FateDiceIcon,
             HbrMagicSwordArt.FateFragmentIcon, HbrMagicSwordArt.FateDiceGlyph, HbrMagicSwordArt.CircleMask })
{
    Expect(path.StartsWith("res://", StringComparison.Ordinal), $"{path} is a res:// path");
    Expect(File.Exists(Path.Combine(repo, path["res://".Length..])), $"{path} exists");
}
string fateArt = Path.Combine(repo, "images", "ui", "hbr_fate_dice");
var fateRecords = JsonDocument.Parse(File.ReadAllText(Path.Combine(fateArt, "sources.json"))).RootElement
    .GetProperty("files").EnumerateArray().ToDictionary(r => r.GetProperty("file").GetString()!);
foreach (string path in new[] { HbrMagicSwordArt.FateDiceIcon, HbrMagicSwordArt.FateFragmentIcon, HbrMagicSwordArt.FateDiceGlyph })
{
    string name = Path.GetFileName(path);
    string file = Path.Combine(fateArt, name);
    Expect(fateRecords.TryGetValue(name, out JsonElement record), $"{name} is in hbr_fate_dice/sources.json");
    Expect(record.GetProperty("sha256").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(),
        $"{name} matches its SHA-256");
    var (pw, ph) = PngSize(file);
    Expect(record.GetProperty("size")[0].GetInt32() == pw && record.GetProperty("size")[1].GetInt32() == ph, $"{name} size recorded");
    string import = File.ReadAllText(file + ".import");
    Expect(import.Contains("mipmaps/generate=true", StringComparison.Ordinal) &&
           import.Contains($"source_file=\"{path}\"", StringComparison.Ordinal), $"{name} imports with mipmaps");
}
foreach (string file in new[] { "Core/Rewards/HbrFateFragmentReward.cs", "Core/Nodes/RestSite/FateDiceRepairRestSiteOption.cs" })
{
    string source = File.ReadAllText(Path.Combine(repo, file));
    Expect(source.Contains("HbrMagicSwordArt.FateFragmentIcon", StringComparison.Ordinal) &&
           !source.Contains("soul_of_anchor", StringComparison.Ordinal), $"{file} shows the fragment placeholder, not the anchor soul");
}
Expect(File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Screens", "NManualPhoneHome.cs"))
        .Contains("HbrManualHomeButton.FateDice => HbrMagicSwordArt.FateDiceGlyph", StringComparison.Ordinal),
    "the manual's 命运骰子 button shows the dice glyph");

// ---------------------------------------------------------------- merchant fate fragment stand
// The stand's one or two slots sit in the rug's free column right of the card removal service. Each vanilla
// slot is read from the recovered scenes: its place on the rug, and its hit box and price at rest (the slot
// scene's 0.65) and hovered (NMerchantSlot's 0.8). A hovered stand slot clears every resting vanilla slot and a
// resting stand slot clears every hovered one, since only one thing is hovered at a time; both stay on the rug's
// cloth, read from shop_rug.png drawn over the rug's 1747x978.
string merchant = Path.Combine(scenes, "merchant");
if (File.Exists(Path.Combine(merchant, "merchant_inventory.tscn")))
{
    var rugNodes = SceneNodes(Path.Combine(merchant, "merchant_inventory.tscn"));
    Expect(Num(rugNodes["SlotsContainer"], "offset_right") - Num(rugNodes["SlotsContainer"], "offset_left") == Shelf.RugWidth &&
           Num(rugNodes["SlotsContainer"], "offset_bottom") - Num(rugNodes["SlotsContainer"], "offset_top") == Shelf.RugHeight,
        "the rug is 1747x978 as the stand assumes");
    var vanilla = new List<(string Name, HbrHudRect Still, HbrHudRect Hover)>();
    void AddSlots(string container, string slotScene)
    {
        var holder = rugNodes["SlotsContainer/" + container];
        foreach (var (path, props) in rugNodes.Where(n => n.Key.StartsWith("SlotsContainer/" + container + "/", StringComparison.Ordinal) &&
                                                          n.Key.Count(c => c == '/') == 2))
        {
            float x = Num(holder, "offset_left") + Num(props, "offset_left");
            float y = Num(holder, "offset_top") + Num(props, "offset_top");
            vanilla.Add(SlotBox(path, Path.Combine(merchant, slotScene), x, y));
        }
    }
    AddSlots("CharacterCards", "merchant_card.tscn");
    AddSlots("ColorlessCards", "merchant_card.tscn");
    AddSlots("Relics", "merchant_relic.tscn");
    AddSlots("Potions", "merchant_potion.tscn");
    var removal = rugNodes["SlotsContainer/MerchantCardRemoval"];
    vanilla.Add(SlotBox("MerchantCardRemoval", Path.Combine(merchant, "merchant_card_removal.tscn"),
        Num(removal, "offset_left"), Num(removal, "offset_top")));
    Expect(vanilla.Count == 14, $"5 + 2 cards, 3 relics, 3 potions and card removal read from the scene ({vanilla.Count})");

    var rugImage = ReadPng(Path.Combine(scenes, "..", "images", "rooms", "merchant_room", "shop_rug.png"));
    bool OnCloth(float x, float y)
    {
        int px = (int)(x * rugImage.Width / Shelf.RugWidth), py = (int)(y * rugImage.Height / Shelf.RugHeight);
        return px >= 0 && py >= 0 && px < rugImage.Width && py < rugImage.Height && rugImage.Rgba[(py * rugImage.Width + px) * 4 + 3] >= 200;
    }
    for (int count = 1; count <= 2; count++)
    for (int i = 0; i < count; i++)
    {
        HbrHudRect rest = Shelf.Footprint(i, count, 1f);
        HbrHudRect hover = Shelf.Footprint(i, count, Shelf.HoverScale);
        Expect(Shelf.Slot(i, count) == rest, "a slot at rest is its own rectangle");
        foreach (var (name, vanillaRest, vanillaHover) in vanilla)
            Expect(!hover.Overlaps(vanillaRest) && !rest.Overlaps(vanillaHover), $"stand slot {i + 1}/{count} {rest}/{hover} clears {name} {vanillaRest}/{vanillaHover}");
        Expect(hover.X >= 0f && hover.Y >= 0f && hover.Right <= Shelf.RugWidth && hover.Bottom <= Shelf.RugHeight,
            $"stand slot {i + 1}/{count} stays on the rug");
        float cx = Shelf.ColumnX, cy = Shelf.CentreY(i, count), r = Shelf.Icon * Shelf.HoverScale / 2f;
        for (int k = 0; k < 8; k++)
            Expect(OnCloth(cx + r * MathF.Cos(k * MathF.PI / 4f), cy + r * MathF.Sin(k * MathF.PI / 4f)),
                $"stand slot {i + 1}/{count}'s hovered icon is on the rug cloth (direction {k})");
        Expect(OnCloth(rest.X + 8f, rest.Bottom - 4f) && OnCloth(rest.Right - 8f, rest.Bottom - 4f),
            $"stand slot {i + 1}/{count}'s price is on the rug cloth");
    }
    Expect(!Shelf.Footprint(0, 2, Shelf.HoverScale).Overlaps(Shelf.Footprint(1, 2, 1f)) &&
           !Shelf.Footprint(1, 2, Shelf.HoverScale).Overlaps(Shelf.Footprint(0, 2, 1f)), "the two stand slots clear each other");
}
else
{
    Console.WriteLine($"skip merchant stand: {merchant} not found");
}
string shelfSource = File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Merchant", "NHbrFateFragmentShelf.cs"));
Expect(shelfSource.Contains("Op = HbrFateDiceOp.Buy, Fragment = offer.Fragment, ShopKey = _manual.CurrentFateShopKey", StringComparison.Ordinal) &&
       shelfSource.Contains("HbrFateDiceNetActions.Request(_manual, command)", StringComparison.Ordinal) &&
       !Regex.IsMatch(shelfSource, @":\s*MerchantEntry\b|OnTryPurchaseWrapper|LoseGold"),
    "the stand buys with the manual's synced Buy command, not as a vanilla merchant entry");

Console.WriteLine($"hbr-battle-hud: {checks} checks passed");
return 0;

// ---------------------------------------------------------------- helpers
// The nodes of a .tscn by path below its root ("" is the root), each with its property lines.
static Dictionary<string, Dictionary<string, string>> SceneNodes(string scene)
{
    var nodes = new Dictionary<string, Dictionary<string, string>>();
    Dictionary<string, string>? current = null;
    foreach (string raw in File.ReadLines(scene))
    {
        string line = raw.Trim();
        Match header = Regex.Match(line, "^\\[node name=\"([^\"]+)\"");
        if (header.Success)
        {
            Match parent = Regex.Match(line, "parent=\"([^\"]*)\"");
            string name = header.Groups[1].Value;
            string path = !parent.Success ? "" : parent.Groups[1].Value == "." ? name : parent.Groups[1].Value + "/" + name;
            nodes[path] = current = new Dictionary<string, string>();
            continue;
        }
        if (line.StartsWith('[')) { current = null; continue; }
        int eq = line.IndexOf(" = ", StringComparison.Ordinal);
        if (current != null && eq > 0) current[line[..eq]] = line[(eq + 3)..];
    }
    return nodes;
}

// A number property, 0 when the scene leaves it at its default; Vector2(a, b) reads a.
static float Num(Dictionary<string, string> props, string key)
{
    if (!props.TryGetValue(key, out string? value)) return 0f;
    int open = value.IndexOf('(');
    Match number = Regex.Match(open >= 0 ? value[(open + 1)..] : value, @"-?\d+(\.\d+)?");
    return float.Parse(number.Value, CultureInfo.InvariantCulture);
}

// A vanilla merchant slot placed at (x, y) on the rug: its hit box and price box, at the slot scene's own scale
// (0.65) and at NMerchantSlot's hover scale (0.8), both growing around the slot's origin.
static (string Name, HbrHudRect Still, HbrHudRect Hover) SlotBox(string name, string slotScene, float x, float y)
{
    var nodes = SceneNodes(slotScene);
    HbrHudRect Local(string node) => new(Num(nodes[node], "offset_left"), Num(nodes[node], "offset_top"),
        Num(nodes[node], "offset_right") - Num(nodes[node], "offset_left"),
        Num(nodes[node], "offset_bottom") - Num(nodes[node], "offset_top"));
    HbrHudRect hit = Local("Hitbox"), cost = Local("Cost");
    float left = MathF.Min(hit.X, cost.X), top = MathF.Min(hit.Y, cost.Y);
    var box = new HbrHudRect(left, top, MathF.Max(hit.Right, cost.Right) - left, MathF.Max(hit.Bottom, cost.Bottom) - top);
    HbrHudRect At(float scale) => new(x + box.X * scale, y + box.Y * scale, box.Width * scale, box.Height * scale);
    float rest = Num(nodes[""], "scale");
    return (name, At(rest), At(0.8f));
}

static (int Width, int Height) PngSize(string path)
{
    byte[] head = new byte[24];
    using (var stream = File.OpenRead(path)) stream.ReadExactly(head);
    return (BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(20)));
}

// Minimal PNG reader: 8-bit grey, grey+alpha, RGB or RGBA, not interlaced; returns RGBA.
static (int Width, int Height, byte[] Rgba) ReadPng(string path)
{
    byte[] data = File.ReadAllBytes(path);
    int width = 0, height = 0, channels = 4, pos = 8;
    byte[]? palette = null, alphas = null;
    using var idat = new MemoryStream();
    while (pos < data.Length)
    {
        int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
        string type = Encoding.ASCII.GetString(data, pos + 4, 4);
        ReadOnlySpan<byte> body = data.AsSpan(pos + 8, length);
        if (type == "IHDR")
        {
            width = BinaryPrimitives.ReadInt32BigEndian(body);
            height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
            if (body[8] != 8 || body[12] != 0) throw new InvalidDataException($"{path}: expected 8-bit, not interlaced");
            channels = body[9] switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"{path}: colour type {body[9]}") };
            if (body[9] == 3) palette = Array.Empty<byte>();
        }
        else if (type == "PLTE") palette = body.ToArray();
        else if (type == "tRNS") alphas = body.ToArray();
        else if (type == "IDAT") idat.Write(body);
        else if (type == "IEND") break;
        pos += 12 + length;
    }
    idat.Position = 0;
    using var inflated = new MemoryStream();
    using (var zlib = new ZLibStream(idat, CompressionMode.Decompress)) zlib.CopyTo(inflated);
    byte[] src = inflated.ToArray();
    int stride = width * channels;
    byte[] raw = new byte[height * stride];
    for (int y = 0; y < height; y++)
    {
        int filter = src[y * (stride + 1)], s = y * (stride + 1) + 1, d = y * stride;
        for (int x = 0; x < stride; x++)
        {
            int a = x >= channels ? raw[d + x - channels] : 0, b = y > 0 ? raw[d - stride + x] : 0;
            int c = x >= channels && y > 0 ? raw[d - stride + x - channels] : 0;
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            int paeth = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
            raw[d + x] = (byte)(src[s + x] + filter switch
            {
                0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, 4 => paeth,
                _ => throw new InvalidDataException($"{path}: filter {filter}")
            });
        }
    }
    byte[] rgba = new byte[width * height * 4];
    for (int i = 0; i < width * height; i++)
    {
        int o = i * 4, r = i * channels;
        if (palette != null)
        {
            int index = raw[r];
            rgba[o] = palette[index * 3]; rgba[o + 1] = palette[index * 3 + 1]; rgba[o + 2] = palette[index * 3 + 2];
            rgba[o + 3] = alphas != null && index < alphas.Length ? alphas[index] : (byte)255;
            continue;
        }
        switch (channels)
        {
            case 1: rgba[o] = rgba[o + 1] = rgba[o + 2] = raw[r]; rgba[o + 3] = 255; break;
            case 2: rgba[o] = rgba[o + 1] = rgba[o + 2] = raw[r]; rgba[o + 3] = raw[r + 1]; break;
            case 3: rgba[o] = raw[r]; rgba[o + 1] = raw[r + 1]; rgba[o + 2] = raw[r + 2]; rgba[o + 3] = 255; break;
            default: Array.Copy(raw, r, rgba, o, 4); break;
        }
    }
    return (width, height, rgba);
}

// Node paths of a .tscn below its root, e.g. "Visuals/Image".
static HashSet<string> ScenePaths(string scene)
{
    var paths = new HashSet<string>();
    foreach (Match match in Regex.Matches(File.ReadAllText(scene), "^\\[node name=\"([^\"]+)\"([^\\]]*)\\]", RegexOptions.Multiline))
    {
        Match parent = Regex.Match(match.Groups[2].Value, "parent=\"([^\"]+)\"");
        if (!parent.Success) continue; // the scene root
        string under = parent.Groups[1].Value;
        paths.Add(under == "." ? match.Groups[1].Value : under + "/" + match.Groups[1].Value);
    }
    return paths;
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

// Member names ("m:", "p:", "f:", "e:") and method signatures ("s:Name:Return(Params)") of the given types.
static Dictionary<string, HashSet<string>> ReadTypes(string dll, IEnumerable<string> fullNames)
{
    using var stream = File.OpenRead(dll);
    using var pe = new PEReader(stream);
    MetadataReader md = pe.GetMetadataReader();
    var wanted = new HashSet<string>(fullNames);
    var provider = new NameProvider();
    var result = new Dictionary<string, HashSet<string>>();
    foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
    {
        TypeDefinition type = md.GetTypeDefinition(handle);
        string full = NameProvider.FullName(md, type.Namespace, type.Name);
        if (!wanted.Contains(full)) continue;
        var members = new HashSet<string>();
        foreach (MethodDefinitionHandle m in type.GetMethods())
        {
            MethodDefinition method = md.GetMethodDefinition(m);
            string name = md.GetString(method.Name);
            members.Add("m:" + name);
            MethodSignature<string> signature = method.DecodeSignature(provider, null);
            members.Add($"s:{name}:{signature.ReturnType}({string.Join(",", signature.ParameterTypes)})");
        }
        foreach (PropertyDefinitionHandle p in type.GetProperties()) members.Add("p:" + md.GetString(md.GetPropertyDefinition(p).Name));
        foreach (FieldDefinitionHandle f in type.GetFields()) members.Add("f:" + md.GetString(md.GetFieldDefinition(f).Name));
        foreach (EventDefinitionHandle e in type.GetEvents()) members.Add("e:" + md.GetString(md.GetEventDefinition(e).Name));
        result[full] = members;
    }
    return result;
}

sealed class NameProvider : ISignatureTypeProvider<string, object?>
{
    internal static string FullName(MetadataReader md, StringHandle ns, StringHandle name)
    {
        string space = md.GetString(ns);
        return space.Length == 0 ? md.GetString(name) : space + "." + md.GetString(name);
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        return FullName(reader, type.Namespace, type.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        TypeReference type = reader.GetTypeReference(handle);
        return FullName(reader, type.Namespace, type.Name);
    }
    public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPinnedType(string elementType) => elementType;
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? context, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
}
