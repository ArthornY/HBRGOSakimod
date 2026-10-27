using HBRGOSakiMod.Core.Helpers;
using System.Text.Json;
using F = HBRGOSakiMod.Core.Helpers.HbrOriginalNumericFormula;

int checks = 0;
void Check(string name, decimal actual, decimal expected, decimal tolerance=0m)
{
    checks++;
    if (Math.Abs(actual - expected) > tolerance) throw new Exception($"{name}: {actual} != {expected}");
}
HbrBattleStats stats = new(300, 300, 300, 300, 300, 300);
HbrStatWeights weights = new(2, 1, 0, 0, 0, 0);
HbrSkillPowerCurve curve = new(100, 200, 100);
Check("normalized 2:1", F.EvaluateWeightedStat(new(300, 600,0,0,0,0),weights),400m);
Check("equal stats exact", F.EvaluateWeightedStat(stats,weights),300m);
Check("zero weights", F.EvaluateWeightedStat(stats,default),0m);
Check("negative weights ignored", F.EvaluateWeightedStat(stats,new(-2,1,0,0,0,0)),300m);
Check("negative stats ignored", F.EvaluateWeightedStat(new(-300,300,0,0,0,0),weights),100m);
Check("max decimal mean", F.EvaluateWeightedStat(new(decimal.MaxValue,decimal.MaxValue,0,0,0,0),new(decimal.MaxValue,decimal.MaxValue,0,0,0,0)),decimal.MaxValue);
Check("attack lower floor",F.EvaluateAttackCurve(100,300,curve),1);
Check("attack below border",F.EvaluateAttackCurve(250,300,curve),50);
Check("attack border",F.EvaluateSkillPower(stats,weights,300,curve),100);
Check("attack limit",F.EvaluateSkillPower(stats,weights,200,curve),200);
Check("attack overflow continues",F.EvaluateSkillPower(stats,weights,100,curve),250);
Check("critical shifts border",F.EvaluateSkillPower(stats,weights,300,curve,true),150);
Check("critical parameter clamped",F.EvaluateSkillPower(new(20,20,0,0,0,0),weights,25,curve,true),120);
Check("zero attack curve limit",F.EvaluateAttackCurve(300,300,new(100,200,0)),200);
Check("inverted attack endpoints",F.EvaluateAttackCurve(300,300,new(200,100,100)),200);
Check("attack saturation",F.EvaluateAttackCurve(decimal.MaxValue,0,new(decimal.MaxValue,decimal.MaxValue,1)),decimal.MaxValue);
Check("buff border",F.EvaluateBuffPotency(100,curve),200);
Check("buff above limit",F.EvaluateBuffPotency(200,curve),204);
Check("buff empty stat",F.EvaluateBuffPotency(0,curve),100);
Check("debuff negative diff",F.EvaluateDebuffPotency(100,300,curve),100);
Check("debuff border",F.EvaluateDebuffPotency(300,300,curve),100);
Check("debuff limit",F.EvaluateDebuffPotency(400,300,curve),200);
Check("debuff overflow",F.EvaluateDebuffPotency(500,300,curve),220);
Check("top two",F.SumStrongestTwo([50,30,80,-20]),130);
Check("same value independent slots",F.SumStrongestTwo([50,50,50]),100);
Check("exclusive competes",F.SelectStackedPotency([50,30,80],[140,90]),140);
Check("ordinary pair wins",F.SelectStackedPotency([50,30,80],[110,90]),130);
Check("stack saturation",F.SumStrongestTwo([decimal.MaxValue,decimal.MaxValue]),decimal.MaxValue);
Check("natural critical",F.CriticalProbability(300,300),0.015m);
Check("luck critical",F.CriticalProbability(350,300),0.035m);
Check("natural critical cap",F.CriticalProbability(10000,0),0.15m);
Check("critical cap with buffs",F.CriticalProbability(300,300,200),1);
Check("critical multiplier",F.CriticalMultiplier(50),2);
Check("shared attack bucket",F.AdditivePercentMultiplier([50,50,30]),2.3m);
Check("destruction independent of damage",F.DestructionGain(5,2.75m,100),27.5m);
Check("destruction resist",F.DestructionGain(5,2.75m,100,50),13.75m);
Check("destruction rate cap",F.AdvanceDestruction(298,10,300),300);
Check("destruction no regression on malformed cap",F.AdvanceDestruction(100,10,50),100);
SkillPowerChecks.Run(Check);
IncomingCurveCheckReport incoming = IncomingCurveChecks.Run(Check);
foreach (var vector in JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reference-vectors.json"))).RootElement.EnumerateArray())
{
    var c = new HbrSkillPowerCurve(vector.GetProperty("minimum").GetDecimal(),vector.GetProperty("maximum").GetDecimal(),vector.GetProperty("limit").GetDecimal());
    decimal p = vector.GetProperty("parameter").GetDecimal(), e = vector.GetProperty("enemy").GetDecimal();
    int level = vector.GetProperty("level").GetInt32();
    string kind = vector.GetProperty("kind").GetString()!;
    decimal result = kind switch { "attack" => F.EvaluateAttackCurve(p,e,F.ScaleAttackCurveForLevel(c,level)), "buff" => F.EvaluateBuffPotency(p,c,level), _=>F.EvaluateDebuffPotency(p,e,c,level)};
    // Author JS uses binary floating point then floors to hundredths, while this
    // engine uses decimal. A single hundredth rounding difference is allowed.
    Check($"reference {kind} {p}:{e} lv{level}",result,vector.GetProperty("expected").GetDecimal(),kind=="attack"?0.01m:0.000001m);
}
var report=new{passed=true,checks,source="ead9f319d18fb47e749718adac0d4384b785c0bc",incoming,sourcePath="Core/Helpers/HbrOriginalNumericFormula.cs",sourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(SourceFile()))),skillPowerSourcePath="Core/Helpers/HbrSkillPower.cs",skillPowerSourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(SourceFile())!, "HbrSkillPower.cs"))))};
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(report));

static string SourceFile([System.Runtime.CompilerServices.CallerFilePath] string caller = "" ) => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(caller)!, "../../Core/Helpers/HbrOriginalNumericFormula.cs"));
