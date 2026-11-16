using System.Security.Cryptography;
using System.Text.Json;

static class DualChaseStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunDualChase(reportPath);
}

static partial class EffectChecks
{
    public static void RunDualChase(string reportPath)
    {
        bool ok = false; string? error = null;
        ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try { Install(); Localization(); DualSwordsChaseCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real native SpendResources/HasEnoughResourcesFor, native OnPlayWrapper replay and nested automatic play, live production mode/chase/cost/pipeline and combo hook. Presentation and attack damage terminal isolated; no Godot/game/packaging."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " dual-swords chase checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
