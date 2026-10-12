using Godot;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Uses the supplied image with its original transparency in every UI.</summary>
internal static class HbrMemoryGardenIconHelper
{
    internal const string IconPath = "res://images/ui/memory_garden.png";
    private static Texture2D _texture;

    internal static Texture2D GetTexture()
    {
        if (!GodotObject.IsInstanceValid(_texture))
            _texture = ResourceLoader.Load<Texture2D>(IconPath);
        return GodotObject.IsInstanceValid(_texture) ? _texture : null;
    }

    internal static void Apply(TextureRect icon)
    {
        if (!GodotObject.IsInstanceValid(icon)) return;
        Texture2D texture = GetTexture();
        if (!GodotObject.IsInstanceValid(texture)) return;

        icon.Texture = texture;
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }
}
