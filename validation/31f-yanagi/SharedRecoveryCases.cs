using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

static partial class EffectChecks
{
    static async Task YanagiSharedRecoveryCases()
    {
        YanagiReset();var binding=FormCard<BindingRedemptionHbrgoSaki>();var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);
        Set(Combat,"_allies",new List<Creature>{Hero,phantom});var shell=Power<HighDimensionalShellPower>(phantom,1);decimal capacity=Math.Min(100m,shell.MaximumDp/2m);
        Native(shell,"SetDpAmount",shell.MaximumDp-capacity,false);decimal start=shell.CurrentDp;
        decimal plain=(decimal)Call("HbrResourceValueHelper","Fixed",Hero,6m)!;
        Call("YanagiButlerCombatHelper","Prepare",binding);await YanagiPlay(binding,phantom);
        Equal(shell.CurrentDp-start,capacity,"actual Binding budget first fills missing DP");Equal(YanagiHeals.Last().Amount,plain*1.2m-capacity,"Binding allocates amplified total budget before DP then HP without double multiplication");
        YanagiHeals.Clear();await YanagiPlay(binding,phantom);Equal(YanagiHeals.Single().Amount,plain*1.2m,"full DP sends one entire amplified Binding budget to HP");
        YanagiReset();binding=FormCard<BindingRedemptionHbrgoSaki>();phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        await YanagiPlay(binding,phantom);Equal(YanagiHeals.Single().Amount,plain,"unprepared Binding preserves its old fixed HP budget");
        var ember=FormCard<EmberRecoveryHbrgoSaki>();Call("YanagiButlerCombatHelper","Prepare",ember);YanagiHeals.Clear();await YanagiPlay(ember);Equal(YanagiHeals.Single().Amount,plain*1.2m,"actual EmberRecovery callsite receives one prepared multiplier");

        YanagiReset();var source=FormCard<StrikeIronclad>();phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});shell=Power<HighDimensionalShellPower>(phantom,1);Call("YanagiButlerCombatHelper","Prepare",source);
        start=shell.CurrentDp;decimal flat=Math.Min(10m,shell.MaximumDp/10m);await(Task)Call("PhantomHelper","RestoreDp",new ThrowingPlayerChoiceContext(),phantom,flat,source)!;
        Equal(shell.CurrentDp-start,decimal.Floor(flat*1.2m),"actual shared phantom recovery scales source once");
        YanagiReset();var dp=NewDp(0);source=FormCard<StrikeIronclad>();Call("YanagiButlerCombatHelper","Prepare",source);await(Task)Call("WorldsFinestCombatHelper","RestoreDp",new ThrowingPlayerChoiceContext(),source,100m)!;Equal(dp.Dp,120m,"Tama nonphantom shared path scales one fixed budget");
        YanagiReset();dp=NewDp();source=FormCard<StrikeIronclad>();Call("YanagiButlerCombatHelper","Prepare",source);Native(dp,"set_Dp",dp.MaxDp);SetNativeBlock(0);
        await(Task)Call("HbrMatchaDpHelper","Restore",new ThrowingPlayerChoiceContext(),Player,.1m,true,source)!;
        Equal(NativeBlock,decimal.Floor(dp.MaxDp*.12m)*2m,"Matcha first scales recovery budget once then converts only its overflow to unpowered block");
        Equal((decimal)Call("YanagiButlerCombatHelper","ScaleRecovery",ModelDb.Card<AsTheYoungLadyCommandsHbrgoSaki>(),100m)!,100m,"canonical recovery query never accesses an owner");
    }
}
