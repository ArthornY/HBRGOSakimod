using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

static partial class EffectChecks
{
    static async Task YanagiNativeBlockCases()
    {
        YanagiReset();var defend=FormCard<DefendIronclad>();await YanagiPlay(defend);int baseline=NativeBlock;SetNativeBlock(0);
        Call("YanagiButlerCombatHelper","Prepare",defend);await YanagiPlay(defend);
        Equal(NativeBlock,decimal.Floor(baseline*1.2m),"actual native Defend OnPlay/GainBlock/ModifyBlock preserves exact prepared source");
        Check(baseline>0,"native baseline Defend really writes its resource");
        SetNativeBlock(0);await CreatureCmd.GainBlock(Hero,defend.DynamicVars.Block.BaseValue,defend.DynamicVars.Block.Props,null,true);
        Equal(NativeBlock,baseline,"unrelated GainBlock without CardPlay cannot borrow prepared Defend");
    }
    static void YanagiRecoveryDownCases()
    {
        YanagiReset();var shell=Power<HighDimensionalShellPower>(Enemy,1);decimal amount=shell.MaximumDp*.2m;
        var down=Power<HbrRecoveryDownPower>(Enemy,1);Native(down,"ConfigureEffect",50m,null);
        decimal before=shell.CurrentDp;Native(shell,"RestoreWithCeiling",amount,true,1.5m);
        Equal(shell.CurrentDp-before,amount*.5m,"new150 core applies RecoveryDown exactly once");
        before=shell.CurrentDp;shell.Restore(amount);Equal(shell.CurrentDp-before,amount*.5m,"old nonoverflow Restore applies RecoveryDown exactly once");
        before=shell.CurrentDp;Native(shell,"RestoreWithLimit",amount,true);Equal(shell.CurrentDp-before,amount*.5m,"old overflow wrapper delegates to new core with no double penalty");
    }
    static async Task YanagiShellCeilingCases()
    {
        YanagiReset();var card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>();var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});var shell=Power<PhantomHighDimensionalShellPower>(phantom,1);
        Native(shell,"SetDpAmount",shell.MaximumDp,false);decimal maximum=shell.MaximumDp;
        await(Task)Call("SeiraDpRecoveryHelper","RestoreWithCeiling",new ThrowingPlayerChoiceContext(),phantom,.6m,true,card,1.5m)!;
        Equal(shell.CurrentDp,decimal.Floor(maximum*1.5m),"actual main-phantom pool reaches150ceiling");
        await(Task)Call("SeiraDpRecoveryHelper","Restore",new ThrowingPlayerChoiceContext(),phantom,.1m,true,card)!;
        Equal(shell.CurrentDp,decimal.Floor(maximum*1.5m),"subsequent default120recovery never truncates main-phantom150pool");YanagiSaved(shell);
    }
    static void YanagiInstallResourceLeaves(Harmony h)
    {
        void Patch(MethodBase target,string name)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=Priority.First+100});
        foreach(MethodInfo sound in typeof(SfxCmd).GetMethods(F).Where(method=>method.Name=="Play"))Patch(sound,nameof(Skip));
        foreach(MethodInfo visual in typeof(VfxCmd).GetMethods(F).Where(method=>method.Name=="PlayOnCreatureCenter"))Patch(visual,nameof(Skip));
        foreach(MethodInfo animation in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="TriggerAnim"&&method.ReturnType==typeof(Task)))Patch(animation,nameof(VisualTask));
        foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method=>(method.Name is "Wait" or "CustomScaledWait")&&method.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
        Type history=typeof(CombatManager).GetProperty("History")!.PropertyType;
        foreach(MethodInfo method in history.GetMethods(F).Where(method=>method.Name=="BlockGained"))Patch(method,nameof(Skip));
    }
}
