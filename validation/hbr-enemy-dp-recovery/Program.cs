using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

// The existing bootstrap verifies the candidate/profile and isolates only beta telemetry.
static class OverchargeChecks { public static void Run(string reportPath) => DpRecoveryChecks.Run(reportPath); }

static class DpRecoveryChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> Cases = [];
    static readonly List<BlockCommand> Blocks = [];
    static int Ascension;
    sealed record BlockCommand(Creature Target, decimal Amount, ValueProp Props, object? CardPlay);
    static T State<T>(string name) => (T)typeof(Checks).GetField(name, F)!.GetValue(null)!;
    static CombatState Combat => State<CombatState>("Combat");
    static Creature Hero => State<Creature>("Hero");
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException e) when (e.InnerException != null)
        { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    static object? Host(string name, params object?[] args) => Invoke(typeof(Checks).GetMethod(name, F)!, null, args);
    static void Set(object owner, string field, object? value) => Host("Set", owner, field, value);
    static T Blank<T>() => (T)Invoke(typeof(Checks).GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static T Power<T>(Creature owner, int amount) where T : PowerModel =>
        (T)Invoke(typeof(Checks).GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, owner, amount)!;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Passed.Add(message); }
    static void Equal(decimal actual, decimal expected, string message) => Check(actual == expected, $"{message}: {actual} = {expected}");
    static bool Skip() => false;
    static bool AnimationLeaf(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool AscensionLeaf(AscensionLevel level, int ascensionValue, int fallbackValue, ref int __result)
    { __result = Ascension >= (int)level ? ascensionValue : fallbackValue; return false; }
    static bool BlockLeaf(object?[] __args, ref Task<decimal> __result)
    {
        Blocks.Add(new((Creature)__args[0]!, (decimal)__args[1]!, (ValueProp)__args[2]!, __args[3]));
        __result = Task.FromResult(0m); return false;
    }
    static void Install()
    {
        Host("Setup"); var harmony = new Harmony("hbr.enemy.dp.recovery.checks");
        harmony.Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim)),
            prefix: new HarmonyMethod(typeof(DpRecoveryChecks).GetMethod(nameof(AnimationLeaf), F)!));
        harmony.Patch(AccessTools.Method(typeof(PowerModel), "Flash"),
            prefix: new HarmonyMethod(typeof(DpRecoveryChecks).GetMethod(nameof(Skip), F)!));
        harmony.Patch(typeof(AscensionHelper).GetMethod(nameof(AscensionHelper.GetValueIfAscension),
            [typeof(AscensionLevel), typeof(int), typeof(int)])!,
            prefix: new HarmonyMethod(typeof(DpRecoveryChecks).GetMethod(nameof(AscensionLeaf), F)!));
        MethodInfo block = typeof(CreatureCmd).GetMethods(F).Single(m => m.Name == nameof(CreatureCmd.GainBlock) &&
            m.GetParameters() is var p && p.Length == 5 && p[1].ParameterType == typeof(decimal));
        harmony.Patch(block, prefix: new HarmonyMethod(typeof(DpRecoveryChecks).GetMethod(nameof(BlockLeaf), F)!));
        Type ledger = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighDimensionalShellNativeAmountPatch", true)!;
        Check(harmony.CreateClassProcessor(ledger).Patch().Count == 1,
            "installs real production exact-DP SetAmount bridge; native SetAmount and actual Restore remain active");
        foreach (var row in new[] { ("_enemyHighDimensionalShellPercent", 20), ("_enemyDpGrowthPerActPercent", 0),
            ("_enemyMaxHpGrowthPerActPercent", 0) })
            typeof(HBRGOSakiConfig).GetField(row.Item1, F)!.SetValue(null, row.Item2);
    }
    static void Reset(bool hbr, int ascension = 0)
    {
        Host("Reset"); Host("Mode", hbr, 1); Ascension = ascension;
        Set(State<object>("RunState"), "<AscensionLevel>k__BackingField", ascension);
        Set(Combat, "_enemies", new List<Creature>()); Blocks.Clear();
    }
    static Creature Actor<T>(int hp = 1_000_000) where T : MonsterModel
    {
        Creature actor = (Creature)Host("Actor", Blank<T>(), null, null)!;
        Set(actor, "_maxHp", hp); Set(actor, "_currentHp", hp);
        ((List<Creature>)AccessTools.Field(typeof(CombatState), "_enemies").GetValue(Combat)!).Add(actor);
        return actor;
    }
    static HighDimensionalShellPower Shell(Creature actor, decimal initial)
    {
        var shell = Power<HighDimensionalShellPower>(actor, 1);
        FieldInfo queue = typeof(HighDimensionalShellPower).GetField("_dpChanges", F)!;
        Set(shell, "_dpChanges", Activator.CreateInstance(queue.FieldType));
        Set(shell, "_pendingExtraDpDamage", new Stack<decimal>());
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData))!
            .SetValue(shell, initial.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return shell;
    }
    static void Move(Creature actor, string name) =>
        ((Task)Invoke(actor.Monster!.GetType().GetMethod(name, F)!, actor.Monster, (object)Array.Empty<Creature>())!).GetAwaiter().GetResult();
    static void VerifyBlocks(decimal expected, Creature[] targets, string label)
    {
        Check(Blocks.Count == targets.Length, label + " preserves one block command per living teammate");
        foreach (Creature target in targets)
        {
            BlockCommand command = Blocks.Single(b => ReferenceEquals(b.Target, target));
            Equal(command.Amount, expected, label + " keeps raw native block coefficient before its own block pipeline");
            Check(command.Props == ValueProp.Move && command.CardPlay == null, label + " keeps Move flags and null card play");
        }
    }
    static void HopperCases()
    {
        foreach (bool hbr in new[] { true, false }) foreach (int ascension in new[] { 0, 10 })
        {
            Reset(hbr, ascension); Creature hopper = Actor<HealHopper>(), target = Actor<AbyssKnocker>();
            var hopperDp = Shell(hopper, 100m); var targetDp = Shell(target, 100m); var heroDp = Shell(Hero, 100m);
            decimal raw = ascension >= (int)AscensionLevel.DeadlyEnemies ? 5m : 4m;
            decimal expected = raw * (hbr ? 100m : 1m); decimal hp = target.CurrentHp;
            Move(hopper, "RepairBetaMove");
            Equal(targetDp.CurrentDp, 100m + expected, $"HBR{hbr} asc{ascension} real HealHopper restores teammate DP");
            Equal(hopperDp.CurrentDp, 100m + expected, "real teammate selector includes the acting HealHopper");
            Equal(heroDp.CurrentDp, 100m, "enemy repair does not reach player-side DP");
            Equal(target.CurrentHp, hp, "repair changes DP only and leaves HP untouched");
            VerifyBlocks(raw, [hopper, target], "HealHopper");
            Cases.Add(new { monster = "HealHopper", hbr, ascension, raw, dpGain = expected, block = raw });
        }
        Reset(true, 10); Creature first = Actor<HealHopper>(34300), second = Actor<HealHopper>(34300), receiver = Actor<AbyssKnocker>(34300);
        Shell(first, 100m); Shell(second, 100m); var dp = Shell(receiver, 7862m);
        Equal(dp.MaximumDp, 14700m, "screenshot resource setup uses actual HBR enemy DP maximum 14700");
        Move(first, "RepairBetaMove"); Move(second, "RepairBetaMove");
        Equal(dp.CurrentDp, 8862m, "two ascension-10 HealHoppers restore 1000 total DP rather than 10");
        Equal(receiver.CurrentHp, 34300m, "two repairs preserve screenshot bottom HP 34300");
        Check(Blocks.Count == 6 && Blocks.All(b => b.Amount == 5m), "two repairs retain all six unscaled block coefficients");
    }
    static void HopperGuards()
    {
        Reset(true, 10); Creature hopper = Actor<HealHopper>(), broken = Actor<AbyssKnocker>(), dead = Actor<DeathSlug>();
        var full = Shell(hopper, 100m); typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData))!
            .SetValue(full, (full.MaximumDp - 1m).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var brokenDp = Shell(broken, 100m); Power<BreakPower>(broken, 1);
        var deadDp = Shell(dead, 100m); Set(dead, "_currentHp", 0);
        Move(hopper, "RepairBetaMove");
        Equal(full.CurrentDp, full.MaximumDp, "actual shell Restore clamps scaled repair to remaining DP capacity");
        Equal(brokenDp.CurrentDp, 100m, "Break prevents ally DP recovery");
        Equal(deadDp.CurrentDp, 100m, "dead teammate is excluded from repair");
        VerifyBlocks(5m, [hopper, broken], "guarded HealHopper");
        Blocks.Clear(); Move(hopper, "RepairBetaMove");
        Equal(full.CurrentDp, full.MaximumDp, "repeated repair leaves full DP at its cap");
        Equal(brokenDp.CurrentDp, 100m, "repeated repair continues to respect Break");
    }
    static void OtherMonsterCases()
    {
        foreach (bool hbr in new[] { true, false }) foreach (var row in new (Type Monster, string Move, decimal Recovery, decimal? Block)[] {
            (typeof(AbyssKnocker), "RecoveryMove", 10m, 10m), (typeof(DeathSlug), "RecoveryMove", 30m, null),
            (typeof(FlatHand), "HealMove", 40m, 30m), (typeof(SkullFeather), "HealMove", 100m, 50m) })
        {
            Reset(hbr); MonsterModel monster = (MonsterModel)Invoke(typeof(Checks).GetMethod("Blank", F)!.MakeGenericMethod(row.Monster), null)!;
            Creature actor = (Creature)Host("Actor", monster, null, null)!;
            Set(actor, "_maxHp", 1_000_000); Set(actor, "_currentHp", 1_000_000);
            ((List<Creature>)AccessTools.Field(typeof(CombatState), "_enemies").GetValue(Combat)!).Add(actor);
            var dp = Shell(actor, 100m); Move(actor, row.Move);
            decimal expected = row.Recovery * (hbr ? 100m : 1m);
            Equal(dp.CurrentDp, 100m + expected, $"HBR{hbr} real {row.Monster.Name}.{row.Move} restores fixed DP once");
            Equal(actor.CurrentHp, 1_000_000m, row.Monster.Name + " DP recovery does not heal HP");
            if (row.Block is decimal block) VerifyBlocks(block, [actor], row.Monster.Name);
            else Check(Blocks.Count == 0, row.Monster.Name + " does not invent a block command");
            Cases.Add(new { monster = row.Monster.Name, hbr, raw = row.Recovery, dpGain = expected, block = row.Block });
        }
    }
    static void DerivedBoundaryCases()
    {
        Reset(true); Creature actor = Actor<AbyssKnocker>(); var dp = Shell(actor, 100m);
        Equal(dp.Restore(10m), 10m, "already-resolved direct DP recovery remains 10 rather than globally scaling to 1000");
        Equal(dp.CurrentDp, 110m, "real Restore keeps arbitrary derived recovery in combat units");
        Set(dp, "_pendingBreak", true);
        Equal(dp.Restore(500m), 0m, "pending Break prevents direct shell restoration");
        Equal(dp.CurrentDp, 110m, "pending Break preserves the DP ledger");
    }
    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try { Install(); HopperCases(); HopperGuards(); OtherMonsterCases(); DerivedBoundaryCases(); ok = true; }
        catch (Exception e) { error = e.ToString(); Console.Error.WriteLine(e); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, cases = Cases, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            telemetryIsolation = Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary = "Real custom HealHopper, AbyssKnocker, DeathSlug, FlatHand and SkullFeather recovery Move bodies; real CombatState teammate selection, HBR enablement, fixed-resource conversion, shell Restore/clamp and production exact-DP bridge with native PowerModel.SetAmount. Animation, Flash and final GainBlock leaf are isolated; block parameters are captured before their separate numerical pipeline. Ascension integer choice is controlled to select the real authored 4/5 values. No game process, rendered bars, full native block engine, saves, packaging or installed-file changes. Beta uses verified telemetry-only isolation." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " enemy fixed-DP recovery checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
