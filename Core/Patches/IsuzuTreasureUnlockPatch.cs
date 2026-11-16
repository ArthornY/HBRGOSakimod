using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer),nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
internal static class IsuzuTreasureUnlockBeginPatch
{private static void Prefix(TreasureRoomRelicSynchronizer __instance)=>IsuzuTreasureUnlockHelper.Begin(__instance);}

[HarmonyPatch]
internal static class IsuzuTreasureUnlockAwardPatch
{
    internal static MethodInfo AwardBody()
    {
        Type type=typeof(RelicCmd).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicCollection",true);
        MethodInfo method=AccessTools.Method(type,"AnimateRelicAwards");
        Type state=method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType??throw new MissingMethodException(type.FullName,"AnimateRelicAwards async body");
        return AccessTools.Method(state,"MoveNext");
    }
    private static MethodBase TargetMethod()=>AwardBody();
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
    {
        MethodInfo obtain=typeof(RelicCmd).GetMethod(nameof(RelicCmd.Obtain),new[]{typeof(RelicModel),typeof(MegaCrit.Sts2.Core.Entities.Players.Player),typeof(int)});
        MethodInfo animate=AccessTools.Method(typeof(NRelicInventory),nameof(NRelicInventory.AnimateRelic));
        MethodInfo fallback=AccessTools.Method(typeof(RelicGrabBag),nameof(RelicGrabBag.MoveToFallback));
        int obtains=0,animations=0,fallbacks=0;
        foreach(CodeInstruction code in input)
        {
            if(code.Calls(obtain))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(IsuzuTreasureUnlockHelper),nameof(IsuzuTreasureUnlockHelper.Obtain));obtains++;}
            else if(code.Calls(animate))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(IsuzuTreasureUnlockHelper),nameof(IsuzuTreasureUnlockHelper.Animate));animations++;}
            else if(code.Calls(fallback))
            {code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(IsuzuTreasureUnlockHelper),nameof(IsuzuTreasureUnlockHelper.MoveToFallback));fallbacks++;}
            yield return code;
        }
        if(obtains!=1||animations!=1||fallbacks!=1)throw new InvalidOperationException("Isuzu treasure award callsite changed: obtain="+obtains+",animate="+animations+",fallback="+fallbacks);
    }
}

[HarmonyPatch]
internal static class IsuzuTreasurePendingFilterPatch
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(RelicGrabBag).GetMethods(BindingFlags.Public|BindingFlags.Instance)
        .Where(method=>(method.Name is "PullFromFront" or "PullFromBack")&&method.GetParameters() is var p&&p.Length==3&&
            p[0].ParameterType==typeof(RelicRarity)&&p[1].ParameterType==typeof(Func<RelicModel,bool>));
    private static void Prefix(ref Func<RelicModel,bool> __1,IRunState __2)
    {
        if(__2 is not RunState run||!IsuzuTreasureUnlockHelper.HasPending(run))return;
        Func<RelicModel,bool> old=__1;
        __1=relic=>(old?.Invoke(relic)??true)&&!IsuzuTreasureUnlockHelper.IsPending(run,relic.Id.Entry);
    }
}
