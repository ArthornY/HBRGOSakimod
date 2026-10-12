using Godot;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NHBRGOSakiCreatureVisuals : NCreatureVisuals
{
    private const string NormalPortraitPath =
        "res://images/characters/hbrgo_saki_normal.png";
    private const string DualSwordsPortraitPath =
        "res://images/characters/hbrgo_saki_dual_swords.png";
    private const string ScythePortraitPath =
        "res://images/characters/hbrgo_saki_scythe.png";
    private const string ThirtyAPortraitPath =
        "res://images/characters/hbrgo_saki_30a.png";
    private const string BowPortraitPath =
        "res://images/characters/hbrgo_saki_bow.png";
    private const string NarbyPortraitPath =
        "res://images/characters/hbrgo_saki_narby.png";
    private const float DefaultPortraitReferenceHeight = 1700f;
    private const float BowPortraitReferenceHeight = 1536f;
    private const float DualSwordsPortraitReferenceHeight = 941f;
    private const float DualSwordsPortraitScaleMultiplier = 1.6f;
    // Torso center in the artwork; the sword tips are not the character's center.
    private const float DualSwordsBodyCenterX = 0.64f;

    private Sprite2D _sprite;
    private Vector2 _baseSpriteScale;
    private Vector2 _baseSpritePosition;
    private PortraitMode _currentMode = PortraitMode.Unset;

    public override void _Ready()
    {
        base._Ready();
        _sprite = GetNode<Sprite2D>("%Visuals");
        _baseSpriteScale = _sprite.Scale;
        _baseSpritePosition = _sprite.Position;
        ApplyPortraitTransform(PortraitMode.Normal);
        RefreshPortrait();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshPortrait();
    }

    internal void ShowNarbyPortrait()
    {
        _sprite ??= GetNodeOrNull<Sprite2D>("%Visuals");
        if (_sprite == null)
        {
            return;
        }

        _currentMode = PortraitMode.Narby;
        _sprite.Texture = ResourceLoader.Load<Texture2D>(NarbyPortraitPath);
        ApplyPortraitTransform(PortraitMode.Narby);
    }

    private void RefreshPortrait()
    {
        Creature creature = (GetParent() as NCreature)?.Entity;
        if (creature == null || _sprite == null)
        {
            return;
        }

        PortraitMode mode = GetPortraitMode(creature);
        if (mode == _currentMode)
        {
            return;
        }

        _currentMode = mode;
        _sprite.Texture = mode switch
        {
            PortraitMode.Bow =>
                ResourceLoader.Load<Texture2D>(BowPortraitPath),
            PortraitMode.Narby =>
                ResourceLoader.Load<Texture2D>(NarbyPortraitPath),
            PortraitMode.DualSwords =>
                ResourceLoader.Load<Texture2D>(DualSwordsPortraitPath),
            PortraitMode.ThirtyA =>
                ResourceLoader.Load<Texture2D>(ThirtyAPortraitPath),
            PortraitMode.Scythe =>
                ResourceLoader.Load<Texture2D>(ScythePortraitPath),
            _ => ResourceLoader.Load<Texture2D>(NormalPortraitPath)
        };
        ApplyPortraitTransform(mode);
    }

    private void ApplyPortraitTransform(PortraitMode mode)
    {
        _sprite.Scale = _baseSpriteScale;
        _sprite.Position = _baseSpritePosition;
        if (_sprite.Texture == null || mode == PortraitMode.Narby)
        {
            return;
        }

        // Preserve the pre-replacement display size when loading higher-resolution artwork.
        float referenceHeight = mode switch
        {
            PortraitMode.Bow => BowPortraitReferenceHeight,
            PortraitMode.DualSwords => DualSwordsPortraitReferenceHeight,
            _ => DefaultPortraitReferenceHeight
        };
        float scaleMultiplier = referenceHeight / _sprite.Texture.GetHeight();
        if (mode == PortraitMode.DualSwords)
        {
            scaleMultiplier *= DualSwordsPortraitScaleMultiplier;
        }

        _sprite.Scale = _baseSpriteScale * scaleMultiplier;
        _sprite.Position = GetPortraitPosition(mode, _baseSpritePosition,
            _sprite.Texture.GetWidth(), _sprite.Scale.X);
    }

    private static Vector2 GetPortraitPosition(PortraitMode mode, Vector2 basePosition,
        float textureWidth, float scaleX) => mode == PortraitMode.DualSwords
        ? new Vector2(-(DualSwordsBodyCenterX - 0.5f) * textureWidth * scaleX, basePosition.Y)
        : basePosition;

    private static PortraitMode GetPortraitMode(Creature creature)
    {
        if (creature.Player?.Character is HbrgoSaki30A)
        {
            return PortraitMode.ThirtyA;
        }

        if (creature.HasPower<RaptureScythePower>())
        {
            return PortraitMode.Scythe;
        }

        if (creature.HasPower<ReptureBowPower>())
        {
            return PortraitMode.Bow;
        }

        if (creature.HasPower<ReptureDualSwordsPower>() ||
            creature.HasPower<MagicSwordModePower>() ||
            creature.HasPower<CrimsonBladeFlowPower>())
        {
            return PortraitMode.DualSwords;
        }

        return PortraitMode.Normal;
    }

    private enum PortraitMode
    {
        Unset,
        Narby,
        ThirtyA,
        Normal,
        DualSwords,
        Scythe,
        Bow
    }
}
