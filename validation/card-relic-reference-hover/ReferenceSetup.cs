using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

static partial class EffectChecks
{
    static void InitializeReferenceHost()
    {
        Install();Localization();
        foreach(Type type in Mod.GetTypes().Concat(typeof(PowerModel).Assembly.GetTypes()).Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters&&
            (typeof(CardModel).IsAssignableFrom(t)||typeof(PowerModel).IsAssignableFrom(t)||typeof(RelicModel).IsAssignableFrom(t)||typeof(CardPoolModel).IsAssignableFrom(t))))
        {
            if(!ModelDb.Contains(type))ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
        }
        var h=new Harmony("hbr.inspect.reference.actual-patches");
        foreach(Type type in new[]{typeof(PowerModel),typeof(RelicModel)})h.Patch(AccessTools.PropertyGetter(type,"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod("FormPowerIconLeaf",F)!));
        h.Patch(AccessTools.Method(typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Assets.AssetCache",true)!,"GetTexture2D"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod("FormPowerIconLeaf",F)!));
        foreach(MethodInfo method in typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!.GetMethods(F).Where(m=>m.Name=="GetPath"&&m.ReturnType==typeof(string)))
            h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod("Energy",F)!));
        foreach(string name in new[]{"HBRGOSakiMod.Core.Patches.HbrCardFaceRulesHoverPatch","HBRGOSakiMod.Core.Patches.MoonlightReplayDisplayPatch",
            "HBRGOSakiMod.Core.Patches.Combat.HbrPercentDebuffReferenceHoverTipsPatch","HBRGOSakiMod.Core.Patches.Combat.HbrVigorCardHoverDescriptionPatch"})PatchType(h,Mod,name);
        Assembly baselib=Assembly.Load("BaseLib"),ritsu=Assembly.Load("STS2-RitsuLib.Runtime");
        h.Patch(AccessTools.PropertyGetter(typeof(CardModel),"HoverTips"),transpiler:new HarmonyMethod(baselib.GetType("BaseLib.Patches.Localization.ExtraTooltips",true)!.GetMethod("AddCustomCardTips",F)!));
        PatchType(h,baselib,"BaseLib.Patches.UI.ModSourceTooltip+CardTips");
        h.Patch(ritsu.GetType("STS2RitsuLib.Data.RitsuLibSettingsStore",true)!.GetMethod("IsModSourceHoverTipsEnabled",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(SourceTipSetting),F)!));
        foreach(string type in new[]{"STS2RitsuLib.Keywords.Patches.CardModelHoverTipsModKeywordPatch","STS2RitsuLib.Models.Capabilities.Patches.CardModelCapabilityPatches+HoverTipsPatch","STS2RitsuLib.Content.Patches.ContentSourceCardHoverTipsPatch"})RitsuPostfix(h,ritsu,type);
        h.Patch(AccessTools.PropertyGetter(baselib.GetType("BaseLib.Config.BaseLibConfig",true)!,"ShowRelicModSource"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RelicSourceSetting),F)!));
        h.Patch(baselib.GetType("BaseLib.Utils.WhatMod",true)!.GetMethods(F).Single(m=>m.Name=="FindModName"&&!m.IsGenericMethod&&m.GetParameters().Length==1),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ActualModName),F)!));
        h.Patch(AccessTools.PropertyGetter(typeof(RelicModel),"HoverTips"),prefix:new HarmonyMethod(ritsu.GetType("STS2RitsuLib.Relics.Visibility.Patches.RelicModelHoverTipsVisibilityPatch",true)!.GetMethod("Prefix",F)!));
        PatchType(h,baselib,"BaseLib.Patches.UI.ModSourceTooltip+RelicTips");
    }
}
