#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HBRGOSakiMod.Core.Story;

public enum HbrStoryPlaybackMode { Normal, FastForward, Restore }
public sealed record HbrStoryResolvedScene(string Background, IReadOnlyList<HbrStoryCharacter> Characters,
    bool ChangeBackground = true, bool ChangeCharacters = true);
public sealed record HbrStoryActorState(string Id, string Image, double X, double Y, double Height,
    bool FlipX, double Opacity, double Scale = 1, bool Visible = true, double Blur = 0,
    bool BlinkEnabled = true, bool LipSyncEnabled = true, string Expression = "", double Z = 0,
    string VoiceCharacter = "");
public sealed record HbrStoryCameraState(string Position = "BG_CENTER", double Offset = 0, double Zoom = 1);
public sealed record HbrStoryFilterState(double Gray = 0, double Sepia = 0, HbrStoryRgb Color = null,
    double Vignette = 0, double ChromaticAberration = 0, double BackgroundBlur = 0, double ForegroundBlur = 0);
public sealed record HbrStoryEffectState(string Id, string Name, string Layer = "ForegroundEffect",
    double X = 0, double Y = 0, double Scale = 1, string Target = "screen");
public sealed record HbrStoryStillState(string Image, string Direction = "none", string Motion = "none",
    double Offset = 200, double Scale = 1.05);
public sealed record HbrStoryAudioState(string Cue, double Volume = 1);
public sealed record HbrStoryFadeState(string Color = "black", double Opacity = 0);

/// <summary>Immutable value snapshot. It owns copies, never authored mutable lists or renderer nodes.</summary>
public sealed record HbrStorySceneState
{
    public string Background { get; init; } = "";
    public IReadOnlyList<HbrStoryActorState> Actors { get; init; } = Array.Empty<HbrStoryActorState>();
    public HbrStoryCameraState Camera { get; init; } = new();
    public HbrStoryFilterState Filters { get; init; } = new();
    public IReadOnlyDictionary<string, HbrStoryEffectState> Effects { get; init; } =
        new ReadOnlyDictionary<string, HbrStoryEffectState>(new Dictionary<string, HbrStoryEffectState>(StringComparer.Ordinal));
    public HbrStoryStillState Still { get; init; }
    public HbrStoryAudioState Bgm { get; init; }
    public HbrStoryAudioState Env { get; init; }
    public HbrStoryFadeState Fade { get; init; } = new();
    public static HbrStorySceneState Empty => new();
}

public sealed record HbrStoryDirectionStep(HbrStoryDirection Direction, HbrStorySceneState Before,
    HbrStorySceneState After, double Duration, double WaitSeconds, bool Suppressed = false);
public sealed record HbrStoryDirectionPlan(HbrStorySceneState Before, HbrStorySceneState After,
    IReadOnlyList<HbrStoryDirectionStep> Steps, double BlockingDuration);

/// <summary>Pure endpoint reducer. Tween progress and wall-clock scheduling belong to the renderer.</summary>
public static class HbrStoryDirectionReducer
{
    public static double BackgroundOffset(string position) => position switch
    { "BG_LEFT" => -370, "BG_LEFT_CENTER" => -170, "BG_RIGHT_CENTER" => 170, "BG_RIGHT" => 370, _ => 0 };

    /// <summary>Camera offset is in source-background pixels. Return the clamped source-pixel crop origin.</summary>
    public static double BackgroundCropX(double imageWidth, double imageHeight, double viewportWidth,
        double viewportHeight, HbrStoryCameraState camera)
    {
        if (new[] { imageWidth, imageHeight, viewportWidth, viewportHeight }.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ArgumentOutOfRangeException(nameof(imageWidth));
        camera ??= new();
        double sourceWidth = viewportWidth / (viewportHeight / imageHeight * Math.Max(.01, camera.Zoom));
        double maximum = Math.Max(0, imageWidth - sourceWidth);
        return Math.Clamp(maximum / 2 + BackgroundOffset(camera.Position) + camera.Offset, 0, maximum);
    }

    public static HbrStorySceneState Apply(HbrStorySceneState state, HbrStoryDirection direction, HbrStoryResolvedScene scene = null)
    {
        state ??= HbrStorySceneState.Empty;
        HbrStoryDirectionCatalog.Validate(direction, "direction reducer",state.Actors.Select(actor=>actor.Id).ToArray());
        double N(string key, double fallback = 0) => direction.GetNumber(key, fallback);
        string S(string key, string fallback = "") => direction.GetText(key, fallback);
        HbrStorySceneState Actors(Func<HbrStoryActorState, HbrStoryActorState> update) => state with
        { Actors = Array.AsReadOnly(state.Actors.Select(a => a.Id == direction.Target ? update(a) : a).ToArray()) };
        var filters = state.Filters;
        switch (direction.Op)
        {
            case "scene":
                if (scene == null) return state;
                return state with
                {
                    Background = scene.ChangeBackground ? scene.Background ?? "" : state.Background,
                    Actors = scene.ChangeCharacters ? Array.AsReadOnly((scene.Characters ?? Array.Empty<HbrStoryCharacter>())
                        .Select(a => new HbrStoryActorState(a.Id, a.Image, a.X, a.Y, a.Height, a.FlipX, a.Opacity,
                            VoiceCharacter: a.VoiceCharacter)).ToArray()) : state.Actors
                };
            case "fadeOut": return state with { Fade = new("black", 1) };
            case "fadeIn": return state with { Fade = new("black", 0) };
            case "whiteOut": return state with { Fade = new("white", 1) };
            case "whiteIn": return state with { Fade = new("white", 0) };
            case "show": return Actors(a => a with { Visible = true });
            case "hide": return Actors(a => a with { Visible = false });
            case "moveBy": return Actors(a => a with { X = a.X + N("x") * HbrStoryDirectionCatalog.CharacterCoordinateScale, Y = a.Y - N("y") * HbrStoryDirectionCatalog.CharacterCoordinateScale, Z = a.Z + N("z") });
            case "moveTo": return Actors(a => a with { X = 800 + N("x") * HbrStoryDirectionCatalog.CharacterCoordinateScale, Y = 900 - N("y") * HbrStoryDirectionCatalog.CharacterCoordinateScale, Z = N("z") });
            case "scale": return Actors(a => a with { Scale = N("scale", 1) });
            case "alpha": return Actors(a => a with { Opacity = N("opacity", 1) });
            case "appear": return Actors(a => a with { Visible = true, Blur = 0, Scale = N("scale", a.Scale) });
            case "swelling": return Actors(a => a with { Visible = false, Scale = a.Scale * N("scale", 1.1), Blur = N("blur"), X = a.X + N("x") * HbrStoryDirectionCatalog.CharacterCoordinateScale, Y = a.Y - N("y") * HbrStoryDirectionCatalog.CharacterCoordinateScale });
            case "stageFixBg": return state with { Camera = new(S("position"), 0, 1) };
            case "zoomIn": return state with { Camera = new(S("position"), 0, N("scale", 1.1)) };
            case "zoomOut": return state with { Camera = new(S("position"), 0, 1) };
            case "panL": return state with { Camera = new(S("position"), N("offset"), state.Camera.Zoom) };
            case "panR": return state with { Camera = new(S("position"), -N("offset"), state.Camera.Zoom) };
            case "stagePan": return state with { Camera = new(S("position"), N("offset"), state.Camera.Zoom) };
            case "sepia": return state with { Filters = filters with { Gray = N("gray"), Sepia = N("sepia"), Color = direction.Rgb ?? (S("color") == "SepiaColor1" ? new(240d/255,200d/255,146d/255) : new(1.07,.74,.43)) } };
            case "vignette": return state with { Filters = filters with { Vignette = N("intensity") } };
            case "chromaticAberration": return state with { Filters = filters with { ChromaticAberration = N("intensity") } };
            case "blur": return S("scope") switch
            {
                "character" => Actors(a => a with { Blur = N("blur") }),
                "foreground" => state with { Filters = filters with { ForegroundBlur = N("blur") } },
                _ => state with { Filters = filters with { BackgroundBlur = N("blur") } }
            };
            case "effect": return PutEffect(state, new(S("id"), S("name"), S("layer"), N("x") * HbrStoryDirectionCatalog.CharacterCoordinateScale, N("y") * HbrStoryDirectionCatalog.CharacterCoordinateScale, N("scale",1)));
            case "effectRemove":
                var effects = new Dictionary<string,HbrStoryEffectState>(state.Effects, StringComparer.Ordinal); effects.Remove(S("id"));
                return state with { Effects = new ReadOnlyDictionary<string,HbrStoryEffectState>(effects) };
            case "bgm": return state with { Bgm = new(S("cue"),N("volume",1)) };
            case "bgmStop": return state with { Bgm = null };
            case "env": return state with { Env = new(S("cue"),N("volume",1)) };
            case "envStop": return state with { Env = null };
            case "showStill": return state with { Still = new(S("image"),S("direction"),S("motion"),N("offset",200)) };
            case "hideStill": return state with { Still = null };
            case "blink": return Actors(a => a with { BlinkEnabled = direction.GetBool("enabled",true) });
            case "lipSync": return Actors(a => a with { LipSyncEnabled = direction.GetBool("enabled",true) });
            case "zoomShakeWithEffect":
                state = state with { Camera = state.Camera with { Zoom = state.Camera.Zoom * 1.27 } };
                state = state with { Actors = Array.AsReadOnly(state.Actors.Select(a => a.Id == direction.Target ? a with { Scale = a.Scale * 1.1, Expression = direction.Expression ?? a.Expression } : a).ToArray()) };
                return PutEffect(state,new("lineWork_"+direction.Target,"Adventure/LineWork/LineWork",Scale:.8,Target:direction.Target));
            case "showEffectLineWork": return PutEffect(state,new(S("id"),"Adventure/LineWork/LineWork",Scale:N("scale",.8),Target:S("target","screen")));
            default: return state;
        }
    }

    private static HbrStorySceneState PutEffect(HbrStorySceneState state,HbrStoryEffectState effect)
    {
        var values = new Dictionary<string,HbrStoryEffectState>(state.Effects,StringComparer.Ordinal) { [effect.Id] = effect };
        return state with { Effects = new ReadOnlyDictionary<string,HbrStoryEffectState>(values) };
    }

    /// <summary>Back navigation uses a saved endpoint, never re-applies relative moves or transient instructions.</summary>
    public static HbrStoryDirectionPlan Restore(HbrStorySceneState snapshot)
    {
        snapshot ??= HbrStorySceneState.Empty;
        var restored = snapshot.Fade.Color == "white" ? snapshot with { Fade = new("white",0) } : snapshot;
        return new(snapshot,restored,Array.Empty<HbrStoryDirectionStep>(),0);
    }

    public static HbrStoryDirectionPlan Plan(HbrStorySceneState previous, HbrStoryResolvedScene scene,
        HbrStoryLine line, HbrStoryPresentation presentation, HbrStoryPlaybackMode mode = HbrStoryPlaybackMode.Normal)
    {
        previous ??= HbrStorySceneState.Empty;
        var state = mode != HbrStoryPlaybackMode.Normal && previous.Fade.Color == "white"
            ? previous with { Fade = new("white",0) } : previous;
        var steps = new List<HbrStoryDirectionStep>();
        var directions = line.Direction ?? Array.Empty<HbrStoryDirection>();
        var authored = directions.Any(d => d.Op == "scene") ? directions : new[] { new HbrStoryDirection { Op = "scene" } }.Concat(directions);
        foreach (var direction in authored)
        {
            bool suppressed = mode != HbrStoryPlaybackMode.Normal && direction.Op is "shake" or "shakeXY" or "stopShake" or "whiteOut" or "whiteIn" or "se" or "cutIn" or "miniCutIn" or "bubbleSeActNoWait" or "bubbleTalkAsync" or "bubbleAngerAsync" or "showBlackTextInMiddle";
            var next = suppressed ? state : Apply(state,direction,scene);
            double duration = mode == HbrStoryPlaybackMode.Normal ? HbrStoryDirectionCatalog.Duration(direction) : 0;
            if (mode == HbrStoryPlaybackMode.Normal && direction.Op == "scene" && scene?.ChangeCharacters == true)
            {
                bool changedCast = state.Actors.Select(a=>a.Id).Except(next.Actors.Select(a=>a.Id)).Any() || next.Actors.Select(a=>a.Id).Except(state.Actors.Select(a=>a.Id)).Any();
                bool changedImage = state.Actors.Any(a=>next.Actors.Any(b=>a.Id==b.Id&&a.Image!=b.Image));
                duration = Math.Max(changedCast ? presentation.CharacterFadeDuration : 0,changedImage ? presentation.ExpressionFadeDuration : 0);
            }
            double wait = HbrStoryDirectionCatalog.Find(direction.Op).Blocking && !direction.GetBool("noWait") ? duration : 0;
            if (mode != HbrStoryPlaybackMode.Restore) steps.Add(new(direction,state,next,duration,wait,suppressed));
            state = next;
        }
        return new(previous,state,steps.AsReadOnly(),steps.Sum(step => step.WaitSeconds));
    }
}
