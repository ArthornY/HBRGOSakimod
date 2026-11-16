using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Keywords;

static partial class EffectChecks
{
    static readonly List<object> DiagnosticRows=[];
    static readonly List<object> DiagnosticPatches=[];
    static bool SourceTipSetting(ref bool __result){__result=false;return false;}
    static bool RelicSourceEnabled=true;
    static bool RelicSourceSetting(ref bool __result){__result=RelicSourceEnabled;return false;}
    static bool ActualModName(Type type,ref string? __result)
    {
        if(type.Assembly!=Mod)return true;
        __result="HBRGOSakiMod";return false;
    }
    static bool AttachedCapabilities;
    static void AttachDiagnosticCapabilities(CardModel card)
    {
        ModelCapabilitySet set=card.Capabilities();
        var list=(IList<IModelCapability>)typeof(ModelCapabilitySet).GetField("_capabilities",F)!.GetValue(set)!;
        if(list.Any())return;
        foreach(CardCapability capability in new CardCapability[]{new RollGlossaryCapability(),new ThirtyOneAKeywordCapability(),new ThirtyOneDKeywordCapability()})
        {
            Set(capability,"<IsMutable>k__BackingField",true);capability.Attach(card,true);list.Add(capability);
        }
    }
    static void Capture(string group,string label,Func<object> get)
    {
        try { var result=get(); DiagnosticRows.Add(new{group,label,ok=true,result,error=(string?)null});Console.WriteLine("PASS "+group+"/"+label); }
        catch(Exception e){ DiagnosticRows.Add(new{group,label,ok=false,result=(object?)null,error=e.ToString()});Console.WriteLine("FAIL "+group+"/"+label+": "+e); }
    }
    static void Probe(string group)
    {
        foreach(Type t in new[]{typeof(MakeAChangeKillYourselfHbrgoSaki),typeof(PeacefulMemoriesHbrgoSaki)})
        {
            var canonical=ModelDb.GetById<CardModel>(ModelDb.GetId(t));
            var owned=(CardModel)canonical.MutableClone();Set(owned,"_owner",Player);
            var ownerless=(CardModel)canonical.MutableClone();
            foreach(var pair in new[]{("canonical",canonical),("ownerlessMutable",ownerless),("owned",owned)})
            {
                if(AttachedCapabilities)AttachDiagnosticCapabilities(pair.Item2);
                Capture(group,t.Name+"/"+pair.Item1+"/ExtraHoverTips",()=>((IEnumerable<IHoverTip>)Invoke(AccessTools.PropertyGetter(t,"ExtraHoverTips"),pair.Item2)!).OfType<HoverTip>().Select(tip=>new{id=tip.Id,title=tip.Title,description=tip.Description}).ToArray());
                Capture(group,t.Name+"/"+pair.Item1+"/HoverTips",()=>pair.Item2.HoverTips.OfType<HoverTip>().Select(tip=>new{id=tip.Id,title=tip.Title,description=tip.Description}).ToArray());
            }
        }
    }
    static void PatchType(Harmony h,Assembly assembly,string name)
    {
        var t=assembly.GetType(name,true)!;
        int count=h.CreateClassProcessor(t).Patch().Count;
        DiagnosticPatches.Add(new{type=name,count});
    }
    static void RitsuPostfix(Harmony h,Assembly ritsu,string name)
    {
        var type=ritsu.GetType(name,true)!;
        var postfix=type.GetMethod("Postfix",F)!;
        h.Patch(AccessTools.PropertyGetter(typeof(CardModel),"HoverTips"),postfix:new HarmonyMethod(postfix));
        DiagnosticPatches.Add(new{type=name,count=1});
    }
    public static void RunDiagnostic(string report)
    {
        string? fatal=null;
        AppDomain.CurrentDomain.FirstChanceException+=(s,e)=>{
            if(e.Exception is ArgumentOutOfRangeException)File.AppendAllText(report+".first-chance.log",e.Exception+Environment.NewLine+Environment.StackTrace+Environment.NewLine);
        };
        try
        {
            Install();Localization();
            foreach(Type type in Mod.GetTypes().Concat(typeof(PowerModel).Assembly.GetTypes()).Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters&&
                (typeof(CardModel).IsAssignableFrom(t)||typeof(PowerModel).IsAssignableFrom(t)||typeof(RelicModel).IsAssignableFrom(t)||typeof(CardPoolModel).IsAssignableFrom(t))))
            {
                if(!ModelDb.Contains(type))ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
            }
            var leaves=new Harmony("hbr.inspect.diagnostic.leaves");
            foreach(Type type in new[]{typeof(PowerModel),typeof(RelicModel)})leaves.Patch(AccessTools.PropertyGetter(type,"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod("FormPowerIconLeaf",F)!));
            leaves.Patch(AccessTools.Method(typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Assets.AssetCache",true)!,"GetTexture2D"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod("FormPowerIconLeaf",F)!));
            Probe("baseline-native");
            var ours=new Harmony("hbr.inspect.diagnostic.ours");
            foreach(string name in new[]{"HBRGOSakiMod.Core.Patches.HbrCardFaceRulesHoverPatch","HBRGOSakiMod.Core.Patches.MoonlightReplayDisplayPatch",
                "HBRGOSakiMod.Core.Patches.Combat.HbrPercentDebuffReferenceHoverTipsPatch","HBRGOSakiMod.Core.Patches.Combat.HbrVigorCardHoverDescriptionPatch"})PatchType(ours,Mod,name);
            Probe("production-getter-patches");
            Assembly baselib=Assembly.Load("BaseLib");
            var b=new Harmony("hbr.inspect.diagnostic.baselib");
            var extra=baselib.GetType("BaseLib.Patches.Localization.ExtraTooltips",true)!;
            b.Patch(AccessTools.PropertyGetter(typeof(CardModel),"HoverTips"),transpiler:new HarmonyMethod(extra.GetMethod("AddCustomCardTips",F)!));
            DiagnosticPatches.Add(new{type=extra.FullName+".AddCustomCardTips",count=1});
            Probe("plus-baselib-extra");
            PatchType(b,baselib,"BaseLib.Patches.UI.ModSourceTooltip+CardTips");
            Probe("plus-baselib-source");
            Assembly ritsu=Assembly.Load("STS2-RitsuLib.Runtime");
            var r=new Harmony("hbr.inspect.diagnostic.ritsu");
            r.Patch(ritsu.GetType("STS2RitsuLib.Data.RitsuLibSettingsStore",true)!.GetMethod("IsModSourceHoverTipsEnabled",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(SourceTipSetting),F)!));
            DiagnosticPatches.Add(new{type="Optional content-source setting persistence leaf",value=false});
            foreach(string type in new[]{"STS2RitsuLib.Keywords.Patches.CardModelHoverTipsModKeywordPatch","STS2RitsuLib.Models.Capabilities.Patches.CardModelCapabilityPatches+HoverTipsPatch","STS2RitsuLib.Content.Patches.ContentSourceCardHoverTipsPatch"})
            {RitsuPostfix(r,ritsu,type);Probe("plus-"+type.Split('+').Last().Split('.').Last());}
            foreach(string keyword in new[]{"ROLL","CRITICAL_SUCCESS","ABSOLUTE_CRITICAL_SUCCESS","31A","31D"})
                ModKeywordRegistry.For("HBRGOSakiMod").RegisterCardKeywordOwnedByLocNamespace(keyword,null,ModKeywordCardDescriptionPlacement.None,false);
            AttachedCapabilities=true;
            Probe("registered-keywords-and-real-capabilities");
            var hidden=new Harmony("hbr.inspect.diagnostic.hidden-relic");
            hidden.Patch(AccessTools.PropertyGetter(baselib.GetType("BaseLib.Config.BaseLibConfig",true)!,"ShowRelicModSource"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RelicSourceSetting),F)!));
            hidden.Patch(baselib.GetType("BaseLib.Utils.WhatMod",true)!.GetMethods(F).Single(m=>m.Name=="FindModName"&&!m.IsGenericMethod&&m.GetParameters().Length==1),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ActualModName),F)!));
            hidden.Patch(AccessTools.PropertyGetter(typeof(RelicModel),"HoverTips"),prefix:new HarmonyMethod(ritsu.GetType("STS2RitsuLib.Relics.Visibility.Patches.RelicModelHoverTipsVisibilityPatch",true)!.GetMethod("Prefix",F)!));
            Probe("ritsu-hidden-relic-prefix-only");
            PatchType(hidden,baselib,"BaseLib.Patches.UI.ModSourceTooltip+RelicTips");
            Probe("negative-control-hidden-relic-plus-baselib-source");
        }
        catch(Exception e){fatal=e.ToString();Console.Error.WriteLine(e);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{ok=fatal==null,rows=DiagnosticRows,patches=DiagnosticPatches,fatal,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real native getter and ExtraHoverTips, production getter patches, real BaseLib and Ritsu getter patches; fixture native model/logging/texture leaves only. No Godot UI or installed state changes."},new JsonSerializerOptions{WriteIndented=true}));
        if(fatal!=null)throw new Exception("Diagnostic fatal; see "+report);
    }
}
