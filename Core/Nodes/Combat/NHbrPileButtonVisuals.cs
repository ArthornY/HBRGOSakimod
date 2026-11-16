using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// A combat pile drawn as an HBR HUD tool circle (circle1 + gaugeCircleOff01 + StatusEffect glow)
// with a TerminalIcon glyph and the UIBattleAttack remaining-count badge. The native pile keeps
// its input, hotkey, hover tip, pile screen and count bookkeeping; its icon and count art are hidden.
public partial class NHbrPileButtonVisuals : Control
{
    internal const string NodeName = "HbrPileVisuals";

    private static readonly FieldInfo CountLabelField = AccessTools.Field(typeof(NCombatCardPile), "_countLabel");
    private static readonly FieldInfo ShowField = AccessTools.Field(typeof(NCombatCardPile), "_showPosition");
    private static readonly FieldInfo HideField = AccessTools.Field(typeof(NCombatCardPile), "_hidePosition");
    private static readonly FieldInfo ExhaustOffsetField = AccessTools.Field(typeof(NExhaustPileButton), "_posOffset");

    private static readonly Color GlowColor = new(HbrBattleHudLayout.PileGlowColor);
    private static readonly Color DiscColor = new(HbrBattleHudLayout.PileDiscColor);
    private static readonly Color DiscPressedColor = new(HbrBattleHudLayout.PileDiscPressedColor);
    private static readonly Color RingColor = new(HbrBattleHudLayout.PileRingColor);
    private static readonly Color RingHoverColor = new(HbrBattleHudLayout.PileRingHoverColor);
    private static readonly Color CountRingColor = new(HbrBattleHudLayout.CountRingColor);
    private static readonly Color EmptyTextColor = new(HbrBattleHudLayout.EmptyCountTextColor);
    private static readonly Color TextOutline = new(0.07f, 0.03f, 0.09f, 0.9f);

    private NCombatCardPile _pile;
    private HbrPileKind _kind;
    private Control _visuals;
    private TextureRect _glow;
    private TextureRect _disc;
    private TextureRect _ring;
    private TextureRect _icon;
    private Control _count;
    private TextureRect _countDisc;
    private Label _countLabel;
    private Tween _scaleTween;
    private Tween _colourTween;
    private Tween _countTween;
    private Tween _fadeTween;
    private float _scaleTarget = 1f;
    private bool _hovered;
    private bool _pressed;
    private bool _empty;
    private int _shownCount = -1;

    internal static NHbrPileButtonVisuals Of(NCombatCardPile pile) =>
        GodotObject.IsInstanceValid(pile) ? pile.GetNodeOrNull<NHbrPileButtonVisuals>(NodeName) : null;

    internal static void Attach(NCombatCardPile pile)
    {
        if (!GodotObject.IsInstanceValid(pile)) return;
        NHbrPileButtonVisuals visuals = Of(pile);
        if (visuals == null)
        {
            visuals = new NHbrPileButtonVisuals { Name = NodeName, _pile = pile, _kind = KindOf(pile) };
            pile.AddChild(visuals);
            // Behind the controller hotkey icon.
            pile.MoveChild(visuals, 0);
            visuals.Build();
            visuals.Relayout();
        }
        visuals.SyncCount(bump: false);
    }

    private static HbrPileKind KindOf(NCombatCardPile pile) => pile switch
    {
        NDrawPileButton => HbrPileKind.Draw,
        NDiscardPileButton => HbrPileKind.Discard,
        _ => HbrPileKind.Exhaust
    };

    private void Build()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (string path in new[] { "Icon", "CountContainer" })
        {
            if (_pile.GetNodeOrNull<CanvasItem>(path) is { } native) native.Visible = false;
        }
        float size = HbrBattleHudLayout.PileSize;
        Position = Vector2.Zero;
        Size = new Vector2(size, size);

        _visuals = new Control { Name = "Visuals", Size = Size, PivotOffset = Size / 2f, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_visuals);
        _glow = HbrBattleHudArt.Sprite("Glow", "StatusEffect", HbrBattleHudLayout.PileGlow, new Color(GlowColor, 0f));
        _visuals.AddChild(_glow);
        _disc = HbrBattleHudArt.Sprite("Disc", "circle1", HbrBattleHudLayout.PileDisc, DiscColor);
        _visuals.AddChild(_disc);
        _ring = HbrBattleHudArt.Sprite("Ring", "gaugeCircleOff01", HbrBattleHudLayout.PileRing, RingColor);
        _visuals.AddChild(_ring);
        float icon = HbrBattleHudLayout.PileIconSize(_kind);
        _icon = HbrBattleHudArt.Sprite("Icon", null, new HbrHudRect((size - icon) / 2f, (size - icon) / 2f, icon, icon));
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _visuals.AddChild(_icon);

        HbrHudRect badge = HbrBattleHudLayout.PileCount;
        _count = new Control
        {
            Name = "Count",
            Position = new Vector2(badge.X, badge.Y),
            Size = new Vector2(badge.Width, badge.Height),
            PivotOffset = new Vector2(badge.Width, badge.Height) / 2f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _visuals.AddChild(_count);
        float inset = HbrBattleHudLayout.CountDiscInset;
        _countDisc = HbrBattleHudArt.Sprite("Disc", "circle1",
            new HbrHudRect(inset, inset, badge.Width - inset * 2f, badge.Height - inset * 2f));
        _count.AddChild(_countDisc);
        _count.AddChild(HbrBattleHudArt.Sprite("Ring", "gaugeCircleOff01",
            new HbrHudRect(0f, 0f, badge.Width, badge.Height), CountRingColor));
        _countLabel = HbrBattleHudArt.Label("Number", "0", HbrBattleHudLayout.CountTextSize, Colors.White);
        HbrBattleHudArt.Outline(_countLabel, TextOutline, 5);
        _countLabel.Position = new Vector2(-8f, -8f);
        _countLabel.Size = new Vector2(badge.Width + 16f, badge.Height + 16f);
        _count.AddChild(_countLabel);
        ApplyState(animate: false);
    }

    // The preview layout: draw at (35, H-150), discard at (W-405, H-150), exhaust at (W-405, H-280).
    // The native piles remember where they slide in from and to, so those are moved as well.
    private void Relayout()
    {
        HbrHudRect offsets = HbrBattleHudLayout.PileOffsets(_kind);
        float anchor = _kind == HbrPileKind.Draw ? 0f : 1f;
        _pile.AnchorLeft = anchor;
        _pile.AnchorRight = anchor;
        _pile.AnchorTop = 1f;
        _pile.AnchorBottom = 1f;
        _pile.OffsetLeft = offsets.X;
        _pile.OffsetTop = offsets.Y;
        _pile.OffsetRight = offsets.Right;
        _pile.OffsetBottom = offsets.Bottom;
        if (_kind == HbrPileKind.Exhaust)
        {
            // NExhaustPileButton recomputes its slots from this offset whenever the window resizes.
            ExhaustOffsetField?.SetValue(_pile, new Vector2(-offsets.X, -offsets.Y));
            Vector2 show = NGame.Instance != null ? NGame.Instance.Size + new Vector2(offsets.X, offsets.Y) : _pile.Position;
            ShowField?.SetValue(_pile, show);
        }
        else
        {
            ShowField?.SetValue(_pile, _pile.Position);
        }
        FixHidePosition();
    }

    internal void FixHidePosition()
    {
        if (ShowField?.GetValue(_pile) is not Vector2 show) return;
        (float x, float y) = HbrBattleHudLayout.PileHideShift(_kind);
        HideField?.SetValue(_pile, show + new Vector2(x, y));
    }

    // NCombatCardPile updates its own count label as each card lands; the badge copies it.
    internal void SyncCount(bool bump)
    {
        string text = (CountLabelField?.GetValue(_pile) as Label)?.Text ?? "0";
        int count = int.TryParse(text, out int parsed) ? parsed : 0;
        bool grew = _shownCount >= 0 && count > _shownCount;
        _shownCount = count;
        _countLabel.Text = text;
        if (_empty != (count == 0))
        {
            _empty = count == 0;
            ApplyState(animate: true);
        }
        if (bump && grew) Bump();
    }

    internal void SetHovered(bool hovered)
    {
        _hovered = hovered;
        if (!hovered) _pressed = false;
        ApplyState(animate: true);
    }

    internal void SetPressed(bool pressed)
    {
        _pressed = pressed;
        ApplyState(animate: true);
    }

    // AnimIn slides the pile in from its hide slot; it also fades in, as the exhaust pile does in the preview.
    internal void FadeIn() => Fade(1f, from: 0f);

    internal void FadeOut() => Fade(0f, from: null);

    private void Fade(float alpha, float? from)
    {
        _fadeTween?.Kill();
        if (from.HasValue) _pile.Modulate = new Color(_pile.Modulate, from.Value);
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_pile, "modulate:a", alpha, 0.3);
    }

    private void Bump()
    {
        _visuals.Scale = Vector2.One * HbrBattleHudLayout.PileBumpScale;
        _count.Scale = Vector2.One * HbrBattleHudLayout.CountBumpScale;
        _scaleTween?.Kill();
        _scaleTween = CreateTween().SetParallel();
        _scaleTween.TweenProperty(_visuals, "scale", Vector2.One * _scaleTarget, 0.5)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        _countTween?.Kill();
        _countTween = CreateTween();
        _countTween.TweenProperty(_count, "scale", Vector2.One, 0.5)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private void ApplyState(bool animate)
    {
        _icon.Texture = HbrBattleHudArt.Texture(HbrBattleHudLayout.PileIconPath(_kind, _hovered, _empty));
        _countDisc.Modulate = new Color(HbrBattleHudLayout.CountDisc(_kind, _empty));
        _countLabel.AddThemeColorOverride("font_color", _empty ? EmptyTextColor : Colors.White);

        float glow = _hovered ? HbrBattleHudLayout.PileGlowHoverAlpha : 0f;
        Color ring = _hovered ? RingHoverColor : RingColor;
        Color disc = _pressed ? DiscPressedColor : DiscColor;
        float iconAlpha = HbrBattleHudLayout.PileIconAlpha(_hovered, _empty);
        _colourTween?.Kill();
        if (!animate)
        {
            _glow.Modulate = new Color(GlowColor, glow);
            _ring.Modulate = ring;
            _disc.Modulate = disc;
            _icon.Modulate = new Color(1f, 1f, 1f, iconAlpha);
        }
        else
        {
            _colourTween = CreateTween().SetParallel();
            _colourTween.TweenProperty(_glow, "modulate", new Color(GlowColor, glow), 0.15);
            _colourTween.TweenProperty(_ring, "modulate", ring, 0.15);
            _colourTween.TweenProperty(_disc, "modulate", disc, 0.15);
            _colourTween.TweenProperty(_icon, "modulate:a", iconAlpha, 0.2);
        }

        // .pile.hover .vis: 1.08 in .1s; .pressed: .94 in .15s; otherwise back to 1 over .5s.
        float scale = _pressed ? HbrBattleHudLayout.PilePressScale : _hovered ? HbrBattleHudLayout.PileHoverScale : 1f;
        if (Mathf.IsEqualApprox(scale, _scaleTarget)) return;
        _scaleTarget = scale;
        double duration = _pressed ? 0.15 : _hovered ? 0.1 : 0.5;
        if (!animate)
        {
            _visuals.Scale = Vector2.One * scale;
            return;
        }
        _scaleTween?.Kill();
        _scaleTween = CreateTween();
        _scaleTween.TweenProperty(_visuals, "scale", Vector2.One * scale, duration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }
}
