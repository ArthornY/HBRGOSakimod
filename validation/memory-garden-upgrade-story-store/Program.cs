using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Story;

var passed=new List<string>();
void Check(bool condition,string name){if(!condition)throw new Exception(name);passed.Add(name);}
string report=Path.GetFullPath(args.Length>0?args[0]:"local-store-report.json");
string root=Path.Combine(Path.GetTempPath(),"hbr-upgrade-story-store-checks",Environment.ProcessId.ToString());Directory.CreateDirectory(root);
int paths=0;string NewPath()=>Path.Combine(root,"case-"+(++paths)+".json");
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var errors=new List<string>();string error=null;
try
{
    string path=NewPath();var store=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
    Check(!store.HasStarted("run-a","31O","ruka",1)&&!store.HasSeen("31o","ruka",1),"empty store has neither started nor seen receipts");
    Check(!File.Exists(path),"read-only queries do not create a local file");
    foreach(string perspective in new[]{"31O","30A","Hotaru"})foreach(int level in Enumerable.Range(1,7))
    {
        Check(store.MarkStarted("run-a",perspective," RUKA ",level),$"first durable start claim {perspective}/{level}");
        Check(!store.MarkStarted("run-a",perspective.ToLowerInvariant(),"ruka",level),$"duplicate normalized start rejected {perspective}/{level}");
        Check(store.HasStarted("run-a",perspective.ToUpperInvariant(),"ruka",level)&&!store.HasSeen(perspective,"ruka",level),$"start alone never marks seen {perspective}/{level}");
        Check(store.MarkSeen(perspective,"Ruka",level)&&store.HasSeen(perspective.ToLowerInvariant(),"ruka",level),$"completion or skip marks one normalized seen key {perspective}/{level}");
    }
    var reloaded=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
    Check(reloaded.HasStarted("run-a","31o","ruka",3)&&!reloaded.MarkStarted("run-a","31o","ruka",3),"new store after simulated SL preserves the started claim");
    Check(reloaded.HasSeen("31O","ruka",3),"seen history survives process/file reconstruction");
    Check(!reloaded.HasStarted("run-b","31o","ruka",3)&&reloaded.MarkStarted("run-b","31o","ruka",3),"new run uses an independent started partition");
    Check(reloaded.HasSeen("31o","ruka",3),"cross-run seen history remains shared");
    Check(reloaded.MarkStarted("run-a","31o","yuki",3)&&!reloaded.HasSeen("31o","yuki",3),"different actor does not inherit another actor's seen receipt");
    Check(reloaded.MarkStarted("run-abandoned","31o","tama",4)&&!reloaded.HasSeen("31o","tama",4),"abandoned playback keeps only its durable started receipt");
    reloaded=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
    Check(!reloaded.MarkStarted("run-abandoned","31o","tama",4)&&!reloaded.HasSeen("31o","tama",4),"abandoned playback is not replayed on SL but remains unseen in a later run");
    string before=Hash(path);DateTime stamp=new(2000,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(path,stamp);
    Check(reloaded.MarkSeen("31o","ruka",3)&&Hash(path)==before&&File.GetLastWriteTimeUtc(path)==stamp,"repeated seen commit is a no-write idempotent success");

    path=NewPath();store=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
    foreach(int level in new[]{int.MinValue,-1,0,8,int.MaxValue})
        Check(!store.MarkStarted("r","31o","ruka",level)&&!store.MarkSeen("31o","ruka",level),"out-of-range level rejected "+level);
    foreach(string perspective in new[]{null,""," ","31a","unknown"})Check(!store.MarkStarted("r",perspective,"ruka",1),"invalid perspective never claims: "+perspective);
    foreach(string actor in new[]{null,""," ",new string('a',257)})Check(!store.MarkSeen("31o",actor,1),"invalid actor never seen: "+(actor?.Length??-1));
    foreach(string key in new[]{null,""," ",new string('x',2049)})Check(!store.MarkStarted(key,"31o","ruka",1),"invalid stable run key rejected: "+(key?.Length??-1));
    Check(!File.Exists(path),"invalid input never produces a local receipt file");

    foreach(string payload in new[]{"{", "{}", "{\"version\":2,\"seen\":[]}", "{\"version\":\"1\"}"})
    {
        path=NewPath();File.WriteAllText(path,payload);string original=Hash(path);int logs=errors.Count;store=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
        Check(!store.HasSeen("31o","ruka",1)&&!store.MarkStarted("r","31o","ruka",1)&&!store.MarkSeen("31o","ruka",1),"unreadable/unsupported file disables claims safely: "+payload);
        Check(Hash(path)==original&&errors.Count==logs+1,"bad file is preserved and warns only once per load: "+payload);
    }
    path=NewPath();File.WriteAllText(path,"""{"version":1,"seen":[{"perspective":"31O","actor":"RUKA","level":1},{"perspective":"31o","actor":"ruka","level":1},{"perspective":"bad","actor":"ruka","level":2},{"perspective":"31o","actor":"ruka","level":8},null],"runs":{"saved":[{"perspective":"Hotaru","actor":"TAMA","level":7}],"empty":null}}""");
    store=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
    Check(store.HasSeen("31o","ruka",1)&&!store.HasSeen("31o","ruka",2)&&store.HasStarted("saved","hotaru","tama",7),"actual JSON payload normalizes and deduplicates valid rows while ignoring invalid segments");
    Check(store.MarkSeen("30a","yuki",2),"valid imported file remains writable after cleanup");
    using(var document=JsonDocument.Parse(File.ReadAllText(path)))Check(document.RootElement.GetProperty("seen").GetArrayLength()==2,"written payload does not retain malformed or duplicate seen rows");

    path=NewPath();Directory.CreateDirectory(path);store=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);
    Check(!store.MarkStarted("r","31o","ruka",1)&&!store.HasStarted("r","31o","ruka",1),"failed disk start commit rolls back its in-memory claim");
    Check(!store.MarkSeen("31o","ruka",1)&&!store.HasSeen("31o","ruka",1),"failed disk seen commit rolls back its in-memory flag");
    Check(Directory.Exists(path)&&!File.Exists(path+".tmp"),"failed atomic replacement preserves destination and cleans only its own temporary file");
    Directory.Delete(path);Check(store.MarkStarted("r","31o","ruka",1),"transient write failure can recover without a lost receipt");

    path=NewPath();store=new HbrMemoryGardenUpgradeStoryLocalFile(path,errors.Add);var claims=new ConcurrentBag<bool>();
    Parallel.For(0,64,_=>claims.Add(store.MarkStarted("parallel","31o","ruka",1)));
    Check(claims.Count(result=>result)==1,"64 concurrent starts share one atomic durable claim");
    Parallel.For(0,32,_=>{if(!store.MarkSeen("31o","ruka",1))throw new Exception("parallel seen failed");});
    Check(new HbrMemoryGardenUpgradeStoryLocalFile(path).HasSeen("31o","ruka",1),"concurrent seen notifications serialize and survive a fresh load");
    using(var document=JsonDocument.Parse(File.ReadAllText(path)))Check(document.RootElement.GetProperty("runs").GetProperty("parallel").GetArrayLength()==1&&document.RootElement.GetProperty("seen").GetArrayLength()==1,"concurrent commits retain one JSON row per exact segment");
    Check(Enum.GetNames<HbrMemoryGardenStoryPlayback>().SequenceEqual(new[]{"All","UnseenOnly","Off"}),"playback setting has the three requested stable options");
}
catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
Directory.CreateDirectory(Path.GetDirectoryName(report));File.WriteAllText(report,JsonSerializer.Serialize(new{ok=error==null,checks=passed.Count,passed,error,expectedIoWarnings=errors.Count,testDirectory=root,boundary="Direct linked production BCL local-file implementation, injected temporary paths and real JSON/file operations. Concurrent claims use the actual store lock. No Godot user directory, no gameplay/net/Manual data, no main mod build."},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine((error==null?"PASS ":"FAIL ")+passed.Count+" local upgrade-story receipt checks");if(error!=null)Environment.ExitCode=1;
