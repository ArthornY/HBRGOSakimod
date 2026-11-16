using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.TestSupport;

static class DisplayStandalone
{
    public static void Run(string path)=>EffectChecks.RunDisplay(path);
    public static void RunBody(string path)=>EffectChecks.RunDisplayBody(path);
}
static partial class EffectChecks
{
    static bool DisplayNoListeners(ref IEnumerable<MegaCrit.Sts2.Core.Models.AbstractModel> __result){__result=Array.Empty<MegaCrit.Sts2.Core.Models.AbstractModel>();return false;}
    static void DisplayProbabilityCases()
    {
        var should=(Func<bool,int,Func<int,int>,bool>)Helper("ThirtyOneXLanguagePrankHelper").GetMethod("ShouldTrigger",F)!.CreateDelegate(typeof(Func<bool,int,Func<int,int>,bool>));
        int calls=0;int Roll(int max){calls++;Check(max==1000,"probability samples exact1000domain");return 0;}
        Check(!should(false,1000,Roll)&&!should(false,1,Roll)&&calls==0,"no31X member never triggers or samples local randomness");
        Check(!should(true,0,Roll)&&!should(true,-10,Roll)&&calls==0,"zero/negative permille never triggers or samples");
        Check(should(true,1000,Roll)&&should(true,1500,Roll)&&calls==0,"1000and clamped-overflow always trigger without sampling");
        var config=typeof(HBRGOSakiConfig).GetProperty("ThirtyOneXHiddenEffectPermille")!;Equal((int)config.GetValue(null)!,1m,"hidden probability defaults to one permille");
        int deterministic=0;for(int index=0;index<100000;index++){int value=index%1000;if(should(true,1,_=>value))deterministic++;}
        Equal(deterministic,100m,"default probability is exactly0point1percent over a uniform domain");
        var random=new Random(7281);int sampled=0;for(int index=0;index<1000000;index++)if(should(true,1,random.Next))sampled++;
        Check(sampled is >850 and <1150,"independent local MonteCarlo sampling agrees with one-permille probability");Cases.Add(new{millionTrials=1000000,triggers=sampled});
        int gameBefore=RngCount(RunState.Rng.Niche);for(int index=0;index<1000;index++)should(true,1,random.Next);Equal(RngCount(RunState.Rng.Niche),gameBefore,"probability boundary never consumes game RNG");
    }
    static object DisplaySession(Dictionary<string,string[]> words,Dictionary<string,LocTable> chinese)
    {
        Type helper=Helper("ThirtyOneXLanguagePrankHelper"),type=helper.GetNestedType("Session",F)!;object session=Activator.CreateInstance(type,true)!;
        Set(session,"Combat",Combat);Set(session,"Translator",DisplayTranslator(words,391));Set(session,"Chinese",chinese);helper.GetField("_session",F)!.SetValue(null,session);return session;
    }
    static void DisplayAdmissionCases(Harmony harmony)
    {
        Type helper=Helper("ThirtyOneXLanguagePrankHelper");
        harmony.Patch(typeof(MegaCrit.Sts2.Core.Context.LocalContext).GetProperty("NetId",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoNetId),F)!));
        MethodInfo listenerPrefix=typeof(EffectChecks).GetMethod(nameof(DisplayNoListeners),F)!;
        MethodInfo[] listenerTargets=new[]{RunState.GetType(),typeof(CombatState)}.Select(type=>type.GetMethod("IterateHookListeners",F)!).ToArray();
        foreach(MethodInfo target in listenerTargets)harmony.Patch(target,prefix:new HarmonyMethod(listenerPrefix){priority=Priority.First+100});
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.ThirtyOneXPrankCombatStartPatch",true)!).Patch().Count>0,"installs real native combat-start admission boundary");
        HBRGOSakiConfig.ThirtyOneXHiddenEffectPermille=1000;
        try
        {
            if(Invoke(typeof(Hook).GetMethods(F).Single(method=>method.Name=="BeforeCombatStart"),null,RunState,Combat) is not Task task)throw new Exception("native BeforeCombatStart did not return Task");
            task.GetAwaiter().GetResult();
            Check(!(bool)helper.GetProperty("IsActive",F)!.GetValue(null)!&&ReferenceEquals(helper.GetField("_checkedCombat",F)!.GetValue(null),Combat),"actual native combat-start callback admits no31X session even at1000without loading Godot resources");
            Call("ThirtyOneXLanguagePrankHelper","Clear",true);
            Check(helper.GetField("_checkedCombat",F)!.GetValue(null)==null,"real cleanup resets admission ledger for the next battle");
        }
        finally{HBRGOSakiConfig.ThirtyOneXHiddenEffectPermille=1;foreach(MethodInfo target in listenerTargets)harmony.Unpatch(target,listenerPrefix);}
    }
    static void DisplayLocaleCases(Dictionary<string,string[]> words,Harmony harmony)
    {
        Type helper=Helper("ThirtyOneXLanguagePrankHelper");var manager=LocManager.Instance;
        var english=new Dictionary<string,LocTable>{["display_probe"]=new LocTable("display_probe",new(){["damage"]="Damage {n} points",["bad"]="Bad {missing}", ["inner"]="Inner {n}"},null!)};
        var chinese=new Dictionary<string,LocTable>{["display_probe"]=new LocTable("display_probe",new(){["damage"]="造成{n}点伤害",["bad"]="错误{missing}",["inner"]="内部{n}"},null!)};
        Set(manager,"_tables",english);Set(manager,"_engTables",english);Set(manager,"<Language>k__BackingField","rus");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<ValidationErrors>k__BackingField",Array.AsReadOnly(Array.Empty<LocValidationError>()));
        object session=DisplaySession(words,chinese);var text=new LocString("display_probe","damage");text.Add("n",17);
        string plain=text.GetRawText();Check(plain=="Damage {n} points","raw get outside formatting preserves original table result");
        string formatted=text.GetFormattedText();Check(formatted=="造成17点伤害","actual native SmartFormat temporarily reads Chinese raw then formats numeric value");
        Check(manager.Language=="rus"&&ReferenceEquals(typeof(LocManager).GetField("_tables",F)!.GetValue(manager),english)&&english["display_probe"].GetRawText("damage")=="Damage {n} points","effect never changes active language or mutates native localization tables");
        Check(helper.GetField("_format",F)!.GetValue(null)==null,"successful SmartFormat closes temporary Chinese scope");
        var bad=new LocString("display_probe","bad");try{bad.GetFormattedText();throw new Exception("expected formatting failure");}catch(Exception error)when(!error.Message.Contains("expected formatting")){}
        Check(helper.GetField("_format",F)!.GetValue(null)==null,"native formatting exception finalizer closes temporary scope");
        var inner=new LocString("display_probe","inner");inner.Add("n",23);
        object parent=Call("ThirtyOneXLanguagePrankHelper","BeginFormat",text)!;
        try
        {
            Check(inner.GetFormattedText()=="内部23","nested different-LocString formatting binds its own Chinese source");
            Check(ReferenceEquals(helper.GetField("_format",F)!.GetValue(null),parent),"nested formatting restores the parent scope");
            Check(text.GetRawText()=="造成{n}点伤害","restored parent remains valid until its own boundary finishes");
        }
        finally{Call("ThirtyOneXLanguagePrankHelper","EndFormat",parent);Call("ThirtyOneXLanguagePrankHelper","EndFormat",parent);}
        Check(helper.GetField("_format",F)!.GetValue(null)==null,"duplicate postfix/finalizer cleanup is idempotent");
        object outer=Call("ThirtyOneXLanguagePrankHelper","BeginFormat",text)!;
        try
        {
            bool testing=TestMode.IsOn;TestMode.IsOn=false;
            try
            {
                manager.StartOverridingLanguageAsEnglish();
                Check(text.GetFormattedText()=="Damage 17 points","Metrics native English override survives even an existing same-LocString Chinese scope");
            }
            finally{manager.StopOverridingLanguageAsEnglish();TestMode.IsOn=testing;}
        }
        finally{Call("ThirtyOneXLanguagePrankHelper","EndFormat",outer);}
        Check(manager.Language=="rus"&&helper.GetField("_format",F)!.GetValue(null)==null,"Metrics override restores language and nested scope without persistent changes");
        Call("ThirtyOneXLanguagePrankHelper","Clear",false);Check(!(bool)helper.GetProperty("IsActive",F)!.GetValue(null)!,"battle display Clear disables session");
        Check(text.GetFormattedText()=="Damage 17 points","cleared battle immediately returns to normal localization");
    }
    static void DisplayMetadataCases(Harmony harmony)
    {
        foreach(string name in new[]{"ThirtyOneXPrankDisplayPatch","ThirtyOneXPrankClearPatch","ThirtyOneXPrankDrawPatch"})
        {
            Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches."+name,true)!;var targets=((IEnumerable<MethodBase>)Invoke(patch.GetMethod("TargetMethods",F)!,null)!).ToArray();
            Check(targets.Length==(name.Contains("Display")?11:name.Contains("Draw")?8:5)&&targets.All(method=>method!=null&&!method.ContainsGenericParameters),name+" resolves complete ordinary managed UI/lifecycle bindings without invoking Godot setters");
            Cases.Add(new{patch=name,targets=targets.Select(method=>method.DeclaringType!.FullName+"."+method.Name).ToArray()});
        }
        Type start=Mod.GetType("HBRGOSakiMod.Core.Patches.ThirtyOneXPrankCombatStartPatch",true)!;
        Check(start.GetCustomAttributes<HarmonyPatch>().Any(attribute=>attribute.info.methodName==nameof(Hook.BeforeCombatStart)),"actual BeforeCombatStart hook owns per-battle admission");
        var forbidden=new List<string>();
        foreach(Type type in Helper("ThirtyOneXLanguagePrankHelper").Assembly.GetTypes().Where(type=>type==Helper("ThirtyOneXLanguagePrankHelper")||type.DeclaringType==Helper("ThirtyOneXLanguagePrankHelper")||type==DisplayTranslatorType))
        foreach(MethodInfo method in type.GetMethods(F|BindingFlags.DeclaredOnly).Where(method=>method.GetMethodBody()!=null))
        foreach(var instruction in PatchProcessor.GetOriginalInstructions(method))
            if(instruction.operand is MethodInfo call&&((call.DeclaringType?.Namespace?.StartsWith("MegaCrit.Sts2.Core.Random")??false)||call.DeclaringType==typeof(LocManager)&&call.Name is "SetLanguage" or "SetLanguageInternal"))forbidden.Add(method.Name+"->"+call);
        Check(forbidden.Count==0,"display implementation never calls gameplay RNG or native language setters");
    }
    static void DisplaySmartFormatCallers()
    {
        var found=new List<object>();
        var opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(field=>field.FieldType==typeof(OpCode)).Select(field=>(OpCode)field.GetValue(null)!).ToDictionary(code=>code.Value);
        foreach(Assembly assembly in new[]{typeof(LocManager).Assembly,typeof(BaseLib.Config.ModConfig).Assembly,typeof(STS2RitsuLib.Content.ModContentRegistry).Assembly,Mod}.Distinct())
        foreach(Type type in assembly.GetTypes())
        foreach(MethodBase method in type.GetMethods(F|BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(type.GetConstructors(F)).Where(method=>method.GetMethodBody()!=null))
        {
            // Read metadata only, including open generic bodies. Harmony's dynamic
            // importer cannot import every generic method and is unnecessary here.
            byte[] il=method.GetMethodBody()!.GetILAsByteArray()!;int calls=0;
            for(int offset=0;offset<il.Length;)
            {
                short value=il[offset++];if(value==0xfe)value=(short)(0xfe00|il[offset++]);OpCode opcode=opcodes[value];
                if(opcode.OperandType==OperandType.InlineMethod)
                {
                    MethodBase? call=method.Module.ResolveMethod(BitConverter.ToInt32(il,offset),type.IsGenericType?type.GetGenericArguments():null,method.IsGenericMethod?method.GetGenericArguments():null);
                    if(call?.DeclaringType==typeof(LocManager)&&call.Name==nameof(LocManager.SmartFormat))calls++;
                }
                offset+=opcode.OperandType switch
                {OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,offset),_=>4};
            }
            if(calls>0)found.Add(new{assembly=assembly.GetName().Name,caller=type.FullName+"."+method.Name,calls});
        }
        Cases.Add(new{directSmartFormatCallers=found});
    }
    public static void RunDisplay(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var harmony=new Harmony(ProductionId+".31x-display86");
            harmony.Patch(AccessTools.Method(typeof(LocManager),"TriggerLocaleChange"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            DisplayProbabilityCases();var words=DisplayWords();DisplayTranslatorCases(words);DisplayConfigCases(path);DisplayInstallRichResources(harmony);DisplayMetadataCases(harmony);DisplaySmartFormatCallers();DisplayAdmissionCases(harmony);
            foreach(string name in new[]{"ThirtyOneXPrankFormatScopePatch","ThirtyOneXPrankChineseRawPatch","ThirtyOneXPrankDisplayPatch","ThirtyOneXPrankClearPatch","ThirtyOneXPrankRichClearPatch","ThirtyOneXPrankDrawPatch"})Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches."+name,true)!).Patch().Count>0,"installs native "+name+" without invoking engine setters or lifecycle bodies");
            DisplayLocaleCases(words,harmony);DisplayTerminalCases(words,harmony);DisplayRichBodyCases(words,harmony);ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        finally{Call("ThirtyOneXLanguagePrankHelper","Clear",false);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Production probability/dictionary/translator, real native LocString/LocManager formatting and English override, real BaseLib config persistence, actual UI setter/append/clear and all eight draw-overload entries plus production RefreshNode/Clear source receipts. Engine-facing text storage, IDs/validity, fonts/redraw and empty native-child enumeration are controlled leaves. Lifecycle bindings are installed without running native lifecycle bodies. No Godot native object construction, engine/rendering, font shaping, network or packaging."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+"31X display checks");
    }
}
