using System.Security.Cryptography;
using System.Text.Json;

static class NewSupportStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunNewSupport(reportPath);
}

static partial class EffectChecks
{
    public static void RunNewSupport(string reportPath)
    {
        bool ok = false; string? error = null;
        ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try { Install(); Localization(); NewSupportCardCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real card bodies, native power application/removal and amount hooks, shared effect instances/lifecycle, original HBR critical sampling and final damage pipeline, and native gold mutation. Attack/draw/power-texture/repaint/time presentation leaves isolated; no Godot/game/packaging."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " new-support checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
