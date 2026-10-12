using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NSixSenseBadge : Control
{
    private static ShaderMaterial _lockedMaterial;
    private Texture2D _atlas;
    private SixSenseAttribute _attribute;
    private bool _isLocked;

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value && (!value || GodotObject.IsInstanceValid(Material))) return;
            _isLocked = value;
            Material = value ? GetLockedMaterial() : null;
            QueueRedraw();
        }
    }

    private static ShaderMaterial GetLockedMaterial()
    {
        if (!GodotObject.IsInstanceValid(_lockedMaterial) ||
            !GodotObject.IsInstanceValid(_lockedMaterial.Shader))
            _lockedMaterial = new ShaderMaterial
            {
                Shader = new Shader
                {
                    // CanvasItem's fragment COLOR already includes the atlas
                    // sample and vertex tint. Keep its alpha and silver rim.
                    Code = "shader_type canvas_item; void fragment() { " +
                        "float grey = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114)); " +
                        "COLOR.rgb = vec3(grey); }"
                }
            };
        return _lockedMaterial;
    }

    public SixSenseAttribute Attribute
    {
        get => _attribute;
        set { if (_attribute == value) return; _attribute = value; QueueRedraw(); }
    }

    public override void _Ready()
    {
        SixSenseBadgeHelper.TryGetAtlas(ref _atlas);
        TextureFilter = TextureFilterEnum.Linear;
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0 || !SixSenseBadgeHelper.TryGetAtlas(ref _atlas)) return;
        Vector2[] points = SixSenseBadgeHelper.Hexagon(Size);
        DrawPolygon(points, new[] { Colors.White }, SixSenseBadgeHelper.AtlasUvs(Attribute), _atlas);
        for (int i = 0; i < points.Length; i++)
            DrawLine(points[i], points[(i + 1) % points.Length], new Color("BEBEBE"), .65f, true);
    }
}
