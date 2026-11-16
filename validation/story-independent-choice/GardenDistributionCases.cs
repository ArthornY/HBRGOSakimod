using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Localization;

static partial class StoryChecks
{
    static readonly string[] Venues=["cafe","cinema","gym","library","narvy","studio"];
    static string EncounterCsv(IEnumerable<string> ids)=>string.Join(",",ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    static string StoryFile(string venue)=>Path.Combine(Root,"story/events",venue+"_hbrgo_saki.json");
    static JsonNode OriginalProjection(JsonNode definition,JsonNode original)
    {
        JsonNode result=definition.DeepClone();
        Dictionary<string,JsonNode> oldPages=original["pages"]!.AsArray().ToDictionary(page=>page!["id"]!.GetValue<string>(),page=>page!,StringComparer.Ordinal);
        var pages=new JsonArray();
        foreach(JsonNode? page in definition["pages"]!.AsArray())
        {
            if(!oldPages.TryGetValue(page!["id"]!.GetValue<string>(),out JsonNode? old))continue;
            JsonNode kept=page.DeepClone();HashSet<string> choices=old["choices"]!.AsArray().Select(choice=>choice!["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            kept["choices"]=new JsonArray(page["choices"]!.AsArray().Where(choice=>choices.Contains(choice!["id"]!.GetValue<string>())).Select(choice=>choice!.DeepClone()).ToArray());
            pages.Add(kept);
        }
        result["pages"]=pages;
        return result;
    }
    static void GardenDistributionCases()
    {
        string baseline=Environment.GetEnvironmentVariable("HBR_GARDEN_SCENE_BASELINE_FILE")??
            Path.Combine(Root,"validation/story-independent-choice/fixtures/garden-scenes-before.json");
        JsonNode before=JsonNode.Parse(File.ReadAllText(baseline))!;
        JsonNode allocation=JsonNode.Parse(File.ReadAllText(Path.Combine(Root,"validation/story-independent-choice/fixtures/garden-scenes-allocation.json")))!;
        Type garden=Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrMemoryGarden",true)!;
        object[] entries=((IEnumerable)garden.GetField("Characters",F)!.GetValue(null)!).Cast<object>().ToArray();
        string[] roster=entries.Select(entry=>(string)entry.GetType().GetProperty("Id")!.GetValue(entry)!).Order(StringComparer.Ordinal).ToArray();
        Equal(roster.Length,56,"actual compiled private garden roster contains all56 card characters");
        Check(roster.Distinct(StringComparer.Ordinal).Count()==56,"compiled garden roster itself has unique stable encounter IDs");
        var current=new List<(string Id,string Venue,string Page)>();
        var original=new List<(string Id,string Venue,string Page)>();
        var counts=new Dictionary<string,int>(StringComparer.Ordinal);
        var advertised=new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string,string> names=HbrStoryCharacterCatalog.Entries.ToDictionary(entry=>entry.Id,entry=>entry.Name,StringComparer.Ordinal);
        MethodInfo preview=Mod.GetType("HBRGOSakiMod.Core.Story.HbrStoryChoicePreview",true)!.GetMethod("GetEncounterNames",F)!;
        foreach(string venue in Venues)
        {
            JsonNode json=JsonNode.Parse(File.ReadAllText(StoryFile(venue)))!;
            JsonNode old=before["venues"]![venue]!;
            Check(JsonNode.DeepEquals(OriginalProjection(json,old),old),venue+" retains exact original pages, met records, choice objects and rewards after removing only new routes/navigation");
            HbrStoryDefinition definition=HbrStoryDefinition.Parse(json.ToJsonString(),StoryFile(venue));
            HbrStoryDefinition baselineDefinition=HbrStoryDefinition.Parse(old.ToJsonString(),baseline+"/"+venue);
            foreach(var oldPage in baselineDefinition.Pages)
                Check(definition.GetPage(oldPage.Id).Choices.Take(oldPage.Choices.Count).Select(choice=>choice.Id).SequenceEqual(oldPage.Choices.Select(choice=>choice.Id)),venue+"/"+oldPage.Id+" keeps original choice indexes before appended navigation");
            var reachable=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<string>();pending.Enqueue(definition.StartPage);
            while(pending.TryDequeue(out string? id))
                if(reachable.Add(id))foreach(var choice in definition.GetPage(id).Choices)pending.Enqueue(choice.NextPage);
            foreach(var page in definition.Pages)
            {
                Check(page.Choices.Count<=6,venue+"/"+page.Id+" fits the existing six-choice menu limit");
                HbrStoryPage? oldPage=baselineDefinition.Pages.FirstOrDefault(priorPage=>priorPage.Id==page.Id);
                string locPage=HbrStoryDefinition.EventIdPrefix+definition.Id+".pages."+page.Id;
                if(oldPage==null)
                    Check(new LocString("events",locPage+".description").GetFormattedText()==string.Join("\n",page.Lines.Select(line=>line.Text)),venue+"/"+page.Id+" new native page description reads exact production events localization");
                foreach(string id in page.MetCharacters)current.Add((id,venue,page.Id));
                if(page.MetCharacters.Count>0)Check(reachable.Contains(page.Id),venue+" encounter page "+page.Id+" is reachable through actual choice edges");
                foreach(var choice in page.Choices)
                {
                    if(oldPage==null||oldPage.Choices.All(priorChoice=>priorChoice.Id!=choice.Id))
                    {
                        string key=locPage+".options."+choice.Id;
                        Check(new LocString("events",key+".title").GetFormattedText()==choice.Text,venue+"/"+page.Id+"/"+choice.Id+" new native option title reads production localization");
                        Check(new LocString("events",key+".description").GetFormattedText()==choice.Description,venue+"/"+page.Id+"/"+choice.Id+" new native option description reads production localization");
                    }
                    // The preview promises only this page and this choice's
                    // forced continuation, stopping before another decision.
                    var continuation=new List<HbrStoryPage>{page};var seen=new HashSet<string>(StringComparer.Ordinal){page.Id};
                    HbrStoryPage next=definition.GetPage(choice.NextPage);
                    while(seen.Add(next.Id))
                    {continuation.Add(next);if(next.Choices.Count!=1)break;next=definition.GetPage(next.Choices[0].NextPage);}
                    string[] ids=continuation.SelectMany(step=>step.MetCharacters).Distinct(StringComparer.Ordinal).ToArray();
                    string[] expected=ids.Select(id=>names[id]).ToArray();
                    string[] actual=((IEnumerable<string>)Invoke(preview,null,definition,page,choice)!).ToArray();
                    Check(actual.SequenceEqual(expected),venue+"/"+page.Id+"/"+choice.Id+" previews the exact catalog names for its forced branch");
                    advertised.UnionWith(ids);
                }
            }
            foreach(var page in baselineDefinition.Pages)
                foreach(string id in page.MetCharacters)original.Add((id,venue,page.Id));
            counts[venue]=definition.Pages.Sum(page=>page.MetCharacters.Count);
            Check(counts[venue] is 9 or 10,venue+" has9or10 unique encounter assignments");
        }
        Equal(current.Count,56,"six scenes contain exactly56 actual encounter occurrences");
        Check(current.Select(row=>row.Id).Order(StringComparer.Ordinal).SequenceEqual(roster),"actual six-scene encounter multiset equals the complete compiled roster exactly once");
        Check(current.Select(row=>row.Id).Distinct(StringComparer.Ordinal).Count()==56,"no character is repeated across pages or venues");
        Check(counts.Values.Order().SequenceEqual(new[]{9,9,9,9,10,10})&&counts.Values.Max()-counts.Values.Min()==1,"six scenes have the mathematically most even56-way distribution");
        Equal(original.Count,16,"fixed pre-change baseline has16 original encounter locations");
        var locations=current.ToDictionary(row=>row.Id,row=>(row.Venue,row.Page),StringComparer.Ordinal);
        foreach(var old in original)Check(locations[old.Id]==(old.Venue,old.Page),"original "+old.Id+" stays on its exact original venue/page");
        Equal(current.Count(row=>original.All(old=>old.Id!=row.Id)),40,"exactly40 previously missing characters were added");
        NewRoutes.Clear();
        foreach(var item in current.Where(row=>original.All(old=>old.Id!=row.Id)))
        {
            Check(allocation["assignment"]![item.Id]!.GetValue<string>()==item.Venue,item.Id+" retains the original static random venue allocation");
            HbrStoryDefinition definition=HbrStoryDefinition.Parse(File.ReadAllText(StoryFile(item.Venue)),StoryFile(item.Venue));HbrStoryPage page=definition.GetPage(item.Page);
            Check(page.MetCharacters.SequenceEqual(new[]{item.Id}),item.Id+" has its own single-character encounter page");
            Check(page.Choices.Count==1,item.Id+" has one independent confirmation choice");
            HbrStoryChoice reward=page.Choices.Single();Check(IsNewCharacterReward(reward.Rewards),item.Id+" grants exactly one Sense8and three-card choice, with no extra resource rewards");
            Check(definition.GetPage(reward.NextPage).Choices.Count==0,item.Id+" confirmation leads directly to a terminal page without returning to menus");
            var incoming=definition.Pages.SelectMany(source=>source.Choices.Where(choice=>choice.NextPage==page.Id).Select(choice=>(Page:source,Choice:choice))).ToArray();
            Check(incoming.Length==1&&IsEmptyReward(incoming[0].Choice.Rewards),item.Id+" incoming character option is unique navigation with no duplicate reward");
            NewRoutes.Add(new(item.Id,item.Venue,item.Page,reward.Rewards.Sense));
        }
        Check(advertised.Order(StringComparer.Ordinal).SequenceEqual(roster),"all56 characters appear in at least one truthful branch-choice preview");
        foreach(var other in before["otherStorySha256"]!.AsObject())
        {
            string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,other.Key))));
            Check(hash.Equals(other.Value!.GetValue<string>(),StringComparison.OrdinalIgnoreCase),"unrelated story remains byte-identical: "+other.Key);
        }
        Cases.Add(new{label="garden distribution",roster=roster.Length,counts,original=original.Count,added=40,baselineSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(baseline)))});
    }
    static void CinemaEncounterCases()
    {
        Reset("CINEMA");string opening=EncounterCsv(StoryA.Definition.GetPage("INITIAL").MetCharacters);
        string final=EncounterCsv(StoryA.Definition.GetPage("INITIAL").MetCharacters.Concat(StoryA.Definition.GetPage("SCENE").MetCharacters));
        Check(opening=="tomori"&&ManualA.MemoryGardenMetCharacters==opening&&ManualB.MemoryGardenMetCharacters==opening,"cinema preserves original tomori encounter on both owners' INITIAL page");
        var old=StoryA.CurrentOptions[0];Receive(A,0);
        Check(StoryA.IsFinished&&StoryA.CurrentStoryPageId=="SCENE"&&ManualA.MemoryGardenMetCharacters==final,"old cinema choice retains its exact original encounter set and terminal page");
        Check(StoryB.CurrentStoryPageId=="INITIAL"&&!StoryB.IsFinished&&ManualB.MemoryGardenMetCharacters==opening,"A original cinema completion leaves B at exact original INITIAL encounter set");
        Equal(ManualA.Gentleness,8,"cinema encounter additions preserve A original Gentleness8 reward");
        Choose(old);Check(ManualA.MemoryGardenMetCharacters==final&&ManualB.MemoryGardenMetCharacters==opening&&Offers.Count==1,"stale cinema option cannot duplicate reward or merge terminal encounters across owners");
        Receive(B,0);Check(StoryB.IsFinished&&ManualB.MemoryGardenMetCharacters==final&&Offers.Count==2,"B independently completes the same cinema terminal encounter set");
        Cases.Add(Snapshot("cinema terminal encounters"));
    }
}
