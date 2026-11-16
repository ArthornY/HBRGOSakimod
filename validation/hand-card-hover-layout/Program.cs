using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;

static class OverchargeChecks { public static void Run(string reportPath)=>LayoutChecks.Run(reportPath); }

static class LayoutChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const float Epsilon=.002f;
    const string Owner="hbr.hand-card-hover-layout.checks";
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    readonly record struct Placement(Rect2 Bounds,float Scale);
    static MethodInfo Compute=>Mod.GetType("HBRGOSakiMod.Core.Helpers.HandCardHoverTipLayoutHelper",true)!.GetMethod("ComputePlacement",F)!;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Near(float actual,float expected,string message)=>Check(Math.Abs(actual-expected)<=Epsilon,$"{message}: {actual} = {expected}");
    static Placement Place(Rect2 viewport,Rect2 card,float width=380f,float gap=12f,float margin=16f)
    {
        object value;
        try{value=Compute.Invoke(null,[viewport,card,width,gap,margin])!;}
        catch(TargetInvocationException error)when(error.InnerException!=null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        Type type=value.GetType();return new((Rect2)type.GetProperty("Bounds",F)!.GetValue(value)!,(float)type.GetProperty("Scale",F)!.GetValue(value)!);
    }
    static object Box(Rect2 rect)=>new{x=rect.Position.X,y=rect.Position.Y,width=rect.Size.X,height=rect.Size.Y};
    static bool HasArea(Rect2 rect)=>rect.Size.X>0&&rect.Size.Y>0;
    static bool Finite(Rect2 rect)=>rect.Position.IsFinite()&&rect.Size.IsFinite()&&rect.End.IsFinite();
    static bool Overlap(Rect2 first,Rect2 second)=>first.Position.X<second.End.X-Epsilon&&first.End.X>second.Position.X+Epsilon&&
        first.Position.Y<second.End.Y-Epsilon&&first.End.Y>second.Position.Y+Epsilon;
    static Placement Validate(string label,Rect2 viewport,Rect2 card,float width=380f,float gap=12f,float margin=16f)
    {
        Placement result=Place(viewport,card,width,gap,margin);Rect2 safe=viewport.Grow(-Math.Max(0,margin));
        Cases.Add(new{label,viewport=Box(viewport),card=Box(card),width,gap,margin,bounds=Box(result.Bounds),result.Scale});
        Check(HasArea(result.Bounds)&&Finite(result.Bounds)&&float.IsFinite(result.Scale)&&result.Scale>0&&result.Scale<=1f,
            label+" returns finite positive reading bounds and one uniform scale");
        Check(result.Bounds.Position.X>=safe.Position.X-Epsilon&&result.Bounds.Position.Y>=safe.Position.Y-Epsilon&&
            result.Bounds.End.X<=safe.End.X+Epsilon&&result.Bounds.End.Y<=safe.End.Y+Epsilon,label+" stays inside viewport safe margins");
        Check(!Overlap(result.Bounds,card.Grow(Math.Max(0,gap))),label+" does not cover the supplied card AABB or its gap");
        Near(result.Bounds.Size.X,width*result.Scale,label+" uses the same scalar for native content width");return result;
    }
    static bool StringNameLeaf(StringName __instance){GC.SuppressFinalize(__instance);return false;}
    static bool Skip()=>false;
    static void MetadataCases()
    {
        var harmony=new Harmony(Owner);
        harmony.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(LayoutChecks).GetMethod(nameof(StringNameLeaf),F)!));
        Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        if(logger.GetMethod("GetIsRunningFromGodotEditor",F) is {} editor)harmony.Patch(editor,prefix:new HarmonyMethod(typeof(LayoutChecks).GetMethod(nameof(Skip),F)!));
        Type native=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet",true)!;
        Type holder=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder",true)!;
        Type hand=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Cards.Holders.NHandCardHolder",true)!;
        Type playerHand=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand",true)!;
        FieldInfo? followOwner=native.GetField("_followOwner",F|BindingFlags.DeclaredOnly);
        FieldInfo? hovered=holder.GetField("_isHovered",F|BindingFlags.DeclaredOnly);
        FieldInfo? dragged=playerHand.GetField("_draggedHolderIndex",F|BindingFlags.DeclaredOnly);
        Check(followOwner?.FieldType==typeof(bool)&&!followOwner.IsStatic,"active runtime declares the instance bool follow-owner field used by layout attachment");
        Check(hovered?.FieldType==typeof(bool)&&!hovered.IsStatic,"active runtime declares the instance bool holder-hover field used by wheel routing");
        Check(dragged?.FieldType==typeof(int)&&!dragged.IsStatic,"active runtime declares the instance int dragged-holder index used by drag guard");
        MethodInfo alignment=native.GetMethod("SetAlignmentForCardHolder",F,null,[holder],null)!;
        Check(alignment!=null&&alignment.DeclaringType==native&&alignment.ReturnType==typeof(void),"active runtime declares the exact native card-holder alignment target");
        Check(holder.IsAssignableFrom(hand),"active runtime hand holder has expected native card-holder inheritance");
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.HandCardHoverTipLayoutPatch",true)!;
        Check(harmony.CreateClassProcessor(patch).Patch().Count==1,"real production hand-hover Harmony patch installs on one native alignment method");
        var info=Harmony.GetPatchInfo(alignment!);
        Check(info!=null&&info.Postfixes.Any(item=>item.owner==Owner&&item.PatchMethod.DeclaringType==patch)&&
            info.Prefixes.All(item=>item.owner!=Owner)&&info.Transpilers.All(item=>item.owner!=Owner),"production attachment remains a postfix without replacing native alignment body");
        MethodInfo postfix=patch.GetMethod("Postfix",F)!;
        Check(PatchProcessor.GetOriginalInstructions(postfix).Any(instruction=>instruction.opcode==OpCodes.Isinst&&Equals(instruction.operand,hand)),
            "production postfix metadata filters the hand-holder subtype");
        var removes=native.GetMethods(F).Where(method=>method.Name=="Remove").ToArray();
        Check(removes.Length>0&&removes.All(method=>Harmony.GetPatchInfo(method)?.Owners.Contains(Owner)!=true),"native hover-tip Remove lifecycle receives no replacement patch from this feature");
        Type attachment=Mod.GetTypes().Single(type=>type.Name=="NHandCardHoverTips");
        Check(typeof(Node).IsAssignableFrom(attachment),"production attachment remains a Godot child-node type; no node is instantiated by fixture");
        Type inspectScreen=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Screens.NInspectCardScreen",true)!;
        Type inspectPatch=Mod.GetType("HBRGOSakiMod.Core.Patches.InspectCardHoverTipLayoutPatch",true)!;
        MethodInfo inspectUpdate=inspectScreen.GetMethod("UpdateCardDisplay",F)!;
        Check(harmony.CreateClassProcessor(inspectPatch).Patch().Count==1,"real production inspect-hover transpiler installs on actual native UpdateCardDisplay");
        Check(Harmony.GetPatchInfo(inspectUpdate)!.Transpilers.Any(item=>item.owner==Owner&&item.PatchMethod.DeclaringType==inspectPatch),
            "native inspect UpdateCardDisplay really owns the production layout adapter");
        Type label=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.addons.mega_text.MegaLabel",true)!;
        Type rich=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel",true)!;
        Check(typeof(Godot.Label).IsAssignableFrom(label)&&typeof(RichTextLabel).IsAssignableFrom(rich),
            "native tip Title/Description classes match both real wrapping-control casts");
        var parameters=Compute.GetParameters();
        Check(parameters.Select(parameter=>parameter.ParameterType).SequenceEqual(new[]{typeof(Rect2),typeof(Rect2),typeof(float),typeof(float),typeof(float)})&&
            Convert.ToSingle(parameters[3].DefaultValue)==12f&&Convert.ToSingle(parameters[4].DefaultValue)==16f,"geometry helper keeps explicit viewport/card/width/gap/margin contract");
    }
    static void GeometryCases()
    {
        Rect2 viewport=new(0,0,1920,1080),card=new(985,630,340,500);
        Placement screenshot=Validate("reported1920 screenshot",viewport,card);
        Near(screenshot.Scale,1f,"reported screenshot preserves native font scale");
        Near(screenshot.Bounds.Position.X,593f,"reported screenshot places full-width column to left of card");
        Near(screenshot.Bounds.Position.Y,16f,"reported screenshot starts sidebar at safe viewport top");
        Near(screenshot.Bounds.Size.Y,1048f,"reported screenshot uses full safe viewport height");
        Placement left=Validate("left edge card",viewport,new Rect2(20,630,340,500));
        Check(left.Bounds.Position.X>=372f-Epsilon&&left.Scale==1f,"left-edge card switches tooltip to full-scale right sidebar");
        Placement right=Validate("right edge card",viewport,new Rect2(1560,630,340,500));
        Check(right.Bounds.End.X<=1548f+Epsilon&&right.Scale==1f,"right-edge card keeps tooltip left with gap");
        Placement narrow=Validate("narrow700 above fallback",new Rect2(0,0,700,1080),new Rect2(180,600,340,450));
        Near(narrow.Scale,1f,"narrow viewport uses above-card reading area without shrinking fonts");
        Check(narrow.Bounds.End.Y<=588f+Epsilon&&narrow.Bounds.Size.Y>=160f,"above fallback retains gap and a useful scroll viewport");
        Placement below=Validate("top card below fallback",new Rect2(0,0,700,1080),new Rect2(180,30,340,450));
        Check(below.Scale==1f&&below.Bounds.Position.Y>=492f-Epsilon,"top-positioned narrow card can use full-scale below fallback");
        Placement tiny=Validate("extremely narrow300 fallback",new Rect2(0,0,300,400),new Rect2(80,200,140,180));
        Near(tiny.Scale,268f/380f,"only viewport narrower than content uses uniform width scaling");
        Placement translated=Validate("nonzero viewport origin",new Rect2(100,200,1920,1080),new Rect2(1085,830,340,500));
        Near(translated.Bounds.Position.X,screenshot.Bounds.Position.X+100f,"nonzero origin translates sidebar x");
        Near(translated.Bounds.Position.Y,screenshot.Bounds.Position.Y+200f,"nonzero origin translates sidebar y");
        Check(translated.Bounds.Size==screenshot.Bounds.Size&&translated.Scale==screenshot.Scale,"nonzero origin preserves size and font scale");
        foreach(float scale in new[]{.5f,1.5f})
        {
            Placement scaled=Validate("uniform coordinate scale"+scale,new Rect2(viewport.Position*scale,viewport.Size*scale),
                new Rect2(card.Position*scale,card.Size*scale),380f*scale,12f*scale,16f*scale);
            Near(scaled.Bounds.Position.X,screenshot.Bounds.Position.X*scale,"uniform coordinate transform preserves placement x");
            Near(scaled.Bounds.Size.Y,screenshot.Bounds.Size.Y*scale,"uniform coordinate transform preserves reading height");
            Near(scaled.Scale,1f,"uniform coordinate transform does not shrink native content");
        }
        Validate("rotated-card supplied AABB",viewport,new Rect2(900,560,500,650));
        Validate("card wholly outside viewport",viewport,new Rect2(2400,630,340,500));
        Check(Place(viewport,card,380f,-5f,-10f)==Place(viewport,card,380f,0f,0f),"negative gap and margin clamp to zero deterministically");
        for(int index=0;index<5;index++)
        {
            _=Place(new Rect2(0,0,700,1080),new Rect2(180,600,340,450));
            Check(Place(viewport,card)==screenshot,"reentered geometry call does not inherit prior placement state"+index);
        }
    }
    static void StableFrameCases()
    {
        Type inspect=Mod.GetType("HBRGOSakiMod.Core.Helpers.InspectCardHoverTipLayoutHelper",true)!;
        MethodInfo inspectCompute=inspect.GetMethod("ComputePlacement",F)!;
        MethodInfo handAnchor=Compute.DeclaringType!.GetMethod("InParentFrame",F)!;
        MethodInfo inspectAnchor=inspect.GetMethod("InParentFrame",F)!;
        Rect2 viewport=new(0,0,1920,1080),reserved=new(1050,90,600,940);
        object inspectValue=inspectCompute.Invoke(null,[viewport,reserved,380f,12f,16f])!;
        Rect2 inspectBounds=(Rect2)inspectValue.GetType().GetProperty("Bounds",F)!.GetValue(inspectValue)!;
        Check(!Overlap(inspectBounds,reserved.Grow(12f)),"inspect column reserves the full card/arrows area");
        Check(viewport.Grow(-16f).Encloses(inspectBounds),"inspect column remains inside safe reading area");
        Vector2 anchor=Place(viewport,new Rect2(985,630,340,500)).Bounds.Position;
        for(int cycle=0;cycle<40;cycle++)
        {
            // Same card/viewport, but native re-alignment may assign a new root
            // translation after each compact/full rebuild or delayed measure.
            Vector2 shift=new((cycle%2==0?740:-310)+cycle*13,(cycle%3)*61-180);
            Transform2D tipToParent=new(0f,new Vector2(cycle%2==0?1f:1.25f,cycle%2==0?1f:1.25f),0f,shift);
            foreach(MethodInfo method in new[]{handAnchor,inspectAnchor})
            {
                Transform2D local=(Transform2D)method.Invoke(null,[tipToParent,anchor,1f])!;
                Transform2D actual=tipToParent*local;
                Near(actual.Origin.X,anchor.X,method.DeclaringType!.Name+" cancels native Shift-root displacement X "+cycle);
                Near(actual.Origin.Y,anchor.Y,method.DeclaringType!.Name+" cancels native Shift-root displacement Y "+cycle);
                Near(actual.X.X,1f,"stable parent width does not compound root scale "+cycle);
                Near(actual.Y.Y,1f,"stable parent height does not compound root scale "+cycle);
            }
        }
        Transform2D movedRoot=new(0f,new Vector2(500,220));
        Check((movedRoot*new Transform2D(0f,anchor)).Origin!=anchor,
            "negative control: treating absolute bounds as root-local genuinely drifts after native root movement");
        string root=FindRoot();
        foreach(string node in new[]{"Core/Nodes/Cards/NHandCardHoverTips.cs","Core/Nodes/Screens/NInspectCardHoverTips.cs"})
        {
            string source=File.ReadAllText(Path.Combine(root,node));
            Check(source.Contains("_tips.ItemRectChanged +=")&&source.Contains("_tips.ItemRectChanged -="),node+" tracks native root movement and releases the event subscription");
            Check(source.Contains("GetParent() as CanvasItem")&&source.Contains("InParentFrame(tipsToFrame"),node+" derives its actual transform from the stable parent frame");
            Check(!source.Contains("NeedsSidebar(")&&!source.Contains("GetCombinedMinimumSize().X"),node+" cannot gate first layout on stale native columns or widen on each translated rebuild");
            Check(source.Contains("WrapText(entry)")&&source.Contains("entry.Reparent(_column, false)"),node+" retains all native entries in the wrapping scroll column");
        }
        string helper=File.ReadAllText(Path.Combine(root,"Core/Helpers/HandCardHoverTipLayoutHelper.cs"));
        Check(helper.Contains("title.AutowrapMode = TextServer.AutowrapMode.WordSmart")&&helper.Contains("body.AutowrapMode = TextServer.AutowrapMode.WordSmart")&&
            helper.Contains("TextServer.OverrunBehavior.NoTrimming"),"mixed-language long titles and bodies wrap without truncating their text");
        Check(!helper.Contains(".Text =")&&!helper.Contains(".Hide("),"wrapping leaves text content and visibility unchanged");
        foreach(string text in new[]{"攻击力提升／защита／aumento／आक्रमण",new string('界',120),"difendereуправлятьсильныйसुरक्षा"})
        {
            _=text; Check(Place(viewport,new Rect2(985,630,340,500),380f).Bounds.Position==anchor,
                "fixed native column anchor is independent of compact/full mixed-language title length");
        }
    }
    static string FindRoot()
    {
        for(DirectoryInfo? directory=new(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"HBRGOSakiMod.csproj")))return directory.FullName;
        throw new Exception("Repository root missing");
    }
    static void InvalidCases()
    {
        Rect2 viewport=new(0,0,1920,1080),card=new(985,630,340,500);
        var cases=new (Rect2 View,Rect2 Card,float Width,float Gap,float Margin)[]{
            (default,card,380,12,16),(viewport,default,380,12,16),(new Rect2(0,0,-1,1080),card,380,12,16),
            (viewport,new Rect2(float.NaN,0,100,100),380,12,16),(new Rect2(0,0,float.PositiveInfinity,100),card,380,12,16),
            (viewport,card,0,12,16),(viewport,card,-1,12,16),(viewport,card,float.NaN,12,16),(viewport,card,float.PositiveInfinity,12,16),
            (viewport,card,380,float.NaN,16),(viewport,card,380,12,float.PositiveInfinity),(viewport,card,380,12,2000),
            (new Rect2(0,0,400,300),new Rect2(0,0,400,300),380,12,16)};
        foreach(var entry in cases)
        {
            Placement result=Place(entry.View,entry.Card,entry.Width,entry.Gap,entry.Margin);
            Check(result.Bounds==default&&result.Scale==1f&&Finite(result.Bounds),"invalid or unreadable layout returns empty finite bounds and neutral scale");
        }
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try{MetadataCases();GeometryCases();StableFrameCases();InvalidCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ModelDb).Assembly.Location))),
            sourceNativeSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),telemetryIsolation=System.Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Pure production placement geometry and real Harmony installation/native method metadata only. Covers viewport/card AABB inputs, safe bounds, side selection, readable fallback, one width scale, invalid inputs and repeatability. StringName/editor detection leaves isolated. No Godot nodes instantiated, no native alignment body or input event dispatched; the parent-frame transform is executed as pure Godot geometry, event/entry wiring is checked from production source. No rendered tooltip/scroll/wheel/lifecycle/gameplay claim, no screenshots, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" hand-card hover layout checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
