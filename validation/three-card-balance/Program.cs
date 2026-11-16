using System.Security.Cryptography;
using System.Text.Json;

static class ThreeCardStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunThreeCards(reportPath);
}

static partial class EffectChecks
{
    public static void RunThreeCards(string reportPath)
    {
        bool ok = false; string? error = null;
        ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try { Install(); ThreeCardBalanceCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real native AutoPlay/OnPlayWrapper and Exhaust, real curse hooks through native AfterCardPlayed, real arrow distinct-card generation/upgrade and OD card OnPlay. Managed pile presentation, card pool test data, the choice screen and attack terminal isolated; no Godot/game/packaging."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " three-card checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
