using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NHBRGOSakiCreatureVisuals : NCreatureVisuals
{
    private const string NormalPortraitPath =
        "res://images/characters/hbrgo_saki_normal.png";
    private const string CornerNormalPortraitPath =
        "res://images/characters/hbrgo_saki_corner_normal.png";
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
    private Texture2D _nativeTexture;
    private Transform2D _nativeSpriteTransform;
    private Vector2 _nativeBoundsPosition;
    private Vector2 _nativeBoundsSize;
    private readonly Dictionary<Marker2D, Transform2D> _nativeMarkers = new();
    private bool _cornerPresentation;
    private float _cornerHeight;
    private static readonly MethodInfo UpdateCreatureBounds = AccessTools.Method(
        typeof(NCreature), "UpdateBounds", new[] { typeof(Node) });

    internal bool HasCornerPresentation => _cornerPresentation;

    public override void _Ready()
    {
        base._Ready();
        _sprite = GetNode<Sprite2D>("%Visuals");
        _baseSpriteScale = _sprite.Scale;
        _baseSpritePosition = _sprite.Position;
        _nativeBoundsPosition = Bounds.Position;
        _nativeBoundsSize = Bounds.Size;
        foreach (Node child in GetChildren())
            if (child is Marker2D marker) _nativeMarkers.Add(marker, marker.Transform);
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
        _nativeTexture = ResourceLoader.Load<Texture2D>(NarbyPortraitPath);
        _sprite.Texture = _nativeTexture;
        ApplyPortraitTransform(PortraitMode.Narby);
        NSakiCornerCreatureLayout.NotifyPortraitChanged(GetParent() as NCreature);
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
        _nativeTexture = mode switch
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
        _sprite.Texture = _nativeTexture;
        ApplyPortraitTransform(mode);
        NSakiCornerCreatureLayout.NotifyPortraitChanged(GetParent() as NCreature);
    }

    private void ApplyPortraitTransform(PortraitMode mode)
    {
        // Always retain the original mode's transform independently of the
        // corner portrait. Boss formations and phantom anchors still use it.
        _sprite.Texture = _nativeTexture ?? _sprite.Texture;
        _sprite.Scale = _baseSpriteScale;
        _sprite.Position = _baseSpritePosition;
        if (_sprite.Texture == null)
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
        float scaleMultiplier = mode == PortraitMode.Narby
            ? 1f : referenceHeight / _sprite.Texture.GetHeight();
        if (mode == PortraitMode.DualSwords)
        {
            scaleMultiplier *= DualSwordsPortraitScaleMultiplier;
        }

        _sprite.Scale = _baseSpriteScale * scaleMultiplier;
        _sprite.Position = GetPortraitPosition(mode, _baseSpritePosition,
            _sprite.Texture.GetWidth(), _sprite.Scale.X);
        _nativeSpriteTransform = _sprite.Transform;

        if (_cornerPresentation)
        {
            if (mode == PortraitMode.Normal)
                _sprite.Texture = ResourceLoader.Load<Texture2D>(CornerNormalPortraitPath);
            ApplyCornerGeometry(mode);
        }
    }

    internal void SetCornerPresentation(bool enabled, float localHeight = 430f)
    {
        if (_sprite == null ||
            (_cornerPresentation == enabled && (!enabled || Mathf.IsEqualApprox(_cornerHeight, localHeight))))
            return;
        _cornerPresentation = enabled;
        _cornerHeight = localHeight;
        ApplyPortraitTransform(_currentMode);
        if (!enabled)
        {
            Bounds.Position = _nativeBoundsPosition;
            Bounds.Size = _nativeBoundsSize;
            foreach (var pair in _nativeMarkers)
                if (GodotObject.IsInstanceValid(pair.Key)) pair.Key.Transform = pair.Value;
            RefreshCreatureBounds();
        }
    }

    internal bool TryGetNativePortraitRect(out Rect2 bounds)
    {
        bounds = default;
        if (_nativeTexture == null || _sprite == null) return false;
        Vector2 size = _nativeTexture.GetSize();
        Vector2 origin = _sprite.Offset - (_sprite.Centered ? size * 0.5f : Vector2.Zero);
        bounds = GetTransform() * (_nativeSpriteTransform * new Rect2(origin, size));
        return true;
    }

    private void ApplyCornerGeometry(PortraitMode mode)
    {
        // These are the alpha bounds of the existing source PNGs. Keeping the
        // transparent padding out of sizing makes the supplied normal portrait
        // actually 430px tall without decoding textures every rendered frame.
        Rect2 visible = mode switch
        {
            PortraitMode.Normal => new Rect2(11f, 16f, 692f, 1200f),
            PortraitMode.DualSwords => new Rect2(1850f, 184f, 2512f, 2690f),
            PortraitMode.Scythe => new Rect2(712f, 150f, 2327f, 3925f),
            PortraitMode.ThirtyA => new Rect2(712f, 152f, 2327f, 3922f),
            PortraitMode.Bow => new Rect2(933f, 138f, 3163f, 3797f),
            _ => new Rect2(Vector2.Zero, _sprite.Texture.GetSize())
        };
        float scale = _cornerHeight / visible.Size.Y;
        _sprite.Scale = Vector2.One * scale;
        Vector2 textureOrigin = _sprite.Centered ? _sprite.Texture.GetSize() * 0.5f : Vector2.Zero;
        _sprite.Position = (textureOrigin - _sprite.Offset -
            new Vector2(visible.GetCenter().X, visible.End.Y)) * scale;
        Vector2 size = visible.Size * scale;
        Bounds.Position = new Vector2(-size.X * 0.5f, -size.Y);
        Bounds.Size = size;
        // Targeting, hit VFX and speech now refer to the single real body.
        VfxSpawnPosition.Position = new Vector2(0f, -size.Y * 0.55f);
        IntentPosition.Position = new Vector2(0f, -size.Y - 36f);
        if (TalkPosition != null) TalkPosition.Position = new Vector2(size.X * 0.25f, -size.Y * 0.88f);
        if (OrbPosition != null && OrbPosition != IntentPosition)
            OrbPosition.Position = new Vector2(0f, -size.Y - 36f);
        RefreshCreatureBounds();
    }

    private void RefreshCreatureBounds()
    {
        if (GetParent() is NCreature creature && creature.IsNodeReady() && creature.Hitbox != null)
            UpdateCreatureBounds?.Invoke(creature, new object[] { this });
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
