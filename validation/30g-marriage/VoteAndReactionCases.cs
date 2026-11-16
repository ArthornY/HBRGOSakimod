using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static readonly List<(decimal Raw,int Hits,decimal Destruction)> GDischarges=[];
    static uint GReserveChoice(){GChoiceReservations++;return (uint)GChoiceReservations;}
    static bool GReserveLeaf(ref uint __result){__result=GReserveChoice();return false;}
    static bool GSyncLeaf(){GChoiceSyncs++;return false;}
    static void GObserveOffers(object __result)
    {
        int[] ids=((IEnumerable)__result).Cast<object>().Select(x=>(int)x.GetType().GetProperty("EffectId")!.GetValue(x)!).ToArray();GUpgradeOffers.Add(ids);
        Check(ids.Length==3&&ids.Distinct().Count()==3,"72 vote uses the real garden catalog's three distinct upgrade offers");
    }
    static bool GDischargeLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {if(__instance.ModelSource is CardModel)return true;__result=GDischargeBody(__instance);return false;}
    static async Task<AttackCommand> GDischargeBody(AttackCommand command)
    {
        object scope=Call("HighHitDamageVisualHelper","BeginAttack",command)!;
        try
        {
            decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;int hits=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!;
            object damage=Call("HighHitDamageVisualHelper","BeginDamage",ValueProp.Move,Hero,null)!;
            try{GDischarges.Add((raw,hits,(decimal)Call("ThirtyGBondHelper","GetDestructionGainMultiplier",null,Enemy,Hero,ValueProp.Move)!));}finally{Call("HighHitDamageVisualHelper","CloseDamage",damage);}
            Check(command.Attacker==Hero&&command.IsMultiTargeted&&command.ModelSource is not CardModel,"72 discharge is holder's normal all-enemy attack without card-play source");
            Equal((decimal)Call("ThirtyGBondHelper","GetDestructionGainMultiplier",null,Enemy,Hero,ValueProp.Unpowered)!,1m,"72 discharge coefficient does not leak to an unpowered nested reaction");
            for(int i=0;i<hits;i++)
            {
                var result=new MegaCrit.Sts2.Core.Entities.Creatures.DamageResult(Enemy,ValueProp.Move);Set(result,"<UnblockedDamage>k__BackingField",1);
                Call("CombatAttackHitHelper","Bind",new[]{result},Hero,null,null,0m,null);await NewGiven(null!,result);
            }
            return await Task.FromResult(command);
        }
        finally{Call("HighHitDamageVisualHelper","CloseAttack",scope);}
    }
    static async Task GReactionCases()
    {
        foreach(int reaction in new[]{1,2,3})
        {
            GReset(6);GDischarges.Clear();var target=GOrigin<NarcissismIsFineHbrgoSaki>();var mirror=GMirror(target);GSeedFor(6,reaction);var gauge=(Overclock)ModelDb.Relic<Overclock>().ToMutable();Set(gauge,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(gauge).ToList());await GPrepare();
            Check(GGraduated(mirror),"72 every d3 reaction still graduates the target; reaction"+reaction);
            Check(((ThirtyGProposalPresentation)GPresentations.Single()).Reaction==reaction,"72 presentation payload preserves independent raw d3 reaction"+reaction);
            if(reaction==1)
            {
                await GRelic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(GGraduated(mirror),"72 reaction1 stays graduated on first native player-turn-start hook");
                Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await GRelic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(!GGraduated(mirror),"72 reaction1 releases at the second real own-turn hook");Check(mirror.CanPlay(out _,out _),"72 released real card recovers its native manual-play eligibility");
            }
            if(reaction==2){Check(GDischarges.Single()==(3.06m,3,.4m),"72 reaction2 uses actual51percent times3 with original total coefficient1point2");Equal(gauge.PreciseCharge,7.5m,"72 real after-damage hooks charge2point5OD for each of three discharge hits");}
            else Equal(GDischarges.Count,0,"72 other reactions do not discharge");
            Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();mirror=GMirror(target);await GPrepare();Equal(GHeart(mirror),reaction==3?2:1,"72 only reaction3 doubles next encounter's heart");
        }
        GReset(6);var kiryu=GDeck[1];GMirror(kiryu);GSeedFor(6,3);await GPrepare();Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();GMirror(kiryu);await GPrepare();Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();var heart=GMirror(kiryu);await GPrepare();
        Equal(GHeart(heart),2,"72 Kiryu plus reaction3 still caps extra plays at2 instead of stacking to4");
    }
    static async Task GVoteCases()
    {
        GReset(4);GChoiceReservations=GChoiceSyncs=0;GUpgradeOffers.Clear();var first=GOrigin<NarcissismIsFineHbrgoSaki>();var second=GOrigin<PaintTheWorldMyColorHbrgoSaki>();
        Call("ThirtyGProposalState","EnsureIdentities",Player);string a=GId(first),b=GId(second);
        GUpdateData(json=>{json["RoundTargets"]=new JsonArray(a,a,b);});
        await (Task)Call("ThirtyGBondHelper","ResolveVote",new ThrowingPlayerChoiceContext(),GRelic)!;
        Equal(first.CurrentUpgradeLevel,1,"72 duplicate proposals to the same physical card upgrade it only once per vote");Equal(second.CurrentUpgradeLevel,1,"72 another physical round target is also permanently upgraded once");
        Check(GChoiceReservations==2&&GChoiceSyncs==2&&GUpgradeOffers.Count==2,"72 each eligible permanent target follows the real synchronized three-choice upgrade coordinator once");
        Equal(((ThirtyGCollectionRelic)GRelic).Popularity,1,"72 first vote resets popularity to next round's initial1");Equal(((ThirtyGCollectionRelic)GRelic).NextRoundStart,1,"72 first vote records initial1");
        for(int i=0;i<12;i++)await (Task)Call("ThirtyGBondHelper","ResolveVote",new ThrowingPlayerChoiceContext(),GRelic)!;
        Equal(((ThirtyGCollectionRelic)GRelic).Popularity,9,"72 repeated rounds cap the next popularity start at9");Equal(((ThirtyGCollectionRelic)GRelic).NextRoundStart,9,"72 next-round start never exceeds9");
        for(int i=1;i<7;i++){Native(first,"PrepareUpgradeEffect",i);first.UpgradeInternal();first.FinalizeUpgradeInternal();}
        GUpdateData(json=>{json["RoundTargets"]=new JsonArray(a);});int choices=GChoiceReservations;await (Task)Call("ThirtyGBondHelper","ResolveVote",new ThrowingPlayerChoiceContext(),GRelic)!;
        Check(first.CurrentUpgradeLevel==7&&GChoiceReservations==choices,"72 fully upgraded physical targets are skipped without opening another choice");
        GReset(2);first=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(first);for(int i=0;i<12;i++)Native(GRelic,"GainPopularity");GSeedFor(2,san:25);await GPrepare();
        Check(first.CurrentUpgradeLevel==0&&((ThirtyGCollectionRelic)GRelic).Popularity==13,"72 popularity above10 continues accumulating below four permanent member kinds");
        Call("ThirtyGBondHelper","EndBattle",Player);GOrigin<MarriageMonakaFixture>();GOrigin<MarriageChieFixture>();GNextBattle();GMirror(first);second=GOrigin<PaintTheWorldMyColorHbrgoSaki>();GMirror(second);GResetSan();await GPrepare();
        Check(first.CurrentUpgradeLevel==1&&second.CurrentUpgradeLevel==1&&((ThirtyGCollectionRelic)GRelic).Popularity==1,"72 first eligible proposal after reaching four kinds upgrades the whole saved round then discards overflow popularity");
        GReset(4);first=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(first);Native(GRelic,"GainPopularity");for(int i=0;i<8;i++)Native(GRelic,"GainPopularity");GSeedFor(4,san:25);await GPrepare();
        Check(first.CurrentUpgradeLevel==1&&((ThirtyGCollectionRelic)GRelic).Popularity==1,"72 actual tenth proposal invokes vote and permanent upgrade before combat opening finishes");
    }
}
