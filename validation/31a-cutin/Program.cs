using System.Text.Json;
using HBRGOSakiMod.Core.Helpers;

string root = Path.GetFullPath(args[0]);
string report = Path.GetFullPath(args[1]);
var passed = new List<string>();
void Check(bool value, string name) { if (!value) throw new Exception(name); passed.Add(name); }
bool ok = false; string error = null;
try
{
    var library = ThirtyOneACutinLibrary.Parse(File.ReadAllText(Path.Combine(root, "data/thirty_one_a_cutin.json")));
    Check(library.Lines.Count == 1708 && library.Replies.Count == 534, "production parser loads every authored line and response");
    Check(library.Lines.Count(line => line.Enabled) == 1372, "actual drop and condition-active choices yield 1372 eligible lines");
    Check(library.Replies.Count(reply => reply.Enabled) == 521, "all thirteen disabled chains stay disabled");
    Check(library.Lines.Count(line => line.Role == "Live") == 2, "both working-picker Live tags merged into runtime data");
    var rng = new Random(20261114);
    foreach (string actor in new[] { "RKayamori", "YIzumi", "MAikawa", "TTojo", "Karen", "TKunimi" })
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < 500; i++)
        {
            var line = library.Pick(actor, "中了", false, _ => false, rng);
            Check(line != null && line.Character == actor && line.Enabled && !line.ReplyOnly &&
                string.IsNullOrEmpty(line.Condition) && line.Role is not "Live" and not "没中", "eligible actor, hit-role and inactive-condition selection: " + actor + "/" + i);
            seen.Add(line.Key);
        }
        Check(seen.Count > 5, "actor uses broad data pool: " + actor);
    }
    for (int i = 0; i < 100; i++)
    {
        var live = library.Pick("Karen", null, true, _ => true, rng);
        Check(live?.Role == "Live", "Live selection uses only its dedicated voice line " + i);
        var miss = library.Pick("MAikawa", "没中", false, _ => true, rng);
        Check(miss != null && miss.Role != "中了", "Megumi miss cannot select hit-tagged phrase " + i);
    }
    foreach (var line in library.Lines.Where(line => line.Enabled))
    {
        var used = new HashSet<string> { line.Key };
        var current = line;
        while (true)
        {
            var next = library.PickReply(current, used, _ => false, rng);
            if (next == null) break;
            Check(next.Enabled && !used.Contains(next.Key) && string.IsNullOrEmpty(next.Condition) && next.Role != "Live",
                "response remains eligible, context-safe and nonrepeating: " + next.Key);
            used.Add(next.Key); current = next;
        }
        Check(used.Count <= 4, "every real conversation stops within four distinct lines: " + line.Key);
    }
    // A controlled cyclic graph checks a true loop, rather than relying on the
    // delivered corpus to happen to contain one. This exercises production selection.
    string synthetic = """{"lines":[{"k":"a","c":"x","n":"X","cn":"a","voice":"a","ec":["Encount"],"drop":""},{"k":"b","c":"x","n":"X","cn":"b","voice":"b","ec":["Encount"],"drop":""}],"chains":[{"def":true,"steps":[{"k":"a"},{"k":"b","n":"X","cn":"b","voice":"b","c":"x","expr":"Encount"}]},{"def":true,"steps":[{"k":"b"},{"k":"a","n":"X","cn":"a","voice":"a","c":"x","expr":"Encount"}]}]}""";
    var cyclic = ThirtyOneACutinLibrary.Parse(synthetic);
    var start = cyclic.Lines[0]; var visited = new HashSet<string> { start.Key };
    var response = cyclic.PickReply(start, visited, _ => true, rng);
    Check(response?.Key == "b", "cyclic response graph can choose its first unvisited reply");
    visited.Add("b"); Check(cyclic.PickReply(response, visited, _ => true, rng) == null, "cyclic response graph stops before returning to first line");
    string fallback = """{"lines":[{"k":"a","c":"RKayamori","n":"月歌","cn":"a","voice":"a","ec":["Encount"],"drop":""}],"chains":[{"def":true,"steps":[{"k":"a"},{"n":"可怜","cn":"reply","voice":"original_Karen","expr":"Surprise"}]},{"def":true,"steps":[{"k":"a"},{"n":"可镰","cn":"reply","voice":"original_Karen","expr":"Anger"}]}]}""";
    var mapped = ThirtyOneACutinLibrary.Parse(fallback);
    Check(mapped.Replies[0].Reply.Character == "KAsakura" && mapped.Replies[0].Reply.Expressions.Single() == "Surprise",
        "reply labelled Karen ordinary identity keeps KAsakura Surprise artwork even with Karen voice cue suffix");
    Check(mapped.Replies[1].Reply.Character == "Karen", "reply labelled Karen killer identity keeps Karen artwork");
    var roles = new[] { "RKayamori", "YIzumi", "MAikawa", "TTojo", "Karen", "TKunimi" };
    foreach (string actor in roles)
    {
        var sample = library.Pick(actor, null, false, _ => false, rng);
        Check(sample != null && sample.Expressions.Any(expression => File.Exists(Path.Combine(root, "images/ui/thirty_one_a_cutin", actor + "DefaultAdvCutIn" + expression + ".png"))),
            "every primary bond actor has a currently installed selectable portrait: " + actor);
    }
    ok = true;
}
catch (Exception failure) { error = failure.ToString(); }
Directory.CreateDirectory(Path.GetDirectoryName(report)!);
File.WriteAllText(report, JsonSerializer.Serialize(new { ok, checks = passed.Count, passed, error,
    boundary = "Production data-only parser and weighted candidate/response selection. Exact working-picker flags and real corpus, plus a synthetic cycle. No Godot rendering or audio audition." }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine((ok ? "PASS " : "FAIL ") + passed.Count + " cut-in library checks");
if (!ok) { Console.Error.WriteLine(error); return 1; }
return 0;
