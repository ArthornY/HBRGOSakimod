using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Dodge retains its independent HBR evasion scale. Requirement28 replaces the
// HBR 31A bond entirely; legacy-mode saves retain their previous native bond.
static partial class EffectChecks
{
    static bool DodgeIsolated;
    static int ThirtyOneACount;
    static bool FixedThirtyOneACount(ref int __result) { __result = ThirtyOneACount; return false; }

    static void IsolateDodge()
    {
        if (DodgeIsolated) return;
        var harmony = new Harmony(ProductionId + ".dodge");
        foreach (string name in new[] { "DodgeVisibleBlockPatch", "DodgeVisibleBlockChangedPatch", "DodgeBlockGainPatch",
                     "DodgeBlockLossPatch", "DodgeBlockDamagePatch", "DodgePowerAmountPatch", "DodgePowerApplyPatch", "DodgePowerRemovePatch" })
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "installs production " + name);
        // The collection counts distinct 31A cards in the permanent deck; this host fixes the count.
        harmony.Patch(Mod.GetType("HBRGOSakiMod.Core.Models.Capabilities.ThirtyOneAKeywordRules", true)!.GetMethod("GetPermanentDistinctCount", F)!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FixedThirtyOneACount), F)!));
        // Block conversion and the stat cases create powers from their canonical models, and the
        // relic's own hover tip reads its canonical model; this host registers them on demand.
        foreach (Type type in new[] { typeof(DodgePower), typeof(StrengthPower), typeof(DexterityPower), typeof(ThirtyOneACollection) })
            if (!ModelDb.Contains(type))
            {
                ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
        DodgeIsolated = true;
    }

    static ThirtyOneACollection OwnedCollection()
    {
        var relic = (ThirtyOneACollection)ModelDb.GetById<RelicModel>(ModelDb.GetId(typeof(ThirtyOneACollection))).ToMutable();
        Set(relic, "_owner", Player);
        return relic;
    }

    static ThirtyOneACollection DodgeSetup(bool hbr, int kinds)
    {
        Reset(hbr);
        ThirtyOneACount = kinds;
        var relic = OwnedCollection();
        Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual"), relic });
        SetNativeBlock(0);
        return relic;
    }

    static FieldInfo NativeBlockField => typeof(Creature).GetField("_block", F)!;
    static void SetNativeBlock(int block) => NativeBlockField.SetValue(Hero, block);
    static int NativeBlock => (int)NativeBlockField.GetValue(Hero)!;
    static int DodgeLayers => Hero.GetPower<DodgePower>()?.Amount ?? 0;
    static int DodgeStats => (int)Call("DodgeCombatHelper", "GetStatBonus", Hero)!;
    static decimal HeroStat(string name) { object stats = H("Stats", Hero)!; return (decimal)stats.GetType().GetProperty(name, F)!.GetValue(stats)!; }
    static decimal HeroBlock(ValueProp props = ValueProp.Move) => (decimal)H("Block", Hero, 5m, props, null)!;

    // Fills a native method's arguments by type: the amount, the damage flags, defaults elsewhere.
    static object? NativeAmount(object model, string method, decimal amount, ValueProp props, out string signature)
    {
        MethodInfo info = model.GetType().GetMethods(F).Where(m => m.Name == method).OrderByDescending(m => m.GetParameters().Length).First();
        signature = method + "(" + string.Join(", ", info.GetParameters().Select(p => p.ParameterType.Name)) + ")";
        object?[] args = info.GetParameters().Select(p => p.ParameterType == typeof(decimal) ? amount
            : p.ParameterType == typeof(ValueProp) ? props
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        return Invoke(info, model, args);
    }

    static async Task DodgeCases()
    {
        IsolateDodge();
        CombatInProgressWithTurn();
        try { await DodgeMechanicsCases(); await DodgeStatCases(); }
        finally { CombatInProgress(false); ThirtyOneACount = 0; }
    }

    static async Task DodgeMechanicsCases()
    {
        string gainSignature = "", damageSignature = "";
        async Task<(int dodge, int block)> Gain(bool hbr, int kinds, decimal amount, int start = 0)
        {
            DodgeSetup(hbr, kinds);
            if (start > 0) Power<DodgePower>(Hero, start);
            if (NativeAmount(Hero, "GainBlockInternal", amount, ValueProp.Move, out gainSignature) is Task task) await task;
            Cases.Add(new { source = "Dodge block gain", hbr, kinds, amount, start, dodge = DodgeLayers, block = NativeBlock });
            return (DodgeLayers, NativeBlock);
        }
        async Task<(bool dodged, int left, decimal absorbed, bool removed)> Hit(bool hbr, int layers, decimal damage)
        {
            DodgeSetup(hbr, 2);
            Power<DodgePower>(Hero, layers);
            object? result = NativeAmount(Hero, "DamageBlockInternal", damage, ValueProp.Move, out damageSignature);
            if (result is Task task) { await task; result = task.GetType().GetProperty("Result")?.GetValue(task); }
            decimal absorbed = result is decimal value ? value : 0m;
            bool dodged = absorbed == damage && DodgeLayers < layers, removed = Hero.GetPower<DodgePower>() == null;
            Cases.Add(new { source = "Dodge hit", hbr, layers, damage, dodged, left = DodgeLayers, absorbed, removed });
            return (dodged, DodgeLayers, absorbed, removed);
        }
        int Visible(bool hbr, int layers, int native)
        {
            DodgeSetup(hbr, 4);
            Power<DodgePower>(Hero, layers);
            SetNativeBlock(native);
            int visible = Hero.Block;
            Cases.Add(new { source = "Dodge counted as Block", hbr, layers, native, visible });
            return visible;
        }
        async Task<int> TurnStart(bool hbr, int kinds, int layers)
        {
            DodgeSetup(hbr, kinds);
            var power = Power<DodgePower>(Hero, layers);
            await (Task)Native(power, "BeforeSideTurnStart", new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero }, Combat)!;
            Cases.Add(new { source = "Dodge turn start", hbr, kinds, layers, after = DodgeLayers });
            return DodgeLayers;
        }

        // Real values first; every case below is recorded before any assertion.
        decimal hbrBlock = (decimal)Call("HbrSkillPower", "EvaluateBlockFromUnits", 5m, 100m, 100m)!;
        DodgeSetup(true, 2);
        decimal hbrHit = Actual(Hero, Enemy, 6m, ValueProp.Move, null);
        Cases.Add(new { source = "Dodge HBR references", hbrBlock, hbrHit });
        int need = (int)decimal.Ceiling(hbrHit / 100m);
        var gain = await Gain(true, 2, hbrBlock);
        var gainNearCap = await Gain(true, 2, hbrBlock, start: 39);
        var gainAtCap = await Gain(true, 2, hbrBlock, start: 40);
        var gainTiny = await Gain(true, 2, 99m);
        var gainOneKind = await Gain(true, 1, hbrBlock);
        var gainNative = await Gain(false, 2, 5m);
        var hitFull = await Hit(true, 40, hbrHit);
        var hitShort = await Hit(true, need - 1, hbrHit);
        var hitExact = await Hit(true, need, hbrHit);
        var hitTiny = await Hit(true, 40, 30m);
        var hitNative = await Hit(false, 40, 30m);
        var hitNativeShort = await Hit(false, 29, 30m);
        var hitNativeHalf = await Hit(false, 23, 22.5m);
        // Dodge from elsewhere (Hotaru's cannon upgrade) has no 31A relic and evades the same way.
        Reset(true);
        Power<DodgePower>(Hero, need);
        object? plainResult = NativeAmount(Hero, "DamageBlockInternal", hbrHit, ValueProp.Move, out _);
        bool relicFreeDodged = plainResult is decimal plainAbsorbed && plainAbsorbed == hbrHit && Hero.GetPower<DodgePower>() == null;
        Cases.Add(new { source = "Dodge hit without 31A", layers = need, damage = hbrHit, dodged = relicFreeDodged });
        int visible = Visible(true, 40, 300), visibleNative = Visible(false, 40, 3);
        int[] kept = { await TurnStart(true, 2, 40), await TurnStart(true, 3, 40), await TurnStart(true, 4, 40),
            await TurnStart(true, 6, 80), await TurnStart(false, 3, 40), await TurnStart(false, 4, 40) };
        Cases.Add(new { source = "Dodge native signatures", gainSignature, damageSignature });

        // HBR's new bond no longer converts Block. Legacy native behavior stays intact.
        Equal(hbrBlock, 500m, "reference HBR Block: native5 at Intelligence 100 through the production block formula");
        Check(gain == (0, 500), $"HBR Block500 remains Block with two 31A kinds: {gain}");
        Check(gainNearCap == (39, 500), $"HBR Block no longer changes existing Dodge near the cap: {gainNearCap}");
        Check(gainAtCap == (40, 500), $"at the cap HBR Block is gained normally: {gainAtCap}");
        Check(gainTiny == (0, 99), $"HBR Block99 is retained without conversion loss: {gainTiny}");
        Check(gainOneKind == (0, 500), $"one kind keeps HBR Block: {gainOneKind}");
        Check(gainNative == (5, 0), $"disabled HBR turns native5 Block into 5 layers: {gainNative}");

        // Evasion: layers times 100 must cover the whole HBR hit; spending rounds up per 100.
        Check(hbrHit > 80m, $"reference HBR hit {hbrHit} from Small Hopper's native6 exceeds even 80 layers taken literally");
        Check(hitFull.dodged && hitFull.left == 40 - need && hitFull.absorbed == hbrHit,
            $"40 layers evade the HBR hit {hbrHit} and spend {need}: {hitFull}");
        Check(!hitShort.dodged && hitShort.left == need - 1, $"{need - 1} layers cannot evade {hbrHit} and keep their layers: {hitShort}");
        Check(hitExact.dodged && hitExact.left == 0 && hitExact.removed,
            $"exactly {need} layers evade {hbrHit} and the power is removed: {hitExact}");
        Check(hitTiny.dodged && hitTiny.left == 39, $"an HBR hit of 30 spends one layer: {hitTiny}");
        Check(hitNative.dodged && hitNative.left == 10, $"disabled HBR: 40 layers evade 30 and keep 10: {hitNative}");
        Check(!hitNativeShort.dodged && hitNativeShort.left == 29, $"disabled HBR: 29 layers cannot evade 30: {hitNativeShort}");
        Check(hitNativeHalf.dodged && hitNativeHalf.left == 0 && hitNativeHalf.removed, $"disabled HBR: 23 layers evade 22.5 and spend 23: {hitNativeHalf}");
        Check(relicFreeDodged, $"without a 31A relic, {need} HBR layers still evade {hbrHit}");

        Equal(visible, 300m, "HBR 31A no longer makes Dodge count as visible Block");
        Equal(visibleNative, 43m, "disabled HBR: 40 layers count as 40 Block on top of native 3");

        Check(kept.SequenceEqual(new[] { 20, 20, 20, 20, 20, 40 }),
            "HBR Dodge keeps its normal cap40 and halves at every 31A tier; native four-kind retention remains: " + string.Join(", ", kept));
    }

    static async Task DodgeStatCases()
    {
        // The bonus from 10, 20, 40 and 80 layers (six kinds for the expanded cap).
        var bonus = new List<(bool hbr, int layers, int stats)>();
        foreach (bool hbr in new[] { true, false })
            foreach (int layers in new[] { 10, 20, 40, 80 })
            {
                DodgeSetup(hbr, 6);
                Power<DodgePower>(Hero, layers);
                bonus.Add((hbr, layers, DodgeStats));
            }
        Reset(true);
        Power<DodgePower>(Hero, 40);
        int relicFree = DodgeStats;

        // Parity with real Strength and Dexterity of the same amount: HBR attributes, attack
        // damage of native 6 against Small Hopper and block of native 5; native flat amounts.
        var parity = new List<object>();
        bool same = true;
        foreach (bool hbr in new[] { true, false })
            foreach (int layers in new[] { 0, 10, 20, 40, 80 })
            {
                DodgeSetup(hbr, 6);
                if (layers > 0) Power<DodgePower>(Hero, layers);
                int stats = DodgeStats;
                decimal damage = Actual(Enemy, Hero, 6m, ValueProp.Move, null), block = HeroBlock();
                decimal unpoweredDamage = Actual(Enemy, Hero, 6m, ValueProp.Unpowered, null), unpoweredBlock = HeroBlock(ValueProp.Unpowered);
                decimal[] attributes = hbr ? new[] { HeroStat("Strength"), HeroStat("Dexterity"), HeroStat("Intelligence") } : Array.Empty<decimal>();
                DodgeSetup(hbr, 0);
                if (stats > 0) { Power<StrengthPower>(Hero, stats); Power<DexterityPower>(Hero, stats); }
                decimal realDamage = Actual(Enemy, Hero, 6m, ValueProp.Move, null), realBlock = HeroBlock();
                decimal realUnpoweredDamage = Actual(Enemy, Hero, 6m, ValueProp.Unpowered, null), realUnpoweredBlock = HeroBlock(ValueProp.Unpowered);
                decimal[] realAttributes = hbr ? new[] { HeroStat("Strength"), HeroStat("Dexterity"), HeroStat("Intelligence") } : Array.Empty<decimal>();
                same &= damage == realDamage && block == realBlock && unpoweredDamage == realUnpoweredDamage &&
                        unpoweredBlock == realUnpoweredBlock && attributes.SequenceEqual(realAttributes);
                parity.Add(new { hbr, layers, stats, damage, realDamage, block, realBlock, unpoweredDamage, unpoweredBlock, attributes });
            }
        Cases.Add(new { source = "Dodge Strength and Dexterity", bonus = bonus.Select(row => new { row.hbr, row.layers, row.stats }), relicFree, parity });

        // Double Powers doubles real Strength and the layers; the bonus follows the layers only.
        DodgeSetup(true, 4);
        var strength = Power<StrengthPower>(Hero, 3);
        Power<DodgePower>(Hero, 10);
        decimal beforeDouble = HeroStat("Strength");
        var doubler = Owned<DoublePowersHbrgoSaki>();
        await (Task)Native(doubler, "OnPlay", new ThrowingPlayerChoiceContext(), Play(doubler))!;
        int doubledStrength = Hero.GetPowerAmount<StrengthPower>(), doubledLayers = DodgeLayers;
        decimal afterDouble = HeroStat("Strength");
        object? spent = NativeAmount(Hero, "DamageBlockInternal", 814m, ValueProp.Move, out _);
        int spentLayers = DodgeLayers;
        decimal afterSpend = HeroStat("Strength");
        Powers(Hero).Remove(strength);
        decimal withoutStrength = HeroStat("Strength");
        Cases.Add(new { source = "Dodge stats under Double Powers", beforeDouble, doubledStrength, doubledLayers, afterDouble, spentLayers, afterSpend, withoutStrength });

        // Dexterity adds Block and Block turns into Dodge; the cap ends the loop.
        var loops = new List<object>();
        foreach (bool hbr in new[] { true, false })
        {
            DodgeSetup(hbr, 2);
            var steps = new List<(int layers, int block, int stats)>();
            for (int step = 0; step < 10; step++)
            {
                decimal gained = HeroBlock();
                if (NativeAmount(Hero, "GainBlockInternal", gained, ValueProp.Move, out _) is Task task) await task;
                steps.Add((DodgeLayers, NativeBlock, DodgeStats));
            }
            loops.Add(new { hbr, steps = steps.Select(row => new { row.layers, row.block, row.stats }) });
            int capped = steps.FindIndex(row => row.layers == 40);
            Check(hbr ? steps.All(row => row.layers == 0 && row.stats == 0) &&
                  steps.Select((row, index) => row.block == 500 * (index + 1)).All(value => value) :
                  capped > 0 && steps.All(row => row.layers <= 40) && steps.Skip(capped).All(row => row.layers == 40 && row.stats == steps[capped].stats) &&
                  steps.Take(capped + 1).All(row => row.block == 0) && steps[capped + 1].block > 0 &&
                  steps.Skip(capped + 2).Select((row, index) => row.block - steps[capped + 1 + index].block).All(delta => delta == steps[capped + 1].block),
                $"HBR has no Block-Dodge-stat loop; legacy native loop still stops at cap (HBR {hbr}): " +
                string.Join(" ", steps.Select(row => $"{row.layers}/{row.block}")));
        }
        Cases.Add(new { source = "Dodge Dexterity loop", loops });

        Check(bonus.Where(row => row.hbr).All(row => row.stats == 0),
            "HBR: all Dodge layer counts grant no removed 31A Strength or Dexterity");
        Check(bonus.Where(row => !row.hbr).Select(row => row.stats).SequenceEqual(new[] { 2, 4, 8, 16 }),
            "disabled HBR: 10/20/40/80 layers give 2/4/8/16 Strength and Dexterity");
        Equal(relicFree, 0m, "Dodge without a 31A relic grants no Strength or Dexterity");
        Check(same, "every layer count matches real Strength and Dexterity of the same amount: powered and Unpowered damage and Block, and HBR attributes");
        Check(beforeDouble == 106m && doubledStrength == 6 && doubledLayers == 20 && afterDouble == 112m,
            $"Double Powers still doubles real Strength and Dodge, without virtual 31A stats: {beforeDouble} -> {afterDouble}");
        Check(spent is decimal && spentLayers == 11 && afterSpend == 112m && withoutStrength == 100m,
            $"spending Dodge leaves real Strength unchanged; removing Strength restores100: {afterSpend}, {withoutStrength}");
    }

    static bool DodgeTextIsolated;

    static void DodgeTextCases()
    {
        Localization();
        if (!DodgeTextIsolated)
        {
            // The relic description swap for HBR runs lives in this production patch.
            Check(new Harmony(ProductionId + ".dodge-text").CreateClassProcessor(
                Mod.GetType("HBRGOSakiMod.Core.Patches.HotaruRelicDescriptionPatch", true)!).Patch().Count > 0,
                "installs production HotaruRelicDescriptionPatch");
            DodgeTextIsolated = true;
        }
        static string Plain(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]", "");
        string Smart(PowerModel power)
        {
            LocString smart = power.SmartDescription; smart.Add("OwnerName", "测试角色"); smart.Add("OnPlayer", true);
            Invoke(AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), power, smart, null);
            return Plain(smart.GetFormattedText());
        }
        string RelicText(RelicModel relic) => Plain(((LocString)typeof(RelicModel).GetProperty("DynamicDescription", F)!.GetValue(relic)!).GetFormattedText());
        string[] RelicTips(RelicModel relic) => ((System.Collections.IEnumerable)typeof(RelicModel).GetProperty("HoverTips", F)!.GetValue(relic)!)
            .Cast<object>().Select(tip => Plain(tip.GetType().GetProperty("Description", F)?.GetValue(tip) as string ?? "")).ToArray();
        string Compact(string key, int kinds)
        {
            LocString text = new("relics", key); text.Add("hbrCount", kinds);
            text.Add("Count", kinds); text.Add("CutinCount", kinds >= 6 ? 6 : kinds >= 4 ? 3 : kinds >= 2 ? 2 : 0);
            text.Add("NextTier", kinds < 2 ? "2种：2人" : kinds < 4 ? "4种：3人" : kinds < 6 ? "6种：6人和Live" : "已达到最高档");
            text.Add("LivePending", "");
            return Plain(text.GetFormattedText());
        }
        string Glossary(bool hbr)
        {
            // The manual's glossary entry for the power, through the production entry builder.
            string prefix = ModelDb.GetId(typeof(DodgePower)).Entry;
            Type entryType = Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrManualGlossaryEntry", true)!;
            var entries = (System.Collections.IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), entryType), StringComparer.Ordinal)!;
            Invoke(Helper("HbrManualGlossaryCatalog").GetMethod("AddEntry", F)!, null, entries, LocManager.Instance.GetTable("powers"), "powers",
                prefix, "HBR 能力", "", "", hbr);
            object entry = entries["powers:" + prefix]!;
            return Plain((string)entryType.GetProperty("Description")!.GetValue(entry)!);
        }

        const string HbrPower = "若闪避层数×100不小于伤害值，移除伤害值÷100（向上取整）层闪避，抵挡此次伤害";
        const string NativePower = "若闪避层数不小于伤害值，移除等量闪避，抵挡此次伤害";
        const string HbrRate = "每层闪避提供2点力量和2点敏捷";
        const string NativeRate = "每5层闪避提供1点力量和1点敏捷";
        const string Tiers = "集齐4种31A牌后，回合开始时不再失去闪避；集齐6种后，上限翻倍为80层";
        string dodgeId = ModelDb.GetId(typeof(DodgePower)).Entry;
        Cases.Add(new { source = "Dodge text ids", dodgeId });
        foreach (string key in new[] { "DODGE_POWER", "HBRGOSAKIMOD-DODGE_POWER" })
        {
            string Text(string field) { LocString text = new("powers", key + field); text.Add("DodgeStats", 8); return Plain(text.GetFormattedText()); }
            Check(Text(".hbrDescription").Contains(HbrPower) && !Text(".hbrDescription").Contains("31A") &&
                  !Text(".hbrOwnedDescription").Contains("31A") &&
                  Text(".description").Contains(NativePower) && Text(".description").Contains(NativeRate) && Text(".description").Contains(Tiers) &&
                  Text(".ownedDescription").Contains(NativeRate + "（当前各8点）") &&
                  new[] { ".description", ".hbrDescription", ".ownedDescription", ".hbrOwnedDescription" }.All(field => !Text(field).Contains("2.5%")),
                $"{key} states the new rules in all four texts, without the old damage bonus");
        }

        ThirtyOneACount = 4;
        try
        {
            var relic = DodgeSetup(true, 4);
            var power = Power<DodgePower>(Hero, 40);
            string hbrPower = Smart(power), hbrRelic = RelicText(relic), hbrGlossary = Glossary(true);
            string[] hbrTips = RelicTips(relic);
            string[] hbrCompact = new[] { 1, 2, 4, 6 }.Select(kinds => Compact("THIRTY_ONE_A_COLLECTION.hbrCompactDescription", kinds)).ToArray();
            string[] nativeCompact = new[] { 1, 2, 4, 6 }.Select(kinds => Compact("THIRTY_ONE_A_COLLECTION.compactDescription", kinds)).ToArray();
            H("Mode", false, 1);
            string nativePower = Smart(power), nativeRelic = RelicText(relic), nativeGlossary = Glossary(false);
            string[] nativeTips = RelicTips(relic);
            H("Mode", true, 1);
            Reset(true);
            string relicFreePower = Smart(Power<DodgePower>(Hero, 40));
            Cases.Add(new { source = "Dodge texts", hbrPower, hbrRelic, hbrTips, hbrCompact, hbrGlossary, nativePower, nativeRelic, nativeTips, nativeCompact, nativeGlossary, relicFreePower });

            Check(hbrPower.Contains(HbrPower) && !hbrPower.Contains("31A") && !hbrPower.Contains("当前各"), "HBR live Dodge states independent evasion without removed bond stats");
            Check(hbrRelic.Contains("插嘴") && hbrRelic.Contains("抽牌后") && hbrRelic.Contains("Live") && !hbrRelic.Contains("闪避"),
                "HBR 31A relic describes the new noisy squad bond and no old Dodge tiers");
            Check(hbrTips.Any(tip => tip.Contains("额外回合不触发")) && hbrTips.All(tip => !tip.Contains(HbrRate)),
                "HBR 31A hover provides cut-in and next-debuff rules rather than a Dodge tip");
            Check(hbrGlossary.Contains(HbrPower) && !hbrGlossary.Contains("31A"), "the manual's HBR Dodge entry keeps only independent evasion");
            Check(Equals(Call("HbrCompactTextKeys", "CompactKey", "THIRTY_ONE_A_COLLECTION.hbrDescription"), "THIRTY_ONE_A_COLLECTION.hbrCompactDescription"),
                "the HBR relic text has its short hover text");
            Check(hbrCompact.All(text => text.Contains("人插嘴") && !text.Contains("闪避")) &&
                  hbrCompact[0].Contains("0人插嘴") && hbrCompact[1].Contains("2人插嘴") &&
                  hbrCompact[2].Contains("3人插嘴") && hbrCompact[3].Contains("6人插嘴"),
                "HBR short relic text shows current 0/2/3/6 speakers without old Dodge tiers");
            Check(nativePower.Contains(NativePower) && nativePower.Contains(NativeRate + "（当前各8点）") &&
                  nativeRelic.Contains("每5层闪避提供1点力量和1点敏捷") && nativeRelic.Contains("获得的格挡转为等量的闪避") &&
                  nativeRelic.Contains("闪避层数同时视为格挡值；你的回合开始时不再失去闪避") && !nativeRelic.Contains("2.5%") &&
                  nativeTips.Any(tip => tip.Contains(NativePower) && tip.Contains(NativeRate)) && nativeTips.All(tip => !tip.Contains("×100") && !tip.Contains("当前各")) &&
                  nativeGlossary.Contains(NativePower) && nativeGlossary.Contains(NativeRate),
                "disabled HBR states the native rules in the live power, relic, tip and glossary");
            Check(nativeCompact.All(text => text.Contains("每5层闪避+1力量、+1敏捷")) &&
                  nativeCompact[0].Contains("下一档（2种）：获得的格挡转为等量的闪避。") && nativeCompact[1].Contains("获得的格挡转为等量闪避") &&
                  nativeCompact[2].Contains("闪避同时视为格挡，回合开始时不再失去") && nativeCompact[3].Contains("闪避上限80层"),
                "the native short relic text states the native rules at every tier");
            Check(relicFreePower.Contains(HbrPower) && !relicFreePower.Contains("当前各"), "Dodge without a 31A relic states its rule without a current amount");
        }
        finally { ThirtyOneACount = 0; }
    }
}
