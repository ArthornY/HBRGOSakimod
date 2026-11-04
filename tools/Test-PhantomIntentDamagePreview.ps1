$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '..\Core\Helpers\PhantomIntentDamagePreview.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = @'

public static class PhantomIntentDamagePreviewRegression
{
    public static string Run()
    {
        int calls = 0;
        decimal[] repeated = new decimal[1000];
        Array.Fill(repeated, 12m);
        int[] actual = PhantomIntentDamagePreview.Calculate(repeated, amount =>
        {
            calls++;
            return amount * 1.5m;
        });
        Check(calls == 1 && actual.Length == 1000,
            "A 1000-hit preview must evaluate identical damage once and retain every hit.");
        foreach (int damage in actual) Check(damage == 18, "Repeated hit damage changed.");

        calls = 0;
        actual = PhantomIntentDamagePreview.Calculate([3.1m, 7m, 3.9m, 3.1m, 7m], amount =>
        {
            calls++;
            return amount * 10m;
        });
        Check(calls == 3, "Different decimal base values must have separate calculations.");
        Check(actual.AsSpan().SequenceEqual(new[] { 31, 70, 39, 31, 70 }),
            "Mixed hit order, precision or duplicate count changed.");

        decimal targetMultiplier = 2m;
        Func<decimal, decimal> calculateForTarget = amount => amount * targetMultiplier;
        int[] firstTarget = PhantomIntentDamagePreview.Calculate([5m, 5m], calculateForTarget);
        targetMultiplier = 3m;
        int[] nextTarget = PhantomIntentDamagePreview.Calculate([5m, 5m], calculateForTarget);
        Check(firstTarget[0] == 10 && nextTarget[0] == 15,
            "A new target or combat state must be recalculated without a shared cache.");

        actual = PhantomIntentDamagePreview.Calculate([-2m, 1.9m, -2m], amount => amount);
        Check(actual.AsSpan().SequenceEqual(new[] { 0, 1, 0 }),
            "Native nonnegative integer display conversion changed.");
        actual = PhantomIntentDamagePreview.Calculate([2m], amount => amount * 2m);
        Check(actual.Length == 1 && actual[0] == 4, "Single-hit preview changed.");
        actual = PhantomIntentDamagePreview.Calculate(Array.Empty<decimal>(), _ =>
            throw new Exception("An empty preview must not evaluate damage."));
        Check(actual.Length == 0, "Empty preview changed.");
        return "PASS: repeated/mixed/decimal hits, target and state isolation, display conversion, single/empty previews.";
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
'@

# Compile the actual production helper in memory; no Godot runtime, game assets,
# project restore or delivered mod output is needed for these regression checks.
Add-Type -TypeDefinition ($source + $tests)
[HBRGOSakiMod.Core.Helpers.PhantomIntentDamagePreviewRegression]::Run()
