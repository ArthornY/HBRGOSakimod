using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace HBRGOSakiMod.Core.Nodes.Events.Story;

// Every original effect resolves its own rendered frames or original particle
// texture in the manifest. Missing effects are skipped, never substituted by a
// visually unrelated generic image.
internal sealed partial class NLibraryStoryEffect : Control
{
    private Texture2D[] _frames = Array.Empty<Texture2D>();
    private TextureRect _image;
    private Texture2D _particleTexture;
    private int _particleCount;
    private double _age;
    private double _fps = 24, _life = 5;
    private float _particleSize = 30;
    private bool _loop;
    private string _kind;
    private int _frame = -1;
    private readonly Random _random = new();
    private readonly List<(Vector2 Origin, Vector2 Drift, float Phase)> _particles = new();
    private static Dictionary<string,JsonElement> _catalog;
    private static Dictionary<string,JsonElement> Catalog
    {
        get
        {
            if(_catalog!=null)return _catalog;_catalog=new(StringComparer.Ordinal);
            const string path="res://data/story_effects.json";if(!FileAccess.FileExists(path))return _catalog;
            using var json=JsonDocument.Parse(FileAccess.GetFileAsString(path));
            if(json.RootElement.GetProperty("schemaVersion").GetInt32()!=1)return _catalog;
            foreach(var entry in json.RootElement.GetProperty("effects").EnumerateObject())_catalog[entry.Name]=entry.Value.Clone();
            return _catalog;
        }
    }
    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
    internal string EffectName { get; private set; }
    internal static NLibraryStoryEffect Create(string name)
    {
        try
        {
            if (!Catalog.TryGetValue(name, out JsonElement row)) return Missing(name);
            var effect = new NLibraryStoryEffect { EffectName = name, MouseFilter = MouseFilterEnum.Ignore,
                _kind = Text(row, "kind", "sequence"), _fps = Number(row, "fps", 24), _loop = Flag(row, "loop", false) };
            effect.Size = new Vector2(1600, 900);
            if (row.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Array && size.GetArrayLength() == 2)
                effect.Size = new Vector2(size[0].GetSingle(), size[1].GetSingle());
            if (effect._kind == "sequence")
            {
                effect._frames = row.GetProperty("frames").EnumerateArray().Select(v => Load(v.GetString())).ToArray();
                if (effect._frames.Length == 0 || effect._frames.Any(f => f == null)) { effect.Free(); return Missing(name); }
                effect._image = new TextureRect { Texture = effect._frames[0], Size = effect.Size,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = MouseFilterEnum.Ignore, UseParentMaterial = true };
                effect.AddChild(effect._image);
            }
            else if (effect._kind is "particles" or "recollect")
            {
                effect._particleTexture = Load(Text(row,"texture", ""));
                if (effect._particleTexture == null) { effect.Free(); return Missing(name); }
                // Original Recollect uses two independently phased groups of 40,
                // 30 px particles, five-second life and zero initial velocity.
                effect._particleCount = (int)Math.Clamp(Number(row,"count",80),1,160);
                effect._particleSize = (float)Number(row,"particleSize",30);
                effect._life = Math.Max(.1,Number(row,"lifetime",5)); effect._loop = true;
                for (int i=0;i<effect._particleCount;i++)
                    effect._particles.Add((new Vector2((float)effect._random.NextDouble()*1600,(float)effect._random.NextDouble()*900),Vector2.Zero,(float)effect._random.NextDouble()));
            }
            else if (effect._kind == "texture")
            {
                var texture = Load(Text(row,"texture", ""));
                if (texture == null) { effect.Free(); return Missing(name); }
                effect._image = new TextureRect { Texture = texture, Size = effect.Size,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = MouseFilterEnum.Ignore, UseParentMaterial = true };
                effect.AddChild(effect._image); effect._loop = true;
            }
            else { effect.Free(); return Missing(name); }
            if (Text(row,"blend","") is "add" or "additive") effect.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            return effect;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        { if (Warned.Add(name)) GD.PushWarning("[HBR story] Invalid effect " + name + ": " + error.Message); return null; }
    }
    private static NLibraryStoryEffect Missing(string name)
    { if (Warned.Add(name)) GD.PushWarning("[HBR story] Missing original effect resources: " + name); return null; }
    private static Texture2D Load(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("res://images/",StringComparison.Ordinal) && !path.Contains("..",StringComparison.Ordinal) && ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    private static string Text(JsonElement row,string key,string fallback) => row.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.String ? v.GetString() : fallback;
    private static double Number(JsonElement row,string key,double fallback) => row.TryGetProperty(key,out var v) && v.TryGetDouble(out double n) && double.IsFinite(n) ? n : fallback;
    private static bool Flag(JsonElement row,string key,bool fallback) => row.TryGetProperty(key,out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;
    internal static IEnumerable<string> CollectImagePaths(string name)
    {
        JsonElement row;
        try{if(!Catalog.TryGetValue(name,out row))return Array.Empty<string>();}
        catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException){return Array.Empty<string>();}
        var paths=new List<string>();
        if(row.TryGetProperty("texture",out var texture)&&texture.ValueKind==JsonValueKind.String)paths.Add(texture.GetString());
        if(row.TryGetProperty("frames",out var frames)&&frames.ValueKind==JsonValueKind.Array)paths.AddRange(frames.EnumerateArray().Where(f=>f.ValueKind==JsonValueKind.String).Select(f=>f.GetString()));
        return paths.Where(p=>!string.IsNullOrEmpty(p)&&p.StartsWith("res://images/",StringComparison.Ordinal)&&!p.Contains("..",StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
    }
    internal void Restart(){_age=0;_frame=-1;Visible=true;}
    internal void Tick(double delta)
    {
        _age += Math.Max(0,delta);
        if (_frames.Length>0)
        {
            int frame=(int)(_age*Math.Max(1,_fps));
            if (!_loop && frame>=_frames.Length) { Visible=false; return; }
            frame%=_frames.Length;
            if (frame!=_frame) { _frame=frame; _image.Texture=_frames[frame]; }
        }
        if (_particles.Count>0) QueueRedraw();
    }
    public override void _Draw()
    {
        if (_particleTexture==null) return;
        foreach(var particle in _particles)
        {
            float age=(float)((_age/_life+particle.Phase)%1);
            float alpha=MathF.Sin(age*MathF.PI)*.65f;
            Vector2 point=particle.Origin+particle.Drift*age;
            DrawTextureRect(_particleTexture,new Rect2(point-Vector2.One*_particleSize/2,Vector2.One*_particleSize),false,new Color(1,1,1,alpha));
        }
    }
}
