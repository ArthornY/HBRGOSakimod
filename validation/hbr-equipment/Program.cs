using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Equipment;

int checks = 0;
int sections = 0;
string root = FindRoot();

void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException("FAIL " + description);
}

void Equal<T>(T actual, T expected, string description)
{
    checks++;
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
        throw new InvalidOperationException($"FAIL {description}: expected {expected}, got {actual}");
}

void Section(string name, Action body)
{
    body();
    sections++;
    Console.WriteLine($"PASS {name}");
}

HbrEquipmentState Seeded() => HbrEquipmentState.Empty with { Seed = 0x1234_5678_9ABC_DEF0UL };

HbrEquipmentState Add(HbrEquipmentState state, string template, out int id, int[] rolls = null, params HbrPlusSlot[] plus) =>
    HbrEquipmentRules.AddItem(state, template, rolls, plus, out id);

HbrEquipmentState Put(HbrEquipmentState state, string template, HbrEquipmentSlot slot, int[] rolls = null,
    params HbrPlusSlot[] plus)
{
    state = Add(state, template, out int id, rolls, plus);
    HbrEquipmentState next = HbrEquipmentRules.Equip(state, id, slot, out string error);
    if (next == null) throw new InvalidOperationException($"FAIL equip {template} into {slot}: {error}");
    return next;
}

HbrEquipmentTemplate T(string id) => HbrEquipmentCatalog.Find(id) ?? throw new InvalidOperationException("missing " + id);

// The six protection/euphoria chains have a user-approved two-energy policy.
decimal AdaptedEnergy(HbrEquipmentTemplate template) => template.Id is
    "Acc.Necklace006.F001" or "Acc.Necklace006.F002" or "Acc.Necklace006.F003" or
    "Acc.Necklace006.F004" or "Acc.Necklace006.F005" or "Acc.Necklace006.F006" ? 2m : 1m;

using JsonDocument snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "validation", "hbr-equipment", "master-snapshot.json")));
Dictionary<string, JsonElement> rows = snapshot.RootElement.GetProperty("rows").EnumerateArray()
    .ToDictionary(row => row.GetProperty("label").GetString(), row => row);

Section("Every catalog row matches the frozen hbr.quest master data", () =>
{
    Equal(HbrEquipmentCatalog.All.Select(row => row.Id).Distinct().Count(), HbrEquipmentCatalog.All.Count, "Unique ids");
    Equal(rows.Count, HbrEquipmentCatalog.All.Count, "Snapshot holds exactly the catalog rows");
    foreach (HbrEquipmentTemplate template in HbrEquipmentCatalog.All)
    {
        Check(rows.TryGetValue(template.Id, out JsonElement row), $"{template.Id} in snapshot");
        Equal(template.Rarity, row.GetProperty("rarity").GetInt32(), $"{template.Id} rarity");
        Equal(template.OriginalName, row.GetProperty("jpName").GetString(), $"{template.Id} Japanese name");
        string image = row.GetProperty("image").GetString().Replace(".webp", ".png");
        // Documented stand-in: the high charms' Charm_*2 icons are missing locally.
        if (template.Id.StartsWith("Acc.Charm_Dimension.", StringComparison.Ordinal)) image = image.Replace("2.png", "1.png");
        Equal(template.Icon, image, $"{template.Id} icon");
        string cnName = row.GetProperty("cnName").ValueKind == JsonValueKind.String ? row.GetProperty("cnName").GetString() : null;
        if (cnName != null)
        {
            Equal(template.Name, cnName, $"{template.Id} official Chinese name");
            string firstLine = row.GetProperty("cnText").GetString().Split('\n')[0].Replace(" ", string.Empty).TrimEnd('/');
            Equal(template.Flavor, firstLine, $"{template.Id} official Chinese flavor line");
        }
        else
        {
            // High charms are not on the China server yet: marked mod translations.
            Check(template.Id.StartsWith("Acc.Charm_Dimension.", StringComparison.Ordinal) && template.Name.StartsWith("高级", StringComparison.Ordinal),
                $"{template.Id} is a marked translation");
        }
        var expected = row.GetProperty("effects").EnumerateArray()
            .Select(effect => (Stat: MapType(effect.GetProperty("type").GetString()), Value: effect.GetProperty("value").GetDecimal()))
            .Where(effect => effect.Stat.HasValue)
            .Select(effect => (effect.Stat.Value, effect.Stat == HbrEquipmentStat.CriticalRate ? effect.Value / 100m : effect.Value))
            .OrderBy(effect => effect.Item1).ToList();
        var actual = template.Values.Select(value => (value.Stat, value.Original)).OrderBy(value => value.Stat).ToList();
        Check(expected.SequenceEqual(actual), $"{template.Id} values {string.Join(",", actual)} = {string.Join(",", expected)}");
        int maxLv = row.GetProperty("maxLv").ValueKind == JsonValueKind.Number ? row.GetProperty("maxLv").GetInt32() : 0;
        Equal(template.PlusSlots, maxLv, $"{template.Id} + slots");
        if (template.Kind == HbrEquipmentKind.Booster)
            Equal(template.ChipSlots, row.GetProperty("maxSlot").GetInt32(), $"{template.Id} chip slots");
        if (template.Kind is HbrEquipmentKind.Booster or HbrEquipmentKind.Chip)
            Equal(template.Price, GoldPrice(row.GetProperty("price").GetDecimal()), $"{template.Id} gold price from GP");
    }
    JsonElement[] materials = snapshot.RootElement.GetProperty("materials").EnumerateArray().ToArray();
    Equal(materials[0].GetProperty("cnName").GetString(), HbrEquipmentCatalog.PlusReleaseName, "青绿琥珀之露 official name");
    Equal(materials[1].GetProperty("cnName").GetString(), HbrEquipmentCatalog.PlusChangeName, "阳石之露 official name");
    Equal(materials[0].GetProperty("image").GetString().Replace(".webp", ".png"), HbrEquipmentCatalog.PlusReleaseIcon, "Release icon");
    Equal(materials[1].GetProperty("image").GetString().Replace(".webp", ".png"), HbrEquipmentCatalog.PlusChangeIcon, "Change icon");
});

Section("Random tables and adapted effects follow the original passives", () =>
{
    foreach (HbrEquipmentTemplate template in HbrEquipmentCatalog.All)
    {
        JsonElement row = rows[template.Id];
        var groups = new List<(HbrEquipmentStat? Stat, HbrEquipmentEffect? Effect, List<(decimal, int, int)> Rows)>();
        var effects = new List<HbrEquipmentEffectValue>();
        foreach (JsonElement table in row.GetProperty("rolls").EnumerateArray())
        {
            JsonElement first = table[0];
            string type = first.GetProperty("type").GetString();
            List<(decimal, int, int)> Rows(Func<JsonElement, decimal> value) => table.EnumerateArray()
                .Select(entry => (value(entry), entry.GetProperty("plus").GetInt32(), entry.GetProperty("ratio").GetInt32())).ToList();
            if (MapType(type) is { } stat) groups.Add((stat, null, Rows(entry => entry.GetProperty("value").GetDecimal())));
            else if (type == "Sp") effects.Add(new(HbrEquipmentEffect.EnergyPerTurn, AdaptedEnergy(template)));
            else if (type == "PassiveSkill" && HitEffect(first.GetProperty("passive")) is { } hit)
                // The table keeps its grades and weights; each value is the passive's largest bonus.
                groups.Add((null, hit, Rows(entry => Percent(entry.GetProperty("passive").GetProperty("power")[1].GetDecimal()))));
            else if (type == "PassiveSkill" && first.GetProperty("passive").GetProperty("skillType").GetString() == "AttackUpOnlyNormal")
                effects.Add(new(HbrEquipmentEffect.PerfectStrike, 1m));
            else
                // Element rules have no counterpart: the element skill count and
                // the element given to normal attacks.
                Check(first.GetProperty("passive").GetProperty("skillType").GetString() is "SkillLimitCountUp" or "AdditionalElementOnlyNormal",
                    $"{template.Id} leaves out only element tables");
        }
        foreach (JsonElement entry in row.GetProperty("effects").EnumerateArray())
        {
            string type = entry.GetProperty("type").GetString();
            if (type == "Sp") effects.Add(new(HbrEquipmentEffect.EnergyPerTurn, AdaptedEnergy(template)));
            else if (type == "PassiveSkill") { if (FixedEffect(entry.GetProperty("passive")) is { } mapped) effects.Add(mapped); }
            else Check(MapType(type).HasValue, $"{template.Id} fixed row {type} is a stat");
        }
        Equal(template.RollGroups.Count, groups.Count, $"{template.Id} random table count");
        for (int i = 0; i < groups.Count; i++)
        {
            HbrEquipmentRollGroup group = template.RollGroups[i];
            Check(group.Stat == groups[i].Stat && group.Effect == groups[i].Effect &&
                group.Rows.Select(entry => (entry.Original, entry.Plus, entry.Weight)).SequenceEqual(groups[i].Rows),
                $"{template.Id} random table {i}");
        }
        Check(template.Effects.OrderBy(effect => effect.Effect).SequenceEqual(effects.OrderBy(effect => effect.Effect)),
            $"{template.Id} effects {string.Join(",", template.Effects)} = {string.Join(",", effects)}");
        Equal(template.HasEffects, effects.Count > 0 || groups.Any(group => group.Effect.HasValue), $"{template.Id} effect flag");
    }
    Equal(HbrEquipmentCatalog.All.Count(template => template.Rarity == 6), 25 + 4 + 5 + 16, "★6: 25 rings, 4 earrings, 5 bracelets, 16 necklaces");
    Equal(HbrEquipmentCatalog.All.Count(template => template.InPool(HbrEquipmentPool.Boss)), 45 + 10, "Boss rows: 45 gate bracelets and 10 ancient chains");
});

Section("Every original sprite is present with a matching provenance hash", () =>
{
    string art = Path.Combine(root, "images", "ui", "hbr_equipment");
    using JsonDocument sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(art, "sources.json")));
    var listed = new HashSet<string>(StringComparer.Ordinal);
    foreach (JsonElement file in sources.RootElement.GetProperty("files").EnumerateArray())
    {
        string relative = file.GetProperty("file").GetString();
        string path = Path.Combine(art, relative.Replace('/', Path.DirectorySeparatorChar));
        Check(File.Exists(path), $"{relative} exists");
        Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            file.GetProperty("sha256").GetString(), $"{relative} sha256");
        listed.Add(relative);
    }
    foreach (string icon in HbrEquipmentCatalog.All.Select(row => row.Icon)
                 .Append(HbrEquipmentCatalog.PlusReleaseIcon).Append(HbrEquipmentCatalog.PlusChangeIcon).Distinct())
        Check(listed.Contains("icons/" + icon), $"icon {icon} is recorded");
    foreach (string ui in new[] { "FriendStreetShop.png", "MSatsukiDefault.png", "btnExchangeShopListNomal.png",
                 "btnExchangeShopListActive.png", "btnExchangeShopListDisable.png", "itemBackground.png", "IconGP.png",
                 "IconHomeShopDefault.png", "IconHomeCatShopDefault.png", "SelectFlameBaceOff.png", "SelectFlameBaceOn.png" })
        Check(listed.Contains(ui), $"UI sprite {ui} is recorded");
});

Section("Original values scale by the item's grade; critical rate keeps its percentage", () =>
{
    Check(HbrEquipmentCatalog.GradeScales.SequenceEqual([1.75m, 1.9m, 2.1m]), "Grade scales ×1.75 / ×1.9 / ×2.1");
    // Boosters by the act whose shop sells them: their stars are only the icon grade.
    Equal(HbrEquipmentCatalog.ScaleOf(T("SBooster_0101")), 1.75m, "第一阶段 (act 1 shop) ×1.75");
    Equal(HbrEquipmentCatalog.ScaleOf(T("SBooster_0301")), 1.9m, "倾注 (act 2 shop) ×1.9");
    Equal(T("SBooster_0601").Rarity, 1, "大师 has one star");
    Equal(HbrEquipmentCatalog.ScaleOf(T("SBooster_0601")), 2.1m, "大师 (act 3 shop) ×2.1 despite one star");
    // Every other item by stars: ★1–2, ★3–4, ★5–6.
    foreach ((string id, decimal scale) in new[]
             {
                 ("Acc.Ring001a", 1.75m), ("Acc.Ring003a", 1.75m), ("Acc.Ring004a", 1.9m), ("Acc.Ring005a", 1.9m),
                 ("Acc.Ring005d", 2.1m), ("Acc.Ring006a_00", 2.1m), ("Acc.Bracelet006a", 2.1m), ("Acc.Charm01.F001", 1.9m),
                 ("Acc.Charm_Dimension.F001", 1.9m), ("SChip_1001", 1.75m), ("SChip_2007", 1.75m), ("SChip_3007", 1.9m),
                 ("SChip_4006", 1.9m), ("SChip_5001", 2.1m), ("SChip_5004", 2.1m)
             })
        Equal(HbrEquipmentCatalog.ScaleOf(T(id)), scale, $"{id} grade scale");
    foreach (HbrEquipmentTemplate chip in HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Chip))
        Equal(HbrEquipmentCatalog.Grade(chip), chip.TierFrom, $"{chip.Id}: chip stars match the act it is sold in");
    Check(HbrEquipmentCatalog.All.All(t => HbrEquipmentCatalog.GradeScales.Contains(HbrEquipmentCatalog.ScaleOf(t))), "Every row has a grade");

    HbrEquipmentBonus first = HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = "SBooster_0101" });
    Equal(first, new HbrEquipmentBonus(14m, 14m, 14m, 14m, 14m, 14m, 0m, 0m), "第一阶段 all +8 → +14");
    Equal(HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = "SChip_2001" }).CriticalRate, 0.2m, "暴击芯片Ⅱ +0.2%");
    Equal(HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = "Acc.Earring005a" }).Dp, 912m, "秘银耳坠 ★4 DP 480 → 912");
    Equal(HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = "Acc.Charm01.F003", Rolls = [4] }).Vitality, 28.5m, "体力吊饰 +15 → 28.5");
    Equal(HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = "Acc.Charm_Dimension.F005" }).Intelligence, 34.2m, "高级智慧吊饰 18 → 34.2");
    var plus = new HbrEquipmentItem
    {
        Template = "Acc.Ring005a",
        Plus = [new(HbrEquipmentStat.Strength, 3), new(HbrEquipmentStat.Dp, 2), new(HbrEquipmentStat.CriticalRate, 1)]
    };
    HbrEquipmentBonus ring = HbrEquipmentRules.ItemBonus(plus);
    Equal(ring.Strength, 45.6m + 5.7m, "秘银戒指 ★4: 24 + 力量 +3 → 51.3");
    Equal(ring.Dp, 76m, "DP +2 level → 40 → 76 on a ★4 ring");
    Equal(ring.CriticalRate, 0.1m, "暴击率 +1 level → 0.1%");
    Equal(HbrEquipmentText.PlusSlotsText(plus), "力量 +5.7、DP +76、暴击率 +0.1%", "+ slots read in the ring's scale");
    var low = new HbrEquipmentItem { Template = "Acc.Ring003a", Plus = [new(HbrEquipmentStat.Strength, 2)] };
    Equal(HbrEquipmentRules.ItemBonus(low).Strength, (15m + 2m) * 1.75m, "★2 ring and its + slot both ×1.75");
    Equal(HbrEquipmentText.PlusSlotText(low, 0), "力量 +3.5", "★2 + slot text");
    Equal(HbrEquipmentRules.DisplayPlus(plus), 6, "+N is the sum of + levels");
    Equal(HbrEquipmentText.Name(plus), "秘银戒指+6", "Name shows +N");
    Equal(HbrEquipmentText.Name(new HbrEquipmentItem { Template = "Acc.Charm01.F001", Rolls = [1] }), "力量吊饰+1", "Charm grade");
    Equal(HbrEquipmentText.Summary(first), "全属性 +14", "Booster summary");
    Equal(HbrEquipmentText.Summary(HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = "SBooster_0102" })),
        "体力、精神 +26.25 / 其他属性 +21", "Grouped summary like the original text");
    Equal(HbrEquipmentText.Describe(T("SBooster_0306")), "力量 +108.3 / 其他属性 +36.1 / 芯片装备栏 0", "村正 text (act 2 shop)");
    Equal(HbrEquipmentText.Describe(T("Acc.Charm01.F001")), "力量 +11.4～28.5", "Charm range text");
    Equal(HbrEquipmentText.CharmCraftText(0), "随机获得一个吊饰，属性 +11.4～28.5\n第二幕起有几率获得高级吊饰（+34.2）", "Act 1 charm alchemy text");
    Equal(HbrEquipmentText.CharmCraftText(1), "随机获得一个吊饰，属性 +11.4～28.5\n10% 几率获得高级吊饰（+34.2）", "Act 2 charm alchemy text");
    Equal(HbrEquipmentText.CharmCraftText(2), "随机获得一个吊饰，属性 +11.4～28.5\n25% 几率获得高级吊饰（+34.2）", "Act 3 charm alchemy text");
    Equal(HbrEquipmentText.ScaleRule(),
        "原作数值按装备档次换算后计入战斗属性：★1–2 ×1.75，★3–4 ×1.9，★5–6 ×2.1；增幅器按出售的幕，第一到第三幕依次 ×1.75、×1.9、×2.1。暴击率保持原作数值。",
        "Equipment page rule text");
});

Section("Booster chip slots, slot kinds and moving items", () =>
{
    HbrEquipmentState state = Seeded();
    state = Add(state, "SChip_1001", out int chipA);
    state = Add(state, "SChip_1002", out int chipB);
    Equal(HbrEquipmentRules.Equip(state, chipA, HbrEquipmentSlot.Chip(0), out _), null, "No booster, no chip slot");
    state = Put(state, "SBooster_0201", HbrEquipmentSlot.Booster);
    Equal(HbrEquipmentRules.ChipSlotCount(state), 3, "控制器 opens three chip slots");
    state = HbrEquipmentRules.Equip(state, chipA, HbrEquipmentSlot.Chip(0), out _);
    state = HbrEquipmentRules.Equip(state, chipB, HbrEquipmentSlot.Chip(2), out _);
    Equal(HbrEquipmentRules.Equip(state, chipA, HbrEquipmentSlot.Chip(3), out string closed), null, "Fourth slot is closed");
    Equal(closed, "增幅器的芯片栏不够", "Closed chip reason");
    Equal(HbrEquipmentRules.Equip(state, chipA, HbrEquipmentSlot.Ring, out string kind), null, "Chip cannot go in a ring slot");
    Equal(kind, "不能装在这一栏", "Kind reason");
    HbrEquipmentState moved = HbrEquipmentRules.Equip(state, chipA, HbrEquipmentSlot.Chip(1), out _);
    Equal(moved.At(HbrEquipmentSlot.Chip(0)), 0, "Moving clears the old slot");
    Equal(moved.At(HbrEquipmentSlot.Chip(1)), chipA, "Moving fills the new slot");
    Equal(moved.Revision, state.Revision + 1, "Each change bumps the revision");
    Equal(HbrEquipmentRules.Equip(moved, chipA, HbrEquipmentSlot.Chip(1), out _), moved, "Same slot is a no-op");
    HbrEquipmentState swapped = Put(moved, "SBooster_0101", HbrEquipmentSlot.Booster);
    Equal(HbrEquipmentRules.ChipSlotCount(swapped), 1, "第一阶段 has one chip slot");
    Equal(swapped.At(HbrEquipmentSlot.Chip(1)), 0, "Chip in a closed slot returns to the inventory");
    Equal(swapped.At(HbrEquipmentSlot.Chip(2)), 0, "Second chip also returns");
    Check(swapped.FindItem(chipA) != null && swapped.FindItem(chipB) != null, "Returned chips stay owned");
    HbrEquipmentState withBooster = Put(swapped, "SBooster_0201", HbrEquipmentSlot.Booster);
    HbrEquipmentState withChip = HbrEquipmentRules.Equip(withBooster, chipA, HbrEquipmentSlot.Chip(0), out _);
    Equal(withChip.At(HbrEquipmentSlot.Chip(0)), chipA, "Chip refits after a booster swap");
    HbrEquipmentState bare = HbrEquipmentRules.Unequip(withChip, HbrEquipmentSlot.Booster, out _);
    Equal(bare.At(HbrEquipmentSlot.Chip(0)), 0, "Removing the booster closes every chip slot");
    Equal(HbrEquipmentRules.Unequip(bare, HbrEquipmentSlot.Booster, out _), null, "Unequipping an empty slot fails");
});

Section("Accessory level, charm slots and the original level formula", () =>
{
    HbrEquipmentState state = Seeded();
    state = Put(state, "Acc.Ring004a", HbrEquipmentSlot.Ring, null, new HbrPlusSlot(HbrEquipmentStat.Luck, 3));
    Equal(HbrEquipmentRules.AccessoryLevel(state), 3 + 1, "★3 + half of +3");
    Equal(HbrEquipmentRules.OpenCharmSlots(state), 1, "Lv.4 opens the first charm slot");
    state = Put(state, "Acc.Charm01.F001", HbrEquipmentSlot.Charm(0), [2]);
    Equal(HbrEquipmentRules.Equip(Add(state, "Acc.Charm01.F002", out int second), second, HbrEquipmentSlot.Charm(1), out string why),
        null, "Second charm slot needs Lv.8");
    Equal(why, "饰品 Lv. 不足，吊饰栏未解锁", "Charm reason");
    state = Put(state, "Acc.Earring004a", HbrEquipmentSlot.Earring);
    state = Put(state, "Acc.Bracelet004a", HbrEquipmentSlot.Bracelet);
    state = Put(state, "Acc.Necklace005a", HbrEquipmentSlot.Necklace);
    Equal(HbrEquipmentRules.AccessoryLevel(state), 12 + 1, "Four ★3 plus half of +3");
    Equal(HbrEquipmentRules.OpenCharmSlots(state), 3, "Lv.13 opens three slots");
    int charm = state.At(HbrEquipmentSlot.Charm(0));
    HbrEquipmentState lower = HbrEquipmentRules.Unequip(HbrEquipmentRules.Unequip(HbrEquipmentRules.Unequip(
        state, HbrEquipmentSlot.Earring, out _), HbrEquipmentSlot.Bracelet, out _), HbrEquipmentSlot.Ring, out _);
    Equal(HbrEquipmentRules.AccessoryLevel(lower), 3, "Only the necklace remains");
    Equal(lower.At(HbrEquipmentSlot.Charm(0)), 0, "A charm in a locked slot returns to the inventory");
    Check(lower.FindItem(charm) != null, "The charm is still owned");
    Equal(HbrEquipmentCatalog.CharmSlotLevels.Length, HbrEquipmentCatalog.CharmSlots, "Six thresholds");
});

Section("+ slots open in order with 青绿琥珀之露 and change with 阳石之露", () =>
{
    HbrEquipmentState state = Seeded();
    state = Add(state, "Acc.Ring005a", out int ring);
    Equal(HbrEquipmentRules.OpenPlus(state, ring, out string noMaterial), null, "Needs a material");
    Equal(noMaterial, "青绿琥珀之露不足", "Material reason");
    state = HbrEquipmentRules.AddMaterials(state, 4, 2);
    for (int i = 0; i < 3; i++) state = HbrEquipmentRules.OpenPlus(state, ring, out _);
    Equal(state.FindItem(ring).Plus.Length, 3, "★4 ring has three + slots");
    Equal(state.PlusRelease, 1, "Three materials used");
    Equal(HbrEquipmentRules.OpenPlus(state, ring, out string full), null, "No fourth slot");
    Equal(full, "这件饰品没有可解锁的 + 栏", "Full reason");
    Equal(HbrEquipmentRules.OpenPlus(Add(state, "Acc.Ring001a", out int low), low, out _), null, "★1 ring has no + slot");
    HbrPlusSlot[] before = state.FindItem(ring).Plus;
    int counter = state.RandomCounter;
    HbrEquipmentState changed = HbrEquipmentRules.ChangePlus(state, ring, 1, out _);
    Equal(changed.PlusChange, 1, "One 阳石之露 used");
    Check(changed.RandomCounter > counter, "Rolling advances the saved counter");
    Equal(changed.FindItem(ring).Plus[0], before[0], "Other slots keep their effect");
    Equal(changed.FindItem(ring).Plus[2], before[2], "Other slots keep their effect");
    Equal(HbrEquipmentRules.ChangePlus(state, ring, 3, out _), null, "Locked index cannot change");
    Equal(HbrEquipmentRules.ChangePlus(state, ring, 1, out _).Serialize(), changed.Serialize(),
        "Same seed and counter roll the same result");
    foreach (HbrPlusSlot slot in Enumerable.Range(0, 400).Select(i => HbrEquipmentGenerator.RollPlusSlot(new HbrEquipmentRandom(99, i))))
        Check(slot.Level is >= 1 and <= 3 && Enum.IsDefined(slot.Stat), "+ slot within the pool");
});

Section("Commands: revisions, combat lock, gold, alchemy, selling and claims", () =>
{
    HbrEquipmentState state = Seeded();
    state = Add(state, "Acc.Ring004a", out int ring);
    var equip = new HbrEquipmentCommand { Revision = state.Revision, Op = HbrEquipmentOp.Equip, ItemId = ring, Slot = HbrEquipmentSlot.Ring.Position };
    Check(HbrEquipmentCommands.Evaluate(state, equip, 0, true, 0).Succeeded, "Equip out of combat");
    Equal(HbrEquipmentCommands.Evaluate(state, equip, 0, false, 0).Error, HbrEquipmentCommands.LoadoutLocked, "Equip locked in combat");
    Equal(HbrEquipmentCommands.Evaluate(state, equip with { Revision = state.Revision - 1 }, 0, true, 0).Succeeded, false, "Stale revision refused");
    var buy = new HbrEquipmentCommand { Revision = state.Revision, Op = HbrEquipmentOp.Buy, Offer = "item.SBooster_0101" };
    Equal(HbrEquipmentCommands.Evaluate(state, buy, 0, false, 24).Error, "金币不足", "Not enough gold");
    HbrEquipmentOutcome bought = HbrEquipmentCommands.Evaluate(state, buy, 0, false, 25);
    Check(bought.Succeeded && bought.GoldCost == 25 && bought.State.Items.Any(item => item.Template == "SBooster_0101"), "Buying works in combat and costs 25");
    Equal(HbrEquipmentCommands.Evaluate(state, buy with { Offer = "item.SBooster_0601" }, 0, true, 999).Error, "这件商品现在没有货", "Other act stock");
    var craft = new HbrEquipmentCommand { Revision = state.Revision, Op = HbrEquipmentOp.Buy, Offer = "craft.earring" };
    HbrEquipmentOutcome crafted = HbrEquipmentCommands.Evaluate(state, craft, 1, true, 999);
    HbrEquipmentItem made = crafted.State.Items.Last();
    Equal(T(made.Template).Kind, HbrEquipmentKind.Earring, "Alchemy makes the chosen kind");
    Equal(crafted.GoldCost, 75, "Act 2 accessory alchemy costs 75");
    Equal(HbrEquipmentCommands.Evaluate(state, craft, 1, true, 999).State.Serialize(), crafted.State.Serialize(), "Alchemy is deterministic");
    Check(crafted.State.RandomCounter > state.RandomCounter, "Alchemy advances the counter");
    HbrEquipmentState worn = HbrEquipmentRules.Equip(state, ring, HbrEquipmentSlot.Ring, out _);
    HbrEquipmentState stocked = HbrEquipmentRules.AddMaterials(worn, 1, 0);
    var openWorn = new HbrEquipmentCommand { Revision = stocked.Revision, Op = HbrEquipmentOp.OpenPlus, ItemId = ring };
    Equal(HbrEquipmentCommands.Evaluate(stocked, openWorn, 0, false, 0).Error, HbrEquipmentCommands.LoadoutLocked,
        "Worn + slots locked in combat");
    Check(HbrEquipmentCommands.Evaluate(stocked, openWorn, 0, true, 0).Succeeded, "Worn + slots open out of combat");
    var sell = new HbrEquipmentCommand { Revision = worn.Revision, Op = HbrEquipmentOp.Sell, ItemId = ring };
    Equal(HbrEquipmentCommands.Evaluate(worn, sell, 0, true, 0).Error, "请先卸下这件装备", "Worn item cannot be sold");
    HbrEquipmentOutcome sold = HbrEquipmentCommands.Evaluate(state, sell with { Revision = state.Revision }, 0, true, 0);
    Check(sold.Succeeded && sold.GoldGain == 15 && sold.State.FindItem(ring) == null, "Selling a ★3 ring returns 15 gold");
    var claim = HbrEquipmentClaim.ForItem("claim:room:0", new HbrRolledItem(T("SChip_5004"), Array.Empty<int>(), Array.Empty<HbrPlusSlot>()));
    var claimCommand = new HbrEquipmentCommand { Op = HbrEquipmentOp.Claim, Claim = claim, Revision = -5 };
    HbrEquipmentOutcome claimed = HbrEquipmentCommands.Evaluate(state, claimCommand, 2, false, 0);
    Check(claimed.Succeeded && claimed.State.Items.Any(item => item.Template == "SChip_5004"), "Claims ignore revision and the combat lock");
    Equal(HbrEquipmentCommands.Evaluate(claimed.State, claimCommand, 2, false, 0).Error, "已经领取过了", "A receipt blocks a second claim");
    HbrEquipmentOutcome materials = HbrEquipmentCommands.Evaluate(state, new HbrEquipmentCommand
    {
        Op = HbrEquipmentOp.Claim, Claim = HbrEquipmentClaim.ForMaterials("claim:room:materials", 1, 2)
    }, 0, true, 0);
    Check(materials.State.PlusRelease == 1 && materials.State.PlusChange == 2, "Material claims add counts");
});

Section("Saved data and network payloads round-trip; damaged data degrades safely", () =>
{
    HbrEquipmentState state = Seeded();
    state = Put(state, "SBooster_0302", HbrEquipmentSlot.Booster);
    state = Put(state, "SChip_3007", HbrEquipmentSlot.Chip(3));
    state = Put(state, "Acc.Ring005b", HbrEquipmentSlot.Ring, null, new HbrPlusSlot(HbrEquipmentStat.Strength, 2));
    state = Add(state, "Acc.Earring006.F003", out int earring, [1, 5, 8]);
    state = Put(state, "Acc.Charm01.F001", HbrEquipmentSlot.Charm(0), [3]);
    state = HbrEquipmentRules.AddMaterials(state, 2, 1).WithReceipt("drop:a").WithReceipt("claim:a:0");
    string json = state.Serialize();
    HbrEquipmentState parsed = HbrEquipmentState.Parse(json);
    Equal(parsed.Serialize(), json, "Round trip keeps every field");
    Equal(HbrEquipmentRules.Totals(parsed), HbrEquipmentRules.Totals(state), "Round trip keeps the totals");
    Check(parsed.FindItem(earring).Rolls.SequenceEqual([1, 5, 8]), "Round trip keeps one row per random table");
    string shortRolls = json.Replace("\"Rolls\":[1,5,8]", "\"Rolls\":[2]");
    Check(HbrEquipmentState.Parse(shortRolls).FindItem(earring).Rolls.SequenceEqual([2, 0, 0]), "Missing table rows fall back to the first row");
    string wideRolls = json.Replace("\"Rolls\":[1,5,8]", "\"Rolls\":[9,-3,99,4]");
    Check(HbrEquipmentState.Parse(wideRolls).FindItem(earring).Rolls.SequenceEqual([2, 0, 8]), "Out-of-range rows are clamped and extras dropped");
    Equal(HbrEquipmentState.Parse(null), HbrEquipmentState.Empty, "Missing data");
    Equal(HbrEquipmentState.Parse("{not json"), HbrEquipmentState.Empty, "Broken JSON");
    Equal(HbrEquipmentState.Parse(json.Replace("\"Schema\":1", "\"Schema\":99")), HbrEquipmentState.Empty, "Unknown schema");
    string damaged = json.Replace("\"Acc.Ring005b\"", "\"Acc.Removed\"");
    HbrEquipmentState repaired = HbrEquipmentState.Parse(damaged);
    Equal(repaired.Items.Length, state.Items.Length - 1, "Unknown template dropped");
    Equal(repaired.At(HbrEquipmentSlot.Ring), 0, "Its slot is cleared");
    Equal(repaired.At(HbrEquipmentSlot.Charm(0)), 0, "Charm locked by the lower level is cleared");
    Check(repaired.FindItem(state.At(HbrEquipmentSlot.Charm(0))) != null, "The charm itself is kept");
    var command = new HbrEquipmentCommand
    {
        Revision = 7, Op = HbrEquipmentOp.Claim, ItemId = 3, Slot = 5, Index = 1, Offer = "craft.ring",
        Claim = HbrEquipmentClaim.ForItem("claim:x:0", new HbrRolledItem(T("Acc.Earring006.F001"), [2, 0, 8], [new(HbrEquipmentStat.Luck, 2)]))
    };
    HbrEquipmentCommand back = JsonSerializer.Deserialize<HbrEquipmentCommand>(JsonSerializer.SerializeToUtf8Bytes(command));
    Equal(JsonSerializer.Serialize(back), JsonSerializer.Serialize(command), "Network payload round trip");
    Equal(back.Claim.Items[0].Plus[0], new HbrPlusSlot(HbrEquipmentStat.Luck, 2), "Plus slot survives the payload");
    Check(back.Claim.Items[0].Rolls.SequenceEqual([2, 0, 8]), "Rolled rows survive the payload");
    HbrEquipmentItem claimed = back.Claim.ApplyTo(Seeded()).Items.Single();
    Check(claimed.Rolls.SequenceEqual([2, 0, 8]) && claimed.Template == "Acc.Earring006.F001", "A claim adds the rolled rows");
});

Section("Drop tables follow their weights and keep the Gold Hopper material", () =>
{
    var normal = Enumerable.Range(0, 6000).Select(i => HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Normal, 1, false,
        HbrEquipmentRandom.ForKey(42, $"room-{i}"))).ToList();
    double rate = normal.Count(drop => drop.Items.Count > 0) / 6000d;
    Check(Math.Abs(rate - HbrEquipmentGenerator.NormalDropPercent / 100d) < 0.03, $"Normal drop rate {rate:P1}");
    var rarities = normal.SelectMany(drop => drop.Items).GroupBy(item => item.Template.Rarity)
        .ToDictionary(group => group.Key, group => group.Count());
    int total = rarities.Values.Sum();
    IReadOnlyList<int> weights = HbrEquipmentGenerator.RarityWeightsFor(1);
    foreach (int rarity in new[] { 3, 4 })
    {
        // Necklaces have no ★2/★5 rows; nearest-rarity fallback moves part of
        // those rolls, so compare only the rarities every kind has.
        double share = rarities.GetValueOrDefault(rarity) / (double)total;
        Check(Math.Abs(share - weights[rarity - 1] / 100d) < 0.06, $"★{rarity} share {share:P1}");
    }
    Equal(HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Normal, 0, false, HbrEquipmentRandom.ForKey(1, "same")).Items.Select(item => item.Template.Id).FirstOrDefault(),
        HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Normal, 0, false, HbrEquipmentRandom.ForKey(1, "same")).Items.Select(item => item.Template.Id).FirstOrDefault(),
        "Same room key, same drop");
    for (int i = 0; i < 300; i++)
    {
        HbrEquipmentDrop elite = HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Elite, 0, false, HbrEquipmentRandom.ForKey(7, $"e{i}"));
        Check(elite.Items.Count == 1 && elite.Items[0].Template.IsAccessory && elite.Items[0].Template.Rarity >= 2, "Elite: an upgraded accessory");
        HbrEquipmentDrop boss = HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Boss, 0, false, HbrEquipmentRandom.ForKey(7, $"b{i}"));
        Check(boss.PlusRelease == 1 && boss.PlusChange == 1 && boss.Items[0].Template.IsAccessory &&
            !boss.Items[0].Template.InPool(HbrEquipmentPool.Boss) &&
            boss.Items.Skip(1).All(item => item.Template.Kind == HbrEquipmentKind.Bracelet &&
                item.Template.InPool(HbrEquipmentPool.Boss) && item.Template.InTier(0)),
            "Act 1 boss: accessory and both materials, maybe a gate bracelet, no artifact or chain");
        Check(elite.Items.All(item => item.Template.InPool(HbrEquipmentPool.Drop)), "Elite drops use drop rows");
        HbrEquipmentDrop hopper = HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Normal, 0, true, HbrEquipmentRandom.ForKey(7, $"h{i}"));
        Check(hopper.PlusRelease == 1, "Gold Hopper keeps its 青绿琥珀之露");
        foreach (HbrRolledItem item in elite.Items.Concat(boss.Items))
            Check(item.Plus.Length <= item.Template.PlusSlots, "Initial + slots within the template");
    }
    int artifacts = Enumerable.Range(0, 2000).Count(i => HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Boss, 2, false,
        HbrEquipmentRandom.ForKey(9, $"final-{i}")).Items.Any(item => !item.Template.IsSold && item.Template.Kind == HbrEquipmentKind.Chip));
    Check(Math.Abs(artifacts / 2000d - HbrEquipmentGenerator.BossArtifactPercent / 100d) < 0.04, $"Final-act boss artifact chip rate {artifacts / 2000d:P1}");
});

Section("★6 and boss rows come from their original sources", () =>
{
    HbrEquipmentKind[] accessories = [HbrEquipmentKind.Ring, HbrEquipmentKind.Earring, HbrEquipmentKind.Bracelet, HbrEquipmentKind.Necklace];
    // Drops: the corridor rings and 天命 drop; crafted ★6 rows never do.
    var drops = Enumerable.Range(0, 4000).SelectMany(i => HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Elite, 2, false,
        HbrEquipmentRandom.ForKey(5, $"late-{i}")).Items).ToList();
    Check(drops.All(item => item.Template.InPool(HbrEquipmentPool.Drop)), "Elite drops keep to drop rows");
    Check(drops.Any(item => item.Template.Kind == HbrEquipmentKind.Ring && item.Template.Rarity == 6), "Act 3 elites drop ★6 element rings");
    Check(drops.Any(item => item.Template.Id == "Acc.Necklace005e"), "天命项链 drops");
    Check(drops.All(item => item.Template.Rarity < 6 || item.Template.Kind == HbrEquipmentKind.Ring), "No crafted ★6 row drops");
    Equal(HbrEquipmentGenerator.RarityWeightsFor(2).Sum(), 100, "Drop weights sum to 100");
    Equal(HbrEquipmentGenerator.RarityWeightsFor(2, alchemy: true).Sum(), 100, "Alchemy weights sum to 100");
    // Alchemy: the crafted ★6 rows appear late; rings stop at ★5.
    foreach (HbrEquipmentKind kind in accessories)
    {
        var crafted = Enumerable.Range(0, 1500).Select(i => HbrEquipmentGenerator.RollAccessory(kind, 2, false,
            HbrEquipmentRandom.ForKey(77, $"craft-{kind}-{i}"), HbrEquipmentPool.Alchemy)).ToList();
        Check(crafted.All(item => item.Template.Kind == kind && item.Template.InPool(HbrEquipmentPool.Alchemy)), $"{kind} alchemy rows");
        double sixes = crafted.Count(item => item.Template.Rarity == 6) / 1500d;
        if (kind == HbrEquipmentKind.Ring) Equal(sixes, 0d, "Alchemy never makes ★6 rings");
        else Check(Math.Abs(sixes - 0.35) < 0.04, $"Act 3 {kind} alchemy ★6 share {sixes:P1}");
        var early = Enumerable.Range(0, 600).Select(i => HbrEquipmentGenerator.RollAccessory(kind, 0, false,
            HbrEquipmentRandom.ForKey(78, $"early-{kind}-{i}"), HbrEquipmentPool.Alchemy)).ToList();
        Check(early.All(item => item.Template.Rarity <= 4), $"Act 1 {kind} alchemy stays at ★4 or lower");
    }
    // Boss extras: a gate bracelet of the act, and from act 2 an ancient chain.
    for (int tier = 0; tier < 3; tier++)
    {
        var bosses = Enumerable.Range(0, 3000).Select(i => HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Boss, tier, false,
            HbrEquipmentRandom.ForKey(11, $"boss-{tier}-{i}"))).ToList();
        var extras = bosses.SelectMany(drop => drop.Items.Where(item => item.Template.InPool(HbrEquipmentPool.Boss))).ToList();
        double gates = bosses.Count(drop => drop.Items.Any(item => item.Template.InPool(HbrEquipmentPool.Boss) &&
            item.Template.Kind == HbrEquipmentKind.Bracelet)) / 3000d;
        double chains = bosses.Count(drop => drop.Items.Any(item => item.Template.InPool(HbrEquipmentPool.Boss) &&
            item.Template.Kind == HbrEquipmentKind.Necklace)) / 3000d;
        Check(Math.Abs(gates - HbrEquipmentGenerator.BossGatePercent / 100d) < 0.03, $"Act {tier + 1} gate bracelet rate {gates:P1}");
        Check(tier == 0 ? chains == 0d : Math.Abs(chains - HbrEquipmentGenerator.BossChainPercent / 100d) < 0.03,
            $"Act {tier + 1} ancient chain rate {chains:P1}");
        Check(extras.Where(item => item.Template.Kind == HbrEquipmentKind.Bracelet).All(item => item.Template.InTier(tier) &&
            item.Template.TierFrom == tier), $"Act {tier + 1} gate bracelets are that act's tier");
        Check(extras.Select(item => item.Template.Id).Distinct().Count() == (tier == 0 ? 15 : 25), $"Act {tier + 1} boss rows all appear");
    }
    Check(Enumerable.Range(0, 3000).SelectMany(i => HbrEquipmentGenerator.RollCombatDrop(HbrDropRoom.Normal, 2, false,
        HbrEquipmentRandom.ForKey(12, $"plain-{i}")).Items).All(item => !item.Template.InPool(HbrEquipmentPool.Boss)), "Boss rows never drop in normal fights");
});

Section("Earring curves, effect totals and the effect text", () =>
{
    // The curves come straight from the passive data: power[0] at hitRange[0], power[1] at hitRange[1].
    foreach (HbrEquipmentTemplate template in HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Earring).Where(template => template.Rarity == 6))
    {
        HbrEquipmentRollGroup group = template.RollGroups.Single(group => group.Effect.HasValue);
        JsonElement table = rows[template.Id].GetProperty("rolls")[0];
        for (int grade = 0; grade < group.Rows.Count; grade++)
        {
            JsonElement passive = table[grade].GetProperty("passive");
            decimal p0 = Percent(passive.GetProperty("power")[0].GetDecimal()), p1 = Percent(passive.GetProperty("power")[1].GetDecimal());
            decimal h0 = passive.GetProperty("hitRange")[0].GetDecimal(), h1 = passive.GetProperty("hitRange")[1].GetDecimal();
            foreach (int hits in new[] { 0, 1, 2, 4, 7, 9, 10, 15 })
            {
                decimal clamped = Math.Clamp(hits, Math.Min(h0, h1), Math.Max(h0, h1));
                decimal expected = p0 + (p1 - p0) * (clamped - h0) / (h1 - h0);
                decimal actual = HbrEquipmentRules.HitScaledPercent(group.Effect.Value, group.Rows[grade].Original, hits);
                Check(Math.Abs(actual - expected) < 0.000001m, $"{template.Id} grade {grade} at {hits} hits: {actual} = {expected}");
            }
        }
    }
    Equal(HbrEquipmentRules.HitScaledPercent(HbrEquipmentEffect.DpDamage, 15m, 10), 5m, "Exact floor at ten hits");
    Equal(HbrEquipmentRules.HitScaledPercent(HbrEquipmentEffect.OverdriveGain, 15m, 10), 15m, "Exact top at ten hits");
    Equal(HbrEquipmentRules.HitScaledPercent(HbrEquipmentEffect.DpDamage, 0m, 1), 0m, "No earring, no bonus");

    HbrEquipmentState state = Seeded();
    state = Put(state, "Acc.Ring006a_03", HbrEquipmentSlot.Ring);
    state = Put(state, "Acc.Earring006.F001", HbrEquipmentSlot.Earring, [2, 8, 8]);
    state = Put(state, "Acc.Bracelet006.F001", HbrEquipmentSlot.Bracelet, [8, 0]);
    state = Put(state, "Acc.Necklace006.F009", HbrEquipmentSlot.Necklace);
    HbrEquipmentEffectTotals totals = HbrEquipmentRules.EffectTotals(state);
    Equal(totals[HbrEquipmentEffect.AttackRoll], 17m, "炎之戒指 8% → attack rolls +17");
    Equal(totals[HbrEquipmentEffect.SkillRoll], 0m, "Fire favours attacks only");
    Equal(totals[HbrEquipmentEffect.DpDamage], 15m, "击碎耳坠 top grade 15%");
    Equal(totals[HbrEquipmentEffect.SkillDamage], 10m, "火耀远古项链 skill attack +10%");
    Equal(totals[HbrEquipmentEffect.DestructionFlat], 10m, "火耀远古项链 destruction +10%");
    Equal(totals[HbrEquipmentEffect.EnergyPerTurn], 1m, "Starting SP +3 → one energy a turn");
    Check(totals.Has(HbrEquipmentEffect.PerfectStrike), "炎击手环 turns Strikes into Perfected Strikes");
    Equal(default(HbrEquipmentEffectTotals)[HbrEquipmentEffect.EnergyPerTurn], 0m, "Empty totals read zero");
    Equal(HbrEquipmentRules.Totals(state), new HbrEquipmentBonus(77.7m, 77.7m, 100.8m, 94.5m, 60.9m, 0m, 2520m, 0m),
        "★6 fixed rows and both rolled tables scale by 2.1");
    HbrEquipmentItem earring = state.FindItem(state.At(HbrEquipmentSlot.Earring));
    Equal(HbrEquipmentRules.DisplayPlus(earring), 6, "Three top grades give +6");
    Equal(HbrEquipmentText.Name(earring), "击碎耳坠+6", "Name shows the table grades");
    Equal(HbrEquipmentRules.AccessoryLevel(state), 24 + (6 + 2) / 2, "Four ★6 plus half of +8");
    Equal(HbrEquipmentRules.OpenCharmSlots(state), 6, "Lv.28 opens every charm slot");
    Equal(HbrEquipmentText.ItemText(state.FindItem(state.At(HbrEquipmentSlot.Ring))), "力量、灵巧 +77.7 / 攻击牌出目 +17", "Ring text");
    Equal(HbrEquipmentText.ItemText(earring), "智慧 +100.8 / DP +2520 / 对DP技能攻击力最高 +15%（连击数越少效果越大）", "Earring text");
    Equal(HbrEquipmentText.ItemText(state.FindItem(state.At(HbrEquipmentSlot.Bracelet))),
        "体力 +94.5 / 精神 +60.9 / 战斗开始时，基础打击变为完美打击", "Bracelet text");
    Equal(HbrEquipmentText.ItemText(state.FindItem(state.At(HbrEquipmentSlot.Necklace))),
        "技能攻击力 +10% / 破坏率上升量 +10% / 每回合开始时获得 1 点能量", "Offensive chain text");
    Equal(HbrEquipmentText.ItemText(new HbrEquipmentItem { Template = "Acc.Necklace006.F010" }),
        "DP +630 / 受到的伤害减少 10% / 每回合开始时获得 1 点能量", "Defensive chain text");
    Equal(HbrEquipmentText.ItemText(new HbrEquipmentItem { Template = "Acc.Bracelet016c" }),
        "体力、精神 +79.8 / 受到Boss的伤害减少 15%", "Gate bracelet text (★5)");
    Equal(HbrEquipmentText.Describe(T("Acc.Earring006.F003")),
        "DP +840～2520 / 智慧 +67.2～100.8 / OD计量获取最高 +10～15%（连击数越多效果越大）", "Crafted earring range text");
    Equal(string.Join(" | ", HbrEquipmentText.EffectLines(totals)),
        "攻击牌出目 +17 | 对DP技能攻击力最高 +15%（连击数越少效果越大） | 技能攻击力 +10% | 破坏率上升量 +10% | " +
        "每回合开始时获得 1 点能量 | 战斗开始时，基础打击变为完美打击", "Loadout effect lines");
    Check(!T("Acc.Ring005a").HasEffects && T("Acc.Ring006a_00").HasEffects && T("Acc.Earring006.F004").HasEffects,
        "Only passive rows replace their flavor line");
    Equal(HbrEquipmentText.Tooltip(new HbrEquipmentItem { Template = "Acc.Ring005a" }), "秘银戒指\n提升攻击能力\n力量、灵巧 +45.6",
        "An ordinary tooltip keeps the official flavor line");
    Equal(HbrEquipmentText.Tooltip(new HbrEquipmentItem { Template = "Acc.Ring006a_00" }), "炎之戒指\n力量、灵巧 +77.7 / 攻击牌出目 +10",
        "A passive tooltip drops the original-effect line");
});

Section("Selling never pays back alchemy", () =>
{
    HbrEquipmentKind[] accessories = [HbrEquipmentKind.Ring, HbrEquipmentKind.Earring, HbrEquipmentKind.Bracelet, HbrEquipmentKind.Necklace];
    for (int tier = 0; tier < 3; tier++)
    {
        foreach (HbrEquipmentKind kind in accessories)
        {
            foreach (HbrEquipmentTemplate template in Enumerable.Range(0, 800).Select(i => HbrEquipmentGenerator.RollAccessory(kind, tier, false,
                         HbrEquipmentRandom.ForKey(31, $"sell-{tier}-{kind}-{i}"), HbrEquipmentPool.Alchemy).Template).Distinct())
            {
                int best = template.RollGroups.Sum(group => group.Rows.Max(row => row.Plus)) + template.PlusSlots * HbrEquipmentCatalog.MaxPlusLevel;
                var maxed = new HbrEquipmentItem
                {
                    Template = template.Id,
                    Rolls = template.RollGroups.Select(group => group.Rows.Count - 1).ToArray(),
                    Plus = Enumerable.Repeat(new HbrPlusSlot(HbrEquipmentStat.Dp, HbrEquipmentCatalog.MaxPlusLevel), template.PlusSlots).ToArray()
                };
                Equal(HbrEquipmentRules.DisplayPlus(maxed), best, $"{template.Id} best grade");
                Check(HbrEquipmentShop.SellPrice(maxed) < HbrEquipmentCatalog.AccessoryCraftPrice(tier),
                    $"Act {tier + 1} {template.Id} sells below its alchemy price");
            }
        }
        Check(HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Charm).Where(template => template.InTier(tier)).All(template =>
            HbrEquipmentShop.SellPrice(new HbrEquipmentItem { Template = template.Id, Rolls = template.RollGroups.Select(group => group.Rows.Count - 1).ToArray() }) <
            HbrEquipmentCatalog.CharmCraftPrice(tier)), $"Act {tier + 1} charms sell below their alchemy price");
    }
});

Section("Shop stock follows the original chapter lineups", () =>
{
    Equal(HbrEquipmentRules.TierForAct(-1), 0, "Tier floor");
    Equal(HbrEquipmentRules.TierForAct(5), 2, "Later acts use the last tier");
    var lineups = new[]
    {
        new[] { "0101", "0102", "0103", "0104", "0105", "0201", "0202", "0203", "0204", "0205" },
        new[] { "0301", "0302", "0303", "0304", "0305", "0306", "0401", "0402", "0403", "0404", "0405" },
        new[] { "0501", "0502", "0601", "0602", "0603", "0701", "0702" }
    };
    for (int tier = 0; tier < 3; tier++)
    {
        IReadOnlyList<HbrShopOffer> offers = HbrEquipmentShop.ShopOffers(tier);
        var boosters = offers.Where(offer => offer.Template?.Kind == HbrEquipmentKind.Booster)
            .Select(offer => offer.Template.Id.Substring("SBooster_".Length)).ToArray();
        Check(boosters.SequenceEqual(lineups[tier]), $"Tier {tier} boosters");
        Check(offers.Where(offer => offer.Template?.Kind == HbrEquipmentKind.Chip).All(offer => offer.Template.IsSold && offer.Template.InTier(tier)), $"Tier {tier} chips");
        Equal(offers.Count(offer => offer.Kind is HbrShopOfferKind.PlusRelease or HbrShopOfferKind.PlusChange), 2, "Both materials on sale");
        Equal(HbrEquipmentShop.AlchemyOffers(tier).Count, 5, "Alchemy for four accessories and charms");
    }
    Check(HbrEquipmentShop.ShopOffers(2).All(offer => offer.Template == null || offer.Template.Id != "SChip_5004"), "Artifact chips are not sold");
});

Section("Expected gear keeps each act's fights near the calibration targets", () =>
{
    // The model player of Calibration.cs, through the production drop, shop and
    // alchemy code. Targets for half of about 2000 gold spent on equipment:
    // normal fights about even, elites and bosses below. Spending all of it may
    // overshoot normal fights but keeps bosses at most a little above even.
    HbrEquipmentCalibration.ActResult[] half = HbrEquipmentCalibration.Run(1000, 0.5m);
    HbrEquipmentCalibration.ActResult[] all = HbrEquipmentCalibration.Run(1000, 1.0m);
    for (int act = 0; act < 3; act++)
    {
        (HbrEquipmentCalibration.ActResult h, HbrEquipmentCalibration.ActResult a) = (half[act], all[act]);
        Console.WriteLine($"     act {act + 1}, half the gold: first ×{h.First:0.00}, normal ×{h.Normal:0.00} (last ×{h.LastNormal:0.00}), " +
            $"elite ×{h.Elite:0.00}, boss ×{h.Boss:0.00} with gear +{h.BossGear:0}; all the gold: normal ×{a.Normal:0.00}, " +
            $"elite ×{a.Elite:0.00}, boss ×{a.Boss:0.00}");
        Check(h.Normal is >= 0.9 and <= 1.2, $"Act {act + 1} half gold: normal fights ×0.9–1.2 on average");
        Check(h.Elite is >= 0.65 and <= 0.95, $"Act {act + 1} half gold: elites ×0.65–0.95");
        Check(h.Boss is >= 0.6 and <= 0.85, $"Act {act + 1} half gold: boss ×0.6–0.85");
        Check(a.Boss <= 1.1, $"Act {act + 1} all gold: boss at most ×1.1");
        Check(a.Normal >= h.Normal && a.Boss >= h.Boss, $"Act {act + 1} more gold never means less gear");
    }
    Check(HbrEquipmentCalibration.Run(50, 0.5m).SequenceEqual(HbrEquipmentCalibration.Run(50, 0.5m)), "The calibration is deterministic");
});

Console.WriteLine($"OK {sections} sections, {checks} checks");

// Path of the mod checkout from the build output folder.
static string FindRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null && !File.Exists(Path.Combine(directory.FullName, "HBRGOSakiMod.csproj")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("HBRGOSakiMod.csproj not found above the build output");
}

static HbrEquipmentStat? MapType(string type) => type switch
{
    "Power" => HbrEquipmentStat.Strength,
    "Dexterity" => HbrEquipmentStat.Dexterity,
    "Wisdom" => HbrEquipmentStat.Intelligence,
    "Toughness" => HbrEquipmentStat.Vitality,
    "Spirit" => HbrEquipmentStat.Spirit,
    "Luck" => HbrEquipmentStat.Luck,
    "Dp" => HbrEquipmentStat.Dp,
    "CriticalRate" => HbrEquipmentStat.CriticalRate,
    _ => null
};

static decimal Percent(decimal ratio) => ratio * 100m;

// Hit-scaled earrings, by the passive's kind and target.
static HbrEquipmentEffect? HitEffect(JsonElement passive) => passive.GetProperty("skillType").GetString() switch
{
    "AttackUpRangeByHit" => passive.GetProperty("dp").GetDecimal() > 0m ? HbrEquipmentEffect.DpDamage : HbrEquipmentEffect.HpDamage,
    "OverDrivePointUpByHit" => HbrEquipmentEffect.OverdriveGain,
    "DamageRateUpByHit" => HbrEquipmentEffect.DestructionGain,
    _ => null
};

// The documented adaptations of fixed passives; the element skill-count bonus has no counterpart.
static HbrEquipmentEffectValue? FixedEffect(JsonElement passive)
{
    string type = passive.GetProperty("skillType").GetString();
    decimal power = passive.GetProperty("power")[0].GetDecimal();
    string[] elements = passive.GetProperty("elements").EnumerateArray().Select(element => element.GetString()).ToArray();
    return type switch
    {
        // Element rings: 4/5/6/8/10% → +10/12/13/17/20 roll points for the card type.
        "AttackUp" when elements.Length == 1 => new HbrEquipmentEffectValue(elements[0] switch
        {
            "Fire" or "Thunder" => HbrEquipmentEffect.AttackRoll,
            "Ice" or "Light" => HbrEquipmentEffect.SkillRoll,
            "Dark" => HbrEquipmentEffect.PowerRoll,
            _ => throw new InvalidOperationException("unmapped element " + elements[0])
        }, Math.Round(10m + (power - 0.04m) / 0.06m * 10m, MidpointRounding.AwayFromZero)),
        "AttackUp" => new HbrEquipmentEffectValue(HbrEquipmentEffect.SkillDamage, Percent(power)),
        "DamageRateUp" => new HbrEquipmentEffectValue(HbrEquipmentEffect.DestructionFlat, Percent(power)),
        "DefenseUp" => new HbrEquipmentEffectValue(HbrEquipmentEffect.DamageTaken, Percent(power)),
        // One gate boss at 25/50/75% becomes every boss at 5/10/15%.
        "DefenseUpFromEnemyGroup" => new HbrEquipmentEffectValue(HbrEquipmentEffect.BossDamageTaken, power * 20m),
        "SkillLimitCountUp" => null,
        _ => throw new InvalidOperationException("unmapped passive " + type)
    };
}

// The documented GP → gold mapping: 25 + 25·log10(GP/100), to the nearest 5.
static int GoldPrice(decimal gp) => gp <= 0m ? 0 :
    (int)(Math.Round((25d + 25d * Math.Log10((double)gp / 100d)) / 5d, MidpointRounding.AwayFromZero) * 5d);

