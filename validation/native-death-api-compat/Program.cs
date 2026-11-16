using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

static class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance;
    const string Owner = "HBRGOSakiMod.NativeDeathApiChecks";
    static readonly List<string> Passed = [];
    static readonly List<object> Rows = [];
    static Assembly Mod = null!;
    static MethodInfo Native = null!;
    static AbstractModel? LastModel;
    static object?[] LastArguments = [];
    static int Entries;
    static Task Result = Task.CompletedTask;
    static Exception? Failure;

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Passed.Add(label);
    }

    static bool Spy(AbstractModel __instance, object?[] __args, ref Task __result)
    {
        Entries++;
        LastModel = __instance;
        LastArguments = __args;
        if (Failure is not null) throw Failure;
        __result = Result;
        return false;
    }

    static object?[] Arguments(PlayerChoiceContext context, Creature target, bool prevented, float duration)
    {
        return Native.GetParameters().Select(parameter => parameter.ParameterType == typeof(PlayerChoiceContext)
            ? (object?)context
            : parameter.ParameterType == typeof(Creature) ? target
            : parameter.ParameterType == typeof(bool) ? prevented
            : parameter.ParameterType == typeof(float) ? duration
            : parameter.ParameterType == typeof(CardModel) ? null
            : throw new Exception("Unexpected native argument: " + parameter)).ToArray();
    }

    static Task Invoke(AbstractModel listener, PlayerChoiceContext context, Creature target,
        bool prevented, float duration)
    {
        try { return (Task)Native.Invoke(listener, Arguments(context, target, prevented, duration))!; }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    static void DispatchCases(AbstractModel listener, string model,
        PlayerChoiceContext context, Creature target)
    {
        foreach (bool prevented in new[] { false, true })
        {
            Entries = 0;
            LastModel = null;
            LastArguments = [];
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Result = completion.Task;
            Task task = Invoke(listener, context, target, prevented, 1.25f);
            string label = model + " prevented=" + prevented + ": ";
            Check(Entries == 1, label + "native AbstractModel entry invokes the model once");
            Check(ReferenceEquals(LastModel, listener), label + "listener identity retained");
            Check(LastArguments.Length == 4, label + "bridge supplies the model's four-argument contract");
            Check(ReferenceEquals(LastArguments[0], context), label + "choice context retained");
            Check(ReferenceEquals(LastArguments[1], target), label + "target retained");
            Check((bool)LastArguments[2]! == prevented, label + "removal-prevented flag retained");
            Check((float)LastArguments[3]! == 1.25f, label + "animation duration retained");
            Check(ReferenceEquals(task, completion.Task), label + "original Task returned to native scheduler");
            Check(!task.IsCompleted, label + "pending Task not completed or awaited prematurely");
            completion.SetResult();
            task.GetAwaiter().GetResult();
            Check(task.IsCompletedSuccessfully, label + "asynchronous completion remains observable");
        }

        Entries = 0;
        var asynchronousFailure = new InvalidOperationException(model + " async sentinel");
        Result = Task.FromException(asynchronousFailure);
        Task failed = Invoke(listener, context, target, false, 0f);
        Check(ReferenceEquals(failed, Result), model + ": faulted Task identity retained");
        try { failed.GetAwaiter().GetResult(); throw new Exception("Async failure swallowed"); }
        catch (InvalidOperationException exception)
        {
            Check(ReferenceEquals(exception, asynchronousFailure), model + ": async failure retained");
        }
        Check(Entries == 1, model + ": async failure dispatched once");

        Entries = 0;
        var synchronousFailure = new InvalidOperationException(model + " sync sentinel");
        Failure = synchronousFailure;
        try { Invoke(listener, context, target, false, 0f); throw new Exception("Sync failure swallowed"); }
        catch (InvalidOperationException exception)
        {
            Check(ReferenceEquals(exception, synchronousFailure), model + ": synchronous failure retained");
        }
        finally { Failure = null; }
        Check(Entries == 1, model + ": synchronous failure dispatched once");

        Entries = 0;
        Result = Task.FromCanceled(new CancellationToken(canceled: true));
        Task cancelled = Invoke(listener, context, target, true, 0f);
        Check(ReferenceEquals(cancelled, Result) && cancelled.IsCanceled,
            model + ": cancellation Task retained without wrapping");
        Check(Entries == 1, model + ": cancellation dispatched once");
    }

    static void RunCases()
    {
        Mod = Assembly.LoadFrom(Bootstrap.ModPath);
        Type bridge = Mod.GetType("HBRGOSakiMod.Core.Compatibility.HbrDeathHookCompatibility", true)!;
        Type contract = Mod.GetType("HBRGOSakiMod.Core.Compatibility.IHbrAfterDeathListener", true)!;
        Native = (MethodInfo)bridge.GetMethod("TargetMethod", F)!.Invoke(null, null)!;
        Check(Native.DeclaringType == typeof(AbstractModel), "bridge selects actual native AbstractModel declaration");
        Check(Native.Name == "AfterDeath" && Native.ReturnType == typeof(Task), "bridge selects native Task death entry");
        Check(Native.GetParameters().Length is 4 or 5, "native entry has a supported real signature");
        Check(Native.GetParameters().Count(parameter => parameter.ParameterType == typeof(CardModel)) ==
            Native.GetParameters().Length - 4, "native-only call source is supplied separately from the model contract");

        var harmony = new Harmony(Owner);
        harmony.CreateClassProcessor(bridge).Patch();
        Check(Harmony.GetPatchInfo(Native)?.Prefixes.Any(patch => patch.owner == Owner) == true,
            "real production Harmony class installs on the actual native entry");

        var context = (PlayerChoiceContext)RuntimeHelpers.GetUninitializedObject(typeof(ThrowingPlayerChoiceContext));
        var target = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
        string[] models =
        [
            "Monsters.GoldHopper", "Relics.MomentOfReign", "Powers.DeathSlugRevivalPower",
            "Powers.FlatHandRevivalPower", "Powers.RefuseToLeavePower", "Powers.PhantomCardUpgradeBenefitPower",
            "Powers.PhantomPower", "Powers.PhantomNoExhaustPower", "Powers.SkullFeatherRevivalPower"
        ];
        foreach (string name in models)
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Models." + name, true)!;
            Check(contract.IsAssignableFrom(type), name + ": real model implements the production contract");
            MethodInfo method = type.GetMethod("AfterDeath", F | BindingFlags.DeclaredOnly, null,
                [typeof(PlayerChoiceContext), typeof(Creature), typeof(bool), typeof(float)], null)!;
            Check(method is not null, name + ": model keeps its declared four-parameter effect");
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Spy), F)!));
            Check(Harmony.GetPatchInfo(method)?.Prefixes.Count(patch => patch.owner == Owner) == 1,
                name + ": a single spy replaces only the effect body");
            int start = Passed.Count;
            DispatchCases((AbstractModel)RuntimeHelpers.GetUninitializedObject(type), name, context, target);
            Rows.Add(new { model = type.FullName, bodyReplacedBySpy = true, successfulDispatchChecks = Passed.Count - start });
        }

        Entries = 0;
        Type nativeModel = typeof(AbstractModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.StrengthPower", true)!;
        Check(!contract.IsAssignableFrom(nativeModel), "native StrengthPower does not implement the mod contract");
        var original = (AbstractModel)RuntimeHelpers.GetUninitializedObject(nativeModel);
        Task nativeTask = Invoke(original, context, target, false, 0f);
        Check(nativeTask.IsCompletedSuccessfully, "non-mod model retains its native base hook result");
        Check(Entries == 0, "non-mod model never invokes a mod effect spy");
    }

    public static int Run()
    {
        Exception? error = null;
        try { RunCases(); }
        catch (Exception exception) { error = exception; }
        var report = new
        {
            ok = error is null,
            checks = Passed.Count,
            passed = Passed,
            models = Rows,
            nativeEntry = Native?.ToString(),
            nativeParameterCount = Native?.GetParameters().Length,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Bootstrap.ModPath))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(AbstractModel).Assembly.Location))),
            error = error?.ToString(),
            boundary = "Real native AbstractModel death entry and production Harmony bridge; nine real production model instances. Only each final model effect body is replaced by an observing spy. No Godot/gameplay/animation execution; no invented native types."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Bootstrap.ReportPath)!);
        File.WriteAllText(Bootstrap.ReportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {Passed.Count} native death API checks");
        if (error is not null) Console.Error.WriteLine(error);
        return error is null ? 0 : 1;
    }
}
