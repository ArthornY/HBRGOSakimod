using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NStaticHbrgoMonsterVisuals : NCreatureVisuals
{
    private const string FlatHandPhaseOneBackgroundPath =
        "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase1.png";
    private const string FlatHandPhaseOneShellBrokenBackgroundPath =
        "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase1_shell_broken.png";
    private const string FlatHandPhaseTwoBackgroundPath =
        "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase2.png";
    private const string FlatHandPhaseTwoShellBrokenBackgroundPath =
        "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase2_shell_broken.png";
    private const string FlatHandPhaseThreeBackgroundPath =
        "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase3.png";
    private const string FlatHandPhaseThreeShellBrokenBackgroundPath =
        "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase3_shell_broken.png";
    private const float FlatHandHealthBarLeftRatio = 0.53125f;
    private const float FlatHandHealthBarTopRatio = 0.40463f;
    private const float FlatHandHitboxHorizontalPadding = 20f;
    private const float FlatHandHitboxTopPadding = 52f;
    private const float FlatHandHitboxBottomPadding = 24f;
    private const float FlatHandIntentOffset = 90f;
    private const float FlatHandAlliedFloorRatio = 0.85f;
    private const float FlatHandPhaseOneSplitCenterRatio = 0.55f;
    private const float FlatHandPhaseTwoSplitCenterRatio = 0.53f;
    private const float FlatHandPhaseThreeSplitCenterRatio = 0.63f;
    private const float AlliedStatusBarHeadGap = 50f;
    private const float EnemyStatusBarHorizontalOffset = 300f;
    internal const float StateDisplayContentGap = 4f;

    internal const float FlatHandHealthBarWidth = 250f;
    internal const float SummonedBossStatusScale = 0.5f;

    [Export]
    public float TargetHeight { get; set; } = 240f;

    [Export]
    public float MaxWidth { get; set; } = 360f;

    [Export]
    public float MinBoundsWidth { get; set; }

    [Export]
    public float VisualBottomY { get; set; } = -12f;

    private Creature _creature;
    private Tween _deathTween;
    private TextureRect _flatHandBackdrop;
    private Texture2D _flatHandBackdropTexture;
    private string _flatHandBackdropPath;

    protected bool IsAlliedSummon => _creature?.PetOwner != null;

    public override void _Ready()
    {
        base._Ready();
        _creature = (GetParent() as NCreature)?.Entity;
        if (_creature?.Monster is FlatHand &&
            GetCombatRoom() != null)
        {
            GetCurrentBody().Visible = false;
        }
        else
        {
            ApplyLayout();
        }

        if (_creature != null)
        {
            _creature.Died += OnDied;
        }
    }

    public override void _ExitTree()
    {
        if (_creature != null)
        {
            _creature.Died -= OnDied;
        }

        _deathTween?.Kill();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (_creature?.Monster is not FlatHand flatHand)
        {
            return;
        }

        UpdateFlatHandBackdrop(flatHand);
        UpdateFlatHandCombatLayout();
    }

    public void SetVisualTexture(Texture2D texture)
    {
        Sprite2D sprite = GetNodeOrNull<Sprite2D>("%Visuals");
        if (sprite == null || texture == null)
        {
            return;
        }

        sprite.Texture = texture;
        Modulate = Colors.White;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        Sprite2D sprite = GetNodeOrNull<Sprite2D>("%Visuals");
        if (sprite?.Texture == null)
        {
            return;
        }

        float scale = Mathf.Min(
            TargetHeight / sprite.Texture.GetHeight(),
            MaxWidth / sprite.Texture.GetWidth());
        float halfWidth = sprite.Texture.GetWidth() * scale * 0.5f;
        float halfHeight = sprite.Texture.GetHeight() * scale * 0.5f;
        float centerY = VisualBottomY - halfHeight;
        float topY = centerY - halfHeight;
        float boundsWidth = Mathf.Max(halfWidth * 2f, MinBoundsWidth);

        sprite.Scale = Vector2.One * scale;
        sprite.Position = new Vector2(0f, centerY);

        Control bounds = GetNodeOrNull<Control>("%Bounds");
        if (bounds != null)
        {
            bounds.Position = new Vector2(-boundsWidth * 0.5f, topY);
            bounds.Size = new Vector2(boundsWidth, halfHeight * 2f);
        }

        SetMarker("%CenterPos", new Vector2(0f, centerY));
        SetMarker("%IntentPos", new Vector2(0f, topY - 24f));
        SetMarker("%TalkPos", new Vector2(0f, topY - 12f));
    }

    private void SetMarker(string path, Vector2 position)
    {
        Marker2D marker = GetNodeOrNull<Marker2D>(path);
        if (marker != null)
        {
            marker.Position = position;
        }
    }

    private void UpdateFlatHandBackdrop(FlatHand flatHand)
    {
        NCombatRoom room = GetCombatRoom();
        if (room?.SceneContainer == null)
        {
            return;
        }

        GetCurrentBody().Visible = false;

        Control backgroundContainer =
            GetSceneBossBackdropContainer(room);
        if (backgroundContainer == null)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_flatHandBackdrop))
        {
            _flatHandBackdrop = new TextureRect
            {
                Name = "FlatHandPhaseBackdrop",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            _flatHandBackdrop.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            ConfigureSceneBossBackdropLayer(
                _flatHandBackdrop,
                room);
            backgroundContainer.AddChild(_flatHandBackdrop);
        }

        if (room.Background != null)
        {
            room.Background.Visible = false;
        }

        string nextPath = GetFlatHandBackdropPath(flatHand);
        if (_flatHandBackdropPath != nextPath)
        {
            Texture2D texture = ResourceLoader.Load<Texture2D>(nextPath);
            if (texture != null)
            {
                _flatHandBackdropTexture = texture;
                _flatHandBackdropPath = nextPath;
            }
        }

        UpdateSceneBossBackdropLayout(
            _flatHandBackdrop,
            room,
            _flatHandBackdropTexture,
            GetFlatHandSplitCenterRatio(flatHand));
    }

    private static float GetFlatHandSplitCenterRatio(FlatHand flatHand)
    {
        if (flatHand.Respawns <= 0)
        {
            return FlatHandPhaseOneSplitCenterRatio;
        }

        return flatHand.Respawns == 1
            ? FlatHandPhaseTwoSplitCenterRatio
            : FlatHandPhaseThreeSplitCenterRatio;
    }

    private string GetFlatHandBackdropPath(FlatHand flatHand)
    {
        bool hasHighDimensionalShell =
            _creature.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m;

        if (flatHand.Respawns <= 0)
        {
            return hasHighDimensionalShell
                ? FlatHandPhaseOneBackgroundPath
                : FlatHandPhaseOneShellBrokenBackgroundPath;
        }

        if (flatHand.Respawns == 1)
        {
            return hasHighDimensionalShell
                ? FlatHandPhaseTwoBackgroundPath
                : FlatHandPhaseTwoShellBrokenBackgroundPath;
        }

        return hasHighDimensionalShell
            ? FlatHandPhaseThreeBackgroundPath
            : FlatHandPhaseThreeShellBrokenBackgroundPath;
    }

    private void UpdateFlatHandCombatLayout()
    {
        NCombatRoom room = GetCombatRoom();
        UpdateAlliedCombatLayout(
            room,
            FlatHandAlliedFloorRatio,
            AlliedStatusBarHeadGap);
        UpdateSceneBossCombatLayout(
            room,
            FlatHandHealthBarLeftRatio,
            FlatHandHealthBarTopRatio,
            FlatHandHealthBarWidth,
            FlatHandHitboxHorizontalPadding,
            FlatHandHitboxTopPadding,
            FlatHandHitboxBottomPadding,
            FlatHandIntentOffset);
    }

    protected void UpdateSceneBossBackdropLayout(
        TextureRect backdrop,
        NCombatRoom room,
        Texture2D sourceTexture,
        float splitSourceCenterRatio = 0.75f)
    {
        if (backdrop == null ||
            room?.SceneContainer == null ||
            sourceTexture == null)
        {
            return;
        }

        ConfigureSceneBossBackdropLayer(backdrop, room);
        bool useSplitLayout = HasAlliedAndEnemySceneBosses(room);
        Rect2 sceneRect = GetSceneBossBackdropRect(room);
        Vector2 scenePosition = sceneRect.Position;
        Vector2 sceneSize = sceneRect.Size;
        if (useSplitLayout)
        {
            float halfWidth = sceneSize.X * 0.5f;
            backdrop.GlobalPosition = scenePosition +
                (IsAlliedSummon
                    ? Vector2.Zero
                    : Vector2.Right * halfWidth);
            backdrop.Size = new Vector2(halfWidth, sceneSize.Y);
            if (backdrop.Texture is not AtlasTexture bodyHalf ||
                bodyHalf.Atlas != sourceTexture)
            {
                backdrop.Texture = GetMainBodyHalf(
                    sourceTexture,
                    splitSourceCenterRatio);
            }
        }
        else
        {
            backdrop.GlobalPosition = scenePosition;
            backdrop.Size = sceneSize;
            if (backdrop.Texture != sourceTexture)
            {
                backdrop.Texture = sourceTexture;
            }
        }

        backdrop.FlipH = IsAlliedSummon;
    }

    protected static Control GetSceneBossBackdropContainer(
        NCombatRoom room)
    {
        return room;
    }

    protected static void ConfigureSceneBossBackdropLayer(
        CanvasItem backdrop,
        NCombatRoom room)
    {
        backdrop.ZIndex = room.SceneContainer.ZIndex - 1;
    }

    protected static Rect2 GetSceneBossBackdropRect(NCombatRoom room)
    {
        return room.GetGlobalRect();
    }

    protected bool HasAlliedAndEnemySceneBosses(NCombatRoom room)
    {
        bool hasAlliedSceneBoss = false;
        bool hasEnemySceneBoss = false;
        foreach (NCreature creatureNode in room.CreatureNodes)
        {
            Creature creature = creatureNode.Entity;
            if (!IsSceneBoss(creature))
            {
                continue;
            }

            if (creature.PetOwner == null)
            {
                hasEnemySceneBoss = true;
            }
            else
            {
                hasAlliedSceneBoss = true;
            }
        }

        return hasAlliedSceneBoss && hasEnemySceneBoss;
    }

    internal static bool IsSceneBoss(Creature creature)
    {
        return creature?.Monster is FlatHand or
            RedCrimsonBossMonster or
            DeathSlug or
            SkullFeather;
    }

    private static AtlasTexture GetMainBodyHalf(
        Texture2D texture,
        float sourceCenterRatio)
    {
        float textureWidth = texture.GetWidth();
        float halfWidth = textureWidth * 0.5f;
        float centerX = textureWidth * Mathf.Clamp(
            sourceCenterRatio,
            0.25f,
            0.75f);
        float left = Mathf.Clamp(
            centerX - halfWidth * 0.5f,
            0f,
            textureWidth - halfWidth);
        return new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(
                left,
                0f,
                halfWidth,
                texture.GetHeight()),
            FilterClip = true
        };
    }

    protected void UpdateSceneBossCombatLayout(
        NCombatRoom room,
        float healthBarLeftRatio,
        float healthBarTopRatio,
        float healthBarWidth,
        float hitboxHorizontalPadding,
        float hitboxTopPadding,
        float hitboxBottomPadding,
        float intentOffset)
    {
        NCreature creatureNode = GetParent() as NCreature;
        if (room?.SceneContainer == null ||
            creatureNode?.Entity != _creature ||
            !creatureNode.IsNodeReady())
        {
            return;
        }

        if (IsAlliedSummon)
        {
            UpdateSummonedBossCombatLayout(
                room,
                creatureNode,
                healthBarTopRatio,
                healthBarWidth,
                hitboxHorizontalPadding,
                hitboxTopPadding,
                hitboxBottomPadding,
                intentOffset);
            return;
        }

        NCreatureStateDisplay stateDisplay =
            creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        NHealthBar healthBar =
            stateDisplay?.GetNodeOrNull<NHealthBar>("%HealthBar");
        Control hpBar = healthBar?.HpBarContainer;
        if (stateDisplay == null || hpBar == null)
        {
            return;
        }

        Vector2 sceneSize = room.SceneContainer.Size;
        Vector2 hpBarPosition =
            room.SceneContainer.GlobalPosition +
            new Vector2(
                sceneSize.X * healthBarLeftRatio +
                    EnemyStatusBarHorizontalOffset,
                sceneSize.Y * healthBarTopRatio);

        if (!Mathf.IsEqualApprox(
                hpBar.Size.X,
                healthBarWidth))
        {
            hpBar.Size = new Vector2(
                healthBarWidth,
                hpBar.Size.Y);
        }

        stateDisplay.GlobalPosition +=
            hpBarPosition - hpBar.GlobalPosition;
        hpBar.GlobalPosition = hpBarPosition;
        AlignStateDisplayContentToHealthBar(stateDisplay, hpBar);

        Vector2 hitboxPosition = hpBarPosition + new Vector2(
            -hitboxHorizontalPadding,
            -hitboxTopPadding);
        Vector2 hitboxSize = new(
            healthBarWidth + hitboxHorizontalPadding * 2f,
            hitboxTopPadding + hpBar.Size.Y +
                hitboxBottomPadding);

        Bounds.GlobalPosition = hitboxPosition;
        Bounds.Size = hitboxSize;
        creatureNode.Hitbox.GlobalPosition = hitboxPosition;
        creatureNode.Hitbox.Size = hitboxSize;
        creatureNode.Hitbox.PivotOffset = hitboxSize * 0.5f;

        Control selectionReticle =
            creatureNode.GetNodeOrNull<Control>("%SelectionReticle");
        if (selectionReticle != null)
        {
            selectionReticle.GlobalPosition = hitboxPosition;
            selectionReticle.Size = hitboxSize;
            selectionReticle.PivotOffset = hitboxSize * 0.5f;
        }

        Vector2 barCenter = hpBarPosition + new Vector2(
            healthBarWidth * 0.5f,
            hpBar.Size.Y * 0.5f);
        creatureNode.IntentContainer.GlobalPosition = new Vector2(
            barCenter.X - creatureNode.IntentContainer.Size.X * 0.5f,
            hitboxPosition.Y - intentOffset);

        VfxSpawnPosition.GlobalPosition = barCenter;
        IntentPosition.GlobalPosition = new Vector2(
            barCenter.X,
            hitboxPosition.Y - intentOffset * 0.5f);
        if (TalkPosition != null)
        {
            TalkPosition.GlobalPosition = IntentPosition.GlobalPosition;
        }
    }

    private void UpdateSummonedBossCombatLayout(
        NCombatRoom room,
        NCreature creatureNode,
        float healthBarTopRatio,
        float healthBarWidth,
        float hitboxHorizontalPadding,
        float hitboxTopPadding,
        float hitboxBottomPadding,
        float intentOffset)
    {
        NCreatureStateDisplay stateDisplay =
            creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        NHealthBar healthBar =
            stateDisplay?.GetNodeOrNull<NHealthBar>("%HealthBar");
        Control hpBar = healthBar?.HpBarContainer;
        if (stateDisplay == null || healthBar == null || hpBar == null)
        {
            return;
        }

        float compactHealthBarWidth =
            healthBarWidth * SummonedBossStatusScale;
        healthBar.UpdateWidthRelativeToReferenceValue(
            Mathf.Max(1f, _creature.MaxHp),
            compactHealthBarWidth);

        Vector2 summonPosition = creatureNode.GlobalPosition;
        // Anchor to the same scene height as this boss's enemy form. Deriving
        // both positions from the scene avoids feeding last frame's moved bounds
        // or IntentPos marker back into the next layout update.
        float intentTop = room.SceneContainer.GlobalPosition.Y +
            room.SceneContainer.Size.Y * healthBarTopRatio -
            hitboxTopPadding - intentOffset;
        Vector2 hpBarPosition = new(
            summonPosition.X - compactHealthBarWidth * 0.5f,
            intentTop + hitboxTopPadding + intentOffset);
        stateDisplay.GlobalPosition = new Vector2(
            summonPosition.X, hpBarPosition.Y);
        hpBar.GlobalPosition = hpBarPosition;
        AlignStateDisplayContentToHealthBar(stateDisplay, hpBar);

        float compactHorizontalPadding =
            hitboxHorizontalPadding * SummonedBossStatusScale;
        float compactTopPadding =
            hitboxTopPadding * SummonedBossStatusScale;
        float compactBottomPadding =
            hitboxBottomPadding * SummonedBossStatusScale;
        Vector2 hitboxPosition = hpBarPosition + new Vector2(
            -compactHorizontalPadding,
            -compactTopPadding);
        Vector2 hitboxSize = new(
            compactHealthBarWidth + compactHorizontalPadding * 2f,
            compactTopPadding + hpBar.Size.Y +
                compactBottomPadding);

        Bounds.GlobalPosition = hitboxPosition;
        Bounds.Size = hitboxSize;
        creatureNode.Hitbox.GlobalPosition = hitboxPosition;
        creatureNode.Hitbox.Size = hitboxSize;
        creatureNode.Hitbox.PivotOffset = hitboxSize * 0.5f;

        Control selectionReticle =
            creatureNode.GetNodeOrNull<Control>("%SelectionReticle");
        if (selectionReticle != null)
        {
            selectionReticle.GlobalPosition = hitboxPosition;
            selectionReticle.Size = hitboxSize;
            selectionReticle.PivotOffset = hitboxSize * 0.5f;
        }

        Vector2 barCenter = hpBarPosition + new Vector2(
            compactHealthBarWidth * 0.5f,
            hpBar.Size.Y * 0.5f);
        // Only the bar width and hitbox padding are compacted. Intent icons and
        // DP retain their size, so preserve the enemy form's full vertical gap.
        creatureNode.IntentContainer.GlobalPosition = new Vector2(
            barCenter.X - creatureNode.IntentContainer.Size.X * 0.5f,
            intentTop);

        VfxSpawnPosition.GlobalPosition = barCenter;
        IntentPosition.GlobalPosition = new Vector2(
            barCenter.X,
            intentTop + creatureNode.IntentContainer.Size.Y * 0.5f);
        if (TalkPosition != null)
        {
            TalkPosition.GlobalPosition = IntentPosition.GlobalPosition;
        }
    }

    protected void UpdateAlliedCombatLayout(
        NCombatRoom room,
        float floorRatio,
        float statusBarHeadGap)
    {
        if (room?.SceneContainer == null)
        {
            return;
        }

        if (IsAlliedSummon && HasAlliedAndEnemySceneBosses(room))
        {
            return;
        }

        float floorY = room.SceneContainer.GlobalPosition.Y +
            room.SceneContainer.Size.Y * floorRatio;
        foreach (NCreature playerNode in room.CreatureNodes)
        {
            if (!playerNode.Entity.IsPlayer)
            {
                continue;
            }

            Rect2 boundsRect = GetDisplayedVisualRect(playerNode);
            Vector2 layoutOffset = Vector2.Down *
                (floorY - boundsRect.End.Y);
            playerNode.GlobalPosition += layoutOffset;
            foreach (NCreature petNode in room.CreatureNodes)
            {
                if (petNode.Entity.PetOwner?.Creature ==
                    playerNode.Entity)
                {
                    petNode.GlobalPosition += layoutOffset;
                }
            }

            boundsRect = GetDisplayedVisualRect(playerNode);
            NCreatureStateDisplay stateDisplay =
                playerNode.GetNodeOrNull<NCreatureStateDisplay>(
                    "%HealthBar");
            NHealthBar healthBar =
                stateDisplay?.GetNodeOrNull<NHealthBar>("%HealthBar");
            Control hpBar = healthBar?.HpBarContainer;
            if (stateDisplay == null || hpBar == null)
            {
                continue;
            }

            Vector2 desiredHpBarPosition = new(
                boundsRect.GetCenter().X - hpBar.Size.X * 0.5f,
                boundsRect.Position.Y - statusBarHeadGap -
                    hpBar.Size.Y);
            stateDisplay.GlobalPosition +=
                desiredHpBarPosition - hpBar.GlobalPosition;
            hpBar.GlobalPosition = desiredHpBarPosition;
            AlignStateDisplayContentToHealthBar(stateDisplay, hpBar);
        }
    }

    private static Rect2 GetDisplayedVisualRect(NCreature creatureNode)
    {
        if (creatureNode.Visuals.GetCurrentBody() is not Sprite2D sprite ||
            sprite.Texture == null)
        {
            return creatureNode.Visuals.Bounds.GetGlobalRect();
        }

        Rect2 localRect = sprite.GetRect();
        Vector2 firstCorner = sprite.ToGlobal(localRect.Position);
        Vector2 secondCorner = sprite.ToGlobal(localRect.End);
        Vector2 position = new(
            Mathf.Min(firstCorner.X, secondCorner.X),
            Mathf.Min(firstCorner.Y, secondCorner.Y));
        Vector2 size = new(
            Mathf.Abs(secondCorner.X - firstCorner.X),
            Mathf.Abs(secondCorner.Y - firstCorner.Y));
        return new Rect2(position, size);
    }

    private static void AlignStateDisplayContentToHealthBar(
        NCreatureStateDisplay stateDisplay,
        Control hpBar)
    {
        HBRGOSakiMod.Core.Helpers.CombatHudLayoutHelper.AlignPowerContainerToHealthBar(
            stateDisplay, hpBar, 0f, StateDisplayContentGap);
        HBRGOSakiMod.Core.Helpers.CombatHudLayoutHelper.AlignNameplateToHealthBar(
            stateDisplay, hpBar, StateDisplayContentGap);
    }

    protected NCombatRoom GetCombatRoom()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is NCombatRoom room)
            {
                return room;
            }

            current = current.GetParent();
        }

        return null;
    }

    private void OnDied(Creature _)
    {
        if (_creature?.Monster is IHbrgoRevivingStaticMonster
            {
                ShouldSuppressDeathFade: true
            })
        {
            return;
        }

        _deathTween?.Kill();
        Color faded = Modulate;
        faded.A = 0f;
        _deathTween = CreateTween();
        _deathTween
            .TweenProperty(
                this,
                "modulate",
                faded,
                HbrgoStaticMonster.DeathFadeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
    }
}
