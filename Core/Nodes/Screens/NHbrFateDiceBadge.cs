using Godot;
using HBRGOSakiMod.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The fate dice on its manual page: the dice icon with one deep red crack drawn over it per crack
// (design v3, section 5), up to HbrFateDiceCracks.MaxDrawn.
public partial class NHbrFateDiceBadge : Control
{
    private Texture2D _dice;
    private int _cracks;

    internal int Cracks
    {
        get => _cracks;
        set
        {
            if (_cracks == value) return;
            _cracks = value;
            QueueRedraw();
        }
    }

    internal static NHbrFateDiceBadge Create(float size, int cracks) => new()
    {
        Name = "DiceBadge",
        CustomMinimumSize = new Vector2(size, size),
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
        MouseFilter = MouseFilterEnum.Ignore,
        _cracks = cracks
    };

    public override void _Ready()
    {
        _dice = HbrEquipmentArt.Load(HbrMagicSwordArt.FateDiceIcon);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float side = Mathf.Min(Size.X, Size.Y);
        Rect2 area = new((Size - new Vector2(side, side)) / 2f, new Vector2(side, side));
        if (_dice != null) DrawTextureRect(_dice, area, false);
        float width = Mathf.Max(1.5f, side * 0.018f);
        int drawn = Mathf.Min(_cracks, HbrFateDiceCracks.MaxDrawn);
        for (int i = 0; i < drawn; i++) HbrFateDiceCracks.Draw(this, area, i, width);
    }
}
