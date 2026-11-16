using System.Reflection;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
static partial class EffectChecks
{
    static async Task YCDelayedCases()
    {
        YCReset();var dp=NewDp(0);YCChoices=[3];await ChasePlay(YCard(1,4));var regen=Hero.GetPower<YingxiaRegenerationFormationPower>()!;
        decimal tick=decimal.Floor(dp.MaxDp*.07m);Equal(regen.Amount,3,"93 regen starts exactly3nextownturns even with Amplify");await YCStart();Equal(dp.Dp,0,"93 duplicate reset in application turn does not heal immediately");
        for(int turn=2;turn<=4;turn++)
        {
            Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",turn);await YCStart();Equal(dp.Dp,tick*(turn-1),"93 native ordinary next turn heals exactly7percent tick="+turn);await YCStart();Equal(dp.Dp,tick*(turn-1),"93 repeated same reset cannot double-heal tick="+turn);
        }
        Check(!Hero.HasPower<YingxiaRegenerationFormationPower>(),"93 third heal removes completed visible regeneration");Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",5);await YCStart();Equal(dp.Dp,tick*3,"93 no fourth regeneration tick");
        YCReset();dp=NewDp();Set(dp,"_dp",dp.MaxDp-1);YCChoices=[3];await ChasePlay(YCard(4));Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await YCStart();Equal(dp.Dp,dp.MaxDp,"93 regen uses normal maximumDPcap and never overflows");
        YCReset();Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard(6));Check(Hero.GetPower<CharlottaEngagementRingPower>()is{Amount:1},"94 ring is a visible next-turn pending ability");await YCStart();Equal(Player.PlayerCombatState.Energy,3,"94 ring does not activate in application turn");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await YCStart();Equal(Player.PlayerCombatState.Energy,4,"94 native ordinary reset completes first then ring adds1");Check(!Hero.HasPower<CharlottaEngagementRingPower>(),"94 ring removes itself after one payout");await YCStart();Equal(Player.PlayerCombatState.Energy,3,"94 repeated ordinary reset cannot repay removed ring");
        YCReset();Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard(6));for(int i=0;i<5;i++)FCard(typeof(YCNongardenSkill),hand:false);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await FSetupTurn();Equal(Player.PlayerCombatState.Energy,4,"94 full actual native SetupPlayerTurn resets energy before ring callback and native starting draw");
        YCReset();Power<YCReplayPower>(Hero,1).Repeats=2;Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(CCard(6));Check(Hero.Powers.OfType<CharlottaEngagementRingPower>().Count()==3,"94 each native card resolution schedules its own one-energy ring");Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await YCStart();Equal(Player.PlayerCombatState.Energy,6,"94 three independent pending rings pay once each after one full3energy reset");
        foreach(bool od in new[]{false,true})
        {
            YCReset();dp=NewDp(0);YCChoices=[3];await ChasePlay(YCard(4));Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard(6));
            using(MisatoExtra(od))
            {
                await YCStart(inserted:true);Equal(Player.PlayerCombatState.Energy,4,"93-94 real inserted common start pays ring afterreset od="+od);Equal(dp.Dp,decimal.Floor(dp.MaxDp*.07m),"93 real inserted common start triggers next regen turn od="+od);
                await(Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;Equal(dp.Dp,decimal.Floor(dp.MaxDp*.07m),"93 duplicate inserted token does not heal twice od="+od);
                Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard(6));await(Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;Equal(Player.PlayerCombatState.Energy,0,"94 newly granted ring in already-started inserted turn waits od="+od);
            }
            await YCStart();Equal(Player.PlayerCombatState.Energy,3,"94 returning to original ordinary turn cannot consume newly inserted ring od="+od);
            Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await YCStart();Equal(Player.PlayerCombatState.Energy,4,"94 next true ordinary turn pays inserted pending ring od="+od);Equal(dp.Dp,decimal.Floor(dp.MaxDp*.07m)*2,"93 true ordinary turn after inserted is second regen tick od="+od);
        }
        YCReset();dp=NewDp(0);YCChoices=[3];await ChasePlay(YCard(4));var savedRegen=YCSaveRestore(Hero.GetPower<YingxiaRegenerationFormationPower>()!);Equal(savedRegen.LastOrdinaryTurn,1,"93 real derived native Save/Fill restores inherited last ordinary turn setter");
        YCReset();Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(CCard(6));var ring=Hero.GetPower<CharlottaEngagementRingPower>()!;var peer=SupportTeammate();Set(peer.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await YCStart(owner:peer);Check(Hero.Powers.Contains(ring),"94 teammate reset cannot consume owner ring");
    }
    static void YCMetadataCases()
    {
        YCReset();var y=YCard(0,1,2,3,4,5,6);var c=CCard(0,1,2,3,4,5,6);Check(y.CurrentUpgradeLevel==7&&c.CurrentUpgradeLevel==7,"93-94 each physical card can acquire all seven distinct upgrades");
        Check(y.UpgradeEffectOrder.Distinct().Count()==7&&c.UpgradeEffectOrder.Distinct().Count()==7,"93-94 seven upgrade ledgers contain no duplicate effect");
        Check(y.ThirtyOneXMember==ThirtyOneXMember.LiYingxia&&c.ThirtyOneXMember==ThirtyOneXMember.CharlottaSkopovskaya,"93-94 actual members use existing fifth/sixth faction enum identities");
        foreach((CardModel card,string id)in new[]{((CardModel)y,"yingxia"),((CardModel)c,"charlotta")})
        {
            Check((string)Call("HbrMemoryGarden","CharacterIdOf",card)! == id,"93-94 actual garden character maps to "+id);
            Check((bool)Invoke(Helper("HbrMemoryGardenCardCatalog").GetMethod("IsGardenCard",F,null,new[]{typeof(CardModel)},null)!,null,card)! ,"93-94 actual garden classifier recognizes "+id);
        }
        var content=Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack",true)!;var all=((Type[])content.GetField("CardTypes",F)!.GetValue(null)!);
        Check(all.Count(t=>t==typeof(WitnessMySupremacyHbrgoSaki))==1&&all.Count(t=>t==typeof(LoveCharlottaHbrgoSaki))==1,"93-94 main cards are explicitly registered exactly once");
        Check(!all.Contains(typeof(PallasCatDollHbrgoSaki))&&!all.Contains(typeof(YingxiaFormationChoiceHbrgoSaki)),"93-94 token toy and formation selectors are excluded from normal garden/card pool");
        MisatoPermanent(y);MisatoPermanent(c);var marker=(ThirtyOneXCollection)ModelDb.Relic<ThirtyOneXCollection>().ToMutable();Set(marker,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(marker).ToList());Equal(marker.DisplayAmount,2,"93-94 real permanent deck counts Yingxia and Charlotta as two distinct31Xmembers");
        Check(y.PortraitPath.EndsWith("memory_corridor_hbrgo_saki.png")&&c.PortraitPath==y.PortraitPath,"93-94 default placeholder portrait is retained");
    }
}
