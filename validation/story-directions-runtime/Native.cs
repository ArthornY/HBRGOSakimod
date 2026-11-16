#nullable disable
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Events;
using HBRGOSakiMod.Core.Story;
using Mono.Cecil;
using Environment=System.Environment;

static class DirectionChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    #if SOURCE_LINKED_CHECKS
    static readonly Assembly Mod=typeof(NLibraryEvent).Assembly;
#else
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
#endif
    static readonly Type Reader=typeof(NLibraryEvent),Stage=Mod.GetType("HBRGOSakiMod.Core.Nodes.Events.Story.NLibraryStoryStage",true),Clock=Mod.GetType("HBRGOSakiMod.Core.Nodes.Events.Story.HbrStoryAnimationClock",true),Audio=Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrStoryAudioSession",true);
    static readonly List<string> Passed=new(),Trace=new();
    static Harmony Harmony;
    static object CurrentClock;static NLibraryEvent CurrentReader;
    static int Paints,Voices,Cutins,Destroyed;static bool MissingCutin,LastAngry;static HbrStoryStillState HiddenStill;static float FadeAlpha,MaterialBlur;
    static TaskCompletionSource<bool> VoiceGate;
    static readonly List<Task> FadeTasks=new();
    static float ScenePoseSeconds,SceneAlphaSeconds;static int GhostFreed,GhostShown;
    static void Check(bool value,string message){if(!value)throw new Exception(message+"; trace="+string.Join(",",Trace));Passed.Add(message);}
    static object Blank(Type t)=>RuntimeHelpers.GetUninitializedObject(t);
    static object Get(object o,string n)=>o.GetType().GetField(n,F)?.GetValue(o)??o.GetType().GetProperty(n,F)?.GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
    static object Invoke(MethodInfo method,object target,params object[] args){try{return method.Invoke(target,args);}catch(TargetInvocationException e){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object Call(object target,string n,params object[] args)=>Invoke(target.GetType().GetMethod(n,F),target,args);
    static object Static(Type t,string n,params object[] args)=>Invoke(t.GetMethod(n,F),null,args);
    static bool VoidStage(MethodBase __originalMethod,object[] __args)
    {
        Trace.Add(__originalMethod.Name);
        if(__originalMethod.Name=="HideStill")HiddenStill=(HbrStoryStillState)__args[0];
        if(__originalMethod.Name=="Scene")Trace.Add("scene:"+__args[0]);
        return false;
    }
    static bool Fade(Color color,float alpha,float duration,CancellationToken token,ref Task __result)
    {
        Trace.Add("fade:"+(color.R>0?"white":"black")+":"+alpha);
        __result=(Task)Call(CurrentClock,"Animate","fade",(double)duration,(Action<float>)(t=>FadeAlpha=t*alpha),token);FadeTasks.Add(__result);return false;
    }
    static bool CancelMotion(bool finish){Trace.Add("CancelMotion");if(finish)Call(CurrentClock,"CompleteAll");return false;}
    static bool AudioVoid(MethodBase __originalMethod,object[] __args){Trace.Add("audio:"+__originalMethod.Name);return false;}
    static bool Voice(string cue,bool monologue,CancellationToken cancellationToken,ref Task __result){Voices++;Trace.Add("voice:"+cue);__result=VoiceGate?.Task.WaitAsync(cancellationToken)??Task.CompletedTask;return false;}
    static bool CloseAudio(ref Task __result){__result=Task.CompletedTask;Trace.Add("audio:CloseAsync");return false;}
    static bool BoolAudio(ref bool __result){__result=VoiceGate!=null&&!VoiceGate.Task.IsCompleted;return false;}
    static bool FloatAudio(ref float __result){__result=.1f;return false;}
    static bool VisualMap(string section,string key,ref string __result){__result=section=="characterIds"&&key=="RKayamori"?"ruka":null;return false;}
    static bool Paint(object page){Paints++;Trace.Add("paint");return false;}
    static bool Block(){return false;}
    static bool FakeView(HbrStoryDirection d,bool mini,ref Control __result){Cutins++;__result=MissingCutin?null:(Control)Blank(typeof(Control));Trace.Add(mini?"mini":"cutin");return false;}
    static bool Black(HbrStoryDirection d,ref Control __result){__result=(Control)Blank(typeof(Control));Trace.Add("blacktext");return false;}
    static bool Bubble(HbrStoryDirection d,bool angry,bool restoring,ref Control __result){LastAngry=angry;__result=(Control)Blank(typeof(Control));Trace.Add("bubble:"+d.Op+":"+restoring);return false;}
    static bool Destroy(Control node){Destroyed++;return false;}
    static bool Visible(Control node,bool visible){Trace.Add("visible:"+visible);return false;}
    static bool Opacity(Control node,float alpha){return false;}
    static void Patch(Harmony h,Type type,string target,string prefix)=>h.Patch(type.GetMethod(target,F),prefix:new HarmonyMethod(typeof(DirectionChecks).GetMethod(prefix,F)));
    static void Install()
    {
        typeof(Checks).GetMethod("Setup",F).Invoke(null,null);
        var h=Harmony=new Harmony("hbr.story.direction.runtime-checks");
        foreach(string method in new[]{"Scene","Background","Actors","Camera","Filters","Effect","KeepEffects","Still","HideStill","CrossFade","Shake","StopShake","Tick","SetViewport","RetriggerEffect"})Patch(h,Stage,method,nameof(VoidStage));
        Patch(h,Stage,"Fade",nameof(Fade));Patch(h,Stage,"CancelMotion",nameof(CancelMotion));
        foreach(string method in new[]{"Bgm","BgmStop","Env","EnvStop","Se","StopVoice","Process","SetCovered","Dispose"})Patch(h,Audio,method,nameof(AudioVoid));
        Patch(h,Audio,"Voice",nameof(Voice));Patch(h,Audio,"CloseAsync",nameof(CloseAudio));Patch(h,Audio,"get_IsVoicePlaying",nameof(BoolAudio));Patch(h,Audio,"get_VoiceAmplitude",nameof(FloatAudio));
        Patch(h,Mod.GetType("HBRGOSakiMod.Core.Nodes.Events.Story.NLibraryStoryVisualResources",true),"Resolve",nameof(VisualMap));
        Patch(h,Reader,"PaintLine",nameof(Paint));Patch(h,Reader,"SetDirectionBlock",nameof(Block));Patch(h,Reader,"CreateCutInView",nameof(FakeView));Patch(h,Reader,"CreateBlackTextView",nameof(Black));Patch(h,Reader,"Bubble",nameof(Bubble));Patch(h,Reader,"DestroyTransient",nameof(Destroy));Patch(h,Reader,"SetTransientVisible",nameof(Visible));Patch(h,Reader,"SetTransientOpacity",nameof(Opacity));
    }
    static HbrStoryLine Line(params HbrStoryDirection[] directions)=>new(){Kind="narration",Text="Temporary fixture only.",Direction=directions};
    static NLibraryEvent Configure(params HbrStoryLine[] lines)
    {
        if(lines.Length>0&&lines[0].Characters==null)typeof(HbrStoryLine).GetProperty("Characters").SetValue(lines[0],new[]{new HbrStoryCharacter{Id="A",Image="res://images/fixture/actor.png"}});
        CurrentReader=(NLibraryEvent)Blank(Reader);CurrentClock=Activator.CreateInstance(Clock,true);var stage=Blank(Stage);Set(stage,"_clock",CurrentClock);
        foreach(string field in new[]{"_pages","_directionPlans","_visitedDirectionLines","_directionTransient"})Set(CurrentReader,field,Activator.CreateInstance(Reader.GetField(field,F).FieldType));
        Set(CurrentReader,"_directionStage",stage);Set(CurrentReader,"_storyAudio",Blank(Audio));Set(CurrentReader,"_logicalScene",HbrStorySceneState.Empty);
        var definition=HbrMemoryUpgradeStoryDefinition.Parse(JsonSerializer.Serialize(new{schemaVersion=1,characterId="ruka",presentation=new{background="res://images/fixture/bg.png"},segments=new[]{new{level=1,perspectives=new[]{"31o"},lines}}},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
        Set(CurrentReader,"_upgradeMemory",definition.Segments[0]);
        Call(CurrentReader,"PrepareDirectionPage",definition.Segments[0].Page);
        Type page=Reader.GetNestedType("DialoguePage",F);var pages=(IList)Get(CurrentReader,"_pages");
        for(int i=0;i<lines.Length;i++)pages.Add(Activator.CreateInstance(page,F,null,new object[]{lines[i].Text,false,"", "",new HbrPortraitLayout(),Array.Empty<HbrStoryCharacter>(),"res://images/fixture/bg.png",i,lines[i]},null));
        Paints=Voices=Cutins=Destroyed=0;LastAngry=false;MissingCutin=false;VoiceGate=null;FadeTasks.Clear();Trace.Clear();return CurrentReader;
    }
    static void Show(int index=0){Set(CurrentReader,"_pageIndex",index);Call(CurrentReader,"ShowLine");}
    static async Task Drain(){await Task.Delay(12);}
    static async Task Tick(double seconds){Call(CurrentClock,"Tick",seconds);await Drain();}
    static bool Busy=>(bool)Get(CurrentReader,"_directionBusy");
    static async Task Cases()
    {
        Configure(Line(new HbrStoryDirection(){Op="whiteOut",Duration=1},new HbrStoryDirection(){Op="wait",Duration=.5},new HbrStoryDirection(){Op="scene"},new HbrStoryDirection(){Op="sepia",Gray=1,Sepia=.5},new HbrStoryDirection(){Op="whiteIn",Duration=3,NoWait=true},new HbrStoryDirection(){Op="wait",Duration=2}));
        Show();Check(Busy&&!Trace.Any(t=>t.StartsWith("scene:")),"actual author-line controller waits before explicit scene");await Tick(1);Check(!Trace.Any(t=>t.StartsWith("scene:")),"first fade completion alone does not bypass wait");await Tick(.5);Check(Trace.Any(t=>t.StartsWith("scene:")),"explicit scene changes at white-covered position");Check(Busy&&Paints==0,"text does not start before blocking directions finish");await Tick(2);Check(!Busy&&Paints==1,"noWait whiteIn overlaps following two-second wait");Check(!FadeTasks[^1].IsCompleted,"three-second nonblocking fade is still running at text onset");
        // A second pagination fragment has the same authored identity and cannot replay.
        var pages=(IList)Get(CurrentReader,"_pages");pages.Add(pages[0]);Show(1);Check(Paints==2&&Trace.Count(t=>t.StartsWith("scene:"))==1,"continuation page does not replay original directions");Check(!FadeTasks[^1].IsCanceled,"same-line pagination retains nonblocking transition");await Tick(1);Check(FadeTasks[^1].IsCompletedSuccessfully,"nonblocking fade finishes after text is already visible");
        Configure(Line(new HbrStoryDirection(){Op="whiteOut",Duration=10},new HbrStoryDirection(){Op="scene"},new HbrStoryDirection(){Op="se",Cue="SY_FIXTURE"},new HbrStoryDirection(){Op="shake"},new HbrStoryDirection(){Op="bgm",Cue="SB_FIXTURE"}));Show();Call(CurrentReader,"ProcessDirections",.01,true,true);await Drain();Check(Paints==1&&!Busy&&FadeAlpha==0,"Ctrl cancels blocked white flash and restores final visible state");Check(!Trace.Contains("audio:Se")&&!Trace.Contains("Shake")&&Trace.Contains("audio:Bgm"),"Ctrl suppresses transient SE/shake but retains BGM state");
        Configure(Line(new HbrStoryDirection(){Op="se",Cue="SY_FIXTURE"},new HbrStoryDirection(){Op="bubbleAngerAsync",Target="A",Text="fixture"}));Show();await Drain();int se=Trace.Count(t=>t=="audio:Se");Set(CurrentReader,"_restoreDirectionRequested",true);Show();await Drain();Check(LastAngry&&Trace.Any(t=>t=="bubble:bubbleAngerAsync:True"),"back navigation restores the correct angry-bubble visual");Check(Trace.Count(t=>t=="audio:Se")==se,"back navigation does not replay sound effects");
        Configure(Line(new HbrStoryDirection(){Op="miniCutIn",Character="RKayamori",Index=1,Voice="VC_FIXTURE"}));VoiceGate=new(TaskCreationOptions.RunContinuationsAsynchronously);Show();Check(Busy&&Cutins==1&&Voices==1,"actual mini controller starts one view and its voice");Call(CurrentReader,"SignalDirectionAdvance");await Drain();Check(Busy&&!Trace.Contains("visible:False"),"mini click alone cannot finish while voice is playing");VoiceGate.TrySetResult(true);await Drain();await Tick(.49);Check(!Trace.Contains("visible:False"),"mini retains original half-second post-voice hold");await Tick(.01);Check(Trace.Contains("visible:False")&&Busy,"mini hides before its separate tail wait");await Tick(.3);Check(!Busy&&Paints==1&&Destroyed==1,"mini completes after .3-second tail with no input fall-through");
        Configure(Line(new HbrStoryDirection(){Op="showBlackTextInMiddle",Text="Fixture time jump"}));Show();await Tick(.5);Check(Busy&&Paints==0,"middle text waits for native-like click after fade in");Call(CurrentReader,"SignalDirectionAdvance");await Drain();await Tick(.5);Check(!Busy&&Paints==1,"middle-text click then fade out advances narration once");
        Configure(Line(new HbrStoryDirection(){Op="wait",Duration=600}));Show();Call(CurrentReader,"DisposeDirections");await Tick(1000);Check(Paints==0,"closing reader cancels a long wait without late text writes");
        Configure(Line(new HbrStoryDirection(){Op="cutIn",Character="RKayamori",Expression="Smile"}));MissingCutin=true;Show();await Drain();Check(!Busy&&Paints==1,"missing cut-in asset skips visual safely without blocking story");
        Configure(Line(new HbrStoryDirection(){Op="whiteOut",Duration=3,NoWait=true}),Line());Show();await Drain();Task fade=FadeTasks[0];Show(1);await Drain();Check(!fade.IsCanceled,"nonblocking transition survives next authored line");await Tick(3);Check(fade.IsCompletedSuccessfully,"cross-line transition eventually completes normally");
        Configure(Line(new HbrStoryDirection{Op="effect",Id="FX",Name="Adventure/LineWork/LineWork"},new HbrStoryDirection{Op="effect",Id="FX",Name="Adventure/LineWork/LineWork",X=120,Layer="BG"}));Show();await Drain();Check(Trace.Count(t=>t=="RetriggerEffect")==2,"each explicit same-id effect command restarts its own visual");
        Configure(Line(new HbrStoryDirection{Op="showStill",Image="res://images/fixture/cg.png",Direction="left",Motion="zoomOut"},new HbrStoryDirection{Op="hideStill",Direction="right",Motion="zoomIn",Offset=320}));Show();await Drain();Check(HiddenStill?.Direction=="right"&&HiddenStill.Motion=="zoomIn"&&HiddenStill.Offset==320,"hideStill consumes explicit exit-direction/motion/offset overrides");
        var alias=new HbrStoryLine{Kind="dialogue",Text="fixture",Speaker="Anonymous girl",Voice="VC_Fixture_RKayamori",Characters=new[]{new HbrStoryCharacter{Id="RUKA",Image="res://images/fixture/actor.png"}}};Configure(alias);Show();await Drain();Check((string)Get(CurrentReader,"_voiceSpeaker")=="RUKA","known voice cue chooses active actor without guessing anonymous speaker text");
        Configure(new HbrStoryLine{Kind="dialogue",Text="fixture",Speaker="Unknown",Voice="VC_Fixture_RKayamori",Characters=new[]{new HbrStoryCharacter{Id="A",Image="res://images/fixture/actor.png",VoiceCharacter="RKayamori"}}});Show();await Drain();Check((string)Get(CurrentReader,"_voiceSpeaker")=="A","explicit voiceCharacter supports arbitrary actor IDs without name guessing");
        Configure(new HbrStoryLine{Kind="dialogue",Text="fixture",Voice="VC_FIXTURE"});Show();await Drain();int voice=Voices;Set(CurrentReader,"_restoreDirectionRequested",true);Show();await Drain();Check(Voices==voice&&voice==1,"author voice plays once and stays silent on backward restoration");
    }
    static void CameraCases()
    {
        MethodInfo camera=Stage.GetMethod("Camera",F);Harmony.Unpatch(camera,HarmonyPatchType.Prefix,Harmony.Id);
        Patch(Harmony,Stage,"LayoutBackground",nameof(Block));
        var stage=Blank(Stage);var clock=Activator.CreateInstance(Clock,true);Set(stage,"_clock",clock);Set(stage,"_cameraZoom",1f);Set(stage,"_cameraTargetZoom",1f);
        Call(stage,"Camera",0f,0f,1.1f,3f,false);Call(stage,"Camera",0f,0f,1f,0f,false);Call(clock,"Tick",3d);
        Check((float)Get(stage,"_cameraZoom")==1f,"actual Camera resets a pending zoom even before its first frame");
        Call(stage,"Camera",0f,0f,1.1f,3f,false);Call(clock,"Tick",1d);float zoom=(float)Get(stage,"_cameraZoom");Call(stage,"Camera",0f,0f,1.1f,3f,false);Call(clock,"Tick",2d);
        Check(zoom>1f&&(float)Get(stage,"_cameraZoom")==1.1f,"same pending camera endpoint does not restart its elapsed duration");
        Call(stage,"Camera",0f,0f,1.2f,3f,false);Call(clock,"Tick",1d);Call(stage,"Camera",0f,0f,1.2f,0f,true);Call(clock,"Tick",2d);
        Check((float)Get(stage,"_cameraZoom")==1.2f,"explicit zero-duration camera command snaps even to the same pending endpoint");
        Patch(Harmony,Stage,"Camera",nameof(VoidStage));
    }
    static void FilterCases()
    {
        MethodInfo filters=Stage.GetMethod("Filters",F);Harmony.Unpatch(filters,HarmonyPatchType.Prefix,Harmony.Id);Patch(Harmony,Stage,"ApplyMaterials",nameof(Block));
        var stage=Blank(Stage);var clock=Activator.CreateInstance(Clock,true);Set(stage,"_clock",clock);Set(stage,"_filterTargets",new float[7]);var sepia=new Color(1.07f,.74f,.43f);Set(stage,"_sepiaColor",sepia);Set(stage,"_targetSepiaColor",sepia);
        void Filter(float gray,float brown,float vignette,float time,string channel)=>Call(stage,"Filters",gray,brown,sepia,vignette,0f,0f,0f,0f,time,channel);
        Filter(0,0,.7f,3,"vignette");Call(clock,"Tick",1d);Filter(1,.5f,.7f,3,"sepia");Call(clock,"Tick",2d);
        Check(Math.Abs((float)Get(stage,"_vignette")-.7f)<.0001,"actual renderer preserves independent vignette duration when sepia starts later");
        Check(Math.Abs((float)Get(stage,"_gray")-2f/3)<.0001,"new sepia channel retains its own three-second timeline");
        Filter(1,.5f,.1f,3,"vignette");Call(clock,"Tick",1d);Filter(1,.5f,.1f,0,"vignette");Call(clock,"Tick",5d);
        Check(Math.Abs((float)Get(stage,"_vignette")-.1f)<.0001,"zero-duration filter override cancels its stale animation callback");Patch(Harmony,Stage,"Filters",nameof(VoidStage));
    }
    static bool ReadMaterial(object material,float blur){MaterialBlur=blur;return false;}
    static void ChoiceBlurCases()
    {
        Check(Stage.GetMethod("ChoiceBlur",F)!=null,"actual new stage exposes legacy choice-background blur bridge");
        Patch(Harmony,Stage,"ApplyMaterial",nameof(ReadMaterial));var stage=Blank(Stage);
        Call(stage,"ChoiceBlur",true);Check(MaterialBlur==8f,"legacy choice presentation still blurs the visible background by8");
        Set(stage,"_backgroundBlur",2f);Call(stage,"ChoiceBlur",false);Check(MaterialBlur==2f,"leaving choices restores authored persistent blur rather than clearing it");
    }
    static bool RecordSceneActor(float fade,float alphaDuration){ScenePoseSeconds=fade;SceneAlphaSeconds=alphaDuration;return false;}
    static void FreeGhost(Node node){GhostFreed++;}
    static void ShowGhost(CanvasItem node){GhostShown++;}
    static IEnumerable<CodeInstruction> GhostLeaves(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var instruction in instructions)
        {
            if(instruction.operand is MethodInfo method&&method.Name is "QueueFree" or "Show")
            {instruction.opcode=System.Reflection.Emit.OpCodes.Call;instruction.operand=typeof(DirectionChecks).GetMethod(method.Name=="QueueFree"?nameof(FreeGhost):nameof(ShowGhost),F);}
            yield return instruction;
        }
    }
    static void LegacySceneCases()
    {
        var stage=Blank(Stage);Set(stage,"_actors",Activator.CreateInstance(Stage.GetField("_actors",F).FieldType));
        MethodInfo scene=Stage.GetMethod("Scene",F);Harmony.Unpatch(scene,HarmonyPatchType.Prefix,Harmony.Id);Patch(Harmony,Stage,"SetActor",nameof(RecordSceneActor));
        Type frame=Stage.GetNestedType("ActorFrame",F);var frames=Array.CreateInstance(frame,1);
        frames.SetValue(Activator.CreateInstance(frame,F,null,new object[]{"A","res://images/fixture/actor.png",900f,900f,800f,false,1f,1f,true,0f,true,true,0d},null),0);
        Call(stage,"Scene","res://images/fixture/bg.png",frames,.3f,.3f,false,0f);
        Check(ScenePoseSeconds==0&&SceneAlphaSeconds==.3f,"legacy scene pose changes remain immediate while character opacity alone gets default fade");
        Patch(Harmony,Stage,"Scene",nameof(VoidStage));
        MethodInfo cross=Stage.GetMethod("CrossFade",F);Harmony.Unpatch(cross,HarmonyPatchType.Prefix,Harmony.Id);
        Harmony.Patch(cross,transpiler:new HarmonyMethod(typeof(DirectionChecks).GetMethod(nameof(GhostLeaves),F)));
        Set(stage,"_heldBackground",Blank(typeof(TextureRect)));var ghosts=(IList)Activator.CreateInstance(Stage.GetField("_ghosts",F).FieldType);Set(stage,"_ghosts",ghosts);
        var ghost=Blank(Stage.GetNestedType("Ghost",F));Set(ghost,"Node",Blank(typeof(Control)));ghosts.Add(ghost);GhostFreed=GhostShown=0;
        Call(stage,"CrossFade",0f);
        Check(GhostFreed==2&&GhostShown==0&&ghosts.Count==0&&Get(stage,"_heldBackground")==null,"zero-duration crossFade releases old images immediately without showing a one-frame ghost");
        Patch(Harmony,Stage,"CrossFade",nameof(VoidStage));
    }
    static void FontResourceCases()
    {
        Type visual=Mod.GetType("HBRGOSakiMod.Core.Nodes.Events.Story.NLibraryStoryVisualResources",true),resources=Mod.GetType("HBRGOSakiMod.Core.Nodes.Events.Story.NLibraryStoryRuntimeResources",true);
        FieldInfo read=visual.GetField("_read",F),manifest=visual.GetField("_manifest",F);object previousRead=read.GetValue(null),previousManifest=manifest.GetValue(null);
        using var valid=JsonDocument.Parse("{\"fonts\":{\"dialogue\":{\"path\":\"res://fonts/story/fixture.ttf\"},\"invalid\":{\"path\":\"res://fonts/story/../private.ttf\"}}}");
        try
        {
            read.SetValue(null,true);manifest.SetValue(null,valid.RootElement.Clone());
            Check((string)Static(visual,"FontPath","dialogue")=="res://fonts/story/fixture.ttf"&&Static(visual,"FontPath","invalid")==null,"optional new-dialogue font resolution confines resources to approved story TTF paths");
            MethodInfo collect=resources.GetMethods(F).Single(m=>m.Name=="Collect"&&m.GetParameters()[0].ParameterType==typeof(HbrStoryPresentation));
            string[] Paths(HbrStoryLine line)=>((IEnumerable<string>)Invoke(collect,null,new HbrStoryPresentation{Background="res://images/fixture/bg.png"},new[]{line},null)).ToArray();
            Check(!Paths(Line()).Any(p=>p.EndsWith(".ttf"))&&Paths(Line(new HbrStoryDirection{Op="bubbleTalkAsync",Target="A",Text="fixture"})).Contains("res://fonts/story/fixture.ttf"),"Novel font preloading is limited to segments using new dialogue-bubble or cut-in operations");
        }
        finally{read.SetValue(null,previousRead);manifest.SetValue(null,previousManifest);}
    }
    static void Contracts()
    {
        using var module=ModuleDefinition.ReadModule(Mod.Location);var reader=module.GetType(Reader.FullName);
        IEnumerable<TypeDefinition> All(TypeDefinition type)=>new[]{type}.Concat(type.NestedTypes.SelectMany(All));
        var calls=All(reader).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>();
        var owned=calls.Where(m=>m.DeclaringType.FullName.Contains("StoryDirection")||m.DeclaringType.FullName.Contains("StoryAudio"));
        var choice=reader.Methods.SingleOrDefault(m=>m.Name=="SetChoicePresentation");
        if(choice!=null)Check(choice.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="ChoiceBlur"),"actual reader choice-presentation callsite reaches the visible stage blur");
        Check(owned.Any(),"actual compiled reader includes direction and audio integrations");
        var directionMethods=reader.Methods.Where(m=>m.Name is "ExecuteDirection" or "ApplySnapshot" or "ShowDirectedLine" or "ProcessDirections").ToArray();
        foreach(var method in directionMethods)
        {
            var nested=reader.NestedTypes.FirstOrDefault(t=>t.Name.StartsWith("<"+method.Name+">"));
            foreach(var body in new[]{method}.Concat(nested?.Methods.AsEnumerable()??Enumerable.Empty<MethodDefinition>()).Where(m=>m.HasBody))
                Check(!body.Body.Instructions.Any(i=>i.Operand is MethodReference m&&(m.DeclaringType.Name.Contains("EventSynchronizer")||m.DeclaringType.Name=="NOverlayStack"||m.Name=="SetCurrentRoom")),"local controller method has no synchronized room/overlay action: "+body.FullName);
        }
    }
    public static void Run(string path)=>RunAsync(path).GetAwaiter().GetResult();
    static async Task RunAsync(string path)
    {
        string error=null;try{Install();await Cases();CameraCases();FilterCases();ChoiceBlurCases();LegacySceneCases();FontResourceCases();Contracts();}catch(Exception e){error=e.ToString();Console.Error.WriteLine(error);}
        var report=new{ok=error==null,sourceLinked=Mod==typeof(DirectionChecks).Assembly,productionMainBinding=Mod!=typeof(DirectionChecks).Assembly,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual compiled reader ShowLine/PrepareDirectionPage/ExecuteDirection/ApplySnapshot/seek/input/cancellation tasks with real pure clock and reducer. Godot stage mutations, UI factories and audio terminals are explicitly controlled leaves. No engine, rendered shader, playback/listening, real input signals or multiplayer session claimed."};Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count);if(error!=null)throw new Exception("See "+path);
    }
}
