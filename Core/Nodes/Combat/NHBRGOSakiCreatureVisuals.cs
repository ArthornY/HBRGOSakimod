using Godot;
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
    private const string BowPortraitPath =
        "res://images/characters/hbrgo_saki_bow.png";
    private const string NarbyPortraitPath =
        "res://images/characters/hbrgo_saki_narby.png";

    private Sprite2D _sprite;
    private PortraitMode _currentMode = PortraitMode.Unset;

    public override void _Ready()
    {
        base._Ready();
        _sprite = GetNode<Sprite2D>("%Visuals");
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
        _sprite.Texture = ResourceLoader.Load<Texture2D>(
            mode switch
            {
                PortraitMode.Narby => NarbyPortraitPath,
                PortraitMode.DualSwords => DualSwordsPortraitPath,
                PortraitMode.Scythe => ScythePortraitPath,
                PortraitMode.Bow => BowPortraitPath,
                _ => NormalPortraitPath
            });
    }

    private static PortraitMode GetPortraitMode(Creature creature)
    {
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
        Normal,
        DualSwords,
        Scythe,
        Bow
    }
}
