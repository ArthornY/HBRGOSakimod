using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Story;

namespace HBRGOSakiMod.Core.Nodes.Events.Story;

// Resolve logical references before a reader is attached. This is asset work
// only; it cannot start sounds, instantiate a scene, or change story progress.
internal static class NLibraryStoryRuntimeResources
{
    internal static IEnumerable<string> Collect(HbrStoryPresentation presentation,IEnumerable<HbrStoryLine> lines,Func<string,string> audioPath=null)
    {var authored=lines.ToArray();return Collect(HbrStoryResources.Collect(presentation,authored),authored,audioPath);}
    internal static IEnumerable<string> Collect(HbrStoryDefinition definition,Func<string,string> audioPath=null)=>
        Collect(HbrStoryResources.Collect(definition),definition.Pages.SelectMany(page=>page.Lines),audioPath);
    private static IEnumerable<string> Collect(IEnumerable<HbrStoryResourceRef> references,IEnumerable<HbrStoryLine> lines,Func<string,string> audioPath)
    {
        var paths=new HashSet<string>(StringComparer.Ordinal);
        void Add(string path){if(!string.IsNullOrEmpty(path))paths.Add(path);}
        foreach(var reference in references)
        {
            switch(reference.Kind)
            {
                case HbrStoryResourceKind.Image:Add(reference.Key);break;
                case HbrStoryResourceKind.AudioCue:Add(audioPath?.Invoke(reference.Key));break;
                case HbrStoryResourceKind.Effect:foreach(var path in NLibraryStoryEffect.CollectImagePaths(reference.Key))Add(path);break;
                case HbrStoryResourceKind.CutIn:Add(NLibraryStoryVisualResources.Resolve("cutins",reference.Key));break;
                case HbrStoryResourceKind.MiniCutIn:Add(NLibraryStoryVisualResources.Resolve("miniCutins",reference.Key));break;
                case HbrStoryResourceKind.Face:
                    var face=NLibraryStoryFace.ReadDefinition(reference.Key);
                    if(face!=null)foreach(var crop in face.Collect(reference.Key[..^10]+".png"))Add(crop.Key);
                    break;
            }
        }
        foreach(var line in lines)
        foreach(var direction in line.Direction)
        {
            if(direction.Op is "bubbleTalkAsync" or "bubbleAngerAsync" or "miniCutIn" or "cutIn")
            {
                foreach(string key in new[]{"bubbleTalk","bubbleAngry","bubbleNozzle","bubbleNozzleAngry"})Add(NLibraryStoryVisualResources.Image(key));
                Add(NLibraryStoryVisualResources.FontPath("dialogue"));
            }
            if(direction.Op=="cutIn"&&!string.IsNullOrEmpty(direction.Image))
                foreach(string key in new[]{"cutinFrame","cutinMask","cutinBackground"})Add(NLibraryStoryVisualResources.Image(key));
        }
        return paths.ToArray();
    }
}
