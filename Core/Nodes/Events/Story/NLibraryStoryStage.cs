using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Story;

namespace HBRGOSakiMod.Core.Nodes.Events.Story;

internal sealed partial class NLibraryStoryStage : Control
{
    internal sealed record ActorFrame(string Id,string Image,float X,float Y,float Height,bool Flip,float Opacity,float Scale,bool Visible,float Blur,bool Blink,bool Lip,double Z=0);
    private sealed class Actor
    {
        internal ActorFrame Target;
        internal Control Root;
        internal TextureRect Image;
        internal NLibraryStoryFace Face;
        internal ShaderMaterial Material;
        internal float CurrentBlur;
        internal int Order;
        internal string SpeechName;
    }
    private readonly Dictionary<string,Actor> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string,NLibraryStoryEffect> _effects = new(StringComparer.Ordinal);
    private readonly HbrStoryAnimationClock _clock = new();
    private readonly Dictionary<Control,(string Actor,Vector2 Offset)> _attachments=new();
    private readonly Random _random = new();
    private Control _backgroundLayer, _actorLayer, _backEffects, _frontEffects, _stillLayer, _overlay;
    private Control[] _shakeLayers;
    private bool _hadLayerShake;
    private TextureRect _background, _still, _heldBackground;
    private ColorRect _fade;
    private ShaderMaterial _backgroundMaterial,_stillMaterial;
    private Rect2 _viewport=new(0,0,1600,900);
    private string _backgroundPath, _stillPath;
    private Vector2 _cameraOffset;
    private float _cameraZoom=1,_cameraTargetZoom=1;
    private Vector2 _cameraTargetOffset;
    private sealed class ShakeJob { internal double Time; internal Vector2 Amplitude; }
    private sealed class Ghost { internal CanvasItem Node; internal double Age,Duration; internal float Alpha; }
    private readonly Dictionary<string,ShakeJob> _shakes=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Vector2> _shakeBases=new(StringComparer.Ordinal);
    private readonly List<Ghost> _ghosts=new();
    private float _gray,_sepia,_vignette,_chromatic,_backgroundBlur,_foregroundBlur,_invert,_choiceBlur;
    private Color _sepiaColor=new(1.07f,.74f,.43f),_targetSepiaColor=new(1.07f,.74f,.43f);
    private readonly float[] _filterTargets=new float[7];
    private bool _filtersDirty;
    internal bool Covered {get;set;}
    internal HbrStoryAnimationClock Clock => _clock;
    internal void Build()
    {
        Size=new Vector2(1600,900); ClipContents=false; MouseFilter=MouseFilterEnum.Ignore;
        _backgroundLayer=Layer("Background"); _backEffects=Layer("BackgroundEffects");
        _actorLayer=Layer("Actors"); _actorLayer.ClipContents=true; _frontEffects=Layer("ForegroundEffects"); _stillLayer=Layer("Still"); _overlay=Layer("StoryOverlay");
        _shakeLayers=new[]{_backgroundLayer,_backEffects,_actorLayer,_frontEffects,_stillLayer,_overlay};
        _backgroundMaterial=CreateFilterMaterial(0);
        _background=new TextureRect { Material=_backgroundMaterial, ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore };
        _backgroundLayer.AddChild(_background);
        _fade=new ColorRect { Color=new Color(0,0,0,0),Size=Size,MouseFilter=MouseFilterEnum.Ignore };
        _overlay.AddChild(_fade);
    }
    internal void SetViewport(Rect2 viewport)
    {_viewport=viewport;if(_fade!=null){_fade.Position=viewport.Position;_fade.Size=viewport.Size;}LayoutBackground();}
    private Control Layer(string name)
    {var node=new Control{Name=name,Size=Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(node);return node;}
    private static Texture2D Texture(string path)=>NLibraryStoryVisualResources.Texture(path);
    private static ShaderMaterial CreateFilterMaterial(float blur)=>new(){Shader=new Shader{Code=FilterShader}};
    internal void Scene(string background,IReadOnlyList<ActorFrame> characters,float characterFade,float expressionFade,bool instant=false,float backgroundFade=0)
    {
        Background(background,instant?0:backgroundFade);
        var ids=characters.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        foreach(string id in _actors.Keys.Where(id=>!ids.Contains(id)).ToArray()) RemoveActor(id,instant?0:characterFade);
        for(int i=0;i<characters.Count;i++){var character=characters[i];SetActor(character,0,instant?0:expressionFade,alphaDuration:instant?0:characterFade);if(_actors.TryGetValue(character.Id,out var actor))actor.Order=i;}
        ReorderActors();
    }
    internal void Background(string path,float duration=0)
    {
        if(_backgroundPath==path)return;
        Texture2D next=Texture(path);if(next==null)return;
        TextureRect previous=null;
        if(_background.Texture!=null)
        {
            previous=new TextureRect{Texture=_background.Texture,Position=_background.Position,Size=_background.Size,Material=_backgroundMaterial,
                ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};
            _backgroundLayer.AddChild(previous);
        }
        _background.Texture=next;_backgroundPath=path;LayoutBackground();
        if(previous!=null)
        {
            if(_heldBackground!=null)_heldBackground.QueueFree();_heldBackground=null;
            if(duration>0)FadeFree(previous,duration);else{previous.Hide();_heldBackground=previous;}
        }
    }
    private void SetActor(ActorFrame frame,float fade,float expressionFade,float easeIn=1,float easeOut=1,float alphaDuration=-1,string channel="")
    {
        if(_actors.TryGetValue(frame.Id,out Actor actor)&&actor.Target==frame&&channel.Length==0)return;
        bool created=actor==null;
        if(created)
        {
            actor=new Actor {Root=new Control{MouseFilter=MouseFilterEnum.Ignore},Material=CreateFilterMaterial(frame.Blur),CurrentBlur=frame.Blur,SpeechName=HbrStoryCharacterCatalog.Entries.FirstOrDefault(c=>string.Equals(c.Id,frame.Id,StringComparison.OrdinalIgnoreCase))?.Name};
            actor.Image=new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore,Material=actor.Material};
            _actorLayer.AddChild(actor.Root);actor.Root.AddChild(actor.Image);_actors.Add(frame.Id,actor);
        }
        if(actor.Target?.Image!=frame.Image)
        {
            Texture2D next=Texture(frame.Image);if(next==null){if(created)RemoveActor(frame.Id,0);return;}
            if(actor.Image.Texture!=null&&expressionFade>0)
            {
                var old=new TextureRect{Texture=actor.Image.Texture,Position=actor.Image.Position,Size=actor.Image.Size,FlipH=actor.Image.FlipH,Material=actor.Material,
                    ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};
                actor.Root.AddChild(old);FadeFree(old,expressionFade);
            }
            actor.Image.Texture=next;
            if(actor.Face!=null){actor.Face.QueueFree();actor.Face=null;}
            actor.Face=NLibraryStoryFace.TryCreate(frame.Image,next);
            if(actor.Face!=null){actor.Face.SetSharedMaterial(actor.Material);actor.Root.AddChild(actor.Face);}
        }
        ActorFrame previous=actor.Target;actor.Target=frame;
        bool positionChange=!created&&(previous.X!=frame.X||previous.Y!=frame.Y||channel is "move" or "all");
        bool scaleChange=!created&&(previous.Scale!=frame.Scale||channel is "scale" or "all");
        bool blurChange=!created&&(previous.Blur!=frame.Blur||channel is "blur" or "all");
        bool alphaChange=created||previous.Visible!=frame.Visible||previous.Opacity!=frame.Opacity||channel is "alpha" or "show" or "hide" or "all";
        ReorderActors();
        float width=frame.Height*actor.Image.Texture.GetWidth()/actor.Image.Texture.GetHeight();
        Vector2 targetPosition=new(frame.X,frame.Y),targetScale=Vector2.One*frame.Scale;
        actor.Image.Position=new Vector2(-width/2,-frame.Height);actor.Image.Size=new Vector2(width,frame.Height);actor.Image.FlipH=frame.Flip;
        if(actor.Face!=null)
        {actor.Face.Position=actor.Image.Position;actor.Face.Layout(actor.Image.Size,frame.Flip);actor.Face.BlinkEnabled=frame.Blink;actor.Face.LipSyncEnabled=frame.Lip;}
        actor.Root.Visible=true;
        float opacity=frame.Visible?frame.Opacity:0;
        if(created){actor.Root.Position=targetPosition;actor.Root.Scale=targetScale;actor.Root.Modulate=new Color(1,1,1,(alphaDuration<0?fade:alphaDuration)>0?0:opacity);}
        if(positionChange)
        {
            Vector2 from=_shakeBases.TryGetValue(frame.Id,out var unshaken)?unshaken:actor.Root.Position;
            Observe(_clock.Animate("position:"+frame.Id,fade,t=>actor.Root.Position=from.Lerp(targetPosition,Ease(t,easeIn,easeOut))));
        }
        if(scaleChange)
        {
            Vector2 from=actor.Root.Scale;
            Observe(_clock.Animate("scale:"+frame.Id,fade,t=>actor.Root.Scale=from.Lerp(targetScale,Ease(t,easeIn,easeOut))));
        }
        if(blurChange)
        {
            float from=actor.CurrentBlur;
            Observe(_clock.Animate("actorBlur:"+frame.Id,fade,t=>{actor.CurrentBlur=Mathf.Lerp(from,frame.Blur,t);_filtersDirty=true;}));
        }
        if(alphaChange)
        {
            float seconds=alphaDuration<0?fade:alphaDuration,fromAlpha=actor.Root.Modulate.A;
            if(seconds<=0){_clock.Cancel("alpha:"+frame.Id);actor.Root.Modulate=new Color(1,1,1,opacity);}
            else Observe(_clock.Animate("alpha:"+frame.Id,seconds,t=>actor.Root.Modulate=new Color(1,1,1,Mathf.Lerp(fromAlpha,opacity,t))));
        }
        ApplyMaterial(actor.Material,actor.CurrentBlur);
    }
    private void ReorderActors()
    {int order=0;foreach(var actor in _actors.Values.OrderBy(a=>a.Target?.Z??0).ThenBy(a=>a.Order)){if(actor.Target!=null)_actorLayer.MoveChild(actor.Root,order++);}}
    internal void Actors(IReadOnlyList<ActorFrame> actors,float duration=0,float easeIn=1,float easeOut=1,float alphaDuration=-1,string channel="")
    {foreach(var frame in actors)SetActor(frame,duration,duration,easeIn,easeOut,alphaDuration,channel);}
    private static float Ease(float t,float easeIn,float easeOut)=>t<.5f?.5f*MathF.Pow(2*t,Math.Max(.01f,easeIn)):1-.5f*MathF.Pow(2*(1-t),Math.Max(.01f,easeOut));
    private void RemoveActor(string id,float duration)
    {
        Actor actor=_actors[id];_actors.Remove(id);_clock.Cancel("position:"+id);_clock.Cancel("scale:"+id);_clock.Cancel("actorBlur:"+id);_clock.Cancel("alpha:"+id);
        if(duration>0)FadeFree(actor.Root,duration);else actor.Root.QueueFree();
    }
    private void FadeFree(CanvasItem node,float duration)
    { _ghosts.Add(new Ghost{Node=node,Duration=Math.Max(.001,duration),Alpha=node.Modulate.A}); }
    internal void CrossFade(float duration)
    {
        // A zero-time transition is a seek endpoint: never flash the held old image.
        if(duration<=0)
        {
            if(_heldBackground!=null){_heldBackground.QueueFree();_heldBackground=null;}
            foreach(var ghost in _ghosts)ghost.Node.QueueFree();_ghosts.Clear();return;
        }
        if(_heldBackground!=null){_heldBackground.Show();FadeFree(_heldBackground,duration);_heldBackground=null;}
        foreach(var ghost in _ghosts){ghost.Age=0;ghost.Duration=duration;ghost.Alpha=ghost.Node.Modulate.A;}
    }
    internal void Camera(float offsetX,float offsetY,float zoom,float duration=0,bool force=false)
    {
        Vector2 target=new(offsetX,offsetY);float targetZoom=Math.Max(.05f,zoom);
        if(!force&&_cameraTargetOffset==target&&_cameraTargetZoom==targetZoom)return;
        _cameraTargetOffset=target;_cameraTargetZoom=targetZoom;_clock.Cancel("camera");
        if(_cameraOffset==target&&_cameraZoom==targetZoom){LayoutBackground();return;}
        Vector2 from=_cameraOffset;float fromZoom=_cameraZoom;
        Observe(_clock.Animate("camera",duration,t=>{_cameraOffset=from.Lerp(target,t);_cameraZoom=Mathf.Lerp(fromZoom,targetZoom,t);LayoutBackground();}));
    }
    private void LayoutBackground()
    {
        if(_background.Texture==null)return;
        Vector2 raw=new(_background.Texture.GetWidth(),_background.Texture.GetHeight());
        float scale=Math.Max(_viewport.Size.X/raw.X,_viewport.Size.Y/raw.Y)*_cameraZoom;Vector2 size=raw*scale;
        // Original camera offsets are source-image pixels; crop never exposes a seam.
        float x=_viewport.Position.X+Math.Clamp((_viewport.Size.X-size.X)/2-_cameraOffset.X*scale,Math.Min(0,_viewport.Size.X-size.X),0);
        float y=_viewport.Position.Y+Math.Clamp((_viewport.Size.Y-size.Y)/2-_cameraOffset.Y*scale,Math.Min(0,_viewport.Size.Y-size.Y),0);
        _background.Position=new Vector2(x,y);_background.Size=size;
    }
    internal void Filters(float gray,float sepia,Color sepiaColor,float vignette,float chromatic,float backgroundBlur,float foregroundBlur,float invert,float duration=0,string channel="")
    {
        float[] targets={gray,sepia,vignette,chromatic,backgroundBlur,foregroundBlur,invert};
        string[] keys={"gray","sepia","vignette","chromaticAberration","backgroundBlur","foregroundBlur","invert"};
        for(int i=0;i<targets.Length;i++)
        {
            int index=i;bool force=channel==keys[i]||channel=="sepia"&&i is 0 or 1;
            if(_filterTargets[i]==targets[i]&&!force)continue;_filterTargets[i]=targets[i];
            float from=i switch{0=>_gray,1=>_sepia,2=>_vignette,3=>_chromatic,4=>_backgroundBlur,5=>_foregroundBlur,_=>_invert};
            Observe(_clock.Animate("filter:"+keys[i],duration,t=>
            {
                float value=Mathf.Lerp(from,targets[index],t);
                switch(index){case 0:_gray=value;break;case 1:_sepia=value;break;case 2:_vignette=value;break;case 3:_chromatic=value;break;case 4:_backgroundBlur=value;break;case 5:_foregroundBlur=value;break;default:_invert=value;break;}
                _filtersDirty=true;
            }));
        }
        if(_targetSepiaColor!=sepiaColor||channel=="sepia")
        {_targetSepiaColor=sepiaColor;Color from=_sepiaColor;Observe(_clock.Animate("filter:color",duration,t=>{_sepiaColor=from.Lerp(sepiaColor,t);_filtersDirty=true;}));}
        if(_filtersDirty){ApplyMaterials();_filtersDirty=false;}
    }
    private void ApplyMaterials(){ApplyMaterial(_backgroundMaterial,Math.Max(_backgroundBlur,_choiceBlur));if(_stillMaterial!=null)ApplyMaterial(_stillMaterial,_foregroundBlur);foreach(var actor in _actors.Values)ApplyMaterial(actor.Material,Math.Max(_foregroundBlur,actor.CurrentBlur));}
    internal void ChoiceBlur(bool choosing)
    {_choiceBlur=choosing?8:0;ApplyMaterial(_backgroundMaterial,Math.Max(_backgroundBlur,_choiceBlur));}
    private void ApplyMaterial(ShaderMaterial material,float blur)
    {material.SetShaderParameter("gray",_gray);material.SetShaderParameter("sepia",_sepia);material.SetShaderParameter("sepia_color",_sepiaColor);material.SetShaderParameter("vignette",_vignette);material.SetShaderParameter("chromatic",_chromatic);material.SetShaderParameter("blur",blur);material.SetShaderParameter("invert_amount",_invert);}
    internal void Effect(string id,string name,string layer,float x,float y,float scale,string target="screen")
    {
        Control parent=layer=="BG"?_backEffects:_frontEffects;
        if(_effects.TryGetValue(id,out var old))
        {
            if(old.EffectName==name){if(old.GetParent()!=parent)old.Reparent(parent,false);old.Position=new Vector2(x,y);old.Scale=Vector2.One*scale;BindEffect(old,target,x,y);return;}
            old.QueueFree();_effects.Remove(id);
        }
        var effect=NLibraryStoryEffect.Create(name);if(effect==null)return;
        parent.AddChild(effect);effect.Position=new Vector2(x,y);effect.Scale=Vector2.One*scale;_effects[id]=effect;BindEffect(effect,target,x,y);
    }
    private void BindEffect(Control effect,string target,float x,float y)
    {if(_actors.TryGetValue(target??"",out var actor))AttachOverlayToActor(effect,target,new Vector2(x-actor.Target.X,y-actor.Target.Y));else DetachOverlay(effect);}
    internal void RetriggerEffect(string id){if(_effects.TryGetValue(id,out var effect))effect.Restart();}
    internal void KeepEffects(IEnumerable<string> ids)
    {var keep=ids.ToHashSet(StringComparer.Ordinal);foreach(string id in _effects.Keys.Where(k=>!keep.Contains(k)).ToArray()){DetachOverlay(_effects[id]);_effects[id].QueueFree();_effects.Remove(id);}}
    internal void Still(string path,float x,float y,float zoom,float duration=0)
    {
        if(string.IsNullOrEmpty(path)){HideStill(null,duration);return;}
        Texture2D texture=Texture(path);if(texture==null)return;
        bool created=_still==null;
        if(created)
        {
            _stillMaterial=CreateFilterMaterial(0);ApplyMaterial(_stillMaterial,_foregroundBlur);
            _still=new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore,Material=_stillMaterial};_stillLayer.AddChild(_still);
        }
        TextureRect image=_still;image.Texture=texture;_stillPath=path;
        Vector2 fromPosition=image.Position,fromScale=image.Scale;float alpha=created&&duration>0?0:image.Modulate.A;
        image.Size=_viewport.Size;image.PivotOffset=image.Size/2;
        Observe(_clock.Animate("still",duration,t=>{image.Position=fromPosition.Lerp(_viewport.Position+new Vector2(x,y),t);image.Scale=fromScale.Lerp(Vector2.One*zoom,t);image.Modulate=new Color(1,1,1,Mathf.Lerp(alpha,1,t));}));
    }
    internal void HideStill(HbrStoryStillState previous,float duration)
    {
        if(_still==null)return;TextureRect image=_still;_still=null;_stillPath=null;
        Vector2 destination=previous?.Direction switch{"left"=>new(-(float)previous.Offset*5/6,0),"right"=>new((float)previous.Offset*5/6,0),"up"=>new(0,-(float)previous.Offset*5/6),"down"=>new(0,(float)previous.Offset*5/6),_=>Vector2.Zero};
        Vector2 from=image.Position,fromScale=image.Scale;float alpha=image.Modulate.A;float scale=previous?.Motion=="zoomOut"?1.1f:1;
        Observe(Hide());async Task Hide(){try{await _clock.Animate("still",duration,t=>{image.Position=from.Lerp(_viewport.Position+destination,t);image.Scale=fromScale.Lerp(Vector2.One*scale,t);image.Modulate=new Color(1,1,1,alpha*(1-t));});}catch(OperationCanceledException){}finally{image.QueueFree();}}
    }
    internal Task Fade(Color color,float alpha,float duration,CancellationToken token)
    {
        Color from=_fade.Color;Color to=new(color.R,color.G,color.B,alpha);
        return _clock.Animate("fade",duration,t=>_fade.Color=from.Lerp(to,t),token);
    }
    internal void Shake(string target,float x,float y,double seconds)
    {_shakes[target??"screen"]=new ShakeJob{Time=seconds,Amplitude=new Vector2(x,y)*100*5/6};}
    internal void StopShake(string target=null){if(string.IsNullOrEmpty(target)||target=="screen")_shakes.Clear();else _shakes.Remove(target);ClearShake();}
    private void ClearShake()
    {
        if(_hadLayerShake){foreach(var layer in _shakeLayers)layer.Position=Vector2.Zero;_fade.Position=_viewport.Position;_hadLayerShake=false;}
        foreach(var pair in _shakeBases)if(_actors.TryGetValue(pair.Key,out var actor))actor.Root.Position=pair.Value;
        _shakeBases.Clear();
    }
    internal void AttachOverlayToActor(Control overlay,string actor,Vector2 offset)
    {_attachments[overlay]=(actor,offset);UpdateAttachments();}
    internal void DetachOverlay(Control overlay)=>_attachments.Remove(overlay);
    private void UpdateAttachments()
    {foreach(var row in _attachments.ToArray()){if(!GodotObject.IsInstanceValid(row.Key)||row.Key.IsQueuedForDeletion()){_attachments.Remove(row.Key);continue;}if(_actors.TryGetValue(row.Value.Actor,out var actor))row.Key.Position=actor.Root.Position+row.Value.Offset;}}
    internal void Tick(double delta,string speaker,float voiceAmplitude,bool voicePlaying)
    {
        _clock.Paused=Covered;if(Covered)return;ClearShake();_clock.Tick(delta);if(_filtersDirty){ApplyMaterials();_filtersDirty=false;}
        for(int i=_ghosts.Count-1;i>=0;i--)
        {var ghost=_ghosts[i];ghost.Age+=delta;ghost.Node.Modulate=new Color(1,1,1,ghost.Alpha*(float)Math.Clamp(1-ghost.Age/ghost.Duration,0,1));if(ghost.Age>=ghost.Duration){ghost.Node.QueueFree();_ghosts.RemoveAt(i);}}
        foreach(var effect in _effects.Values)effect.Tick(delta);
        foreach(var pair in _actors)
        {
            string id=pair.Key;string name=pair.Value.SpeechName;
            bool speaking=voicePlaying&&(string.Equals(id,speaker,StringComparison.OrdinalIgnoreCase)||name==speaker||(!string.IsNullOrEmpty(speaker)&&name?.EndsWith(speaker,StringComparison.Ordinal)==true));
            pair.Value.Face?.Tick(delta,speaking,voiceAmplitude);
        }
        if(_shakes.Count>0)
        {
            float multiplier=MegaCrit.Sts2.Core.Nodes.Screens.Settings.NScreenshakePaginator.GetShakeMultiplier(MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.ScreenShakeOptionIndex);
            foreach(var pair in _shakes.ToArray())
            {
                pair.Value.Time-=delta;if(pair.Value.Time<=0){_shakes.Remove(pair.Key);continue;}
                Vector2 offset=new((float)(_random.NextDouble()*2-1)*pair.Value.Amplitude.X,(float)(_random.NextDouble()*2-1)*pair.Value.Amplitude.Y);offset*=multiplier;
                if(pair.Key is "BG" or "background"){_backgroundLayer.Position+=offset;_hadLayerShake=true;}
                else if(_actors.TryGetValue(pair.Key,out var actor)){_shakeBases.TryAdd(pair.Key,actor.Root.Position);actor.Root.Position+=offset;}
                else{foreach(var layer in _shakeLayers)layer.Position+=offset;_fade.Position=_viewport.Position-_overlay.Position;_hadLayerShake=true;}
            }
        }
        UpdateAttachments();
    }
    internal void CancelMotion(bool finish)
    {
        if(finish)_clock.CompleteAll();StopShake();
        foreach(var ghost in _ghosts)ghost.Node.QueueFree();_ghosts.Clear();
        if(_heldBackground!=null){_heldBackground.QueueFree();_heldBackground=null;}
    }
    internal Rect2 ViewportRect=>_viewport;
    internal Control Overlay=>_overlay;
    public override void _ExitTree(){_clock.Dispose();}
    private static async void Observe(Task task){try{await task;}catch(OperationCanceledException){}catch(Exception error){GD.PushWarning("[HBR story] Visual animation failed: "+error.Message);}}
    private const string FilterShader="""
shader_type canvas_item;
varying vec4 scene_tint;
void vertex(){scene_tint=COLOR;}
uniform float gray=0.0; uniform float sepia=0.0; uniform vec4 sepia_color=vec4(1.07,.74,.43,1.0);
uniform float vignette=0.0; uniform float chromatic=0.0; uniform float blur=0.0; uniform float invert_amount=0.0;
void fragment(){vec2 d=TEXTURE_PIXEL_SIZE*blur;vec4 c=texture(TEXTURE,UV)*.4;
c+=texture(TEXTURE,UV+d)*.15+texture(TEXTURE,UV-d)*.15+texture(TEXTURE,UV+vec2(d.x,-d.y))*.15+texture(TEXTURE,UV+vec2(-d.x,d.y))*.15;
float l=dot(c.rgb,vec3(.299,.587,.114));c.rgb=mix(c.rgb,vec3(l),gray);c.rgb=mix(c.rgb,c.rgb*sepia_color.rgb,sepia);
vec2 shift=vec2(chromatic*.003,0.0);c.r=mix(c.r,texture(TEXTURE,UV+shift).r,min(chromatic,1.0));c.b=mix(c.b,texture(TEXTURE,UV-shift).b,min(chromatic,1.0));
c.rgb*=1.0-clamp(length(SCREEN_UV-.5)*vignette,0.0,.95);c.rgb=mix(c.rgb,vec3(1.0)-c.rgb,invert_amount);COLOR=c*scene_tint;}
""";
}
