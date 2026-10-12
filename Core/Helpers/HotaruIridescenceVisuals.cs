using System.Runtime.CompilerServices;
using Godot;

namespace HBRGOSakiMod.Core.Helpers;

// GPU TIME animates the ribbon; only native relic refresh events bind materials.
internal static class HotaruIridescenceVisuals
{
    private const string MaterialPath = "res://materials/relics/hotaru_iridescence.tres";
    private static readonly ConditionalWeakTable<TextureRect, Binding> Bindings = new();
    private static readonly StringName[] NativeParameters = { "pulse", "is_used", "is_wax" };
    private static ShaderMaterial _template;
    private static bool _reportedMissingMaterial;

    private sealed class Binding
    {
        internal Material Original;
        internal ShaderMaterial Animated;
    }

    internal static void Apply(TextureRect texture)
    {
        if (!GodotObject.IsInstanceValid(texture)) return;
        if (Bindings.TryGetValue(texture, out Binding binding) &&
            texture.Material == binding.Animated && GodotObject.IsInstanceValid(binding.Animated)) return;

        ShaderMaterial template = GetTemplate();
        if (template == null) return;

        // Each icon needs independent native pulse/disabled/wax parameters.
        // The shader is shared, while the small mutable material stays local.
        var animated = (ShaderMaterial)template.Duplicate();
        CopyNativeParameters(texture.Material, animated);
        Bindings.Remove(texture);
        Bindings.Add(texture, new Binding { Original = texture.Material, Animated = animated });
        texture.Material = animated;
    }

    internal static void Restore(TextureRect texture)
    {
        if (!GodotObject.IsInstanceValid(texture) || !Bindings.TryGetValue(texture, out Binding binding)) return;
        if (texture.Material == binding.Animated)
        {
            // UpdateTexture has already written the newly selected relic's state.
            CopyNativeParameters(binding.Animated, binding.Original);
            texture.Material = GodotObject.IsInstanceValid(binding.Original) ? binding.Original : null;
        }
        Bindings.Remove(texture);
    }

    private static ShaderMaterial GetTemplate()
    {
        if (GodotObject.IsInstanceValid(_template) && GodotObject.IsInstanceValid(_template.Shader)) return _template;
        _template = ResourceLoader.Load<ShaderMaterial>(MaterialPath);
        if (GodotObject.IsInstanceValid(_template) && GodotObject.IsInstanceValid(_template.Shader)) return _template;
        if (!_reportedMissingMaterial)
        {
            _reportedMissingMaterial = true;
            GD.PushWarning("Hotaru Iridescence material could not be loaded; keeping the static rainbow icon.");
        }
        return null;
    }

    private static void CopyNativeParameters(Material source, Material destination)
    {
        if (source is not ShaderMaterial from || destination is not ShaderMaterial to ||
            !GodotObject.IsInstanceValid(from) || !GodotObject.IsInstanceValid(to)) return;
        foreach (StringName parameter in NativeParameters)
            to.SetShaderParameter(parameter, from.GetShaderParameter(parameter));
    }
}
