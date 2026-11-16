using System.Security.Cryptography;
using System.Text.Json;

static class NativeProfiledStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunNativeProfiles(reportPath);
}

static partial class EffectChecks
{
    public static void RunNativeProfiles(string reportPath)
    {
        bool ok = false; string? error = null;
        ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try
        {
            Install();
            NativeDamageProfileCases().GetAwaiter().GetResult();
            ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, cases = Cases, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "All registered raw/world native profiles through generated wrappers into real native Damage adapters and the actual HBR calculation boundary; every original native decimal call site transpiles. Actual FlameBarrier/MagicBomb async bodies, synchronous RollingBoulder helper and CaptureSpirit/Tingsha/Metronome/TheBomb typed adapters run. Common host isolates terminal HP writes, waiting and VFX; no full game, Godot rendering or packaging."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " native-profiled checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
