using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Events.Story;
using HBRGOSakiMod.Core.Story;

namespace HBRGOSakiMod.Core.Nodes.Events;

public partial class NLibraryEvent
{
    private NLibraryStoryStage _directionStage;
    private HbrStoryAudioSession _storyAudio;
    private readonly List<HbrStoryDirectionPlan> _directionPlans=new();
    private readonly HashSet<int> _visitedDirectionLines=new();
    private readonly List<Control> _directionTransient=new();
    private HbrStorySceneState _logicalScene=HbrStorySceneState.Empty;
    private CancellationTokenSource _directionCancellation;
    private TaskCompletionSource<bool> _directionAdvance;
    private int _directionVersion,_activeSourceLine=-1;
    private bool _directionBusy,_restoreDirectionRequested,_directionFast,_closingStoryAudio;
    private float _nextCrossFade=-1;
    private bool _sceneAppliedThisLine;
    private string _voiceSpeaker="";

    private void BuildDirectionUi()
    {
        _directionStage=new NLibraryStoryStage{Name="StoryDirectionStage"};
        AddChild(_directionStage);MoveChild(_directionStage,_stageCharacters.GetIndex());_directionStage.Build();
        _background.Hide();_stageCharacters.Hide();_directionStage.Background(StoryBackgroundPath);
        _storyAudio=new HbrStoryAudioSession(this);
    }
    private void ResizeDirections()
    {if(_directionStage==null)return;_directionStage.Position=_canvas.Position;_directionStage.Scale=_canvas.Scale;_directionStage.SetViewport(new Rect2(-_canvas.Position/_canvas.Scale,Size/_canvas.Scale));}
    private void PrepareDirectionPage(HbrStoryPage page)
    {
        CancelDirectionLine();_directionStage?.CancelMotion(true);ClearDirectionTransient();
        _directionPlans.Clear();_visitedDirectionLines.Clear();_activeSourceLine=-1;
        HbrStorySceneState state=_logicalScene;
        for(int i=0;i<page.Lines.Count;i++)
        {
            HbrStoryLine line=page.Lines[i];
            var scene=new HbrStoryResolvedScene(page.ResolveBackground(ReaderPresentation,i),page.ResolveCharacters(i),i==0||!string.IsNullOrEmpty(line.Background),i==0||line.Characters!=null);
            var plan=HbrStoryDirectionReducer.Plan(state,scene,line,ReaderPresentation);
            _directionPlans.Add(plan);state=plan.After;
        }
    }
    private void ShowLine()
    {
        DialoguePage page=_pages[_pageIndex];int source=page.SourceLineIndex;
        if(_directionStage==null||source<0||source>=_directionPlans.Count)
        {
            _directionStage?.Scene(page.Background,Frames(page.Characters),(float)(ReaderPresentation?.CharacterFadeDuration??.3),(float)(ReaderPresentation?.ExpressionFadeDuration??.3));
            PaintLine(page);return;
        }
        bool restore=_restoreDirectionRequested||_visitedDirectionLines.Contains(source)&&_activeSourceLine!=source;
        _restoreDirectionRequested=false;
        if(_activeSourceLine==source&&!restore){PaintLine(page);return;}
        CancelDirectionLine();ClearDirectionTransient();_sceneAppliedThisLine=false;_nextCrossFade=-1;_activeSourceLine=source;_visitedDirectionLines.Add(source);
        _directionCancellation=new CancellationTokenSource();int version=++_directionVersion;
        _directionBusy=true;SetDirectionBlock();
        ObserveDirection(ShowDirectedLine(page,source,restore,version,_directionCancellation.Token));
    }
    private void SetDirectionBlock(){_advanceButton.Disabled=false;_dialogue.Text="";_continueMarker.Hide();}
    private async Task ShowDirectedLine(DialoguePage page,int source,bool restore,int version,CancellationToken token)
    {
        try
        {
            HbrStoryDirectionPlan plan=_directionPlans[source];
            if(restore||_directionFast)
            {
                _directionStage.CancelMotion(true);
                ApplySnapshot(SeekState(plan.After),null,0,true);_logicalScene=plan.After;
                if(restore)foreach(var step in plan.Steps.Where(s=>s.Direction.Op is "bubbleTalkAsync" or "bubbleAngerAsync"))Bubble(step.Direction,step.Direction.Op=="bubbleAngerAsync",true);
            }
            else
            {
                foreach(var step in plan.Steps)
                {
                    token.ThrowIfCancellationRequested();
                    await ExecuteDirection(step,token);
                }
                _logicalScene=plan.After;
            }
            if(version!=_directionVersion||token.IsCancellationRequested)return;
            _directionBusy=false;PaintLine(page);
            if(!restore&&!_directionFast&&!string.IsNullOrEmpty(page.SourceLine?.Voice))
            {_voiceSpeaker=ResolveVoiceSpeaker(page.SourceLine.Voice,page.Speaker);ObserveDirection(_storyAudio.Voice(page.SourceLine.Voice,page.Thought,token));}
        }
        catch(OperationCanceledException){}
        catch(Exception error)
        {
            GD.PushWarning("[HBR story] Direction failed safely: "+error.Message);
            if(version==_directionVersion&&!token.IsCancellationRequested)
            {
                try{ApplySnapshot(SeekState(_directionPlans[source].After),null,0,true);_logicalScene=_directionPlans[source].After;}
                catch(Exception visualError){GD.PushWarning("[HBR story] Incomplete visual state skipped: "+visualError.Message);}
                finally{_directionBusy=false;PaintLine(page);}
            }
        }
    }
    private async Task ExecuteDirection(HbrStoryDirectionStep step,CancellationToken token)
    {
        HbrStoryDirection d=step.Direction;string op=d.Op;float duration=(float)step.Duration;
        float N(string key,float fallback=0)=>(float)d.GetNumber(key,fallback);
        switch(op)
        {
            case "wait":await _directionStage.Clock.Wait(duration,token);return;
            case "fadeOut":case "fadeIn":case "whiteOut":case "whiteIn":
                var fade=_directionStage.Fade(op.StartsWith("white",StringComparison.Ordinal)?Colors.White:Colors.Black,op.EndsWith("Out",StringComparison.Ordinal)?1:0,duration,d.GetBool("noWait")?CancellationToken.None:token);
                if(d.GetBool("noWait"))ObserveDirection(fade);else await fade;return;
            case "crossFade":_directionStage.CrossFade(duration);if(!_sceneAppliedThisLine)_nextCrossFade=duration;return;
            case "shake":_directionStage.Shake(d.GetText("target"),N("amplitude"),N("amplitude"),duration);return;
            case "shakeXY":_directionStage.Shake(d.GetText("target"),N("amplitudeX"),N("amplitudeY"),duration);return;
            case "stopShake":_directionStage.StopShake(d.GetText("target","screen"));return;
            case "zoomOut":
                _directionStage.Camera((float)(HbrStoryDirectionReducer.BackgroundOffset(step.After.Camera.Position)+step.After.Camera.Offset),0,N("scale",1.1f),0,true);
                _directionStage.Camera((float)(HbrStoryDirectionReducer.BackgroundOffset(step.After.Camera.Position)+step.After.Camera.Offset),0,(float)step.After.Camera.Zoom,duration,true);return;
            case "bgm":_storyAudio.Bgm(d.Cue,N("fadeOut",.5f),N("fadeIn",.5f),false,N("volume",1));return;
            case "bgmStop":_storyAudio.BgmStop(N("fadeOut",.5f));return;
            case "env":_storyAudio.Env(d.Cue,N("fadeIn",.5f),false,N("volume",1));return;
            case "envStop":_storyAudio.EnvStop(N("fadeOut",.5f));return;
            case "se":_storyAudio.Se(d.Cue,N("volume",1));return;
            case "cutIn":await CutIn(d,false,token);return;
            case "miniCutIn":await CutIn(d,true,token);return;
            case "showBlackTextInMiddle":await BlackText(d,token);return;
            case "bubbleSeActNoWait":Bubble(d,false,false,step.Before);if(!string.IsNullOrEmpty(d.Cue))_storyAudio.Se(d.Cue);return;
            case "bubbleTalkAsync":case "bubbleAngerAsync":Bubble(d,op=="bubbleAngerAsync",false,step.Before);_voiceSpeaker=d.Target;if(!string.IsNullOrEmpty(d.Voice))ObserveDirection(_storyAudio.Voice(d.Voice,false,token));return;
            case "zoomShakeWithEffect":
                _directionStage.Shake("background",.05f,.05f,1);_directionStage.Shake(d.Target,.1f,.1f,.5);
                ApplySnapshot(step.After,step.Before,.2f,false);
                var lineWork=step.After.Effects.Values.FirstOrDefault(e=>e.Target==d.Target&&e.Name=="Adventure/LineWork/LineWork");if(lineWork!=null)_directionStage.RetriggerEffect(lineWork.Id);return;
            case "effect":case "showEffectLineWork":
                ApplySnapshot(step.After,step.Before,duration,false);_directionStage.RetriggerEffect(d.GetText("id"));return;
            case "showStill":ApplyStill(step.After.Still,null,duration,false);return;
            case "hideStill":
                var previousStill=step.Before.Still;
                if(previousStill!=null)previousStill=previousStill with{Direction=d.Direction??previousStill.Direction,Motion=d.Motion??previousStill.Motion,Offset=d.Offset??previousStill.Offset};
                _directionStage.HideStill(previousStill,duration);return;
            case "blur":
                if(d.GetText("scope")=="character")_directionStage.Actors(Frames(step.After.Actors),duration,1,1,-1,"blur");
                else ApplySnapshot(step.After,step.Before,duration,false,d.GetText("scope")=="foreground"?"foregroundBlur":"backgroundBlur");return;
            case "appear":
                // Place the transparent blurred entry image at its original offset,
                // then converge to the reducer's stable endpoint.
                var entry=step.After.Actors.Select(a=>a.Id==d.Target?a with {X=a.X+N("x")*5/6,Y=a.Y-N("y")*5/6,Opacity=0,Blur=N("blur")}:a).ToArray();
                _directionStage.Actors(Frames(entry),0,1,1,-1,"all");ApplySnapshot(step.After,step.Before,duration,false);return;
            case "moveBy":case "moveTo":_directionStage.Actors(Frames(step.After.Actors),duration,N("easeIn",1),N("easeOut",1),-1,"move");return;
            case "swelling":_directionStage.Actors(Frames(step.After.Actors),duration,1,1,N("disappearDuration",duration),"all");return;
            default:ApplySnapshot(step.After,step.Before,duration,false,op);return;
        }
    }
    private void ApplySnapshot(HbrStorySceneState state,HbrStorySceneState before,float duration,bool instant,string op="")
    {
        float time=instant?0:duration;
        bool scene=op=="scene"||before==null;
        if(scene)
        {
            _sceneAppliedThisLine=true;
            float fade=instant?0:_nextCrossFade>=0?_nextCrossFade:(float)(ReaderPresentation?.CharacterFadeDuration??.3);
            _directionStage.Scene(state.Background,Frames(state.Actors),fade,instant?0:_nextCrossFade>=0?_nextCrossFade:(float)(ReaderPresentation?.ExpressionFadeDuration??.3),instant,_nextCrossFade>=0?_nextCrossFade:0);_nextCrossFade=-1;
        }
        else if(!ReferenceEquals(state.Actors,before.Actors))_directionStage.Actors(Frames(state.Actors),time,1,1,-1,op is "show" or "hide" or "alpha" or "scale"?op:"");
        bool cameraCommand=op is "stageFixBg" or "zoomIn" or "zoomOut" or "panL" or "panR" or "stagePan";
        if(before==null||state.Camera!=before.Camera||cameraCommand)_directionStage.Camera((float)(HbrStoryDirectionReducer.BackgroundOffset(state.Camera.Position)+state.Camera.Offset),0,(float)state.Camera.Zoom,time,cameraCommand);
        if(before==null||state.Filters!=before.Filters||op is "sepia" or "vignette" or "chromaticAberration" or "backgroundBlur" or "foregroundBlur"||state.Effects.Values.Any(e=>e.Name.Contains("InvertColor",StringComparison.Ordinal))!=before.Effects.Values.Any(e=>e.Name.Contains("InvertColor",StringComparison.Ordinal)))
        {
            var f=state.Filters;var c=f.Color??new HbrStoryRgb(1.07,.74,.43);
            _directionStage.Filters((float)f.Gray,(float)f.Sepia,new Color((float)c.R,(float)c.G,(float)c.B),(float)f.Vignette,(float)f.ChromaticAberration,(float)f.BackgroundBlur,(float)f.ForegroundBlur,state.Effects.Values.Any(e=>e.Name.Contains("InvertColor",StringComparison.Ordinal))?1:0,time,op);
        }
        _directionStage.KeepEffects(state.Effects.Keys);
        foreach(var effect in state.Effects.Values)
        {
            if(effect.Name.Contains("InvertColor",StringComparison.Ordinal))continue;
            float x=(float)effect.X,y=(float)effect.Y;
            if(effect.Target!="screen") {var actor=state.Actors.FirstOrDefault(a=>a.Id==effect.Target);if(actor!=null){x+=(float)actor.X-800;y+=(float)actor.Y-450;}}
            _directionStage.Effect(effect.Id,effect.Name,effect.Layer,x,y,(float)effect.Scale,effect.Target);
        }
        if(before==null||state.Still!=before.Still)ApplyStill(state.Still,before?.Still,time,instant);
        if(before==null||state.Bgm!=before.Bgm)
        {if(state.Bgm==null)_storyAudio.BgmStop(instant?0:.5f,instant);else _storyAudio.Bgm(state.Bgm.Cue,.5f,.5f,instant,(float)state.Bgm.Volume);}
        if(before==null||state.Env!=before.Env)
        {if(state.Env==null)_storyAudio.EnvStop(instant?0:.5f,instant);else _storyAudio.Env(state.Env.Cue,.5f,instant,(float)state.Env.Volume);}
        if(instant)ObserveDirection(_directionStage.Fade(state.Fade.Color=="white"?Colors.White:Colors.Black,(float)state.Fade.Opacity,0,CancellationToken.None));
    }
    private static IReadOnlyList<NLibraryStoryStage.ActorFrame> Frames(IReadOnlyList<HbrStoryCharacter> actors)=>actors.Select(a=>new NLibraryStoryStage.ActorFrame(a.Id,a.Image,a.X,a.Y,a.Height,a.FlipX,a.Opacity,1,true,0,true,true)).ToArray();
    private static IReadOnlyList<NLibraryStoryStage.ActorFrame> Frames(IReadOnlyList<HbrStoryActorState> actors)=>actors.Select(a=>new NLibraryStoryStage.ActorFrame(a.Id,ExpressionImage(a), (float)a.X,(float)a.Y,(float)a.Height,a.FlipX,(float)a.Opacity,(float)a.Scale,a.Visible,(float)a.Blur,a.BlinkEnabled,a.LipSyncEnabled,a.Z)).ToArray();
    private static string ExpressionImage(HbrStoryActorState actor)
    {
        if(string.IsNullOrEmpty(actor.Expression))return actor.Image;
        int index=actor.Image.LastIndexOf("Adv",StringComparison.Ordinal);if(index<0)return actor.Image;
        string path=actor.Image[..(index+3)]+actor.Expression+System.IO.Path.GetExtension(actor.Image);
        return ResourceLoader.Exists(path)?path:actor.Image;
    }
    private string ResolveVoiceSpeaker(string cue,string fallback)
    {
        int marker=cue.LastIndexOf('_');if(marker<0)return fallback;
        string code=cue[(marker+1)..];string stable=NLibraryStoryVisualResources.Resolve("characterIds",code);
        var state=_activeSourceLine>=0&&_activeSourceLine<_directionPlans.Count?_directionPlans[_activeSourceLine].After:_logicalScene;
        bool Matches(HbrStoryActorState actor)
        {
            if(!string.IsNullOrEmpty(actor.VoiceCharacter))
            {
                string identity=NLibraryStoryVisualResources.Resolve("characterIds",actor.VoiceCharacter)??actor.VoiceCharacter;
                return string.Equals(identity,stable??code,StringComparison.OrdinalIgnoreCase)||string.Equals(actor.VoiceCharacter,code,StringComparison.OrdinalIgnoreCase);
            }
            if(string.IsNullOrEmpty(stable))return false;
            return string.Equals(actor.Id,stable,StringComparison.OrdinalIgnoreCase)||string.Equals(actor.Id,code,StringComparison.OrdinalIgnoreCase)||
                stable=="tezuka_saki"&&actor.Id=="TEZUKA"||System.IO.Path.GetFileNameWithoutExtension(actor.Image).StartsWith(code,StringComparison.Ordinal);
        }
        var matches=state.Actors.Where(Matches).Take(2).ToArray();
        return matches.Length==1?matches[0].Id:stable==null?fallback:string.Empty;
    }
    private void ApplyStill(HbrStoryStillState still,HbrStoryStillState previous,float duration,bool instant)
    {
        if(still==null){_directionStage.HideStill(previous,duration);return;}
        Vector2 from=still.Direction switch{"left"=>new(-(float)still.Offset*5/6,0),"right"=>new((float)still.Offset*5/6,0),"up"=>new(0,-(float)still.Offset*5/6),"down"=>new(0,(float)still.Offset*5/6),_=>Vector2.Zero};
        if(!instant&&previous==null)_directionStage.Still(still.Image,from.X,from.Y,still.Motion=="zoomOut"?1.1f:1,0);
        _directionStage.Still(still.Image,0,0,(float)still.Scale,duration);
    }
    private Control Bubble(HbrStoryDirection d,bool angry,bool restoring,HbrStorySceneState snapshot=null)
    {
        string text=d.GetText("text");Vector2 point=new(800,300);
        var state=snapshot??(_activeSourceLine>=0&&_activeSourceLine<_directionPlans.Count?_directionPlans[_activeSourceLine].After:_logicalScene);
        var actor=state.Actors.FirstOrDefault(a=>a.Id==d.Target);
        if(actor!=null)point=new Vector2((float)actor.X,(float)(actor.Y-actor.Height*.75));
        point+=new Vector2((float)d.GetNumber("x")*5/6,-(float)d.GetNumber("y")*5/6)-new Vector2(210,70);
        if(d.Op=="bubbleSeActNoWait")
        {
            var word=MakeLabel(text,58);word.Position=point;word.Scale=Vector2.One*(float)d.GetNumber("scale",.7);word.AddThemeColorOverride("font_color",Colors.White);word.AddThemeColorOverride("font_outline_color",Colors.Black);word.AddThemeConstantOverride("outline_size",5);_directionStage.Overlay.AddChild(word);_directionTransient.Add(word);return word;
        }
        string asset=NLibraryStoryVisualResources.Image(angry?"bubbleAngry":"bubbleTalk");
        Texture2D bubble=NLibraryStoryVisualResources.Texture(asset);
        if(bubble==null){WarnMissingDirectionAsset(asset??(angry?"bubbleAngry":"bubbleTalk"));return null;}
        var root=new Control{Position=point,Size=new Vector2(420,140),MouseFilter=MouseFilterEnum.Ignore};
        Control backdrop=NLibraryStoryVisualResources.Bubble(angry,root.Size);root.AddChild(backdrop);
        Texture2D nozzle=NLibraryStoryVisualResources.Texture(NLibraryStoryVisualResources.Image(angry?"bubbleNozzleAngry":"bubbleNozzle"));
        if(nozzle!=null)root.AddChild(new TextureRect{Texture=nozzle,Position=new Vector2(40,125),Size=new Vector2(45,38),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore});
        var label=MakeLabel(text,32);if(NLibraryStoryVisualResources.Font("dialogue") is {} dialogueFont)label.AddThemeFontOverride("font",dialogueFont);label.Position=new Vector2(50,30);label.Size=new Vector2(320,80);label.AddThemeColorOverride("font_color",Colors.Black);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;root.AddChild(label);

        _directionStage.Overlay.AddChild(root);_directionTransient.Add(root);
        if(actor!=null)_directionStage.AttachOverlayToActor(root,actor.Id,new Vector2((float)d.GetNumber("x")*5/6-210,-(float)(actor.Height*.75)-(float)d.GetNumber("y")*5/6-70));
        return root;
    }
    private async Task CutIn(HbrStoryDirection d,bool mini,CancellationToken token)
    {
        Control root=CreateCutInView(d,mini);if(root==null)return;
        _voiceSpeaker=string.Empty;
        Task voice=string.IsNullOrEmpty(d.Voice)?Task.CompletedTask:_storyAudio.Voice(d.Voice,false,token);
        if(mini){_directionAdvance=new(TaskCreationOptions.RunContinuationsAsynchronously);await Task.WhenAll(voice,_directionAdvance.Task.WaitAsync(token));_directionAdvance=null;await _directionStage.Clock.Wait(.5,token);}
        else await _directionStage.Clock.Wait(d.GetNumber("duration",2.5),token);
        SetTransientVisible(root,false);await _directionStage.Clock.Wait(.3,token);DestroyTransient(root);_directionTransient.Remove(root);
    }
    private Control CreateCutInView(HbrStoryDirection d,bool mini)
    {
        string image=CutInImage(d,mini);Texture2D cutin=NLibraryStoryVisualResources.Texture(image);if(cutin==null) {WarnMissingDirectionAsset(image??d.Character);return null;}
        var root=new Control{Size=new Vector2(1600,900),MouseFilter=MouseFilterEnum.Ignore};_directionStage.Overlay.AddChild(root);_directionTransient.Add(root);
        var portrait=new TextureRect{Texture=cutin,Position=new Vector2(mini?170:110+(float)d.GetNumber("offset")*5/6,mini?270:40),Size=mini?new Vector2(480,430):new Vector2(650,820),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};root.AddChild(portrait);
        Texture2D frame=NLibraryStoryVisualResources.Texture(NLibraryStoryVisualResources.Image("cutinFrame"));
        if(frame!=null&&!mini&&!(string.IsNullOrEmpty(d.Image)&&NLibraryStoryVisualResources.Flag("cutinsFramed")))root.AddChild(new TextureRect{Texture=frame,Position=portrait.Position,Size=portrait.Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore});
        var panel=new Control{Position=new Vector2(mini?600:710,mini?380:390),Size=new Vector2(750,200),MouseFilter=MouseFilterEnum.Ignore};root.AddChild(panel);
        var bubble=NLibraryStoryVisualResources.Texture(NLibraryStoryVisualResources.Image(mini&&d.GetText("bubble")=="Shout"?"bubbleAngry":"bubbleTalk"));
        if(bubble!=null)panel.AddChild(NLibraryStoryVisualResources.Bubble(mini&&d.GetText("bubble")=="Shout",panel.Size));
        var text=MakeLabel(d.Text??"",42);if(NLibraryStoryVisualResources.Font("dialogue") is {} dialogueFont)text.AddThemeFontOverride("font",dialogueFont);text.Size=new Vector2(630,130);text.Position=new Vector2(60,35);text.AddThemeColorOverride("font_color",Colors.Black);text.AutowrapMode=TextServer.AutowrapMode.WordSmart;panel.AddChild(text);
        return root;
    }
    private static readonly HashSet<string> MissingDirectionAssets=new(StringComparer.Ordinal);
    private static void WarnMissingDirectionAsset(string path){if(MissingDirectionAssets.Add(path??""))GD.PushWarning("[HBR story] Missing direction asset: "+path);}
    private static string CutInImage(HbrStoryDirection d,bool mini)
    {
        if(!string.IsNullOrEmpty(d.Image))return d.Image;
        string mapped=NLibraryStoryVisualResources.Resolve(mini?"miniCutins":"cutins",mini?$"{d.Character}/{(int)d.GetNumber("index"):00}":$"{d.Character}/{d.GetText("outfit","Default")}/{d.Expression}");
        if(!string.IsNullOrEmpty(mapped))return mapped;
        if(mini)return $"res://images/story/mini_cutin/{d.Character}MiniCutIn{(int)d.GetNumber("index"):00}.png";
        string file=$"{d.Character}{d.GetText("outfit","Default")}AdvCutIn{d.Expression}.png";
        string a="res://images/ui/thirty_one_a_cutin/"+file;if(ResourceLoader.Exists(a))return a;
        return "res://images/ui/thirty_g_proposal/cutin/"+file;
    }
    private async Task BlackText(HbrStoryDirection d,CancellationToken token)
    {
        Control black=CreateBlackTextView(d);
        await _directionStage.Clock.Animate("middletext",d.GetNumber("fadeIn",.5),t=>SetTransientOpacity(black,t),token);
        _directionAdvance=new(TaskCreationOptions.RunContinuationsAsynchronously);await _directionAdvance.Task.WaitAsync(token);_directionAdvance=null;
        await _directionStage.Clock.Animate("middletext",d.GetNumber("fadeOut",.5),t=>SetTransientOpacity(black,1-t),token);DestroyTransient(black);_directionTransient.Remove(black);
    }
    private Control CreateBlackTextView(HbrStoryDirection d)
    {
        var black=new ColorRect{Color=Colors.Black,Position=_directionStage.ViewportRect.Position,Size=_directionStage.ViewportRect.Size,MouseFilter=MouseFilterEnum.Ignore};var label=MakeLabel(d.Text??"",44);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;label.Size=black.Size;black.AddChild(label);_directionStage.Overlay.AddChild(black);_directionTransient.Add(black);
        return black;
    }
    private static void SetTransientOpacity(Control node,float alpha)=>node.Modulate=new Color(1,1,1,alpha);
    private static void SetTransientVisible(Control node,bool visible)=>node.Visible=visible;
    private void DestroyTransient(Control node){_directionStage?.DetachOverlay(node);if(GodotObject.IsInstanceValid(node))node.QueueFree();}
    private bool SignalDirectionAdvance(){if(!_directionBusy)return false;_directionAdvance?.TrySetResult(true);return true;}
    private void ProcessDirections(double delta,bool active,bool fast)
    {
        if(_directionStage==null)return;
        _storyAudio.SetCovered(!active);_directionStage.Covered=!active;
        if(active){if(!_closingStoryAudio)_storyAudio.Process(delta);_directionStage.Tick(delta,_voiceSpeaker,_storyAudio.VoiceAmplitude,_storyAudio.IsVoicePlaying);}
        if(fast&&!_directionFast)
        {
            _directionFast=true;_storyAudio.StopVoice();ClearDirectionTransient();
            if(_activeSourceLine>=0&&_activeSourceLine<_directionPlans.Count)
            {
                CancelDirectionLine();_directionStage.CancelMotion(true);ApplySnapshot(SeekState(_directionPlans[_activeSourceLine].After),null,0,true);_logicalScene=_directionPlans[_activeSourceLine].After;_directionBusy=false;if(_pages.Count>0)PaintLine(_pages[_pageIndex]);
            }
        }
        else if(!fast)_directionFast=false;
    }
    private static HbrStorySceneState SeekState(HbrStorySceneState state)=>state.Fade.Color=="white"?state with{Fade=new HbrStoryFadeState("white",0)}:state;
    private void CancelDirectionLine(){_directionVersion++;_directionCancellation?.Cancel();_directionCancellation?.Dispose();_directionCancellation=null;_directionAdvance?.TrySetCanceled();_directionAdvance=null;_storyAudio?.StopVoice();}
    private void ClearDirectionTransient(){foreach(var node in _directionTransient)DestroyTransient(node);_directionTransient.Clear();}
    private async Task CloseStoryAudio()
    {
        if(_storyAudio==null||_closingStoryAudio)return;_closingStoryAudio=true;CancelDirectionLine();
        try{await _storyAudio.CloseAsync(.5f);}finally{_storyAudio?.Dispose();}
    }
    private async Task CloseThenProceed(int index, MegaCrit.Sts2.Core.Models.EventModel expected)
    {
        await CloseStoryAudio();
        if(!GodotObject.IsInstanceValid(this)||!IsInsideTree()||!ReferenceEquals(_event,expected))return;
        _dispatchPending=false;foreach(var button in _choiceButtons)button.Disabled=false;ConfigureFocusNavigation();
        if(IsReaderActive())SelectOption(index);
    }
    private void DisposeDirections(){CancelDirectionLine();ClearDirectionTransient();_storyAudio?.Dispose();_storyAudio=null;}
    private static async void ObserveDirection(Task task){try{await task;}catch(OperationCanceledException){}catch(Exception error){GD.PushWarning("[HBR story] Local playback error: "+error.Message);}}
}
