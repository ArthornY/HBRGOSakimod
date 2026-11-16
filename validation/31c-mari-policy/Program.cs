using System.Text.Json;
using HBRGOSakiMod.Core.Equipment;

var passed = new List<string>();
void Check(bool value, string label) { if (!value) throw new Exception(label); passed.Add(label); }
void Equal(int value, int expected, string label) => Check(value == expected, $"{label}: {value} = {expected}");
string path = Path.GetFullPath(args.Length > 0 ? args[0] : "mari-policy-report.json");
bool ok = false;
string? error = null;
try
{
    HbrSatsukiShopPolicy none = default;
    HbrSatsukiShopPolicy active = new(true, false, false, false, false, false, false, false, 1);
    HbrSatsukiShopPolicy full = new(true, true, true, true, true, true, true, true, 3);
    Equal(none.Price(1001, true, true), 1001, "absent permanent Mari preserves original price");
    Equal(active.Price(1001, true, true), 1001, "base card has no unearned discount");
    Equal((active with { Discount10 = true }).Price(1000, false, false), 900, "Flash discounts ordinary stock");
    Equal((active with { Alchemy20 = true, MemberCount = 2 }).Price(1000, true, false), 1000, "two31C kinds cannot activate DiamondDust");
    Equal((active with { Alchemy20 = true, MemberCount = 3 }).Price(1000, true, false), 800, "three31C kinds activate alchemy20percent discount");
    Equal((active with { Alchemy20 = true, MemberCount = 6 }).Price(1000, false, false), 1000, "DiamondDust never discounts ordinary stock");
    Equal((active with { FirstBuyHalf = true }).Price(1000, false, true), 500, "HappyDay halves first ordinary purchase");
    Equal((active with { FirstBuyHalf = true }).Price(1000, true, true), 500, "the same first-purchase receipt also covers alchemy");
    Equal((active with { FirstBuyHalf = true }).Price(1000, true, false), 1000, "spent first-purchase receipt has no discount");
    Equal(full.Price(1000, true, true), 360, "three independent discounts multiply rather than add");
    Equal(full.Price(31, true, true), 11, "round only the final product, not each discount step");
    Equal(full.Price(31, true, false), 22, "round final two-discount product only once");
    Equal(full.Price(1000, false, true), 450, "stock and plus materials get Flash and HappyDay only");
    Equal(full.Price(1000, true, false), 720, "later alchemy retains Flash and DiamondDust");
    Equal(full.Price(1, true, true), 1, "discounted price retains minimum one gold");
    Equal(full.Price(int.MaxValue, true, true), 773094112, "large valid prices do not overflow");
    Check(!none.HasStarPromotion(0), "no Mari means no base star promotion");
    Check(active.HasStarPromotion(0) && active.HasStarPromotion(29), "30percent uses inclusive0 through29");
    Check(!active.HasStarPromotion(30) && !active.HasStarPromotion(99), "30percent excludes30 and99");
    for (int rarity = 1; rarity <= 6; rarity++)
        Equal(active.PromoteRarity(rarity, true), Math.Min(6, rarity + 1), "one extra star caps at6 from " + rarity);
    Equal(active.PromoteRarity(5, false), 5, "failed promotion keeps rolled rarity");
    Equal(none.PromoteRarity(5, true), 5, "disabled policy cannot promote an accessory");
    Equal(active.HighCharmChance(0), 30, "first-tier high-charm chance gains30percentage points");
    Equal(active.HighCharmChance(10), 40, "second-tier high-charm chance gains30percentage points");
    Equal(active.HighCharmChance(25), 55, "third-tier high-charm chance gains30percentage points");
    Equal(active.HighCharmChance(90), 100, "high-charm probability caps at100");
    Equal(none.HighCharmChance(25), 25, "disabled high-charm chance remains unchanged");
    foreach (var row in new[] { (0, 4), (1, 5), (2, 6), (3, 6), (8, 6) })
        Equal(HbrSatsukiShopPolicy.HighestAlchemyRarity(row.Item1), row.Item2, "first-alchemy ceiling for actIndex " + row.Item1);
    Equal(active.OpenedPlusCount(1, 3), 1, "base card does not invent saturation upgrade");
    Equal(full.OpenedPlusCount(0, 3), 1, "saturation opens one extra empty plus slot");
    Equal(full.OpenedPlusCount(2, 3), 3, "saturation advances to the real slot capacity");
    Equal(full.OpenedPlusCount(3, 3), 3, "saturation never exceeds capacity");
    Equal(full.OpenedPlusCount(0, 0), 0, "charms with no plus slots remain without plus slots");
    Equal(active.RefundGold(31, 100), 0, "base card cannot refund without AmplifierCannon");
    Equal(full.RefundGold(31, 50), 0, "refund requires strictly greater than50");
    Equal(full.RefundGold(31, 51), 15, "refund is half the actual paid price rounded down");
    Equal(full.RefundGold(1000, 100), 500, "maximum raw d100 refunds half");
    Equal(full.RefundGold(1, 99), 0, "one gold cannot refund a fractional gold");
    Equal(HbrSatsukiShopPolicy.ReforgePrice(31), 15, "reforge costs half the already-discounted paid price");
    Equal(HbrSatsukiShopPolicy.ReforgePrice(1), 1, "reforge price minimum is one gold");
    Check(active.CacheKey != full.CacheKey && full.CacheKey != (full with { MemberCount = 2 }).CacheKey,
        "UI cache key changes for upgrade and31C threshold changes without another deck scan");
    ok = true;
}
catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(ex); }
File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = passed.Count, passed, error,
    boundary = "Pure immutable pricing, probability endpoints and resulting rarity/slots/refund amounts; no RNG, native game, UI, transaction storage or packaging." }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {passed.Count} Mari policy checks");
return ok ? 0 : 1;
