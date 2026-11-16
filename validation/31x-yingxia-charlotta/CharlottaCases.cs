using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static int YCJealous(LoveCharlottaHbrgoSaki card)=>(int)Call("CharlottaLoveCombatHelper","JealousyCount",card)!;
    static async Task YCCharlottaCases()
    {
        foreach(int paid in new[]{0,1,3,5})foreach(bool flash in new[]{false,true})
        {
            YCReset();var card=CCard(flash?new[]{0}:Array.Empty<int>());Set(Player.PlayerCombatState!,"_energy",paid);
            Check(card.EnergyCost.CostsX&&card.Type==CardType.Attack&&card.Rarity==CardRarity.Rare&&!card.Keywords.Contains(CardKeyword.Exhaust),"94 native X rare attack nonexhausts paid="+paid+" flash="+flash);
            await ChasePlay(card);Equal(Player.PlayerCombatState.Energy,0,"94 native X payment spends actual energy once");Check(card.Pile==Player.PlayerCombatState.DiscardPile,"94 normal native result pile is Discard");
            int x=paid+(flash?1:0);
            if(x==0)Check(YCStrikes.Count==0&&YCSegments.Count==0,"94 zero paidX without bonuses never constructs attack and cannot trigger minimum1");
            else
            {
                Check(YCStrikes.Single().Raw==3.84m*x&&YCStrikes.Single().Hits==3&&YCStrikes.Single().Targets.SequenceEqual(new[]{Enemy}),"94 each of three hits is64percent times actual paidX plus Flash");
                Check(YCSegments.Count==3&&YCSegments.All(s=>s.Coefficient==4.25m/3m),"94 all native segments have original total4point25destruction coefficient");
            }
        }
        YCReset();var gauge=NewGauge();Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard());Equal(gauge.PreciseCharge,2m,"94 zero-Xremains a real played attack with normal2card-use OD only");Check(YCAttackOdGains.Count==0,"94 zero-Xcreates no attack-hit OD receipt");Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(CCard());Equal(YCAttackOdGains.Single(),7.5m,"94 actual three-hit native command charges standard2point5OD per hit");Equal(gauge.PreciseCharge,11.5m,"94 two actual attack-card uses stay separate from7point5hit OD");
        YCReset();Set(Player.PlayerCombatState!,"_energy",2);var plain=CCard();await ChasePlay(plain);decimal baseline=YCSegments.First().Final;
        YCReset();Set(Player.PlayerCombatState!,"_energy",2);var vow=CCard(3);await ChasePlay(vow);Check(Hero.GetPower<HbrEternalVowPower>()is{Amount:1},"94 first play grants one visible vow before its own damage");
        Equal(YCPotency(Hero.GetPower<HbrEternalVowPower>()!),25,"94 vow is fixed25rather than an INT-scaled AttackUp");Equal(YCSegments.First().Final,decimal.Floor(baseline*1.25m),"94 vow enters actual normal amplification bucket");
        Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(vow);Check(Hero.Powers.OfType<HbrEternalVowPower>().Count()==1,"94 same physical card cannot grant vow twice this battle");
        Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(CCard(3));Check(Hero.Powers.OfType<HbrEternalVowPower>().Count()==2,"94 independent physical vow cards use the common two-instance framework");
        var restored=YCSaveRestore(Hero.Powers.OfType<HbrEternalVowPower>().First());Equal(YCPotency(restored),25,"94 real SavedProperties JSON preserves inherited vow potency");Check(restored.HbrExitOverride>0&&restored.HbrLimitOverride>0,"94 real SavedProperties restores inherited eternal/default profile fields");
        await restored.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Player,new[]{Hero});await restored.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy});Equal(restored.Amount,1,"94 eternal vow never decrements on ordinary side-end ticks");
        YCReset();var origin=CCard(3,4);MisatoPermanent(origin);var mirror=(LoveCharlottaHbrgoSaki)GMirror(origin,true);Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(mirror);
        Check(origin.VowGrantedThisBattle&&origin.DollGrantedThisBattle,"94 combat mirror publishes both once-battle receipts to its actual permanent physical origin");
        YCSaveCache(typeof(LoveCharlottaHbrgoSaki));var saveOptions=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};var payload=System.Text.Json.JsonSerializer.Serialize(MegaCrit.Sts2.Core.Saves.Runs.SavedProperties.From(origin),saveOptions);
        var copy=(LoveCharlottaHbrgoSaki)origin.MutableClone();Check(!copy.VowGrantedThisBattle&&!copy.DollGrantedThisBattle,"94 a new physical mutable clone does not inherit spent once-battle receipts");
        System.Text.Json.JsonSerializer.Deserialize<MegaCrit.Sts2.Core.Saves.Runs.SavedProperties>(payload,saveOptions)!.Fill(copy);Check(copy.VowGrantedThisBattle&&copy.DollGrantedThisBattle,"94 actual saved-property JSON restores both spent receipts for a loaded card");
        await mirror.BeforeCombatStart();Check(!origin.VowGrantedThisBattle&&!origin.DollGrantedThisBattle&&!mirror.VowGrantedThisBattle&&!mirror.DollGrantedThisBattle,"94 real fresh-combat callback on populated combat copy resets both permanent physical receipts");
        YCReset();var toyCard=CCard(4);Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(toyCard);var toy=Player.PlayerCombatState.Hand.Cards.OfType<PallasCatDollHbrgoSaki>().Single();
        Check(toy.EnergyCost.GetWithModifiers(CostModifiers.All)==0&&toy.Keywords.Contains(CardKeyword.Retain)&&toy.Keywords.Contains(CardKeyword.Exhaust),"94 generated Pallas cat doll is zero-cost retain+exhaust");
        Check(((IEnumerable<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Contains(toy),"94 generated doll is registered in the real combat card state");
        await ChasePlay(toy);Equal(Player.PlayerCombatState.Energy,1,"94 real doll wrapper grants exactly one energy");Check(toy.Pile==Player.PlayerCombatState.ExhaustPile,"94 doll Exhaust is handled by native wrapper");
        Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(toyCard);Check(!Player.PlayerCombatState.Hand.Cards.OfType<PallasCatDollHbrgoSaki>().Any(),"94 source card gives no second doll in same battle");
        YCReset();var multi=CCard(1,2);Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(multi);Check(multi.TargetType==TargetType.AllEnemies&&YCStrikes.Single().DefenseBefore==1,"94 grenade changes target type and applies defense-down before damage: target="+multi.TargetType+" before="+YCStrikes.Single().DefenseBefore+" upgrades="+string.Join(',',multi.UpgradeEffectOrder)+" level="+multi.CurrentUpgradeLevel+" directGetter="+typeof(LoveCharlottaHbrgoSaki).GetProperty("TargetType")!.GetValue(multi));
        var fragile=Enemy.GetPower<FragilePower>();Check(fragile!=null&&fragile.Amount==1,"94 kiss applies fragile after successful actual hits");
        Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",fragile)!,(decimal)Call("HbrPercentDebuffHelper","ResolveBasis",fragile,42m,Hero,Enemy)!,"94 kiss42uses common explicit potency snapshot");
        YCReset();var second=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;Set(second,"_currentHp",5000000);Set(second,"_maxHp",5000000);Set(Combat,"_enemies",new List<Creature>{Enemy,second});Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(CCard(1,2));
        Check(YCStrikes.Single().Targets.ToHashSet().SetEquals(new[]{Enemy,second})&&YCSegments.Count==6,"94 actual grenade uses one native three-hit AoE request reaching both physical enemies");
        Check(Enemy.GetPower<HbrDefenseDownPower>()is{Amount:1}&&second.GetPower<HbrDefenseDownPower>()is{Amount:1}&&Enemy.GetPower<FragilePower>()is{Amount:1}&&second.GetPower<FragilePower>()is{Amount:1},"94 grenade pre-debuff and post-hit kiss apply independently to both actual physical recipients");
        YCReset();Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard(2));Check(!Enemy.HasPower<FragilePower>(),"94 zero-X without any hit cannot apply kiss to an unharmed target");
        YCReset();var jealousy=CCard(5);await ChasePlay(FCard(typeof(YCNongardenSkill)));Equal(YCJealous(jealousy),0,"94 ordinary nongarden skill is not jealousy");
        YCChoices=[2];await ChasePlay(YCard(0));Equal(YCJealous(jealousy),1,"94 actual other garden play adds one jealousy");YCChoices=[2];await ChasePlay(YCard(0),true);Equal(YCJealous(jealousy),2,"94 actual other garden AutoPlay adds jealousy");
        YCChoices=[2];await ChasePlay(YCard(0));Equal(YCJealous(jealousy),2,"94 jealousy caps at two additionalX");Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(jealousy);Equal(YCStrikes.Last().Raw,11.52m,"94 capped jealousy changes resolvedXbut not native paid1energy");
        YCReset();jealousy=CCard(0,5);Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(jealousy);Equal(YCStrikes.Single().Raw,3.84m,"94 current physical card never counts itself as jealousy; Flash alone adds1");
        YCReset();jealousy=CCard(5);Power<YCReplayPower>(Hero,1).Repeats=2;Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(jealousy);Check(YCStrikes.Count==3&&YCStrikes.All(s=>s.Raw==3.84m),"94 native extra resolutions reuse paidXand do not turn own replays into jealousy");
        YCReset();jealousy=CCard(5);var peer=SupportTeammate();Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});var peerCard=FCard(typeof(WitnessMySupremacyHbrgoSaki),peer);YCChoices=[2];await ChasePlay(peerCard);Equal(YCJealous(jealousy),0,"94 teammate plays never enter owner's jealousy ledger");
        foreach(bool od in new[]{false,true})
        {
            YCReset();jealousy=CCard(5);YCChoices=[2];await ChasePlay(YCard(0));Equal(YCJealous(jealousy),1,"94 ordinary jealousy starts1beforeextra od="+od);
            using(MisatoExtra(od)){Equal(YCJealous(jealousy),0,"94 inserted turn starts independent jealousy od="+od);YCChoices=[2];await ChasePlay(YCard(0));Equal(YCJealous(jealousy),1,"94 inserted turn counts its own garden plays od="+od);}
            Equal(YCJealous(jealousy),1,"94 return from inserted turn preserves old ordinary jealousy od="+od);
        }
        YCReset();jealousy=CCard(5);MisatoPermanent(jealousy);YCChoices=[2];await ChasePlay(YCard(0));Equal(YCJealous(jealousy),1,"94 permanent-card preview with no card.CombatState still reads active owner's jealousy safely");
        YCReset(false);Set(Player.PlayerCombatState!,"_energy",2);await ChasePlay(CCard(0,1,2,3,4,5,6));Check(YCStrikes.Count==0&&!Hero.HasPower<HbrEternalVowPower>()&&!Hero.HasPower<CharlottaEngagementRingPower>(),"94 legacy mode has no new attacks/vow/ring/toy");
    }
}
