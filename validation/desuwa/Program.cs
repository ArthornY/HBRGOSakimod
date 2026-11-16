using System.Security.Cryptography;
using System.Text.Json;

static class DesuwaStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunDesuwa(reportPath);
}

static partial class EffectChecks
{
    public static void RunDesuwa(string reportPath)
    {
        bool ok = false; string? error = null;
        ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try { Install(); Localization(); DesuwaCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MegaCrit.Sts2.Core.Models.CardModel).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real card OnPlay, final-die selection/modifiers and cached roll, native Ritsu capability lookup, GainBlock/Hook/writer and FocusedShot callbacks, card descriptions, model clone/legacy upgrade and source pool filters. Raw die, absent keyword registration and capability attachment, history and presentation/time leaves isolated. No full save loading, Godot/game UI, multiplayer, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Desuwa checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
