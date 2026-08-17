using System;
using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NRedCrimsonCreatureVisuals : NCreatureVisuals
{
    private const float BaseVisualScale = 0.18f;
    private const float VisualCenterX = 140f;
    private const float RaisedVisualBottomY = -60f;

    // 75% of the previous level-one bar width, including the game's padding.
    internal const float FixedHealthBarWidth = 326.124f;

    private static readonly float[] _levelScaleMultipliers =
    {
        1f,
        1.2f,
        1.4f,
        1.6f,
        1.8f,
        2f,
        2.5f
    };

    private bool _hasAppliedLevelVisuals;

    public override void _Ready()
    {
        base._Ready();
        ApplyLevelVisuals();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_hasAppliedLevelVisuals)
        {
            ApplyLevelVisuals();
        }
    }

    private void ApplyLevelVisuals()
    {
        RedCrimson redCrimson =
            (GetParent() as NCreature)?.Entity?.Monster as RedCrimson;
        Sprite2D sprite = GetNodeOrNull<Sprite2D>("%Visuals");
        if (redCrimson == null || sprite?.Texture == null)
        {
            return;
        }

        int level = Math.Clamp(redCrimson.SummonLevel, 0, 6);
        float multiplier = _levelScaleMultipliers[level];
        float visualScale = BaseVisualScale * multiplier;
        float halfWidth = sprite.Texture.GetWidth() * visualScale * 0.5f;
        float halfHeight = sprite.Texture.GetHeight() * visualScale * 0.5f;
        float centerY = RaisedVisualBottomY - halfHeight;

        sprite.Scale = new Vector2(-visualScale, visualScale);
        sprite.Position = new Vector2(VisualCenterX, centerY);

        Marker2D center = GetNodeOrNull<Marker2D>("%CenterPos");
        Marker2D intent = GetNodeOrNull<Marker2D>("%IntentPos");
        Marker2D talk = GetNodeOrNull<Marker2D>("%TalkPos");
        Control bounds = GetNodeOrNull<Control>("%Bounds");

        if (center != null)
        {
            center.Position = sprite.Position;
        }

        float topY = centerY - halfHeight;
        if (intent != null)
        {
            intent.Position = new Vector2(VisualCenterX, topY - 20f);
        }

        if (talk != null)
        {
            talk.Position = new Vector2(VisualCenterX, topY - 10f);
        }

        if (bounds != null)
        {
            bounds.Position = new Vector2(
                VisualCenterX - halfWidth,
                topY);
            bounds.Size = new Vector2(
                halfWidth * 2f,
                halfHeight * 2f);
        }

        _hasAppliedLevelVisuals = true;
    }
}
