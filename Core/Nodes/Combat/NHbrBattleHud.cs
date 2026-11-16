using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The HBR battle HUD's own layer on NCombatUi: the dark fade along the bottom edge and the standing
// TURN indicator. It exists only while the local player is one of the mod's characters; the end turn
// disc, pile circles and turn banners check for it.
public partial class NHbrBattleHud : Control
{
    internal const string NodeName = "HbrBattleHud";

    private CombatState _state;
    private Player _me;
    private TextureRect _gradient;
    private NHbrTurnIndicator _turn;
    private Tween _fadeTween;
    private int _turnToken = int.MinValue;
    private int _extra;

    internal static NHbrBattleHud Of(NCombatUi ui) =>
        GodotObject.IsInstanceValid(ui) ? ui.GetNodeOrNull<NHbrBattleHud>(NodeName) : null;

    internal static NHbrBattleHud Current => NCombatRoom.Instance?.Ui is { } ui ? Of(ui) : null;

    internal static void Attach(NCombatUi ui, CombatState state, Player me)
    {
        if (!GodotObject.IsInstanceValid(ui) || Of(ui) != null) return;
        // Appended after NSakiCardSelectionPortrait, so at the same z the fade lies over the portrait.
        ui.AddChild(new NHbrBattleHud { Name = NodeName, _state = state, _me = me });
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Above the battlefield and the selection portrait, below the hand, piles and energy.
        _gradient = new TextureRect
        {
            Name = "BottomFade",
            Texture = BottomFade(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            ZAsRelative = false,
            ZIndex = -1
        };
        AddChild(_gradient);
        _gradient.AnchorLeft = 0f;
        _gradient.AnchorRight = 1f;
        _gradient.AnchorTop = 1f;
        _gradient.AnchorBottom = 1f;
        _gradient.OffsetLeft = 0f;
        _gradient.OffsetRight = 0f;
        _gradient.OffsetTop = -HbrBattleHudLayout.GradientHeight;
        _gradient.OffsetBottom = 0f;

        _turn = new NHbrTurnIndicator { Name = "TurnIndicator", ZAsRelative = false, ZIndex = -1 };
        AddChild(_turn);
        _turn.AnchorLeft = 0f;
        _turn.AnchorRight = 0f;
        _turn.AnchorTop = 1f;
        _turn.AnchorBottom = 1f;
        _turn.OffsetLeft = HbrBattleHudLayout.TurnLeft;
        _turn.OffsetTop = -HbrBattleHudLayout.TurnFromBottom;
        _turn.OffsetRight = HbrBattleHudLayout.TurnLeft + HbrBattleHudLayout.TurnWidth;
        _turn.OffsetBottom = -HbrBattleHudLayout.TurnFromBottom + HbrBattleHudLayout.TurnHeight;
        RefreshTurn(bump: false);

        Modulate = new Color(1f, 1f, 1f, 0f);
        Fade(1f);
    }

    public override void _EnterTree()
    {
        if (CombatManager.Instance != null) CombatManager.Instance.TurnStarted += OnTurnStarted;
    }

    public override void _ExitTree()
    {
        if (CombatManager.Instance != null) CombatManager.Instance.TurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(CombatState state)
    {
        if (state?.CurrentSide == CombatSide.Player) RefreshTurn(bump: true);
    }

    // The local player's turn number changes once per turn the player takes, extra turns included,
    // so it tells a new turn apart however the banner and TurnStarted are ordered.
    internal (int Turn, int Extra) RefreshTurn(bool bump)
    {
        int round = _state?.RoundNumber ?? 1;
        int token = _me?.PlayerCombatState?.TurnNumber ?? 0;
        if (token != _turnToken)
        {
            bool extra = CombatManager.Instance?.PlayersTakingExtraTurn.Contains(_me) == true;
            _extra = extra ? _extra + 1 : 0;
            _turnToken = token;
            _turn?.Show(round, _extra, bump && _turn.IsInsideTree());
        }
        return (round, _extra);
    }

    internal void AnimOut() => Fade(0f);

    private void Fade(float alpha)
    {
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", alpha, 0.3);
    }

    private static GradientTexture2D BottomFade()
    {
        Color colour = new(HbrBattleHudLayout.GradientColor);
        Gradient gradient = new();
        var stops = HbrBattleHudLayout.GradientStops;
        gradient.Offsets = stops.Select(stop => stop.Offset).ToArray();
        gradient.Colors = stops.Select(stop => new Color(colour, stop.Alpha)).ToArray();
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 4,
            Height = 128,
            FillFrom = new Vector2(0f, 0f),
            FillTo = new Vector2(0f, 1f)
        };
    }
}
