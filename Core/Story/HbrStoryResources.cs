#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Story;

[JsonConverter(typeof(JsonStringEnumConverter<HbrStoryResourceKind>))]
public enum HbrStoryResourceKind { Image, AudioCue, Effect, Face, CutIn, MiniCutIn }
public sealed record HbrStoryResourceRef(HbrStoryResourceKind Kind, string Key, bool Optional = false);

/// <summary>Typed logical references. Cue/effect/cut-in IDs must be resolved by their resource providers, not ResourceLoader.</summary>
public static class HbrStoryResources
{
    public static void CheckCue(string cue, string source)
    {
        if (string.IsNullOrEmpty(cue) || cue.Length > 256 || !Regex.IsMatch(cue,@"\A[A-Za-z0-9_]+\z",RegexOptions.CultureInvariant))
            throw new InvalidOperationException(source + ": audio cue must be a name, not a path.");
    }
    public static void CheckImage(string path,string source)
    {
        if (path == null || !path.StartsWith("res://images/",StringComparison.Ordinal) || path.Contains('\\') ||
            path[6..].Contains(':') || path.Any(char.IsControl) || path[6..].Split('/').Any(p => p is "" or "." or "..") ||
            !new[] { ".png",".jpg",".jpeg",".webp",".svg" }.Contains(System.IO.Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(source + ": expected an image inside res://images/.");
    }
    public static IReadOnlyList<HbrStoryResourceRef> Collect(HbrStoryPresentation presentation,IEnumerable<HbrStoryLine> lines)
    {
        var refs=new List<HbrStoryResourceRef>();
        void Image(string path,bool face=false)
        {
            if (string.IsNullOrEmpty(path)) return;
            refs.Add(new(HbrStoryResourceKind.Image,path));
            if (face) refs.Add(new(HbrStoryResourceKind.Face,path[..path.LastIndexOf('.')] + ".face.json",true));
        }
        void Audio(string cue) { if (!string.IsNullOrEmpty(cue)) refs.Add(new(HbrStoryResourceKind.AudioCue,cue)); }
        if (presentation != null) { Image(presentation.Background);Image(presentation.ChoicePortrait);Image(presentation.ThoughtPortrait); }
        foreach (var line in lines ?? Array.Empty<HbrStoryLine>())
        {
            Image(line.Background);Image(line.Portrait);Audio(line.Voice);
            foreach(var actor in line.Characters ?? Array.Empty<HbrStoryCharacter>()) Image(actor.Image,true);
            foreach(var direction in line.Direction ?? Array.Empty<HbrStoryDirection>())
            {
                Image(direction.Image);Audio(direction.Cue);Audio(direction.Voice);
                if(direction.Op=="effect") refs.Add(new(HbrStoryResourceKind.Effect,direction.Name));
                if(direction.Op is "zoomShakeWithEffect" or "showEffectLineWork") refs.Add(new(HbrStoryResourceKind.Effect,"Adventure/LineWork/LineWork"));
                if(direction.Op=="cutIn" && string.IsNullOrEmpty(direction.Image)) refs.Add(new(HbrStoryResourceKind.CutIn,$"{direction.Character}/{direction.GetText("outfit")}/{direction.Expression}"));
                if(direction.Op=="miniCutIn" && string.IsNullOrEmpty(direction.Image)) refs.Add(new(HbrStoryResourceKind.MiniCutIn,$"{direction.Character}/{direction.GetNumber("index"):00}"));
            }
        }
        return refs.Distinct().ToArray();
    }
    public static IReadOnlyList<HbrStoryResourceRef> Collect(HbrStoryDefinition definition) =>
        Collect(definition.Presentation,definition.Pages.SelectMany(page => new[] { new HbrStoryLine
        { Kind="narration",Text="resource scene",Background=page.Background,Characters=page.Characters } }.Concat(page.Lines)));

    public static IReadOnlyList<string> Missing(IEnumerable<HbrStoryResourceRef> references,Func<HbrStoryResourceRef,bool> exists) =>
        references.Where(r=>!r.Optional && !exists(r)).Select(r=>$"{r.Kind}: {r.Key}").ToArray();
}
