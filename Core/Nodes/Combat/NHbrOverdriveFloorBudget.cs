using System;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// UIBattleOverdrive2 presents its usage allowance with filled/empty tokens.
// Keep the original 26-pixel icon, 0.9 scale and 27-pixel spacing, extending
// the original nine-token row to the mod's twelve shared floor levels.
public partial class NHbrOverdriveFloorBudget : Control
{
    private const string Art = "res://images/ui/overclock/original/";
    internal const float TokenSize = 26f * 0.9f;
    internal const float TokenSpacing = 27f;
    internal static float RowWidth => (Overclock.FloorLevelLimit - 1) * TokenSpacing + TokenSize;

    private readonly TextureRect[] _tokens = new TextureRect[Overclock.FloorLevelLimit];
    private Texture2D _filled;
    private Texture2D _empty;
    private int _remaining = Overclock.FloorLevelLimit;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(RowWidth, TokenSize);
        _filled = ResourceLoader.Load<Texture2D>(Art + "IconOverDriveToken.png");
        _empty = ResourceLoader.Load<Texture2D>(Art + "IconOverDriveTokenEmpty.png");
        for (int i = 0; i < _tokens.Length; i++)
        {
            _tokens[i] = new TextureRect
            {
                Name = $"FloorLevel{i + 1}",
                Position = new Vector2(i * TokenSpacing, 0f),
                Size = Vector2.One * TokenSize,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_tokens[i]);
        }
        RefreshTokens();
    }

    public void SetRemaining(int remaining)
    {
        int next = Math.Clamp(remaining, 0, Overclock.FloorLevelLimit);
        if (_remaining == next) return;
        _remaining = next;
        RefreshTokens();
    }

    private void RefreshTokens()
    {
        if (_tokens[0] == null) return;
        for (int i = 0; i < _tokens.Length; i++)
            _tokens[i].Texture = i < _remaining ? _filled : _empty;
    }
}
