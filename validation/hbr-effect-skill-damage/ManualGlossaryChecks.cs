using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

// Manual glossary (2026-11-06, "补上"). The HBR texts of Eat Passersby, Pain Resonance and
// Hanging Shot read {DamageSkill}, which the glossary's formatter did not know, so the manual
// showed "（对应数值）%倍率". It now states the per-stack coefficient as their card-hover tips do.
// The other entries that read one live instance (2026-11-07, "补上") now state the rule:
// card amounts as "base（升级后 upgraded）", live state in words, assembled texts expanded.
static partial class EffectChecks
{
    const string GlossaryPlaceholder = "（对应数值）";
    const string ManualEnergyIcon = "[img=28x28]res://images/packed/sprite_fonts/colorless_energy_icon.png[/img]";
    static string ReportDirectory = "";

    static string GlossaryEntry(string table, string prefix, bool hbr)
    {
        Type entryType = Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrManualGlossaryEntry", true)!;
        var entries = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), entryType), StringComparer.Ordinal)!;
        Invoke(Helper("HbrManualGlossaryCatalog").GetMethod("AddEntry", F)!, null, entries,
            LocManager.Instance.GetTable(table), table, prefix, "HBR 能力", "", "", hbr);
        return (string)entryType.GetProperty("Description")!.GetValue(entries[table + ":" + prefix]!)!;
    }

    static string EntryText(object entry, string property) => (string)entry.GetType().GetProperty(property)!.GetValue(entry)!;

    // The whole manual as the production Build returns it, for comparing before and after.
    static void DumpGlossary(bool hbr, object[] entries)
    {
        if (string.IsNullOrEmpty(ReportDirectory)) return;
        Directory.CreateDirectory(ReportDirectory);
        var text = new System.Text.StringBuilder();
        foreach (object entry in entries.OrderBy(entry => EntryText(entry, "Id"), StringComparer.Ordinal))
            text.Append("### ").Append(EntryText(entry, "Id")).Append(" | ").Append(EntryText(entry, "Title"))
                .Append(" | ").Append(EntryText(entry, "Category")).Append('\n')
                .Append(EntryText(entry, "Description")).Append("\n\n");
        File.WriteAllText(Path.Combine(ReportDirectory, hbr ? "manual-glossary-hbr.txt" : "manual-glossary-native.txt"),
            text.ToString(), new System.Text.UTF8Encoding(false));
    }

    // Every manual entry that still shows the placeholder, with the variables that produce it.
    static object[] VagueGlossaryEntries(bool hbr, Dictionary<string, string> descriptions)
    {
        InjectGlossaryModels();
        var entries = ((System.Collections.IEnumerable)Call("HbrManualGlossaryCatalog", "Build", hbr)!).Cast<object>().ToArray();
        DumpGlossary(hbr, entries);
        foreach (object entry in entries) descriptions[EntryText(entry, "Id")] = EntryText(entry, "Description");
        Cases.Add(new { source = "Manual glossary scope", hbr, entries = entries.Length,
            tables = entries.GroupBy(entry => EntryText(entry, "Id").Split(':')[0])
                .ToDictionary(group => group.Key, group => group.Count()) });
        var vague = new List<object>();
        foreach (object entry in entries)
        {
            string id = EntryText(entry, "Id");
            string description = EntryText(entry, "Description");
            if (!description.Contains(GlossaryPlaceholder)) continue;
            string table = id[..id.IndexOf(':')], prefix = id[(id.IndexOf(':') + 1)..];
            LocTable loc = LocManager.Instance.GetTable(table);
            string key = hbr && loc.HasEntry(prefix + ".hbrDescription") ? prefix + ".hbrDescription" : prefix + ".description";
            string raw = loc.HasEntry(key) ? loc.GetRawText(key) : "";
            string[] unknown = System.Text.RegularExpressions.Regex.Matches(raw, @"\{([^{}]*)\}").Select(match => match.Value)
                .Where(variable => ((string)Call("HbrManualGlossaryPresentation", "FormatDescription", variable, id)!).StartsWith(GlossaryPlaceholder))
                .Distinct().ToArray();
            vague.Add(new { id, key, unknown });
        }
        return vague.ToArray();
    }

    static void InjectGlossaryModels()
    {
        foreach (Type type in ((IEnumerable<Type>)Helper("HbrManualGlossaryDiscovery").GetProperty("ReferencedPowerTypes", F)!.GetValue(null)!)
                     .Concat(new[] { typeof(Overclock), typeof(DeflectorProtection), typeof(MomentOfReign), typeof(Millsage),
                         typeof(HotaruIridescence), typeof(HotaruElectronicSoldierManual) }))
            if (!ModelDb.Contains(type))
            {
                ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
    }

    // The 27 powers whose manual entry showed the placeholder on 2026-11-06 (HBR 24, native 24).
    static readonly Type[] PlaceholderPowers =
    {
        typeof(HotaruBreathingShelterPower), typeof(FragilePower), typeof(HotaruLingeringEchoPower), typeof(HbrDefenseDownPower),
        typeof(HotaruFarewellInertiaPower), typeof(HbrAttackDownPower), typeof(GhostStatePower), typeof(HotaruSeeYouAgainPower),
        typeof(HotaruBreathingRhythmPower), typeof(SaviorGrandEntrancePower), typeof(HotaruUntilDawnPower), typeof(DestructionRatePower),
        typeof(NararaLayeredDpPower), typeof(HotaruSixSenseRemnantsPower), typeof(HotaruUnfadingScarsPower), typeof(BoostUpFormDamagePower),
        typeof(HotaruWakingCommandPower), typeof(HotaruTraceOldScarPower), typeof(HotaruTrailingSilencePower), typeof(HbrCalamityPower),
        typeof(HotaruMyCoffinPower), typeof(HotaruOnlyThisBeatPower), typeof(HotaruMyHappinessPower), typeof(ReptureBowPower),
        typeof(HotaruBreathAfterMissPower), typeof(FatePower), typeof(UnbrokenRemnantPower)
    };

    // A power's hover without an instance, as the game builds it for a card's hover tips.
    static string CanonicalHover(Type type)
    {
        try
        {
            PowerModel model = ModelDb.GetById<PowerModel>(ModelDb.GetId(type));
            LocString text = model.Description;
            Invoke(AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), model, text, null);
            return text.GetFormattedText();
        }
        catch (Exception error) { return "ERROR " + error.GetType().Name + ": " + error.Message; }
    }

    static string OwnedHover(PowerModel power)
    {
        try
        {
            LocString text = power.Description;
            Invoke(AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), power, text, null);
            return text.GetFormattedText();
        }
        catch (Exception error) { return "ERROR " + error.GetType().Name + ": " + error.Message; }
    }

    static string CardFace(CardModel card)
    {
        try { LocString text = card.Description; card.DynamicVars.AddTo(text); return text.GetFormattedText(); }
        catch (Exception error) { return "ERROR " + error.GetType().Name + ": " + error.Message; }
    }

    // A card's DynamicVar before and after its upgrade, read from the real model.
    static (decimal Base, decimal Upgraded) CardValue<T>(string name) where T : CardModel, new()
    {
        T card = Owned<T>(), upgraded = Owned<T>();
        upgraded.UpgradeInternal(); upgraded.FinalizeUpgradeInternal();
        return (card.DynamicVars[name].BaseValue, upgraded.DynamicVars[name].BaseValue);
    }

    static void CardAmount<T>(string name, decimal expected, decimal upgraded, string wording) where T : CardModel, new()
    {
        var value = CardValue<T>(name);
        Cases.Add(new { source = "Manual glossary card amount", card = typeof(T).Name, name, value.Base, value.Upgraded });
        Check(value.Base == expected && value.Upgraded == upgraded,
            $"{typeof(T).Name}.{name} is {expected} (upgraded {upgraded}), as the manual writes {wording}: {value.Base}/{value.Upgraded}");
    }

    // Recorded hovers and settlements the manual wording is checked against.
    static void GlossaryProbeCases()
    {
        InjectGlossaryModels();
        foreach (bool hbr in new[] { true, false })
        {
            H("Mode", hbr, 1);
            Cases.Add(new { source = "Manual glossary canonical hovers", hbr,
                hovers = PlaceholderPowers.ToDictionary(type => ModelDb.GetId(type).Entry, CanonicalHover) });
        }
        H("Mode", true, 1);

        // Breathing Shelter: the power stores the card's native block units and gains them as powered block.
        Reset();
        var shelter = Power<HotaruBreathingShelterPower>(Hero, 1);
        Set(shelter, "<BlockReward>k__BackingField", 12); Set(shelter, "<ExtraActions>k__BackingField", 1);
        string shelterHbr = OwnedHover(shelter);
        decimal shelterBlockHbr = (decimal)H("Block", Hero, 12m, ValueProp.Move, null)!;
        string shelterCardHbr = CardFace(Owned<HotaruBreathingShelter>());
        H("Mode", false, 1);
        string shelterNative = OwnedHover(shelter);
        decimal shelterBlockNative = (decimal)H("Block", Hero, 12m, ValueProp.Move, null)!;
        H("Mode", true, 1);
        Cases.Add(new { source = "Manual glossary probe: Breathing Shelter", shelterHbr, shelterBlockHbr, shelterCardHbr,
            shelterNative, shelterBlockNative });
        Check(shelterHbr.Contains("获得12点格挡") && shelterBlockHbr == 1200m && shelterCardHbr.Contains("获得1200点"),
            "HBR Breathing Shelter hover states 12 block while the 12 powered units settle as 1200 (stays out of the manual change)");
        Check(shelterNative.Contains("额外行动1次") && shelterNative.Contains("获得12点格挡") && shelterBlockNative == 12m,
            "native Breathing Shelter gains 12 block and one extra action, as the native manual now writes");

        // Six Sense Remnants: one native growth unit for both cannon ledgers.
        Reset();
        var remnants = Power<HotaruSixSenseRemnantsPower>(Hero, 1);
        Set(remnants, "<GrowthReward>k__BackingField", 1); Set(remnants, "<ChargeReward>k__BackingField", 1);
        string remnantsHbr = OwnedHover(remnants), remnantsCardHbr = CardFace(Owned<HotaruSixSenseRemnants>());
        H("Mode", false, 1);
        string remnantsNative = OwnedHover(remnants), remnantsCardNative = CardFace(Owned<HotaruSixSenseRemnants>());
        H("Mode", true, 1);
        Cases.Add(new { source = "Manual glossary probe: Six Sense Remnants", remnantsHbr, remnantsCardHbr, remnantsNative, remnantsCardNative });
        Check(remnantsHbr.Contains("各增加1。") && remnantsCardHbr.Contains("伤害增加1000点") && remnantsCardHbr.Contains("格挡增加100点") &&
            !remnantsCardHbr.Contains("基础伤害") && !remnantsCardHbr.Contains("基础格挡"),
            "67 Six Sense Remnants ordinary face preserves actual1000 damage/100 block growth without internal base-damage terminology");

        // Fixed amounts converted by mode, read from the owned powers' real descriptions.
        Reset();
        var fate = Power<FatePower>(Hero, 1); var unbroken = Power<UnbrokenRemnantPower>(Hero, 10);
        string fateHbr = OwnedHover(fate), unbrokenHbr = OwnedHover(unbroken);
        H("Mode", false, 1);
        string fateNative = OwnedHover(fate), unbrokenNative = OwnedHover(unbroken);
        H("Mode", true, 1);
        Cases.Add(new { source = "Manual glossary probe: fixed amounts", fateHbr, fateNative, unbrokenHbr, unbrokenNative });
        Check(fateNative.Contains("层数×3点最大生命值") && fateHbr.Contains("层数×300点最大生命值"),
            "Fate loses stacks×3 max HP natively and stacks×300 under HBR");
        Check(unbrokenNative.Contains("恢复10点DP") && unbrokenHbr.Contains("恢复1000点DP"),
            "Unbroken Remnant restores its stacks natively and stacks×100 under HBR");

        // Rapture Bow's follow-up: one native unit through the attack's own damage pipeline, so the
        // bow's own multipliers apply in both modes; only the unit's conversion differs.
        Reset();
        var bow = (ReptureBowPower)ModelDb.Power<ReptureBowPower>().ToMutable();
        Set(bow, "_owner", Hero); Set(bow, "_amount", 1);
        ((List<PowerModel>)H("Powers", Hero)!).Add(bow);
        MethodInfo follow = typeof(ReptureBowPower).GetMethod("CalculateExtraDpDamage", F)!;
        decimal followHbr = (decimal)Invoke(follow, null, Enemy, ValueProp.Move, Hero, null)!;
        H("Mode", false, 1);
        decimal followNative = (decimal)Invoke(follow, null, Enemy, ValueProp.Move, Hero, null)!;
        H("Mode", true, 1);
        decimal skill = 1000m * (1m + (100m - 150m) / 140m);
        Cases.Add(new { source = "Manual glossary probe: Rapture Bow follow-up", followHbr, followNative, hbrUnit = skill });
        Check(followNative == 1m && followHbr == decimal.Floor(skill),
            $"HBR Rapture Bow follow-up resolves the one unit as a 16.67% skill ({followHbr} vs native {followNative})");

        // Native Sea Breeze (Savior effect 5) doubles Vulnerable's bonus when the damage resolves.
        Reset(); H("Mode", false, 1);
        Power<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>(Enemy, 1);
        decimal vulnerable = (decimal)H("Preview", Enemy, Hero, 10m, ValueProp.Move, null)!;
        var savior = Power<SaviorGrandEntrancePower>(Hero, 1);
        Set(savior, "<UpgradeEffects>k__BackingField", new[] { 5 });
        decimal seaBreeze = (decimal)H("Preview", Enemy, Hero, 10m, ValueProp.Move, null)!;
        H("Mode", true, 1);
        Cases.Add(new { source = "Manual glossary probe: native Sea Breeze", vulnerable, seaBreeze });
        Check(vulnerable == 15m && seaBreeze == 20m,
            $"native Sea Breeze doubles an existing Vulnerable's bonus at damage time: 10 → {vulnerable} → {seaBreeze}");
        // HBR Sea Breeze doubles the base value of a defense down being applied, before the stats.
        Reset();
        PowerModel defenseDown = ModelDb.Power<HbrDefenseDownPower>();
        decimal appliedPlain = (decimal)Call("HbrPercentDebuffHelper", "ResolvePercent", defenseDown, 1m, Hero, Enemy)!;
        Set(Power<SaviorGrandEntrancePower>(Hero, 1), "<UpgradeEffects>k__BackingField", new[] { 5 });
        decimal appliedSeaBreeze = (decimal)Call("HbrPercentDebuffHelper", "ResolvePercent", defenseDown, 1m, Hero, Enemy)!;
        Cases.Add(new { source = "Manual glossary probe: HBR Sea Breeze", appliedPlain, appliedSeaBreeze });
        Check(appliedPlain > 0m && appliedSeaBreeze == appliedPlain * 2m,
            $"HBR Sea Breeze doubles the applied defense down base value: {appliedPlain}% → {appliedSeaBreeze}%");
        Reset();

        // Card amounts the manual writes, from the real cards before and after their upgrade.
        CardAmount<HotaruBreathingShelter>("BlockReward", 12m, 12m, "12");
        CardAmount<HotaruBreathingShelter>("ExtraActions", 1m, 1m, "1");
        CardAmount<HotaruSixSenseRemnants>("Charge", 1m, 1m, "1");
        CardAmount<HotaruSixSenseRemnants>("BaseGain", 1m, 2m, "1（升级后2）");
        CardAmount<HotaruUnfadingScars>("EffectPercent", 0m, 25m, "0（升级后25）");
        CardAmount<HotaruUntilDawn>("Energy", 2m, 3m, "two icons (upgraded three)");
        CardAmount<HotaruWakingCommand>("Energy", 1m, 1m, "one icon");
        CardAmount<HotaruWakingCommand>("Activations", 1m, 1m, "1");
        CardAmount<HotaruTrailingSilence>("CostReduction", 1m, 1m, "1");
        CardAmount<HotaruOnlyThisBeat>("RollPercent", 20m, 20m, "20");
        CardAmount<HotaruBreathingRhythm>("Attacks", 3m, 3m, "3 uses");
        CardAmount<HotaruBreathingRhythm>("Block", 4m, 6m, "4（升级后6）");
        CardAmount<HotaruTraceOldScar>("CostReduction", 1m, 1m, "1 per choice");
        CardAmount<HotaruLowEcho>("RollBonus", 40m, 40m, "40 points");
        CardAmount<HotaruFarewellInertia>("BaseCharge", 1m, 1m, "1");
        CardAmount<HotaruFarewellInertia>("CriticalLimit", 2m, 3m, "2（升级后3）");
        CardAmount<HotaruFarewellInertia>("ExtraActions", 1m, 1m, "1");
        CardAmount<HotaruBreathAfterMiss>("MissLimit", 2m, 3m, "2（升级后3）");
        CardAmount<HotaruBreathAfterMiss>("Block", 4m, 6m, "4（升级后6）");
        CardAmount<HotaruBreathAfterMiss>("ChargePower", 1m, 1m, "1");
        CardAmount<HotaruBreathAfterMiss>("Cards", 1m, 1m, "1");
        CardAmount<HotaruMyCoffin>("SacrificePercent", 100m, 200m, "100（升级后200）");
        CardAmount<HotaruMyCoffin>("RedemptionPercent", 50m, 100m, "50（升级后100）");
    }

    static void ManualGlossaryCases()
    {
        Localization();
        var targets = new[] { typeof(EatPassersbyPower), typeof(PainResonancePower), typeof(HangingShotPower) }
            .Select(type => ModelDb.GetId(type).Entry)
            .Select(prefix => (prefix, hbr: GlossaryEntry("powers", prefix, true), native: GlossaryEntry("powers", prefix, false))).ToArray();
        foreach (var target in targets) Cases.Add(new { source = "Manual glossary", target.prefix, target.hbr, target.native });
        object[] vagueHbr, vagueNative;
        var hbrManual = new Dictionary<string, string>(StringComparer.Ordinal);
        var nativeManual = new Dictionary<string, string>(StringComparer.Ordinal);
        try { vagueHbr = VagueGlossaryEntries(true, hbrManual); vagueNative = VagueGlossaryEntries(false, nativeManual); }
        catch (Exception error) { Cases.Add(new { source = "Manual glossary build", error = error.ToString() }); throw; }
        Cases.Add(new { source = "Manual glossary placeholders", hbr = vagueHbr, native = vagueNative });
        try { GlossaryProbeCases(); }
        catch (Exception error) { Cases.Add(new { source = "Manual glossary probe", error = error.ToString() }); throw; }

        foreach (var (prefix, hbr, native) in targets)
        {
            Check(hbr.Contains("（层数×16.67）%倍率") && !hbr.Contains(GlossaryPlaceholder) && !hbr.Contains("具体数值由施加此效果"),
                $"the manual's HBR {prefix} entry states the per-stack coefficient: {hbr}");
            Check(!native.Contains("倍率") && !native.Contains("层数×16.67"), $"the manual's legacy {prefix} entry keeps its native text");
            Check(vagueHbr.All(entry => (string)entry.GetType().GetProperty("id")!.GetValue(entry)! != "powers:" + prefix),
                $"the real HBR glossary build no longer lists {prefix} with a placeholder");
        }

        // Only the entries whose hover and settlement disagree keep the placeholder.
        string[] Ids(object[] vague) => vague.Select(entry => (string)entry.GetType().GetProperty("id")!.GetValue(entry)!).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Check(Ids(vagueHbr).SequenceEqual(new[] { "powers:HOTARU_BREATHING_SHELTER_POWER", "powers:HOTARU_MY_HAPPINESS_POWER", "powers:HOTARU_SIX_SENSE_REMNANTS_POWER" }),
            "the HBR manual keeps the placeholder only for Breathing Shelter, Six Sense Remnants and My Happiness: " + string.Join(",", Ids(vagueHbr)));
        Check(Ids(vagueNative).SequenceEqual(new[] { "powers:HOTARU_MY_HAPPINESS_POWER", "powers:SAVIOR_GRAND_ENTRANCE_POWER" }),
            "the native manual keeps the placeholder only for My Happiness and Savior: " + string.Join(",", Ids(vagueNative)));
        foreach ((bool hbr, Dictionary<string, string> manual) in new[] { (true, hbrManual), (false, nativeManual) })
        {
            string[] raw = manual.Where(pair => pair.Value.Contains('{') || pair.Value.Contains('}')).Select(pair => pair.Key).ToArray();
            Check(raw.Length == 0, $"no {(hbr ? "HBR" : "native")} manual entry shows a raw variable: {string.Join(",", raw)}");
        }

        string Entry(bool hbr, string prefix) => (hbr ? hbrManual : nativeManual)["powers:" + prefix];
        string Raw(string key) => LocManager.Instance.GetTable("powers").GetRawText(key).Trim();
        void Has(bool hbr, string prefix, params string[] parts)
        {
            string text = Entry(hbr, prefix);
            foreach (string part in parts)
                Check(text.Contains(part), $"{(hbr ? "HBR" : "native")} manual {prefix} reads 「{part}」: {text}");
        }
        string Icons(int count) => string.Concat(Enumerable.Repeat(ManualEnergyIcon, count));

        // Static rule texts that the canonical hovers already show.
        foreach (bool hbr in new[] { true, false })
        {
            Check(Entry(hbr, "HBR_ATTACK_DOWN_POWER") == Raw("HBR_ATTACK_DOWN_POWER.hbrReferenceDescription") &&
                  Entry(hbr, "HBR_DEFENSE_DOWN_POWER") == Raw("HBR_DEFENSE_DOWN_POWER.hbrReferenceDescription"),
                $"{(hbr ? "HBR" : "native")} manual attack/defense down read their reference rule, as their canonical hovers do");
            Check(Entry(hbr, "BOOST_UP_FORM_DAMAGE_POWER").StartsWith(Raw("BOOST_UP_FORM_DAMAGE_POWER.canonicalDescription")),
                $"{(hbr ? "HBR" : "native")} manual Boost Up form damage reads its canonical rule");
            Has(hbr, "BOOST_UP_FORM_DAMAGE_POWER", "「新星肅清」（每次+50%）", "「祈禱之花，託付風中」（每次+25%）");
            Has(hbr, "HOTARU_LINGERING_ECHO_POWER", "下张牌的最终出目增加（待结算的点数）。", "原始出目不高于30时给予40点");
            Has(hbr, "HOTARU_FAREWELL_INERTIA_POWER", "仅统计每张手动牌的起手骰。\n每份效果：回合开始获得“1＋永久退役浮游炮数”层充能。每回合前2（升级后3）次大成功后，使一门浮游炮下次完整行动额外行动1次。");
            Has(hbr, "HOTARU_SEE_YOU_AGAIN_POWER", "全部效果变为[blue]2^N[/blue]倍", "当前[blue]N[/blue]层", "额外生效[blue]2×(2^N−1)[/blue]点敏捷", "N 表示该能力的层数或累计数值。", "「战斗开始时获得2层敏捷」");
            Has(hbr, "HOTARU_UNTIL_DAWN_POWER", "下回合开始时，获得" + Icons(2) + "（升级后" + Icons(3) + "）。", "多次打出时，数值相加。");
            Has(hbr, "NARARA_LAYERED_DP_POWER", "剩余[blue]（剩余条数）[/blue]条DP");
            Has(hbr, "HOTARU_UNFADING_SCARS_POWER", "最终效果提高0（升级后25）%（乘算）。");
            Has(hbr, "HOTARU_WAKING_COMMAND_POWER", "下回合开始，获得" + ManualEnergyIcon + "，并选择当时现存的浮游炮，激发共1次。");
            Has(hbr, "HOTARU_TRACE_OLD_SCAR_POWER", "牌型的费用降低（最低为0）：（循着同一道刻痕所选的牌型，每次降低1）。");
            Has(hbr, "HOTARU_TRAILING_SILENCE_POWER", "技能牌费用降低1，最低为0。", "同一回合多次获得时，数值相加。");
            Has(hbr, "HBR_CALAMITY_POWER", "剩余（剩余回合数）个敌方回合", "敌方回合结束时减1", "每次施加独立保留", "不限制份数", "数值相加");
            Has(hbr, "HOTARU_MY_COFFIN_POWER", "棺木每次行动额外行动N次；", "所得格挡的（100×N）%DP伤害。", "其中（升级版的层数）层为升级版",
                "\n\n[b]选择牺牲后[/b]\n小萤本体每次实际获得格挡时，对当前生命值最高的敌人造成所得格挡100（升级后200）%的DP伤害",
                "\n\n[b]选择救赎后[/b]\n你的浮游炮每次实际造成伤害后，小萤本体获得等于该次实际伤害50（升级后100）%的[gold]格挡[/gold]");
            Has(hbr, "HOTARU_ONLY_THIS_BEAT_POWER", "本回合最终出目的百分比加成增加20%。");
            Has(hbr, "REPTURE_BOW_POWER", hbr ? "额外造成1次16.67%倍率[gold]DP伤害[/gold]" : "额外造成1次1点[gold]DP伤害[/gold]");
        }

        // HBR-only and mode-specific wording.
        Check(Entry(true, "FRAGILE_POWER") == Raw("FRAGILE_POWER.hbrReferenceDescription"), "HBR manual Fragile reads its reference rule");
        Check(Entry(false, "FRAGILE_POWER") == Raw("FRAGILE_POWER.description"), "native manual Fragile keeps its native rule");
        Has(true, "SAVIOR_GRAND_ENTRANCE_POWER", "击晕目标[blue]1[/blue]回合。", "眩晕之刃：", "极限冲击：",
            "全力挥砍：", "坚硬之刃：", "音乐排行榜上的一闪：", "海风高压电：", "最高潮！青春登台：");
        Check(!Entry(true, "SAVIOR_GRAND_ENTRANCE_POWER").Contains("神乐净火"), "Savior's HBR reference excludes the deleted DP follow-up");
        Check(!Entry(true, "SAVIOR_GRAND_ENTRANCE_POWER").Contains("闪光"), "Savior's HBR manual leaves out the cost-only effect, as its hover does");
        Has(true, "GHOST_STATE_POWER", "基础技能系数增加6‰");
        Has(true, "HOTARU_BREATHING_RHYTHM_POWER", "本回合接下来（剩余次数）次手动打出攻击牌时，获得（20×B）%倍率", "呼吸间的节拍每次给予3次。");
        Has(false, "HOTARU_BREATHING_RHYTHM_POWER", "本回合接下来（剩余次数）次手动打出攻击牌时，获得4（升级后6）点[gold]格挡[/gold]。", "格挡相加，次数分别计算。");
        Has(true, "DESTRUCTION_RATE_POWER", "当前：[blue]（当前破坏率）%[/blue]；上限：[blue]（当前上限）%[/blue]。");
        Has(false, "HOTARU_BREATHING_SHELTER_POWER", "额外行动1次。每回合首次受到此类敌人的攻击前，获得12点格挡。", "多次打出时，数值相加。");
        Has(false, "HOTARU_SIX_SENSE_REMNANTS_POWER", "获得1层充能，并使所有浮游炮的基础伤害与基础格挡各增加1（升级后2）。");
        Has(false, "HOTARU_BREATH_AFTER_MISS_POWER", "仅统计每张手动牌的起手骰。\n每份效果：每回合前2（升级后3）次未大成功，各获得1层充能和4（升级后6）点格挡；首次大成功时，抽1张牌。");
        Has(false, "FATE_POWER", "每次出牌使主目标失去层数×3点最大生命值。");
        Has(false, "UNBROKEN_REMNANT_POWER", "为其恢复（层数）点DP");
        Has(true, "FATE_POWER", "层数×300点最大生命值");
        Has(true, "UNBROKEN_REMNANT_POWER", "（层数×100）点DP");
    }
}
