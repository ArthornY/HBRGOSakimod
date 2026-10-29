using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

static class Checks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Mod = typeof(HighDimensionalShellPower).Assembly;
    static readonly List<string> Passed = [];
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static bool Enabled, SuppressEnhancement;
    static int NativeAscension;
    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static void Set(object owner, string field, object? value)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetField(field, F) is { } found) { found.SetValue(owner, value); return; }
        throw new MissingFieldException(owner.GetType().Name, field);
    }
    static object? Call(Type type, string name, params object[] arguments) => type.GetMethod(name, F)!.Invoke(null, arguments);
    static decimal Number(Type type, string name, params object[] arguments) => (decimal)Call(type, name, arguments)!;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Passed.Add(label);
    }
    static void Config(int hp = 100, int hpGrowth = 0, int dp = 20, int dpGrowth = 0)
    {
        foreach (var pair in new[] { ("_enemyMaxHpPercent", hp), ("_enemyMaxHpGrowthPerActPercent", hpGrowth),
            ("_enemyHighDimensionalShellPercent", dp), ("_enemyDpGrowthPerActPercent", dpGrowth) })
            typeof(HBRGOSakiConfig).GetField(pair.Item1, F)!.SetValue(null, pair.Item2);
    }
    static Creature Make(int max = 101, int current = 31, int act = 0, int ascension = 0,
        CombatSide side = CombatSide.Enemy, MonsterModel? monster = null)
    {
        monster ??= Blank<TestSubject>();
        Set(monster, "<IsMutable>k__BackingField", true);
        var run = Blank<RunState>(); Set(run, "_currentActIndex", act); Set(run, "<AscensionLevel>k__BackingField", ascension);
        var player = Blank<Player>(); Set(player, "_runState", run);
        var ally = Blank<Creature>(); Set(ally, "<Player>k__BackingField", player); Set(ally, "<Side>k__BackingField", CombatSide.Player);
        Set(player, "<Creature>k__BackingField", ally);
        var combat = Blank<CombatState>(); Set(combat, "<RunState>k__BackingField", run);
        Set(combat, "_allies", new List<Creature> { ally }); Set(combat, "_enemies", new List<Creature>());
        var creature = Blank<Creature>(); Set(creature, "<Monster>k__BackingField", monster);
        Set(creature, "<Side>k__BackingField", side); Set(creature, "_powers", new List<PowerModel>());
        Set(creature, "_maxHp", max); Set(creature, "_currentHp", current); creature.CombatState = combat;
        Set(monster, "_creature", creature);
        return creature;
    }
    static void Initial(Creature creature) => Call(Helper("EnemyHealthScalingHelper"), "ApplyInitialHealth", creature);
    static decimal Replacement(Creature creature, decimal amount) => Number(Helper("EnemyHealthScalingHelper"), "ScaleReplacementHealth", amount, creature);
    static void Patch(Harmony harmony, MethodBase target, string method) =>
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(Checks).GetMethod(method, F)!));
    static bool EnabledLeaf(ref bool __result) { __result = Enabled; return false; }
    static bool SuppressionLeaf(ref bool __result) { __result = SuppressEnhancement; return false; }
    static bool AscensionLeaf(AscensionLevel level, int ascensionValue, int fallbackValue, ref int __result)
    { __result = NativeAscension >= (int)level ? ascensionValue : fallbackValue; return false; }
    static bool TaskLeaf(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool HealLeaf(Creature creature, decimal amount, ref Task __result)
    { creature.HealInternal(amount); __result = Task.CompletedTask; return false; }
    static bool Skip() => false;
    static void AddPower(Creature creature, PowerModel power)
    {
        Set(power, "<IsMutable>k__BackingField", true); Set(power, "_owner", creature);
        ((List<PowerModel>)typeof(Creature).GetField("_powers", F)!.GetValue(creature)!).Add(power);
    }
    public static async Task Run()
    {
        Harmony harmony = new("hbr.enemy.resource.native.checks");
        Patch(harmony, typeof(StringName).GetConstructor([typeof(string)])!, nameof(Skip));
        Patch(harmony, Helper("HbrBattleStatsHelper").GetMethod("IsEnabled", F, [typeof(Creature)])!, nameof(EnabledLeaf));
        Patch(harmony, Helper("PhantomAscensionHelper").GetMethod("ShouldSuppressEnemyShellEnhancement", F)!, nameof(SuppressionLeaf));
        Patch(harmony, typeof(AscensionHelper).GetMethod(nameof(AscensionHelper.GetValueIfAscension),
            [typeof(AscensionLevel), typeof(int), typeof(int)])!, nameof(AscensionLeaf));
        Patch(harmony, typeof(MonsterModel).GetMethod(nameof(MonsterModel.AfterAddedToRoom))!, nameof(TaskLeaf));
        Patch(harmony, typeof(TestSubject).GetMethod(nameof(MonsterModel.AfterAddedToRoom))!, nameof(TaskLeaf));
        Patch(harmony, typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.Heal))!, nameof(HealLeaf));
        harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.EnemyHealthScalingPatch", true)!).Patch();
        harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.EnemyPhaseHealthScalingPatch", true)!).Patch();
        Type adapter = Helper("HbrEnemyResourceAdapter"), profile = Helper("HbrEnemyResourceProfile");
        foreach (bool enabled in new[] { false, true })
        {
            Enabled = enabled;
            foreach (int act in new[] { 0, 1, 2 })
            {
                Config(hp: 150, hpGrowth: 50, dpGrowth: 50);
                Creature creature = Make(act: act);
                decimal scale = enabled ? 700m : 1m;
                decimal growth = act == 0 ? 1m : act == 1 ? 1.5m : 2.25m;
                decimal maximum = decimal.Floor(101m * 1.5m * growth * scale);
                decimal current = decimal.Floor(31m * 1.5m * growth * scale);
                decimal dpReference = enabled ? maximum * 1000m / 700m : maximum;
                await creature.AfterAddedToRoom();
                Check(creature.MaxHp == maximum && creature.CurrentHp == current, $"entry baseline enabled={enabled}, act={act}");
                await creature.AfterAddedToRoom();
                Check(creature.MaxHp == maximum && creature.CurrentHp == current, $"entry repeated once enabled={enabled}, act={act}");
                Check(Number(adapter, "GetDpReferenceMaximum", creature) == dpReference, $"DP reference remains independent of new HP coefficient enabled={enabled}, act={act}");
                Check(HighDimensionalShellPower.CalculateAmount(creature) == decimal.Floor(dpReference * 0.2m * growth), $"ordinary DP preserves independent growth enabled={enabled}, act={act}");
                Check(Number(typeof(PartialDerivativeShieldPower), "CalculateInitialAmount", creature) == decimal.Floor(dpReference * growth), $"full-resource DP enabled={enabled}, act={act}");
                decimal phase = decimal.Floor(207m * 1.5m * growth * scale);
                Check(Replacement(creature, 207m) == phase && Replacement(creature, 207m) == phase, $"raw phase replacement never compounds enabled={enabled}, act={act}");
                creature.SetMaxHpInternal(phase);
                decimal phaseDpReference = enabled ? phase * 1000m / 700m : phase;
                Check(HighDimensionalShellPower.CalculateAmount(creature) == decimal.Floor(phaseDpReference * 0.2m * growth), $"phase DP uses fresh compatible baseline enabled={enabled}, act={act}");
                creature.SetMaxHpInternal(777m);
                Check(creature.MaxHp == 777 && HighDimensionalShellPower.CalculateAmount(creature) == decimal.Floor((enabled ? 222m : 155.4m) * growth), $"ordinary max-HP write remains unscaled enabled={enabled}, act={act}");
            }
        }
        Enabled = true; Config();
        Check(Replacement(Make(), 207m) == 207m, "uninitialized phase is left for outer entry");
        foreach (bool enabled in new[] { false, true })
        {
            Enabled = enabled; Config(hp: 150);
            Creature subject = Make(); await subject.AfterAddedToRoom();
            foreach (int rawHp in new[] { 207, 309 })
            {
                await (Task)typeof(TestSubject).GetMethod("Revive", F)!.Invoke(subject.Monster, [rawHp])!;
                decimal expected = decimal.Floor(rawHp * 1.5m * (enabled ? 700m : 1m));
                Check(subject.MaxHp == expected && subject.CurrentHp == expected,
                    $"actual native phase replaces raw HP enabled={enabled}, hp={rawHp}");
            }
        }
        Enabled = true; Config();
        foreach (string identity in new[] { "ally", "pet", "phantom" })
        {
            Creature creature = Make(max: 14, current: 14,
                side: identity == "ally" ? CombatSide.Player : CombatSide.Enemy, monster: Blank<SmallHopper>());
            if (identity == "pet") Set(creature, "_petOwner", Blank<Player>());
            if (identity == "phantom") AddPower(creature, Blank<PhantomPower>());
            Initial(creature);
            Check(creature.MaxHp == 14 && HighDimensionalShellPower.CalculateAmount(creature) == 2m &&
                Replacement(creature, 207m) == 207m && Number(adapter, "GetMaxHpScale", creature) == 1m,
                identity + " SmallHopper keeps old HP/DP resource path");
        }
        Creature dead = Make(current: 0); Initial(dead);
        Check(dead.MaxHp == 70700 && dead.CurrentHp == 0, "dead entry preserves zero HP");
        Creature reentrant = Make(); reentrant.MaxHpChanged += (_, _) => Initial(reentrant); Initial(reentrant);
        Check(reentrant.MaxHp == 70700 && reentrant.CurrentHp == 21700, "observer reentry cannot double scale");
        Config(hp: 0); Creature zero = Make(); Initial(zero);
        Check(zero.MaxHp == 1 && zero.CurrentHp == 1, "zero HP configuration retains living minimum");
        Config(dp: 0); Creature noShell = Make(max: 70000);
        Check(HighDimensionalShellPower.CalculateAmount(noShell) == 0m, "ordinary zero-DP option remains zero");
        Check(Number(typeof(PartialDerivativeShieldPower), "CalculateInitialAmount", noShell) == 100000m, "simulation shield retains independence from normal-DP option");
        Check(Number(typeof(NararaLayeredDpPower), "CalculateUnadjustedInitialAmount", noShell) == 1m, "Narara zero-DP option keeps prior intrinsic minimum");
        Config(dp: 40); Creature enhanced = Make(max: 70000, ascension: 10);
        Check(HighDimensionalShellPower.CalculateAmount(enhanced) == 60000m, "ordinary DP option and ascension retain old product");
        Check(Number(typeof(NararaLayeredDpPower), "CalculateUnadjustedInitialAmount", enhanced) == 300000m, "Narara intrinsic full layer scales relative to default DP option");
        SuppressEnhancement = true;
        Check(HighDimensionalShellPower.CalculateAmount(enhanced) == 40000m, "phantom difficulty suppression still controls DP enhancement");
        SuppressEnhancement = false;
        Config();
        Creature large = Make(max: 999999999);
        var exact = Blank<LargeHealthPower>(); AddPower(large, exact);
        exact.MaxHpData = "18446744073709551616"; exact.CurrentHpData = "12345678901234567890";
        Check(decimal.Floor(Number(adapter, "GetDpReferenceMaximum", large)) == 26352491533870788022m, "resource boundary reads exact HP beyond native proxy with independent DP reference");
        Check(HighDimensionalShellPower.CalculateAmount(large) == 5270498306774157604m, "large DP baseline retains exact rational floor");
        Check(exact.MaxHpData == "18446744073709551616" && exact.CurrentHpData == "12345678901234567890", "reading resource baseline never rewrites saved health");
        object splitProfile = Activator.CreateInstance(profile, F, null, [1m, 1000m, 400m], CultureInfo.InvariantCulture)!;
        Check((decimal)profile.GetMethod("GetDpReferenceMaximum", F)!.Invoke(splitProfile, [123456m])! == 246912m, "separate DP profile coefficient can change independently of HP coefficient");
        var boss = Blank<NararaShibaBossMonster>(); Creature narara = Make(max: 80, current: 80, monster: boss); Initial(narara);
        typeof(NararaShibaBossMonster).GetMethod("InitializeDpLayers", F)!.Invoke(boss, null);
        decimal frozen = (decimal)typeof(NararaShibaBossMonster).GetProperty("ExactDpLayerMaximum", F)!.GetValue(boss)!;
        Check(narara.MaxHp == 80000 && frozen == 80000 && boss.DpLayersRemaining == 3, "Narara keeps exceptional 1000-times HP and freezes independent layer resource once");
        Set(boss, "<DpLayersRemaining>k__BackingField", 2); narara.SetMaxHpInternal(160000m); Config(dp: 40);
        typeof(NararaShibaBossMonster).GetMethod("InitializeDpLayers", F)!.Invoke(boss, null);
        Check(Number(typeof(NararaLayeredDpPower), "CalculateInitialAmount", narara) == frozen && boss.DpLayersRemaining == 2, "Narara existing layer cap and remaining layers survive HP/config changes");
        var stored = Blank<HighDimensionalShellPower>(); AddPower(narara, stored);
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData), F)!.SetValue(stored, "765432109876543210");
        _ = HighDimensionalShellPower.CalculateAmount(narara);
        Check(stored.CurrentDpData == "765432109876543210", "fresh resource calculation never rescales existing DP ledger");
        Config();
        CheckSharedEnemyHp(adapter, profile);
        await CheckSmallHopper(adapter, profile);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            passed = true, checks = Passed.Count, cases = Passed,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            boundary = "Actual native Creature entry, production health helper and DP resource calculators. Enablement, monster entry and phantom suppression are controlled leaves; exact HP/DP saved strings are fixture state. No Godot, game save/config writes, engine combat or original per-enemy HBR resource data is used."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS " + Passed.Count + " enemy resource native checks");
    }

    static void CheckSharedEnemyHp(Type adapter, Type profile)
    {
        Enabled = true; Config();
        // Explicit source/output pairs are encounter-category representatives,
        // not replacements for those monsters' native HP tables or samplers.
        foreach (var row in new (string Category, MonsterModel Monster, int RawHp, int Hp, int Dp)[]
        {
            ("native normal LivingFog", Blank<LivingFog>(), 14, 9800, 2800),
            ("native elite Byrdonis", Blank<Byrdonis>(), 42, 29400, 8400),
            ("native boss Queen", Blank<Queen>(), 140, 98000, 28000),
            ("custom normal GoldHopper", Blank<GoldHopper>(), 14, 9800, 2800),
            ("custom elite AbyssKnocker", Blank<AbyssKnocker>(), 42, 29400, 8400),
            ("custom boss RedCrimson", Blank<RedCrimsonBossMonster>(), 140, 98000, 28000),
            ("custom boss Narara exception", Blank<NararaShibaBossMonster>(), 140, 140000, 28000),
        })
        {
            Creature creature = Make(max: row.RawHp, current: row.RawHp / 2, monster: row.Monster);
            Initial(creature);
            int scale = row.Monster is NararaShibaBossMonster ? 1000 : 700;
            Check(creature.MaxHp == row.Hp && creature.CurrentHp == row.RawHp / 2 * scale,
                row.Category + " keeps native HP proportions and category-independent scaling");
            Check(HighDimensionalShellPower.CalculateAmount(creature) == row.Dp,
                row.Category + " preserves independent raw-HP-times-200 ordinary DP");
            Initial(creature);
            Check(creature.MaxHp == row.Hp, row.Category + " scaling remains once-only");
            Check(Replacement(creature, 42m) == 42m * scale,
                row.Category + " phase replacement retains its own HP coefficient");
            object actualProfile = Call(adapter, "GetProfile", creature)!;
            Check((decimal)profile.GetProperty("NativeMaxHpScale", F)!.GetValue(actualProfile)! == scale,
                row.Category + " selects the expected HP coefficient");
        }
        Creature unit = Make(max: 1, current: 1, monster: Blank<LivingFog>()); Initial(unit);
        Check(unit.MaxHp == 700 && HighDimensionalShellPower.CalculateAmount(unit) == 200m,
            "generic native one HP yields exact 700 HP and 200 DP without repeating-ratio loss");
        Creature two = Make(max: 2, current: 2, monster: Blank<LivingFog>()); Initial(two);
        Check(two.MaxHp == 1400 && HighDimensionalShellPower.CalculateAmount(two) == 400m,
            "generic native two HP yields exact 1400 HP and 400 DP instead of 399");
        Creature odd = Make(max: 101, current: 31, monster: Blank<LivingFog>()); Initial(odd);
        Check(odd.MaxHp == 70700 && odd.CurrentHp == 21700 && HighDimensionalShellPower.CalculateAmount(odd) == 20200m,
            "generic odd native HP retains exact times-700 HP and times-200 DP");
        object ordinaryProfile = Call(adapter, "GetProfile", odd)!;
        Check((decimal)profile.GetProperty("NativeReferenceHp", F)!.GetValue(ordinaryProfile)! == 1m &&
            (decimal)profile.GetProperty("ReferenceMaxHp", F)!.GetValue(ordinaryProfile)! == 700m &&
            (decimal)profile.GetProperty("ReferenceMaxDp", F)!.GetValue(ordinaryProfile)! == 200m,
            "ordinary profile stores 700 HP and independent 200 DP per native HP");
        foreach (int players in new[] { 1, 2, 4 })
        {
            Config(hp: 150, hpGrowth: 50, dpGrowth: 50);
            Creature summoned = Make(max: 42, current: 42, act: 1, monster: Blank<AbyssKnocker>());
            decimal rawMultiplayer = decimal.Floor(Creature.ScaleHpForMultiplayer(42m, null, players, 1));
            summoned.ScaleMonsterHpForMultiplayer(null, players, 1);
            Initial(summoned);
            decimal expectedHp = decimal.Floor(rawMultiplayer * 1.5m * 1.5m * 700m);
            Check(summoned.MaxHp == expectedHp &&
                HighDimensionalShellPower.CalculateAmount(summoned) == decimal.Floor(expectedHp * 300m / 700m),
                $"fresh custom elite/summon keeps native multiplayer and independent act growth players={players}");
            Check(Replacement(summoned, 42m) == 66150m,
                $"custom elite phase takes raw HP through one 700-times calculation players={players}");
        }
        Config();
        foreach (bool enabled in new[] { false, true })
        {
            Enabled = enabled;
            foreach (string identity in new[] { "ally", "pet", "phantom" })
            {
                Creature excluded = Make(max: 14, current: 7,
                    side: identity == "ally" ? CombatSide.Player : CombatSide.Enemy,
                    monster: Blank<NararaShibaBossMonster>());
                if (identity == "pet") Set(excluded, "_petOwner", Blank<Player>());
                if (identity == "phantom") AddPower(excluded, Blank<PhantomPower>());
                Initial(excluded);
                Check(excluded.MaxHp == 14 && excluded.CurrentHp == 7 && Replacement(excluded, 42m) == 42m &&
                    HighDimensionalShellPower.CalculateAmount(excluded) == 2m,
                    $"Narara exception excludes {identity} enabled={enabled}");
            }
            Creature native = Make(max: 14, current: 7, monster: Blank<Queen>()); Initial(native);
            Check(native.MaxHp == (enabled ? 9800 : 14) && native.CurrentHp == (enabled ? 4900 : 7) &&
                HighDimensionalShellPower.CalculateAmount(native) == (enabled ? 2800m : 2m),
                $"native boss HP/DP follows HBR enablement enabled={enabled}");
        }
        Enabled = true;
    }

    static async Task CheckSmallHopper(Type adapter, Type profile)
    {
        Enabled = true; Config();
        foreach (var row in new[] { (12, 8400, 2571), (13, 9100, 2785), (14, 9800, 3000),
            (15, 10500, 3214), (16, 11200, 3428), (17, 11900, 3642) })
        {
            Creature hopper = Make(max: row.Item1, current: row.Item1, monster: Blank<SmallHopper>());
            await hopper.AfterAddedToRoom();
            Check(hopper.MaxHp == row.Item2 && hopper.CurrentHp == row.Item2 &&
                HighDimensionalShellPower.CalculateAmount(hopper) == row.Item3,
                $"SmallHopper native {row.Item1} keeps calibrated HP/independent DP {row.Item2}/{row.Item3}");
            await hopper.AfterAddedToRoom();
            Check(hopper.MaxHp == row.Item2, $"SmallHopper native {row.Item1} entry remains once-only");
        }
        Creature baseline = Make(max: 14, current: 14, monster: Blank<SmallHopper>()); Initial(baseline);
        object baselineProfile = Call(adapter, "GetProfile", baseline)!;
        Check((decimal)profile.GetProperty("NativeReferenceHp", F)!.GetValue(baselineProfile)! == 14m &&
            (decimal)profile.GetProperty("ReferenceMaxHp", F)!.GetValue(baselineProfile)! == 9800m &&
            (decimal)profile.GetProperty("ReferenceMaxDp", F)!.GetValue(baselineProfile)! == 3000m,
            "SmallHopper profile stores distinct native reference, HP and DP values");
        object extraDpProfile = Activator.CreateInstance(profile, F, null, [14m, 9800m, 6000m], CultureInfo.InvariantCulture)!;
        Check((decimal)profile.GetProperty("NativeMaxHpScale", F)!.GetValue(extraDpProfile)! ==
                (decimal)profile.GetProperty("NativeMaxHpScale", F)!.GetValue(baselineProfile)! &&
            (decimal)profile.GetMethod("GetDpReferenceMaximum", F)!.Invoke(extraDpProfile, [9800m])! == 30000m,
            "changing independent reference DP does not change the HP coefficient");
        foreach (int difficulty in new[] { 0, 8 })
        {
            NativeAscension = difficulty;
            int minimum = difficulty == 0 ? 12 : 13, maximum = difficulty == 0 ? 16 : 17;
            var rolls = new HashSet<int>();
            bool preservesEveryRoll = true;
            for (uint seed = 0; seed < 32; seed++)
            {
                Creature native = Make(monster: Blank<SmallHopper>());
                native.SetUniqueMonsterHpValue([native], new Rng(seed));
                rolls.Add(native.MaxHp);
                int raw = native.MaxHp; Initial(native);
                preservesEveryRoll &= native.MaxHp == raw * 700m;
            }
            Check(preservesEveryRoll, $"native SmallHopper sampled HP reaches adapter unchanged difficulty={difficulty}");
            Check(rolls.SetEquals(Enumerable.Range(minimum, maximum - minimum + 1)),
                $"actual native HP sampler retains the full difficulty={difficulty} range");
        }
        NativeAscension = 0;
        Creature first = Make(monster: Blank<SmallHopper>()), second = Make(monster: Blank<SmallHopper>());
        first.SetUniqueMonsterHpValue([first], new Rng(77)); second.SetUniqueMonsterHpValue([second], new Rng(77));
        Check(first.MaxHp == second.MaxHp, "native HP sampler keeps same-seed replay determinism");
        var existing = new List<Creature>();
        for (int i = 0; i < 5; i++)
        {
            Creature next = Make(monster: Blank<SmallHopper>()); next.SetUniqueMonsterHpValue(existing.Append(next).ToArray(), new Rng((uint)i)); existing.Add(next);
        }
        Check(existing.Select(creature => creature.MaxHp).Distinct().Count() == 5, "native HP sampler retains its same-side duplicate avoidance");
        foreach (int act in new[] { 0, 1, 2 })
        {
            foreach (int players in new[] { 1, 2, 4 })
            {
                Config(hp: 150, hpGrowth: 50, dp: 40, dpGrowth: 50);
                Creature multiplayer = Make(max: 14, current: 14, act: act, monster: Blank<SmallHopper>());
                decimal nativeScaled = Creature.ScaleHpForMultiplayer(14m, null, players, act);
                multiplayer.ScaleMonsterHpForMultiplayer(null, players, act);
                decimal nativeMaximum = decimal.Floor(nativeScaled);
                Check(multiplayer.MaxHp == nativeMaximum, $"native multiplayer HP rounding retained players={players}, act={act}");
                decimal growth = act == 0 ? 1m : act == 1 ? 1.5m : 2.25m;
                decimal expectedHp = decimal.Floor(nativeMaximum * 1.5m * growth * 700m);
                await multiplayer.AfterAddedToRoom();
                Check(multiplayer.MaxHp == expectedHp &&
                    HighDimensionalShellPower.CalculateAmount(multiplayer) == decimal.Floor(expectedHp * 3000m * 2m * growth / 9800m),
                    $"SmallHopper keeps multiplayer, HP/DP configuration and act growth players={players}, act={act}");
            }
        }
        Config();
        Check(Replacement(baseline, 14m) == 9800m && Replacement(baseline, 14m) == 9800m,
            "SmallHopper raw replacement source retains calibrated one-pass HP");
        baseline.SetMaxHpInternal(9900m);
        Check(HighDimensionalShellPower.CalculateAmount(baseline) == 3030m,
            "SmallHopper fresh DP continues following dynamic maximum-HP changes");
        var storedDp = Blank<HighDimensionalShellPower>(); AddPower(baseline, storedDp);
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData), F)!.SetValue(storedDp, "1234");
        baseline.SetMaxHpInternal(10000m); _ = HighDimensionalShellPower.CalculateAmount(baseline);
        Check(storedDp.CurrentDpData == "1234", "SmallHopper maximum-HP changes never rescale existing current DP");
        Creature huge = Make(max: 999999999, monster: Blank<SmallHopper>());
        var exact = Blank<LargeHealthPower>(); AddPower(huge, exact);
        exact.MaxHpData = "18446744073709551616"; exact.CurrentHpData = "18446744073709551616";
        Check(HighDimensionalShellPower.CalculateAmount(huge) == 5646962471543740290m,
            "SmallHopper large DP avoids premature clamping of its reference intermediate");
        Config(dp: 1000);
        Check(HighDimensionalShellPower.CalculateAmount(huge) == 18446744073709551616m,
            "SmallHopper configured DP saturates only at the final resource cap");
        Config(); Enabled = false;
        Creature legacy = Make(max: 14, current: 14, monster: Blank<SmallHopper>()); Initial(legacy);
        Check(legacy.MaxHp == 14 && HighDimensionalShellPower.CalculateAmount(legacy) == 2m,
            "disabled SmallHopper profile keeps native HP and old configured shell");
        Enabled = true;
    }
}
