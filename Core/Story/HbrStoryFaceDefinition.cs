#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Story;

public sealed class HbrStoryFacePatch
{
    public required string Image { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
}
/// <summary>Adjacent X.face.json: transparent, same-camera face crops in original-image pixel coordinates.</summary>
public sealed class HbrStoryFaceDefinition
{
    public HbrStoryFacePatch EyesClosed { get; init; }
    public IReadOnlyList<HbrStoryFacePatch> Mouth { get; init; } = Array.Empty<HbrStoryFacePatch>();
    public double BlinkMinimum { get; init; } = 2;
    public double BlinkMaximum { get; init; } = 6;
    public double BlinkDuration { get; init; } = .12;
    public static HbrStoryFaceDefinition Parse(string json,string source="face")
    {
        using var doc=JsonDocument.Parse(json);
        void Unique(JsonElement value)
        {
            if(value.ValueKind==JsonValueKind.Object)
            { var names=new HashSet<string>(StringComparer.Ordinal);foreach(var field in value.EnumerateObject()) { if(!names.Add(field.Name))throw new InvalidOperationException(source+": duplicate field "+field.Name);Unique(field.Value); } }
            else if(value.ValueKind==JsonValueKind.Array) foreach(var child in value.EnumerateArray())Unique(child);
        }
        Unique(doc.RootElement);
        var result=JsonSerializer.Deserialize<HbrStoryFaceDefinition>(json,new JsonSerializerOptions
        { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidOperationException(source+": missing object.");
        result.Validate(source);return result;
    }
    public void Validate(string source="face")
    {
        if(Mouth==null || Mouth.Count is not (0 or 3))throw new InvalidOperationException(source+": mouth must contain exactly three patches or be omitted.");
        if(!double.IsFinite(BlinkMinimum)||!double.IsFinite(BlinkMaximum)||!double.IsFinite(BlinkDuration)||BlinkMinimum<.1||BlinkMaximum<BlinkMinimum||BlinkMaximum>120||BlinkDuration<.01||BlinkDuration>2)
            throw new InvalidOperationException(source+": invalid blink timing.");
        foreach(var patch in Mouth.Concat(EyesClosed==null?Array.Empty<HbrStoryFacePatch>():new[]{EyesClosed}))
        {
            if(patch==null||patch.X<0||patch.Y<0||patch.X>16384||patch.Y>16384)throw new InvalidOperationException(source+": invalid patch coordinates.");
            ResolveImage("res://images/example.png",patch.Image);
        }
    }
    public static string ResolveImage(string originalImage,string image)
    {
        HbrStoryResources.CheckImage(originalImage,"face original");
        if(image==null)throw new InvalidOperationException("Missing face patch image.");
        string resolved=image.StartsWith("res://",StringComparison.Ordinal)?image:originalImage[..(originalImage.LastIndexOf('/')+1)]+image;
        HbrStoryResources.CheckImage(resolved,"face patch");return resolved;
    }
    public IReadOnlyList<HbrStoryResourceRef> Collect(string originalImage) =>
        Mouth.Concat(EyesClosed==null?Array.Empty<HbrStoryFacePatch>():new[]{EyesClosed})
        .Select(p=>new HbrStoryResourceRef(HbrStoryResourceKind.Image,ResolveImage(originalImage,p.Image))).Distinct().ToArray();
    public void ValidateBounds(string originalImage,int width,int height,Func<string,(int Width,int Height)> patchSize)
    {
        Validate();if(width<=0||height<=0)throw new InvalidOperationException("Invalid face base dimensions.");
        foreach(var p in Mouth.Concat(EyesClosed==null?Array.Empty<HbrStoryFacePatch>():new[]{EyesClosed}))
        { var size=patchSize(ResolveImage(originalImage,p.Image));if(size.Width<=0||size.Height<=0||(long)p.X+size.Width>width||(long)p.Y+size.Height>height)throw new InvalidOperationException("Face patch lies outside original image."); }
    }
}
