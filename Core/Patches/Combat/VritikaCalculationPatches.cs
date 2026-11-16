using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel),nameof(CardModel.OnPlayWrapper))]
internal static class VritikaCalculationPlayCleanupPatch
{
    [HarmonyPriority(Priority.Last)]private static void Prefix(CardModel __instance,out object __state)=>__state=VritikaCalculationCombatHelper.CapturePlayToken(__instance);
    private static void Postfix(object __state,ref Task __result){if(__state!=null)__result=Finish(__result,__state);}
    private static async Task Finish(Task original,object token){try{await original;}finally{VritikaCalculationCombatHelper.ClosePlay(token);}}
    private static Exception Finalizer(Exception __exception,object __state){if(__exception!=null)VritikaCalculationCombatHelper.ClosePlay(__state);return __exception;}
}

[HarmonyPatch]
internal static class VritikaCalculationOrdinaryTurnEndPatch
{
    private static MethodInfo TargetMethod()=>PhantomTurnParticipants.ResolveHook(typeof(IEnumerable<Creature>),"AfterTurnEnd","AfterSideTurnEnd");
    private static void Postfix(CombatSide side,IEnumerable<Creature> participants,ref Task __result)
    {
        if(side!=CombatSide.Player)return;
        Player[] owners=participants.Select(creature=>creature.Player??creature.PetOwner).Where(player=>player!=null).Distinct().ToArray();
        if(owners.Length>0)__result=Finish(__result,owners);
    }
    private static async Task Finish(Task original,Player[] owners)
    {await original;foreach(Player owner in owners)if(OverclockImmediateTurnCoordinator.GetTurnToken(owner)==null)VritikaCalculationCombatHelper.OnTurnEnd(owner);}
}
