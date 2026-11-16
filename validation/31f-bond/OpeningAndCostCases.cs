using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
static partial class EffectChecks
{
    static bool FMarkerNotLocal(ref bool __result){__result=false;return false;}
    static void FMetadataCases()
    {
        Check(Enum.GetValues<ThirtyOneFMember>().Length==6,"79 six exact future31F members are defined");
        Type[] actual=Mod.GetTypes().Where(t=>!t.IsAbstract&&typeof(CardModel).IsAssignableFrom(t)&&typeof(IThirtyOneFMemberCard).IsAssignableFrom(t)).ToArray();
        var expected=new Dictionary<string,string>{{"AsTheYoungLadyCommandsHbrgoSaki","mion"},{"LetMeShowYouClassHbrgoSaki","kanata"},{"ThisIsMyStageHbrgoSaki","shiki"},{"LeaveAnyBattlefieldToMeHbrgoSaki","chiroru"},{"LegacyFlashHbrgoSaki","inori"},{"FollowBehindMeHbrgoSaki","maki"}};
        Check(actual.Select(t=>t.Name).ToHashSet().SetEquals(expected.Keys),"80-82 production contains exactly the six authorized31F cards, retaining future member markers");
        Type content=Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack",true)!;
        var contentCards=(Type[])content.GetField("CardTypes",F)!.GetValue(null)!;
        var internalRelics=(Type[])content.GetField("InternalRelicTypes",F)!.GetValue(null)!;
        var gardenCards=(IReadOnlyList<Type>)Helper("HbrMemoryGardenCardCatalog").GetProperty("CardTypes",F)!.GetValue(null)!;
        Check(actual.All(t=>contentCards.Contains(t)&&gardenCards.Contains(t))&&internalRelics.Contains(typeof(ThirtyOneFCollection)),"80-82 actual content/garden catalogs register all six cards and internal31F marker");
        var h=new HarmonyLib.Harmony(ProductionId+".31f-real-marker");
        MethodInfo local=typeof(MegaCrit.Sts2.Core.Context.LocalContext).GetMethod("IsMe",F,new[]{typeof(MegaCrit.Sts2.Core.Entities.Players.Player)})!;
        h.Patch(local,prefix:new HarmonyLib.HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FMarkerNotLocal),F)!));
        try
        {
            foreach(Type type in actual)
            {
                if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
                FReset(0);var original=FCard(type);MisatoPermanent(original);
                Check((string)Call("HbrMemoryGarden","CharacterIdOf",original)! == expected[type.Name],"80-82 actual garden identity "+type.Name);
                Set(Player,"_relics",Player.Relics.Where(r=>r is not ThirtyOneFCollection).ToList());
                Set(Player,"<RelicGrabBag>k__BackingField",new MegaCrit.Sts2.Core.Runs.RelicGrabBag());Set(RunState,"<SharedRelicGrabBag>k__BackingField",new MegaCrit.Sts2.Core.Runs.RelicGrabBag());
                ((Task)Call("TeamCollectionRelics","EnsureObtained",Player)!).GetAwaiter().GetResult();
                var acquired=Player.GetRelic<ThirtyOneFCollection>();
                Check(acquired?.DisplayAmount==1&&acquired.Owner==Player,"80-82 first real permanent member acquires native31F marker "+type.Name);
                ((Task)Call("TeamCollectionRelics","EnsureObtained",Player)!).GetAwaiter().GetResult();Check(Player.Relics.OfType<ThirtyOneFCollection>().Count()==1,"80-82 repeated marker notification remains idempotent "+type.Name);
            }
        }
        finally{h.UnpatchAll(h.Id);}

        for(int count=0;count<=6;count++)
        {
            var relic=FReset(count);Equal(relic.DisplayAmount,count,"79 permanent native counter "+count);
            Equal((bool)Call("ThirtyOneFBondHelper","HasSharedCosts",Player)!?1:0,count>=4?1:0,"79 four-kind threshold "+count);
            Equal((bool)Call("ThirtyOneFBondHelper","HasBulwark",Player)!?1:0,count>=6?1:0,"79 six-kind threshold "+count);
            if(count>0){FOrigin(0);Equal(relic.DisplayAmount,count,"79 duplicate physical member does not add a kind");}
        }
        FReset(6);var marker=Player.GetRelic<ThirtyOneFCollection>()!;
        string title=marker.Title.GetFormattedText(),description=marker.DynamicDescription.GetFormattedText();
        Check(title=="31F"&&description.Contains("25920")&&description.Contains("DP归零")&&!description.Contains("执事的嗜好"),"79+88 actual plain squad title and numeric counter format without old bond name");
        Check(marker.PackedIconPath=="res://images/relics/thirty_one_f.png","79 actual marker points to approved original logo");
        var png=File.ReadAllBytes(Path.Combine(RepositoryRoot,"images/relics/thirty_one_f.png"));
        Check(Convert.ToHexString(SHA256.HashData(png))=="8B75B5E8BAC924AC4775DE66C772A8C5E4367A427B6963D9BF1788AEA97CC52A","79 420pixel logo is exact supplied original bytes");
        using var provenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,"images/relics/thirty_one_f.sources.json")));
        Check(provenance.RootElement.GetProperty("size")[0].GetInt32()==420&&provenance.RootElement.GetProperty("size")[1].GetInt32()==420,"79 provenance retains original420x420 dimensions");
    }
    static async Task FOpeningCases()
    {
        var orders=new HashSet<string>();
        for(int seed=0;seed<5;seed++)
        {
            var relic=FReset(2);Set(RunState,"<Rng>k__BackingField",new RunRngSet("31f-opening-"+seed));
            var normal1=FCard(typeof(FOtherCard),hand:false);var members=new List<CardModel>();
            for(int i=0;i<8;i++)members.Add(FCard(FMembers[i%2],hand:false));var normal2=FCard(typeof(FOtherCard),hand:false);
            var innate=FCard(typeof(FOtherCard),hand:false);innate.AddKeyword(CardKeyword.Innate);
            await FSetupTurn();var hand=Player.PlayerCombatState!.Hand.Cards.ToArray();var draw=Player.PlayerCombatState.DrawPile.Cards.ToArray();
            Check(hand.Length==5&&ReferenceEquals(hand[0],innate),"79 true native SetupPlayerTurn keeps vanilla Innate before prepared31F; seed"+seed);
            Check(hand.Skip(1).All(c=>c is IThirtyOneFMemberCard),"79 all remaining initial slots draw31F first; seed"+seed);
            Check(draw.Take(4).All(c=>c is IThirtyOneFMemberCard)&&ReferenceEquals(draw[^2],normal1)&&ReferenceEquals(draw[^1],normal2),"79 excess31F stays above other cards with other relative order unchanged; seed"+seed);
            Equal(RngCount(RunState.Rng.CombatCardSelection),7,"79 actual shuffle uses exactly n-1 synchronized selection samples; seed"+seed);
            int before=RngCount(RunState.Rng.CombatCardSelection);await relic.BeforeHandDraw(Player,new ThrowingPlayerChoiceContext(),Combat);
            Equal(RngCount(RunState.Rng.CombatCardSelection),before,"79 repeated opening callback does not reshuffle");
            orders.Add(string.Join(",",hand.Skip(1).Select(c=>c.GetType().Name)));
        }
        Check(orders.Count>1,"79 different real seeds yield more than one internal member ordering");
        FReset(1);var first=FCard(typeof(FOtherCard),hand:false);FCard(typeof(FMember0),hand:false);for(int i=0;i<5;i++)FCard(typeof(FOtherCard),hand:false);
        await FSetupTurn();Check(ReferenceEquals(Player.PlayerCombatState!.Hand.Cards[0],first),"79 one kind never reorders native opening draw");
        FReset(2,false);first=FCard(typeof(FOtherCard),hand:false);FCard(typeof(FMember0),hand:false);for(int i=0;i<5;i++)FCard(typeof(FOtherCard),hand:false);
        await FSetupTurn();Check(ReferenceEquals(Player.PlayerCombatState!.Hand.Cards[0],first),"79 legacy mode does not gain HBR-only opening priority");
    }
    static async Task FTurnDraw(decimal count=0m,bool hand=true)=>await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(),count,Player,fromHandDraw:hand);
    static int FCost(CardModel card)=>card.EnergyCost.GetWithModifiers(CostModifiers.All);
    static async Task FCostCases()
    {
        FReset(4);Set(Player.PlayerCombatState!,"_energy",0);var high=FCard(typeof(FMember3));var low=FCard(typeof(FMember1));var x=FCard(typeof(FMemberX));var blocked=FCard(typeof(FMemberBlocked));var other=FCard(typeof(FOtherCard));
        Check(!high.CanPlay(out UnplayableReason lack,out _)&&(lack&UnplayableReason.EnergyCostTooHigh)!=0,"79 fixture has no energy but eligible cost must still participate");
        await FTurnDraw();Equal(FCost(high),1,"79 zero-count true native starting draw still equalizes4to1");Equal(FCost(low),1,"79 lowest card never increases");
        Check(x.EnergyCost.CostsX&&blocked.EnergyCost.Canonical==0&&FCost(other)==5,"79 X, logic-blocked zero and nonmember excluded from comparison: X="+x.EnergyCost.CostsX+" blocked="+blocked.EnergyCost.Canonical+" other="+FCost(other));
        var mid=FCard(typeof(FMember2),hand:false);await FTurnDraw(1m,false);Equal(FCost(mid),3,"79 midturn non-hand draw does not capture newly-entered31F");
        await FTurnDraw();Equal(FCost(mid),3,"79 repeated true draw same turn cannot include midturn card");
        high.EnergyCost.AddThisTurn(2);Equal(FCost(high),3,"79 later vanilla local cost modifier applies in original order");high.EnergyCost.EndOfTurnCleanup();Equal(FCost(high),4,"79 actual native cleanup restores base4");
        FReset(4);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));for(int i=2;i<CardPile.MaxCardsInHand;i++)FCard(typeof(FOtherCard));await FTurnDraw(5m);Equal(FCost(high),1,"79 hand-full native Draw early return still applies four-kind boundary");
        FReset(4);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));FBlockDraw=true;await FTurnDraw(5m);Equal(FCost(high),1,"79 ShouldDraw-denied native return still applies boundary");FBlockDraw=false;
        FReset(4);high=FCard(typeof(FMember3));FCard(typeof(FMemberX));FCard(typeof(FMemberBlocked));await FTurnDraw();Equal(FCost(high),4,"79 fewer than two eligible members has no effect");
        FReset(3);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));await FTurnDraw();Equal(FCost(high),4,"79 three kinds cannot activate shared costs");
        FReset(4,false);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));await FTurnDraw();Equal(FCost(high),4,"79 legacy mode does not gain four-kind cost changes");
        foreach(var row in new[]{(Offset:1m,Factor:1m),(Offset:2m,Factor:1m),(Offset:0m,Factor:.5m),(Offset:-2m,Factor:1m),(Offset:0m,Factor:0m)})
        {
            FReset(4);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));var power=Power<FGlobalCostPower>(Hero,1);power.Offset=row.Offset;power.Factor=row.Factor;int minimum=FCost(low);
            await FTurnDraw();Equal(FCost(high),minimum,"79 real native global add/half/discount applied exactly once "+row);Equal(FCost(low),minimum,"79 current lowest retained under global hook "+row);
            high.EnergyCost.EndOfTurnCleanup();Equal(FCost(high),Math.Max(0,(int)((4+row.Offset)*row.Factor)),"79 native cleanup removes only this-turn override "+row);
        }
        FReset(4);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));var special=Power<FGlobalCostPower>(Hero,1);special.Offset=1m;special.Factor=1m;special.HighOnly=true;
        await FTurnDraw();Equal(FCost(high),1,"79 per-card global plus1 is calibrated instead of applied twice");Equal(high.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"79 per-card global plus1 resolves local preimage0 for final1");
        FReset(4);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));special=Power<FGlobalCostPower>(Hero,1);special.Factor=1m;special.FloorHigh=true;await FTurnDraw();
        Equal(FCost(high),4,"79 unreachable third-party fixed floor leaves high card unchanged rather than raising or looping");Check(special.Probes<=80,"79 fixed-floor calibration remains bounded including ordinary preview queries");
        FReset(4);high=FCard(typeof(FMember3));FNestedProbe=high;FCard(typeof(FMember1),hand:false);FCard(typeof(FMember2),hand:false);Power<FNestedDrawPower>(Hero,1);FNestedArmed=true;await FTurnDraw(1m);
        Equal(FNestedObserved,4,"79 nested true hand-draw does not prematurely commit outer boundary");Check(Player.PlayerCombatState!.Hand.Cards.All(c=>FCost(c)==1),"79 outer completion includes all nested initial-draw cards exactly once");
        FReset(4);high=FCard(typeof(FMember3));low=FCard(typeof(FMember1));await FTurnDraw();var inserted=FCard(typeof(FMember2));
        using(MisatoExtra(false)){await FTurnDraw();Equal(FCost(inserted),1,"79 personal extra turn uses a distinct postdraw receipt");}
        var later=FCard(typeof(FMember3));await FTurnDraw();Equal(FCost(later),4,"79 return to ordinary turn preserves earlier receipt");
        using(MisatoExtra(true)){await FTurnDraw();Equal(FCost(later),1,"79 OD extra turn uses its own receipt");}
    }
}
