using MegaCrit.Sts2.Core.Saves.Runs;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Characters;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class ActRosterChecks
{
    static bool FlavorNetId(ref ulong? __result) { __result=HeroPlayer.NetId;return false; }
    static bool FlavorWait(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool FlavorListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result = Combat.Creatures.SelectMany(c => c.Powers.Cast<AbstractModel>().Concat(c.Monster == null ? [] : new AbstractModel[] { c.Monster }))
            .ToArray(); return false;
    }
    static MonsterModel FlavorRoom(Type type, int asc = 0, int variant = 0)
    {
        ResetTable(HeroKind.Hbr, asc, dp: 500, overdrive: 0);
        Set(Hero, "_currentHp", 5000); Set(Combat, "_allies", new List<Creature> { Hero });
        Set(State<object>("RunState"), "<Rng>k__BackingField", new RunRngSet("enemy-flavor-fixed-seed"));
        MonsterModel monster = TableActor(type, variant, out Creature actor);
        Set(actor, "_currentHp", 1_000_000); Set(actor, "_maxHp", 1_000_000);
        Set(monster,"<Id>k__BackingField",ModelDb.GetId(type)); monster.RunRng=State<RunState>("RunState").Rng; SetUp(monster); FullShell(actor); Await(monster.AfterAddedToRoom()); monster.RollMove([Hero]);
        return monster;
    }
    static async Task FlavorDispatch(string name, CombatSide side)
    {
        if (name == "AfterSideTurnEnd" && !typeof(Hook).GetMethods(F).Any(m => m.Name == name)) name = "AfterTurnEnd";
        MethodInfo method = typeof(Hook).GetMethods(F).Single(m => m.Name == name);
        Creature[] participants = Combat.Creatures.Where(c => c.Side == side && c.IsAlive).ToArray();
        object?[] args = method.GetParameters().Select(p => p.ParameterType == typeof(CombatSide) ? (object)side :
            p.ParameterType == typeof(PlayerChoiceContext) ? new ThrowingPlayerChoiceContext() :
            p.ParameterType == typeof(Player) ? HeroPlayer :
            p.ParameterType.IsAssignableFrom(typeof(CombatState)) ? Combat :
            p.ParameterType.IsAssignableFrom(typeof(Creature[])) ? participants :
            throw new Exception("Unmapped turn parameter " + p)).ToArray();
        await (Task)Invoke(method, null, args)!;
    }
    static int FlavorBeforeRollStep, FlavorBeforeRollMask;
    static async Task FlavorTurn(MonsterModel monster)
    {
        await FlavorDispatch("BeforeSideTurnStart", CombatSide.Enemy);
        await monster.PerformMove();
        await FlavorDispatch("AfterSideTurnEnd", CombatSide.Enemy);
        FlavorBeforeRollStep=((HbrgoMoveTableMonster)monster).MoveTableStep; FlavorBeforeRollMask=((HbrgoMoveTableMonster)monster).DpOnceUsedMask;
        monster.RollMove([Hero]);
    }
    static void FlavorDpBreak(MonsterModel monster) => Await(Call(monster.Creature.GetPower<HighDimensionalShellPower>()!, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
    static string FlavorRng() => JsonSerializer.Serialize(State<RunState>("RunState").Rng.ToSerializable());
    static void RefreshFlavor(MonsterModel monster) => Static("HBRGOSakiMod.Core.Helpers.HbrEnemyFlavorSchedulingHelper", "RefreshOwner", monster.Creature);
    static void FlavorInstall()
    {
        Install(); var h = new Harmony("hbr.enemy.flavor.checks");
        h.Patch(typeof(Hook).Assembly.GetType("MegaCrit.Sts2.Core.Context.LocalContext",true)!.GetProperty("NetId",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(FlavorNetId),F)!));
        // Base fixture's hook enumeration is a presentation-free replacement.
        // Keep native Hook ordering, adding monster listeners required by death.
        h.Unpatch(typeof(CombatState).GetMethod(nameof(CombatState.IterateHookListeners))!, HarmonyPatchType.Prefix, "hbr.fragile.native.checks");
        h.Unpatch(typeof(RunState).GetMethod(nameof(RunState.IterateHookListeners))!, HarmonyPatchType.Prefix, "hbr.fragile.native.checks");
        foreach (Type t in new[] { typeof(CombatState), typeof(RunState) }) h.Patch(t.GetMethod("IterateHookListeners")!, prefix:new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(FlavorListeners),F)!));
        h.Patch(typeof(Cmd).GetMethod("CustomScaledWait")!, prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(FlavorWait),F)!));
        foreach (string name in new[] { "HbrEnemyFlavorBeforeMovePatch", "HbrProvokeCardTargetPatch" })
            Check(h.CreateClassProcessor(ModType("HBRGOSakiMod.Core.Patches.Combat." + name)).Patch().Count > 0, "installs native flavor boundary " + name);
        Check(h.CreateClassProcessor(ModType("HBRGOSakiMod.Core.Compatibility.HbrDeathHookCompatibility")).Patch().Count > 0,"installs native cross-profile monster death listener dispatch");
        foreach (Type type in new[] { typeof(PillarOverdriveDetectionPower), typeof(BarrelDemiseCountdownPower), typeof(DiamondDpCheckPower), typeof(ShrimpRunawayPower), typeof(HbrProvokePower), typeof(FaintnessStormCountdownPower) })
            if (!ModelDb.Contains(type)) Inject(type);
        Type history = typeof(CombatManager).GetProperty("History",F)!.PropertyType;
        // Native history records require a scene-owned event journal; the move
        // state, OnMovePerformed and side hooks remain the real native bodies.
        h.Patch(history.GetMethod("MonsterPerformedMove",F)!, prefix:new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(Skip),F)!));
        h.Patch(AccessTools.Method(ModType("HBRGOSakiMod.Core.Helpers.CombatPreviewRefreshHelper"),"RefreshCombatState"), prefix:new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(Skip),F)!));
    }
    static async Task PillarFlavorCases()
    {
        foreach(int asc in new[]{0,10})
        {
            var pillar=(HbrgoMoveTableMonster)FlavorRoom(typeof(PillarGuardian),asc);
            Check(pillar.Creature.HasPower<PillarOverdriveDetectionPower>(),"M10 native room entry installs OD marker");
            string queued=pillar.NextMove.Id, rng=FlavorRng(); int step=pillar.MoveTableStep;
            for(int level=0;level<=3;level++)
            {
                Set(HeroOd!,"_charge",level*100); RefreshFlavor(pillar);
                Check((pillar.NextMove.Id=="DEATHS_DOOR_MOVE")== (level==3),"M10 OD level"+level+" changes advertised move exactly at max");
                Check(FlavorRng()==rng && pillar.MoveTableStep==step,"M10 repeated OD-only preview does not reroll or consume table step");
                decimal baseline;
                var marker=pillar.Creature.GetPower<PillarOverdriveDetectionPower>()!;
                Powers(pillar.Creature).Remove(marker); baseline=EnemyHit(pillar.Creature); Powers(pillar.Creature).Add(marker);
                Near(EnemyHit(pillar.Creature),baseline*(1m+.5m*level),"M10 actual native damage hook respects target OD level"+level);
            }
            Set(HeroOd!,"_charge",200);RefreshFlavor(pillar);Check(pillar.NextMove.Id==queued,"M10 lowered OD restores same already selected move");
            SetProp(HeroOd!,"PreciseCharge",299m);
            Check(pillar.NextMove.Id==queued,"M10 real OD setter below max retains ordinary intent");
            SetProp(HeroOd!,"PreciseCharge",300m);
            Check(pillar.NextMove.Id=="DEATHS_DOOR_MOVE" && FlavorRng()==rng,"M10 real OD threshold event updates live intent without manual refresh or RNG");
            SetProp(HeroOd!,"PreciseCharge",299m);
            Check(pillar.NextMove.Id==queued,"M10 real OD downward threshold event restores original intent");

            Set(HeroOd!,"_charge",300);RefreshFlavor(pillar);int before=Attacks.Count;await FlavorTurn(pillar);
            Check(Attacks.Count==before+1 && Attacks.Last().Hits==1 && Attacks.Last().Damage==(asc>=7?125:100) && AttackScopes.Last()=="AllUnits", "M10 actual DeathsDoor is one authored all-unit100/125 hit");
            Check(pillar.MoveTableStep!=step,"M10 DeathsDoor consumes replaced ordinary table step");
        }
    }
    static void PillarTargetCases()
    {
        var pillar=FlavorRoom(typeof(PillarGuardian));var marker=pillar.Creature.GetPower<PillarOverdriveDetectionPower>()!;
        var peer=Blank<Player>();Set(peer,"_runState",State<object>("RunState"));Set(peer,"_relics",new List<RelicModel>());Set(peer,"<Character>k__BackingField",Blank<Ironclad>());
        Creature target=(Creature)FixtureCall("Actor",null,peer,null)!;Set(peer,"<Creature>k__BackingField",target);
        ((List<Creature>)Field(Combat,"_allies")!).Add(target);
        decimal Damage(Creature victim,ValueProp props)=> (decimal)FixtureCall("Preview",victim,pillar.Creature,10m,props,null)!;
        decimal Without(Creature victim,ValueProp props){Powers(pillar.Creature).Remove(marker);decimal amount=Damage(victim,props);Powers(pillar.Creature).Add(marker);return amount;}
        Near(Damage(target,ValueProp.Move),Without(target,ValueProp.Move),"M10 native target without Overclock receives no OD factor");
        var od=Blank<Overclock>();Set(od,"<IsMutable>k__BackingField",true);Set(od,"_owner",peer);Set(od,"_charge",100);Set(peer,"_relics",new List<RelicModel>{od});
        Set(HeroOd!,"_charge",300);
        Near(Damage(target,ValueProp.Move),Without(target,ValueProp.Move)*1.5m,"M10 mixed-party peer uses own OD1 rather than teammate OD3");
        Creature pet=PhantomActor(typeof(SmallHopper));((List<Creature>)Field(Combat,"_allies")!).Add(pet);
        Near(Damage(pet,ValueProp.Move),Without(pet,ValueProp.Move),"M10 player-owned summon never inherits its owner's OD factor");
        Near(Damage(target,ValueProp.Move|ValueProp.Unpowered),Without(target,ValueProp.Move|ValueProp.Unpowered),"M10 OD detection never multiplies unpowered damage");
    }
    static async Task BarrelFlavorCases()
    {
        var barrel=(HbrgoMoveTableMonster)FlavorRoom(typeof(BarrelWalker));
        for(int turn=1;turn<=12;turn++)
        {
            bool special=barrel.NextMove.Id=="DEMISE_CANNON_MOVE";
            Check(special==(turn%4==0),"M11 turn"+turn+" cannon occurs every fourth turn");
            int cursor=barrel.MoveTableStep,once=barrel.DpOnceUsedMask,before=Attacks.Count;string rng=FlavorRng();
            for(int i=0;i<4;i++)RefreshFlavor(barrel);
            Check(FlavorRng()==rng,"M11 pure intent refresh never rolls AI");
            await FlavorTurn(barrel);
            if(special)
            {
                Check(Attacks.Count==before+1 && Attacks.Last().Damage==40 && Attacks.Last().Hits==1 && AttackScopes.Last()=="AllUnits","M11 cannon actually executes one40 all-unit hit");
                Check(FlavorBeforeRollStep==cursor && FlavorBeforeRollMask==once && barrel.NextMove.Id==barrel.FlavorQueuedTableMoveId,"M11 cannon holds original cursor/once mask and resumes it");
                Check(barrel.Creature.GetPower<BarrelDemiseCountdownPower>()!.Amount==4,"M11 cannon reset stays4 across same turn end");
                Check(FlavorRng()==rng,"M11 resumed held ordinary move uses no second AI roll");
            }
        }
        barrel=(HbrgoMoveTableMonster)FlavorRoom(typeof(BarrelWalker));for(int i=0;i<3;i++)await FlavorTurn(barrel);
        Check(barrel.NextMove.Id=="DEMISE_CANNON_MOVE","M11 fixture reaches announced cannon");FlavorDpBreak(barrel);
        Check(barrel.NextMove.Id!="DEMISE_CANNON_MOVE" && barrel.Creature.GetPower<BarrelDemiseCountdownPower>()!.Amount==4,"M11 real DP Break cancels cannon and resets4");
        var countdown=barrel.Creature.GetPower<BarrelDemiseCountdownPower>()!; await countdown.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[barrel.Creature]);
        Check(countdown.Amount==4,"M11 Break pauses countdown");
    }
    static async Task DiamondFlavorCases()
    {
        var diamond=(HbrgoMoveTableMonster)FlavorRoom(typeof(DiamondEyeball));
        for(int turn=1;turn<=12;turn++)
        {
            string next=diamond.NextMove.Id;bool check=turn%4==3,boom=turn%4==0;
            Check((next=="DP_REMAINING_CHECK_MOVE")==check && (next=="DIVIDE_EXPLOSION_MOVE")==boom,"M12 turn"+turn+" follows check3/explode4 cadence");
            int cursor=diamond.MoveTableStep,attacks=Attacks.Count;string rng=FlavorRng();await FlavorTurn(diamond);
            if(check)Check(Attacks.Count==attacks && FlavorBeforeRollStep==cursor && FlavorRng()==rng,"M12 check is no damage and holds exact selected table option");
            if(boom)Check(Attacks.Count==attacks+1 && Attacks.Last().Damage==47 && Attacks.Last().Hits==1 && AttackScopes.Last()=="AllUnits" && FlavorBeforeRollStep==cursor && FlavorRng()==rng,"M12 full-DP explosion47 and resume consumes no new AI choice");
        }
        foreach((decimal ratio,int damage) in new[]{(1m,47),(.75m,47),(.749m,23),(.5m,23),(.499m,15),(.25m,15),(.249m,8)})
        {
            diamond=(HbrgoMoveTableMonster)FlavorRoom(typeof(DiamondEyeball));for(int i=0;i<3;i++)await FlavorTurn(diamond);
            var shell=diamond.Creature.GetPower<HighDimensionalShellPower>()!;string rng=FlavorRng();
            Call(shell,"SetDpAmount",decimal.Ceiling((decimal)Prop(shell,"MaxAmount")!*ratio),true);
            Check(FlavorRng()==rng && diamond.NextMove.Id=="DIVIDE_EXPLOSION_MOVE","M12 DP-tier refresh retains queued explosion without reroll");
            await FlavorTurn(diamond);Check(Attacks.Last().Damage==damage,"M12 real explosion ratio"+ratio+" yields raw"+damage);
        }
        diamond=(HbrgoMoveTableMonster)FlavorRoom(typeof(DiamondEyeball));for(int i=0;i<3;i++)await FlavorTurn(diamond);FlavorDpBreak(diamond);
        Check(diamond.NextMove.Id!="DIVIDE_EXPLOSION_MOVE" && !diamond.Creature.GetPower<DiamondDpCheckPower>()!.ExplosionPending,"M12 real Break between check/explosion cancels pending blast");
        var power=diamond.Creature.GetPower<DiamondDpCheckPower>()!;int counter=power.Amount;await power.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[diamond.Creature]);Check(power.Amount==(counter>1?counter-1:4),"M12 countdown continues during Break");
    }

    static async Task FlavorSaveCases()
    {
        var source=(HbrgoMoveTableMonster)FlavorRoom(typeof(BarrelWalker));for(int turn=0;turn<3;turn++)await FlavorTurn(source);
        string queued=source.FlavorQueuedTableMoveId;var options=new JsonSerializerOptions{IncludeFields=true};
        string json=JsonSerializer.Serialize(SavedProperties.From(source),options);
        var copy=(HbrgoMoveTableMonster)TableActor(typeof(BarrelWalker),0,out Creature actor);
        JsonSerializer.Deserialize<SavedProperties>(json,options)!.Fill(copy);
        Check(copy.FlavorTablePaused && copy.FlavorQueuedTableMoveId==queued && copy.FlavorQueueCursorData==source.FlavorQueueCursorData,"native SavedProperties roundtrip preserves paused move identity and exact before/after cursor");
        Set(copy,"<Id>k__BackingField",ModelDb.GetId(typeof(BarrelWalker)));copy.RunRng=State<RunState>("RunState").Rng;
        SetUp(copy,"DEMISE_CANNON_MOVE");Power<BarrelDemiseCountdownPower>(actor,1);FullShell(actor);
        string rng=FlavorRng();await copy.PerformMove();await FlavorDispatch("AfterSideTurnEnd",CombatSide.Enemy);copy.RollMove([Hero]);
        Check(copy.NextMove.Id==queued && FlavorRng()==rng,"restored paused cannon resumes original saved table option without rerolling");
    }
    public static void RunFlavor(string reportPath)
    {
        bool ok=false;Exception? error=null;
        try{FlavorInstall();PillarFlavorCases().GetAwaiter().GetResult();PillarTargetCases();BarrelFlavorCases().GetAwaiter().GetResult();DiamondFlavorCases().GetAwaiter().GetResult();CrabFlavorCases().GetAwaiter().GetResult();CatFlavorCases().GetAwaiter().GetResult();FlavorIsolationCases();FlavorSaveCases().GetAwaiter().GetResult();GateFlavorCases().GetAwaiter().GetResult();IvoryAbyssProtectionCases().GetAwaiter().GetResult();ok=true;}
        catch(Exception ex){error=ex;Console.Error.WriteLine(ex);}
        var report=new{suite="hbr-enemy-flavor",passed=ok,checks=Passed,error=error?.ToString(),modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),boundaries=new[]{"native move state/PerformMove/turn/death dispatch; attack executor leaf records raw commands","wait/scene preview/history sinks isolated; no Godot UI or live multiplayer"}};
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);File.WriteAllText(reportPath,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("flavor validation failed",error);Console.WriteLine("hbr-enemy-flavor "+Passed.Count+" passed");
    }
}
