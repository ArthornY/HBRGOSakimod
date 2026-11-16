using System.Reflection;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static async Task YCFormationCases()
    {
        for(int formation=0;formation<3;formation++)foreach(bool amplified in new[]{false,true})
        {
            YCReset();YCChoices=[formation];var card=YCard(amplified?new[]{1}:Array.Empty<int>());
            Check(card.Type==CardType.Skill&&card.Rarity==CardRarity.Rare&&FCost(card)==1&&card.Keywords.Contains(CardKeyword.Exhaust),"93 base rare1skill retains exhaust formation="+formation);
            await ChasePlay(card);Check(card.Pile==Player.PlayerCombatState!.ExhaustPile&&YCOptionCount==3,"93 native wrapper exhausts after three-choice selection");
            decimal bonus=amplified?10m:0m;
            if(formation==0)
            {
                var p=Hero.GetPower<HbrAttackUpPower>()!;Check(p!=null&&p.Amount==1,"93 crane grants one attack application");
                Equal(YCPotency(p),(decimal)Native(p,"ResolvePotency",50m+bonus,Hero,Hero)!,"93 crane uses current common buff formula");
            }
            else if(formation==1)
            {
                var rate=Hero.GetPower<HbrCriticalRateUpPower>()!;var damage=Hero.GetPower<HbrCriticalDamageUpPower>()!;
                Check(rate!=null&&damage!=null&&rate.Amount==1&&damage.Amount==1,"93 fish grants one independent application of both critical powers");
                Equal(YCPotency(rate),(decimal)Native(rate,"ResolvePotency",50m+bonus,Hero,Hero)!,"93 fish critical rate uses common formula");Equal(YCPotency(damage),45m+bonus,"93 fish critical damage retains its original percent basis");
            }
            else
            {
                var p=Hero.GetPower<HbrDefenseUpPower>()!;Check(p!=null&&p.Amount==1,"93 square has one enemy-end duration");
                Equal(YCPotency(p),(decimal)Native(p,"ResolvePotency",30m+bonus,Hero,Hero)!,"93 square uses common defense formula");
                await p.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Player,new[]{Hero});Check(Hero.Powers.Contains(p),"93 square survives player end");await p.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy});Check(!Hero.Powers.Contains(p),"93 square expires at next enemy end");
            }
            Check(YCStrikes.Count==0,"93 plain formation is not an attack command");
        }
        YCReset();var flash=YCard(0);Equal(FCost(flash),0,"93 Flash reduces1to0 without losing Exhaust");
        YCReset();YCChoices=[0];await ChasePlay(YCard(5));Equal(Hero.GetPowerAmount<HbrComboSmallPower>(),5,"93 Seventh Strategy grants five small-combo uses after crane selection");
        YCReset();YCChoices=[1];await ChasePlay(YCard(5));Check(!Hero.HasPower<HbrComboSmallPower>(),"93 Seventh Strategy does not attach combo to fish formation");
        YCReset();YCChoices=[0,1];await ChasePlay(YCard(6));Check(Hero.HasPower<HbrAttackUpPower>()&&Hero.HasPower<HbrCriticalRateUpPower>()&&Hero.HasPower<HbrCriticalDamageUpPower>(),"93 native multi-choice applies two different formations");
        YCReset();YCChoices=[2,3];await ChasePlay(YCard(4,6));Check(YCOptionCount==4&&Hero.HasPower<HbrDefenseUpPower>()&&Hero.GetPowerAmount<YingxiaRegenerationFormationPower>()==3,"93 regeneration is a fourth valid dual-formation choice");
        YCReset();YCRejectSelection=true;await ChasePlay(YCard(2,3));Check(YCStrikes.Count==0&&!Enemy.HasPower<HbrDefenseDownPower>(),"93 abandoned/empty selector cannot run attached benefits or attacks");
        YCReset();YCChoices=[0,0];await ChasePlay(YCard(6));Check(!Hero.HasPower<HbrAttackUpPower>(),"93 duplicate invalid selections cannot turn dual choice into duplicate buffs");
        YCReset();YCChoices=[2];var attack=YCard(2);await ChasePlay(attack);Check(YCStrikes.Single()is{Raw:2.82m,Hits:4}&&YCStrikes.Single().Card.Type==CardType.Skill,"93 Sleeping Dragon is exactly47percent four hits while the played card stays Skill");
        Check(YCSegments.Count==4&&YCSegments.All(s=>s.Coefficient==.5m),"93 authored random attack uses original destruction1point5 across four native segments");
        YCReset();YCChoices=[2];var gauge=NewGauge();await ChasePlay(YCard(2));Equal(YCAttackOdGains.Single(),10m,"93 actual skill-attached four-hit attack accumulates standard2point5OD per connected hit");Equal(gauge.PreciseCharge,15m,"93 normal skill-use5OD remains separate from ten attack-hit OD");
        Check(Enemy.GetPower<HbrDefenseDownPower>()is{Amount:1},"93 Sleeping Dragon applies one30defense-down after its attack");
        Equal((decimal)Call("YingxiaFormationCombatHelper","GetDestructionGainMultiplier",attack,Enemy,Hero,ValueProp.Move)!,1,"93 stale source outside direct command does not leak destruction coefficient");
        YCReset();YCChoices=[2];await ChasePlay(YCard(3));Check(Enemy.GetPower<HbrDefenseDownPower>()is{Amount:1}&&YCStrikes.Count==0,"93 Mantis applies all-enemy defense-down independently of optional attack");
        var down=Enemy.GetPower<HbrDefenseDownPower>()!;Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",down)!,(decimal)Call("HbrPercentDebuffHelper","ResolveBasis",down,20m,Hero,Enemy)!,"93 Mantis20uses actual common attribute formula");
        YCReset();Power<ArtifactPower>(Enemy,1);YCChoices=[2];await ChasePlay(YCard(3));Check(!Enemy.HasPower<HbrDefenseDownPower>()&&Enemy.GetPowerAmount<ArtifactPower>()==0,"93 original Artifact protects against explicit Mantis debuff");
        YCReset();var peer=SupportTeammate();Creature pet=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);
        Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature,pet,phantom});YCChoices=[0];await ChasePlay(YCard());
        Check(Hero.HasPower<HbrAttackUpPower>()&&peer.Creature.HasPower<HbrAttackUpPower>()&&phantom.HasPower<HbrAttackUpPower>()&&!pet.HasPower<HbrAttackUpPower>(),"93 team formation targets both players and owned main phantom, excludes ordinary pet");
        YCReset(false);await ChasePlay(YCard(0,1,2,3,4,5,6));Check(YCStrikes.Count==0&&YCOptionCount==0&&!Hero.HasPower<HbrAttackUpPower>(),"93 legacy mode has no new HBR selection, buffs or damage");
    }
    static void YCSaveCache(Type type)
    {
        Assembly native=typeof(SavedProperties).Assembly;
        if(native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache")is{} old)old.GetMethod("InjectTypeIntoCache",F)!.Invoke(null,new[]{type});
        else{Type cache=native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,new[]{type});cache.GetField("_initialized",F)!.SetValue(null,true);}
    }
    static T YCSaveRestore<T>(T original) where T:PowerModel
    {
        YCSaveCache(typeof(T));var options=new JsonSerializerOptions{IncludeFields=true};string json=JsonSerializer.Serialize(SavedProperties.From(original),options);
        var saved=JsonSerializer.Deserialize<SavedProperties>(json,options)!;var clone=(T)ModelDb.Power<T>().MutableClone();clone.ApplyInternal(original.Owner,original.Amount);saved.Fill(clone);return clone;
    }
}
