using MegaCrit.Sts2.Core.Runs;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

static partial class ActRosterChecks
{
    static async Task FlavorDeath(Creature dead, bool prevented=false)
    {
        var method=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterDeath" && m.GetParameters().Length is 5 or 6);
        object?[] args=method.GetParameters().Select(p=>p.ParameterType==typeof(bool)?(object)prevented:
            p.ParameterType==typeof(float)?0f:p.ParameterType==typeof(Creature)?dead:
            p.ParameterType==typeof(CardModel)?null:p.ParameterType.IsInstanceOfType(State<object>("RunState"))?State<object>("RunState"):
            p.ParameterType.IsInstanceOfType(Combat)?Combat:throw new Exception("Unmapped death argument"+p)).ToArray();
        await (Task)Invoke(method,null,args)!;
    }
    static async Task CrabFlavorCases()
    {
        var crab=FlavorRoom(typeof(ShrimpCrab)); Creature actor=crab.Creature;
        Creature other=Actor(typeof(ShrimpCrab)), plain=Actor(typeof(SmallHopper));
        var attack=OwnedCardOf<StrikeIronclad>();
        await MoveOf(crab,"STEEL_SHIELD_MOVE").PerformMove([Hero]);
        Check(actor.HasPower<HbrProvokePower>() && actor.HasPower<HbrDefenseUpPower>(),"M13 actual ProvokeShield grants both existing defense and new taunt");
        Check(attack.IsValidTarget(actor) && !attack.IsValidTarget(other) && !attack.IsValidTarget(plain),"M13 native single-target Attack validity permits only provoking enemies");
        Check(ReferenceEquals(Static("HBRGOSakiMod.Core.Helpers.HbrProvokeTargetingHelper","ResolveTarget",attack,plain),actor),"M13 stale automatic single-target Attack redirects to actual provoking enemy");
        var skill=OwnedCardOf<StrikeIronclad>();Set(skill,"<Type>k__BackingField",CardType.Skill);
        Check(skill.IsValidTarget(plain),"M13 non-Attack single-target cards remain unrestricted");
        var all=OwnedCardOf<StrikeIronclad>();Set(all,"<TargetType>k__BackingField",TargetType.AllEnemies);
        Check((bool)Static("HBRGOSakiMod.Core.Helpers.HbrProvokeTargetingHelper","IsAllowedTarget",all,plain)!,"M13 AoE target selection remains unrestricted");
        await MoveOf(other.Monster!,"STEEL_SHIELD_MOVE").PerformMove([Hero]);
        Check(attack.IsValidTarget(actor) && attack.IsValidTarget(other) && !attack.IsValidTarget(plain),"M13 multiple provocateurs remain selectable");
        var candidates=(Creature[])Static("HBRGOSakiMod.Core.Helpers.HbrProvokeTargetingHelper","ConfusionCandidates",attack)!;
        Check(candidates.Length==2 && candidates.Contains(actor)&&candidates.Contains(other),"M13 actual Confusion candidate builder excludes all unprovoking enemies");
        var confusion=Power<HbrConfusionPower>(Hero,3);int self=0,enemy=0;
        for(int seed=0;seed<32;seed++)
        {
            Set(State<object>("RunState"),"<Rng>k__BackingField",new RunRngSet("provoke-confusion-"+seed));
            SetProp(confusion,"UsedThisTurn",false);Set(confusion,"_selfHitCard",null);
            Creature chosen=(Creature)Call(confusion,"ChooseCardTarget",attack,actor)!;
            if(Field(confusion,"_selfHitCard")!=null)self++;else enemy++;
            Check(chosen==actor || chosen==other,"M13 real Confusion roll"+seed+" never selects unprovoking enemy");
        }
        Check(self>0 && enemy>0,"M13 real Confusion RNG retains both provoke-target and self-hit outcomes");
        await PowerCmd.Remove(confusion);
        await FlavorDispatch("AfterSideTurnEnd",CombatSide.Enemy);
        Check(actor.HasPower<HbrProvokePower>(),"M13 Provoke survives first enemy end");
        await FlavorDispatch("AfterSideTurnEnd",CombatSide.Enemy);
        Check(!actor.HasPower<HbrProvokePower>() && attack.IsValidTarget(plain),"M13 second enemy end expires Provoke and restores native selection");
        decimal before=EnemyHit(actor);Set(other,"_currentHp",0);
        await FlavorDeath(other,true);Check(!actor.HasPower<ShrimpRunawayPower>(),"M13 prevented death cannot activate Runaway");
        await FlavorDeath(other);var runaway=actor.GetPower<ShrimpRunawayPower>()!;
        Check(runaway!=null && crab.GetType().GetProperty("RunawayTriggered")!.GetValue(crab) is true,"M13 actual native AfterDeath dispatch grants Runaway exactly once");
        Near(EffectStrength(runaway!),ExpectedEnemyBuff(100m,actor,HeroKind.Hbr),"M13 Runaway resolves fixed100 basis with normal enemy buff attributes");
        Near(EnemyHit(actor),before*(1m+EffectStrength(runaway!)/100m),"M13 real native modifier dispatch applies Runaway through standard attack-up bucket");
        await FlavorDeath(other);Check(actor.Powers.OfType<ShrimpRunawayPower>().Count()==1,"M13 repeated same confirmed death never grants a second Runaway");
        await FlavorDispatch("AfterSideTurnEnd",CombatSide.Enemy);Check(actor.HasPower<ShrimpRunawayPower>(),"M13 Runaway remains after turn end as Eternal");
        Effect<HbrAttackUpPower>("ApplyTimedBuff",actor,20);
        decimal total=actor.Powers.OfType<HbrAttackUpPower>().Sum(EffectStrength);
        Near(EnemyHit(actor),before*(1m+total/100m),"M13 Runaway and normal AttackUp combine additively once in existing same-effect bucket");
        ResetTable(HeroKind.Hbr,0);Creature phantom=PhantomActor(typeof(ShrimpCrab));SetUp(phantom.Monster!);
        await MoveOf(phantom.Monster!,"STEEL_SHIELD_MOVE").PerformMove([Hero]);Check(!phantom.HasPower<HbrProvokePower>(),"M13 phantom replay receives no enemy-only Provoke");
    }
    static async Task CatFlavorCases()
    {
        foreach(Type type in new[]{typeof(CatHorn),typeof(CatHornKiller)})
        foreach(int variant in Enumerable.Range(0,type==typeof(CatHorn)?2:1))
        {
            var cat=(HbrgoMoveTableMonster)FlavorRoom(type,0,variant);Creature actor=cat.Creature;
            Creature first=Actor(typeof(SmallHopper)), second=Actor(typeof(SmallHopper));
            // Execute the real table until its authored healing slot is selected.
            for(int guard=0;cat.FlavorQueuedTableMoveId!="HEAL_MOVE" && guard<20;guard++)await FlavorTurn(cat);
            Check(cat.FlavorQueuedTableMoveId=="HEAL_MOVE" && cat.NextMove.Id=="POWER_GENERATOR_MOVE","M14 "+type.Name+variant+" original healing step advertises generator with exactly3 living enemies");
            string rng=FlavorRng();int cursor=cat.MoveTableStep;
            Set(second,"_currentHp",0);await FlavorDeath(second);
            Check(cat.NextMove.Id=="HEAL_MOVE" && FlavorRng()==rng && cat.MoveTableStep==cursor,"M14 actual ally death immediately changes generator back to same selected heal without AI reroll");
            Set(second,"_currentHp",1_000_000);RefreshFlavor(cat);
            Check(cat.NextMove.Id=="POWER_GENERATOR_MOVE" && FlavorRng()==rng,"M14 restored3-enemy condition swaps same healing slot back to generator");
            Power<HbrCalamityPower>(first,50);
            await cat.PerformMove();
            foreach(Creature target in new[]{actor,first,second})
            {
                HbrAttackUpPower buff=target.Powers.OfType<HbrAttackUpPower>().Single();
                Near(EffectStrength(buff),ExpectedEnemyBuff(200m,actor,HeroKind.Hbr),"M14 generator actual body grants200 basis to each living enemy including self");
                Check(buff.Amount==2,"M14 generator has existing EnemyTurnEnd2 duration");
                Check(ReferenceEquals(buff.Applier,actor),"M14 generator retains Cat applier even for independently weakened teammate");
            }
            await FlavorDispatch("AfterSideTurnEnd",CombatSide.Enemy);cat.RollMove([Hero]);
            Check(cat.MoveTableStep!=cursor || cat.FlavorQueuedTableMoveId!="HEAL_MOVE","M14 generator consumes its replacement slot instead of pausing original table");
        }
    }
    static void FlavorIsolationCases()
    {
        foreach(HeroKind kind in new[]{HeroKind.Legacy,HeroKind.Original})
        foreach(Type type in new[]{typeof(PillarGuardian),typeof(BarrelWalker),typeof(DiamondEyeball),typeof(BigotryGate),typeof(CatHorn),typeof(CatHornKiller)})
        {
            ResetTable(kind,0);var model=TableActor(type,0,out Creature actor);SetUp(model);Await(model.AfterAddedToRoom());
            Check(!actor.Powers.Any(p=>p is PillarOverdriveDetectionPower or BarrelDemiseCountdownPower or DiamondDpCheckPower or FaintnessStormCountdownPower),kind+" "+type.Name+" room entry preserves legacy mechanics");
        }
        foreach(Type type in new[]{typeof(PillarGuardian),typeof(BarrelWalker),typeof(DiamondEyeball),typeof(BigotryGate)})
        {
            ResetTable(HeroKind.Hbr,0);Creature actor=PhantomActor(type);SetUp(actor.Monster!);Await(actor.Monster!.AfterAddedToRoom());
            Check(!actor.Powers.Any(p=>p is PillarOverdriveDetectionPower or BarrelDemiseCountdownPower or DiamondDpCheckPower or FaintnessStormCountdownPower),type.Name+" phantom receives no new enemy markers");
        }
    }
}
