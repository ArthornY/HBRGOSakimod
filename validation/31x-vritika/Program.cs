using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class Vritika92Standalone{public static void Run(string path)=>EffectChecks.X91Run(path,EffectChecks.VritikaCases);}
static partial class EffectChecks
{
    static JsonObject CalcSaved(VritikaCalculationPower power)=>JsonNode.Parse(power.NormalSequences)!.AsArray().Single()!.AsObject();
    static int CalcProgress(VritikaCalculationPower? power=null)=>CalcSaved(power??Hero.GetPower<VritikaCalculationPower>()!)["Progress"]!.GetValue<int>();
    static void CalcLoadOrder(int bits)
    {
        var power=Hero.GetPower<VritikaCalculationPower>()!;JsonObject row=CalcSaved(power);row["Bits"]=bits;row["Progress"]=0;row["Listening"]=true;
        Native(power,"set_NormalSequences",new JsonArray(row.DeepClone()).ToJsonString());object table=Helper("VritikaCalculationCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);
    }
    static CardModel CalcCard(bool skill)=>skill?FormCard<X92Skill>():FormCard<X92Attack>();
    static async Task<VritikaCalculationPower> CalcStart(params int[] upgrades)
    {await X91Play(NewGarden<NumbersAreTheSourceHbrgoSaki>(upgrades));return Hero.GetPower<VritikaCalculationPower>()!;}
    static async Task CalcThree(VritikaCalculationPower power)
    {int bits=CalcSaved(power)["Bits"]!.GetValue<int>();for(int step=0;step<3;step++)await X91Play(CalcCard((bits&(1<<step))!=0));}
    static async Task CalculationBasicCases()
    {
        X91Reset();var card=NewGarden<NumbersAreTheSourceHbrgoSaki>();Equal(card.EnergyCost.Canonical,1,"92 native canonical cost1");Check(card.Type==CardType.Skill&&card.MaxUpgradeLevel==7,"92 rare skill has seven upgrades");
        int niche=RngCount(RunState.Rng.Niche),targets=RngCount(RunState.Rng.CombatTargets),selection=RngCount(RunState.Rng.CombatCardSelection);
        await X91Play(card);var power=Hero.GetPower<VritikaCalculationPower>()!;
        Equal(RngCount(RunState.Rng.Niche),niche,"named calculation order consumes no shared niche RNG");Equal(RngCount(RunState.Rng.CombatTargets),targets,"named order consumes no target RNG");Equal(RngCount(RunState.Rng.CombatCardSelection),selection,"named order consumes no selection RNG");
        var down=Enemy.GetPower<HbrAttackDownPower>()!;Equal(down.Amount,1,"base attack-down expires on next enemy end");Equal((decimal)Native(down,"get_EffectPercent")!,(decimal)Call("HbrPercentDebuffHelper","ResolveBasis",down,20m,Hero,Enemy)!,"base20attackdown resolves through real attribute formula");
        Check(CalcSaved(power)["Bits"]!.GetValue<int>() is >=0 and <8&&CalcProgress()==0,"visible persisted three-step order starts at zero and excludes its own skill");
        string json=power.NormalSequences;X91SaveType(typeof(VritikaCalculationPower));var snapshot=SavedProperties.From(power);var copy=(VritikaCalculationPower)power.MutableClone();snapshot.Fill(copy);Check(copy.NormalSequences==json&&copy.SerialData==power.SerialData,"actual SavedProperties roundtrip preserves sequence/order/serial");
        await CalcThree(power);Check(X92Bonuses.Select(row=>row.Bonus).SequenceEqual(new[]{10m,10m,10m}),"three real native matching wrappers each receive10percent");Equal(CalcProgress(),3,"three matches complete progress3");Equal(X91Energy,1,"success grants1energy once");Equal(X91Draws,1,"success draws1once");
        Equal((decimal)Call("VritikaCalculationCombatHelper","GetCalculationBonusPercent",X92Bonuses.Last().Source,Hero,ValueProp.Move)!,0,"bonus ends with its exact card wrapper");
        X91Reset();power=await CalcStart();await X91Play(FormCard<X92Power>());Check(!(bool)CalcSaved(power)["Listening"]!.GetValue<bool>()&&CalcProgress()==0,"a real native power play interrupts the order without progress");Equal(X91Energy,0,"interruption grants no reward");
        X91Reset();power=await CalcStart(5);await CalcThree(power);Check(X92Bonuses.Select(row=>row.Bonus).SequenceEqual(new[]{10m,20m,30m}),"Multiplication grants distinct10/20/30percent per matched wrapper");
        X91Reset();power=await CalcStart();int previousSerial=int.Parse(power.SerialData);await X91Play(NewGarden<NumbersAreTheSourceHbrgoSaki>());Equal(CalcProgress(),0,"new calculation physical card resets to stepzero");Equal(int.Parse(power.SerialData),previousSerial+1,"new physical play advances independent named-stream serial exactly once");
        X91Reset();power=await CalcStart(0);Equal(Player.PlayerCombatState!.Energy,30,"Flash pays no energy through native resource wrapper");
    }
    static async Task CalculationUpgradesAndReplayCases()
    {
        X91Reset();await CalcStart(1);Check(X91Hits.Count==3&&X91Hits.All(hit=>hit.Raw==3.06m&&hit.Destruction==1.2m/3m),"CompleteSubtraction performs real51percent three-hit command at coefficient1point2");
        X91Reset();await CalcStart(2);Equal(Enemy.GetPower<HbrAttackDownPower>()!.Amount,3,"DancingKali attack-down persists three enemy ends");Equal(Enemy.GetPower<HbrCalamityPower>()!.Amount,3,"DancingKali applies actual calamity3");
        X91Reset();await CalcStart(3);Equal(Hero.GetPower<HbrDefenseUpPower>()!.Amount,1,"Rhythm defense-up lasts one enemy end");
        X91Reset();var dp=NewDp(0);var power=await CalcStart(4);await CalcThree(power);Equal(dp.Dp,decimal.Floor(dp.MaxDp*.05m),"successful UltimateCurry restores actual5percent maximumDP without overcap");
        X91Reset();power=await CalcStart(6);CalcLoadOrder(0);for(int step=0;step<4;step++)await X91Play(FormCard<X92Attack>());Check(X91Hits.Count==4&&X91Hits.All(hit=>hit.Raw==1.2m),"Kabaddi attacks once per physical attack play and continues after success");
        Check(X92Bonuses.Count==4,"Kabaddi damage is not an additional attack-card play");
        X91Reset();power=await CalcStart(6);CalcLoadOrder(1);await X91Play(FormCard<X92Attack>());Equal(X91Hits.Count,0,"mismatching attack immediately stops Kabaddi");
        X91Reset();power=await CalcStart(6);CalcLoadOrder(0);var observer=Power<X91ReplayPower>(Hero,1);observer.Replays=2;var attack=FormCard<X92Attack>();await X91Play(attack);
        Check(X92Bonuses.Count==3&&X92Bonuses.All(row=>row.Bonus==10m),"native GeneratePlayCount3 retains the one physical wrapper bonus on all replays");Equal(CalcProgress(),1,"native replay series advances sequence only once");Equal(X91Hits.Count,1,"native replay series triggers Kabaddi only once");
        observer.Replays=0;await X91Play(FormCard<X92Attack>());observer.Replays=2;await X91Play(FormCard<X92Attack>());Equal(X91Energy,1,"third-step replay series awards energy once");Equal(X91Draws,1,"third-step replay series awards draw once");Equal(X91Hits.Count,3,"three physical plays with replays have three Kabaddi attacks");
        X91Reset();observer=Power<X91ReplayPower>(Hero,1);observer.Replays=2;await X91Play(NewGarden<NumbersAreTheSourceHbrgoSaki>());power=Hero.GetPower<VritikaCalculationPower>()!;Equal(int.Parse(power.SerialData),1,"calculation-card own native replay series generates its order only once");
        X91Reset();power=await CalcStart();CalcLoadOrder(0);await X91Play(FormCard<X92Attack>(),true);Equal(CalcProgress(),1,"real native AutoPlay participates in calculation");
        X91Reset();var full=NewGarden<NumbersAreTheSourceHbrgoSaki>(0,1,2,3,4,5,6);X91SaveType(full.GetType());var saved=SavedProperties.From(full);var clone=(NumbersAreTheSourceHbrgoSaki)full.MutableClone();saved.Fill(clone);Check(clone.UpgradeEffectOrder.SequenceEqual(full.UpgradeEffectOrder),"all seven upgrades survive native SavedProperties");
    }
    static async Task CalculationScopeAndAmountCases()
    {
        X91Reset();var power=await CalcStart();CalcLoadOrder(0);await X91Play(FormCard<X92Attack>());Equal(CalcProgress(),1,"ordinary calculation has one prior match");
        using(MisatoExtra(false))
        {
            await X91Play(FormCard<X92Attack>());Equal(X92Bonuses.Last().Bonus,0,"ordinary sequence does not boost a personal extra-turn card");
            await X91Play(NewGarden<NumbersAreTheSourceHbrgoSaki>());
            string[] order=((string)Call("VritikaCalculationCombatHelper","Display",Player)!.GetType().GetProperty("Sequence")!.GetValue(Call("VritikaCalculationCombatHelper","Display",Player)!)!).Split(" → ");
            await X91Play(CalcCard(order[0]=="技能"));await X91Play(CalcCard(order[1]=="技能"));
            Equal((int)Native(power,"get_DisplayAmount")!,2,"personal calculation advances its own two steps while ordinary sequence is paused");Equal(CalcProgress(power),1,"extra play does not overwrite persisted ordinary progress");
            object token=Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player)!;
            Call("VritikaCalculationCombatHelper","OnTurnEnd",Player,token);Equal((int)Native(power,"get_DisplayAmount")!,0,"ending personal scope removes only its visible current sequence");
        }
        // The persisted normal branch is kept when an extra turn ends.
        Check(Hero.HasPower<VritikaCalculationPower>()&&(int)Native(power,"get_DisplayAmount")! ==1,"returning ordinary turn restores its exact paused one-step sequence");
        MethodInfo end=typeof(Hook).GetMethods(F).Single(method=>method.Name is"AfterTurnEnd"or"AfterSideTurnEnd");
        object?[] endArgs=end.GetParameters().Select(parameter=>parameter.ParameterType==typeof(CombatSide)?(object)CombatSide.Player:
            parameter.ParameterType.IsInstanceOfType(Combat)?Combat:parameter.ParameterType.IsInstanceOfType(RunState)?RunState:
            typeof(PlayerChoiceContext).IsAssignableFrom(parameter.ParameterType)?new ThrowingPlayerChoiceContext():
            parameter.ParameterType==typeof(IEnumerable<Creature>)?new[]{Hero}:throw new Exception(parameter.ToString())).ToArray();
        await(Task)Invoke(end,null,endArgs)!;Check(!Hero.HasPower<VritikaCalculationPower>(),"real native ordinary turn-end hook clears the completed turn's sequence");
        X91Reset();power=await CalcStart(5);CalcLoadOrder(1);var skill=FormCard<X92Skill>();decimal baselineBlock=Hook.ModifyBlock(Combat,Hero,5m,ValueProp.Move,skill,null,out _);Call("YanagiButlerCombatHelper","Prepare",skill);
        decimal block=0,recovery=0;X92Action=async(context,play)=>
        {
            block=Hook.ModifyBlock(Combat,Hero,5m,ValueProp.Move,play.Card,play,out _);
            recovery=(decimal)Call("YanagiButlerCombatHelper","ScaleRecovery",play.Card,100m)!;
            await Task.Yield();
        };
        await X91Play(skill);Equal(recovery,130m,"calculation10and prepared20 combine additively before one recovery budget multiplication");
        Equal(block,baselineBlock*1.3m,"real native ModifyBlock combines prepared20and calculation10 in one block multiplier");
        X91Reset();power=await CalcStart();CalcLoadOrder(0);ExecutionContext? captured=null;X92Action=(_,play)=>{captured=ExecutionContext.Capture();return Task.CompletedTask;};var card=FormCard<X92Attack>();await X91Play(card);
        ExecutionContext.Run(captured!,_=>Equal((decimal)Call("VritikaCalculationCombatHelper","GetCalculationBonusPercent",card,Hero,ValueProp.Move)!,0,"captured execution context cannot revive a closed play mark"),null);
        X91Reset();power=await CalcStart();CalcLoadOrder(0);X92Action=(_,_)=>Task.FromException(new InvalidOperationException("controlled calculation failure"));card=FormCard<X92Attack>();try{await X91Play(card);throw new Exception("expected controlled failure");}catch(Exception failure)when(failure.ToString().Contains("controlled calculation")){}
        Equal((decimal)Call("VritikaCalculationCombatHelper","GetCalculationBonusPercent",card,Hero,ValueProp.Move)!,0,"faulted native wrapper clears source bonus");
    }
    static async Task CalculationSavedAndNativeResourceCases()
    {
        X91Reset();var power=await CalcStart();int bits=CalcSaved(power)["Bits"]!.GetValue<int>();await X91Play(CalcCard((bits&1)!=0));
        X91SaveType(typeof(VritikaCalculationPower));var options=new JsonSerializerOptions{IncludeFields=true};var saved=JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(power),options),options)!;
        var restored=(VritikaCalculationPower)ModelDb.Power<VritikaCalculationPower>().ToMutable();saved.Fill(restored);power.RemoveInternal();restored.ApplyInternal(Hero,1);
        object table=Helper("VritikaCalculationCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);
        await X91Play(CalcCard((bits&2)!=0));await X91Play(CalcCard((bits&4)!=0));Equal(CalcProgress(restored),3,"SavedProperties restored power continues the same second/third order steps");Equal(X91Energy,1,"restored sequence awards its completion once");
        X91Reset();power=await CalcStart();CalcLoadOrder(0);var next=FormCard<X92Attack>();int rng=RngCount(RunState.Rng.Niche);
        for(int index=0;index<5;index++)Equal((decimal)Call("VritikaCalculationCombatHelper","GetPreviewCalculationBonusPercent",next)!,10,"readonly next-step preview predicts matching bonus");
        Equal(CalcProgress(),0,"preview does not advance sequence");Equal(RngCount(RunState.Rng.Niche),rng,"preview consumes no combat RNG");
        Equal((decimal)Call("VritikaCalculationCombatHelper","GetCalculationBonusPercent",next,Hero,ValueProp.Move)!,0,"predicted bonus cannot leak into actual source outside a play");
        X91Reset();power=await CalcStart();CalcLoadOrder(1);var dp=NewDp(0);X92Action=async(ctx,play)=>await(Task)Call("SeiraDpRecoveryHelper","RestoreAmount",ctx,Hero,10m,false,play.Card)!;await X91Play(FormCard<X92Skill>());Equal(dp.Dp,11,"matched native skill wrapper scales one explicit DP recovery budget by10percent");
        Type native=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards.NotYet",true)!;X91Register(native);
        CardModel HealingCard()=>(CardModel)Invoke(typeof(EffectChecks).GetMethod(nameof(FormCard),F)!.MakeGenericMethod(native),null,false)!;
        X91Reset();await X91Play(HealingCard());decimal ordinary=X91Heals.Last();
        X91Reset();power=await CalcStart();CalcLoadOrder(1);await X91Play(HealingCard());Equal(X91Heals.Last(),ordinary*1.1m,"actual original NotYet OnPlay healing callsite receives the matching card's10percent bonus");
        X91Reset();power=await CalcStart();CalcLoadOrder(1);X92Action=async(_,_)=>await CreatureCmd.Heal(Hero,100m);await X91Play(FormCard<X92Skill>());Equal(X91Heals.Last(),100m,"unbound HP healing called during a marked play does not borrow that play's bonus");
        X91Reset();var baseline=FormCard<X92Attack>();await(Task)Invoke(Helper("HbrEffectApplication").GetMethods(F).Single(method=>method.Name=="Apply"&&!method.IsGenericMethod),null,new ThrowingPlayerChoiceContext(),(HbrEffectPower)ModelDb.Power<HbrAttackUpPower>().ToMutable(),Hero,10m,Hero,null,1,null,false,10m)!;
        decimal equivalent=Actual(Enemy,Hero,6m,ValueProp.Move,baseline);
        X91Reset();power=await CalcStart();CalcLoadOrder(0);X92Action=async(_,play)=>await Command(Hero,6m,ValueProp.Move,play.Card);await X91Play(FormCard<X92Attack>());
        Equal(X91Hits.Last().Final,equivalent,"matching10percent damage uses the same original attack-increase area as an actual10percent attack buff");
    }
    public static async Task VritikaCases(Harmony h){await CalculationBasicCases();await CalculationUpgradesAndReplayCases();await CalculationScopeAndAmountCases();await CalculationSavedAndNativeResourceCases();}
}
