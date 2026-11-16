using System.Security.Cryptography;
using System.Text.Json;

static class CardTemplateStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunCardTemplates(reportPath);
}

static partial class EffectChecks
{
    public static void RunCardTemplates(string reportPath)
    {
        bool ok = false; string? error = null;
        ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try
        {
            Install();
            if (Environment.GetEnvironmentVariable("HBR_CARD_TEMPLATE_BASELINE") == "1")
                CardTemplateBaselineCases().GetAwaiter().GetResult();
            else CardTemplateBalanceCases().GetAwaiter().GetResult();
            ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, cases = Cases, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual BladeDance/Opening/NoSelf/TrainingTime/LuckySeven card bodies, native power application and delayed-turn hook, native PopulateCombatState cloning plus production template exclusion, complete preview/actual numeric hooks. Damage-animation, turn-request, block-loss and pile-removal visual leaves isolated; no Godot/game/packaging."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " card-template checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
