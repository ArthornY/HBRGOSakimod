using System.Text.Json;
using HBRGOSakiMod.Core.Story;

static class Checks
{
    static readonly List<string> Passed=new();
    static void Check(bool test,string name) { if(!test)throw new Exception(name);Passed.Add(name); }
    static void Reject(Action action,string name) { try { action(); } catch(Exception e) when(e is InvalidOperationException or JsonException) { Passed.Add(name);return; }throw new Exception("did not reject: "+name); }
    static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,TypeInfoResolver=HbrStoryDefinition.CreateSerializationResolver()};
    static HbrStoryDirection Read(string json)=>JsonSerializer.Deserialize<HbrStoryDirection>(json,Json);
    static HbrStoryCharacter Actor(string id="RUKA",string image="res://images/ruka.png")=>new(){Id=id,Image=image};
    static HbrStoryLine Line(params HbrStoryDirection[] directions)=>new(){Kind="dialogue",Speaker="月歌",Text="test",Direction=directions};
    static HbrStoryDefinition Story(HbrStoryLine line)=>new(){SchemaVersion=1,Id="DIRECTION_TEST",Title="test",Conditions=new(){PartyCharacters=Array.Empty<string>(),Acts=Array.Empty<int>(),OncePerRun=false,AfterEventIds=Array.Empty<string>()},Presentation=new(){Background="res://images/background.png"},StartPage="FIRST",Pages=new[]{new HbrStoryPage{Id="FIRST",Characters=new[]{Actor()},Lines=new[]{line},Choices=Array.Empty<HbrStoryChoice>()}}};
    public static void Main(string[] args)
    {
        string output=args.FirstOrDefault()??"outputs/story-directions-101-20261116/schema/pure-report.json";
        try
        {
            var old=Story(Line());old.Validate();string oldJson=JsonSerializer.Serialize(old,Json);
            Check(!oldJson.Contains("\"direction\"")&&!oldJson.Contains("\"voice\"")&&!oldJson.Contains("FadeDuration")&&!oldJson.Contains("voiceCharacter"),"legacy omits all new default fields");
            var oldCopy=HbrStoryDefinition.Parse(oldJson);Check(oldCopy.Pages[0].Lines[0].Direction.Count==0&&oldCopy.Pages[0].Lines[0].Voice=="","legacy parses with defaults");
            Check(oldCopy.Presentation.CharacterFadeDuration==.3&&oldCopy.Presentation.ExpressionFadeDuration==.3,"default actor/expression fades .3 seconds");
            foreach(string identity in new[]{"RKayamori","ruka","tezuka_saki"})
            {
                var explicitVoice=Story(new HbrStoryLine{Kind="dialogue",Text="test",Characters=new[]{new HbrStoryCharacter{Id="A",Image="res://images/a.png",VoiceCharacter=identity}}});
                explicitVoice.Validate();var voiceRoundtrip=HbrStoryDefinition.Parse(JsonSerializer.Serialize(explicitVoice,Json));
                var actor=voiceRoundtrip.Pages[0].Lines[0].Characters[0];
                Check(actor.Id=="A"&&actor.VoiceCharacter==identity,"generic actor explicit voice identity roundtrip "+identity);
                var voiceState=HbrStoryDirectionReducer.Apply(HbrStorySceneState.Empty,new(){Op="scene"},new("res://images/a.png",new[]{actor}));
                Check(HbrStoryDirectionReducer.Restore(HbrStoryDirectionReducer.Apply(voiceState,new(){Op="moveBy",Target="A",X=50})).After.Actors[0].VoiceCharacter==identity,"voice identity survives motion and back snapshot "+identity);
            }
            foreach(string invalid in new[]{"../Ruka","Ruka/Default","月歌",new string('a',129)})
                Reject(()=>Story(new HbrStoryLine{Kind="dialogue",Text="test",Characters=new[]{new HbrStoryCharacter{Id="A",Image="res://images/a.png",VoiceCharacter=invalid}}}).Validate(),"reject unsafe voice identity "+invalid);
            Reject(()=>Story(new HbrStoryLine{Kind="dialogue",Text="test",Characters=new[]{new HbrStoryCharacter{Id="A",Image="res://images/a.png",VoiceCharacter=null}}}).Validate(),"reject null voice identity");
            var disabled=JsonSerializer.Serialize(new HbrStoryPresentation{Background="res://images/a.png",CharacterFadeDuration=0,ExpressionFadeDuration=0},Json);
            Check(disabled.Contains("\"characterFadeDuration\":0")&&disabled.Contains("\"expressionFadeDuration\":0"),"zero explicitly disables fades and survives serialization");
            Check(HbrStoryDirectionCatalog.Descriptors.Count==47,"complete closed command inventory 47");
            foreach(var descriptor in HbrStoryDirectionCatalog.Descriptors)
            {
                var obj=new Dictionary<string,object>{{"op",descriptor.Op}};
                foreach(var p in descriptor.Parameters.Where(p=>p.Required)) obj[p.Name]=p.Name switch{"target"=>"RUKA","name"=>"Adventure/Recollect/Recollect","id"=>"recollect","character"=>"RKayamori","expression"=>"Encount","image"=>"res://images/still.png","cue"=>"SB0004","text"=>"test",_=>p.DefaultValue??"test"};
                var d=Read(JsonSerializer.Serialize(obj));HbrStoryDirectionCatalog.Validate(d,"test",new[]{"RUKA"});
                Check(Read(JsonSerializer.Serialize(d,Json)).Op==descriptor.Op,"roundtrip "+descriptor.Op);
                Check(descriptor.Label.Contains(descriptor.Op)&&descriptor.Label.Any(c=>c>127),"Chinese editor descriptor "+descriptor.Op);
                foreach(var p in descriptor.Parameters.Where(p=>p.Kind is "number" or "integer"))
                {
                    var below=new Dictionary<string,object>(obj){[p.Name]=p.Minimum.Value-1};
                    Reject(()=>Read(JsonSerializer.Serialize(below)),"below minimum "+descriptor.Op+"."+p.Name);
                    var above=new Dictionary<string,object>(obj){[p.Name]=p.Maximum.Value+1};
                    Reject(()=>Read(JsonSerializer.Serialize(above)),"above maximum "+descriptor.Op+"."+p.Name);
                }
                obj["illegalExtra"]=0;Reject(()=>Read(JsonSerializer.Serialize(obj)),"unknown property "+descriptor.Op);
            }
            foreach(string bad in new[]{"{\"op\":\"execute\"}","{\"op\":\"wait\",\"duration\":-1}","{\"op\":\"wait\",\"duration\":1,\"duration\":2}","{\"op\":\"wait\",\"duration\":null}","{\"op\":\"wait\",\"target\":null}","{\"op\":\"se\",\"cue\":\"../secret\"}","{\"op\":\"miniCutIn\",\"character\":\"RKayamori\",\"index\":6}","{\"op\":\"effect\",\"name\":\"../../file\",\"id\":\"x\"}"})Reject(()=>Read(bad),"reject "+bad);
            foreach(string badCue in new[]{"SB0004\n","SB0004\r\n","\nSB0004","SB0004\0"})
            {
                Reject(()=>HbrStoryResources.CheckCue(badCue,"test"),"strict cue full-string boundary "+JsonSerializer.Serialize(badCue));
                Reject(()=>Read(JsonSerializer.Serialize(new{op="se",cue=badCue})),"direction rejects cue boundary "+JsonSerializer.Serialize(badCue));
                Reject(()=>Story(new HbrStoryLine{Kind="dialogue",Text="test",Voice=badCue}).Validate(),"line voice rejects cue boundary "+JsonSerializer.Serialize(badCue));
            }
            Reject(()=>Story(Line(new HbrStoryDirection{Op="show",Target="MISSING"})).Validate(),"authored unknown character target");
            Reject(()=>HbrStoryDirectionCatalog.Validate(new(){Op="wait",Duration=double.NaN},"test"),"programmatic NaN rejected");
            Reject(()=>Read("{\"op\":\"blur\",\"scope\":\"character\",\"blur\":1}"),"character blur needs target");
            Reject(()=>Read("{\"op\":\"sepia\",\"rgb\":{\"r\":1,\"g\":1}}"),"RGB needs all three components");
            Reject(()=>Read("{\"op\":\"sepia\",\"rgb\":{\"r\":1,\"g\":1,\"b\":1,\"a\":1}}"),"RGB rejects extra component");
            Check(Read("{\"op\":\"sepia\",\"rgb\":{\"r\":1.07,\"g\":.74,\"b\":.43}}".Replace(":.",":0.")).Rgb.R==1.07,"HDR original SepiaColor2 valid");
            Check(Read("{\"op\":\"bgm\",\"cue\":\"SB0004\",\"fadeOut\":2}").GetNumber("fadeIn")==2,"BGM omitted fadeIn inherits fadeOut");
            Check(Read("{\"op\":\"swelling\",\"target\":\"RUKA\",\"duration\":2}").GetNumber("disappearDuration")==2,"swelling disappearance defaults to authored duration");
            var initial=new HbrStoryResolvedScene("res://images/now.png",new[]{Actor()});
            var s=HbrStoryDirectionReducer.Apply(HbrStorySceneState.Empty,new(){Op="scene"},initial);
            var recollect=Line(new(){Op="whiteOut",Duration=1},new(){Op="wait",Duration=.5},new(){Op="scene"},new(){Op="effect",Id="recall",Name="Adventure/Recollect/Recollect"},new(){Op="sepia",Gray=1,Sepia=.5},new(){Op="whiteIn",Duration=3,NoWait=true},new(){Op="wait",Duration=2});
            var recallScene=new HbrStoryResolvedScene("res://images/past.png",new[]{Actor("YUKI","res://images/yuki.png")});
            var plan=HbrStoryDirectionReducer.Plan(s,recallScene,recollect,old.Presentation);
            Check(plan.Steps[0].Before.Background=="res://images/now.png"&&plan.Steps[1].After.Background=="res://images/now.png"&&plan.Steps[2].After.Background=="res://images/past.png","explicit scene commits under white instead of at line start");
            Check(plan.BlockingDuration==3.5&&plan.Steps[5].WaitSeconds==0,"recollect opening overlap: 1+.5+2, whiteIn3 noWait");
            Check(s.Effects.Count==0&&plan.After.Effects.Count==1&&plan.After.Filters.Gray==1&&plan.After.Actors[0].Id=="YUKI","persistent fold has no mutation of previous snapshot");
            var ending=Line(new(){Op="wait",Duration=.2},new(){Op="whiteOut",Duration=2},new(){Op="scene"},new(){Op="sepia",Gray=0,Sepia=0},new(){Op="effectRemove",Id="recall"},new(){Op="wait",Duration=1},new(){Op="whiteIn",Duration=2,NoWait=true},new(){Op="wait",Duration=1});
            var end=HbrStoryDirectionReducer.Plan(plan.After,initial,ending,old.Presentation);
            Check(Math.Abs(end.BlockingDuration-4.2)<1e-9&&end.After.Effects.Count==0&&end.After.Filters.Gray==0,"recollect ending overlap 4.2s and cleanup");
            var fast=HbrStoryDirectionReducer.Plan(s,recallScene,recollect,old.Presentation,HbrStoryPlaybackMode.FastForward);
            Check(fast.BlockingDuration==0&&fast.Steps.All(x=>x.Duration==0)&&fast.Steps[0].Suppressed&&fast.After.Background==plan.After.Background&&fast.After.Effects.Count==1,"Ctrl suppresses flash/waits but applies persistent scene/filter/effect");
            var silent=Line(new(){Op="shake"},new(){Op="se",Cue="AS_Deforme_Proud"},new(){Op="cutIn",Character="RKayamori",Expression="Encount"},new(){Op="bgm",Cue="SB0004"},new(){Op="env",Cue="EN_Rain"});
            var restored=HbrStoryDirectionReducer.Plan(plan.After,new("",Array.Empty<HbrStoryCharacter>(),false,false),silent,old.Presentation,HbrStoryPlaybackMode.Restore);
            Check(restored.Steps.Count==0&&restored.After.Bgm.Cue=="SB0004"&&restored.After.Env.Cue=="EN_Rain"&&restored.After.Effects.Count==1,"back restoration emits no transient work and restores music/env");
            var moved=HbrStoryDirectionReducer.Apply(s,new(){Op="moveBy",Target="RUKA",X=120});
            var inherited=HbrStoryDirectionReducer.Plan(moved,new("",Array.Empty<HbrStoryCharacter>(),false,false),Line(),old.Presentation);
            Check(moved.Actors[0].X==900&&inherited.After.Actors[0].X==900,"inherited unchanged scene preserves prior motion");
            var depth=HbrStoryDirectionReducer.Apply(s,new(){Op="moveBy",Target="RUKA",Z=12});
            Check(depth.Actors[0].Z==12&&HbrStoryDirectionReducer.Restore(depth).After.Actors[0].Z==12,"native move depth persists through back snapshot");
            Check(HbrStoryDirectionReducer.Apply(depth,new(){Op="moveTo",Target="RUKA",Z=-3}).Actors[0].Z==-3,"absolute move replaces depth rather than accumulating");
            Check(HbrStoryDirectionReducer.Restore(moved).After.Actors[0].X==900&&HbrStoryDirectionReducer.Restore(moved).Steps.Count==0,"back endpoint restore never applies relative motion twice");
            Check(HbrStoryDirectionReducer.Apply(moved,new(){Op="appear",Target="RUKA"}).Actors[0].Scale==moved.Actors[0].Scale,"appear absent scale preserves live scale");
            var transitioned=HbrStoryDirectionReducer.Plan(s,recallScene,Line(),old.Presentation);
            Check(transitioned.Steps[0].Duration==.3&&transitioned.BlockingDuration==0,"implicit scene preserves automatic .3 cast fade without blocking text");
            var whiteFast=HbrStoryDirectionReducer.Plan(s with{Fade=new("white",1)},new("",Array.Empty<HbrStoryCharacter>(),false,false),Line(),old.Presentation,HbrStoryPlaybackMode.FastForward);
            Check(whiteFast.After.Fade.Opacity==0,"Ctrl midway through whiteOut clears white overlay");
            var sceneLine=new HbrStoryLine{Kind="dialogue",Text="switch",Characters=new[]{Actor("YUKI")},Direction=new[]{new HbrStoryDirection{Op="hide",Target="RUKA"},new HbrStoryDirection{Op="scene"},new HbrStoryDirection{Op="show",Target="YUKI"}}};
            Story(sceneLine).Validate();Check(true,"target validation follows scene position: old actor before, new after");
            Reject(()=>Story(new HbrStoryLine{Kind="dialogue",Text="switch",Characters=new[]{Actor("YUKI")},Direction=new[]{new HbrStoryDirection{Op="scene"},new HbrStoryDirection{Op="show",Target="RUKA"}}}).Validate(),"old actor after scene rejected");
            Check(HbrStoryDirectionReducer.BackgroundCropX(2772,1176,1600,900,new("BG_LEFT"))>=0&&HbrStoryDirectionReducer.BackgroundCropX(2772,1176,1600,900,new("BG_RIGHT",10000))<=2772,"background camera source-pixel crop clamps");
            Check(Read("{\"op\":\"cutIn\",\"character\":\"RKayamori\",\"expression\":\"Encount\"}").GetNumber("duration")==2.5,"native cutIn default2.5");
            var refs=HbrStoryResources.Collect(old.Presentation,new[]{new HbrStoryLine{Kind="dialogue",Text="test",Voice="VC_Ruka",Characters=new[]{Actor()},Direction=silent.Direction.Concat(new[]{new HbrStoryDirection{Op="effect",Id="recollect",Name="Adventure/Recollect/Recollect"},new HbrStoryDirection{Op="showStill",Image="res://images/cg.png"},new HbrStoryDirection{Op="miniCutIn",Character="RKayamori",Index=1}}).ToArray()}});
            foreach(var kind in Enum.GetValues<HbrStoryResourceKind>())Check(refs.Any(r=>r.Kind==kind),"typed resource collection "+kind);
            Check(HbrStoryResources.Missing(refs,r=>r.Kind!=HbrStoryResourceKind.Face).Count==0,"optional face sidecars do not invalidate old art");
            var upgrade=new HbrMemoryUpgradeStoryDefinition{SchemaVersion=1,CharacterId="ruka",Presentation=old.Presentation,Segments=new[]{new HbrMemoryUpgradeStorySegment{Level=1,Perspectives=new[]{"31o"},Lines=new[]{new HbrStoryLine{Kind="dialogue",Text="memory",Direction=new[]{new HbrStoryDirection{Op="showStill",Image="res://images/cg.png"}}}}}}};
            upgrade.Validate();Check(upgrade.Segments[0].AssetPaths.Contains("res://images/cg.png"),"requirement100 AssetPaths includes direction image without treating cue IDs as image paths");
            var face=HbrStoryFaceDefinition.Parse("{\"eyesClosed\":{\"image\":\"eyes.png\",\"x\":12,\"y\":23},\"mouth\":[{\"image\":\"m1.png\",\"x\":10,\"y\":40},{\"image\":\"m2.png\",\"x\":10,\"y\":40},{\"image\":\"m3.png\",\"x\":10,\"y\":40}]}");
            face.ValidateBounds("res://images/ruka.png",100,100,_=>(20,20));Check(face.Collect("res://images/ruka.png").Count==4,"face sidecar patches resolve relative to original and validate bounds");
            foreach(string f in new[]{"{\"mouth\":[{\"image\":\"m.png\",\"x\":0,\"y\":0}]}","{\"eyesClosed\":{\"image\":\"../bad.png\",\"x\":0,\"y\":0}}","{\"eyesClosed\":{\"image\":\"e.png\",\"x\":-1,\"y\":0}}","{\"blinkMaximum\":1}","{\"unknown\":true}"})Reject(()=>HbrStoryFaceDefinition.Parse(f),"invalid face "+f);
            Reject(()=>face.ValidateBounds("res://images/ruka.png",20,20,_=>(20,20)),"face patch cannot overflow base pixels");
            Check(HbrStoryDirectionReducer.Plan(s,recallScene,Line(HbrStoryDirectionCatalog.RecallOpening.ToArray()),old.Presentation).BlockingDuration==3.5,"shared recall opening preset preserves overlap");
            Check(Math.Abs(HbrStoryDirectionReducer.Plan(plan.After,initial,Line(HbrStoryDirectionCatalog.RecallEnding.ToArray()),old.Presentation).BlockingDuration-4.2)<1e-9,"shared recall ending preset preserves overlap");
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonSerializer.Serialize(new{ok=true,count=Passed.Count,checks=Passed,boundary="Pure actual production DTO/converter/validator/reducer/resource/face contracts; no rendering, audio decode, Godot, main SDK, D or user data writes."},new JsonSerializerOptions{WriteIndented=true}));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"direction-catalog.json"),JsonSerializer.Serialize(HbrStoryDirectionCatalog.Descriptors,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true}));
            Console.WriteLine($"{Passed.Count} checks passed");
        }
        catch(Exception e){Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonSerializer.Serialize(new{ok=false,count=Passed.Count,checks=Passed,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
}

