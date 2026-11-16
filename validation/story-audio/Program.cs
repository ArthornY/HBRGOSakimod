using HBRGOSakiMod.Core.Story;
using System.Text.Json;
using System.Text.Json.Nodes;

int checks = 0;
void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
void Reject(string json) { try { HbrStoryAudioLibrary.Parse(json); } catch (Exception e) when (e is JsonException or InvalidOperationException) { checks++; return; } throw new Exception("Invalid library accepted: " + json); }
string valid = """{"schemaVersion":1,"cues":{"SB0012":{"path":"res://sounds/story/intro.ogg","sampleRate":44100,"channels":2,"totalSamples":5636880,"loopStart":545843,"loopEnd":5636880,"sourceTotalSamples":5637248,"sourceLoopEnd":5636880,"sourceSha256":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}} """;
var library = HbrStoryAudioLibrary.Parse(valid);
Check(library.Cues["SB0012"].LoopStart == 545843 && library.Cues["SB0012"].Channels == 2, "Stereo intro and original loop samples retained");
foreach (var pair in new (string, object?)[] { ("sampleRate",0),("channels",0),("channels",6),("totalSamples",-1L),("loopStart",-1L),("loopStart",5636880L),("loopEnd",5636879L),("loopEnd",null),("sourceTotalSamples",50L),("sourceLoopEnd",5636879L),("sourceSha256","x"),("path","res://sounds/story/../outside.ogg") })
{ var node=JsonNode.Parse(valid)!;node["cues"]!["SB0012"]![pair.Item1]=JsonSerializer.SerializeToNode(pair.Item2);Reject(node.ToJsonString()); }
Reject(valid.Replace("\"channels\":2", "\"channels\":2,\"channels\":1"));
Reject(valid.Replace("\"channels\":2", "\"channels\":2,\"typo\":1"));
Reject(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
Reject(valid.Replace("\"SB0012\":", "\"../../SB0012\":"));
foreach (string path in new[] {"res://sounds/story/a.ogg","res://sounds/thirty_one_a_cutin/VC_a.ogg","res://sounds/story/sub/VC_a.OGG"}) Check(HbrStoryAudioLibrary.IsAudioPath(path),"Valid resource " + path);
foreach (string path in new[] {"res://sounds/story//a.ogg","res://sounds/story/./a.ogg","res://sounds/story/../a.ogg","res://sounds/story/a:other.ogg","C:/a.ogg","res://sounds/story/a.wav","res://images/a.ogg","res://sounds/story/a\\b.ogg","res://sounds/story/a\n.ogg"}) Check(!HbrStoryAudioLibrary.IsAudioPath(path),"Unsafe or unsupported resource " + path);
foreach(string cue in new[]{"AS_Recollection_Coming","SB0012","VC_MC01_a"})Check(HbrStoryAudioLibrary.IsCueName(cue),"Cue name");
foreach(string cue in new[]{"../VC","a/b","a.ogg","","x\n","VC:foo"})Check(!HbrStoryAudioLibrary.IsCueName(cue),"Reject cue path");
Check(HbrStoryAudioLibrary.EffectiveGain(1,.5f,.5f)==.0625f,"Native square master/group curve");
Check(HbrStoryAudioLibrary.EffectiveGain(1,0,1)==0,"Master mute");
Check(HbrStoryAudioLibrary.EffectiveGain(1,1,0)==0,"Group mute");
Check(HbrStoryAudioLibrary.EffectiveGain(1,2,2)==1,"Clamped current settings");
Check(HbrStoryAudioLibrary.EffectiveGain(1,float.NaN,1)==0,"Invalid volume silenced");
var fade=new HbrStoryAudioEnvelope(0);fade.FadeTo(1,3);Check(!fade.Tick(1)&&Math.Abs(fade.Gain-1f/3)<.0001,"Fade intermediate");
fade.FadeTo(1,0);Check(fade.Tick(.5)&&fade.Gain==1,"Ctrl instant cancels previous fade without gain bounce");
fade.FadeTo(0,.5);Check(!fade.Tick(.25)&&fade.Gain==.5f,"Exit half fade");Check(fade.Tick(.25)&&fade.Gain==0,"Exit full fade");
fade.FadeTo(1,1);fade.Tick(.2);float previous=fade.Gain;fade.FadeTo(.6f,.5);fade.Tick(.25);Check(Math.Abs(fade.Gain-(previous+.6f)/2)<.0001,"New fade starts at live gain");
fade.FadeTo(0,0);fade.Tick(double.NaN);Check(fade.Gain==0,"No stale fade on rewind");
fade.FadeTo(float.NaN,double.PositiveInfinity);Check(fade.Tick(-1)&&fade.Gain==0,"Invalid fade inputs do not create NaN");
if (args.Length > 0) { var production=HbrStoryAudioLibrary.Parse(File.ReadAllText(args[0]));Check(production.Cues.ContainsKey("AS_Recollection_Coming"),"Actual published preset SE manifest validates"); }
Console.WriteLine(JsonSerializer.Serialize(new {ok=true,checks,boundary="Production pure audio library/envelope only; no Godot, actual audio output, FMOD/SoundPack lease integration or user data."}));
