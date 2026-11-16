using System.Text.Json;
using HBRGOSakiMod.Core.FateDice;

// Pure checks for 31O's fate dice (2026-11-09, design v3 section 4,
// outputs/deck-rethink-31o-20261108/magic-sword-fate-dice-design.md): the fifteen fragments and their
// texts, slots, cracks and the roll penalty, fitting and removing fragments, reward and merchant
// rolls, the saved record and old saves, and the zhs texts of the new swordheart and magic sword
// rules. The production rules that need the game run in hbr-effect-skill-damage (FateDiceChecks.cs).
// Usage: Checks <repo>
string repo = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../.."));
int checks = 0;

void Expect(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
}

HbrFateDiceState With(int bosses = 0, int cracks = 0, string[] equipped = null, string[] bag = null) =>
    HbrFateDiceState.Normalize(new HbrFateDiceState
    {
        Version = HbrFateDiceState.CurrentVersion, BossesDefeated = bosses, Cracks = cracks,
        Equipped = equipped ?? Array.Empty<string>(), Bag = bag ?? Array.Empty<string>()
    });

bool Same(string[] actual, params string[] expected) => actual.SequenceEqual(expected);

// ---------------------------------------------------------------- the fifteen fragments
var all = HbrFateDiceCatalog.All;
Expect(all.Count == 15, "fifteen fragments");
Expect(all.Select(f => f.Id).Distinct().Count() == 15 && all.Select(f => f.Name).Distinct().Count() == 15, "ids and names are unique");
var texts = new (string Name, HbrFateFragmentCategory Category, HbrFateFragmentRarity Rarity, string Text)[]
{
    ("宽刃", HbrFateFragmentCategory.Range, HbrFateFragmentRarity.Common, "大成功的原始出目下限降低2。"),
    ("绝境", HbrFateFragmentCategory.Range, HbrFateFragmentRarity.Uncommon, "生命低于一半时，大成功的原始出目下限降低5。"),
    ("托底", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Common, "原始出目低于20时按20算。"),
    ("先手", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Rare, "每回合第一次掷骰取优势骰：额外投1枚骰子，优先取大成功，否则取最高出目。"),
    ("再来", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Common, "每场战斗第一次原始出目不超过10时，自动重掷一次。"),
    ("七之刻印", HbrFateFragmentCategory.SpecialNumber, HbrFateFragmentRarity.Rare, "原始出目里带数字7时也获得1层剑心；原始出目是7的倍数时改为获得2层。"),
    ("镜面", HbrFateFragmentCategory.SpecialNumber, HbrFateFragmentRarity.Rare, "原始出目是11、22……99时，这张牌的伤害和格挡翻倍。"),
    ("整数", HbrFateFragmentCategory.SpecialNumber, HbrFateFragmentRarity.Common, "原始出目是10的倍数时，抽1张牌。"),
    ("狂骰", HbrFateFragmentCategory.Gamble, HbrFateFragmentRarity.Uncommon, "大成功的原始出目下限降低5；原始出目不超过10时，自己受到7点伤害。"),
    ("孤注", HbrFateFragmentCategory.Gamble, HbrFateFragmentRarity.Rare, "出目修正+15%，但不能取优势骰。"),
    ("幸运数字", HbrFateFragmentCategory.Style, HbrFateFragmentRarity.Uncommon, "双剑形态下，原始出目为7或77时也算大成功，不受出目修正影响。"),
    ("鹰眼", HbrFateFragmentCategory.Style, HbrFateFragmentRarity.Uncommon, "弓形态下大成功时，给予所有敌人2层脆弱。"),
    ("剑鸣", HbrFateFragmentCategory.Style, HbrFateFragmentRarity.Uncommon, "大成功时多获得2层剑心。"),
    ("加速", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Uncommon, "出目修正+20%。"),
    ("主人公特典", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Rare, "出目修正-10%，并额外投1枚骰子；优先取大成功，否则取最高出目。"),
};
for (int i = 0; i < texts.Length; i++)
{
    HbrFateFragment fragment = all[i];
    Expect(fragment.Name == texts[i].Name && fragment.Category == texts[i].Category && fragment.Rarity == texts[i].Rarity &&
           fragment.Description == texts[i].Text, $"fragment {i}: {fragment.Name} reads \"{fragment.Description}\"");
    Expect(HbrFateDiceCatalog.Find(fragment.Id) == fragment && HbrFateDiceCatalog.Exists(fragment.Id), $"{fragment.Name} is found by id");
}
Expect(HbrFateDiceCatalog.Find("NOPE") == null && HbrFateDiceCatalog.Find(null) == null, "unknown ids are not fragments");
Expect(all.Count(f => f.Rarity == HbrFateFragmentRarity.Common) == 4 && all.Count(f => f.Rarity == HbrFateFragmentRarity.Uncommon) == 6 &&
       all.Count(f => f.Rarity == HbrFateFragmentRarity.Rare) == 5, "4 common, 6 uncommon, 5 rare");
Expect(HbrFateDiceCatalog.Describe(HbrFateDiceCatalog.Find(HbrFateDiceCatalog.WildDie), true).Contains("受到700点伤害") &&
       HbrFateDiceCatalog.Describe(HbrFateDiceCatalog.Find(HbrFateDiceCatalog.WildDie), false).Contains("受到7点伤害"),
    "狂骰 shows its fixed self-damage ×100 under HBR numerics");
Expect(all.Where(f => f.Id != HbrFateDiceCatalog.WildDie).All(f => HbrFateDiceCatalog.Describe(f, true) == f.Description),
    "the other fragments read the same in both modes");
Expect(HbrFateDiceCatalog.Price(HbrFateFragmentRarity.Common) == 80 && HbrFateDiceCatalog.Price(HbrFateFragmentRarity.Uncommon) == 120 &&
       HbrFateDiceCatalog.Price(HbrFateFragmentRarity.Rare) == 160, "merchant prices 80/120/160");
Expect(Enum.GetValues<HbrFateFragmentCategory>().Select(HbrFateDiceCatalog.CategoryName).SequenceEqual(new[] { "区间", "稳定", "特殊点数", "赌一把", "流派" }) &&
       Enum.GetValues<HbrFateFragmentCategory>().Select(HbrFateDiceCatalog.CategoryColor)
           .All(hex => hex.Length == 6 && hex.All(Uri.IsHexDigit)), "category names and rim colours");

// ---------------------------------------------------------------- slots and cracks
HbrFateDiceState empty = HbrFateDiceState.Empty;
Expect(empty.Version == 0 && empty.Slots == 2 && empty.Capacity == 2 && empty.Cracks == 0 && empty.RawPenalty == 0 &&
       empty.Swordheart == 0, "a new run: two empty slots, no crack, no kept swordheart");
HbrFateDiceState boss = empty;
for (int i = 1; i <= 5; i++)
{
    boss = boss.WithBossVictory();
    Expect(boss.Slots == Math.Min(5, 2 + i), $"{i} bosses beaten: {Math.Min(5, 2 + i)} slots");
}
HbrFateDiceState two = With(equipped: new[] { HbrFateDiceCatalog.WideEdge, HbrFateDiceCatalog.Floor });
HbrFateDiceState cracked = two.WithCrack();
Expect(cracked.Cracks == 1 && cracked.CrackedSlots == 1 && cracked.Capacity == 1 && Same(cracked.Equipped, HbrFateDiceCatalog.WideEdge) &&
       Same(cracked.Bag, HbrFateDiceCatalog.Floor) && cracked.RawPenalty == 0,
    "a crack takes a slot and pushes the last fitted fragment back to the bag");
HbrFateDiceState one = With(equipped: new[] { HbrFateDiceCatalog.WideEdge });
Expect(Same(one.WithCrack().Equipped, HbrFateDiceCatalog.WideEdge) && one.WithCrack().Bag.Length == 0, "a crack takes an empty slot first");
HbrFateDiceState full = two.WithCrack().WithCrack();
Expect(full.Capacity == 0 && full.Equipped.Length == 0 && full.Bag.Length == 2 && full.RawPenalty == 0, "two cracks fill two slots");
Expect(full.WithCrack().RawPenalty == 3 && full.WithCrack().WithCrack().RawPenalty == 6 && full.WithCrack().OverflowCracks == 1,
    "each crack beyond the slots lowers raw rolls by 3");
Expect(With(bosses: 3, cracks: 6).RawPenalty == 3 && With(bosses: 3, cracks: 5).RawPenalty == 0, "five slots take five cracks before the penalty");
// A new slot from a boss is taken by a crack that had nowhere to go.
HbrFateDiceState overflow = With(cracks: 3);
Expect(overflow.RawPenalty == 3 && overflow.WithBossVictory().RawPenalty == 0 && overflow.WithBossVictory().Capacity == 0,
    "a boss's new slot takes the crack that was lowering rolls");
HbrFateDiceState repaired = full.WithCrack().Repaired();
Expect(repaired.Cracks == 2 && repaired.RawPenalty == 0 && repaired.Equipped.Length == 0 && repaired.Bag.Length == 2,
    "修骰子 removes one crack; fragments stay in the bag");
Expect(ReferenceEquals(empty.Repaired(), empty), "nothing to repair without a crack");
Expect(cracked.Repaired().Capacity == 2 && cracked.Repaired().Equipped.Length == 1, "a repaired slot is free again");

// ---------------------------------------------------------------- fitting fragments
HbrFateDiceState got = empty.WithFragment(HbrFateDiceCatalog.Mirror).WithFragment(HbrFateDiceCatalog.Reroll).WithFragment(HbrFateDiceCatalog.Floor);
Expect(Same(got.Equipped, HbrFateDiceCatalog.Mirror, HbrFateDiceCatalog.Reroll) && Same(got.Bag, HbrFateDiceCatalog.Floor),
    "new fragments fill free slots, then wait in the bag");
Expect(ReferenceEquals(got.WithFragment(HbrFateDiceCatalog.Floor), got) && ReferenceEquals(got.WithFragment("NOPE"), got),
    "an owned or unknown fragment is not added again");
Expect(ReferenceEquals(got.Equip(HbrFateDiceCatalog.Floor), got), "no fitting into a full set");
HbrFateDiceState swapped = got.Unequip(HbrFateDiceCatalog.Reroll).Equip(HbrFateDiceCatalog.Floor);
Expect(Same(swapped.Equipped, HbrFateDiceCatalog.Mirror, HbrFateDiceCatalog.Floor) && Same(swapped.Bag, HbrFateDiceCatalog.Reroll),
    "take one off, fit another");
Expect(ReferenceEquals(got.Unequip(HbrFateDiceCatalog.Floor), got) && ReferenceEquals(got.Equip(HbrFateDiceCatalog.Mirror), got),
    "only fitted fragments come off, only bagged ones go on");
Expect(swapped.Revision == got.Revision + 2 && got.Revision == 3, "every change raises the revision");

// ---------------------------------------------------------------- kept swordheart
Expect(empty.WithSwordheart(12, 7).Swordheart == 7 && empty.WithSwordheart(5, 7).Swordheart == 5 && empty.WithSwordheart(-3, 7).Swordheart == 0,
    "kept swordheart is cut to the kept maximum");
Expect(ReferenceEquals(empty.WithSwordheart(0, 7), empty) && empty.WithSwordheart(9, 9).Swordheart == 9,
    "an unchanged amount keeps the record; a lasting raise (剑心锻造器) keeps more");

// ---------------------------------------------------------------- rewards and the merchant
HbrFateDiceState rolled = empty.WithReceipt("fate:elite-1");
Expect(rolled.HasReceipt("fate:elite-1") && ReferenceEquals(rolled.WithReceipt("fate:elite-1"), rolled), "a rolled set is recorded once");
HbrFateDiceState claimed = rolled.Claim("fate:elite-1", HbrFateDiceCatalog.SwordSong);
Expect(Same(claimed.Equipped, HbrFateDiceCatalog.SwordSong) && claimed.HasClaimed("fate:elite-1"), "a claimed fragment goes into a free slot");
Expect(ReferenceEquals(claimed.Claim("fate:elite-1", HbrFateDiceCatalog.Floor), claimed) &&
       ReferenceEquals(empty.Claim("fate:never", HbrFateDiceCatalog.Floor), empty) &&
       ReferenceEquals(rolled.Claim("fate:elite-1", "NOPE"), rolled), "one claim per rolled set, of a real fragment");
HbrFateDiceState many = empty;
for (int i = 0; i < 70; i++) many = many.WithReceipt("r" + i);
Expect(many.Receipts.Length == 64 && many.HasReceipt("r69") && !many.HasReceipt("r5"), "receipts keep the latest 64");

var commands = new Func<HbrFateDiceState, HbrFateDiceCommand, bool, int, string, IReadOnlyList<HbrFateShopOffer>, HbrFateDiceOutcome>(
    HbrFateDiceCommands.Evaluate);
HbrFateDiceState bagged = With(bag: new[] { HbrFateDiceCatalog.Floor });
Expect(!commands(bagged, new HbrFateDiceCommand { Op = HbrFateDiceOp.Equip, Fragment = HbrFateDiceCatalog.Floor }, false, 0, null, null).Succeeded &&
       commands(bagged, new HbrFateDiceCommand { Op = HbrFateDiceOp.Equip, Fragment = HbrFateDiceCatalog.Floor }, true, 0, null, null).Succeeded,
    "fitting is locked during combat and open outside it");
Expect(commands(rolled, new HbrFateDiceCommand { Op = HbrFateDiceOp.Claim, Receipt = "fate:elite-1", Fragment = HbrFateDiceCatalog.Floor },
    false, 0, null, null).Succeeded, "a reward is claimed whenever its screen is open");

ulong seed = 0x1234_5678_9ABC_DEF0UL;
HbrFateShopOffer[] stock = HbrFateDiceOffers.Shop(empty, "shop-a", HbrFateDiceRandom.ForKey(seed, "shop:shop-a"));
Expect(stock.Length is 1 or 2 && stock.All(o => !o.Sold && o.Price == HbrFateDiceCatalog.Price(HbrFateDiceCatalog.Find(o.Fragment).Rarity)) &&
       stock.Select(o => o.Fragment).Distinct().Count() == stock.Length, "a merchant sells one or two fragments at their rarity's price");
HbrFateShopOffer offer = stock[0];
var buy = new HbrFateDiceCommand { Op = HbrFateDiceOp.Buy, Fragment = offer.Fragment, ShopKey = "shop-a" };
Expect(!commands(empty, buy, true, offer.Price - 1, "shop-a", stock).Succeeded, "not enough gold");
Expect(!commands(empty, buy, true, 999, "shop-b", stock).Succeeded && !commands(empty, buy, true, 999, null, stock).Succeeded,
    "only the merchant the player stands in sells");
Expect(!commands(empty, buy with { Fragment = all.First(f => stock.All(o => o.Fragment != f.Id)).Id }, true, 999, "shop-a", stock).Succeeded,
    "only what the merchant has");
HbrFateDiceOutcome bought = commands(empty, buy, false, 999, "shop-a", stock);
Expect(bought.Succeeded && bought.GoldCost == offer.Price && bought.State.Owns(offer.Fragment) &&
       bought.State.HasReceipt(HbrFateDiceState.ShopReceipt("shop-a", offer.Fragment)), "buying costs its price and keeps a receipt");
HbrFateShopOffer[] after = HbrFateDiceOffers.Shop(bought.State, "shop-a", HbrFateDiceRandom.ForKey(seed, "shop:shop-a"));
Expect(after.Length == stock.Length && after.Single(o => o.Fragment == offer.Fragment).Sold &&
       !commands(bought.State, buy, true, 999, "shop-a", after).Succeeded, "the bought fragment stays listed as sold and cannot be bought again");
int ones = 0, twos = 0;
for (int i = 0; i < 200; i++)
{
    HbrFateShopOffer[] s = HbrFateDiceOffers.Shop(bought.State, "room-" + i, HbrFateDiceRandom.ForKey(seed, "shop:room-" + i));
    Expect(s.All(o => !o.Sold && o.Fragment != offer.Fragment), $"room-{i}: a fragment owned before the visit is not sold again");
    if (s.Length == 1) ones++;
    if (s.Length == 2) twos++;
}
Expect(ones > 30 && twos > 30, $"merchants carry one or two fragments ({ones} with one, {twos} with two)");
Expect(HbrFateDiceOffers.Shop(empty, null, HbrFateDiceRandom.ForKey(seed, "x")).Length == 0, "no merchant, no stock");

// Elite: three common or uncommon picks; boss: three rare picks; never an owned fragment.
for (int i = 0; i < 100; i++)
{
    string[] elite = HbrFateDiceOffers.Roll(empty, HbrFateOfferKind.Elite, HbrFateDiceRandom.ForKey(seed, "fate:e" + i));
    Expect(elite.Length == 3 && elite.Distinct().Count() == 3 &&
           elite.All(id => HbrFateDiceCatalog.Find(id).Rarity != HbrFateFragmentRarity.Rare), $"elite {i}: three common or uncommon fragments");
    string[] bossPicks = HbrFateDiceOffers.Roll(empty, HbrFateOfferKind.Boss, HbrFateDiceRandom.ForKey(seed, "fate:b" + i));
    Expect(bossPicks.Length == 3 && bossPicks.Distinct().Count() == 3 &&
           bossPicks.All(id => HbrFateDiceCatalog.Find(id).Rarity == HbrFateFragmentRarity.Rare), $"boss {i}: three rare fragments");
}
Expect(HbrFateDiceOffers.Roll(empty, HbrFateOfferKind.Elite, HbrFateDiceRandom.ForKey(seed, "fate:x"))
        .SequenceEqual(HbrFateDiceOffers.Roll(empty, HbrFateOfferKind.Elite, HbrFateDiceRandom.ForKey(seed, "fate:x"))),
    "the same room rolls the same set on every peer and after a reload");
string[] rares = all.Where(f => f.Rarity == HbrFateFragmentRarity.Rare).Select(f => f.Id).ToArray();
HbrFateDiceState threeRares = With(bosses: 3, equipped: rares.Take(3).ToArray());
string[] topped = HbrFateDiceOffers.Roll(threeRares, HbrFateOfferKind.Boss, HbrFateDiceRandom.ForKey(seed, "fate:t"));
Expect(topped.Length == 3 && topped.Count(id => HbrFateDiceCatalog.Find(id).Rarity == HbrFateFragmentRarity.Rare) == 2 &&
       topped.Count(id => HbrFateDiceCatalog.Find(id).Rarity == HbrFateFragmentRarity.Uncommon) == 1 && topped.All(id => !threeRares.Owns(id)),
    "with three rares owned a boss offers the other two plus an uncommon");
string[] nonRares = all.Where(f => f.Rarity != HbrFateFragmentRarity.Rare).Select(f => f.Id).ToArray();
HbrFateDiceState mostOwned = With(bosses: 3, equipped: nonRares.Take(5).ToArray(), bag: nonRares.Skip(5).Take(4).ToArray());
string[] eliteTopped = HbrFateDiceOffers.Roll(mostOwned, HbrFateOfferKind.Elite, HbrFateDiceRandom.ForKey(seed, "fate:m"));
Expect(eliteTopped.Length == 3 && eliteTopped.Count(id => HbrFateDiceCatalog.Find(id).Rarity != HbrFateFragmentRarity.Rare) == 1 &&
       eliteTopped.All(id => !mostOwned.Owns(id)), "with one common or uncommon left an elite tops up with rares");
HbrFateDiceState everything = With(bosses: 3, equipped: all.Take(5).Select(f => f.Id).ToArray(), bag: all.Skip(5).Select(f => f.Id).ToArray());
Expect(everything.Equipped.Length == 5 && everything.Bag.Length == 10 &&
       HbrFateDiceOffers.Roll(everything, HbrFateOfferKind.Boss, HbrFateDiceRandom.ForKey(seed, "fate:all")).Length == 0,
    "with every fragment owned no fragment reward is offered");
HbrFateDiceRandom random = HbrFateDiceRandom.ForKey(seed, "r");
Expect(Enumerable.Range(0, 500).Select(_ => random.NextInt(7)).All(n => n is >= 0 and < 7) &&
       HbrFateDiceRandom.ForKey(seed, "r").NextInt(1) == 0, "random picks stay in range");

// ---------------------------------------------------------------- saved record and old saves
HbrFateDiceState saved = With(bosses: 1, cracks: 2, equipped: new[] { HbrFateDiceCatalog.Mirror }, bag: new[] { HbrFateDiceCatalog.Floor })
    .WithSwordheart(5, 7).WithReceipt("fate:a");
string json = saved.Serialize();
HbrFateDiceState loaded = HbrFateDiceState.Parse(json);
Expect(loaded.Serialize() == json && loaded.Swordheart == 5 && loaded.Cracks == 2 && Same(loaded.Equipped, HbrFateDiceCatalog.Mirror) &&
       Same(loaded.Bag, HbrFateDiceCatalog.Floor) && loaded.HasReceipt("fate:a") && loaded.Version == 1, "the record survives a save and load: " + json);
Expect(!json.Contains("Slots") && !json.Contains("Capacity") && !json.Contains("RawPenalty"), "derived values are not saved");
Expect(HbrFateDiceState.Parse("") == HbrFateDiceState.Empty && HbrFateDiceState.Parse(null) == HbrFateDiceState.Empty &&
       HbrFateDiceState.Parse("not json") == HbrFateDiceState.Empty && HbrFateDiceState.Parse("[1,2]") == HbrFateDiceState.Empty,
    "an empty or broken record reads as a fresh fate die");
HbrFateDiceState cleaned = HbrFateDiceState.Parse(
    "{\"Version\":1,\"Cracks\":-2,\"Swordheart\":-1,\"Equipped\":[\"WIDE_EDGE\",\"WIDE_EDGE\",\"NOPE\",\"FLOOR\",\"MIRROR\"],\"Bag\":[\"FLOOR\",\"REROLL\"]}");
Expect(Same(cleaned.Equipped, HbrFateDiceCatalog.WideEdge, HbrFateDiceCatalog.Floor) &&
       Same(cleaned.Bag, HbrFateDiceCatalog.Reroll, HbrFateDiceCatalog.Mirror) && cleaned.Cracks == 0 && cleaned.Swordheart == 0,
    "loading drops unknown and duplicate fragments, clamps counts and fits the set to its slots");
// Old saves: no record means two empty slots plus one per boss already behind the run.
Expect(HbrFateDiceState.Effective(empty, 0).Slots == 2 && HbrFateDiceState.Effective(empty, 1).Slots == 3 &&
       HbrFateDiceState.Effective(empty, 2).Slots == 4 && HbrFateDiceState.Effective(empty, 2).Version == 1,
    "a run from before the fate dice reads as empty slots for the act it is in");
Expect(HbrFateDiceState.Effective(saved, 2) == saved && HbrFateDiceState.Effective(saved, 2).Slots == 3,
    "a stored record keeps its own boss count");

// ---------------------------------------------------------------- zhs texts of the new rules
string zhs = Path.Combine(repo, "HBRGOSakiMod", "localization", "zhs");
JsonElement Table(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(zhs, name + ".json"))).RootElement;
string Text(JsonElement table, string key) => table.TryGetProperty(key, out JsonElement value) ? value.GetString() : null;
JsonElement powers = Table("powers"), keywords = Table("card_keywords"), cards = Table("cards"), relics = Table("relics"), rest = Table("rest_site_ui");
string swordheart = Text(powers, "SWORDHEART_POWER.description");
Expect(swordheart.Contains("跨回合、跨战斗保留") && swordheart.Contains("基础上限为7层") && swordheart.Contains("大成功[/gold]获得2层") &&
       swordheart.Contains("7的倍数时获得1层") && swordheart.Contains("按[gold]魔剑[/gold]按钮") && !swordheart.Contains("放入手牌"),
    "剑心: kept across combats, two per critical success, one per raw multiple of seven, opens the magic sword by button");
Expect(Text(powers, "SWORDHEART_CLARITY_POWER.description").Contains("上限翻倍") &&
       Text(powers, "SWORDHEART_CLARITY_POWER.description").Contains("花掉的剑心翻倍") &&
       !Text(powers, "SWORDHEART_CLARITY_POWER.description").Contains("7的倍数") &&
       Text(cards, "SWORDHEART_CLARITY_HBRGO_SAKI.description").Contains("上限翻倍") &&
       Text(cards, "SWORDHEART_CLARITY_HBRGO_SAKI.description").Contains("花掉的剑心翻倍") &&
       !Text(cards, "SWORDHEART_CLARITY_HBRGO_SAKI.description").Contains("7的倍数"),
    "剑心通明 doubles this combat's maximum and consumption without adding multiple-of-seven gains");
foreach (string key in new[] { "MAGIC_SWORD_MODE_POWER.description", "MAGIC_SWORD_MODE_POWER.compactDescription" })
{
    string text = Text(powers, key);
    Expect(text.Contains("再按一次") && text.Contains("魔剑幸运七") && !text.Contains("千早同学") && !text.Contains("仅一次") &&
           !text.Contains("75%") && !text.Contains("免疫"), key + ": closes by the button, no survival rules, keeps 千早同学？");
    Expect(text.Contains("额外获得{hbrgoSakiEnergyPrefix:energyIcons(1)}") &&
           !text.Contains("失去{hbrgoSakiEnergyPrefix:energyIcons(1)}") &&
           text.Contains("2%") && text.Contains("结束当前回合"),
        key + ": shows bonus energy, deck scaling and ending the turn on exit");
}
Expect(Text(powers, "MAGIC_SWORD_MODE_POWER.description").Contains("段数等于这次花掉的剑心") &&
       Text(powers, "MAGIC_SWORD_MODE_POWER.description").Contains("原有的形态照常生效"), "魔剑模式 lays over the current form");
Expect(Text(powers, "MAGIC_SWORD_MODE_POWER.description").Contains("终结技结算后增加一道裂痕") &&
       Text(powers, "MAGIC_SWORD_MODE_POWER.description").Contains("战斗结束时魔剑仍开着"),
    "magic sword wording settles cracks after a finisher or an open-mode combat end");
foreach (string suffix in new[] { "description", "description.hbr", "upgradedDescription", "upgradedDescription.hbr" })
    Expect(Text(cards, "TELEPORT_HBRGO_SAKI." + suffix).Contains("不打出终结技，不结束回合，不增加裂痕"),
        "Teleport " + suffix + " explains the emergency-retreat exception");
foreach (string key in new[] { "REPTURE_DUAL_SWORDS_POWER.description", "REPTURE_DUAL_SWORDS_POWER.compactDescription",
             "REPTURE_BOW_POWER.description", "REPTURE_BOW_POWER.hbrDescription", "RAPTURE_SCYTHE_POWER.description" })
    Expect(!Text(powers, key).Contains("7/77") && !Text(powers, key).Contains("魔剑模式"), key + ": the magic sword no longer weakens it; 7/77 is a fragment");
foreach (string key in new[] { "STRONGEST_SHIELD_POWER.description", "STRONGEST_SHIELD_POWER.enhancedDescription",
             "PSEUDO_SPACE_WARP_POWER.description", "HBRGOSAKIMOD-PSEUDO_SPACE_WARP_POWER.description", "NOT_OVER_YET_POWER.swordheartDescription" })
    Expect(Text(powers, key).Contains("大成功[/gold]获得2层剑心") || Text(powers, key).Contains("大成功获得2层剑心"),
        key + ": a critical success gives two swordheart");
foreach (string key in new[] { "HBRGO_SAKI_MOD_KEYWORD_ROLL.sakiAttackSkillDescription", "HBRGO_SAKI_MOD_KEYWORD_ROLL.sakiPowerDescription" })
    Expect(Text(keywords, key).Contains("原始出目是7的倍数时获得1层") && !Text(keywords, key).Contains("17获得1层") &&
           Text(keywords, key).Contains("命运碎片"), key + ": the new swordheart rule and the range fragments");
Expect(Text(keywords, "HBRGO_SAKI_MOD_KEYWORD_CRITICAL_SUCCESS.sakiDescription").Contains("2层[gold]剑心") &&
       Text(keywords, "HBRGO_SAKI_MOD_KEYWORD_CRITICAL_SUCCESS.sakiCompactDescription").Contains("2层[gold]剑心"), "大成功: two swordheart");
Expect(Text(cards, "MAGIC_SWORD_MODE_HBRGO_SAKI.description").Contains("裂痕") &&
       !Text(cards, "MAGIC_SWORD_MODE_HBRGO_SAKI.description").Contains("双剑形态"), "the old 魔剑模式 card opens it like the button");
Expect(Text(relics, "HBR_FATE_FRAGMENT_REWARD.description") is { } reward && reward.Contains("{Name}") && reward.Contains("{Category}") &&
       reward.Contains("{Effect}"), "the fragment reward text");
Expect(Text(rest, "OPTION_HBR_FATE_DICE_REPAIR.name") == "修骰子" &&
       Text(rest, "OPTION_HBR_FATE_DICE_REPAIR.description").Contains("{cracks}"), "the rest site's 修骰子");
foreach (string file in Directory.GetFiles(zhs, "*.json"))
foreach (JsonProperty entry in JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateObject())
    if (entry.Value.ValueKind == JsonValueKind.String)
        Expect(!entry.Value.GetString().Contains("满层[gold]剑心") && !entry.Value.GetString().Contains("满层剑心") &&
               !entry.Value.GetString().Contains("每含有1个7"), $"{Path.GetFileName(file)} {entry.Name}: no old swordheart rule");

Console.WriteLine($"hbr-fate-dice: {checks} checks passed");
return 0;
