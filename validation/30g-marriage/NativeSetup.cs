using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

static partial class EffectChecks
{
    static AbstractRoom GRoom=null!;
    static bool GRoomLeaf(ref AbstractRoom __result){__result=GRoom;return false;}
    static bool GRunLeaf(ref RunState __result){__result=RunState;return false;}
    static bool GLocalLeaf(Player player,ref bool __result){__result=player==Player;return false;}
    static void GSaveType(Type type)
    {
        Assembly native=typeof(SavedProperties).Assembly;
        if(native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is {} legacy){legacy.GetMethod("InjectTypeIntoCache",F)!.Invoke(null,[type]);return;}
        Type cache=native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;
        cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,[type]);cache.GetField("_initialized",F)!.SetValue(null,true);
    }
    static void GInstall(Harmony harmony)
    {
        foreach(Type type in new[]{typeof(ThirtyGCollectionRelic),typeof(NarcissismIsFineHbrgoSaki),typeof(PaintTheWorldMyColorHbrgoSaki),typeof(AbsolutelyNoEffortHbrgoSaki),typeof(HbrAttackUpPower),typeof(HbrCriticalRateUpPower),typeof(HbrCriticalDamageUpPower),typeof(HbrDefenseUpPower),typeof(HbrAttackDownPower),typeof(HbrDefenseDownPower),typeof(FragilePower),typeof(Overclock)}.Concat(GMembers))
        {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));GSaveType(type);}
        foreach(string name in new[]{"ThirtyGCombatEntryPatch","ThirtyGGraduatedHookListenersPatch","ThirtyGGraduatedShouldPlayPatch","ThirtyGGraduatedWrapperPatch","ThirtyGGraduatedAutoPlayPatch","ThirtyGHeartfeltHandEndPatch","ThirtyGCardIdentityWritePatch","ThirtyGCardIdentityReadPatch","HbrEffectApplicationPatch","HbrEffectInstanceStackingPatch"})
            Check(harmony.CreateClassProcessor(Mod.GetTypes().Single(t=>t.Name==name)).Patch().Count>0,"72 installs production patch "+name);
        foreach(Type type in new[]{typeof(RunState),typeof(CombatState)})harmony.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GListeners),F)!){priority=Priority.First+200});
        GRoom=Blank<RestSiteRoom>();
        Set(RunManager.Instance,"<NetService>k__BackingField",Blank<NetSingleplayerGameService>());
        Set(RunManager.Instance,"<PlayerChoiceSynchronizer>k__BackingField",Blank<PlayerChoiceSynchronizer>());
        harmony.Patch(typeof(RunManager).GetMethod(nameof(RunManager.DebugOnlyGetState))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GRunLeaf),F)!));
        harmony.Patch(typeof(RunState).GetProperty(nameof(RunState.CurrentRoom))!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GRoomLeaf),F)!));
        harmony.Patch(typeof(LocalContext).GetProperty("NetId")!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoNetId),F)!));
        harmony.Patch(typeof(LocalContext).GetMethod(nameof(LocalContext.IsMe),new[]{typeof(Player)})!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GLocalLeaf),F)!));
        foreach(var method in typeof(PlayerChoiceSynchronizer).GetMethods(F).Where(m=>m.Name=="ReserveChoiceId"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GReserveLeaf),F)!));
        foreach(var method in typeof(PlayerChoiceSynchronizer).GetMethods(F).Where(m=>m.Name=="SyncLocalChoice"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GSyncLeaf),F)!));
        harmony.Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Add"&&m.GetParameters()[0].ParameterType==typeof(CardModel)&&m.GetParameters()[1].ParameterType==typeof(PileType)),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GMoveHand),F)!));
        harmony.Patch(Helper("HbrMemoryGardenUpgradeCatalog").GetMethod("RollOffers",F)!,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GObserveOffers),F)!));
        harmony.Patch(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand).GetMethod("Execute")!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GDischargeLeaf),F)!){priority=Priority.Last});
        ThirtyGProposalPresentationBridge.Requested+=GPresentation;HBRGOSakiMod.Core.Helpers.ThirtyGBondHelper.RunChanged+=(player,state)=>GRunNotifications.Add((player,state));HBRGOSakiMod.Core.Helpers.ThirtyGProposalState.Changed+=card=>GCardNotifications.Add(card);
    }
}
