using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The radial burst behind the home buttons: a soft cyan glow, 200 seeded spokes and four dashed
// rings, turning once every 160 seconds like the preview's SVG.
public partial class NManualBurst : Control
{
    private const float Half = 330f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        PivotOffset = Size / 2f;
        Gradient gradient = new()
        {
            Offsets = new[] { 0f, 0.5f, 1f },
            Colors = new[] { new Color("63e2ea42"), new Color("3b7fbf1a"), new Color(0f, 0f, 0f, 0f) }
        };
        AddChild(new TextureRect
        {
            Name = "Glow",
            Size = Size,
            Texture = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 256,
                Height = 256,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f)
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            ShowBehindParent = true
        });
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) Rotation += (float)(Mathf.Tau / HbrManualPhoneLayout.BurstPeriod * delta);
    }

    public override void _Draw()
    {
        float scale = Size.X / (Half * 2f);
        Vector2 centre = Size / 2f;
        Color cyan = new(HbrManualPhoneLayout.Cyan2);
        Color violet = new("b48cff");
        foreach (var (angle, from, to, isViolet, alpha, width) in HbrManualPhoneLayout.BurstSpokes())
        {
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawLine(centre + direction * from * scale, centre + direction * to * scale,
                new Color(isViolet ? violet : cyan, alpha), width, true);
        }
        Color ring = new("8fe9f0");
        foreach (var (radius, alpha, dash, gap) in HbrManualPhoneLayout.BurstRings)
        {
            float r = radius * scale;
            int dashes = Mathf.Max(1, Mathf.FloorToInt(Mathf.Tau * r / ((dash + gap) * scale)));
            float step = Mathf.Tau / dashes;
            float sweep = step * dash / (dash + gap);
            for (int i = 0; i < dashes; i++)
                DrawArc(centre, r, i * step, i * step + sweep, 3, new Color(ring, alpha), 3f * scale, true);
        }
    }
}
