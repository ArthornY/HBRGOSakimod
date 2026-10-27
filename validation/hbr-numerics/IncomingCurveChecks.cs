using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Helpers;
using F = HBRGOSakiMod.Core.Helpers.HbrOriginalNumericFormula;

internal readonly record struct IncomingCurveCheckReport(
    int PrimaryDiagramVectors, int RoyComparisons, int DocumentedRoyDifferences,
    int DecimalGuardrailVectors, int FrozenCodeHashes,
    string PrimarySource, string PrimaryImageDate, string ComparedSourceCommit);

internal static class IncomingCurveChecks
{
    internal static IncomingCurveCheckReport Run(Action<string, decimal, decimal, decimal> check)
    {
        void Equal(string name, decimal actual, decimal expected, decimal tolerance = 0m) =>
            check("incoming " + name, actual, expected, tolerance);
        void True(string name, bool result) => Equal(name, result ? 1m : 0m, 1m);
        string root = SourceDirectory();
        int sourceHashes = 0;
        using (JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(
                   Path.Combine(root, "incoming-reference", "sources.json"))))
        {
            foreach (JsonProperty file in metadata.RootElement.GetProperty("files").EnumerateObject())
            {
                string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                    Path.Combine(root, "incoming-reference", file.Name))));
                True("frozen source hash " + file.Name,
                    actualHash.Equals(file.Value.GetString(), StringComparison.OrdinalIgnoreCase));
                sourceHashes++;
            }
        }

        int vectors = 0, differences = 0;
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "incoming-vectors.json")));
        decimal tolerance = fixture.RootElement.GetProperty("tolerance").GetDecimal();
        foreach (JsonElement vector in fixture.RootElement.GetProperty("vectors").EnumerateArray())
        {
            string name = vector.GetProperty("name").GetString()!;
            decimal enemy = vector.GetProperty("enemyParameter").GetDecimal();
            decimal defense = vector.GetProperty("defensiveParameter").GetDecimal();
            HbrSkillPowerCurve curve = Curve(vector);
            if (vector.TryGetProperty("weightedInput", out JsonElement input))
            {
                decimal vit = input.GetProperty("vit").GetDecimal();
                decimal spr = input.GetProperty("spr").GetDecimal();
                HbrStatWeights weights = input.GetProperty("biasType").GetString() == "hp"
                    ? new(0,0,0,2,1,0) : new(0,0,0,1,2,0);
                defense = F.EvaluateWeightedStat(new(0,0,0,vit,spr,0), weights);
                Equal(name + " declared defensive weights", defense,
                    vector.GetProperty("defensiveParameter").GetDecimal(), tolerance);
            }
            decimal actual = F.EvaluateIncomingAttackCurve(enemy, defense, curve);
            decimal expected = vector.GetProperty("expected").GetDecimal();
            decimal roy = vector.GetProperty("comparedRoy").GetDecimal();
            Equal(name + " primary diagram", actual, expected, tolerance);
            True(name + " bounded", actual >= 1m && actual <= curve.MaximumPower);
            // This author implementation disagrees in the negative interval.
            // Report those differences explicitly instead of accepting it as
            // the production oracle or hiding them in a one-point tolerance.
            if (Math.Abs(expected - roy) > tolerance)
            {
                differences++;
                True(name + " documented Roy discrepancy", roy >= actual &&
                    roy - actual <= 1m + tolerance);
            }
            else Equal(name + " Roy shared interval", actual, roy, tolerance);
            vectors++;
        }
        Equal("documented Roy discrepancy count", differences,
            fixture.RootElement.GetProperty("documentedRoyDifferences").GetInt32());

        int guardrails = 0;
        using (JsonDocument extreme = JsonDocument.Parse(File.ReadAllText(
                   Path.Combine(root, "incoming-guardrail-vectors.json"))))
        {
            foreach (JsonElement vector in extreme.RootElement.GetProperty("vectors").EnumerateArray())
            {
                decimal Read(string property) => decimal.Parse(vector.GetProperty(property).GetString()!,
                    NumberStyles.Number, CultureInfo.InvariantCulture);
                Equal(vector.GetProperty("name").GetString()!,
                    F.EvaluateIncomingAttackCurve(Read("enemyParameter"), Read("defensiveParameter"),
                        new(Read("minimum"), Read("maximum"), Read("limit"))), Read("expected"));
                guardrails++;
            }
        }

        // Hand-calculated examples distinguish the source diagram from Roy's
        // L-1 slope and from reusing the outgoing curve's overflow behavior.
        HbrSkillPowerCurve ordinary = new(1000m, 2000m, 100m);
        Equal("defensive midpoint uses L slope", F.EvaluateIncomingAttackCurve(400,450,ordinary),500m);
        Equal("floor before defensive threshold", F.EvaluateIncomingAttackCurve(400,499.95m,ordinary),1m);
        Equal("maximum stays capped", F.EvaluateIncomingAttackCurve(1000,0,ordinary),2000m);
        Equal("fractional power is not rounded", F.EvaluateIncomingAttackCurve(400,400,
            new(123.456789m,987.654321m,1m)),123.456789m);
        Equal("fractional positive interpolation", F.EvaluateIncomingAttackCurve(400.25m,400,
            new(123.456789m,987.654321m,1m)),339.506172m);
        Equal("fractional defensive interpolation", F.EvaluateIncomingAttackCurve(400,400.25m,
            new(123.456789m,987.654321m,1m)),92.59259175m);

        // Increasing a defender's stat must never increase incoming base power.
        decimal previous = decimal.MaxValue;
        for (decimal defense = 200m; defense <= 600m; defense += 2.5m)
        {
            decimal current = F.EvaluateIncomingAttackCurve(400m, defense, ordinary);
            True("defense monotonic at " + defense, current <= previous);
            previous = current;
        }
        return new(vectors, vectors, differences, guardrails, sourceHashes,
            fixture.RootElement.GetProperty("primarySource").GetString()!,
            fixture.RootElement.GetProperty("primaryImageDate").GetString()!,
            fixture.RootElement.GetProperty("comparedSourceCommit").GetString()!);
    }

    private static HbrSkillPowerCurve Curve(JsonElement vector) => new(
        vector.GetProperty("minimum").GetDecimal(), vector.GetProperty("maximum").GetDecimal(),
        vector.GetProperty("limit").GetDecimal());

    private static string SourceDirectory([CallerFilePath] string caller = "") =>
        Path.GetDirectoryName(caller)!;
}
