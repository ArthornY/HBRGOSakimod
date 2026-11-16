using System.Security.Cryptography;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;

static partial class EffectChecks
{
    static void RunFormTransitionFullEffectChecks(string reportPath)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Localization();
            FormTransitionFullEffectCases().GetAwaiter().GetResult();
            ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Player).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real production switch prefix, native PowerCmd.Apply/AfterApplied mode removal, actual card OnPlay, native DamageVar.UpdateCardPreview, shared damage/block hooks and native Chihaya Description/HoverTips with production localization patch. Explicit leaf isolation: native OnPlayWrapper body tail delegates directly to actual OnPlay, AttackCommand.Execute captures raw request then calculates through real Actual hook, draw animation, Cmd waits, idle IsOverOrEnding gate and PowerModel.Icon texture loading. This does not simulate the complete native card cleanup/replay series, actual draw or render a game. No Godot, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " form-transition checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
