using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// One slash of Magic Sword Lucky Seven: a tapered streak across a target that shoots out along its line, then
// fades. Drawn in code (design v3: 「斩光：程序画」) with additive blending, so on the dimmed battle it glows and
// over a bright enemy it only brightens. Crimson and violet alternate; the big slash stands for every hit past
// the seventh.
public partial class NHbrLuckySevenSlash : Control
{
    private static CanvasItemMaterial _additive;

    private Color _color;
    private float _angle;
    private float _length;
    private float _width;
    private float _life;
    private float _speed = 1f;
    private float _age;

    internal static NHbrLuckySevenSlash Create(Vector2 centre, int slash, bool big, float speed)
    {
        (float X, float Y) offset = HbrLuckySevenTimeline.SlashOffset(slash);
        NHbrLuckySevenSlash node = new()
        {
            Name = $"Slash{slash}",
            MouseFilter = MouseFilterEnum.Ignore,
            Position = centre + new Vector2(offset.X, offset.Y),
            Material = Additive,
            _color = new Color(HbrLuckySevenTimeline.SlashColor(slash)),
            _angle = Mathf.DegToRad(HbrLuckySevenTimeline.SlashAngle(slash, big)),
            _length = big ? HbrLuckySevenTimeline.BigSlashLength : HbrLuckySevenTimeline.SlashLength,
            _width = big ? HbrLuckySevenTimeline.BigSlashWidth : HbrLuckySevenTimeline.SlashWidth,
            _life = big ? HbrLuckySevenTimeline.BigSlashLife : HbrLuckySevenTimeline.SlashLife,
            _speed = speed
        };
        return node;
    }

    private static CanvasItemMaterial Additive =>
        GodotObject.IsInstanceValid(_additive)
            ? _additive
            : _additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _age += (float)delta * _speed;
        if (_age >= _life)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float p = _age / _life;
        // Shoots out over the first third, then fades while staying at full length.
        float reach = 1f - (1f - Mathf.Clamp(p / 0.32f, 0f, 1f)) * (1f - Mathf.Clamp(p / 0.32f, 0f, 1f));
        float fade = p < 0.42f ? 1f : 1f - (p - 0.42f) / 0.58f;
        Vector2 direction = new(Mathf.Cos(_angle), Mathf.Sin(_angle));
        Vector2 normal = new(-direction.Y, direction.X);
        Vector2 start = -direction * _length * 0.5f;
        Vector2 end = start + direction * _length * reach;
        Vector2 middle = (start + end) * 0.5f;
        // Glow first, then the hot core: each layer a thin diamond that tapers to both ends.
        for (int layer = 2; layer >= 0; layer--)
        {
            float width = _width * (layer == 0 ? 0.55f : layer == 1 ? 1.6f : 3.4f) * (0.6f + 0.4f * reach);
            Color colour = layer == 0 ? Colors.White.Lerp(_color, 0.18f) : _color;
            colour.A = fade * (layer == 0 ? 1f : layer == 1 ? 0.55f : 0.22f);
            DrawColoredPolygon(new[] { start, middle + normal * width, end, middle - normal * width }, colour);
        }
    }
}
