using System.Text.Json;
using System.Security.Cryptography;
using HBRGOSakiMod.Core.Story;

static class PureChecks
{
    static readonly List<string> Passed=[];
    static void Check(bool value,string name){if(!value)throw new Exception(name);Passed.Add(name);}
    static object Line(string kind="dialogue",string text="fixture only",string background="")=>new{kind,text,speaker="fixture",background};
    static Dictionary<string,object> Segment(int level,string[] viewpoints,object[] lines)=>new(){{"level",level},{"perspectives",viewpoints},{"lines",lines}};
    static Dictionary<string,object> Story(params object[] segments)=>new(){{"schemaVersion",1},{"characterId","ruka"},{"presentation",new{background="res://images/fixture/background.png",dialogueStyle="original"}},{"segments",segments}};
    static HbrMemoryUpgradeStoryDefinition Parse(Dictionary<string,object> story)=>HbrMemoryUpgradeStoryDefinition.Parse(JsonSerializer.Serialize(story));
    static void Reject(Dictionary<string,object> story,string name)
    {try{Parse(story);throw new Exception("unexpectedly accepted: "+name);}catch(Exception error)when(error is JsonException or InvalidOperationException){Check(true,name);}}
    static void Cases()
    {
        var all=Enumerable.Range(1,7).Select(level=>(object)Segment(level,new[]{"31o"},new[]{Line()})).ToArray();var d=Parse(Story(all));
        foreach(int n in Enumerable.Range(1,7)){Check(d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyOneO,n,out var s)&&s.Level==n,"level"+n+" resolves its authored segment");Check(!d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyA,n,out _),"level"+n+" missing perspective is no-op");}
        Check(!d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyOneO,0,out _)&&!d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyOneO,8,out _),"acquisition0 and out-of-range8 have no story");
        d=Parse(Story(Segment(2,new[]{"31o","hotaru"},new[]{Line("thought")})));Check(d.TryFind(HbrMemoryUpgradeStoryPerspective.Hotaru,2,out _),"shared segment accepts Hotaru");Check(!d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyOneO,1,out _),"missing level is no-op");
        d=Parse(Story(Segment(1,new[]{"31o"},new[]{Line("narration")}),Segment(1,new[]{"30a"},new[]{Line()})));Check(d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyA,1,out _),"disjoint perspective variants at same level are valid");
        Check(Parse(Story()).Segments.Count==0,"empty future content file is valid and inactive");Check(!Parse(Story(Segment(1,new[]{"31o"},Array.Empty<object>()))).TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyOneO,1,out _),"empty lines are no-op");
        foreach(int level in new[]{0,8,-1})Reject(Story(Segment(level,new[]{"31o"},new[]{Line()})),"reject level "+level);
        Reject(Story(Segment(1,new[]{"unknown"},new[]{Line()})),"reject unknown perspective");Reject(Story(Segment(1,new[]{"31o","31O"},new[]{Line()})),"reject normalized duplicate perspective");
        Reject(Story(Segment(1,new[]{"31o"},new[]{Line()}),Segment(1,new[]{"31o"},new[]{Line()})),"reject overlapping segment key");
        foreach(string key in new[]{"choices","rewards","metCharacters","combat","nextPage","script"}){var s=Segment(1,new[]{"31o"},new[]{Line()});s[key]=Array.Empty<object>();Reject(Story(s),"reject gameplay field "+key);}
        var bad=Story();bad["characterId"]="../ruka";Reject(bad,"reject actor path traversal");bad=Story();bad["schemaVersion"]=2;Reject(bad,"reject unsupported schema");
        Reject(Story(Segment(1,new[]{"31o"},new[]{Line("invalid")})),"reuse original line kind validator");Reject(Story(Segment(1,new[]{"31o"},new[]{Line(text:" ")})),"reject blank narration");
        Reject(Story(Segment(1,new[]{"31o"},new[]{Line(background:"res://images/../escape.png")})),"reuse original image traversal validator");
        Reject(Story(Segment(1,new[]{"31o"},new[]{new{kind="dialogue",speaker="x",text="x",characters=new[]{new{id="ACTOR",image="res://images/f.png",height=3000}}}})),"reuse original portrait geometry validator");
        string duplicate=JsonSerializer.Serialize(Story()).Replace("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1");try{HbrMemoryUpgradeStoryDefinition.Parse(duplicate);throw new Exception("accepted duplicate");}catch(InvalidOperationException){Check(true,"reject duplicate JSON property");}
        d=Parse(Story(Segment(1,new[]{"31o"},new[]{Line(background:"res://images/fixture/second.png")})));d.TryFind(HbrMemoryUpgradeStoryPerspective.ThirtyOneO,1,out var segment);
        Check(segment.Page.ResolveBackground(segment.EffectivePresentation,0)=="res://images/fixture/second.png","original per-line background resolution retained");Check(segment.Page.Choices.Count==0&&segment.Page.MetCharacters.Count==0,"adapted reader page has no choices or encounter rewards");
        foreach(var perspective in Enum.GetValues<HbrMemoryUpgradeStoryPerspective>()){string key=HbrMemoryUpgradeStoryPerspectives.Key(perspective);Check(HbrMemoryUpgradeStoryPerspectives.TryParse(key.ToUpperInvariant(),out var parsed)&&parsed==perspective,"stable viewpoint key roundtrip: "+key);}
    }
    static int Main(string[] args)
    {
        string error=null;try{Cases();}catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        string report=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,boundary="Pure real production schema plus existing story validator. All fixture text exists only in this excluded validation assembly, no runtime story resource."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count);return error==null?0:1;
    }
}
