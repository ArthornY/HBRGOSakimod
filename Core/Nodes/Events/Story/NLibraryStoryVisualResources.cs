using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace HBRGOSakiMod.Core.Nodes.Events.Story;

internal static class NLibraryStoryVisualResources
{
    private static JsonElement? _manifest;
    private static bool _read;
    private static JsonElement? Manifest
    {
        get
        {
            if(_read)return _manifest;_read=true;
            const string path="res://data/story_visuals.json";
            if(!FileAccess.FileExists(path))return null;
            try{using var doc=JsonDocument.Parse(FileAccess.GetFileAsString(path));if(doc.RootElement.GetProperty("schemaVersion").GetInt32()==1)_manifest=doc.RootElement.Clone();}
            catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException){GD.PushWarning("[HBR story] Visual manifest invalid: "+error.Message);}
            return _manifest;
        }
    }
    internal static string Image(string key)
    {return Manifest is {} root&&root.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;}
    internal static bool Flag(string key)=>Manifest is {} root&&root.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.True;
    internal static string Resolve(string section,string key)
    {return Manifest is {} root&&root.TryGetProperty(section,out var map)&&map.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;}
    internal static string FontPath(string role)
    {
        if(Manifest is not {} root||!root.TryGetProperty("fonts",out var fonts)||!fonts.TryGetProperty(role,out var font)||!font.TryGetProperty("path",out var value)||value.ValueKind!=JsonValueKind.String)return null;
        string path=value.GetString();
        return path!=null&&path.StartsWith("res://fonts/story/",StringComparison.Ordinal)&&path.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase)&&!path.Contains("..",StringComparison.Ordinal)?path:null;
    }
    internal static Godot.Font Font(string role)
    {string path=FontPath(role);return path!=null&&ResourceLoader.Exists(path)?ResourceLoader.Load<FontFile>(path):null;}
    internal static Control Bubble(bool angry,Vector2 size)
    {
        Texture2D texture=Texture(Image(angry?"bubbleAngry":"bubbleTalk"));if(texture==null)return null;
        const float scale=.25f;var picture=new NinePatchRect{Texture=texture,Size=size/scale,Scale=Vector2.One*scale,MouseFilter=Control.MouseFilterEnum.Ignore};
        if(Manifest is {} root&&root.TryGetProperty(angry?"bubbleAngryBorder":"bubbleTalkBorder",out var border))
        {picture.PatchMarginLeft=(int)border.GetProperty("left").GetDouble();picture.PatchMarginTop=(int)border.GetProperty("top").GetDouble();picture.PatchMarginRight=(int)border.GetProperty("right").GetDouble();picture.PatchMarginBottom=(int)border.GetProperty("bottom").GetDouble();}
        return picture;
    }
    internal static Texture2D Texture(string path)=>!string.IsNullOrEmpty(path)&&path.StartsWith("res://images/",StringComparison.Ordinal)&&!path.Contains("..",StringComparison.Ordinal)&&ResourceLoader.Exists(path)?ResourceLoader.Load<Texture2D>(path):null;
}
