using System.Reflection;
using OpCodes=System.Reflection.Emit.OpCodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

static partial class EffectChecks
{
    // Only these native engine-facing leaves are controlled. The production
    // setter prefix, source receipts, display record and cleanup remain real.
    sealed class DisplayTerminal
    {
        internal ulong Id;
        internal int Redraws;
        internal readonly Dictionary<string,string> Slots=new();
    }
    static readonly Dictionary<GodotObject,DisplayTerminal> DisplayTerminals=new(ReferenceEqualityComparer.Instance);
    static bool DisplayTerminalWrite(GodotObject __instance,string __0,MethodBase __originalMethod)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        terminal.Slots[__originalMethod.Name[4..]]=__0;return false;
    }
    static bool DisplayTerminalRead(GodotObject __instance,ref string __result,MethodBase __originalMethod)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        __result=terminal.Slots.GetValueOrDefault(__originalMethod.Name[4..],string.Empty);return false;
    }
    static bool DisplayTerminalId(GodotObject __instance,ref ulong __result)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        __result=terminal.Id;return false;
    }
    static bool DisplayTerminalValid(GodotObject __0,ref bool __result)
    {
        if(ReferenceEquals(__0,null)||!DisplayTerminals.ContainsKey(__0))return true;
        __result=true;return false;
    }
    static bool DisplayTerminalFont(GodotObject __instance,ref Font __result)
    {
        if(!DisplayTerminals.ContainsKey(__instance))return true;
        __result=null!;return false;
    }
    static bool DisplayTerminalAppend(GodotObject __instance,string __0)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        terminal.Slots["Text"]=terminal.Slots.GetValueOrDefault("Text",string.Empty)+__0;return false;
    }
    static bool DisplayTerminalClear(GodotObject __instance)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        terminal.Slots["Text"]=string.Empty;return false;
    }
    static bool DisplayTerminalRedraw(GodotObject __instance)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        terminal.Redraws++;return false;
    }
    static bool DisplayTerminalDraw(GodotObject __instance,string __2)
    {
        if(!DisplayTerminals.TryGetValue(__instance,out var terminal))return true;
        terminal.Slots["DrawString"]=__2;return false;
    }
    static bool DisplayTerminalChildren(Node __instance,ref Godot.Collections.Array<Node> __result)
    {
        if(!DisplayTerminals.ContainsKey(__instance))return true;
        // The paired test-only callsite adapter supplies an empty native child
        // enumeration without creating a Godot Array or dereferencing a handle.
        __result=null!;return false;
    }
    static IEnumerator<Node> DisplayTerminalChildEnumeration(Godot.Collections.Array<Node> ignored)
        => Enumerable.Empty<Node>().GetEnumerator();
    static IEnumerable<CodeInstruction> DisplayTerminalChildTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        int count=0;MethodInfo adapter=typeof(EffectChecks).GetMethod(nameof(DisplayTerminalChildEnumeration),F)!;
        foreach(CodeInstruction instruction in instructions)
        {
            if(instruction.operand is MethodInfo call&&call.Name=="GetEnumerator"&&call.DeclaringType==typeof(Godot.Collections.Array<Node>))
            {var replacement=new CodeInstruction(OpCodes.Call,adapter);replacement.labels.AddRange(instruction.labels);replacement.blocks.AddRange(instruction.blocks);count++;yield return replacement;}
            else yield return instruction;
        }
        if(count!=1)throw new Exception("Expected exactly one controlled Godot child enumeration leaf, got "+count);
    }
    static void DisplayInstallTerminals(Harmony harmony)
    {
        void Patch(MethodBase method,string name)=>harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=Priority.Last});
        Patch(typeof(GodotObject).GetMethod(nameof(GodotObject.GetInstanceId))!,nameof(DisplayTerminalId));
        Patch(typeof(GodotObject).GetMethod(nameof(GodotObject.IsInstanceValid))!,nameof(DisplayTerminalValid));
        Patch(typeof(Control).GetMethod(nameof(Control.GetThemeFont))!,nameof(DisplayTerminalFont));
        Patch(typeof(Node).GetMethod(nameof(Node.GetChildren))!,nameof(DisplayTerminalChildren));
        Patch(typeof(CanvasItem).GetMethod(nameof(CanvasItem.QueueRedraw))!,nameof(DisplayTerminalRedraw));
        foreach(MethodInfo draw in typeof(CanvasItem).GetMethods(F).Where(method=>method.Name is "DrawString" or "DrawStringOutline" or "DrawMultilineString" or "DrawMultilineStringOutline"))Patch(draw,nameof(DisplayTerminalDraw));
        harmony.Patch(Helper("ThirtyOneXLanguagePrankHelper").GetMethod("RefreshNode",F)!,transpiler:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(DisplayTerminalChildTranspiler),F)!));
        foreach(Type type in new[]{typeof(Label),typeof(RichTextLabel)})
        {
            PropertyInfo text=type.GetProperty("Text")!;
            Patch(text.GetMethod!,nameof(DisplayTerminalRead));
            Patch(text.SetMethod!,nameof(DisplayTerminalWrite));
        }
        PropertyInfo tooltip=typeof(Control).GetProperty(nameof(Control.TooltipText))!;
        Patch(tooltip.GetMethod!,nameof(DisplayTerminalRead));
        Patch(tooltip.SetMethod!,nameof(DisplayTerminalWrite));
        Patch(typeof(RichTextLabel).GetMethod(nameof(RichTextLabel.AppendText))!,nameof(DisplayTerminalAppend));
        Patch(typeof(RichTextLabel).GetMethod(nameof(RichTextLabel.AddText))!,nameof(DisplayTerminalAppend));
        Patch(typeof(RichTextLabel).GetMethod(nameof(RichTextLabel.Clear))!,nameof(DisplayTerminalClear));
    }
    static T DisplayFake<T>(ulong id,string text)where T:GodotObject
    {
        T node=Blank<T>();var terminal=new DisplayTerminal{Id=id};terminal.Slots["Text"]=text;DisplayTerminals[node]=terminal;return node;
    }
    static void DisplayTerminalCases(Dictionary<string,string[]> words,Harmony harmony)
    {
        DisplayInstallTerminals(harmony);
        Type helper=Helper("ThirtyOneXLanguagePrankHelper");
        var english=new Dictionary<string,LocTable>{["display_terminal"]=new("display_terminal",new(){["damage"]="Damage {n} points",["name"]="Legacy Flash",["parent"]="Card: {name}{suffix}",["help"]="Help",["childA"]="Damage {n}",["childB"]="Harm {n}",["children"]="{left} / {right}",["normalA"]="Shared {n}",["normalB"]="Shared {n}"},null!)};
        var chinese=new Dictionary<string,LocTable>{["display_terminal"]=new("display_terminal",new(){["damage"]="造成{n}点伤害",["name"]="传承一闪",["parent"]="卡牌：{name}{suffix}",["help"]="帮助",["childA"]="伤害{n}",["childB"]="伤害{n}",["children"]="{left} / {right}",["normalA"]="左{n}",["normalB"]="右{n}"},null!)};
        Set(LocManager.Instance,"_tables",english);Set(LocManager.Instance,"_engTables",english);
        var damage=new LocString("display_terminal","damage");damage.Add("n",17);string original=damage.GetFormattedText();
        var name=new LocString("display_terminal","name");string originalName=name.GetFormattedText();
        var normalA=new LocString("display_terminal","normalA");normalA.Add("n",11);string normalTextA=normalA.GetFormattedText();
        var normalB=new LocString("display_terminal","normalB");normalB.Add("n",11);string normalTextB=normalB.GetFormattedText();
        Check(normalTextA==normalTextB&&!ReferenceEquals(normalTextA,normalTextB),"real native formatter produces distinct string references for equal-valued independent source receipts");
        var sameValueA=DisplayFake<Label>(71010,normalTextA);var sameValueB=DisplayFake<Label>(71011,normalTextB);
        var existing=DisplayFake<Label>(71001,original);DisplayTerminals[existing].Slots["TooltipText"]="Help";
        object session=DisplaySession(words,chinese);Call("ThirtyOneXLanguagePrankHelper","BuildPlainDisplayLookup",session);
        object translator=session.GetType().GetField("Translator",F)!.GetValue(session)!;
        string Expected(ulong id,string property,string raw)=>Translate(translator,(ulong)Call("ThirtyOneXLanguagePrankHelper","SurfaceKey",id,property)!,raw);
        Call("ThirtyOneXLanguagePrankHelper","RefreshNode",existing);
        Check(existing.Text==Expected(71001,"Text","造成17点伤害"),"activation refreshes an already-visible dynamically formatted native-locale label through real RefreshNode");
        Check(existing.TooltipText==Expected(71001,"TooltipText","帮助"),"same surface independently converts its existing tooltip property");
        Call("ThirtyOneXLanguagePrankHelper","RefreshNode",sameValueA);Call("ThirtyOneXLanguagePrankHelper","RefreshNode",sameValueB);
        Check(sameValueA.Text==Expected(71010,"Text","左11")&&sameValueB.Text==Expected(71011,"Text","右11"),"equal-valued native-locale strings retain their own weak source references during activation refresh");
        string initial=existing.Text;
        for(int index=0;index<20;index++)existing.Text=existing.Text;
        Check(existing.Text==initial,"actual UI setter readback is idempotent across repeated assignments");
        ((System.Collections.IDictionary)DisplayTranslatorType.GetField("_cache",F)!.GetValue(translator)!).Clear();existing.Text=existing.Text;
        Check(existing.Text==initial,"UI readback stays identical after bounded translator cache clearing");
        damage.Add("n",18);existing.Text=damage.GetFormattedText();
        Check(existing.Text==Expected(71001,"Text","造成18点伤害")&&existing.Text==initial.Replace("17","18"),"actual numeric refresh updates only digits without choosing new languages");
        var parent=new LocString("display_terminal","parent");parent.Add("name",name);parent.Add("suffix","+7");string composed=parent.GetFormattedText();
        var composition=DisplayFake<Label>(71002,string.Empty);composition.Text=composed;
        Check((string)Call("ThirtyOneXLanguagePrankHelper","GetOriginalDisplay",composed)! == "Card: Legacy Flash+7","active eager child LocString variables preserve their real-locale parent receipt and upgrade suffix");
        var childA=new LocString("display_terminal","childA");childA.Add("n",11);string childTextA=childA.GetFormattedText();
        var childB=new LocString("display_terminal","childB");childB.Add("n",11);string childTextB=childB.GetFormattedText();
        Check(childTextA==childTextB&&!ReferenceEquals(childTextA,childTextB),"equal-valued Chinese child outputs have distinct real native formatter identities");
        var children=new LocString("display_terminal","children");children.Add("left",childTextA);children.Add("right",childTextB);string childComposition=children.GetFormattedText();
        Check((string)Call("ThirtyOneXLanguagePrankHelper","GetOriginalDisplay",childComposition)! == "Damage 11 / Harm 11","same-Chinese/different-native-locale child variables restore independently without value-key contamination");
        var title=DisplayFake<Label>(71003,string.Empty);title.Text=name.GetFormattedText()+"+3";
        var raw=DisplayFake<Label>(71004,string.Empty);raw.Text="未知19";
        var emptied=DisplayFake<Label>(71009,"previous");emptied.Text="未知20";emptied.TooltipText="未知提示";emptied.Text=string.Empty;emptied.TooltipText=string.Empty;
        var rich=DisplayFake<RichTextLabel>(71005,"Preamble ");Call("ThirtyOneXLanguagePrankHelper","RefreshNode",rich);
        rich.AppendText(damage.GetFormattedText());rich.AddText(" end");
        Check(rich.Text.StartsWith("Preamble ")&&rich.Text.EndsWith(" end"),"native rich append/add leaves preserve surrounding text while production callbacks track complete output");
        string richOutput=rich.Text;rich.Text=rich.Text;Check(rich.Text==richOutput,"full rich-text readback does not reroll an appended display");
        var clearedRich=DisplayFake<RichTextLabel>(71006,string.Empty);clearedRich.AppendText("discarded ");clearedRich.Clear();clearedRich.AppendText(damage.GetFormattedText());
        var drawing=DisplayFake<Label>(71007,string.Empty);string drawingText=name.GetFormattedText()+"+4";
        drawing.DrawString(null!,default,drawingText);
        Check(DisplayTerminals[drawing].Slots["DrawString"]==Expected(71007,"DrawString","传承一闪+4"),"actual CanvasItem.DrawString managed entry transforms its text without an engine draw");
        foreach(MethodInfo draw in typeof(CanvasItem).GetMethods(F).Where(method=>method.Name is "DrawString" or "DrawStringOutline" or "DrawMultilineString" or "DrawMultilineStringOutline"))
        {
            object?[] args=draw.GetParameters().Select((parameter,index)=>index==0?null:index==1?(object)default(Vector2):index==2?drawingText:
                parameter.HasDefaultValue?parameter.DefaultValue:parameter.ParameterType.IsValueType?Activator.CreateInstance(parameter.ParameterType):null).ToArray();
            Invoke(draw,drawing,args);
            Check(DisplayTerminals[drawing].Slots["DrawString"]==Expected(71007,"DrawString","传承一闪+4"),"actual independently bound draw terminal transforms text: "+draw.Name+"/"+args.Length);
        }
        Type ribbonType=Mod.GetType("HBRGOSakiMod.Core.Nodes.Screens.NMemoryGardenRibbon",true)!;
        var ribbon=(GodotObject)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(ribbonType);DisplayTerminals[ribbon]=new(){Id=71008};
        MethodInfo display=ribbonType.GetMethod("Display",F)!;object slot=Enum.GetValues(display.GetParameters()[0].ParameterType).GetValue(0)!;
        Invoke(display,ribbon,slot,drawingText);
        Check((string)ribbonType.GetField("_text",F)!.GetValue(ribbon)! == originalName+"+4","real memory-garden ribbon retains its original-locale backing text for later redraw");
        // The saved per-property receipt, not either bounded input map, is the
        // authority for readback and cleanup after cache eviction.
        ((System.Collections.IDictionary)session.GetType().GetField("OriginalFormats",F)!.GetValue(session)!).Clear();
        ((System.Collections.IDictionary)helper.GetField("FormattedSources",F)!.GetValue(null)!).Clear();
        existing.Text=existing.Text;rich.Text=rich.Text;
        Call("ThirtyOneXLanguagePrankHelper","Clear",false);
        Check(existing.Text=="Damage 18 points"&&existing.TooltipText=="Help","Clear restores independently recorded real-locale text and tooltip including latest numeric value");
        Check(composition.Text=="Card: Legacy Flash+7"&&title.Text=="Legacy Flash+3","Clear restores original-locale child text and externally appended upgrade suffix");
        Check(raw.Text=="未知19","unknown literal UI text restores its exact original input");
        Check(emptied.Text==string.Empty&&emptied.TooltipText==string.Empty,"empty native property assignments discard stale restore receipts instead of reviving cleared UI text");
        Check(rich.Text=="Preamble Damage 18 points end"&&clearedRich.Text=="Damage 18 points","Clear restores complete appended originals and respects a native RichTextLabel.Clear boundary");
        Check(DisplayTerminals[drawing].Redraws>0,"Clear queues a redraw for an actual recorded DrawString surface");
        Check(helper.GetField("_format",F)!.GetValue(null)==null&&!(bool)helper.GetField("_originalFormat",F)!.GetValue(null)!,"UI/locale cleanup leaves neither temporary nor original-format guard active");
    }
}
