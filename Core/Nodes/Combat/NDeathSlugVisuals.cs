using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NDeathSlugVisuals : NStaticHbrgoMonsterVisuals
{
    private const string PhaseOneBackgroundPath =
        "res://images/backgrounds/hbrgo_death_slug/death_slug_phase1.png";
    private const string PhaseTwoBackgroundPath =
        "res://images/backgrounds/hbrgo_death_slug/death_slug_phase2.png";
    private const float HealthBarLeftRatio = 0.34f;
    private const float HealthBarTopRatio = 0.30f;
    private const float HitboxHorizontalPadding = 24f;
    private const float HitboxTopPadding = 52f;
    private const float HitboxBottomPadding = 24f;
    private const float IntentOffset = 90f;
    private const float AlliedFloorRatio = 0.85f;
    private const float AlliedStatusBarHeadGap = 50f;
    private const float SplitBackdropBodyCenterRatio = 0.5f;

    internal const float FixedHealthBarWidth = 375f;

    private Texture2D _phaseOneTexture;
    private Texture2D _phaseTwoTexture;
    private int _displayedPhase;
    private TextureRect _combatBackdrop;

    public override void _Ready()
    {
        base._Ready();
        _phaseOneTexture = ResourceLoader.Load<Texture2D>(
            PhaseOneBackgroundPath);
        _phaseTwoTexture = ResourceLoader.Load<Texture2D>(
            PhaseTwoBackgroundPath);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshPhase();
    }

    private void RefreshPhase()
    {
        var creature = (GetParent() as NCreature)?.Entity;
        if (creature?.Monster is not DeathSlug deathSlug)
        {
            return;
        }

        int phase = deathSlug.Phase;
        NCombatRoom room = GetCombatRoom();
        if (room?.SceneContainer != null)
        {
            GetCurrentBody().Visible = false;
            UpdateCombatBackdrop(room, phase);
            UpdateAlliedCombatLayout(
                room,
                AlliedFloorRatio,
                AlliedStatusBarHeadGap);
            UpdateSceneBossCombatLayout(
                room,
                HealthBarLeftRatio,
                HealthBarTopRatio,
                FixedHealthBarWidth,
                HitboxHorizontalPadding,
                HitboxTopPadding,
                HitboxBottomPadding,
                IntentOffset);
            _displayedPhase = phase;
            return;
        }

        GetCurrentBody().Visible = true;
        if (_displayedPhase == phase)
        {
            return;
        }

        _displayedPhase = phase;
        SetVisualTexture(
            phase == 2
                ? _phaseTwoTexture
                : _phaseOneTexture);
    }

    private void UpdateCombatBackdrop(NCombatRoom room, int phase)
    {
        Control backgroundContainer =
            GetSceneBossBackdropContainer(room);
        if (backgroundContainer == null)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_combatBackdrop))
        {
            _combatBackdrop = new TextureRect
            {
                Name = "DeathSlugPhaseBackdrop",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            _combatBackdrop.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            ConfigureSceneBossBackdropLayer(_combatBackdrop, room);
            backgroundContainer.AddChild(_combatBackdrop);
        }

        if (room.Background != null)
        {
            room.Background.Visible = false;
        }

        Texture2D texture = phase == 2
            ? _phaseTwoTexture
            : _phaseOneTexture;
        if (texture == null)
        {
            return;
        }

        UpdateSceneBossBackdropLayout(
            _combatBackdrop,
            room,
            texture,
            SplitBackdropBodyCenterRatio);
    }
}
