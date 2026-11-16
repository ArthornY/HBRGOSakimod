using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Rewards;

static partial class StoryChecks
{
    sealed record NewRoute(string Id,string Venue,string Page,string Sense);
    static readonly List<NewRoute> NewRoutes=[];
    static readonly string[] SenseNames=["LeadershipCharm","Talent","Gentleness","MentalStrength","Simplicity","Madness"];
    static int[] SenseValues(ElectronicSoldierManual manual)=>[manual.LeadershipCharm,manual.Talent,manual.Gentleness,manual.MentalStrength,manual.Simplicity,manual.Madness];
    static bool NoExtraRewards(HbrStoryRewards reward)=>reward.CardRarity=="default"&&reward.Potions==0&&reward.CardRemovals==0&&reward.CardUpgrades==0&&!reward.HealFull&&reward.MaxDp==0;
    static bool IsEmptyReward(HbrStoryRewards reward)=>reward.Sense==""&&reward.SenseAmount==0&&reward.CardChoices==0&&NoExtraRewards(reward);
    static bool IsNewCharacterReward(HbrStoryRewards reward)=>SenseNames.Any(name=>name==reward.Sense)&&reward.SenseAmount==8&&reward.CardChoices==3&&NoExtraRewards(reward);
    static IReadOnlyList<string> RouteTo(HbrStoryDefinition definition,string target)
    {
        var queue=new Queue<(string Page,List<string> Choices)>();queue.Enqueue((definition.StartPage,[]));var seen=new HashSet<string>(StringComparer.Ordinal);
        while(queue.TryDequeue(out var row))
        {
            if(!seen.Add(row.Page))continue;if(row.Page==target)return row.Choices;
            foreach(var choice in definition.GetPage(row.Page).Choices)queue.Enqueue((choice.NextPage,[..row.Choices,choice.Id]));
        }
        throw new Exception("No actual choice route to "+target);
    }
    static void FollowNewRoute(Player player,HbrStoryEvent story,NewRoute route)
    {
        foreach(string id in RouteTo(story.Definition,route.Page))
        {
            int index=story.CurrentStoryPage.Choices.Select((choice,index)=>(choice,index)).Single(row=>row.choice.Id==id).index;
            Receive(player,index);
        }
        Check(story.CurrentStoryPageId==route.Page,route.Id+" native received-message route reaches the correct owner-specific role page");
    }
    static int[] ExpectedSense(string sense){var values=new int[6];values[Array.IndexOf(SenseNames,sense)]=8;return values;}
    static void NewCharacterRewardCases()
    {
        Equal(NewRoutes.Count,40,"all40 new independent reward branches are exercised natively");
        foreach(NewRoute route in NewRoutes.OrderBy(row=>row.Id,StringComparer.Ordinal))
        {
            Reset(route.Venue.ToUpperInvariant());string initial=EncounterCsv(StoryA.CurrentStoryPage.MetCharacters);
            string expected=EncounterCsv(StoryA.CurrentStoryPage.MetCharacters.Concat(new[]{route.Id}));
            FollowNewRoute(A,StoryA,route);
            Check(ManualA.MemoryGardenMetCharacters==expected&&ManualB.MemoryGardenMetCharacters==initial,route.Id+" encounter set contains only its role plus existing public entry characters, never another newcomer or teammate state");
            Check(SenseValues(ManualA).All(value=>value==0)&&SenseValues(ManualB).All(value=>value==0)&&Offers.Count==0,route.Id+" incoming menu navigation grants no Sense or reward");
            EventOption confirmation=StoryA.CurrentOptions.Single();Receive(A,0);
            int[] sense=ExpectedSense(route.Sense);
            Check(SenseValues(ManualA).SequenceEqual(sense)&&SenseValues(ManualB).All(value=>value==0),route.Id+" confirms exactly8of its declared random Sense and none of the other five or B's Senses");
            Check(Offers.Count==1&&Offers[0].Player==A&&Offers[0].Rewards is[CardReward cards]&&cards.Player==A&&Options(cards)==3,route.Id+" native card reward retains ownerA and exactly three options");
            Check(StoryA.IsFinished&&!StoryB.IsFinished&&StoryB.CurrentStoryPageId==StoryB.Definition.StartPage&&ManualA.MemoryGardenMetCharacters==expected&&ManualB.MemoryGardenMetCharacters==initial,route.Id+" confirmation ends only A's route and preserves both exact encounter sets");
            Choose(confirmation);
            ((Func<Task>)typeof(EventOption).GetProperty("OnChosen",F)!.GetValue(confirmation)!)().GetAwaiter().GetResult();
            Check(SenseValues(ManualA).SequenceEqual(sense)&&Offers.Count==1&&ManualA.MemoryGardenMetCharacters==expected,route.Id+" repeated native click and stale direct callback cannot grant Sense, cards or extra encounters twice");
            FollowNewRoute(B,StoryB,route);Receive(B,0);
            Check(SenseValues(ManualA).SequenceEqual(sense)&&SenseValues(ManualB).SequenceEqual(sense)&&StoryB.IsFinished,route.Id+" same role remains independently claimable once by B without changing A");
            Check(Offers.Count==2&&Offers[1].Player==B&&Offers[1].Rewards is[CardReward second]&&second.Player==B&&Options(second)==3&&ManualB.MemoryGardenMetCharacters==expected,route.Id+" B's card offer and encounter set remain correctly owned");
        }
        foreach(var group in NewRoutes.GroupBy(route=>route.Venue))
        {
            NewRoute first=group.First(),second=group.Skip(1).First();Reset(first.Venue.ToUpperInvariant());
            string[] commonIds=StoryA.CurrentStoryPage.MetCharacters.ToArray();
            FollowNewRoute(A,StoryA,first);FollowNewRoute(B,StoryB,second);Receive(A,0);Receive(B,0);
            Check(ManualA.MemoryGardenMetCharacters==EncounterCsv(commonIds.Append(first.Id))&&ManualB.MemoryGardenMetCharacters==EncounterCsv(commonIds.Append(second.Id)),group.Key+" two owners selecting different new branches retain exact disjoint newcomer records");
            Check(SenseValues(ManualA).SequenceEqual(ExpectedSense(first.Sense))&&SenseValues(ManualB).SequenceEqual(ExpectedSense(second.Sense)),group.Key+" different new branches grant their own declared Sense rewards only");
        }
        Cases.Add(new{label="new independent role rewards",branches=NewRoutes.Count,rewards=NewRoutes.Select(row=>new{row.Id,row.Venue,row.Page,row.Sense,amount=8,cardChoices=3}).ToArray()});
    }
}
