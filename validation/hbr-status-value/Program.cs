using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Helpers;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

// The same normalized DP reserve offsets as the production gauge. At each
// thickness, clear both the front HP bar and every back phase receipt.
foreach (float width in new[] { 80f, 225f, 450f, 900f })
foreach (float thickness in new[] { .5f, 1f, 2f, 4f })
foreach (float reserveHpHeight in new[] { 0f, 9.7f * thickness, 19.8f * thickness })
foreach (int dpReserves in new[] { 0, 1, 2 })
{
    float dpTop = -6.4f * thickness - reserveHpHeight - 10f * dpReserves * thickness;
    var rect = HbrEnemyStatusValueLayout.GetRect(width, 16f * thickness, width * .5f, dpTop, reserveHpHeight);
    Check(Math.Abs(rect.Left + rect.Width - width) < .001f, "Readout must end exactly at HP right edge.");
    Check(rect.Left >= width * .5f + .9f + HbrEnemyStatusValueLayout.HorizontalGap, "Readout must clear all DP pools on the right.");
    Check(rect.Top + rect.Height <= -reserveHpHeight - HbrEnemyStatusValueLayout.HpClearance + .001f, "Readout cannot overlap any occupied HP frame.");
    Check(rect.Top <= dpTop, "Layered DP layout cannot leave the readout below the current DP row.");
    Check(rect.Height is >= 16f and <= 28f, "Text height remains readable across gauge thickness settings.");
}

foreach (float width in new[] { -20f, 0f, 5f, 12f })
{
    var rect = HbrEnemyStatusValueLayout.GetRect(width, 0f, Math.Max(0f, width) * .5f, 0f, 0f);
    Check(rect.Width >= 0f && rect.Left >= 0f, "Collapsed/narrow gauges cannot give negative text dimensions.");
    Check(Math.Abs(rect.Left + rect.Width - Math.Max(0f, width)) < .001f, "Collapsed gauges retain a valid right edge.");
}

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string[] files = ["Core/Nodes/Screens/NManualBattleStatsPanel.cs", "docs/hbr-numerics-test.md"];
string[] oldTerms = ["敌能力", "敌方能力值", "敌方属性值", "独立能力值"];
foreach (string file in files)
{
    string text = File.ReadAllText(Path.Combine(root, file));
    foreach (string term in oldTerms) Check(!text.Contains(term, StringComparison.Ordinal), file + " retains old E terminology: " + term);
    Check(text.Contains("状态值", StringComparison.Ordinal), file + " must use 状态值.");
}

string manual = File.ReadAllText(Path.Combine(root, files[0]));
Check(manual.Contains("力、器用、知性、体力、精神、运", StringComparison.Ordinal), "Player six-stat terminology remains intact.");
Check(manual.Contains("防御参照属性", StringComparison.Ordinal), "Player defense attributes remain attributes.");
string caption = File.ReadAllText(Path.Combine(root, "Core/Nodes/Combat/NHbrEnemyStatusValueCaption.cs"));
Check(caption.Contains("HbrEnemyProfileHelper.GetEnemyParameter(_creature)", StringComparison.Ordinal), "Readout must use the live E getter, including temporary reductions.");
Check(caption.Contains("EnemyStatScalingHelper.IsEnemy(creature)", StringComparison.Ordinal), "Readout must exclude players and allied pets.");

string layoutPath = Path.Combine(root, "Core/Helpers/HbrEnemyStatusValueLayout.cs");
Console.WriteLine(JsonSerializer.Serialize(new
{
    ok = true, checks, layoutSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(layoutPath))),
    scope = "Production layout helper linked directly; terminology and live getter wiring checked. No Godot/rendering/game session."
}, new JsonSerializerOptions { WriteIndented = true }));
