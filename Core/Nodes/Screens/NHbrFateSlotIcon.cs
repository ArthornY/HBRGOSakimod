using Godot;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// One socket of the fate dice: a fragment inside a ring in its category colour (design v3, section 5:
// 「五类用外圈颜色区分：区间金、稳定蓝、特殊点数紫、赌一把红、流派银」), an empty socket, a socket taken by a
// crack (the dice's deep red crack drawn across it), or a socket not opened yet (a boss opens one more).
// The manual page and the merchant's fragment stand both use it.
public partial class NHbrFateSlotIcon : Control
{
    internal enum Look
    {
        Fragment,
        Empty,
        Cracked,
        Locked
    }

    private static readonly Color FragmentFill = new(0.03f, 0.07f, 0.12f, 0.85f);
    private static readonly Color EmptyFill = new(0.04f, 0.118f, 0.18f, 0.45f);
    private static readonly Color EmptyEdge = new(0.518f, 0.914f, 0.941f, 0.55f);
    private static readonly Color CrackFill = new(0.1f, 0.02f, 0.03f, 0.85f);
    private static readonly Color CrackRim = new(0.56f, 0.09f, 0.13f, 0.9f);
    private static readonly Color LockedEdge = new(0.86f, 0.92f, 0.93f, 0.25f);

    private Look _look;
    private Color _rim = Colors.White;
    private int _crackSeed;
    private bool _dim;
    private Texture2D _fragment;

    internal static NHbrFateSlotIcon Create(Look look, float size, Color? rim = null, int crackSeed = 0) => new()
    {
        Name = "SlotIcon",
        CustomMinimumSize = new Vector2(size, size),
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
        MouseFilter = MouseFilterEnum.Ignore,
        _look = look,
        _rim = rim ?? Colors.White,
        _crackSeed = crackSeed
    };

    internal static NHbrFateSlotIcon ForFragment(HbrFateFragment fragment, float size) =>
        Create(Look.Fragment, size, RimColor(fragment));

    internal static Color RimColor(HbrFateFragment fragment) =>
        fragment == null ? Colors.White : new Color(HbrFateDiceCatalog.CategoryColor(fragment.Category));

    // Sold or out of reach: drawn at half strength.
    internal bool Dim
    {
        get => _dim;
        set
        {
            if (_dim == value) return;
            _dim = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        if (_look == Look.Fragment) _fragment = HbrEquipmentArt.Load(HbrMagicSwordArt.FateFragmentIcon);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float side = Mathf.Min(Size.X, Size.Y);
        if (side <= 0f) return;
        Vector2 centre = Size / 2f;
        float radius = side / 2f;
        float ring = Mathf.Max(2f, side * 0.055f);
        float strength = _dim ? 0.45f : 1f;
        switch (_look)
        {
            case Look.Fragment:
                DrawCircle(centre, radius - ring * 0.5f, Fade(FragmentFill, strength), true, -1f, true);
                DrawArc(centre, radius - ring * 0.5f, 0f, Mathf.Tau, 64, Fade(_rim, 0.35f * strength), ring * 2.2f, true);
                if (_fragment != null)
                {
                    float inner = radius * 0.86f;
                    DrawTextureRect(_fragment, new Rect2(centre - new Vector2(inner, inner), new Vector2(inner, inner) * 2f),
                        false, new Color(1f, 1f, 1f, strength));
                }
                DrawArc(centre, radius - ring * 0.5f, 0f, Mathf.Tau, 64, Fade(_rim, strength), ring, true);
                break;
            case Look.Empty:
                DrawCircle(centre, radius - ring, EmptyFill, true, -1f, true);
                DashedRing(centre, radius - ring * 0.5f, ring * 0.6f, EmptyEdge);
                break;
            case Look.Cracked:
                DrawCircle(centre, radius - ring * 0.5f, CrackFill, true, -1f, true);
                DrawArc(centre, radius - ring * 0.5f, 0f, Mathf.Tau, 64, CrackRim, ring, true);
                Rect2 area = new(centre - new Vector2(radius, radius), new Vector2(side, side));
                float width = Mathf.Max(1.5f, side * 0.045f);
                HbrFateDiceCracks.Draw(this, area, _crackSeed * 2, width);
                HbrFateDiceCracks.Draw(this, area, _crackSeed * 2 + 1, width);
                break;
            default:
                DashedRing(centre, radius - ring * 0.5f, ring * 0.5f, LockedEdge);
                Padlock(centre, radius * 0.42f, LockedEdge);
                break;
        }
    }

    private void DashedRing(Vector2 centre, float radius, float width, Color color)
    {
        const int dashes = 12;
        float step = Mathf.Tau / dashes;
        for (int k = 0; k < dashes; k++)
        {
            float start = k * step - Mathf.Pi / 2f;
            DrawArc(centre, radius, start, start + step * 0.55f, 8, color, width, true);
        }
    }

    private void Padlock(Vector2 centre, float size, Color color)
    {
        Vector2 body = new(size, size * 0.78f);
        Vector2 top = centre + new Vector2(-size / 2f, -size * 0.12f);
        DrawRect(new Rect2(top, body), color);
        DrawArc(centre + new Vector2(0f, -size * 0.12f), size * 0.32f, Mathf.Pi, Mathf.Tau, 16, color,
            Mathf.Max(1.5f, size * 0.13f), true);
    }

    private static Color Fade(Color color, float strength) => new(color.R, color.G, color.B, color.A * strength);
}
