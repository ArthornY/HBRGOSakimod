using System.Security.Cryptography;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;

static partial class EffectChecks
{
    static void RunCrimsonStatInteractionChecks(string reportPath)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Localization();
            CrimsonStatInteractionCases().GetAwaiter().GetResult();
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
            boundary = "Real stat helpers/native temporary-power lifecycle, rune predicate consumers, owner/cannon command context, shared incoming/outgoing damage hooks and actual CreatureCmd.GainBlock -> native AfterBlockGained -> PowerCmd.Apply. Isolated leaves: external rune availability for the owner, idle combat-ending lifecycle, sound/VFX and waits. Broad stat baseline and direct Crimson/Focused cases belong to the separate main stat/effect suites. No Godot process, package, upload or production build."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Crimson stat interaction checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
