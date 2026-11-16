#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.Story;

public sealed record HbrStoryDirectionParameter(string Name, string Kind, bool Required = false,
    object DefaultValue = null, double? Minimum = null, double? Maximum = null, IReadOnlyList<string> Values = null,
    string DefaultFrom = null);
public sealed record HbrStoryDirectionDescriptor(string Op, string Category, string Label,
    IReadOnlyList<HbrStoryDirectionParameter> Parameters, bool Blocking = false, bool Transient = false);

public static class HbrStoryDirectionCatalog
{
    public const int MaximumDirectionsPerLine = 128;
    public const double CharacterCoordinateScale = 5d / 6d;
    public const double ShakeOriginalPixelsPerUnit = 100;
    private static HbrStoryDirectionParameter N(string name, double value, double min = 0, double max = 60, bool required = false) => new(name, "number", required, value, min, max);
    private static HbrStoryDirectionParameter S(string name, string value = null, bool required = false, params string[] values) => new(name, "string", required, value, Values: values.Length > 0 ? values : null);
    private static HbrStoryDirectionParameter B(string name, bool value) => new(name, "boolean", DefaultValue: value);
    private static HbrStoryDirectionDescriptor D(string op, string category, bool blocking, bool transient, params HbrStoryDirectionParameter[] parameters) => new(op, category, Label(op) + "（" + op + "）", parameters, blocking, transient);
    private static string Label(string op) => op switch
    {
        "scene"=>"换上本句场景","fadeOut"=>"淡出到黑屏","fadeIn"=>"从黑屏淡入","whiteOut"=>"淡出到白屏","whiteIn"=>"从白屏淡入","wait"=>"等待","crossFade"=>"整体交叉淡化",
        "show"=>"人物出场","hide"=>"人物退场","moveBy"=>"人物相对移动","moveTo"=>"人物移动到","scale"=>"人物缩放","alpha"=>"人物透明度","appear"=>"模糊滑入","swelling"=>"放大模糊退场",
        "shake"=>"震动","shakeXY"=>"横竖震动","stopShake"=>"停止震动","stageFixBg"=>"背景取景","zoomIn"=>"镜头推近","zoomOut"=>"镜头拉远","panL"=>"镜头左移","panR"=>"镜头右移","stagePan"=>"镜头横移",
        "sepia"=>"回想色调","vignette"=>"暗角","chromaticAberration"=>"色差","blur"=>"模糊","effect"=>"放置特效","effectRemove"=>"移除特效","cutIn"=>"角色插入演出",
        "bgm"=>"播放音乐","bgmStop"=>"停止音乐","se"=>"播放音效","env"=>"播放环境音","envStop"=>"停止环境音","bubbleSeActNoWait"=>"拟声字","bubbleTalkAsync"=>"对话泡","bubbleAngerAsync"=>"生气对话泡",
        "miniCutIn"=>"小插入演出","showStill"=>"展示CG","hideStill"=>"收起CG","showBlackTextInMiddle"=>"黑底居中文字","zoomShakeWithEffect"=>"推近震动集中线","showEffectLineWork"=>"集中线","blink"=>"眨眼开关","lipSync"=>"口型开关",_=>op
    };
    private static HbrStoryDirectionParameter Target => S("target", required: true);
    private static HbrStoryDirectionParameter Time(double value = 0) => N("duration", value);
    private static HbrStoryDirectionParameter X => N("x", 0, -10000, 10000);
    private static HbrStoryDirectionParameter Y => N("y", 0, -10000, 10000);
    private static HbrStoryDirectionParameter Zoom(double value = 1) => N("scale", value, .01, 16);
    private static HbrStoryDirectionParameter Position(string value = "BG_CENTER") => S("position", value, false, "BG_LEFT", "BG_LEFT_CENTER", "BG_CENTER", "BG_RIGHT_CENTER", "BG_RIGHT");
    private static HbrStoryDirectionParameter Cue(bool required = true) => S("cue", required: required);

    public static IReadOnlyList<HbrStoryDirectionDescriptor> Descriptors { get; } = Build();
    public static IReadOnlyList<HbrStoryDirection> RecallOpening { get; } = Array.AsReadOnly(new[]
    {
        new HbrStoryDirection { Op="se",Cue="AS_Recollection_Coming" },
        new HbrStoryDirection { Op="whiteOut",Duration=1 },new HbrStoryDirection { Op="wait",Duration=.5 },
        new HbrStoryDirection { Op="effect",Name="Adventure/Recollect/Recollect",Id="recollect",Layer="ForegroundEffect" },
        new HbrStoryDirection { Op="scene" },new HbrStoryDirection { Op="sepia",Gray=1,Sepia=.5,Color="SepiaColor2" },
        new HbrStoryDirection { Op="whiteIn",Duration=3,NoWait=true },new HbrStoryDirection { Op="wait",Duration=2 }
    });
    public static IReadOnlyList<HbrStoryDirection> RecallEnding { get; } = Array.AsReadOnly(new[]
    {
        new HbrStoryDirection { Op="wait",Duration=.2 },new HbrStoryDirection { Op="whiteOut",Duration=2 },
        new HbrStoryDirection { Op="sepia",Gray=0,Sepia=0,Color="SepiaColor2" },
        new HbrStoryDirection { Op="effectRemove",Id="recollect" },new HbrStoryDirection { Op="scene" },
        new HbrStoryDirection { Op="wait",Duration=1 },new HbrStoryDirection { Op="whiteIn",Duration=2,NoWait=true },
        new HbrStoryDirection { Op="wait",Duration=1 }
    });
    private static IReadOnlyList<HbrStoryDirectionDescriptor> Build()
    {
        var rows = new List<HbrStoryDirectionDescriptor> { D("scene", "scene", false, false) };
        foreach (string op in new[] { "fadeOut", "fadeIn", "whiteOut", "whiteIn" })
            rows.Add(D(op, "transition", true, true, Time(1), B("noWait", false)));
        rows.AddRange(new[] {
            D("wait", "transition", true, true, N("duration", 0, 0, 600, true)),
            D("crossFade", "transition", false, true, Time(.01)),
            D("show", "character", false, false, Target, Time(.5)),
            D("hide", "character", false, false, Target, Time()),
            D("moveBy", "character", false, false, Target, X, Y, N("z",0,-10000,10000),Time(),N("easeIn",1,0,10),N("easeOut",1,0,10)),
            D("moveTo", "character", false, false, Target, X, Y, N("z",0,-10000,10000),Time(),N("easeIn",1,0,10),N("easeOut",1,0,10)),
            D("scale", "character", false, false, Target,Zoom(),Time()),
            D("alpha", "character", false, false, Target,N("opacity",1,0,1,true),Time()),
            D("appear", "character", false, false, Target,X,Y,new("scale","number",Minimum:.01,Maximum:16,DefaultFrom:"current.scale"),Time(.3),N("blur",1,0,32)),
            D("swelling", "character", false, false, Target,X,Y,Zoom(1.1),Time(.3),N("blur",1.5,0,32),N("disappearDuration",.3) with { DefaultFrom="duration" }),
            D("shake", "motion", false, true, S("target","screen"),Time(1),N("amplitude",1,0,10)),
            D("shakeXY", "motion", false, true, S("target","screen"),Time(1),N("amplitudeX",1,0,10),N("amplitudeY",1,0,10)),
            D("stopShake", "motion", false, true, S("target","screen")),
            D("stageFixBg", "camera", false, false, Position()),
            D("zoomIn", "camera", false, false, Position(),Time(3),Zoom(1.1)),
            D("zoomOut", "camera", false, false, Position(),Time(3),Zoom(1.1)),
            D("panL", "camera", false, false, Position("BG_LEFT"),Time(3),N("offset",-100,-10000,10000)),
            D("panR", "camera", false, false, Position("BG_RIGHT"),Time(3),N("offset",-100,-10000,10000)),
            D("stagePan", "camera", false, false, Position(),Time(3),N("offset",-80,-10000,10000)),
            D("sepia", "filter", false, false, N("gray",0,0,1),N("sepia",0,0,1),S("color","SepiaColor2",false,"SepiaColor1","SepiaColor2"),new("rgb","rgb"),Time()),
            D("vignette", "filter", false, false, N("intensity",0,0,1,true),Time()),
            D("chromaticAberration", "filter", false, false, N("intensity",0,0,4,true),Time()),
            D("blur", "filter", false, false, S("scope","background",false,"background","foreground","character"),S("target"),N("blur",0,0,32,true),Time()),
            D("effect", "effect", false, false, S("name",required:true),S("id",required:true),S("layer","ForegroundEffect",false,"ForegroundEffect","BG"),X,Y,Zoom()),
            D("effectRemove", "effect", false, false, S("id",required:true)),
            D("cutIn", "cutIn", true, true, S("character",required:true),S("outfit","Default"),S("expression",required:true),S("text",""),S("voice",""),Time(2.5),N("offset",0,-1920,1920),S("image")),
            D("bgm", "audio", false, false, Cue(),N("fadeOut",.5),N("fadeIn",.5) with { DefaultFrom="fadeOut" },N("volume",1,0,2)),
            D("bgmStop", "audio", false, false, N("fadeOut",.5)),
            D("se", "audio", false, true, Cue(),N("volume",1,0,2)),
            D("env", "audio", false, false, Cue(),N("fadeIn",.5),N("volume",1,0,2)),
            D("envStop", "audio", false, false, N("fadeOut",.5)),
            D("bubbleSeActNoWait", "bubble", false, true, S("text",required:true),S("target"),X,Y,Zoom(.7),Cue(false)),
            D("bubbleTalkAsync", "bubble", false, true, Target,S("text",required:true),X,Y,S("voice","")),
            D("bubbleAngerAsync", "bubble", false, true, Target,S("text",required:true),X,Y,S("voice","")),
            D("miniCutIn", "cutIn", true, true, S("character",required:true),new("index","integer",true,1,1,5),S("text",""),S("voice",""),S("bubble","Normal",false,"Normal","Shout"),S("image")),
            D("showStill", "still", false, false, S("image",required:true),Time(),S("direction","none",false,"none","left","right","up","down"),S("motion","none",false,"none","zoomIn","zoomOut"),N("offset",200,-2000,2000)),
            D("hideStill", "still", false, false, Time(),S("direction","none",false,"none","left","right","up","down"),S("motion","none",false,"none","zoomIn","zoomOut"),N("offset",200,-2000,2000)),
            D("showBlackTextInMiddle", "text", true, true, S("text",required:true),N("fadeIn",.5),N("fadeOut",.5)),
            D("zoomShakeWithEffect", "composite", false, false, Target,S("expression")),
            D("showEffectLineWork", "composite", false, false, S("id","lineWork"),S("target","screen"),Zoom(.8),Cue(false)),
            D("blink", "face", false, false, Target,B("enabled",true)),
            D("lipSync", "face", false, false, Target,B("enabled",true))
        });
        return rows.AsReadOnly();
    }

    public static HbrStoryDirectionDescriptor Find(string op) => Descriptors.FirstOrDefault(d => d.Op == op);
    public static object DefaultValue(string op, string field) => Find(op)?.Parameters.FirstOrDefault(p => p.Name == field)?.DefaultValue;
    public static double Duration(HbrStoryDirection direction) => direction.GetNumber("duration");
    public static void Validate(HbrStoryDirection direction, string source, IReadOnlyCollection<string> characters = null)
    {
        if (direction == null || Find(direction.Op) is not { } definition) throw new InvalidOperationException(source + ": unknown direction.");
        foreach (var field in HbrStoryDirection.Properties)
        {
            if (field.Key == "op") continue;
            object value = field.Value.GetValue(direction);
            HbrStoryDirectionParameter parameter = definition.Parameters.FirstOrDefault(p => p.Name == field.Key);
            if (value != null && parameter == null) throw new InvalidOperationException(source + ": " + direction.Op + " does not accept " + field.Key);
            if (parameter == null) continue;
            if (parameter.Required && value == null) throw new InvalidOperationException(source + ": missing " + field.Key);
            if (value == null) continue;
            if (value is double number && (!double.IsFinite(number) || number < parameter.Minimum || number > parameter.Maximum))
                throw new InvalidOperationException(source + ": out-of-range " + field.Key);
            if (value is int integer && (integer < parameter.Minimum || integer > parameter.Maximum))
                throw new InvalidOperationException(source + ": out-of-range " + field.Key);
            if (value is string text)
            {
                if (text.Length > 8192 || text.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')) throw new InvalidOperationException(source + ": invalid " + field.Key);
                if (parameter.Required && string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException(source + ": empty " + field.Key);
                if (parameter.Values != null && !parameter.Values.Contains(text, StringComparer.Ordinal)) throw new InvalidOperationException(source + ": unknown " + field.Key);
            }
        }
        foreach (string field in new[] { "cue", "voice" }) if (direction.GetValue(field) is string cue && cue.Length > 0) HbrStoryResources.CheckCue(cue, source);
        if (direction.Image != null) HbrStoryResources.CheckImage(direction.Image, source);
        foreach (string field in new[] { "id", "character", "outfit", "expression" })
            if (direction.GetValue(field) is string id && !Regex.IsMatch(id, "^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException(source + ": invalid " + field);
        if (direction.Name != null && !Regex.IsMatch(direction.Name, "^[A-Za-z0-9_]+(?:/[A-Za-z0-9_]+){0,8}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException(source + ": invalid effect name.");
        if (direction.Rgb is { } rgb && new[] { rgb.R, rgb.G, rgb.B }.Any(v => !double.IsFinite(v) || v < 0 || v > 8))
            throw new InvalidOperationException(source + ": RGB components must be finite 0–8.");
        if (direction.Color != null && direction.Rgb != null) throw new InvalidOperationException(source + ": choose color preset or rgb, not both.");
        if (direction.Op == "blur" && direction.GetText("scope") == "character" && string.IsNullOrEmpty(direction.Target))
            throw new InvalidOperationException(source + ": character blur requires target.");
        if (direction.Target != null && direction.Target is not ("screen" or "background" or "foreground") &&
            (!Regex.IsMatch(direction.Target, "^[A-Z][A-Z0-9_]{0,79}$", RegexOptions.CultureInvariant) ||
             characters != null && !characters.Contains(direction.Target))) throw new InvalidOperationException(source + ": unknown character target " + direction.Target);
        if (definition.Category == "character" || direction.Op is "blink" or "lipSync" or "zoomShakeWithEffect" or "bubbleTalkAsync" or "bubbleAngerAsync")
            if (direction.Target is "screen" or "background" or "foreground") throw new InvalidOperationException(source + ": this instruction needs a character target.");
    }
}
