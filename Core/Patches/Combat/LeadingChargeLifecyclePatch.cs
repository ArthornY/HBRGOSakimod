using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
namespace HBRGOSakiMod.Core.Patches.Combat;
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeCardPlayed))]
internal static class LeadingChargeBeforeCardPatch
{private static void Prefix(CardPlay cardPlay)=>LeadingChargeCombatHelper.BeforeCardPlayed(cardPlay);}
[HarmonyPatch(typeof(AttackCommand),nameof(AttackCommand.Execute))]
internal static class LeadingChargeAttackPatch
{
    private static void Prefix(AttackCommand __instance,out LeadingChargeCombatHelper.AttackScope __state)=>__state=LeadingChargeCombatHelper.BeginAttack(__instance);
    private static void Postfix(LeadingChargeCombatHelper.AttackScope __state,ref Task<AttackCommand> __result)
    {if(__state==null)return;try{__result=LeadingChargeCombatHelper.Finish(__result,__state);}finally{LeadingChargeCombatHelper.RestoreCaller(__state);}}
    private static Exception Finalizer(Exception __exception,LeadingChargeCombatHelper.AttackScope __state)
    {if(__exception!=null)LeadingChargeCombatHelper.Close(__state);else LeadingChargeCombatHelper.RestoreCaller(__state);return __exception;}
}
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeFlush))]
internal static class KurosawaBeforeFlushPatch
{
    private static void Postfix(Player player,ref Task __result)=>__result=Finish(__result,player);
    private static async Task Finish(Task native,Player player){await native;KurosawaLeaderCombatHelper.BeforeFlush(player);}
}
[HarmonyPatch(typeof(Hook),nameof(Hook.AfterFlush))]
internal static class KurosawaAfterFlushPatch
{
    private static void Postfix(Player player,PlayerChoiceContext playerChoiceContext,ref Task __result)=>__result=Finish(__result,playerChoiceContext,player);
    private static async Task Finish(Task native,PlayerChoiceContext context,Player player){await native;await KurosawaLeaderCombatHelper.AfterFlush(context,player);}
}
