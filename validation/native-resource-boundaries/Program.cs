using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Runs;

static class NumericBoundaryStandalone {public static void Run(string path)=>EffectChecks.RunNumeric(path).GetAwaiter().GetResult();}
static partial class EffectChecks
{
    static readonly Harmony NumericHarmony=new("hbr.native-resource-boundaries.checks");
    static int NumericStuns,NumericBlockNotifications;
    static void NumericObserveBlockNotification()=>NumericBlockNotifications++;
    static Type PatchType(string name)=>Mod.GetTypes().Single(type=>type.Name==name);
    static object? PatchCall(string name,string method,params object?[] args)=>Invoke(PatchType(name).GetMethod(method,F)!,null,args);
    static bool NumericListeners(ref IEnumerable<AbstractModel> __result){__result=Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(Player.Relics).ToArray();return false;}
    static bool NumericFalse(ref bool __result){__result=false;return false;}
    static bool NumericStun(ref Task __result){NumericStuns++;__result=Task.CompletedTask;return false;}
    static void NumericInstall(string name,bool optional=false)
    {
        var methods=NumericHarmony.CreateClassProcessor(PatchType(name)).Patch();
        Check(optional||methods.Count>0,"actual Harmony installs "+name+" targets="+methods.Count);
    }
    static void NumericSetup()
    {
        H("Setup");Localization();NonCriticalSeed=FindSeed(false);
        foreach(Type type in Mod.GetTypes().Concat(typeof(PowerModel).Assembly.GetTypes()).Where(type=>!type.IsAbstract&&!type.ContainsGenericParameters&&
            (typeof(PowerModel).IsAssignableFrom(type)||typeof(RelicModel).IsAssignableFrom(type)||typeof(CardPoolModel).IsAssignableFrom(type)||type.Assembly==Mod&&typeof(CardModel).IsAssignableFrom(type))))
        {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
        var state=Blank<PlayerCombatState>();Set(state,"_player",Player);Set(state,"_pets",new List<Creature>());Set(state,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",state);
        Set(RunState,"_allCards",new List<CardModel>());Set(Combat,"_allCards",new List<CardModel>());FormReset();
        Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());
        NumericHarmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NumericFalse),F)!));
        foreach(Type type in new[]{typeof(RunState),typeof(CombatState)})NumericHarmony.Patch(type.GetMethod("IterateHookListeners")!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NumericListeners),F)!){priority=Priority.Last});
        foreach(Type type in new[]{typeof(SfxCmd),typeof(VfxCmd)})foreach(var method in type.GetMethods(F).Where(m=>m.Name.StartsWith("Play")))
            NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        foreach(Type type in new[]{typeof(PowerModel),typeof(RelicModel)})foreach(var method in type.GetMethods(F).Where(m=>m.Name=="Flash"))
            NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))
            NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
        NumericHarmony.Patch(typeof(CeremonialBeast).GetMethod("SetStunned",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
        NumericHarmony.Patch(typeof(CreatureCmd).GetMethod("TriggerAnim",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
        foreach(var method in typeof(CreatureCmd).GetMethods(F).Where(m=>m.Name=="Stun"&&m.ReturnType==typeof(Task)))NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NumericStun),F)!));
        Type energy=typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(var method in energy.GetMethods(F).Where(m=>m.Name=="GetPrefix"))NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Energy),F)!));
        foreach(var method in energy.GetMethods(F).Where(m=>m.Name=="GetPool"))NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(EnergyPool),F)!));
        Type history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
        foreach(var method in history.GetMethods(F).Where(m=>m.Name is "BlockGained" or "PowerReceived" or "DamageReceived" or "DamageGiven"))
            NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        foreach(string name in new[]{"HbrNativeFixedResourcePatch","HbrNativeEffectSkillDescriptionPatch","HbrNativeProfiledEffectDescriptionPatch","HbrNativeEffectSkillTextPatch","HbrNativeAnchorBlockSkillPatch","HbrNativeSkittishBlockPatch","HbrNativeSkittishBlockBadgePatch","HbrPlowThresholdRuntimePatch","HbrPlowThresholdDescriptionPatch","HbrPlowThresholdBadgePatch","HpOnlyDamageContextPatch","CreatureDamageBlockInternalPatch","HbrHandDrillTrueBlockPatch","ItsMygoIncomingDamageCapturePatch"})NumericInstall(name);
        if(AccessTools.DeclaredMethod(typeof(HandDrill),"AfterBlockBroken")!=null||PatchType("HbrHandDrillDamageNotificationPatch").GetMethod("Prepare",F)!=null)
            NumericInstall("HbrHandDrillDamageNotificationPatch",true);
        else throw new Exception("Final source must safely Prepare-skip the beta-only notification on stable");
        // Create the cached real Hook delegate before the Hook prefix is installed.
        // A successful false-proof suppression therefore proves late Harmony binding.
        NumericInstall("HbrHandDrillCallbackScopePatch");
        NumericInstall("PhantomBeforeDamageReceivedPatch");
        NumericHarmony.Patch(typeof(Hook).GetMethod("AfterBlockBroken",F)!,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NumericObserveBlockNotification),F)!));
        if(PatchType("HbrPlowThresholdRuntimePatch").GetMethod("Prefix",F)!=null)NumericInstall("LargeCreaturePlowThresholdPatch");
        else Check(Environment.GetEnvironmentVariable("HBR_NUMERIC_DEVELOPMENT")=="1","final Plow must share existing native threshold transpiler");
    }
    static void NumericReset(bool hbr=true)
    {
        FormReset(hbr);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());Set(Hero,"_currentHp",5000);Set(Hero,"_maxHp",5000);Set(Enemy,"_currentHp",500000);Set(Enemy,"_maxHp",500000);
        Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);Set(Combat,"<RoundNumber>k__BackingField",1);
        SetNative(Enemy,0);SetNative(Hero,0);NumericStuns=NumericBlockNotifications=0;((List<RelicModel>)typeof(Checks).GetField("ExtraListeners",F)!.GetValue(null)!).Clear();
    }
    static void SetNative(Creature owner,int value)=>typeof(Creature).GetFields(F).Single(f=>f.Name is "_block" or "<Block>k__BackingField").SetValue(owner,value);
    static decimal NumericNativeBlock(Creature owner)=>System.Convert.ToDecimal(Call("DodgeCombatHelper","GetNativeBlock",owner));
    static T NumericRelic<T>()where T:RelicModel
    {var relic=(T)ModelDb.GetById<RelicModel>(ModelDb.GetId(typeof(T))).ToMutable();Set(relic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(relic).ToList());((List<RelicModel>)typeof(Checks).GetField("ExtraListeners",F)!.GetValue(null)!).Add(relic);return relic;}
    static DamageResult NumericResult(Creature target,int damage,int blocked=0,bool broken=false)
    {var result=Blank<DamageResult>();Set(result,"<Receiver>k__BackingField",target);Set(result,"<UnblockedDamage>k__BackingField",damage);Set(result,"<BlockedDamage>k__BackingField",blocked);Set(result,"<WasBlockBroken>k__BackingField",broken);return result;}
    static AttackCommand NumericAttack()
    {
        if(!ModelDb.Contains(typeof(StrikeIronclad)))ModelDb.Inject(typeof(StrikeIronclad));
        var attack=DamageCmd.Attack(1m);Set(attack,"<Attacker>k__BackingField",Hero);Set(attack,"<ModelSource>k__BackingField",FormCard<StrikeIronclad>());return attack.Targeting(Enemy);
    }
    static string NumericPowerText(PowerModel power)
    {
        LocString text=power.SmartDescription;text.Add("Amount",power.Amount);text.Add("OwnerName","fixture");text.Add("OnPlayer",false);text.Add("IsMultiplayer",false);text.Add("PlayerCount",1);
        var fill=typeof(PowerModel).GetMethod("AddDumbVariablesToDescription",F)!;Invoke(fill,power,text,null);power.DynamicVars.AddTo(text);return text.GetFormattedText();
    }
    static async Task BlockCases()
    {
        NumericReset();var ripple=NumericRelic<RippleBasin>();
        decimal expected=(decimal)Call("HbrNativeEffectSkillHelper","ResolveNativeBlockAmount",Hero,4m)!;
        Equal(expected,400m,"Ripple source4 maps once to INT100 block400");
        await CreatureCmd.GainBlock(Hero,ripple.DynamicVars.Block,null,true);Equal(NumericNativeBlock(Hero),400m,"real native typed BlockVar command resolves Ripple exactly once");
        SetNative(Hero,0);Power<DexterityPower>(Hero,50);expected=(decimal)Call("HbrNativeEffectSkillHelper","ResolveNativeBlockAmount",Hero,4m)!;
        await CreatureCmd.GainBlock(Hero,ripple.DynamicVars.Block,null,true);Equal(NumericNativeBlock(Hero),expected,"Ripple real block follows current INT rather than fixed400");Check(expected>400,"Dexterity actually increases Ripple block");Check(ripple.DynamicDescription.GetFormattedText().Contains(expected.ToString("0")),"actual Ripple native dynamic description matches granted block");
        NumericReset(false);ripple=NumericRelic<RippleBasin>();await CreatureCmd.GainBlock(Hero,ripple.DynamicVars.Block,null,true);Equal(NumericNativeBlock(Hero),4m,"legacy Ripple remains native4");
        NumericReset();var skittish=Power<SkittishPower>(Enemy,7);var attack=NumericAttack();
        ((List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(attack)!).Add(new(){NumericResult(Enemy,1)});
        expected=(decimal)Call("HbrNativeEffectSkillHelper","ResolveNativeBlockAmount",Enemy,7m)!;
        await skittish.AfterAttack(new ThrowingPlayerChoiceContext(),attack);Equal(NumericNativeBlock(Enemy),expected,"real native Skittish AfterAttack gains HBR-scaled block");Equal(skittish.Amount,7,"Skittish raw saved amount remains7");Equal(skittish.DisplayAmount,expected,"Skittish real badge uses resolved block");Check(NumericPowerText(skittish).Contains(expected.ToString("0")),"Skittish real SmartDescription path matches granted block");
        await skittish.AfterAttack(new ThrowingPlayerChoiceContext(),attack);Equal(NumericNativeBlock(Enemy),expected,"Skittish native once-turn receipt retained");
        await skittish.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Player,new[]{Hero});await skittish.AfterAttack(new ThrowingPlayerChoiceContext(),attack);Equal(NumericNativeBlock(Enemy),expected*2,"Skittish native reset permits next trigger");
        NumericReset(false);skittish=Power<SkittishPower>(Enemy,7);attack=NumericAttack();((List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(attack)!).Add(new(){NumericResult(Enemy,1)});
        await skittish.AfterAttack(new ThrowingPlayerChoiceContext(),attack);Equal(NumericNativeBlock(Enemy),7,"legacy Skittish remains native7");
        NumericReset();await CreatureCmd.GainBlock(Enemy,8343900m,ValueProp.Unpowered,null,true);Equal(NumericNativeBlock(Enemy),8343900m,"already-world-unit Plating-style decimal block not multiplied");
    }
    static async Task PlowCases()
    {
        foreach(bool hbr in new[]{true,false})
        {
            NumericReset(hbr);var beast=Blank<CeremonialBeast>();var target=(Creature)H("Actor",beast,null,null)!;
            beast.BeastCryState=new MoveState("BEAST_CRY",_=>Task.CompletedTask);var plow=Power<PlowPower>(target,160);decimal threshold=hbr?112000m:160m;
            Equal(plow.DisplayAmount,threshold,"Plow badge has correct mode-scale");Equal(plow.Amount,160,"Plow raw saved threshold unchanged");Check(NumericPowerText(plow).Contains(threshold.ToString("0")),"Plow actual SmartDescription matches runtime threshold");
            Set(target,"_currentHp",(int)threshold+1);await plow.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,NumericResult(target,1),ValueProp.Move,Hero,null);Check(NumericStuns==0&&Powers(target).Contains(plow),"Plow above threshold does not trigger");
            Set(target,"_currentHp",(int)threshold);Power<StrengthPower>(target,8);await plow.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,NumericResult(target,0),ValueProp.Move,Hero,null);Check(NumericStuns==0,"Plow still requires positive actual HP damage");
            await plow.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,NumericResult(target,1),ValueProp.Move,Hero,null);Check(NumericStuns==1&&!Powers(target).Contains(plow)&&!target.HasPower<StrengthPower>(),"real Plow hook at threshold stuns and removes itself/strength once");
        }
        // Reinstall in the reverse relative order. Only the old patch owns native IL;
        // the new prefix owns the shared comparison helper, so both orders compose.
        var oldPatch=PatchType("LargeCreaturePlowThresholdPatch");var nativeTarget=(MethodBase)Invoke(oldPatch.GetMethod("TargetMethod",F)!,null)!;
        var runtimePatch=PatchType("HbrPlowThresholdRuntimePatch");var newPrefix=runtimePatch.GetMethod("Prefix",F);
        if(newPrefix!=null)
        {
            NumericHarmony.Unpatch(nativeTarget,oldPatch.GetMethod("Transpiler",F)!);
            var comparison=Helper("LargeCreatureHealthHelper").GetMethod("GetCurrentForPlowComparison",F)!;
            NumericHarmony.Unpatch(comparison,newPrefix);NumericInstall("LargeCreaturePlowThresholdPatch");NumericInstall("HbrPlowThresholdRuntimePatch");
            NumericReset();var beast=Blank<CeremonialBeast>();var target=(Creature)H("Actor",beast,null,null)!;beast.BeastCryState=new MoveState("CRY",_=>Task.CompletedTask);
            var plow=Power<PlowPower>(target,160);var large=Power<LargeHealthPower>(target,1);Native(large,"Store",112001m,5000000000m);Set(target,"_currentHp",1);
            await plow.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,NumericResult(target,1),ValueProp.Move,Hero,null);Check(NumericStuns==0,"reverse patch order retains real large HP above threshold despite low native proxy");
            Native(large,"Store",112000m,5000000000m);await plow.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,NumericResult(target,1),ValueProp.Move,Hero,null);Check(NumericStuns==1,"reverse patch order triggers at exact large-health threshold");
        }
        NumericReset();var canonical=ModelDb.Power<PlowPower>();Equal(canonical.DisplayAmount,canonical.Amount,"canonical Plow unbound badge remains safe");
    }
    sealed class NumericEventStop:Exception { }
    static decimal NumericEventAmount;
    static void NumericEventDamage(object[] __args){NumericEventAmount=(decimal)__args[2];throw new NumericEventStop();}
    static async Task WuppyCases()
    {
        NumericReset();Type? type=typeof(EventModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Events.WuppyInTrouble");
        if(type==null){Check(true,"Wuppy absent from native stable: optional event migration safely omitted");return;}
        if(!ModelDb.Contains(type))ModelDb.Inject(type);
        var evt=(EventModel)ModelDb.GetById<EventModel>(ModelDb.GetId(type)).ToMutable();typeof(EventModel).GetProperty("Owner",F)!.SetValue(evt,Player);
        foreach(int hp in new[]{2800,3500,1,999999})
        {Set(Hero,"_currentHp",hp);evt.CalculateVars();Equal(evt.DynamicVars.HpLoss.BaseValue,hp*0.05m,"native Wuppy retains actual currentHP5percent raw result");Equal(evt.DynamicVars.HpLoss.IntValue,decimal.Floor(hp*0.05m),"Wuppy dynamic IntValue no longer multiplies a world-scale percentage result");}
        Set(Hero,"_currentHp",2800);evt.CalculateVars();var stop=new Harmony("hbr.numeric.wuppy.damage-leaf");
        MethodInfo canonical=(MethodInfo)Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!.GetField("Command",F)!.GetValue(null)!;
        stop.Patch(canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NumericEventDamage),F)!){priority=Priority.First});
        try{try{await (Task)Native(evt,"BeCautious")!;throw new Exception("Expected event presentation boundary");}catch(NumericEventStop){Equal(NumericEventAmount,140m,"real Wuppy cautious action forwards currentHP2800 times5percent140 once");}}
        finally{stop.UnpatchAll(stop.Id);}
    }

    static object NumericBegin(Creature target,Creature dealer,ValueProp props)
    {object?[] args={new[]{target},30m,props,dealer,null};return Call("HpOnlyDamageContext","Begin",args)!;}
    static DamageResult NumericBlockReceipt(Creature target,Creature dealer,int block,int dp,decimal amount,ValueProp props)
    {
        SetNative(target,block);if(dp>0){var shell=(HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();Set(shell,"_owner",target);Powers(target).Add(shell);Native(shell,"SetDpAmount",(decimal)dp,false);}
        object scope=NumericBegin(target,dealer,props);
        try
        {
            decimal blocked=target.DamageBlockInternal(amount,props);var result=NumericResult(target,(int)Math.Max(0,amount-blocked),(int)blocked,target.Block==0&&blocked>0);
            Call("HpOnlyDamageContext","BindDpReceipts",new List<DamageResult>{result});
            object?[] proof={result,false};Check((bool)Call("HpOnlyDamageContext","TryGetNativeBlockBreak",proof)!,"actual DamageBlockInternal binds native-block proof");Equal((bool)proof[1]! ?1:0,block>0&&block<=amount&&!props.HasFlag(ValueProp.Unblockable)?1:0,"receipt distinguishes real block from DP/31F absorption");return result;
        }
        finally{Call("HpOnlyDamageContext","Close",scope);}
    }
    static async Task NumericNotify(HandDrill drill,DamageResult result,Creature target)
    {
#if HBR_BETA_NATIVE
        await (Task)PatchCall("HbrHandDrillDamageNotificationPatch","Notify",Combat,new ThrowingPlayerChoiceContext(),target,Hero,result)!;
#else
        await (Task)Native(drill,"AfterDamageGiven",new ThrowingPlayerChoiceContext(),Hero,result,ValueProp.Move,target,null)!;
#endif
    }
#if HBR_BETA_NATIVE
    sealed class NumericNestedBlockPower:PowerModel
    {
        internal bool Armed=true;
        internal static int Calls;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
        public override async Task AfterBlockBroken(PlayerChoiceContext context,Creature target,Creature? breaker)
        {
            Calls++;Cases.Add(new{nestedCall=Calls,armed=Armed,targetIsOwner=target==Owner,proof=(bool)PatchCall("HbrHandDrillBlockReceiptContext","IsProvenNotBroken",target)!});if(!Armed||target!=Owner)return;Armed=false;
            // A real native block removal during a reaction has no damage-result
            // intent. Its notification must not borrow the outer DP proof.
            SetNative(target,1);await NumericLoseBlock(context,target,1m,breaker);
        }
    }
#endif
    static Task NumericLoseBlock(PlayerChoiceContext context,Creature target,decimal amount,Creature? remover)
    {var type=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;return (Task)Invoke(type.GetMethod("LoseBlock",F)!,null,context,target,amount,remover)!;}
    static async Task DrillCases()
    {
        foreach(var row in new (int Block,int Dp,decimal Damage,ValueProp Props,bool Expected)[]{(0,100,30m,ValueProp.Move,false),(5,100,30m,ValueProp.Move,true),(40,100,30m,ValueProp.Move,false),(5,0,30m,ValueProp.Move,true),(0,100,30m,ValueProp.Unpowered,false),(5,100,30m,ValueProp.Unblockable,false)})
        {
            NumericReset();var drill=NumericRelic<HandDrill>();DamageResult result=NumericBlockReceipt(Enemy,Hero,row.Block,row.Dp,row.Damage,row.Props);await NumericNotify(drill,result,Enemy);
            Check(Enemy.HasPower<VulnerablePower>()==row.Expected,"real HandDrill native callback block="+row.Block+" DP="+row.Dp+" props="+row.Props+" expected="+row.Expected);
        }
        NumericReset();var unknown=NumericRelic<HandDrill>();var noReceipt=NumericResult(Enemy,0,5,true);await NumericNotify(unknown,noReceipt,Enemy);Check(Enemy.HasPower<VulnerablePower>(),"unknown native block-break evidence retains original HandDrill behavior");
        NumericReset();NumericRelic<HandDrill>();SetNative(Enemy,5);await NumericLoseBlock(new ThrowingPlayerChoiceContext(),Enemy,5m,Hero);Equal(NumericNativeBlock(Enemy),0,"real native LoseBlock executes without a damage-result receipt");
#if HBR_BETA_NATIVE
        Check(Enemy.HasPower<VulnerablePower>(),"beta native block remover attribution still triggers original HandDrill without receipt");
#else
        Check(!Enemy.HasPower<VulnerablePower>(),"stable native LoseBlock retains its original absence of HandDrill damage-given callback");
#endif

        foreach(int initialBlock in new[]{0,5})
        {
            NumericReset();NumericRelic<HandDrill>();SetNative(Enemy,initialBlock);
            var shell=(HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();Set(shell,"_owner",Enemy);Powers(Enemy).Add(shell);Native(shell,"SetDpAmount",100m,false);
            var extras=typeof(Player).GetProperty("ExtraFields",F)!;Set(Player,"<ExtraFields>k__BackingField",System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(extras.PropertyType));
            var api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;
            var entry=api.GetMethods(F).Single(m=>m.Name=="Damage"&&m.GetParameters() is var ps&&ps.Length==7&&ps[1].ParameterType==typeof(IEnumerable<Creature>));
            var results=(await (Task<IEnumerable<DamageResult>>)Invoke(entry,null,new ThrowingPlayerChoiceContext(),new[]{Enemy},30m,ValueProp.Unpowered,Hero,null,null)!).ToList();
            Check(results.Count==1&&results[0].WasBlockBroken,"real canonical native damage still reports its original DP-inclusive block-break flag");
            Check(Enemy.HasPower<VulnerablePower>()==(initialBlock>0),"real canonical damage notification preserves HandDrill only for actual native block break");
        }

#if HBR_BETA_NATIVE
        NumericReset();var nestedDrill=NumericRelic<HandDrill>();var outerFalse=NumericBlockReceipt(Enemy,Hero,0,100,30m,ValueProp.Move);
        var nested=Blank<NumericNestedBlockPower>();nested.Armed=true;Set(nested,"<IsMutable>k__BackingField",true);Set(nested,"_owner",Enemy);Powers(Enemy).Add(nested);
        await NumericNotify(nestedDrill,outerFalse,Enemy);Check(!nested.Armed,"real native Hook invoked nested power listener before relic");Equal(Enemy.GetPowerAmount<VulnerablePower>(),2,"nested same-target unknown native notification is allowed once while outer known-DP notification remains suppressed");Check(!nested.Armed,"real native Hook invoked the nested listener");
#endif
        await NumericProtectionAndRedirectCases();
        Check(AsyncValue("HpOnlyDamageContext","Current")==null,"block receipt scopes restore caller after all notifications");
    }
    static async Task NumericProtectionAndRedirectCases()
    {
        foreach(int initialBlock in new[]{0,5})
        {
            NumericReset();NumericRelic<HandDrill>();NumericRelic<ThirtyOneFCollection>();
            var members=Mod.GetTypes().Where(t=>!t.IsAbstract&&typeof(CardModel).IsAssignableFrom(t)&&typeof(HBRGOSakiMod.Core.Models.Capabilities.IThirtyOneFMemberCard).IsAssignableFrom(t))
                .Select(t=>(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(t)).ToMutable()).GroupBy(c=>((HBRGOSakiMod.Core.Models.Capabilities.IThirtyOneFMemberCard)c).ThirtyOneFMember).Select(g=>g.First()).ToArray();
            foreach(var card in members){Set(card,"_owner",Player);Player.Deck.AddInternal(card);}
            Check(members.Length==6&&(bool)Call("ThirtyOneFBondHelper","HasBulwark",Player)!,"real six-member31F deck qualifies the established defensive action");
            var shell=(HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();Set(shell,"_owner",Hero);Powers(Hero).Add(shell);Native(shell,"SetDpAmount",20m,false);SetNative(Hero,initialBlock);
            object action=Call("ThirtyOneBEnemyActionHelper","Begin",Enemy)!;Check(action!=null,"real enemy action admits genuine31F owner");Set(action,"ButlerOwners",new HashSet<Player>{Player});
            try
            {
                var api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;var entry=api.GetMethods(F).Single(m=>m.Name=="Damage"&&m.GetParameters() is var ps&&ps.Length==7&&ps[1].ParameterType==typeof(IEnumerable<Creature>));
                var results=(await (Task<IEnumerable<DamageResult>>)Invoke(entry,null,new ThrowingPlayerChoiceContext(),new[]{Hero},30m,ValueProp.Move,Enemy,null,null)!).ToList();
                Equal(shell.CurrentDp,20m,"established31F protection leaves real DP unchanged while native Block is resolved");Equal(NumericNativeBlock(Hero),0m,"31F protection preserves ordinary block-consumption semantics");
                Check(results.Single().WasBlockBroken&&NumericBlockNotifications==1,"DP immunity keeps native block-break notification for other consumers");
                object?[] proof={results.Single(),false};Check(!(bool)Call("HpOnlyDamageContext","TryGetNativeBlockBreak",proof)!,"enemy-dealer31F action cannot borrow player HandDrill capture ownership");
                Check(!Hero.HasPower<VulnerablePower>(),"native HandDrill owner/target rules remain unchanged on enemy31F action");
            }
            finally{Call("ThirtyOneBEnemyActionHelper","Close",action);}
        }
        NumericReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());NumericRelic<MomentOfReign>();var drill=NumericRelic<HandDrill>();
        Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        Check((bool)Call("PhantomHelper","IsOwnedMainPhantom",phantom,Player)!,"redirect fixture is a real owned main phantom");SetNative(phantom,5);SetNative(Hero,0);
        object scope=NumericBegin(Hero,Hero,ValueProp.Move);
        try
        {
            PatchCall("PhantomDamageContext","Push",phantom);Call("HpOnlyDamageContext","RecordPhysicalTarget",phantom);
            decimal blocked=Hero.DamageBlockInternal(5m,ValueProp.Move);var result=NumericResult(Hero,0,(int)blocked,true);Call("HpOnlyDamageContext","BindDpReceipts",new List<DamageResult>{result});
            object?[] proof={result,false};Check((bool)Call("HpOnlyDamageContext","TryGetNativeBlockBreak",proof)!&&(bool)proof[1]!,"main phantom redirect retains genuine physical-block break proof");
            Equal(blocked,5m,"redirected real block absorbs unchanged5");Equal(NumericNativeBlock(phantom),0m,"main phantom real block was consumed once");Equal(NumericNativeBlock(Hero),0m,"redirect does not manufacture owner Block");
            Check(phantom.CurrentHp==5000&&Hero.CurrentHp==5000&&!(bool)PatchType("PhantomDamageContext").GetProperty("IsRedirectingBlock",F)!.GetValue(null)!,"redirect preserves HP and restores original redirect flag");
            await NumericNotify(drill,result,Hero);Check(!Hero.HasPower<VulnerablePower>(),"proof does not change HandDrill's original player-target exclusion");
        }
        finally{Call("HpOnlyDamageContext","Close",scope);}
    }
    public static async Task RunNumeric(string path)
    {
        string? error=null;var failures=new List<string>();try{NumericSetup();foreach(var group in new (string Name,Func<Task> Run)[]{("block",BlockCases),("plow",PlowCases),("wuppy",WuppyCases),("drill",DrillCases)}){try{await group.Run();}catch(Exception ex){failures.Add(group.Name+": "+ex);Console.Error.WriteLine(group.Name+": "+ex);}}if(failures.Count>0)error=string.Join("\n\n",failures);}catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,cases=Cases,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native GainBlock and Skittish/Plow hooks; real DamageBlockInternal with production DP postfix and receipt binding; actual native HandDrill callback and beta notification adapter; Harmony installs exact native MoveNext callsite patches. Wait/audio/visual/history/stun presentation leaves isolated. No Godot rendering or full game turn loop."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" numeric boundary checks");if(error!=null)throw new Exception("See "+path);
    }
}
