using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Cards;

string repo = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../.."));
int checks = 0;

void Expect(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
}

string FileOf(string resPath)
{
    Expect(resPath.StartsWith("res://", StringComparison.Ordinal), $"{resPath} is a res:// path");
    return Path.Combine(repo, resPath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
}

// Minimal PNG reader for the exporter's output: 8-bit RGBA, not interlaced.
(int Width, int Height, byte[] Rgba) ReadPng(string path)
{
    byte[] data = File.ReadAllBytes(path);
    int width = 0, height = 0, pos = 8;
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
            if (body[8] != 8 || body[9] != 6 || body[12] != 0)
                throw new InvalidDataException($"{path}: expected 8-bit RGBA without interlacing");
        }
        else if (type == "IDAT") idat.Write(body);
        else if (type == "IEND") break;
        pos += 12 + length;
    }
    idat.Position = 0;
    using var inflated = new MemoryStream();
    using (var zlib = new ZLibStream(idat, CompressionMode.Decompress)) zlib.CopyTo(inflated);
    byte[] src = inflated.ToArray();
    int stride = width * 4;
    byte[] rgba = new byte[height * stride];
    for (int y = 0; y < height; y++)
    {
        int filter = src[y * (stride + 1)], s = y * (stride + 1) + 1, d = y * stride;
        for (int x = 0; x < stride; x++)
        {
            int a = x >= 4 ? rgba[d + x - 4] : 0, b = y > 0 ? rgba[d - stride + x] : 0;
            int c = x >= 4 && y > 0 ? rgba[d - stride + x - 4] : 0;
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            int paeth = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
            rgba[d + x] = (byte)(src[s + x] + filter switch
            {
                0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, 4 => paeth,
                _ => throw new InvalidDataException($"{path}: filter {filter}")
            });
        }
    }
    return (width, height, rgba);
}

// Alpha of the texture pixel under a point in NCard space, for a texture drawn over `rect` at `scale`.
int AlphaAt((int Width, int Height, byte[] Rgba) png, HbrCardRect rect, int scale, float x, float y)
{
    int px = (int)((x - rect.Left) * scale), py = (int)((y - rect.Top) * scale);
    return png.Rgba[(py * png.Width + px) * 4 + 3];
}

void ExpectTexture(string resPath, HbrCardRect rect, int scale, string description)
{
    string file = FileOf(resPath);
    Expect(File.Exists(file), $"{description}: {file} exists");
    var png = ReadPng(file);
    Expect(png.Width == (int)(rect.Width * scale) && png.Height == (int)(rect.Height * scale),
        $"{description}: {png.Width}x{png.Height} is {rect.Width}x{rect.Height} at {scale}x");
}

// Frames: one per theme and rarity, drawn over the vanilla Frame rect at 2x.
var frame = HbrCardStyleLayout.Frame;
foreach (HbrCardTheme theme in Enum.GetValues<HbrCardTheme>())
foreach (HbrCardTier tier in Enum.GetValues<HbrCardTier>())
{
    string path = HbrCardStyleLayout.FramePath(theme, tier);
    ExpectTexture(path, frame, 2, $"{theme} {tier} frame");
    var png = ReadPng(FileOf(path));
    var window = HbrCardStyleLayout.ArtWindow;
    // The upper half of the window is clear; the overlay only darkens its lower half. Memory frames add faint
    // scanlines (white at 10%) and crack lines over the whole window, hence the allowance.
    int open = theme == HbrCardTheme.Memory ? 40 : 0;
    Expect(AlphaAt(png, frame, 2, window.Left + 3, window.Top + 3) <= open, $"{theme} {tier}: window open at its top left");
    Expect(AlphaAt(png, frame, 2, window.Right - 3, window.Top + 3) <= open, $"{theme} {tier}: window open at its top right");
    Expect(AlphaAt(png, frame, 2, (window.Left + window.Right) / 2, window.Top + 40) <= Math.Max(open, 8),
        $"{theme} {tier}: window open in the middle of its upper half");
    Expect(AlphaAt(png, frame, 2, window.Left - 4, window.Top + 40) == 255, $"{theme} {tier}: body solid left of the window");
    Expect(AlphaAt(png, frame, 2, 0, window.Bottom + 4) == 255, $"{theme} {tier}: body solid under the window");
    Expect(AlphaAt(png, frame, 2, 0, HbrCardStyleLayout.Description.Bottom - 4) == 255, $"{theme} {tier}: body solid behind the description");
    // The window's lower right is cut on a slant, so the body shows there.
    Expect(AlphaAt(png, frame, 2, window.Right - 3, window.Bottom - 3) == 255, $"{theme} {tier}: window corner cut");
    // Top-right and bottom-left corners of the card are cut away; 31O sets a small gold ornament in the very tip.
    Expect(AlphaAt(png, frame, 2, frame.Right - 10, frame.Top + 6) <= 8, $"{theme} {tier}: top-right corner cut");
    Expect(AlphaAt(png, frame, 2, frame.Left + 10, frame.Bottom - 6) <= 8, $"{theme} {tier}: bottom-left corner cut");
    bool ornament = AlphaAt(png, frame, 2, frame.Right - 2, frame.Top + 3) == 255 &&
        AlphaAt(png, frame, 2, frame.Left + 2, frame.Bottom - 3) == 255;
    Expect(ornament == (theme == HbrCardTheme.Saki31O), $"{theme} {tier}: gold corner ornaments only on 31O");
}
ExpectTexture(HbrCardStyleLayout.PhantomGlowPath, HbrCardStyleLayout.PhantomGlow, 2, "phantom glow");
var glow = ReadPng(FileOf(HbrCardStyleLayout.PhantomGlowPath));
Expect(AlphaAt(glow, HbrCardStyleLayout.PhantomGlow, 2, 0, 0) == 0, "phantom glow is hollow");
Expect(AlphaAt(glow, HbrCardStyleLayout.PhantomGlow, 2, frame.Left - 3, 0) > 60, "phantom glow shows just outside the card");
// The type plate carries no attribute icon any more (user: 「这个斩就不要显示了」); only its colour bar.
foreach (HbrCardKind kind in Enum.GetValues<HbrCardKind>())
    Expect(HbrCardStyleLayout.KindAccent(kind).Length == 6, $"{kind} accent colour");
Console.WriteLine("PASS textures");

bool Inside(HbrCardRect inner, HbrCardRect outer) =>
    inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

var art = HbrCardStyleLayout.ArtWindow;
var portrait = HbrCardStyleLayout.Portrait;
Expect(Inside(art, frame), "portrait window inside the card");
Expect(portrait.Left < art.Left && portrait.Top < art.Top && portrait.Right > art.Right && portrait.Bottom > art.Bottom,
    "portrait overshoots its window on every side");
Expect(Math.Abs(portrait.Width / portrait.Height - art.Width / art.Height) < 0.01, "portrait keeps the window's shape");
Expect(Inside(HbrCardStyleLayout.TitleBand, frame), "title band inside the card");
var title = HbrCardStyleLayout.Title;
Expect(title.Left >= HbrCardStyleLayout.TitleBand.Left && title.Right <= HbrCardStyleLayout.TitleBand.Right,
    "title within the band");
Expect(Math.Abs((title.Top + title.Bottom) / 2 - (HbrCardStyleLayout.TitleBand.Top + HbrCardStyleLayout.TitleBand.Bottom) / 2) <= 1,
    "title centred on the band");
float plateBottom = HbrCardStyleLayout.PlateTop + HbrCardStyleLayout.PlateHeight;
var description = HbrCardStyleLayout.Description;
Expect(HbrCardStyleLayout.PlateLeft >= art.Left && HbrCardStyleLayout.PlateTop < art.Bottom,
    "type plate sits on the portrait's lower left edge");
Expect(plateBottom <= description.Top, "description starts below the type plate");
Expect(Inside(description, frame) && description.Bottom <= frame.Bottom - 18, "description clears the slash and the gem");
Expect(HbrCardStyleLayout.PlateLeft + HbrCardStyleLayout.PlateWidth(120) <= art.Right, "a long type name still fits under the portrait");
Expect(HbrCardStyleLayout.PlateWidth(-5) == HbrCardStyleLayout.PlateLabelLeft + HbrCardStyleLayout.PlateLabelRight,
    "plate width ignores negative label widths");
Expect(HbrCardStyleLayout.PlateLabelLeft > HbrCardStyleLayout.PlateBarWidth + 4 && HbrCardStyleLayout.PlateLabelLeft <= 16,
    "type name starts just after the colour bar, with no icon in front");
Console.WriteLine("PASS layout");

// Six-sense card attributes and enemy resistances were removed (2026-11-09): no card badge, enhanced
// glow, hand focus, resistance dots or value callout is drawn any more.
foreach (string removed in new[] { "Core/Nodes/Cards/NSixSenseEnergyIcon.cs", "Core/Nodes/Cards/SixSenseHandFocus.cs",
             "Core/Nodes/Combat/NSixSenseBadge.cs", "Core/Nodes/Combat/NSixSenseResistanceRow.cs",
             "Core/Nodes/Combat/NSixSenseResistanceCallout.cs", "Core/Patches/Combat/SixSenseResistanceHoverPatch.cs",
             "Core/Patches/Combat/SixSenseResistancePatch.cs" })
    Expect(!File.Exists(Path.Combine(repo, removed)), $"{removed} is gone");
string integrationSource = File.ReadAllText(Path.Combine(repo, "Core", "Integration", "HBRGOSakiCombatUiIntegration.cs"));
Expect(!integrationSource.Contains("SixSense", StringComparison.Ordinal), "no six-sense node is attached to cards or health bars");
string layoutSource = File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Cards", "HbrCardStyleLayout.cs"));
Expect(!layoutSource.Contains("Sense", StringComparison.Ordinal) && !layoutSource.Contains("Pip", StringComparison.Ordinal),
    "the card style layout keeps no badge or resistance dot geometry");
foreach (string file in new[] { "StrikePortraitPatch.cs", "Combat/DeflectorProtectionHealthBarPatch.cs" })
    Expect(!File.ReadAllText(Path.Combine(repo, "Core", "Patches", file)).Contains("SixSense", StringComparison.Ordinal),
        $"{file} refreshes no six-sense node");
Console.WriteLine("PASS six-sense badge and resistance dots removed");

Expect(HbrCardStyleLayout.FramePath(HbrCardTheme.Phantom, HbrCardTier.Rare) == HbrCardStyleLayout.FramePath(HbrCardTheme.Saki30A, HbrCardTier.Rare),
    "phantom cards wear the 30A frame");
Expect(Enum.GetValues<HbrCardTheme>().Select(t => HbrCardStyleLayout.FrameKey(t)).Distinct().Count() == 4, "four frame sets");

// The art window is filled whatever the portrait's shape: a letterboxed portrait (keep aspect, centred)
// would let the card's highlight and shadow show through above and below the art.
string cardNodes = Path.Combine(repo, "Core", "Nodes", "Cards");
string styleSource = File.ReadAllText(Path.Combine(cardNodes, "NHbrCardStyle.cs"));
Expect(styleSource.Contains("_portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;", StringComparison.Ordinal),
    "styled cards cover the art window");
Expect(styleSource.Contains("_portrait.StretchMode = _original.PortraitStretch;", StringComparison.Ordinal),
    "vanilla cards get their stretch back");
Expect(File.ReadAllText(Path.Combine(cardNodes, "NHbrgoStrikePortrait.cs")).Contains("NHbrCardStyle.IsStyled(model)", StringComparison.Ordinal),
    "the strike portrait keeps styled cards covered when it swaps art");
var artWindow = HbrCardStyleLayout.ArtWindow;
var portraitRect = HbrCardStyleLayout.Portrait;
Expect(portraitRect.Left <= artWindow.Left && portraitRect.Top <= artWindow.Top &&
       portraitRect.Right >= artWindow.Right && portraitRect.Bottom >= artWindow.Bottom,
    "the portrait bleeds past the art window on every side");
// The gaps seen in game on v0.111.0 were the vanilla attack frame: that version assigns the frame, banner
// and portrait border in NCard.UpdatePortrait (also called from UpdateVisuals) instead of Reload, so the
// HBR frame has to be put back after it. Stable has no UpdatePortrait and skips the patch.
string patchSource = File.ReadAllText(Path.Combine(repo, "Core", "Patches", "HbrCardStylePatch.cs"));
Expect(patchSource.Contains("AccessTools.DeclaredMethod(typeof(NCard), \"UpdatePortrait\")", StringComparison.Ordinal) &&
       patchSource.Contains("private static bool Prepare() => TargetMethod() != null;", StringComparison.Ordinal) &&
       patchSource.Contains("style.ReapplyFrame();", StringComparison.Ordinal), "the HBR frame is put back after UpdatePortrait where it exists");
int reapply = styleSource.IndexOf("internal void ReapplyFrame()", StringComparison.Ordinal);
string reapplyBody = reapply < 0 ? "" : styleSource[reapply..styleSource.IndexOf("\n    }", reapply, StringComparison.Ordinal)];
Expect(reapplyBody.Contains("TryResolve(Card.Model, out HbrCardTheme theme, out HbrCardTier tier, out _)", StringComparison.Ordinal) &&
       reapplyBody.Contains("_frame.Texture = HbrCardStyleTextures.Get(HbrCardStyleLayout.FramePath(theme, tier));", StringComparison.Ordinal) &&
       reapplyBody.Contains("_frame.Material = null;", StringComparison.Ordinal) &&
       reapplyBody.Contains("_portraitBorder.Visible = false;", StringComparison.Ordinal) &&
       reapplyBody.Contains("_titleBanner.Visible = false;", StringComparison.Ordinal),
    "the frame is reapplied only for a card that is still styled, from its current theme and rarity");
string plateSource = File.ReadAllText(Path.Combine(cardNodes, "NHbrCardTypePlate.cs"));
Expect(!plateSource.Contains("TextureRect", StringComparison.Ordinal) && !plateSource.Contains("Texture2D", StringComparison.Ordinal) &&
       !styleSource.Contains("TypeIconPath", StringComparison.Ordinal), "the type plate draws no attribute icon");
Console.WriteLine("PASS art window fill");

// ---- Memory garden names come back with upgrades (ui-concepts/memory-garden-names-20261106) ----
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Encoding gbk = Encoding.GetEncoding(936);
Expect(HbrMemoryTitleReveal.Moji.Length == 512, "the mojibake table holds 8 lead bytes x 64 trail bytes");
byte[] leads = { 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEF };
for (int lead = 0; lead < leads.Length; lead++)
    for (int trail = 0; trail < 64; trail++)
        Expect(gbk.GetString(new[] { leads[lead], (byte)(0x80 + trail) }) == HbrMemoryTitleReveal.Moji[lead * 64 + trail].ToString(),
            $"mojibake table entry {leads[lead]:X2} {0x80 + trail:X2} is the GBK reading");

// The fourteen garden cards (HbrMemoryGarden.Characters) and their Chinese names.
string[] gardenIds =
{
    "HER_SONG_IS_MY_SCREAM_HBRGO_SAKI", "MATCHA_PARFAIT_HBRGO_SAKI", "LOST_STAR_SONG_HBRGO_SAKI", "ANON_AID_HBRGO_SAKI",
    "SOYO_AID_HBRGO_SAKI", "HALF_BODY_HBRGO_SAKI", "MY_LEGEND_BEGINS_HBRGO_SAKI", "HELLO_WORLD_HBRGO_SAKI",
    "SAVIOR_GRAND_ENTRANCE_HBRGO_SAKI", "WORLDS_FINEST_HBRGO_SAKI", "MORE_THAN_ONE_TRUTH_HBRGO_SAKI",
    "CRUSH_YOU_LIKE_A_BUG_HBRGO_SAKI", "SUMMER_GRASS_DREAM_HBRGO_SAKI", "BOOST_UP_HBRGO_SAKI"
};
using var cardsJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
    Path.Combine(repo, "HBRGOSakiMod", "localization", "zhs", "cards.json")));
string[] gardenTitles = gardenIds.Select(id => cardsJson.RootElement.GetProperty(id + ".title").GetString()!).ToArray();
List<Rune> Runes(string text) => text.EnumerateRunes().ToList();
foreach (string gardenName in gardenTitles)
    foreach (Rune rune in gardenName.EnumerateRunes())
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(rune.ToString());
        if (utf8.Length == 3 && (utf8[0] is >= 0xE3 and <= 0xE9 || utf8[0] == 0xEF))
            Expect(HbrMemoryTitleReveal.MojiOf(rune)?.ToString() == gbk.GetString(utf8, 0, 2),
                $"{rune} garbles to its first two UTF-8 bytes read as GBK");
    }
Expect(HbrMemoryTitleReveal.MojiOf(new Rune('我')) == '鎴' && HbrMemoryTitleReveal.MojiOf(new Rune('、')) == '銆' &&
       HbrMemoryTitleReveal.MojiOf(new Rune('！')) == '锛' && HbrMemoryTitleReveal.MojiOf(new Rune('A')) == null,
    "我 -> 鎴, 、 -> 銆, ！ -> 锛, and Latin falls outside the table");

// Every glyph a garbled name can show is in the game's Chinese card font (ZhsFontPathSet: Noto Sans Mono CJK SC).
string fontPath = args.Length > 1 ? args[1] : @"D:\stsmod\sts\fonts\zhs\NotoSansMonoCJKsc-Regular.otf";
if (File.Exists(fontPath))
{
    HashSet<int> mapped = ReadCmap(File.ReadAllBytes(fontPath));
    foreach (char glyph in HbrMemoryTitleReveal.Moji + HbrMemoryTitleReveal.LatinNoise + "「」")
        Expect(mapped.Contains(glyph), $"the card font has {glyph} (U+{(int)glyph:X4})");
    foreach (string gardenName in gardenTitles)
        foreach (Rune rune in gardenName.EnumerateRunes())
            Expect(Rune.IsWhiteSpace(rune) || mapped.Contains(rune.Value), $"the card font has {rune} from a garden name");
}
else Console.WriteLine($"SKIP font coverage: {fontPath} not found");

// Parts: obtaining is one, each upgrade one more, out of the cap + 1; at least one character once obtained.
Expect(HbrMemoryTitleReveal.TotalParts(7) == 8 && HbrMemoryTitleReveal.TotalParts(6) == 7, "cap 7 is 8 parts, cap 6 is 7");
Expect(HbrMemoryTitleReveal.OwnedParts(0, 7) == 1 && HbrMemoryTitleReveal.OwnedParts(7, 7) == 8 &&
       HbrMemoryTitleReveal.OwnedParts(9, 7) == 8 && HbrMemoryTitleReveal.OwnedParts(-1, 7) == 1, "owned parts follow the level, capped");
Expect(HbrMemoryTitleReveal.Shown(10, 0, 8) == 0 && HbrMemoryTitleReveal.Shown(10, 1, 8) == 1 &&
       HbrMemoryTitleReveal.Shown(2, 1, 8) == 1 && HbrMemoryTitleReveal.Shown(20, 4, 8) == 10 &&
       HbrMemoryTitleReveal.Shown(10, 8, 8) == 10 && HbrMemoryTitleReveal.Shown(13, 7, 7) == 13 &&
       HbrMemoryTitleReveal.Shown(2, 7, 8) == 1, "shown characters: floor(n x parts / total), at least one, all at the cap");
for (int total = 7; total <= 8; total++)
    for (int countable = 1; countable <= 24; countable++)
        for (int parts = 0; parts < total; parts++)
            Expect(HbrMemoryTitleReveal.Shown(countable, parts, total) <= HbrMemoryTitleReveal.Shown(countable, parts + 1, total),
                $"no character goes back into hiding ({countable} characters, {parts} -> {parts + 1} of {total})");

for (int index = 0; index < gardenIds.Length; index++)
{
    string id = gardenIds[index], gardenName = gardenTitles[index];
    int total = index is 6 or 11 or 12 ? 7 : 8; // MyLegendBegins, CrushYouLikeABug and SummerGrassDream cap at 6
    List<Rune> original = Runes(gardenName);
    int[] order = HbrMemoryTitleReveal.Order(id, original);
    Expect(order.Length == original.Count(rune => !HbrMemoryTitleReveal.IsKept(rune)) && order.Distinct().Count() == order.Length,
        $"{gardenName}: every hidden position appears once in the reveal order");
    Expect(order.SequenceEqual(HbrMemoryTitleReveal.Order(id, original)), $"{gardenName}: the order is the same every time");
    Expect(HbrMemoryTitleReveal.Garble(id, gardenName, total, total) == gardenName, $"{gardenName}: all parts show the full name");
    HashSet<int> before = new();
    for (int parts = 0; parts <= total; parts++)
    {
        List<Rune> shown = Runes(HbrMemoryTitleReveal.Garble(id, gardenName, parts, total));
        Expect(shown.Count == original.Count, $"{gardenName}: {parts} parts keep the length");
        HashSet<int> revealed = Enumerable.Range(0, original.Count)
            .Where(i => !HbrMemoryTitleReveal.IsKept(original[i]) && shown[i] == original[i]).ToHashSet();
        Expect(Enumerable.Range(0, original.Count).Where(i => HbrMemoryTitleReveal.IsKept(original[i])).All(i => shown[i] == original[i]),
            $"{gardenName}: punctuation and spaces always show ({parts} parts)");
        Expect(revealed.SetEquals(order.Take(HbrMemoryTitleReveal.Shown(order.Length, parts, total))),
            $"{gardenName}: {parts} parts show exactly the first characters of the order");
        Expect(before.IsSubsetOf(revealed), $"{gardenName}: {parts} parts keep what fewer parts showed");
        before = revealed;
        for (int i = 0; i < original.Count; i++)
        {
            if (HbrMemoryTitleReveal.IsKept(original[i]) || revealed.Contains(i)) continue;
            char? moji = HbrMemoryTitleReveal.MojiOf(original[i]);
            Expect(original[i].Value < 0x2E80
                    ? HbrMemoryTitleReveal.LatinNoise.Contains(shown[i].ToString(), StringComparison.Ordinal)
                    : moji.HasValue && shown[i].Value == moji.Value,
                $"{gardenName}: hidden {original[i]} reads as its mojibake ({parts} parts)");
        }
    }
    // In the garden a few hidden characters jump with the static; nothing that has come back jumps.
    string still = HbrMemoryTitleReveal.Garble(id, gardenName, 1, total);
    int jumped = 0, hidden = order.Length - HbrMemoryTitleReveal.Shown(order.Length, 1, total);
    for (long step = 0; step < 40; step++)
    {
        List<Rune> moving = Runes(HbrMemoryTitleReveal.Garble(id, gardenName, 1, total, step));
        List<Rune> resting = Runes(still);
        int changed = Enumerable.Range(0, moving.Count).Count(i => moving[i] != resting[i]);
        jumped += changed;
        Expect(order.Take(HbrMemoryTitleReveal.Shown(order.Length, 1, total)).All(i => moving[i] == original[i]),
            $"{gardenName}: revealed characters do not jump (step {step})");
    }
    Expect(hidden == 0 || (jumped > 0 && jumped <= hidden * 40 * 0.45), $"{gardenName}: hidden characters jump now and then, not all the time");
    // Decoding from 1 part to 3: still hidden at the start, flickering in the middle, settled at the end.
    int fromShown = HbrMemoryTitleReveal.Shown(order.Length, 1, total), toShown = HbrMemoryTitleReveal.Shown(order.Length, 3, total);
    int count = HbrMemoryTitleReveal.DecodeCount(gardenName, id, 1, 3, total);
    Expect(count == toShown - fromShown, $"{gardenName}: the decode brings back {toShown - fromShown} characters");
    Expect(HbrMemoryTitleReveal.Garble(id, gardenName, 3, total, -1, 1, 0) == HbrMemoryTitleReveal.Garble(id, gardenName, 1, total),
        $"{gardenName}: a decode starts from the old name");
    Expect(HbrMemoryTitleReveal.Garble(id, gardenName, 3, total, -1, 1, HbrMemoryTitleReveal.DecodeDuration(count)) ==
           HbrMemoryTitleReveal.Garble(id, gardenName, 3, total), $"{gardenName}: a decode ends on the new name");
    if (count > 0)
    {
        List<Rune> middle = Runes(HbrMemoryTitleReveal.Garble(id, gardenName, 3, total, -1, 1,
            HbrMemoryTitleReveal.DecodeDelay + HbrMemoryTitleReveal.DecodeLength / 2));
        int first = order[fromShown];
        string pool = original[first].Value < 0x2E80 ? HbrMemoryTitleReveal.LatinNoise : HbrMemoryTitleReveal.Moji;
        Expect(middle[first].IsBmp && pool.Contains((char)middle[first].Value),
            $"{gardenName}: the first decoding character flickers through garbage of its own script");
    }
}
Expect(HbrMemoryTitleReveal.Order(gardenIds[4], Runes(gardenTitles[4])).SequenceEqual(HbrMemoryTitleReveal.Order(gardenIds[0], Runes(gardenTitles[4]))) == false,
    "the card id seeds the order");
Expect(HbrMemoryTitleReveal.DecodeDuration(0) == 0 && Math.Abs(HbrMemoryTitleReveal.DecodeDuration(3) - (.25 + 2 * .14 + .52)) < 1e-9,
    "a decode takes .25s, .14s per extra character and .52s for the last");

// Wiring: card faces and garden labels go through HbrMemoryTitleState, whose state is per model (cards are pooled).
string stateSource = File.ReadAllText(Path.Combine(cardNodes, "HbrMemoryTitleState.cs"));
Expect(stateSource.Contains("ConditionalWeakTable<CardModel", StringComparison.Ordinal) &&
       stateSource.Contains("RunManager.Instance.IsInProgress", StringComparison.Ordinal) &&
       stateSource.Contains("HbrMemoryGarden.IsGardenCard(card)", StringComparison.Ordinal),
    "name state is kept per card model, only for the fourteen garden cards, only during a run");
int decorate = styleSource.IndexOf("internal void DecorateTitle()", StringComparison.Ordinal);
string decorateBody = decorate < 0 ? "" : styleSource[decorate..styleSource.IndexOf("\n    }", decorate, StringComparison.Ordinal)];
Expect(decorateBody.Contains("HbrMemoryTitleState.Applies(model)", StringComparison.Ordinal) &&
       decorateBody.Contains("RenderMemoryTitle(model);", StringComparison.Ordinal) &&
       styleSource.Contains("SetProcess(false);", StringComparison.Ordinal),
    "the memory gardenName is rebuilt from the model after every vanilla gardenName update; idle cards get no frame callback");
string screens = Path.Combine(repo, "Core", "Nodes", "Screens");
string gardenSource = File.ReadAllText(Path.Combine(screens, "NMemoryGardenScreen.cs"));
string upgradeSource = File.ReadAllText(Path.Combine(screens, "NMemoryGardenUpgradeScreen.cs"));
Expect(gardenSource.Contains("HbrMemoryTitleState.MarkUnowned(choice.PreviewCard)", StringComparison.Ordinal) &&
       gardenSource.Contains("「{HbrMemoryTitleState.Title(_selected.PreviewCard)}」", StringComparison.Ordinal) &&
       gardenSource.IndexOf("PlayTitleReveal(0)", StringComparison.Ordinal) <
       gardenSource.IndexOf("NMemoryGardenSublimation.Play(this, holder.CardNode", StringComparison.Ordinal),
    "the garden hides the names of cards not obtained, labels them the same way, and decodes a new card in the white light");
Expect(upgradeSource.Contains("HbrMemoryTitleState.Hold(option.PreviewCard, option.PreviewCard.CurrentUpgradeLevel - 1)", StringComparison.Ordinal) &&
       upgradeSource.IndexOf("PlayTitleReveal(heldParts)", StringComparison.Ordinal) > 0 &&
       upgradeSource.IndexOf("PlayTitleReveal(heldParts)", StringComparison.Ordinal) <
       upgradeSource.IndexOf("NMemoryGardenSublimation.Play(this, _previewCard", StringComparison.Ordinal),
    "a recollection preview keeps the old name until confirmed, then decodes before the card shatters");
string sealSource = File.ReadAllText(Path.Combine(screens, "NMemoryGardenSeal.cs"));
string sealShader = File.ReadAllText(Path.Combine(repo, "shaders", "hbr_memory_garden_seal.gdshader"));
Expect(!sealSource.Contains("？？？", StringComparison.Ordinal) && sealSource.Contains("Uv(HbrCardStyleLayout.ArtWindow, 0)", StringComparison.Ordinal) &&
       sealSource.Contains("Uv(HbrCardStyleLayout.Description, 4)", StringComparison.Ordinal),
    "a card not met yet shows its art window as a silhouette, not a ？？？ slate");
Expect(sealShader.Contains("uniform float reveal", StringComparison.Ordinal) &&
       sealShader.Contains("mix(ink, paper, smoothstep(0.42, 0.66, grey))", StringComparison.Ordinal) &&
       sealShader.Contains("smoothstep(wipe - 0.12, wipe, 1.0 - UV.y)", StringComparison.Ordinal),
    "the seal shader draws the two-tone silhouette and drains it from the bottom up");
// A newly met character without upgrade points drains straight into the sealed cover, never through colour.
Expect(sealShader.Contains("target_cover ? vec4(mix(cover, silhouette, keep), 1.0) : vec4(silhouette, keep)", StringComparison.Ordinal) &&
       sealSource.Contains("SetShaderParameter(\"target_cover\", state.Meeting && cover)", StringComparison.Ordinal),
    "a meeting drains into the slot's own cover when it has one");
// Purely cosmetic and per computer: relic save data is part of the multiplayer checksum.
string storeSource = File.ReadAllText(Path.Combine(screens, "HbrMemoryGardenShownStore.cs"));
string relics = Path.Combine(repo, "Core", "Models", "Relics");
Expect(storeSource.Contains("\"user://", StringComparison.Ordinal) &&
       Directory.GetFiles(relics, "ElectronicSoldierManual*.cs").All(file => !File.ReadAllText(file).Contains("Shown", StringComparison.Ordinal)),
    "the characters the garden has shown are stored on this computer, never in the manual relic's save data");
Console.WriteLine("PASS memory garden names");

// ---- Memory card portraits: every transparent cut-in sits on white (NHbrgoStrikePortrait) ----
// Tezuka (ADV cut-in Chase) and Nanami (Superiority) moved from wide illustrations to the slanted cut-in frame the
// other HBR memories use (2026-11-06), mirrored at the user's request.
string strikeSource = File.ReadAllText(Path.Combine(cardNodes, "NHbrgoStrikePortrait.cs"));
int whiteAt = strikeSource.IndexOf("bool white = model is ", StringComparison.Ordinal);
string whiteList = whiteAt < 0 ? "" : strikeSource[whiteAt..strikeSource.IndexOf(';', whiteAt)];
Expect(whiteAt >= 0 && strikeSource.Contains("SetWhiteBackground(_portrait, ref _whiteBackground, white);", StringComparison.Ordinal) &&
       strikeSource.Contains("SetWhiteBackground(_ancientPortrait, ref _ancientWhiteBackground, white);", StringComparison.Ordinal),
    "both portrait nodes take the same white-underlay list");
string portraitDir = Path.Combine(repo, "images", "packed", "card_portraits", "hbrgo_saki");
foreach (string id in gardenIds)
{
    string className = string.Concat(id.Split('_').Select(part => part[..1] + part[1..].ToLowerInvariant()));
    string file = Path.Combine(portraitDir, id.ToLowerInvariant() + ".png");
    byte[] png = File.ReadAllBytes(file);
    int colourType = png[25];
    bool transparent = colourType is 4 or 6 || Encoding.ASCII.GetString(png).Contains("tRNS", StringComparison.Ordinal);
    Expect(!transparent || whiteList.Contains(className + " ", StringComparison.Ordinal) || whiteList.EndsWith(className, StringComparison.Ordinal),
        $"{className}: a transparent portrait gets the white underlay");
}
var frameReference = ReadPng(Path.Combine(portraitDir, "my_legend_begins_hbrgo_saki.png"));
foreach (string name in new[] { "summer_grass_dream_hbrgo_saki", "boost_up_hbrgo_saki" })
{
    var cutIn = ReadPng(Path.Combine(portraitDir, name + ".png"));
    Expect(cutIn.Width == 300 && cutIn.Height == 300, $"{name}: 300x300 like the other HBR memories");
    long alphaDiff = 0;
    bool outsideClear = true;
    for (int y = 0; y < 300; y++)
        for (int x = 0; x < 300; x++)
        {
            int a = cutIn.Rgba[(y * 300 + x) * 4 + 3];
            alphaDiff += Math.Abs(a - frameReference.Rgba[(y * 300 + x) * 4 + 3]);
            if ((x < 35 || x >= 276) && a != 0) outsideClear = false;
        }
    Expect(outsideClear, $"{name}: nothing is drawn outside the frame");
    Expect(cutIn.Rgba[(150 * 300 + 150) * 4 + 3] == 255, $"{name}: the window is opaque");
    Expect(alphaDiff / 90000.0 < 1.0, $"{name}: the frame matches the other cut-ins (mean alpha difference {alphaDiff / 90000.0:0.###})");
}
Console.WriteLine("PASS memory card portraits");

// ---- Texture sizes: Godot clamps Control.Size to the minimum size at the moment it is set (2026-11-07) ----
// Under the default KeepSize a TextureRect's minimum is its texture's size, and outside the tree a later ExpandMode
// change does not refresh the cached minimum. PhantomRosterArt.Image set Texture, Position and Size before
// ExpandMode, so every phantom roster medallion layer kept its source size: a 356px mask and a 396px frame for a
// 190px medallion (user: 「好大...」). Once a texture is set, ExpandMode must come before Position or Size.
List<string> PropertyNames(string body)
{
    var names = new List<string>();
    int depth = 0, start = 0;
    for (int i = 0; i <= body.Length; i++)
    {
        char ch = i < body.Length ? body[i] : ',';
        if (ch is '(' or '{' or '[') depth++;
        else if (ch is ')' or '}' or ']') depth--;
        else if (ch == ',' && depth == 0)
        {
            Match name = Regex.Match(body[start..i], @"^\s*(?://[^\n]*\n\s*)*([A-Z][A-Za-z]+)\s*=(?!=)");
            if (name.Success) names.Add(name.Groups[1].Value);
            start = i + 1;
        }
    }
    return names;
}
var textureRectInitializer = new Regex(@"(?:new\s+TextureRect\s*(?:\(\s*\))?|TextureRect\s+\w+\s*=\s*new\s*\(\s*\))\s*\{");
string? clamped = null;
int scannedRects = 0;
foreach (string file in Directory.EnumerateFiles(Path.Combine(repo, "Core"), "*.cs", SearchOption.AllDirectories))
{
    string source = File.ReadAllText(file);
    foreach (Match match in textureRectInitializer.Matches(source))
    {
        int end = match.Index + match.Length, depth = 1;
        while (depth > 0 && end < source.Length)
        {
            if (source[end] == '{') depth++;
            else if (source[end] == '}') depth--;
            end++;
        }
        List<string> names = PropertyNames(source[(match.Index + match.Length)..(end - 1)]);
        scannedRects++;
        int texture = names.IndexOf("Texture"), expand = names.IndexOf("ExpandMode");
        // The first Position or Size after the texture computes and caches the minimum size.
        int sizing = texture < 0 ? -1 : names.FindIndex(texture + 1, name => name is "Position" or "Size");
        if (sizing >= 0 && names.Contains("Size") && (expand < 0 || expand > sizing))
            clamped ??= $"{Path.GetRelativePath(repo, file)}:{source[..match.Index].Count(c => c == '\n') + 1}";
    }
}
Expect(scannedRects >= 40, $"the scan finds the mod's TextureRect initializers ({scannedRects})");
Expect(clamped == null, $"no textured TextureRect is sized before ExpandMode lets go of the texture size ({clamped})");
string rosterArt = File.ReadAllText(Path.Combine(screens, "PhantomRosterArt.cs"));
int imageAt = rosterArt.IndexOf("internal static TextureRect Image(", StringComparison.Ordinal);
string imageBody = imageAt < 0 ? "" : rosterArt[imageAt..rosterArt.IndexOf("\n    }", imageAt, StringComparison.Ordinal)];
int imageExpand = imageBody.IndexOf("ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize", StringComparison.Ordinal);
Expect(imageExpand >= 0 && imageExpand < imageBody.IndexOf("Texture = Load(path)", StringComparison.Ordinal) &&
       imageExpand < imageBody.IndexOf("Position = position", StringComparison.Ordinal) &&
       imageExpand < imageBody.IndexOf("Size = size", StringComparison.Ordinal),
    "the roster's Image helper lets go of the texture size before taking its position and size");
// Every roster page draws its rings, frames and portrait masks through that helper at the v2 preview's sizes
// (ui-concepts/phantom-roster-20261104): a 190px medallion with a 212px frame, a 240px active frame,
// a 234px selection cursor and a 70px level badge.
string medallionSource = File.ReadAllText(Path.Combine(screens, "NPhantomMedallion.cs"));
foreach ((string layer, string pattern, float preview) in new[]
{
    ("frame", @"PhantomRosterArt\.Frame, Centered\(diameter \* ([\d.]+)f\)", 212f),
    ("active frame", @"PhantomRosterArt\.FrameActive, Centered\(diameter \* ([\d.]+)f\)", 240f),
    ("selection cursor", @"PhantomRosterArt\.SelectCursor, Centered\(diameter \* ([\d.]+)f\)", 234f),
    ("level badge", @"float badge = diameter \* ([\d.]+)f;", 70f),
})
{
    Match ratio = Regex.Match(medallionSource, pattern);
    float size = ratio.Success ? 190 * float.Parse(ratio.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
    Expect(Math.Abs(size - preview) <= 1, $"a 190px medallion's {layer} is {preview}px as in the v2 preview ({size:0.#})");
}
Expect(medallionSource.Contains("TextureRect mask = PhantomRosterArt.Image(PhantomRosterArt.CircleMask, Vector2.Zero, Vector2.One * diameter);", StringComparison.Ordinal) &&
       medallionSource.Contains("Control portrait = CreatePortraitCircle(PhantomRosterArt.GetPortrait(entry.MonsterId), diameter, featured);", StringComparison.Ordinal),
    "the portrait and its circular mask are exactly the medallion's diameter");
Console.WriteLine("PASS texture sizes");

// ---- Phantom portrait backdrop: the roster's circles show the monster on dark violet (2026-11-07) ----
// The user asked for the v2 preview's dark backdrop (「改深色底」). The renderer's capture carries alpha; the card art
// composites it on white (Compose, unchanged), and the roster takes ComposeCutout of the same capture: the same framing
// with straight alpha, laid over a radial gradient. Only the renderer's alpha decides what is background, so white
// fur and highlights stay. Inputs: synthetic captures plus the mod's own monster art and static portrait sources.
static byte[] LegacyCompose(byte[] rgba, int width, int height)
{
    // PhantomPortraitCompositor.Compose as committed before the cutout (HEAD 2abac5a), verbatim.
    const int Width = 512, Height = 384;
    if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
        throw new ArgumentException("Invalid RGBA image dimensions.");
    byte[] output = new byte[Width * Height * 4];
    Array.Fill(output, (byte)255);
    int left = width, top = height, right = -1, bottom = -1;
    for (int y = 0; y < height; y++)
    for (int x = 0; x < width; x++)
    {
        if (rgba[(y * width + x) * 4 + 3] <= 16) continue;
        left = Math.Min(left, x);
        top = Math.Min(top, y);
        right = Math.Max(right, x);
        bottom = Math.Max(bottom, y);
    }
    if (right < left) return output;

    double scale = Math.Min(Width * 0.88 / (right - left + 1),
        Height * 0.88 / (bottom - top + 1));
    double offsetX = (Width - (right - left + 1) * scale) / 2;
    double offsetY = (Height - (bottom - top + 1) * scale) / 2;
    for (int y = 0; y < Height; y++)
    for (int x = 0; x < Width; x++)
    {
        double sx = left + (x + 0.5 - offsetX) / scale - 0.5;
        double sy = top + (y + 0.5 - offsetY) / scale - 0.5;
        int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
        double fx = sx - x0, fy = sy - y0;
        int index = (y * Width + x) * 4;
        for (int channel = 0; channel < 3; channel++)
        {
            double value = Sample(x0, y0, channel) * (1 - fx) * (1 - fy) +
                Sample(x0 + 1, y0, channel) * fx * (1 - fy) +
                Sample(x0, y0 + 1, channel) * (1 - fx) * fy +
                Sample(x0 + 1, y0 + 1, channel) * fx * fy;
            output[index + channel] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }
    }
    return output;

    double Sample(int x, int y, int channel)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return 255;
        int index = (y * width + x) * 4;
        // Composite before interpolation so invisible RGB cannot bleed into the edges.
        return 255 + (rgba[index + channel] - 255) * rgba[index + 3] / 255d;
    }
}

// The four bilinear taps behind a portrait pixel, with the committed framing (the same arithmetic as above).
static Func<int, int, (int X0, int Y0, double Fx, double Fy)>? LegacyTaps(byte[] rgba, int width, int height)
{
    const int Width = 512, Height = 384;
    int left = width, top = height, right = -1, bottom = -1;
    for (int y = 0; y < height; y++)
    for (int x = 0; x < width; x++)
    {
        if (rgba[(y * width + x) * 4 + 3] <= 16) continue;
        left = Math.Min(left, x);
        top = Math.Min(top, y);
        right = Math.Max(right, x);
        bottom = Math.Max(bottom, y);
    }
    if (right < left) return null;
    double scale = Math.Min(Width * 0.88 / (right - left + 1), Height * 0.88 / (bottom - top + 1));
    double offsetX = (Width - (right - left + 1) * scale) / 2;
    double offsetY = (Height - (bottom - top + 1) * scale) / 2;
    return (x, y) =>
    {
        double sx = left + (x + 0.5 - offsetX) / scale - 0.5;
        double sy = top + (y + 0.5 - offsetY) / scale - 0.5;
        int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
        return (x0, y0, sx - x0, sy - y0);
    };
}

const int PortraitWidth = PhantomPortraitCompositor.Width, PortraitHeight = PhantomPortraitCompositor.Height;
var portraitInputs = new List<(string Name, int Width, int Height, byte[] Rgba, bool HasWhitePatch)>();
// Synthetic captures: an antialiased body with a soft fringe, an opaque white patch (fur, a highlight), a translucent
// fin, and garbage RGB under alpha 0 that must never reach the portrait. Sizes cover the renderer's 1024×768 capture,
// small sprites, shapes touching the border, very wide and very tall bodies, a two-pixel sprite and an empty capture.
(int, int, byte[]) Synthetic(int width, int height, int seed, double cx, double cy, double rx, double ry, bool patch)
{
    var random = new Random(seed);
    byte[] rgba = new byte[width * height * 4];
    for (int y = 0; y < height; y++)
    for (int x = 0; x < width; x++)
    {
        int i = (y * width + x) * 4;
        double d = Math.Sqrt(Math.Pow((x + 0.5 - cx) / rx, 2) + Math.Pow((y + 0.5 - cy) / ry, 2));
        double coverage = Math.Clamp((1 - d) * Math.Min(rx, ry) / 2.5, 0, 1);
        double fin = Math.Clamp(1 - Math.Abs(x + 0.5 - cx - rx * 0.9) / (rx * 0.25 + 1), 0, 1) * (Math.Abs(y + 0.5 - cy) < ry * 0.3 ? 0.55 : 0);
        double alpha = Math.Max(coverage, fin);
        if (alpha <= 0)
        {
            rgba[i] = (byte)random.Next(256); rgba[i + 1] = (byte)random.Next(256); rgba[i + 2] = (byte)random.Next(256);
            continue;
        }
        bool white = patch && Math.Sqrt(Math.Pow((x + 0.5 - cx + rx * 0.2) / (rx * 0.35), 2) + Math.Pow((y + 0.5 - cy) / (ry * 0.35), 2)) < 1;
        rgba[i] = white ? (byte)255 : (byte)(40 + 150 * x / width + random.Next(20));
        rgba[i + 1] = white ? (byte)255 : (byte)(30 + 120 * y / height + random.Next(20));
        rgba[i + 2] = white ? (byte)255 : (byte)(90 + random.Next(60));
        rgba[i + 3] = white ? (byte)255 : (byte)Math.Round(alpha * 255);
    }
    return (width, height, rgba);
}
foreach ((string name, int w, int h, int seed, double cx, double cy, double rx, double ry, bool patch) in new[]
{
    ("capture 1024x768", 1024, 768, 1, 520.0, 400.0, 380.0, 300.0, true),
    ("small sprite 37x53", 37, 53, 2, 18.0, 27.0, 15.0, 22.0, true),
    ("body touching the border", 300, 200, 3, 40.0, 100.0, 120.0, 140.0, true),
    ("very wide body", 2000, 60, 4, 1000.0, 30.0, 990.0, 26.0, false),
    ("very tall body", 50, 1500, 5, 25.0, 750.0, 22.0, 740.0, false),
    ("two-pixel sprite", 9, 7, 6, 4.5, 3.5, 0.6, 0.6, false),
})
{
    (int width, int height, byte[] rgba) = Synthetic(w, h, seed, cx, cy, rx, ry, patch);
    portraitInputs.Add((name, width, height, rgba, patch));
}
portraitInputs.Add(("empty capture", 64, 48, Enumerable.Range(0, 64 * 48 * 4).Select(i => (byte)(i % 4 == 3 ? i % 17 : 255)).ToArray(), false));
// The renderer's static sources and every monster cutout the mod ships (the readable RGBA ones).
var portraitFiles = new List<string>
{
    "images/packed/card_portraits/phantoms/rocket.png", "images/packed/card_portraits/phantoms/crusher.png",
    "images/map/boss_nodes/skull_feather.png", "images/map/boss_nodes/death_slug.png",
    "images/monsters/hbrgo_act4/flat_hand_phase12.png",
};
portraitFiles.AddRange(Directory.EnumerateFiles(Path.Combine(repo, "images", "monsters"), "*_cutout.png", SearchOption.AllDirectories)
    .Select(file => Path.GetRelativePath(repo, file).Replace('\\', '/')).Order(StringComparer.Ordinal));
foreach (string file in portraitFiles)
{
    byte[] head = File.ReadAllBytes(Path.Combine(repo, file))[..29];
    if (head[24] != 8 || head[25] != 6 || head[28] != 0) continue; // the reader takes 8-bit RGBA only
    (int width, int height, byte[] rgba) = ReadPng(Path.Combine(repo, file));
    portraitInputs.Add((file, width, height, rgba, false));
}
Expect(portraitInputs.Count(input => input.Name.StartsWith("images/", StringComparison.Ordinal)) >= 40,
    $"the mod's monster art is part of the portrait inputs ({portraitInputs.Count})");

double worstOverWhite = 0, drawnError = 0, drawnErrorBlack = 0;
long portraitPixels = 0, exactOverWhite = 0, opaqueTaps = 0, opaqueExact = 0, opaqueWhite = 0, drawnPixels = 0, drawnVisible = 0, drawnVisibleBlack = 0;
foreach ((string name, int width, int height, byte[] rgba, bool hasWhitePatch) in portraitInputs)
{
    long whiteBefore = opaqueWhite;
    byte[] card = PhantomPortraitCompositor.Compose(rgba, width, height);
    Expect(card.AsSpan().SequenceEqual(LegacyCompose(rgba, width, height)),
        $"{name}: the phantom card art is byte for byte what it was before the cutout");
    byte[] cutout = PhantomPortraitCompositor.ComposeCutout(rgba, width, height);
    Expect(cutout.Length == PortraitWidth * PortraitHeight * 4, $"{name}: the cutout is a {PortraitWidth}x{PortraitHeight} RGBA portrait");

    // Garbage under alpha 0 changes neither picture.
    byte[] scrambled = (byte[])rgba.Clone();
    for (int i = 0; i < scrambled.Length; i += 4)
        if (scrambled[i + 3] == 0) { scrambled[i] = (byte)(i * 7); scrambled[i + 1] = (byte)~(i >> 3); scrambled[i + 2] = (byte)(i >> 11); }
    Expect(PhantomPortraitCompositor.ComposeCutout(scrambled, width, height).AsSpan().SequenceEqual(cutout) &&
           PhantomPortraitCompositor.Compose(scrambled, width, height).AsSpan().SequenceEqual(card),
        $"{name}: the colour of invisible source pixels never reaches either portrait");

    var taps = LegacyTaps(rgba, width, height);
    if (taps == null)
    {
        Expect(cutout.All(value => value == 0) && card.All(value => value == 255),
            $"{name}: a capture with nothing visible gives a transparent cutout and the white card art");
        continue;
    }
    double SourceAlpha(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? 0 : rgba[(y * width + x) * 4 + 3];
    for (int y = 0; y < PortraitHeight; y++)
    for (int x = 0; x < PortraitWidth; x++)
    {
        int index = (y * PortraitWidth + x) * 4;
        portraitPixels++;
        (int x0, int y0, double fx, double fy) = taps(x, y);
        double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
        double alpha = w00 * SourceAlpha(x0, y0) + w10 * SourceAlpha(x0 + 1, y0) + w01 * SourceAlpha(x0, y0 + 1) + w11 * SourceAlpha(x0 + 1, y0 + 1);
        if (cutout[index + 3] != (byte)Math.Clamp((int)Math.Round(alpha), 0, 255))
            Expect(false, $"{name} ({x},{y}): the cutout's alpha is the bilinear alpha of the capture under the card's framing");
        // Laid over white, the cutout is the card art: same framing, same colours, only the backdrop differs.
        bool exact = true;
        for (int channel = 0; channel < 3; channel++)
        {
            double overWhite = cutout[index + channel] * cutout[index + 3] / 255d + 255 - cutout[index + 3];
            double error = Math.Abs(overWhite - card[index + channel]);
            worstOverWhite = Math.Max(worstOverWhite, error);
            exact &= error <= 0.5;
        }
        if (exact) exactOverWhite++;
        // Wherever all four taps are opaque the cutout is opaque with the card's own colour, white included (a level
        // apart at most, where the two sums land either side of a rounding tie).
        bool opaque = SourceAlpha(x0, y0) == 255 && SourceAlpha(x0 + 1, y0) == 255 && SourceAlpha(x0, y0 + 1) == 255 && SourceAlpha(x0 + 1, y0 + 1) == 255;
        if (opaque)
        {
            opaqueTaps++;
            if (cutout[index + 3] != 255 || Math.Abs(cutout[index] - card[index]) > 1 || Math.Abs(cutout[index + 1] - card[index + 1]) > 1 ||
                Math.Abs(cutout[index + 2] - card[index + 2]) > 1)
                Expect(false, $"{name} ({x},{y}): an opaque part of the monster stays opaque with the card art's colour");
            if (cutout[index] == card[index] && cutout[index + 1] == card[index + 1] && cutout[index + 2] == card[index + 2]) opaqueExact++;
            if (cutout[index] >= 250 && cutout[index + 1] >= 250 && cutout[index + 2] >= 250) opaqueWhite++;
        }
    }
    if (hasWhitePatch)
        Expect(opaqueWhite > whiteBefore, $"{name}: the opaque white patch survives in the cutout");
    // Godot draws the cutout with straight alpha: bilinear, no mipmaps, KeepAspectCovered into a 190px circle (texture
    // x 64..448), mixed over the backdrop. Against the ideal premultiplied result the backdrop cancels out, leaving
    // (filtered colour) x (filtered alpha) - (filtered colour x alpha). Empty texels carry their covered neighbours'
    // colour; drawing must come out at least as close to the ideal as with those texels left black.
    foreach (bool blackEmpties in new[] { false, true })
    {
        const int Circle = 190;
        for (int j = 0; j < Circle; j++)
        for (int i = 0; i < Circle; i++)
        {
            double tx = 64 + (i + 0.5) * 384 / Circle - 0.5, ty = (j + 0.5) * 384 / Circle - 0.5;
            int x0 = (int)Math.Floor(tx), y0 = (int)Math.Floor(ty);
            double fx = tx - x0, fy = ty - y0, alpha = 0, worst = 0;
            double[] straight = new double[3], premultiplied = new double[3];
            foreach ((int dx, int dy, double weight) in new[] { (0, 0, (1 - fx) * (1 - fy)), (1, 0, fx * (1 - fy)), (0, 1, (1 - fx) * fy), (1, 1, fx * fy) })
            {
                int k = (Math.Clamp(y0 + dy, 0, PortraitHeight - 1) * PortraitWidth + Math.Clamp(x0 + dx, 0, PortraitWidth - 1)) * 4;
                alpha += weight * cutout[k + 3];
                for (int channel = 0; channel < 3; channel++)
                {
                    double colour = blackEmpties && cutout[k + 3] == 0 ? 0 : cutout[k + channel];
                    straight[channel] += weight * colour;
                    premultiplied[channel] += weight * colour * cutout[k + 3] / 255;
                }
            }
            for (int channel = 0; channel < 3; channel++)
                worst = Math.Max(worst, Math.Abs(straight[channel] * alpha / 255 - premultiplied[channel]));
            if (blackEmpties) { drawnErrorBlack += worst; if (worst > 8) drawnVisibleBlack++; }
            else { drawnError += worst; drawnPixels++; if (worst > 8) drawnVisible++; }
        }
    }
}
Console.WriteLine($"  portraits: {portraitInputs.Count} inputs, {portraitPixels} pixels; over white {100.0 * exactOverWhite / portraitPixels:0.###}% within 0.5 of the card art, worst {worstOverWhite:0.###}");
Console.WriteLine($"  opaque taps {opaqueTaps} ({100.0 * opaqueExact / Math.Max(1, opaqueTaps):0.###}% the card's exact colour, white {opaqueWhite})");
Console.WriteLine($"  drawn in a 190px circle: mean error {drawnError / drawnPixels:0.###} levels, {drawnVisible} pixels off by more than 8 " +
                  $"(empty texels left black: {drawnErrorBlack / drawnPixels:0.###}, {drawnVisibleBlack})");
Expect(worstOverWhite <= 1.5 + 1e-9, $"laid over white the cutout is the card art to within rounding ({worstOverWhite:0.###})");
Expect(opaqueWhite > 1000, $"opaque white parts of the monsters stay white and opaque ({opaqueWhite})");
Expect(drawnError / drawnPixels <= 0.5 && drawnError <= drawnErrorBlack && drawnVisible <= drawnVisibleBlack,
    $"drawn with straight alpha the cutout stays close to the ideal, closer than with black empty texels ({drawnError / drawnPixels:0.###}, {drawnVisible} against {drawnErrorBlack / drawnPixels:0.###}, {drawnVisibleBlack})");
bool Throws(Action action)
{
    try
    {
        action();
        return false;
    }
    catch (ArgumentException)
    {
        return true;
    }
}
Expect(Throws(() => PhantomPortraitCompositor.ComposeCutout(new byte[11], 2, 2)) && Throws(() => PhantomPortraitCompositor.ComposeCutout(new byte[0], 0, 0)) &&
       Throws(() => PhantomPortraitCompositor.Compose(new byte[11], 2, 2)),
    "the cutout rejects malformed captures like the card art");

// Which picture each screen gets.
string helpers = Path.Combine(repo, "Core", "Helpers");
string portraitHelper = File.ReadAllText(Path.Combine(helpers, "PhantomPortraitHelper.cs"));
Expect(portraitHelper.Contains("internal static Texture2D GetPortrait(ModelId monsterId) => Get(monsterId, PhantomPortraitStyle.Card);", StringComparison.Ordinal) &&
       portraitHelper.Contains("internal static Texture2D GetCutout(ModelId monsterId) => Get(monsterId, PhantomPortraitStyle.Cutout);", StringComparison.Ordinal) &&
       portraitHelper.Contains("Dictionary<(ModelId Monster, PhantomPortraitStyle Style), CacheEntry> Cache", StringComparison.Ordinal) &&
       portraitHelper.Contains("_renderer.Enqueue(monster, entry.Texture, style,", StringComparison.Ordinal),
    "card art and roster cutouts are cached and rendered separately");
Expect(portraitHelper.Contains("placeholder.Fill(style == PhantomPortraitStyle.Cutout ? new Color(0f, 0f, 0f, 0f) : Colors.White);", StringComparison.Ordinal),
    "until its render lands a card waits on white and a cutout on nothing, so the roster never flashes white");
string renderer = File.ReadAllText(Path.Combine(repo, "Core", "Nodes", "Cards", "NPhantomPortraitRenderer.cs"));
Expect(Regex.IsMatch(renderer, @"style == PhantomPortraitStyle\.Cutout\s*\?\s*PhantomPortraitCompositor\.ComposeCutout\(rgba, width, height\)\s*:\s*PhantomPortraitCompositor\.Compose\(rgba, width, height\)") &&
       Regex.Matches(renderer, @"await UpdateTexture\(texture, \w+, style\);").Count == 2 &&
       !Regex.IsMatch(renderer, @"UpdateTexture\(texture, \w+\);"),
    "the renderer composes each request in its own style, static sources included");
string portraitPatch = File.ReadAllText(Path.Combine(repo, "Core", "Patches", "PhantomPortraitPatch.cs"));
Expect(Regex.Matches(portraitPatch, @"PhantomPortraitHelper\.GetPortrait\(card\.MonsterId\)").Count == 2 && !portraitPatch.Contains("GetCutout", StringComparison.Ordinal),
    "phantom cards keep the white card art");
int rosterPortraitAt = rosterArt.IndexOf("internal static Texture2D GetPortrait(ModelId monsterId)", StringComparison.Ordinal);
string rosterPortrait = rosterPortraitAt < 0 ? "" : rosterArt[rosterPortraitAt..rosterArt.IndexOf("\n    }", rosterPortraitAt, StringComparison.Ordinal)];
Expect(rosterPortrait.Contains("return PhantomPortraitHelper.GetCutout(monsterId);", StringComparison.Ordinal) &&
       !rosterPortrait.Contains("PhantomPortraitHelper.GetPortrait(", StringComparison.Ordinal) &&
       rosterPortrait.Contains("monsterId == ModelDb.Monster<RedCrimsonBossMonster>().Id ||", StringComparison.Ordinal) &&
       rosterPortrait.Contains("monsterId == ModelDb.Monster<RedCrimson>().Id", StringComparison.Ordinal) &&
       rosterPortrait.Contains("? (RedCrimsonScene, new Rect2(900f, 10f, 760f, 760f))", StringComparison.Ordinal) &&
       rosterPortrait.Contains("monsterId == ModelDb.Monster<FlatHand>().Id", StringComparison.Ordinal) &&
       rosterPortrait.Contains("? (FlatHandScene, new Rect2(580f, 20f, 760f, 760f))", StringComparison.Ordinal),
    "the roster shows the cutout, except Red Crimson (both) and FlatHand, which keep their boss-scene crops");
int circleAt = medallionSource.IndexOf("internal static Control CreatePortraitCircle(Texture2D portrait, float diameter, bool featured = false)", StringComparison.Ordinal);
string circle = circleAt < 0 ? "" : medallionSource[circleAt..medallionSource.IndexOf("\n    }", circleAt, StringComparison.Ordinal)];
int backdropAt = circle.IndexOf("Texture = featured ? PhantomRosterArt.FeaturedPortraitBackdrop : PhantomRosterArt.PortraitBackdrop,", StringComparison.Ordinal);
int artAt = circle.IndexOf("Texture = portrait,", StringComparison.Ordinal);
int flameAt = circle.IndexOf("Texture = PhantomRosterArt.SoulFlame,", StringComparison.Ordinal);
Expect(backdropAt > 0 && artAt > backdropAt && flameAt > artAt && !circle.Contains("ColorRect", StringComparison.Ordinal),
    "inside the circle: the gradient backdrop, then the cutout, then the azure soul-flame");
string rosterScreen = File.ReadAllText(Path.Combine(screens, "NPhantomRosterScreen.Hbr.cs"));
int previewAt = rosterScreen.IndexOf("private void AddPreviewMedallion(PhantomRosterEntry entry)", StringComparison.Ordinal);
string previewBody = previewAt < 0 ? "" : rosterScreen[previewAt..rosterScreen.IndexOf("\n    }", previewAt, StringComparison.Ordinal)];
Expect(rosterScreen.Contains("NPhantomMedallion.CreatePortraitCircle(PhantomRosterArt.GetPortrait(current.Value.MonsterId), 330f, featured: true);", StringComparison.Ordinal) &&
       previewBody.Contains("featured: true", StringComparison.Ordinal) &&
       Regex.Matches(rosterScreen, "featured").Count == 2,
    "the detail portrait and the fusion preview are featured; the list and material grids are not");
// The gradient against the v2 preview's CSS (template.html lines 95, 141 and 188):
// radial-gradient(circle at 50% 40%, centre, #160e2c 70%), sized to the farthest corner.
Match gradient = Regex.Match(rosterArt, @"PortraitGradient\(Color centre\) => Gradient2D\(\s*GradientTexture2D\.FillEnum\.Radial,\s*new Vector2\(([\d.]+)f, ([\d.]+)f\),\s*new Vector2\(([\d.]+)f, ([\d.]+)f \+ ([\d.]+)f \* Mathf\.Sqrt\(([\d.]+)f\)\),\s*new\[\] \{ 0f, 1f \},\s*new\[\] \{ centre, new Color\(""#([0-9a-f]{6})""\) \}\);");
Match plainCentre = Regex.Match(rosterArt, @"PortraitBackdrop => Cached\(ref _portraitBackdrop, \(\) => PortraitGradient\(new Color\(""#([0-9a-f]{6})""\)\)\);");
Match featuredCentre = Regex.Match(rosterArt, @"FeaturedPortraitBackdrop => Cached\(ref _featuredPortraitBackdrop, \(\) => PortraitGradient\(new Color\(""#([0-9a-f]{6})""\)\)\);");
Expect(gradient.Success && plainCentre.Success && featuredCentre.Success, "the roster's portrait gradients are where the check expects them");
float F(Group group) => float.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture);
(float fromX, float fromY) = (F(gradient.Groups[1]), F(gradient.Groups[2]));
(float toX, float toY) = (F(gradient.Groups[3]), F(gradient.Groups[4]) + F(gradient.Groups[5]) * MathF.Sqrt(F(gradient.Groups[6])));
float[] Rgb(string hex) => new[] { 0, 2, 4 }.Select(at => Convert.ToInt32(hex.Substring(at, 2), 16) / 255f).ToArray();
foreach ((string label, string centre, string cssCentre) in new[]
{
    ("list and material medallions", plainCentre.Groups[1].Value, "3b2a62"),
    ("detail portrait and fusion preview", featuredCentre.Groups[1].Value, "46306f"),
})
{
    Expect(centre == cssCentre && gradient.Groups[7].Value == "160e2c", $"{label}: #{centre} to #{gradient.Groups[7].Value} as in the v2 preview");
    float[] inner = Rgb(centre), outer = Rgb(gradient.Groups[7].Value);
    double cssReach = 0.7 * Math.Sqrt(0.5 * 0.5 + 0.6 * 0.6), worst = 0;
    for (int ty = 0; ty < 256; ty++)
    for (int tx = 0; tx < 256; tx++)
    {
        // GradientTexture2D: offset from texel index / (size - 1), clamped, interpolated in sRGB, stored truncated.
        float px = tx / 255f, py = ty / 255f;
        float offset = Math.Clamp(MathF.Sqrt((px - fromX) * (px - fromX) + (py - fromY) * (py - fromY)) /
            MathF.Sqrt((toX - fromX) * (toX - fromX) + (toY - fromY) * (toY - fromY)), 0f, 1f);
        // CSS at the texel's centre on screen.
        double u = (tx + 0.5) / 256, v = (ty + 0.5) / 256;
        double t = Math.Clamp(Math.Sqrt((u - 0.5) * (u - 0.5) + (v - 0.4) * (v - 0.4)) / cssReach, 0, 1);
        for (int channel = 0; channel < 3; channel++)
        {
            byte godot = (byte)Math.Clamp((inner[channel] + (outer[channel] - inner[channel]) * offset) * 255.0, 0, 255);
            double css = (inner[channel] + (outer[channel] - inner[channel]) * t) * 255;
            worst = Math.Max(worst, Math.Abs(godot - css));
        }
    }
    Expect(worst <= 1.5, $"{label}: the gradient texture matches the preview's CSS to within a level ({worst:0.##})");
}
Console.WriteLine("PASS phantom portrait backdrop");
Console.WriteLine($"All {checks} checks passed.");

// The code points an OpenType font maps to a glyph (cmap format 4 or 12, Windows Unicode).
static HashSet<int> ReadCmap(byte[] font)
{
    ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
    uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at));
    int cmap = -1;
    for (int table = 0, count = U16(4); table < count; table++)
        if (Encoding.ASCII.GetString(font, 12 + table * 16, 4) == "cmap") cmap = (int)U32(12 + table * 16 + 8);
    if (cmap < 0) throw new InvalidDataException("no cmap table");
    int best = -1, bestFormat = 0;
    for (int record = 0, count = U16(cmap + 2); record < count; record++)
    {
        int at = cmap + 4 + record * 8, sub = cmap + (int)U32(at + 4), format = U16(sub);
        if (U16(at) == 3 && format == 12) { best = sub; bestFormat = 12; }
        else if (U16(at) == 3 && U16(at + 2) == 1 && format == 4 && bestFormat != 12) { best = sub; bestFormat = 4; }
    }
    HashSet<int> mapped = new();
    if (bestFormat == 12)
    {
        for (uint group = 0, groups = U32(best + 12); group < groups; group++)
        {
            int at = best + 16 + (int)group * 12;
            uint start = U32(at), end = U32(at + 4), glyph = U32(at + 8);
            for (uint code = start; code <= end; code++) if (glyph + (code - start) != 0) mapped.Add((int)code);
        }
        return mapped;
    }
    if (bestFormat != 4) throw new InvalidDataException("no Windows Unicode cmap");
    int segments = U16(best + 6) / 2, ends = best + 14, starts = ends + segments * 2 + 2;
    int deltas = starts + segments * 2, offsets = deltas + segments * 2;
    for (int segment = 0; segment < segments; segment++)
    {
        int start = U16(starts + segment * 2), end = U16(ends + segment * 2);
        short delta = (short)U16(deltas + segment * 2);
        int rangeOffset = U16(offsets + segment * 2);
        for (int code = start; code <= end && code != 0xFFFF; code++)
        {
            int glyph = rangeOffset == 0 ? (code + delta) & 0xFFFF
                : U16(offsets + segment * 2 + rangeOffset + (code - start) * 2) is var raw && raw != 0 ? (raw + delta) & 0xFFFF : 0;
            if (glyph != 0) mapped.Add(code);
        }
    }
    return mapped;
}
