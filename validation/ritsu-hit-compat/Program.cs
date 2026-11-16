using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;

static class RitsuStandalone
{
    public static Action<AttackHitContext, bool>? Observe;
    public static void Run(string path) => EffectChecks.RunRitsuHitCompat(path);
}
public sealed class RitsuHitProbePower : PowerModel, IAttackHitHookListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public Task BeforeAttackHit(AttackHitContext context) { RitsuStandalone.Observe?.Invoke(context, true); context.Damage += 2m; return Task.CompletedTask; }
    public Task AfterAttackHit(AttackHitContext context) { RitsuStandalone.Observe?.Invoke(context, false); return Task.CompletedTask; }
}
static partial class EffectChecks
{
    const string RitsuOwner = "com.ritsukage.sts2-RitsuLib.framework-core";
    static readonly List<string> RHitEvents = [];
    static object?[] RHitRecords = [];
    static decimal RHitAmount;
    static int RHitDamageCalls, RHitBefore, RHitAfter;
    static TaskCompletionSource<IEnumerable<DamageResult>>? RHitWait;
    static Task<IEnumerable<DamageResult>>? RHitLastTask;
    static Task<IEnumerable<DamageResult>>? RHitWrapperTask;
    static void RHitObserveWrapper(Task<IEnumerable<DamageResult>> __result) => RHitWrapperTask = __result;
    static bool RHitLeaf(object[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        RHitEvents.Add("damage"); RHitDamageCalls++; RHitAmount = (decimal)__args[2];
        RHitRecords = ((IEnumerable)__args[1]).Cast<object>().ToArray();
        RHitLastTask = RHitWait?.Task ?? Task.FromResult<IEnumerable<DamageResult>>([]);
        __result = RHitLastTask; return false;
    }
    static void RHitRebuild() { }
    static void RHitClear()
    {
        RHitEvents.Clear(); RHitRecords = []; RHitDamageCalls = RHitBefore = RHitAfter = 0; RHitAmount = 0; RHitWait = null; RHitLastTask = RHitWrapperTask = null;
    }
    static MethodInfo RHitBody() => typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", F)!;
    static Type RHitType() => typeof(IAttackHitHookListener).Assembly.GetType("STS2RitsuLib.Combat.AttackHits.Patches.AttackCommandExecuteHitHookPatch", true)!;
    static void RHitInstallRitsu(Harmony harmony) => harmony.Patch(RHitBody(), transpiler:
        new HarmonyMethod(RHitType().GetMethod("Transpiler", F)!) { priority = 0, after = ["BaseLib"] });
    static void RHitCheckCombined(string label)
    {
        var patches = Harmony.GetPatchInfo(RHitBody())!;
        var ritsu = patches.Transpilers.Single(p => p.owner == RitsuOwner);
        var ours = patches.Transpilers.Single(p => p.PatchMethod.DeclaringType?.Name == "ThirtyOneBDamageCallsitePatch");
        Check(ritsu.priority == 0 && ours.priority == -100 && ours.after.Contains(RitsuOwner), label + " exact framework owner/order survives installation");
        var current = PatchProcessor.GetCurrentInstructions(RHitBody());
        Check(current.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(AttackHitHook) && m.Name == "DamageWithAttackHitHooks") == 1,
            label + " actual composed Execute IL retains exactly one original Ritsu wrapper");
        Check(!current.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(CreatureCmd) && m.Name == "Damage"),
            label + " native per-hit call is owned by the composed Ritsu path");
        var helper = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.RitsuAttackHitDamageCompatibility", true)!;
        var leaves = (MethodBase[])Invoke(helper.GetMethod("GetCallers", F)!, null, Canonical)!;
        Check(leaves.Length == 2 && leaves.All(m => !m.ContainsGenericParameters), label + " only two exact nongeneric Ritsu damage leaves are selected");
        foreach (var leaf in leaves)
            Check(PatchProcessor.GetCurrentInstructions(leaf).Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ThirtyOneBDamageCallsitePatch" && m.Name is "DamageSix" or "DamageEight") == 1,
                label + " Ritsu leaf preserves one ordinary 31B damage adapter: " + leaf.Name);
    }
    static async Task RHitOrderCases()
    {
        var ritsu = new Harmony(RitsuOwner); var ours = new Harmony(ProductionId + ".ritsu-callsite");
        var rebuild = new Harmony(ProductionId + ".ritsu-rebuild");
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.ThirtyOneBDamageCallsitePatch", true)!;
        bool negative = Environment.GetEnvironmentVariable("HBR_RITSU_HIT_NEGATIVE") == "1";
        foreach (bool ritsuFirst in new[] { true, false })
        {
            try
            {
                if (ritsuFirst) RHitInstallRitsu(ritsu);
                bool failed = false;
                try
                {
                    Check(ours.CreateClassProcessor(patch).Patch().Count > 0, "installs actual 31B caller patch, Ritsu first=" + ritsuFirst);
                    if (!ritsuFirst) RHitInstallRitsu(ritsu);
                }
                catch (Exception error) when (negative && error.ToString().Contains("matched 0 target site(s)")) { failed = true; }
                if (negative) { Check(failed, "old delivered DLL reproduces real Ritsu zero-match failure, Ritsu first=" + ritsuFirst); continue; }
                RHitCheckCombined("Ritsu first=" + ritsuFirst);
                rebuild.Patch(RHitBody(), postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RHitRebuild), F)!));
                RHitCheckCombined("rebuild, Ritsu first=" + ritsuFirst);
                await RHitAdapterCases();
            }
            finally { rebuild.UnpatchAll(rebuild.Id); ours.UnpatchAll(ours.Id); ritsu.UnpatchAll(ritsu.Id); }
        }
    }
    static MethodInfo RHitAdapter()
    {
        return typeof(AttackHitHook).GetMethods(F).Single(m => m.Name == "DamageWithAttackHitHooks" && m.GetParameters().Length == Canonical.GetParameters().Length + 3);
    }
    static Array RHitTargets(params Creature[] targets)
    {
        Type element = Canonical.GetParameters()[1].ParameterType.GetGenericArguments()[0];
        Array records = Array.CreateInstance(element, targets.Length);
        for (int i = 0; i < targets.Length; i++)
        {
            object value = element == typeof(Creature) ? targets[i] : Activator.CreateInstance(element, targets[i])!;
            if (element != typeof(Creature)) { element.GetField("hitVfx")!.SetValue(value, "ritsu-hit-" + i); element.GetField("playVfxOnCreatureCenter")!.SetValue(value, i == 0); }
            records.SetValue(value, i);
        }
        return records;
    }
    static Task RHitCall(Array targets, AttackCommand? attack, CardPlay? play, int index = 3, decimal count = 7)
    {
        object?[] first = [new ThrowingPlayerChoiceContext(), targets, 13m, ValueProp.Move, Enemy, null];
        object?[] args = Canonical.GetParameters().Length == 8 ? [.. first, play, ModifyDamageHookType.All, attack, index, count] : [.. first, attack, index, count];
        return (Task)Invoke(RHitAdapter(), null, args)!;
    }
    static async Task RHitAdapterCases()
    {
        BReset(); RHitClear(); Power<RitsuHitProbePower>(Enemy, 1);
        var attack = Blank<AttackCommand>(); Set(attack, "<Attacker>k__BackingField", Enemy);
        Set(attack, "<ModelSource>k__BackingField", Enemy.GetPower<RitsuHitProbePower>());
        var play = Blank<CardPlay>();
        RitsuStandalone.Observe = (context, before) =>
        {
            if (before) { RHitBefore++; RHitEvents.Add("before"); } else { RHitAfter++; RHitEvents.Add("after"); }
            Check(ReferenceEquals(context.Attack, attack) && context.HitIndex == 3 && context.TotalHitCount == 7m,
                "real Ritsu " + (before ? "before" : "after") + " listener keeps attack/index/count");
            if (Canonical.GetParameters().Length == 8) Check(ReferenceEquals(Native(context, "get_CardPlay"), play), "real Ritsu listener retains CardPlay");
        };
        await RHitCall(RHitTargets(Hero), attack, play);
        Check(RHitBefore == 1 && RHitAfter == 1 && RHitDamageCalls == 1 && RHitAmount == 15m,
            "real Ritsu listeners execute once around one damage command and their mutation survives");
        Check(RHitEvents.SequenceEqual(new[] { "before", "damage", "after" }), "real Ritsu hook sequence is preserved");
        if (Canonical.GetParameters().Length == 8)
        {
            Type t = RHitRecords.Single().GetType();
            Check((string)t.GetField("hitVfx")!.GetValue(RHitRecords[0])! == "ritsu-hit-0" && (bool)t.GetField("playVfxOnCreatureCenter")!.GetValue(RHitRecords[0])!, "real Ritsu typed target VFX fields survive");
        }
        RHitClear(); RitsuStandalone.Observe = null;
        RHitGiveShield();
        object scope = BBegin(); ShieldDice.Enqueue(99);
        bool deferred = (bool)Call("ThirtyOneBEnemyActionHelper", "TryDeferAction", Hero, Enemy,
            (Func<Task>)(() => { RHitEvents.Add("pending"); return Task.CompletedTask; }))!;
        Check(deferred, "real pending attached effect is staged before the Ritsu command");
        RitsuStandalone.Observe = (_, before) => RHitEvents.Add(before ? "before" : "after");
        await RHitCall(RHitTargets(Hero), attack, play);
        Check(RHitEvents.SequenceEqual(new[] { "before", "pending", "damage", "after" }), "real pending flush occurs after the before-hit listener and before native damage");
        Check(!(bool)Helper("ThirtyOneBEnemyActionHelper").GetProperty("HasPending", F)!.GetValue(null)!, "hit consumes the staged effect once");
        if (Canonical.GetParameters().Length == 8)
        {
            Type t = RHitRecords.Single().GetType();
            Check((string)t.GetField("hitVfx")!.GetValue(RHitRecords[0])! == "ritsu-hit-0" && (bool)t.GetField("playVfxOnCreatureCenter")!.GetValue(RHitRecords[0])!, "pending preparation also retains target VFX through the real Ritsu wrapper");
        }
        await BFinish(scope);
        // The real public before-hit API may retarget away from the holder.
        RHitClear(); RHitGiveShield(); ShieldDice.Clear(); ShieldDice.Enqueue(0);
        var peer = SupportTeammate();
        Set(RunState, "_players", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player, peer });
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        int rollsBefore = ShieldRolls; object retarget = BBegin();
        Check((bool)Call("ThirtyOneBEnemyActionHelper", "TryDeferAction", Hero, Enemy,
            (Func<Task>)(() => { RHitEvents.Add("pending"); return Task.CompletedTask; }))!, "retarget scenario stages the holder's effect");
        RitsuStandalone.Observe = (context, before) => { RHitEvents.Add(before ? "before" : "after"); if (before) context.Targets = new[] { peer.Creature }; };
        await RHitCall(RHitTargets(Hero), attack, play);
        Type recordType = RHitRecords.Single().GetType();
        Creature actualTarget = RHitRecords[0] as Creature ?? (Creature)recordType.GetField("target")!.GetValue(RHitRecords[0])!;
        Check(ReferenceEquals(actualTarget, peer.Creature) && ShieldRolls == rollsBefore,
            "Ritsu BeforeAttackHit retarget is honored without rolling the unhit holder's shield");
        Check(RHitEvents.SequenceEqual(new[] { "before", "damage", "after" }) && (bool)Helper("ThirtyOneBEnemyActionHelper").GetProperty("HasPending", F)!.GetValue(null)!,
            "retarget keeps the unhit holder's effect pending until action completion");
        if (Canonical.GetParameters().Length == 8) Check((string)recordType.GetField("hitVfx")!.GetValue(RHitRecords[0])! == "ritsu-hit-0", "retarget through Ritsu and 31B preserves the original VFX");
        await BFinish(retarget);
        Check(RHitEvents.Last() == "pending", "unhit holder's effect applies normally when the real action completes");
        // A canceled BeforeAttackHit never reaches a damage-preparation leaf.
        RHitClear(); RHitGiveShield(); ShieldDice.Clear(); ShieldDice.Enqueue(0); rollsBefore = ShieldRolls;
        object canceled = BBegin();
        Check((bool)Call("ThirtyOneBEnemyActionHelper", "TryDeferAction", Hero, Enemy, (Func<Task>)(() => Task.CompletedTask))!, "canceled before-hit scenario has a real pending effect");
        RitsuStandalone.Observe = (_, before) => { if (before) throw new TaskCanceledException("controlled-before-hit"); };
        bool cancellation = false;
        try { await RHitCall(RHitTargets(Hero), attack, play); }
        catch (TaskCanceledException e) { cancellation = e.Message.Contains("controlled-before-hit"); }
        Check(cancellation && RHitDamageCalls == 0 && ShieldRolls == rollsBefore, "canceling a real Ritsu before-hit callback preserves cancellation without early shield roll or damage");
        Call("ThirtyOneBEnemyActionHelper", "Close", canceled);
        Check(!(bool)Helper("ThirtyOneBEnemyActionHelper").GetProperty("HasPending", F)!.GetValue(null)!, "enemy-action failure cleanup leaves no pending effect");
        // The context-null wrapper fallback is the other exact damage leaf.
        RHitClear(); RHitWait = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task task = RHitCall(RHitTargets(Hero), null, null);
        Check(!task.IsCompleted && RHitDamageCalls == 1, "context-null fallback preserves the one real suspended native task");
        RHitWait.SetResult([]); await task;
    }
    static void RHitGiveShield()
    {
        if (!ModelDb.Contains(typeof(AoiInvincibleShieldPower))) ModelDb.Inject(typeof(AoiInvincibleShieldPower));
        if (!ModelDb.Contains(typeof(RollBonusPower))) ModelDb.Inject(typeof(RollBonusPower));
        Powers(Hero).RemoveAll(p => p is AoiInvincibleShieldPower);
        var shield = (AoiInvincibleShieldPower)ModelDb.Power<AoiInvincibleShieldPower>().ToMutable();
        Set(shield, "_owner", Hero); Set(shield, "_amount", 1); Powers(Hero).Add(shield);
    }
    public static void RunRitsuHitCompat(string path)
    {
        bool ok = false; string? error = null;
        try
        {
            if (Environment.GetEnvironmentVariable("HBR_RITSU_HIT_NEGATIVE") != "1")
            {
                Install(); Localization();
                var leaf = new Harmony(ProductionId + ".ritsu-terminal");
                foreach (var p in Harmony.GetPatchInfo(Canonical)?.Prefixes.Where(p => p.PatchMethod == typeof(EffectChecks).GetMethod(nameof(Terminal), F)).ToArray() ?? [])
                    new Harmony(p.owner).Unpatch(Canonical, p.PatchMethod);
                leaf.Patch(Canonical, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RHitLeaf), F)!) { priority = -200 });
                leaf.Patch(typeof(MegaCrit.Sts2.Core.Random.Rng).GetMethod("NextInt", [typeof(int)])!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BRawDice), F)!));
                leaf.Patch(typeof(AttackHitHook).GetMethods(F).Single(m => m.Name == "DamageWithAttackHitHooks" && m.GetParameters().Length == Canonical.GetParameters().Length + 3),
                    postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RHitObserveWrapper), F)!));
                leaf.Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf), F)!));
                leaf.Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
                foreach (var wait in typeof(Cmd).GetMethods(F).Where(m => m.Name is "Wait" or "CustomScaledWait" && m.ReturnType == typeof(Task)))
                    leaf.Patch(wait, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!));
            }
            RHitOrderCases().GetAwaiter().GetResult(); ok = true;
        }
        catch (Exception e) { error = e.ToString(); Console.Error.WriteLine(e); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            ritsuRuntimeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(IAttackHitHookListener).Assembly.Location))),
            negative = Environment.GetEnvironmentVariable("HBR_RITSU_HIT_NEGATIVE") == "1",
            boundary = "Real Harmony installation/rebuild and actual Ritsu/production adapter/listener execution. Engine damage terminal controlled; no Godot, installed DLL edits or full native attack presentation execution." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Ritsu hit compatibility checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
