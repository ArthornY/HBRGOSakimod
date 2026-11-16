using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using Environment=System.Environment;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

static class NararaHpDisplayStandalone
{
    public static void Run(string report)=>EffectChecks.RunNararaHpDisplay(report);
}

static partial class EffectChecks
{
    static readonly List<object> NararaHpDisplayRows=[];
    static readonly List<object> NararaHpLocaleFiles=[];
    static string NararaHpLabel="";
    static string NararaHpLanguage="";
    static Type HpBarsType=>Mod.GetType("HBRGOSakiMod.Core.Models.Powers.NararaLayeredHpPower",true)!;
    static PowerModel NararaDisplayPower=null!;
    sealed class HpPreviewNode { internal bool Visible;internal float Left;internal float Right; }
    static readonly Dictionary<CanvasItem,HpPreviewNode> HpPreviewNodes=new(ReferenceEqualityComparer.Instance);
    static HpPreviewNode HpNode(CanvasItem node)
    {if(!HpPreviewNodes.TryGetValue(node,out var value))HpPreviewNodes[node]=value=new();return value;}
    static bool HpVisibleGet(CanvasItem __instance,ref bool __result){__result=HpNode(__instance).Visible;return false;}
    static bool HpVisibleSet(CanvasItem __instance,bool value){HpNode(__instance).Visible=value;return false;}
    static bool HpOffsetGet(Control __instance,MethodBase __originalMethod,ref float __result)
    {__result=__originalMethod.Name=="get_OffsetLeft"?HpNode(__instance).Left:HpNode(__instance).Right;return false;}
    static bool HpOffsetSet(Control __instance,MethodBase __originalMethod,float value)
    {if(__originalMethod.Name=="set_OffsetLeft")HpNode(__instance).Left=value;else HpNode(__instance).Right=value;return false;}
    static bool HpMargin(ref int __result){__result=8;return false;}
    static bool HpMaximumWidth(ref float __result){__result=120f;return false;}
    static bool HpReadVisible(CanvasItem node)=>HpNode(node).Visible;
    static void HpWriteVisible(CanvasItem node,bool value)=>HpNode(node).Visible=value;
    static float HpReadLeft(Control node)=>HpNode(node).Left;
    static float HpReadRight(Control node)=>HpNode(node).Right;
    static void HpWriteLeft(Control node,float value)=>HpNode(node).Left=value;
    static void HpWriteRight(Control node,float value)=>HpNode(node).Right=value;
    static int HpReadMargin(NinePatchRect node)=>8;
    static Texture2D? HpReadPowerIcon(PowerModel power)=>null;
    static IEnumerable<CodeInstruction> NararaRenderLeaves(IEnumerable<CodeInstruction> instructions)
    {
        var replacements=new Dictionary<MethodInfo,MethodInfo>();
        foreach(var pair in new[]{(typeof(CanvasItem),"Visible",nameof(HpReadVisible),nameof(HpWriteVisible)),
            (typeof(Control),"OffsetLeft",nameof(HpReadLeft),nameof(HpWriteLeft)),
            (typeof(Control),"OffsetRight",nameof(HpReadRight),nameof(HpWriteRight))})
        {
            replacements[AccessTools.PropertyGetter(pair.Item1,pair.Item2)]=typeof(EffectChecks).GetMethod(pair.Item3,F)!;
            replacements[AccessTools.PropertySetter(pair.Item1,pair.Item2)]=typeof(EffectChecks).GetMethod(pair.Item4,F)!;
        }
        foreach(string margin in new[]{"PatchMarginLeft","PatchMarginRight"})
            replacements[AccessTools.PropertyGetter(typeof(NinePatchRect),margin)]=typeof(EffectChecks).GetMethod(nameof(HpReadMargin),F)!;
        replacements[AccessTools.PropertyGetter(typeof(PowerModel),nameof(PowerModel.Icon))]=typeof(EffectChecks).GetMethod(nameof(HpReadPowerIcon),F)!;
        foreach(var instruction in instructions)
        {
            if(instruction.operand is MethodInfo method&&replacements.TryGetValue(method,out var replacement))
            {instruction.opcode=OpCodes.Call;instruction.operand=replacement;}
            yield return instruction;
        }
    }
    static string HpPlain(string text)=>Regex.Replace(text,@"\[[^\]]+\]","");
    static void NararaDisplayInject(Type type)
    {
        if(!ModelDb.Contains(type))ModelDb.Inject(type);
        Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
    }
    static bool CaptureNararaHpLabel(string text){NararaHpLabel=text;return false;}
    static void InstallNararaHpDisplay()
    {
        var harmony=new Harmony("hbr.narara.hp-display.production");
        Type fill=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.LargeHealthBarFillPatch",true)!;
        Check(harmony.CreateClassProcessor(fill).Patch().Count==1,"production HP fill prefix patches one actual native two-argument width method");
        foreach(string name in new[]{"LargeHealthBarPoisonPatch","NararaHealthBarDoomLethalPatch","NararaHealthBarDoomWidthPatch"})
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count==1,"installs actual native preview patch "+name);
        foreach(string name in new[]{"UndispellablePowerFooterPatch","UndispellablePowerExplanationPatch"})
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches."+name,true)!).Patch().Count==1,"installs production "+name);
        harmony.Patch(AccessTools.PropertyGetter(typeof(PowerModel),nameof(PowerModel.Icon)),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        harmony.Patch(typeof(MegaLabel).GetMethod(nameof(MegaLabel.SetTextAutoSize),F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(CaptureNararaHpLabel),F)!));
        var leaves=new Harmony("hbr.narara.hp-display.render-leaves");
        foreach(var pair in new[]{(typeof(CanvasItem),"Visible",nameof(HpVisibleGet),nameof(HpVisibleSet)),
            (typeof(Control),"OffsetLeft",nameof(HpOffsetGet),nameof(HpOffsetSet)),(typeof(Control),"OffsetRight",nameof(HpOffsetGet),nameof(HpOffsetSet))})
        {
            leaves.Patch(AccessTools.PropertyGetter(pair.Item1,pair.Item2),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(pair.Item3,F)!));
            leaves.Patch(AccessTools.PropertySetter(pair.Item1,pair.Item2),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(pair.Item4,F)!));
        }
        foreach(string margin in new[]{"PatchMarginLeft","PatchMarginRight"})
            leaves.Patch(AccessTools.PropertyGetter(typeof(NinePatchRect),margin),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HpMargin),F)!));
        leaves.Patch(AccessTools.PropertyGetter(typeof(NHealthBar),"MaxFgWidth"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HpMaximumWidth),F)!));
        foreach(string name in new[]{"RefreshForeground","RefreshDoom"})
            if(AccessTools.Method(typeof(NHealthBar),name) is { } method)
                leaves.Patch(method,transpiler:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaRenderLeaves),F)!));
        leaves.Patch(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.NararaHealthBarDoomWidthPatch",true)!.GetMethod("Postfix",F)!,
            transpiler:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaRenderLeaves),F)!));
        leaves.Patch(typeof(HoverTip).GetConstructor([typeof(PowerModel),typeof(string),typeof(bool)])!,
            transpiler:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaRenderLeaves),F)!));
        MethodInfo target=(MethodInfo)Invoke(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.NararaHealthBarDoomWidthPatch",true)!.GetMethod("TargetMethod",F)!,null)!;
        int replacements=PatchProcessor.GetCurrentInstructions(target).Count(instruction=>instruction.operand is MethodInfo method&&method.DeclaringType?.Name=="NararaHealthBarDoomPreview"&&method.Name=="GetWidth");
        Check(replacements==1,"real active native Doom refresh contains exactly one explicit absolute-threshold width adapter");
        NararaHpDisplayRows.Add(new{source="native Doom preview target",method=target.Name,replacements});
    }
    static void NararaDisplayLocalization(string language)
    {
        NararaHpLanguage=language;
        string root=Environment.GetEnvironmentVariable("HBR_NARARA_HP_LOCALE_ROOT")??throw new Exception("Staged locale root is required");
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField",language);
        Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>(StringComparer.Ordinal);
        foreach(string table in new[]{"cards","relics","powers","events","potions","static_hover_tips","card_keywords","afflictions","characters","monsters","gameplay_ui"})
        {
            var merged=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string path in new[]{Path.Combine("D:/stsmod/sts/localization",language,table+".json"),
                Path.Combine("D:/stsmod/HBRGOSakiMod/HBRGOSakiMod/localization",language,table+".json"),
                Path.Combine(root,language,table+".json")}.Distinct(StringComparer.Ordinal))
            {
                if(!File.Exists(path))continue;
                foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))!)merged[pair.Key]=pair.Value;
                if(path.StartsWith(root,StringComparison.OrdinalIgnoreCase))
                    NararaHpLocaleFiles.Add(new{language,table,path,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))});
            }
            tables[table]=new LocTable(table,merged,null!);
        }
        Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        NararaDisplayInject(HpBarsType);NararaDisplayInject(typeof(LargeHealthPower));NararaDisplayInject(typeof(NararaLayeredDpPower));
    }
    static void NararaDisplayReset(decimal total,decimal current,int layers,bool hbr=true,bool phantom=false,bool ordinary=false)
    {
        Reset(hbr);
        if(!ordinary)
        {
            var boss=Blank<NararaShibaBossMonster>();Set(boss,"<IsMutable>k__BackingField",true);Set(boss,"_creature",Enemy);
            Set(boss,"<Id>k__BackingField",ModelDb.GetId(typeof(NararaShibaBossMonster)));Set(Enemy,"<Monster>k__BackingField",boss);
        }
        else
        {
            var monster=Blank<SmallHopper>();Set(monster,"<IsMutable>k__BackingField",true);Set(monster,"_creature",Enemy);
            Set(Enemy,"<Monster>k__BackingField",monster);
        }
        Set(Enemy,"_petOwner",phantom?Player:null);Set(Enemy,"<Side>k__BackingField",phantom?CombatSide.Player:CombatSide.Enemy);
        const int proxyCap=999999999;
        int proxyMaximum=(int)Math.Min(total,proxyCap);
        int proxyCurrent=total<=proxyCap?(int)current:current<=0m?0:Math.Clamp((int)decimal.Ceiling(current/total*proxyMaximum),1,proxyMaximum);
        Set(Enemy,"_maxHp",proxyMaximum);Set(Enemy,"_currentHp",proxyCurrent);
        if(total>proxyCap)
        {
            var health=Power<LargeHealthPower>(Enemy,1);
            Native(health,"Store",current,total);
        }
        NararaDisplayPower=(PowerModel)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(HpBarsType),null,Enemy,layers)!;
    }
    static string NararaDisplayReadLabel()
    {
        NararaHpLabel="";
        var label=Blank<MegaLabel>();
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.LargeHealthBarTextPatch",true)!;
        Invoke(patch.GetMethod("Postfix",F)!,null,Enemy,label);
        return NararaHpLabel;
    }
    static float NararaDisplayReadWidth(int amount,float full=120f)
    {
        var bar=Blank<NHealthBar>();Set(bar,"_creature",Enemy);
        return (float)Invoke(typeof(NHealthBar).GetMethod("GetFgWidth",F,null,[typeof(int),typeof(float)],null)!,bar,amount,full)!;
    }
    static void NararaDisplayGaugeCase(decimal total,decimal current,int layers,decimal active,decimal maximum)
    {
        NararaDisplayReset(total,current,layers);
        decimal originalCurrent=(decimal)Call("LargeCreatureHealthHelper","GetCurrentHp",Enemy)!;
        decimal originalMaximum=(decimal)Call("LargeCreatureHealthHelper","GetMaxHp",Enemy)!;
        int proxyCurrent=Enemy.CurrentHp,proxyMaximum=Enemy.MaxHp;
        Equal((decimal)Call("NararaLayeredHpHelper","GetActiveCurrentHp",Enemy)!,active,"display reads authoritative active integer HP");
        Equal((decimal)Call("NararaLayeredHpHelper","GetActiveMaximumHp",Enemy)!,maximum,"display reads exact active layer capacity");
        Equal((int)Call("HbrMultiHealthBar","GetRemainingBars",Enemy)!,layers,"reserve count matches authoritative four-to-one HP amount");
        float width=NararaDisplayReadWidth(proxyCurrent);
        float expected=Math.Max((float)(active/maximum)*120f,active>0m?12f:0f);
        Check(Math.Abs(width-expected)<.002f,"real native GetFgWidth with production prefix displays active quarter ratio");
        string text=NararaDisplayReadLabel();
        string expectedText=active.ToString("0",CultureInfo.InvariantCulture)+"/"+maximum.ToString("0",CultureInfo.InvariantCulture)+" ×"+layers;
        Check(text==expectedText,"production native HP label is current quarter/capacity and remaining-bar count");
        Equal((decimal)Call("LargeCreatureHealthHelper","GetCurrentHp",Enemy)!,originalCurrent,"display does not mutate total exact current HP");
        Equal((decimal)Call("LargeCreatureHealthHelper","GetMaxHp",Enemy)!,originalMaximum,"display does not multiply or mutate total exact maximum HP");
        Check(Enemy.CurrentHp==proxyCurrent&&Enemy.MaxHp==proxyMaximum&&NararaDisplayPower.Amount==layers,"display leaves native proxies and saved remaining count unchanged");
        NararaHpDisplayRows.Add(new{source="active quarter gauge",total,current,layers,active,maximum,width,text,proxyCurrent,proxyMaximum});
    }
    static void NararaGaugeCases()
    {
        foreach(var row in new[]{(1000m,1000m,4,250m,250m),(1000m,900m,4,150m,250m),(1000m,750m,3,250m,250m),
            (1000m,500m,2,250m,250m),(1000m,250m,1,250m,250m),(1000m,120m,1,120m,250m),
            (101m,101m,4,26m,26m),(101m,88m,4,13m,26m),(101m,75m,3,25m,25m),(101m,50m,2,25m,25m),(101m,25m,1,25m,25m),
            (1000000000000m,875000000000m,4,125000000000m,250000000000m),
            (18446744073709551616m,11529215046068469760m,3,2305843009213693952m,4611686018427387904m)})
            NararaDisplayGaugeCase(row.Item1,row.Item2,row.Item3,row.Item4,row.Item5);
        NararaDisplayReset(1000m,900m,4);
        Check(Math.Abs(NararaDisplayReadWidth(Enemy.CurrentHp-25)-60f)<.002f,"native absolute poison-preview delta is applied to active quarter");
        Vector2 reserve=(Vector2)Call("HbrMultiHealthBar","GetReserveOffset",3)!;
        Check(reserve.IsEqualApprox(new Vector2(22f,30.5f)),"four HP bars reuse the existing original-art third reserve offset");
        NararaDisplayReset(1000m,900m,4,false);
        Equal((int)Call("HbrMultiHealthBar","GetRemainingBars",Enemy)!,4m,"legacy enemy also displays intrinsic four HP bars");
        NararaDisplayReset(1000m,900m,4,true,true);
        Equal((int)Call("HbrMultiHealthBar","GetRemainingBars",Enemy)!,1m,"allied Narara phantom keeps one ordinary HP gauge");
        Check(Math.Abs(NararaDisplayReadWidth(Enemy.CurrentHp)-108f)<.002f&&NararaDisplayReadLabel()=="900/1000","phantom native gauge retains total-HP text and ratio");
        NararaDisplayReset(1000m,900m,4,true,false,true);
        Equal((int)Call("HbrMultiHealthBar","GetRemainingBars",Enemy)!,1m,"unrelated monster has no new reserve gauges");
        Check(Math.Abs(NararaDisplayReadWidth(Enemy.CurrentHp)-108f)<.002f,"unrelated native monster keeps its prior fill ratio");
    }
    static void NararaNativePreviewCase(decimal total,decimal current,int layers,int doom,int poison,bool poisonLethal,bool doomLethal,bool doomVisible,float? doomRight=null)
    {
        NararaDisplayReset(total,current,layers,false);
        if(doom>0)Power<DoomPower>(Enemy,doom);
        if(poison>0)Power<PoisonPower>(Enemy,poison);
        var bar=Blank<NHealthBar>();Set(bar,"_creature",Enemy);
        var hp=Blank<NinePatchRect>();var poisonBar=Blank<NinePatchRect>();var doomBar=Blank<NinePatchRect>();
        Set(bar,"_hpForeground",hp);Set(bar,"_poisonForeground",poisonBar);Set(bar,"_doomForeground",doomBar);
        HpPreviewNodes.Clear();
        bool actualPoison=(bool)Invoke(typeof(NHealthBar).GetMethod("IsPoisonLethal",F)!,bar,poison)!;
        bool actualDoom=(bool)Invoke(typeof(NHealthBar).GetMethod("IsDoomLethal",F)!,bar,doom,poison)!;
        Check(actualPoison==poisonLethal,"native poison lethal predicate means total death rather than a depleted HP bar");
        Check(actualDoom==doomLethal,"native Doom lethal predicate retains exact total-HP/explicit-kill semantics");
        Invoke(typeof(NHealthBar).GetMethod("RefreshForeground",F)!,bar);
        Check(HpNode(doomBar).Visible==doomVisible,"real native refresh shows Doom only within the active-bar threshold");
        if(doomRight.HasValue)Check(Math.Abs(HpNode(doomBar).Right-doomRight.Value)<.002f,"native Doom offset uses exact absolute threshold projected into active quarter");
        Check(LargeCreatureHp(Enemy)==current,"native preview does not write the exact gameplay HP ledger");
        NararaHpDisplayRows.Add(new{source="actual native foreground refresh",total,current,layers,doom,poison,actualPoison,actualDoom,
            doomVisible=HpNode(doomBar).Visible,doomRight=HpNode(doomBar).Right,hpRight=HpNode(hp).Right,poisonRight=HpNode(poisonBar).Right});
    }
    static decimal LargeCreatureHp(Creature creature)=>(decimal)Call("LargeCreatureHealthHelper","GetCurrentHp",creature)!;
    static void NararaNativePreviewCases()
    {
        NararaNativePreviewCase(1000m,900m,4,700,0,false,false,false);
        NararaNativePreviewCase(1000m,900m,4,850,0,false,false,true,-64f);
        NararaNativePreviewCase(1000m,900m,4,900,0,false,true,true,-48f);
        NararaNativePreviewCase(1000m,900m,4,1,2000,false,false,false);
        NararaNativePreviewCase(1000m,900m,4,750,2000,false,true,false);
        NararaNativePreviewCase(1200000000m,1050000000m,4,990000000,0,false,false,true,-76f);
        NararaNativePreviewCase(1000000000000m,875000000000m,4,900000000,999999999,false,false,false);
        NararaNativePreviewCase(1000000000000m,100m,1,100,0,false,true,true,-108f);
        NararaNativePreviewCase(1000000000000m,100m,1,0,100,true,false,false);
    }
    static void NararaPowerDisplayCases()
    {
        foreach(string language in new[]{"zhs","eng"})
        {
            NararaDisplayLocalization(language);
            foreach(bool hbr in new[]{false,true})
            {
                NararaDisplayReset(1000m,900m,4,hbr);
                Check((bool)Call("PowerDispelRules","IsUndispellable",NararaDisplayPower)!,"new HP ability is protected on enemy Narara in every mode");
                Check(!(bool)Call("PowerDispelRules","CanPlayerDispel",NararaDisplayPower)!,"player dispel policy rejects the new HP ability");
                Check(!(bool)((Task<bool>)Call("PowerDispelRules","TryPlayerDispel",NararaDisplayPower)!).GetAwaiter().GetResult()&&Enemy.Powers.Contains(NararaDisplayPower),"actual player-dispel entry preserves original HP ability instance");
                var tips=NararaDisplayPower.HoverTips.OfType<HoverTip>().ToArray();
                string text=string.Join("\n",tips.Select(tip=>tip.Description));
                Check(text.Contains('4')&&!text.Contains("{Layers}",StringComparison.Ordinal),"actual power hover formats remaining count");
                string footer=new LocString("card_keywords","HBRGO_UNDISPELLABLE.footer").GetFormattedText();
                Check(text.Contains(footer,StringComparison.Ordinal)&&tips.Length>=2,"actual power hover contains native undispellable footer and explanation");
                Set(NararaDisplayPower,"_amount",2);Set(Enemy,"_currentHp",500);
                string changed=NararaDisplayPower.GetDumbHoverTip().Description;
                Check(changed.Contains('2')&&!changed.Contains("{Layers}",StringComparison.Ordinal),"native ability hover follows updated saved remaining count");
                NararaHpDisplayRows.Add(new{language,mode=hbr?"HBR":"legacy",source="real power hover",text,updated=changed});
            }
            foreach(string suffix in new[]{"title","description","compactDescription","referenceDescription"})
            {
                LocTable table=LocManager.Instance.GetTable("powers");
                Check(table.GetRawText("NARARA_LAYERED_HP_POWER."+suffix)==table.GetRawText("HBRGOSAKIMOD-NARARA_LAYERED_HP_POWER."+suffix),"new actual ID and compatibility alias share "+suffix+" text");
            }
            string manual=GlossaryEntry("powers","NARARA_LAYERED_HP_POWER",false);
            Check(!manual.Contains("{Layers}",StringComparison.Ordinal)&&(manual.Contains('4')||manual.Contains("four",StringComparison.OrdinalIgnoreCase)),"manual reads static four-bar rule rather than live placeholders");
            NararaHpDisplayRows.Add(new{language,source="manual reference",text=manual});
            object?[] compactArgs=["powers","NARARA_LAYERED_HP_POWER.description",(Func<string,bool>)(key=>LocString.Exists("powers",key)),null];
            bool available=(bool)Invoke(Helper("HbrCompactText").GetMethod("TryGetCompactKey",F)!,null,compactArgs)!;
            Check(available&&(string)compactArgs[3]! == "NARARA_LAYERED_HP_POWER.compactDescription","compact selection registers new HP power's actual bare ID");
            if(language=="zhs")
            {
                string guide=LocManager.Instance.GetTable("powers").GetRawText("TOUR_GUIDE_POWER.description");
                Check(!guide.Contains("75%")&&!guide.Contains("50%[/blue]")&&!guide.Contains("25%")&&guide.Contains("DP"),"TourGuide removes old HP thresholds and retains last-DP trigger");
                foreach(string prefix in new[]{"NARARA_LAYERED_DP_POWER","HBRGOSAKIMOD-NARARA_LAYERED_DP_POWER"})
                    foreach(string suffix in new[]{"description","compactDescription"})
                        Check(!LocManager.Instance.GetTable("powers").GetRawText(prefix+"."+suffix).Contains("每回合至多击破1条"),"DP text no longer claims a one-layer-per-turn budget");
            }
        }
        NararaDisplayReset(1000m,900m,4,true,true);
        Check(!(bool)Call("PowerDispelRules","IsUndispellable",NararaDisplayPower)!,"enemy-only protection does not change allied phantom dispel policy");
        Check(!(bool)Call("PhantomPowerTransferHelper","IsCompatibleWithMonster",NararaDisplayPower,Enemy.Monster)!,"enemy HP ability cannot be transplanted even to a Narara player phantom");
    }
    public static void RunNararaHpDisplay(string report)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();InstallNararaHpDisplay();NararaDisplayLocalization("zhs");NararaGaugeCases();NararaNativePreviewCases();NararaPowerDisplayCases();ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        File.WriteAllText(report,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=NararaHpDisplayRows,localeFiles=NararaHpLocaleFiles,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real native GetFgWidth and production UI prefixes/read-only exact layer helpers, real power/undispellable hovers, locale/manual/compact and player-dispel policy. Total and proxy HP seed the UI fixture; gameplay HP writes are tested separately by root. MegaLabel text rendering/Power.Icon and shared host leaves are isolated. No Godot/game visual, package, installation, network or saves."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Narara HP display checks");if(!ok)throw new Exception("Inspect "+report);
    }
}
