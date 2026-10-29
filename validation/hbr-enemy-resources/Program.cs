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
                decimal scale = enabled ? 1000m : 1m;
                decimal growth = act == 0 ? 1m : act == 1 ? 1.5m : 2.25m;
                decimal maximum = decimal.Floor(101m * 1.5m * growth * scale);
                decimal current = decimal.Floor(31m * 1.5m * growth * scale);
                await creature.AfterAddedToRoom();
                Check(creature.MaxHp == maximum && creature.CurrentHp == current, $"entry baseline enabled={enabled}, act={act}");
                await creature.AfterAddedToRoom();
                Check(creature.MaxHp == maximum && creature.CurrentHp == current, $"entry repeated once enabled={enabled}, act={act}");
                Check(Number(adapter, "GetDpReferenceMaximum", creature) == maximum, $"DP reference keeps exact compatibility HP enabled={enabled}, act={act}");
                Check(HighDimensionalShellPower.CalculateAmount(creature) == decimal.Floor(maximum * 0.2m * growth), $"ordinary DP preserves independent growth enabled={enabled}, act={act}");
                Check(Number(typeof(PartialDerivativeShieldPower), "CalculateInitialAmount", creature) == decimal.Floor(maximum * growth), $"full-resource DP enabled={enabled}, act={act}");
                decimal phase = decimal.Floor(207m * 1.5m * growth * scale);
                Check(Replacement(creature, 207m) == phase && Replacement(creature, 207m) == phase, $"raw phase replacement never compounds enabled={enabled}, act={act}");
                creature.SetMaxHpInternal(phase);
                Check(HighDimensionalShellPower.CalculateAmount(creature) == decimal.Floor(phase * 0.2m * growth), $"phase DP uses fresh compatible baseline enabled={enabled}, act={act}");
                creature.SetMaxHpInternal(777m);
                Check(creature.MaxHp == 777 && HighDimensionalShellPower.CalculateAmount(creature) == decimal.Floor(777m * 0.2m * growth), $"ordinary max-HP write remains unscaled enabled={enabled}, act={act}");
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
                decimal expected = decimal.Floor(rawHp * 1.5m * (enabled ? 1000m : 1m));
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
        Check(dead.MaxHp == 101000 && dead.CurrentHp == 0, "dead entry preserves zero HP");
        Creature reentrant = Make(); reentrant.MaxHpChanged += (_, _) => Initial(reentrant); Initial(reentrant);
        Check(reentrant.MaxHp == 101000 && reentrant.CurrentHp == 31000, "observer reentry cannot double scale");
        Config(hp: 0); Creature zero = Make(); Initial(zero);
        Check(zero.MaxHp == 1 && zero.CurrentHp == 1, "zero HP configuration retains living minimum");
        Config(dp: 0); Creature noShell = Make(max: 100000);
        Check(HighDimensionalShellPower.CalculateAmount(noShell) == 0m, "ordinary zero-DP option remains zero");
        Check(Number(typeof(PartialDerivativeShieldPower), "CalculateInitialAmount", noShell) == 100000m, "simulation shield retains independence from normal-DP option");
        Check(Number(typeof(NararaLayeredDpPower), "CalculateUnadjustedInitialAmount", noShell) == 1m, "Narara zero-DP option keeps prior intrinsic minimum");
        Config(dp: 40); Creature enhanced = Make(max: 100000, ascension: 10);
        Check(HighDimensionalShellPower.CalculateAmount(enhanced) == 60000m, "ordinary DP option and ascension retain old product");
        Check(Number(typeof(NararaLayeredDpPower), "CalculateUnadjustedInitialAmount", enhanced) == 300000m, "Narara intrinsic full layer scales relative to default DP option");
        SuppressEnhancement = true;
        Check(HighDimensionalShellPower.CalculateAmount(enhanced) == 40000m, "phantom difficulty suppression still controls DP enhancement");
        SuppressEnhancement = false;
        Config();
        Creature large = Make(max: 999999999);
        var exact = Blank<LargeHealthPower>(); AddPower(large, exact);
        exact.MaxHpData = "18446744073709551616"; exact.CurrentHpData = "12345678901234567890";
        Check(Number(adapter, "GetDpReferenceMaximum", large) == 18446744073709551616m, "resource boundary reads exact HP beyond native proxy");
        Check(HighDimensionalShellPower.CalculateAmount(large) == 3689348814741910323m, "large DP baseline retains exact existing floor");
        Check(exact.MaxHpData == "18446744073709551616" && exact.CurrentHpData == "12345678901234567890", "reading resource baseline never rewrites saved health");
        object splitProfile = Activator.CreateInstance(profile, F, null, [1m, 1000m, 400m], CultureInfo.InvariantCulture)!;
        Check((decimal)profile.GetMethod("GetDpReferenceMaximum", F)!.Invoke(splitProfile, [123456m])! == 246912m, "separate DP profile coefficient can change independently of HP coefficient");
        var boss = Blank<NararaShibaBossMonster>(); Creature narara = Make(max: 80000, monster: boss);
        typeof(NararaShibaBossMonster).GetMethod("InitializeDpLayers", F)!.Invoke(boss, null);
        decimal frozen = (decimal)typeof(NararaShibaBossMonster).GetProperty("ExactDpLayerMaximum", F)!.GetValue(boss)!;
        Check(frozen == 80000 && boss.DpLayersRemaining == 3, "Narara freezes compatible independent layer resource once");
        Set(boss, "<DpLayersRemaining>k__BackingField", 2); narara.SetMaxHpInternal(160000m); Config(dp: 40);
        typeof(NararaShibaBossMonster).GetMethod("InitializeDpLayers", F)!.Invoke(boss, null);
        Check(Number(typeof(NararaLayeredDpPower), "CalculateInitialAmount", narara) == frozen && boss.DpLayersRemaining == 2, "Narara existing layer cap and remaining layers survive HP/config changes");
        var stored = Blank<HighDimensionalShellPower>(); AddPower(narara, stored);
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData), F)!.SetValue(stored, "765432109876543210");
        _ = HighDimensionalShellPower.CalculateAmount(narara);
        Check(stored.CurrentDpData == "765432109876543210", "fresh resource calculation never rescales existing DP ledger");
        Config();
        await CheckSmallHopper(adapter, profile);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            passed = true, checks = Passed.Count, cases = Passed,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            boundary = "Actual native Creature entry, production health helper and DP resource calculators. Enablement, monster entry and phantom suppression are controlled leaves; exact HP/DP saved strings are fixture state. No Godot, game save/config writes, engine combat or original per-enemy HBR resource data is used."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS " + Passed.Count + " enemy resource native checks");
    }

    static async Task CheckSmallHopper(Type adapter, Type profile)
    {
        Enabled = true; Config();
        foreach (var row in new[] { (12, 8571, 2571), (13, 9285, 2785), (14, 10000, 3000),
            (15, 10714, 3214), (16, 11428, 3428), (17, 12142, 3642) })
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
            (decimal)profile.GetProperty("ReferenceMaxHp", F)!.GetValue(baselineProfile)! == 10000m &&
            (decimal)profile.GetProperty("ReferenceMaxDp", F)!.GetValue(baselineProfile)! == 3000m,
            "SmallHopper profile stores distinct native reference, HP and DP values");
        object extraDpProfile = Activator.CreateInstance(profile, F, null, [14m, 10000m, 6000m], CultureInfo.InvariantCulture)!;
        Check((decimal)profile.GetProperty("NativeMaxHpScale", F)!.GetValue(extraDpProfile)! ==
                (decimal)profile.GetProperty("NativeMaxHpScale", F)!.GetValue(baselineProfile)! &&
            (decimal)profile.GetMethod("GetDpReferenceMaximum", F)!.Invoke(extraDpProfile, [10000m])! == 30000m,
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
                preservesEveryRoll &= native.MaxHp == decimal.Floor(raw * 10000m / 14m);
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
                decimal expectedHp = decimal.Floor(nativeMaximum * 1.5m * growth * (10000m / 14m));
                await multiplayer.AfterAddedToRoom();
                Check(multiplayer.MaxHp == expectedHp &&
                    HighDimensionalShellPower.CalculateAmount(multiplayer) == decimal.Floor(expectedHp * 0.6m * growth),
                    $"SmallHopper keeps multiplayer, HP/DP configuration and act growth players={players}, act={act}");
            }
        }
        Config();
        Check(Replacement(baseline, 14m) == 10000m && Replacement(baseline, 14m) == 10000m,
            "SmallHopper raw replacement source retains calibrated one-pass HP");
        baseline.SetMaxHpInternal(10100m);
        Check(HighDimensionalShellPower.CalculateAmount(baseline) == 3030m,
            "SmallHopper fresh DP continues following dynamic maximum-HP changes");
        var storedDp = Blank<HighDimensionalShellPower>(); AddPower(baseline, storedDp);
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData), F)!.SetValue(storedDp, "1234");
        baseline.SetMaxHpInternal(10200m); _ = HighDimensionalShellPower.CalculateAmount(baseline);
        Check(storedDp.CurrentDpData == "1234", "SmallHopper maximum-HP changes never rescale existing current DP");
        Creature huge = Make(max: 999999999, monster: Blank<SmallHopper>());
        var exact = Blank<LargeHealthPower>(); AddPower(huge, exact);
        exact.MaxHpData = "18446744073709551616"; exact.CurrentHpData = "18446744073709551616";
        Check(HighDimensionalShellPower.CalculateAmount(huge) == 5534023222112865484m,
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
