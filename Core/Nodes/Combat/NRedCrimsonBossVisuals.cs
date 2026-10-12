using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NRedCrimsonBossVisuals : NStaticHbrgoMonsterVisuals
{
    private const string IntangibleTexturePath =
        "monsters/hbrgo_act3/red_crimson_slippery.png";
    private const string ExposedTexturePath =
        "monsters/hbrgo_act3/red_crimson_exposed.png";
    private const string IntangibleBackgroundPath =
        "res://images/backgrounds/hbrgo_red_crimson/red_crimson_intangible.png";
    private const string IntangibleShellBrokenBackgroundPath =
        "res://images/backgrounds/hbrgo_red_crimson/red_crimson_intangible_shell_broken.png";
    private const string ExposedBackgroundPath =
        "res://images/backgrounds/hbrgo_red_crimson/red_crimson_exposed.png";
    private const string ExposedShellBrokenBackgroundPath =
        "res://images/backgrounds/hbrgo_red_crimson/red_crimson_exposed_shell_broken.png";
    private const float HealthBarLeftRatio = 0.46f;
    private const float HealthBarTopRatio = 0.3101f;
    private const float HitboxHorizontalPadding = 24f;
    private const float HitboxTopPadding = 52f;
    private const float HitboxBottomPadding = 24f;
    private const float IntentOffset = 90f;
    private const float IntangibleHorizontalOffsetAt1920 = 150f;
    private const float IntangibleAlliedFloorRatio = 0.99f;
    private const float ExposedAlliedFloorRatio = 0.93f;
    private const float AlliedStatusBarHeadGap = 50f;

    internal const float FixedHealthBarWidth = 375f;

    private Texture2D _intangibleTexture;
    private Texture2D _exposedTexture;
    private bool? _showingIntangible;
    private Sprite2D _combatBackdrop;
    private TextureRect _splitCombatBackdrop;
    private Texture2D _combatBackdropTexture;
    private string _combatBackdropPath;

    public override void _Ready()
    {
        base._Ready();
        _intangibleTexture = ResourceLoader.Load<Texture2D>(
            ImageHelper.GetImagePath(IntangibleTexturePath));
        _exposedTexture = ResourceLoader.Load<Texture2D>(
            ImageHelper.GetImagePath(ExposedTexturePath));
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshForm();
    }

    private void RefreshForm()
    {
        var creature = (GetParent() as NCreature)?.Entity;
        if (creature?.Monster is not RedCrimsonBossMonster)
        {
            return;
        }

        bool shouldShowIntangible = creature.HasPower<IntangiblePower>();
        bool hasHighDimensionalShell =
            creature.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m;
        NCombatRoom room = GetCombatRoom();
        if (room?.SceneContainer != null)
        {
            GetCurrentBody().Visible = false;
            UpdateCombatBackdrop(
                room,
                shouldShowIntangible,
                hasHighDimensionalShell);
            UpdateAlliedCombatLayout(
                room,
                shouldShowIntangible
                    ? IntangibleAlliedFloorRatio
                    : ExposedAlliedFloorRatio,
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
            _showingIntangible = shouldShowIntangible;
            return;
        }

        GetCurrentBody().Visible = true;
        if (_showingIntangible == shouldShowIntangible)
        {
            return;
        }

        _showingIntangible = shouldShowIntangible;
        SetVisualTexture(
            shouldShowIntangible
                ? _intangibleTexture
                : _exposedTexture);
    }

    private void UpdateCombatBackdrop(
        NCombatRoom room,
        bool shouldShowIntangible,
        bool hasHighDimensionalShell)
    {
        Control backgroundContainer =
            GetSceneBossBackdropContainer(room);
        if (backgroundContainer == null)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_combatBackdrop))
        {
            _combatBackdrop = new Sprite2D
            {
                Name = "RedCrimsonCombatBackdrop",
                Centered = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear
            };
            ConfigureSceneBossBackdropLayer(_combatBackdrop, room);
            backgroundContainer.AddChild(_combatBackdrop);
        }

        bool useSplitLayout = HasAlliedAndEnemySceneBosses(room);
        if (useSplitLayout &&
            !GodotObject.IsInstanceValid(_splitCombatBackdrop))
        {
            _splitCombatBackdrop = new TextureRect
            {
                Name = "RedCrimsonSplitCombatBackdrop",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            _splitCombatBackdrop.SetAnchorsPreset(
                Control.LayoutPreset.TopLeft);
            ConfigureSceneBossBackdropLayer(
                _splitCombatBackdrop,
                room);
            backgroundContainer.AddChild(_splitCombatBackdrop);
        }

        if (room.Background != null)
        {
            room.Background.Visible = false;
        }

        string nextPath = GetCombatBackdropPath(
            shouldShowIntangible,
            hasHighDimensionalShell);
        if (_combatBackdropPath != nextPath)
        {
            Texture2D texture = ResourceLoader.Load<Texture2D>(nextPath);
            if (texture != null)
            {
                _combatBackdrop.Texture = texture;
                _combatBackdropTexture = texture;
                _combatBackdropPath = nextPath;
            }
        }

        Texture2D backdropTexture = _combatBackdropTexture;
        if (backdropTexture == null)
        {
            return;
        }

        ConfigureSceneBossBackdropLayer(_combatBackdrop, room);
        if (GodotObject.IsInstanceValid(_splitCombatBackdrop))
        {
            ConfigureSceneBossBackdropLayer(
                _splitCombatBackdrop,
                room);
        }

        if (useSplitLayout)
        {
            _combatBackdrop.Visible = false;
            _splitCombatBackdrop.Visible = true;
            UpdateSceneBossBackdropLayout(
                _splitCombatBackdrop,
                room,
                backdropTexture);
            return;
        }

        _combatBackdrop.Visible = true;
        if (GodotObject.IsInstanceValid(_splitCombatBackdrop))
        {
            _splitCombatBackdrop.Visible = false;
        }

        Rect2 sceneRect = GetSceneBossBackdropRect(room);
        Vector2 sceneSize = sceneRect.Size;
        float backdropScale = Mathf.Max(
            sceneSize.X / backdropTexture.GetWidth(),
            sceneSize.Y / backdropTexture.GetHeight());
        float horizontalOverflow = Mathf.Max(
            0f,
            (backdropTexture.GetWidth() * backdropScale -
                sceneSize.X) * 0.5f);
        float desiredOffset = shouldShowIntangible
            ? sceneSize.X / 1920f *
                IntangibleHorizontalOffsetAt1920
            : 0f;
        float horizontalOffset = Mathf.Min(
            desiredOffset,
            horizontalOverflow);
        if (IsAlliedSummon)
        {
            horizontalOffset *= -1f;
        }

        _combatBackdrop.FlipH = IsAlliedSummon;
        _combatBackdrop.Scale = Vector2.One * backdropScale;
        _combatBackdrop.GlobalPosition =
            sceneRect.GetCenter() +
            Vector2.Right * horizontalOffset;
    }

    private static string GetCombatBackdropPath(
        bool shouldShowIntangible,
        bool hasHighDimensionalShell)
    {
        if (shouldShowIntangible)
        {
            return hasHighDimensionalShell
                ? IntangibleBackgroundPath
                : IntangibleShellBrokenBackgroundPath;
        }

        return hasHighDimensionalShell
            ? ExposedBackgroundPath
            : ExposedShellBrokenBackgroundPath;
    }
}
