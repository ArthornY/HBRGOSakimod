using System.Reflection;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static async Task FDefenseCases()
    {
        FReset(6);var dp=NewDp(20);await FMove(5,30,25);
        Equal(dp.Dp,15,"79 earlier nonlethal hit spends5DP, fatal and all later commands preserve remaining15");
        Check(FDamage.Where(h=>h.Dealer==Enemy).All(h=>h.Lost==0m)&&Hero.CurrentHp==5000,"79 full remainder of actual native multicommand action causes zeroHP");
        Check(FCounters.Count==1&&FCounters[0].Raw==25.92m&&FCounters[0].Hits==1&&FCounters[0].Dealer==Hero&&FCounters[0].Target==Enemy,"79 one actual432percent single-target owner counter after whole action");
        Equal(FCounters[0].Destruction,2.5m/3m,"79 total destruction2point5 uses one normalized direct counter context");
        Check(AsyncValue("ThirtyOneBEnemyActionHelper","Current")==null,"79 enemy action scope closed after counter");
        await FMove(20,7);Equal(dp.Dp,0,"79 next enemy action depletes preservedDP normally");Equal(Hero.CurrentHp,4993,"79 subsequent hit after normal next-action break reachesHP");Equal(FCounters.Count,1,"79 once-per-battle ledger prevents a second counter");
        FReset(6);dp=NewDp(20);Hero.GainBlockInternal(30m);await FMove(20);Equal(dp.Dp,20,"79 ordinary block is spent before testing DP lethal threshold");Equal(FCounters.Count,0,"79 fully blocked hit does not reserve protection");
        await FMove(30);Equal(dp.Dp,20,"79 later genuinely fatal postblock remainder can still reserve protection");Equal(FCounters.Count,1,"79 fully blocked earlier action did not consume once-battle use");
        FReset(6);dp=NewDp(0);await FMove(10);Equal(FCounters.Count,0,"79 existing zeroDP does not trigger counter");Equal(Hero.CurrentHp,4990,"79 existing zeroDP does not gain arbitrary immunity");
        FReset(5);dp=NewDp(20);await FMove(30);Equal(dp.Dp,0,"79 five kinds cannot protect DP");Equal(FCounters.Count,0,"79 five kinds never counter");
        FReset(6,false);dp=NewDp(20);await FMove(30);Equal(dp.Dp,0,"79 legacy mode does not gain HBR-only protection");Equal(FCounters.Count,0,"79 legacy mode never counters");
        FReset(6);dp=NewDp(20);await FMoveTo(new[]{30m},new[]{Hero},ValueProp.Unpowered);Equal(dp.Dp,0,"79 unpowered damage cannot consume butler protection");Equal(FCounters.Count,0,"79 unpowered effect does not counter");
        FReset(6);dp=NewDp(20);var pending=new TaskCompletionSource();FGate=pending.Task;Task move=FMove(30,10);
        Equal(dp.Dp,20,"79 pending canonical damage has not prematurely spentDP");Equal(FCounters.Count,0,"79 async enemy move has no premature counter");pending.SetResult();await move;FGate=null;Equal(dp.Dp,20,"79 protection survives awaits through all commands");Equal(FCounters.Count,1,"79 asynchronous completion yields one counter");
        FReset(6);dp=NewDp(20);FKillAttackerAfterHit=true;await FMove(30,20);Equal(dp.Dp,20,"79 protected remaining commands stay immune if attacker dies during that action");Equal(FCounters.Count,0,"79 dead attacker receives no counter");
        FReset(6);dp=NewDp(20);FThrowMove=true;try{await FMove(30);throw new Exception("Missing expected fault");}catch(InvalidOperationException error){Check(error.Message.Contains("controlled"),"79 actual native move fault remains observable");}
        Check(FCounters.Count==0&&AsyncValue("ThirtyOneBEnemyActionHelper","Current")==null,"79 failed enemy move closes scope without retaliation");
        FReset(6);dp=NewDp(20);var originalRelics=Player.Relics.ToArray();var gauge=NewGauge();Set(Player,"_relics",originalRelics.Append(gauge).ToList());
        await FMove(30);Equal(gauge.PreciseCharge,2.5m,"79 actual relic-source after-damage counter grants standard2point5OD per connected hit");
        decimal baseline=FCounters.Single().Final;
        FReset(6);dp=NewDp(20);Power<HbrAttackUpPower>(Hero,1);Native(Hero.GetPower<HbrAttackUpPower>()!,"ConfigureEffect",50m,null);await FMove(30);
        Check(FCounters.Single().Final>baseline,"79 counter enters holder's ordinary attack buff/stat damage pipeline: baseline="+baseline+" buffed="+FCounters.Single().Final+" attackUp="+Hero.GetPowerAmount<HbrAttackUpPower>());
        FReset(6);dp=NewDp(20);await Command(Enemy,30m,ValueProp.Move,null,new[]{Hero});Equal(dp.Dp,20,"79 standalone native enemy damage fallback creates a complete protected attack scope");Equal(FCounters.Count,1,"79 standalone canonical attack fallback retaliates once");
        FReset(6);dp=NewDp(20);Power<BufferPower>(Hero,1);await FMove(30);Equal(dp.Dp,20,"79 existing native Buffer does not prematurely spendDP at block boundary");Equal(FCounters.Count,0,"79 Buffer-reserved hit does not steal the butler once-battle use");
        FReset(6);dp=NewDp(20);FUseNullDealer=true;await FMove(30);Equal(dp.Dp,0,"79 nested null-dealer powered command cannot borrow outer enemy identity");Equal(FCounters.Count,0,"79 null dealer never countered as outer attacker");
        // Actual canonical dealer scope must mask the outer enemy action.
        FReset(6);dp=NewDp(20);await FMoveTo(new[]{30m},new[]{Hero},ValueProp.Move,Hero);
        Equal(dp.Dp,0,"79 nested allied powered canonical hit cannot steal outer enemy protection");Equal(FCounters.Count,0,"79 nested allied hit never falsely counters outer attacker");
    }
    static async Task FCoopCases()
    {
        FReset(6);var ownDp=NewDp(20);Player peer=SupportTeammate();Set(peer,"<NetId>k__BackingField",2UL);Set(RunState,"_players",new List<Player>{Player,peer});
        Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});Set(peer.Creature,"_currentHp",5000);Set(peer.Creature,"_maxHp",5000);
        for(int i=0;i<6;i++)FOrigin(i,peer);
        var peerMarker=(ThirtyOneFCollection)ModelDb.Relic<ThirtyOneFCollection>().ToMutable();Set(peerMarker,"_owner",peer);Set(peer,"_relics",peer.Relics.Append(peerMarker).ToList());
        var peerDp=Blank<DeflectorProtection>();Set(peerDp,"<IsMutable>k__BackingField",true);Set(peerDp,"_owner",peer);Set(peerDp,"_hasInitializedDp",true);Set(peerDp,"_dp",30);Set(peer,"_relics",peer.Relics.Append(peerDp).ToList());
        await FMoveTo(new[]{40m,10m},new[]{Hero,peer.Creature},ValueProp.Move);
        Equal(ownDp.Dp,20,"79 first collecting player retains ownDP");Equal(peerDp.Dp,30,"79 second collecting player independently retains ownDP");
        Check(FCounters.Count==2&&FCounters.Select(c=>c.Dealer).ToHashSet().SetEquals(new[]{Hero,peer.Creature}),"79 multiplayer holders each counter exactly once from own source");
        await FMoveTo(new[]{40m},new[]{Hero,peer.Creature},ValueProp.Move);Equal(ownDp.Dp,0,"79 first player's spent receipt persists");Equal(peerDp.Dp,0,"79 second player's receipt independently persists");Equal(FCounters.Count,2,"79 no replay after either co-op player's reservation is spent");
        FReset(6);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());
        var reign=(MomentOfReign)ModelDb.Relic<MomentOfReign>().ToMutable();Set(reign,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(reign).ToList());
        Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        var shell=(PhantomHighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();await PowerCmd.Apply(new ThrowingPlayerChoiceContext(),shell,phantom,1m,Hero,null);Native(shell,"SetDpAmount",20m,false);
        await FMoveTo(new[]{30m,15m},new[]{Hero},ValueProp.Move);Equal(shell.CurrentDp,20,"79 actual30A canonical primary phantomDP is preserved");Equal(FCounters.Count,1,"79 main phantom protection retaliates as the owner");
        FReset(6);var ownerPetDp=NewDp(20);Creature pet=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,pet});
        var petShell=(HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();await PowerCmd.Apply(new ThrowingPlayerChoiceContext(),petShell,pet,20m,Hero,null);
        await FMoveTo(new[]{30m},new[]{pet},ValueProp.Move);Equal(ownerPetDp.Dp,0,"79 ordinary pet's native owner-block receiver cannot borrow butler protection for ownerDP");Equal(FCounters.Count,0,"79 ordinary pet hit cannot consume the owner's counter");
    }
}
