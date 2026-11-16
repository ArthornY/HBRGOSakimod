using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using HBRGOSakiMod.Core.Story;
using FileAccess = Godot.FileAccess;

namespace HBRGOSakiMod.Core.Nodes.Events.Story;

// Face crops share the base portrait's source-pixel coordinate system. No data
// means no overlays and no change to the artist's original eyes or mouth.
internal sealed partial class NLibraryStoryFace : Control
{
    private sealed record Patch(TextureRect Image, float X, float Y, float Width, float Height);
    private static readonly Dictionary<string,HbrStoryFaceDefinition> Definitions=new(StringComparer.Ordinal);
    internal static HbrStoryFaceDefinition ReadDefinition(string path)
    {
        if(Definitions.TryGetValue(path,out var cached))return cached;Definitions[path]=null;
        if(!FileAccess.FileExists(path))return null;
        try{return Definitions[path]=HbrStoryFaceDefinition.Parse(FileAccess.GetFileAsString(path),path);}
        catch(Exception error)when(error is JsonException or InvalidOperationException or ArgumentException){GD.PushWarning("[HBR story] Invalid face metadata "+path+": "+error.Message);return null;}
    }
    private readonly Random _random = new();
    private readonly List<Patch> _mouth = new();
    private Patch _eyes;
    private double _blinkIn, _closed, _blinkMinimum=2, _blinkMaximum=6, _blinkDuration=.12;
    private float _sourceWidth, _sourceHeight;
    private bool _flip;
    internal bool BlinkEnabled { get; set; } = true;
    internal bool LipSyncEnabled { get; set; } = true;
    internal static NLibraryStoryFace TryCreate(string portrait, Texture2D texture)
    {
        string path = portrait[..portrait.LastIndexOf('.')] + ".face.json";
        HbrStoryFaceDefinition definition=ReadDefinition(path);if(definition==null)return null;
        NLibraryStoryFace face=null;
        try
        {
            definition.ValidateBounds(portrait,texture.GetWidth(),texture.GetHeight(),p=>
            {
                if(!ResourceLoader.Exists(p))return (0,0);
                var crop=ResourceLoader.Load<Texture2D>(p);return crop==null?(0,0):(crop.GetWidth(),crop.GetHeight());
            });
            face=new NLibraryStoryFace { MouseFilter=MouseFilterEnum.Ignore,_sourceWidth=texture.GetWidth(),_sourceHeight=texture.GetHeight(),
                _blinkMinimum=definition.BlinkMinimum,_blinkMaximum=definition.BlinkMaximum,_blinkDuration=definition.BlinkDuration };
            if(definition.EyesClosed!=null)face._eyes=face.ReadPatch(definition.EyesClosed,portrait);
            foreach(var patch in definition.Mouth)face._mouth.Add(face.ReadPatch(patch,portrait));
            face._blinkIn=face._blinkMinimum+face._random.NextDouble()*(face._blinkMaximum-face._blinkMinimum);
            return face;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        { face?.Free();GD.PushWarning("[HBR story] Ignoring invalid face patch " + path + ": " + error.Message); return null; }
    }
    private Patch ReadPatch(HbrStoryFacePatch patch,string portrait)
    {
        string path=HbrStoryFaceDefinition.ResolveImage(portrait,patch.Image);
        var texture=ResourceLoader.Load<Texture2D>(path)??throw new InvalidOperationException("Missing face crop texture.");
        var image=new TextureRect {Texture=texture,Visible=false,MouseFilter=MouseFilterEnum.Ignore,
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale};
        AddChild(image);return new Patch(image,patch.X,patch.Y,texture.GetWidth(),texture.GetHeight());
    }
    internal void SetSharedMaterial(ShaderMaterial material)
    {foreach(var patch in _mouth.Concat(_eyes==null?Array.Empty<Patch>():new[]{_eyes}))patch.Image.Material=material;}
    internal void Layout(Vector2 size, bool flip)
    {
        Size = size; _flip = flip;
        foreach (Patch patch in _mouth.Concat(_eyes == null ? Array.Empty<Patch>() : new[] { _eyes }))
        {
            float x = flip ? _sourceWidth - patch.X - patch.Width : patch.X;
            patch.Image.Position = new Vector2(x * size.X / _sourceWidth, patch.Y * size.Y / _sourceHeight);
            patch.Image.Size = new Vector2(patch.Width * size.X / _sourceWidth, patch.Height * size.Y / _sourceHeight);
            patch.Image.FlipH = _flip;
        }
    }
    internal void Tick(double delta, bool speaking, float amplitude)
    {
        if (_eyes != null)
        {
            if (!BlinkEnabled) _closed = 0;
            else if (_closed > 0) _closed = Math.Max(0, _closed - delta);
            else if ((_blinkIn -= delta) <= 0) { _closed = _blinkDuration; _blinkIn = _blinkMinimum + _random.NextDouble() * (_blinkMaximum - _blinkMinimum); }
            _eyes.Image.Visible = _closed > 0;
        }
        int index = speaking && LipSyncEnabled && amplitude > .015f ? (amplitude > .2f ? 2 : amplitude > .07f ? 1 : 0) : -1;
        for (int i = 0; i < _mouth.Count; i++) _mouth[i].Image.Visible = i == index;
    }
}
