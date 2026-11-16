using System.Collections;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

static class DelayedVariableChecks
{
    private static readonly List<string> Checks = [];
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Run(string modPath, string dependencies, string reportPath)
    {
        Exception? failure = null;
        string? observedStartupError = null;
        string[] nativeNames = [], actualRows = [];
        try
        {
            // This is the real native registration path and real native canonical.
            // Do not construct a fake CardModel or patch native constructors/ModelDb.
            Check(!ModelDb.Contains(typeof(Outbreak)), "Fresh process has not registered Outbreak");
            ModelDb.Inject(typeof(Outbreak));
            CardModel outbreak = ModelDb.Card<Outbreak>();
            Check(ModelDb.Contains(typeof(Outbreak)), "Native ModelDb contains actual Outbreak before first helper access");
            Check(outbreak.GetType() == typeof(Outbreak), "Canonical Outbreak has the actual native type");
            Check(outbreak.Id == ModelDb.GetId(typeof(Outbreak)), "Canonical Outbreak has its native model ID");
            int registered = RegistrationCount();

            // Establish the exact failure precondition without a spy: another
            // native construction really is rejected after canonical registration.
            Exception? duplicate = null;
            try { _ = new Outbreak(); } catch (Exception error) { duplicate = error; }
            Check(duplicate?.GetType().Name == "DuplicateModelException", "Native constructor genuinely rejects duplicate canonical model");
            Check(RegistrationCount() == registered, "Rejected duplicate leaves native registration unchanged");

            nativeNames = outbreak.DynamicVars.Values.Select(variable => variable.Name).Order().ToArray();
            bool stable = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE")!.StartsWith("stable", StringComparison.Ordinal);
            Check(nativeNames.Contains(stable ? "OutbreakPower" : "PoisonPower"), "Native Outbreak exposes the expected version-specific variable");
            Check(!nativeNames.Contains(stable ? "PoisonPower" : "OutbreakPower"), "Native Outbreak does not mix damage and Poison variables");

            Type helper = Assembly.Load("HBRGOSakiMod").GetType("HBRGOSakiMod.Core.Helpers.HbrNativeDelayedDamageVariables", throwOnError: true)!;
            MethodInfo contains = helper.GetMethod("Contains", Static)!;
            // First invocation runs the actual static initializer/Build after the
            // canonical already exists. The old implementation fails right here.
            Exception? initializationFailure = null;
            bool? firstContains = null;
            try { firstContains = (bool)contains.Invoke(null, [outbreak])!; }
            catch (Exception error) { initializationFailure = error; }
            if (Environment.GetEnvironmentVariable("HBR_EXPECT_STARTUP_COLLISION") == "1")
            {
                observedStartupError = initializationFailure?.ToString();
                Check(ExceptionChain(initializationFailure).Any(error => error.GetType().Name == "DuplicateModelException"), "Old candidate reproduces real DuplicateModelException from first scanner initialization");
                Check(ExceptionChain(initializationFailure).Any(error => error.StackTrace?.Contains("HbrNativeDelayedDamageVariables.Build", StringComparison.Ordinal) == true), "Observed old-candidate collision originates in the actual production Build");
                Check(ReferenceEquals(outbreak, ModelDb.Card<Outbreak>()) && RegistrationCount() == registered, "Old failed initialization does not replace the original canonical model");
            }
            else
            {
            if (initializationFailure != null) throw initializationFailure;
            Check(firstContains == stable, "First Contains initializes the real scanner after native registration without duplicate construction");
            MethodInfo build = helper.GetMethod("Build", Static)!;
            MethodInfo tryGet = helper.GetMethod("TryGet", Static)!;
            actualRows = ReadRows((IEnumerable)helper.GetProperty("All", Static)!.GetValue(null)!).Order().ToArray();
            Check(actualRows.Length >= 15, "Existing future-damage variable catalog remains populated");
            Check(actualRows.Count(row => row.StartsWith("Outbreak:", StringComparison.Ordinal)) == (stable ? 1 : 0), "Only stable Outbreak is a delayed-damage catalog row");
            Check(!actualRows.Any(row => row.StartsWith("Outbreak:PoisonPower", StringComparison.Ordinal)), "Beta Poison stacks are never damage-scaled");
            Check(actualRows.Contains("RollingBoulder:IncrementAmount:RollingBoulderPower"), "Other delayed-damage variables remain unchanged");
            for (int iteration = 0; iteration < 5; iteration++)
            {
                string[] fresh = ReadRows((IEnumerable)build.Invoke(null, null)!).Order().ToArray();
                Check(fresh.SequenceEqual(actualRows), $"Actual Build repeat {iteration + 1} is deterministic");
                Check((bool)contains.Invoke(null, [outbreak])! == stable, $"Contains repeat {iteration + 1} keeps version semantics");
                object?[] damageArgs = [outbreak, "OutbreakPower", null];
                Check((bool)tryGet.Invoke(null, damageArgs)! == stable, $"TryGet damage repeat {iteration + 1} keeps version semantics");
                if (stable)
                {
                    object descriptor = damageArgs[2]!;
                    Check(((Type)descriptor.GetType().GetProperty("AppliedPowerType", Instance)!.GetValue(descriptor)!).Name == "OutbreakPower", $"Stable damage repeat {iteration + 1} targets the actual damage power");
                }
                Check(!(bool)tryGet.Invoke(null, [outbreak, "PoisonPower", null])!, $"TryGet Poison repeat {iteration + 1} remains excluded");
                Check(ReferenceEquals(outbreak, ModelDb.Card<Outbreak>()), $"Build repeat {iteration + 1} preserves the native canonical object");
                Check(RegistrationCount() == registered, $"Build repeat {iteration + 1} adds no native models");
            }
            Check(!(bool)contains.Invoke(null, [null])!, "Null models remain absent");
            }
        }
        catch (Exception error) { failure = error; }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            suite = "startup-delayed-variables", ok = failure == null, checks = Checks,
            mode = Environment.GetEnvironmentVariable("HBR_EXPECT_STARTUP_COLLISION") == "1" ? "old-candidate-negative-control" : "fixed-candidate-regression",
            candidate = new { path = modPath, sha256 = DelayedVariableBootstrap.Hash(modPath) },
            native = new { profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"), path = Path.Combine(dependencies, "sts2.dll"), sha256 = DelayedVariableBootstrap.Hash(Path.Combine(dependencies, "sts2.dll")), unmodified = true },
            nativeOutbreakVariables = nativeNames, delayedVariables = actualRows,
            observedStartupError,
            boundaries = new[] { "Actual native ModelDb.Inject and canonical model; no ModelDb or constructor patches", "Actual production static initializer, Contains, Build and TryGet via reflection", "No telemetry isolation or runtime assembly rewriting", "Source/runtime validation; Godot initialization and in-game rendering are not executed" },
            error = failure?.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (failure != null) throw new Exception($"Startup delayed-variable regression failed after {Checks.Count} checks", failure);
        Console.WriteLine($"{Checks.Count} startup delayed-variable checks passed.");
    }

    private static int RegistrationCount() => ((IDictionary)typeof(ModelDb).GetField("_contentById", Static)!.GetValue(null)!).Count;
    private static IEnumerable<Exception> ExceptionChain(Exception? error)
    {
        while (error != null) { yield return error; error = error.InnerException; }
    }
    private static IEnumerable<string> ReadRows(IEnumerable rows)
    {
        foreach (object row in rows)
        {
            Type key = (Type)row.GetType().GetProperty("Key")!.GetValue(row)!;
            foreach (object value in (IEnumerable)row.GetType().GetProperty("Value")!.GetValue(row)!)
                yield return key.Name + ":" + value.GetType().GetProperty("Name", Instance)!.GetValue(value) + ":" + ((Type)value.GetType().GetProperty("AppliedPowerType", Instance)!.GetValue(value)!).Name;
        }
    }
    private static void Check(bool passed, string description)
    {
        if (!passed) throw new Exception(description);
        Checks.Add(description);
    }
}
