using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// NEndTurnButton drawn as the HBR "Start Action" disc (UIBattleAttack, BtnCmdStart*). The native
// button keeps its state machine, input, hotkey, hover tip, vote icons and long-press timing and only
// loses its own artwork; HbrBattleHudPatches forwards each visual change here.
public partial class NHbrEndTurnDisc : Control
{
    internal const string NodeName = "HbrEndTurnDisc";
    private const int HiddenState = 2; // NEndTurnButton.State.Hidden

    private static readonly FieldInfo CombatStateField = AccessTools.Field(typeof(NEndTurnButton), "_combatState");
    private static readonly FieldInfo ShinyField = AccessTools.Field(typeof(NEndTurnButton), "_isShiny");
    private static readonly FieldInfo StateField = AccessTools.Field(typeof(NEndTurnButton), "_state");

    private static readonly Color LabelOutline = new(HbrBattleHudLayout.DiscLabelOutline);
    private static readonly Color LabelGlow = new(HbrBattleHudLayout.DiscGlowColor);
    private static readonly Color SubColor = new(HbrBattleHudLayout.DiscSubColor);
    private static readonly Color WarnColor = new(HbrBattleHudLayout.DiscWarnColor);
    private static readonly Color DisabledColor = new(HbrBattleHudLayout.DisabledTextColor);
    private static readonly Color TextOutline = new(0.07f, 0.03f, 0.09f, 0.9f);

    private NEndTurnButton _button;
    private Control _visuals;
    private TextureRect _aura;
    private TextureRect _active;
    private TextureRect _disabled;
    private Label _label;
    private Label _sub;
    private Tween _scaleTween;
    private Tween _activeTween;
    private Tween _disabledTween;
    private Tween _auraTween;
    private Tween _showTween;
    private float _scaleTarget = 1f;
    private float _activeTarget = -1f;
    private float _disabledTarget = -1f;
    private int _subStyle = -1;
    private bool _hovered;
    private bool _pressed;
    private bool _shiny;
    private bool _hidden;

    internal static NHbrEndTurnDisc Of(NEndTurnButton button) =>
        GodotObject.IsInstanceValid(button) ? button.GetNodeOrNull<NHbrEndTurnDisc>(NodeName) : null;

    internal static void Attach(NEndTurnButton button)
    {
        if (!GodotObject.IsInstanceValid(button) || Of(button) != null) return;
        NHbrEndTurnDisc disc = new() { Name = NodeName, _button = button };
        button.AddChild(disc);
        // Behind the native Visuals, so the controller hotkey icon and the vote icons stay on top.
        button.MoveChild(disc, 0);
        disc.Build();
        disc.Refresh();
    }

    // The disc's top-left corner hangs 305px from the right edge and 280px from the bottom.
    internal static Vector2 ShowPosition(Vector2 viewport) =>
        viewport - new Vector2(HbrBattleHudLayout.DiscFromRight, HbrBattleHudLayout.DiscFromBottom);

    internal static Vector2 HidePosition(Vector2 viewport) =>
        ShowPosition(viewport) + new Vector2(0f, HbrBattleHudLayout.DiscHideDrop);

    private void Build()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (string path in new[] { "Visuals/Image", "Visuals/Glow", "Visuals/GlowVfx", "Visuals/Label", "Bar" })
        {
            if (_button.GetNodeOrNull<CanvasItem>(path) is { } native) native.Visible = false;
        }

        float size = HbrBattleHudLayout.DiscSize;
        _button.Size = new Vector2(size, size);
        _button.PivotOffset = _button.Size / 2f;
        Position = Vector2.Zero;
        Size = _button.Size;
        if (_button.GetNodeOrNull<Control>("PlayerIconContainer") is { } votes)
            votes.Position = new Vector2((size - votes.Size.X) / 2f, HbrBattleHudLayout.DiscVotesTop);

        _visuals = new Control { Name = "Visuals", Size = Size, PivotOffset = Size / 2f, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_visuals);

        float aura = HbrBattleHudLayout.DiscAuraSize;
        float auraOffset = (size - aura) / 2f;
        _aura = HbrBattleHudArt.Sprite("Aura", "auraCircle", new HbrHudRect(auraOffset, auraOffset, aura, aura),
            new Color(1f, 1f, 1f, 0f));
        _aura.PivotOffset = new Vector2(aura, aura) / 2f;
        _visuals.AddChild(_aura);

        float texture = HbrBattleHudLayout.DiscTextureSize;
        float textureOffset = (size - texture) / 2f;
        HbrHudRect rect = new(textureOffset, textureOffset, texture, texture);
        _visuals.AddChild(HbrBattleHudArt.Sprite("Normal", "BtnCmdStartNormal", rect));
        _active = HbrBattleHudArt.Sprite("Active", "BtnCmdStartActive", rect, new Color(1f, 1f, 1f, 0f));
        _visuals.AddChild(_active);
        _disabled = HbrBattleHudArt.Sprite("Disabled", "BtnCmdStartDisable", rect, new Color(1f, 1f, 1f, 0f));
        _visuals.AddChild(_disabled);

        float inset = HbrBattleHudLayout.RingInset;
        NHbrEndTurnLongPressRing ring = new()
        {
            Name = "LongPressRing",
            Position = new Vector2(inset, inset),
            Size = new Vector2(size - inset * 2f, size - inset * 2f)
        };
        ring.Bind(_button.GetNodeOrNull<Control>("Bar"));
        _visuals.AddChild(ring);

        // A centred column: the 31px label (line-height 1), an 8px gap, then the 15px sub line.
        float block = HbrBattleHudLayout.DiscLabelSize + HbrBattleHudLayout.DiscSubGap + HbrBattleHudLayout.DiscSubSize * 1.1f;
        float top = (size - block) / 2f + 1f;
        _label = HbrBattleHudArt.Label("Label", HbrBattleHudLayout.EndTurnLabel, HbrBattleHudLayout.DiscLabelSize,
            Colors.White, 4);
        PlaceLine(_label, top + HbrBattleHudLayout.DiscLabelSize / 2f, 50f);
        _visuals.AddChild(_label);
        _sub = HbrBattleHudArt.Label("Sub", HbrBattleHudLayout.EndTurnSub, HbrBattleHudLayout.DiscSubSize, SubColor, 4);
        PlaceLine(_sub, top + HbrBattleHudLayout.DiscLabelSize + HbrBattleHudLayout.DiscSubGap +
            HbrBattleHudLayout.DiscSubSize * 0.55f, 30f);
        HbrBattleHudArt.Outline(_sub, TextOutline, 6);
        _visuals.AddChild(_sub);
        StyleText(disabled: false, warn: false);

        _hidden = StateField?.GetValue(_button) is { } state && (int)state == HiddenState;
        if (_hidden)
        {
            _button.Modulate = new Color(_button.Modulate, 0f);
            _button.Scale = Vector2.One * HbrBattleHudLayout.DiscHiddenScale;
        }
    }

    private void PlaceLine(Label label, float centre, float height)
    {
        label.Position = new Vector2(0f, centre - height / 2f);
        label.Size = new Vector2(Size.X, height);
        label.PivotOffset = label.Size / 2f;
    }

    // Called after every native change of enablement, shine, turn, vote or hover state.
    internal void Refresh()
    {
        if (!GodotObject.IsInstanceValid(_button) || _label == null) return;
        bool enabled = _button.IsEnabled;
        // A long press ends the turn without a release event; NClickableControl.Disable drops the press too.
        if (!enabled) _pressed = false;
        CombatState state = CombatStateField?.GetValue(_button) as CombatState;
        Player me = state == null ? null : LocalContext.GetMe(state);
        CombatManager combat = CombatManager.Instance;
        bool ended = me != null && combat != null && combat.IsPlayerReadyToEndTurn(me);
        bool shiny = enabled && !ended && ShinyField?.GetValue(_button) is true;
        bool warn = enabled && _hovered && !ended && me?.PlayerCombatState?.HasCardsToPlay() == true;

        SetShiny(shiny);
        FadeTo(ref _activeTween, ref _activeTarget, _active, enabled && (shiny || _hovered) ? 1f : 0f);
        FadeTo(ref _disabledTween, ref _disabledTarget, _disabled, enabled ? 0f : 1f);

        if (enabled && ended)
        {
            _label.Text = HbrBattleHudLayout.UndoLabel;
            _sub.Text = WaitingText(state, combat);
        }
        else
        {
            _label.Text = HbrBattleHudLayout.EndTurnLabel;
            _sub.Text = warn ? HbrBattleHudLayout.EndTurnWarn : HbrBattleHudLayout.EndTurnSub;
        }
        StyleText(!enabled, warn);
        ScaleTo(!enabled ? 1f : _pressed ? HbrBattleHudLayout.DiscPressScale :
            _hovered ? HbrBattleHudLayout.DiscHoverScale : 1f);
    }

    private static string WaitingText(CombatState state, CombatManager combat)
    {
        var acting = state.Players.Where(player => player.Creature?.IsAlive == true).ToList();
        if (combat.PlayersTakingExtraTurn.Count > 0)
            acting = acting.Where(player => combat.PlayersTakingExtraTurn.Contains(player)).ToList();
        int ready = acting.Count(combat.IsPlayerReadyToEndTurn);
        return HbrBattleHudLayout.WaitingText(ready, acting.Count);
    }

    internal void SetHovered(bool hovered)
    {
        _hovered = hovered;
        if (!hovered) _pressed = false;
        Refresh();
    }

    internal void SetPressed(bool pressed)
    {
        _pressed = pressed && GodotObject.IsInstanceValid(_button) && _button.IsEnabled;
        Refresh();
    }

    // AnimIn starts its slide from wherever the button is; a hidden disc rises the preview's 70px.
    internal void PrepareShow()
    {
        if (_hidden) _button.Position = HidePosition(_button.GetViewport().GetVisibleRect().Size);
    }

    internal void SlideIn() => Animate(hidden: false);

    internal void SlideOut() => Animate(hidden: true);

    private void Animate(bool hidden)
    {
        _hidden = hidden;
        _showTween?.Kill();
        _showTween = CreateTween().SetParallel();
        _showTween.TweenProperty(_button, "modulate:a", hidden ? 0f : 1f, 0.3);
        _showTween.TweenProperty(_button, "scale", Vector2.One * (hidden ? HbrBattleHudLayout.DiscHiddenScale : 1f), 0.5)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(hidden ? Tween.TransitionType.Expo : Tween.TransitionType.Back);
    }

    private void SetShiny(bool shiny)
    {
        if (_shiny == shiny) return;
        _shiny = shiny;
        _auraTween?.Kill();
        if (!shiny)
        {
            _auraTween = CreateTween();
            _auraTween.TweenProperty(_aura, "modulate:a", 0f, 0.3);
            return;
        }
        // auraCircle pulses outwards while no card is playable: scale .9 -> 1.34, opacity .9 -> 0, 1.6s.
        _auraTween = CreateTween().SetLoops();
        _auraTween.TweenProperty(_aura, "scale", Vector2.One * HbrBattleHudLayout.DiscAuraToScale, HbrBattleHudLayout.DiscAuraPeriod)
            .From(Vector2.One * HbrBattleHudLayout.DiscAuraFromScale)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        _auraTween.Parallel().TweenProperty(_aura, "modulate:a", 0f, HbrBattleHudLayout.DiscAuraPeriod)
            .From(HbrBattleHudLayout.DiscAuraFromAlpha)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
    }

    private void FadeTo(ref Tween tween, ref float current, CanvasItem item, float alpha)
    {
        if (Mathf.IsEqualApprox(current, alpha)) return;
        current = alpha;
        tween?.Kill();
        tween = CreateTween();
        tween.TweenProperty(item, "modulate:a", alpha, 0.15);
    }

    private void ScaleTo(float scale)
    {
        if (Mathf.IsEqualApprox(_scaleTarget, scale)) return;
        bool press = scale < _scaleTarget && scale < 1f;
        _scaleTarget = scale;
        _scaleTween?.Kill();
        _scaleTween = CreateTween();
        _scaleTween.TweenProperty(_visuals, "scale", Vector2.One * scale, press ? 0.15 : 0.3)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private void StyleText(bool disabled, bool warn)
    {
        int style = disabled ? 2 : warn ? 1 : 0;
        if (_subStyle == style) return;
        _subStyle = style;
        if (disabled)
        {
            _label.AddThemeColorOverride("font_color", DisabledColor);
            HbrBattleHudArt.Outline(_label, new Color(0.067f, 0.067f, 0.067f), 4);
            _sub.AddThemeColorOverride("font_color", DisabledColor);
        }
        else
        {
            _label.AddThemeColorOverride("font_color", Colors.White);
            HbrBattleHudArt.Outline(_label, LabelOutline, 4);
            HbrBattleHudArt.Glow(_label, new Color(LabelGlow, 0.32f), 12);
            _sub.AddThemeColorOverride("font_color", warn ? WarnColor : SubColor);
        }
        // .warn .sub: 16px with .06em spacing instead of 15px with .26em.
        _sub.AddThemeFontSizeOverride("font_size", warn ? HbrBattleHudLayout.DiscWarnSize : HbrBattleHudLayout.DiscSubSize);
        _sub.AddThemeFontOverride("font", HbrBattleHudArt.Spacing(warn ? 1 : 4));
    }
}
