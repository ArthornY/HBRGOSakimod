using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Presentation only: a native card-play node owns the selected-card lifetime.
// The battlefield creature, targeting bounds and combat HUD remain untouched.
public partial class NSakiCardSelectionPortrait : Control
{
    public const string WidgetName = "SakiCardSelectionPortrait";

    private const float ReferenceHeight = 1080f;
    private const float VisiblePortraitHeight = 820f;
    private const string ScytheExtensionPath =
        "res://images/characters/selection/hbrgo_saki_30a_scythe_extension.png";
    // The supplemental 1280x1200 canvas contains the unmodified 1024x1024
    // source at (0,176). Its pixels inside that source rectangle are transparent.
    private static readonly Vector2 ScytheOriginalOffset = new(0f, 176f);

    private enum PortraitMode
    {
        Unset = -1,
        Normal,
        DualSwords,
        Bow,
        ThirtyAScythe
    }

    // Source-pixel body height is separate from the complete visible region,
    // so weapon tips or a supplemental body layer need not shrink the body.
    private readonly record struct PortraitSpec(
        string Path, Rect2 VisibleRegion, float BodyReferenceHeight);

    private static PortraitSpec GetPortraitSpec(PortraitMode mode) => mode switch
    {
        PortraitMode.DualSwords => new(
            "res://images/characters/selection/hbrgo_saki_dual_swords.png",
            new Rect2(0f, 121f, 832f, 1095f), 1095f),
        PortraitMode.Bow => new(
            "res://images/characters/selection/hbrgo_saki_bow.png",
            new Rect2(0f, 122f, 832f, 1094f), 1094f),
        PortraitMode.ThirtyAScythe => new(
            "res://images/characters/selection/hbrgo_saki_30a_scythe.png",
            new Rect2(19f, 0f, 1005f, 1024f), 929f),
        _ => new("res://images/characters/hbrgo_saki_corner_normal.png",
            new Rect2(12f, 16f, 691f, 1200f), 1200f)
    };

    private NCombatUi _ui;
    private NPlayerHand _hand;
    private Player _player;
    private TextureRect _portrait;
    private TextureRect _scytheExtension;
    private Texture2D _scytheExtensionTexture;
    private readonly Texture2D[] _portraitTextures = new Texture2D[4];
    private PortraitMode _portraitMode = PortraitMode.Unset;
    private PortraitSpec _portraitSpec;
    private NCardPlay _activePlay;
    private Callable _onPlayFinished;
    private Callable _onPlayExiting;

    public override void _EnterTree()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ClipContents = false;
        ZAsRelative = false;
        ZIndex = -8;
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _ui = GetParent() as NCombatUi;
        RenderingServer.FramePreDraw += RefreshPresentation;
        BindHand(_ui?.Hand);
    }

    public override void _Ready()
    {
        BuildPortrait();
        RefreshPresentation();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshPresentation;
        BindHand(null);
        _ui = null;
        _player = null;
        Visible = false;
    }

    public void BindPlayer(Player player)
    {
        _player = player;
        if (IsInsideTree()) RefreshPresentation();
    }

    private void BuildPortrait()
    {
        if (IsValid(_portrait)) return;
        _scytheExtension = new TextureRect
        {
            Name = "ScytheCompletionLayer",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            ClipContents = false,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Visible = false
        };
        AddChild(_scytheExtension);
        _portrait = new TextureRect
        {
            Name = "SelectionPortrait",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            ClipContents = false,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale
        };
        AddChild(_portrait);
    }

    private PortraitMode GetPortraitMode()
    {
        if (_player.Character is HbrgoSaki30A) return PortraitMode.ThirtyAScythe;
        var creature = _player.Creature;
        if (creature.HasPower<ReptureBowPower>()) return PortraitMode.Bow;
        if (creature.HasPower<ReptureDualSwordsPower>() ||
            creature.HasPower<MagicSwordModePower>() ||
            creature.HasPower<CrimsonBladeFlowPower>()) return PortraitMode.DualSwords;
        return PortraitMode.Normal;
    }

    private bool RefreshPortraitArt()
    {
        PortraitMode mode = GetPortraitMode();
        if (mode != _portraitMode)
        {
            _portraitMode = mode;
            _portraitSpec = GetPortraitSpec(mode);
            Texture2D texture = _portraitTextures[(int)mode];
            if (!IsValid(texture))
            {
                Texture2D source = ResourceLoader.Load<Texture2D>(_portraitSpec.Path);
                if (source != null)
                {
                    texture = new AtlasTexture
                    {
                        Atlas = source,
                        Region = _portraitSpec.VisibleRegion
                    };
                    _portraitTextures[(int)mode] = texture;
                }
            }
            _portrait.Texture = texture;
        }
        bool scythe = mode == PortraitMode.ThirtyAScythe;
        if (scythe && !IsValid(_scytheExtensionTexture))
            _scytheExtensionTexture = ResourceLoader.Load<Texture2D>(ScytheExtensionPath);
        _scytheExtension.Texture = _scytheExtensionTexture;
        _scytheExtension.Visible = scythe && IsValid(_scytheExtensionTexture);
        return _portrait.Texture != null;
    }

    private void BindHand(NPlayerHand hand)
    {
        if (ReferenceEquals(_hand, hand)) return;
        if (IsValid(_hand))
        {
            _hand.ChildEnteredTree -= OnHandChildEntered;
            _hand.TreeExiting -= OnHandExiting;
        }
        ClearActivePlay();
        _hand = hand;
        if (!IsValid(_hand)) return;
        _hand.ChildEnteredTree += OnHandChildEntered;
        _hand.TreeExiting += OnHandExiting;
        // Attachments can be installed while a card is already selected.
        foreach (Node child in _hand.GetChildren())
        {
            if (child is NCardPlay play && IsLive(play)) SetActivePlay(play);
        }
    }

    private void OnHandChildEntered(Node child)
    {
        if (child is not NCardPlay play || !IsLive(play)) return;
        // Create sets Player/Holder before adding the node. Start runs after
        // ChildEnteredTree, so even an immediate cancellation is observed.
        SetActivePlay(play);
        RefreshPresentation();
    }

    private void OnHandExiting() => BindHand(null);

    private void SetActivePlay(NCardPlay play)
    {
        if (ReferenceEquals(_activePlay, play)) return;
        ClearActivePlay();
        _activePlay = play;
        _onPlayFinished = Callable.From<bool>(_ => OnPlayEnded(play));
        _onPlayExiting = Callable.From(() => OnPlayEnded(play));
        play.Connect(NCardPlay.SignalName.Finished, _onPlayFinished);
        play.Connect(Node.SignalName.TreeExiting, _onPlayExiting);
    }

    private void OnPlayEnded(NCardPlay play)
    {
        // A canceled card can leave the tree after its replacement is active.
        if (ReferenceEquals(_activePlay, play)) ClearActivePlay();
    }

    private void ClearActivePlay()
    {
        if (IsValid(_activePlay))
        {
            if (_activePlay.IsConnected(NCardPlay.SignalName.Finished, _onPlayFinished))
                _activePlay.Disconnect(NCardPlay.SignalName.Finished, _onPlayFinished);
            if (_activePlay.IsConnected(Node.SignalName.TreeExiting, _onPlayExiting))
                _activePlay.Disconnect(Node.SignalName.TreeExiting, _onPlayExiting);
        }
        _activePlay = null;
        Visible = false;
    }

    private void RefreshPresentation()
    {
        if (!IsInsideTree() || !IsLive(_ui))
        {
            Visible = false;
            return;
        }
        BindHand(_ui.Hand);
        if (_activePlay != null && !IsLive(_activePlay)) ClearActivePlay();

        NCombatRoom room = NCombatRoom.Instance;
        bool show = IsValid(_portrait) && HBRGOSakiConfig.ModUI &&
            _player?.Character is HbrgoSaki && LocalContext.IsMe(_player) &&
            IsLive(room) && ReferenceEquals(room.Ui, _ui) &&
            room.Mode == CombatRoomMode.ActiveCombat &&
            CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding &&
            _ui.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(room) &&
            IsLive(_hand) && _hand.CurrentMode == NPlayerHand.Mode.Play &&
            IsLive(_activePlay) && ReferenceEquals(_activePlay.Player, _player) &&
            ReferenceEquals(_activePlay.GetParent(), _hand);
        if (show) show = RefreshPortraitArt();
        if (show)
        {
            // Keep the visible cutout flush with the lower-left UI edge. UI
            // height supplies the same proportion at other window sizes.
            float scale = Mathf.Min(
                VisiblePortraitHeight * Size.Y / ReferenceHeight / _portraitSpec.BodyReferenceHeight,
                Size.X / _portraitSpec.VisibleRegion.Size.X);
            Vector2 portraitSize = _portraitSpec.VisibleRegion.Size * scale;
            _portrait.Size = portraitSize;
            _portrait.Position = new Vector2(0f, Size.Y - portraitSize.Y);
            if (_scytheExtension.Visible)
            {
                // Keep the user's pixels and the generated completion in
                // separate draw nodes, using the same original-pixel scale.
                Vector2 originalOrigin = _portrait.Position - _portraitSpec.VisibleRegion.Position * scale;
                _scytheExtension.Position = originalOrigin - ScytheOriginalOffset * scale;
                _scytheExtension.Size = _scytheExtensionTexture.GetSize() * scale;
            }
        }
        Visible = show;
    }

    private static bool IsValid(GodotObject value) =>
        value != null && GodotObject.IsInstanceValid(value);

    private static bool IsLive(Node node) =>
        IsValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion();
}
