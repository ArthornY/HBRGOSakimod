using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// 30G proposal card faces (requirement 72, picture side): 「卒」 greys and scorches the card, a soul floats
// over it and a red seal sits on the art; 「心动」 gives a pink glow behind, a pink rim and heart marks under
// the cost orb. It is a layer drawn over the card, never the card's own art. State is read only through
// ThirtyGProposalState.Get and its Changed event. The proposal performance holds its own card with HoldMeta.
public partial class NThirtyGCardDecor : Control
{
    internal const string NodeName = "HbrThirtyGDecor";
    internal const string HoldMeta = "hbr30g_hold";
    internal const string ArtRoot = "res://images/ui/thirty_g_proposal/";
    // Card-local layout. The card is 300 x 422 round its centre; the full-card images (glow, rim, mask,
    // scorch) are 2 texture px per card px with 35 card px of margin, so they cover this rectangle.
    internal static readonly Vector2 SheetPos = new(-185, -246), SheetSize = new(370, 492);
    internal static readonly Vector2 SoulSize = new(142.5f, 178.1f);
    internal const float StampSize = 208f;
    internal static readonly Color PillTsukkomi = new(40 / 255f, 70 / 255f, 140 / 255f, 235 / 255f);
    internal static readonly Color PillSerious = new(150 / 255f, 40 / 255f, 100 / 255f, 235 / 255f);
    internal static readonly Color PillKiryu = new(70 / 255f, 50 / 255f, 40 / 255f, 235 / 255f);
    internal static readonly Color PillDischarge = new(150 / 255f, 110 / 255f, 20 / 255f, 235 / 255f);
    private static readonly object Marker = new();
    private static readonly Dictionary<string, Texture2D> Sprites = new(StringComparer.Ordinal);
    // 6-type reaction 「当真」 only reaches the picture side through the performance request.
    private static readonly ConditionalWeakTable<CardModel, object> Serious = new();
    private static Shader _greyShader;
    internal const string GreyShaderCode = """
        shader_type canvas_item;
        // Greys what is already on screen under the card silhouette (the texture is the silhouette).
        uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
        uniform float amount = 1.0;
        void fragment() {
            float m = texture(TEXTURE, UV).a;
            vec3 c = textureLod(screen_tex, SCREEN_UV, 0.0).rgb;
            float l = dot(c, vec3(0.3, 0.59, 0.11));
            vec3 g = vec3(l * 0.78 + 10.0 / 255.0) * 0.92 + c * 0.08;
            COLOR = vec4(g, m * amount);
        }
        """;

    private NCard _card;
    private Control _back;
    private TextureRect _glow, _rim, _grey, _scorch, _stamp, _flash;
    private BackBufferCopy _copy;
    private readonly TextureRect[] _souls = new TextureRect[2];
    private readonly TextureRect[] _marks = new TextureRect[2];
    private readonly TextureRect[] _floaters = new TextureRect[3];
    private PanelContainer _pill;
    private Label _tag;
    private string _pillKey = "";
    private ThirtyGCardState _state;
    private bool _built, _subscribed;
    private double _clock;
    private float _heartIn = 1f;
    private float _revive = -1f;

    internal static bool English => LocManager.Instance?.Language is { } language &&
        !language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    internal static Texture2D Sprite(string name)
    {
        if (Sprites.TryGetValue(name, out Texture2D cached)) return cached;
        string path = ArtRoot + name;
        Texture2D texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Sprites[name] = texture;
        return texture;
    }

    internal static Shader GreyShader => _greyShader ??= new Shader { Code = GreyShaderCode };

    internal static void RememberProposal(ThirtyGProposalPresentation proposal)
    {
        if (proposal?.TargetCard != null && proposal.Reaction == 3) Serious.AddOrUpdate(proposal.TargetCard, Marker);
    }

    internal static TextureRect Picture(Texture2D texture, Vector2 position, Vector2 size) => new()
    {
        Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
        Position = position, Size = size, PivotOffset = size * 0.5f, MouseFilter = MouseFilterEnum.Ignore
    };

    // Places a centred picture: size stays, the centre, scale, rotation and alpha change.
    internal static void Place(TextureRect picture, Vector2 centre, float scale, float rotation, float alpha)
    {
        if (picture == null) return;
        picture.Visible = alpha > 0.001f && scale > 0.001f;
        if (!picture.Visible) return;
        picture.Position = centre - picture.Size * 0.5f;
        picture.Scale = new Vector2(scale, scale);
        picture.Rotation = rotation;
        picture.Modulate = new Color(1, 1, 1, Mathf.Clamp(alpha, 0f, 1f));
    }

    internal static PanelContainer Pill(string text, Color fill, float fontSize, float border)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = Colors.White };
        box.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(border)));
        box.SetCornerRadiusAll(Mathf.RoundToInt(fontSize + 9));
        box.ContentMarginLeft = box.ContentMarginRight = fontSize * 0.9f;
        box.ContentMarginTop = box.ContentMarginBottom = fontSize * 0.28f;
        var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        pill.AddThemeStyleboxOverride("panel", box);
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", Mathf.Max(8, Mathf.RoundToInt(fontSize)));
        label.AddThemeColorOverride("font_color", Colors.White);
        pill.AddChild(label);
        return pill;
    }

    internal static (string Text, Color Fill) GraduatedTag(bool miya) =>
        miya ? (English ? "Nullified this battle" : "本场无效", PillKiryu)
        : (English ? "Next play nullified" : "下次打出无效", PillTsukkomi);

    internal static void Update(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card) || card.Body == null) return;
        NThirtyGCardDecor node = card.Body.GetNodeOrNull<NThirtyGCardDecor>(NodeName);
        if (card.HasMeta(HoldMeta)) { node?.Clear(); return; }
        CardModel model = card.Model;
        ThirtyGCardState state = model == null ? default : ThirtyGProposalState.Get(model);
        if (node == null)
        {
            if (!state.Graduated && state.HeartfeltExtraPlays <= 0) return;
            node = new NThirtyGCardDecor { Name = NodeName, _card = card, MouseFilter = MouseFilterEnum.Ignore };
            card.Body.AddChild(node);
            node._state = default;
        }
        node.Apply(state, animate: node._built);
    }

    internal static void Reset(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card)) return;
        if (card.HasMeta(HoldMeta)) card.RemoveMeta(HoldMeta);
        card.Body?.GetNodeOrNull<NThirtyGCardDecor>(NodeName)?.Clear();
    }

    public override void _EnterTree()
    {
        _card ??= GetParent()?.GetParent() as NCard;
        if (_subscribed) return;
        _subscribed = true;
        ThirtyGProposalState.Changed += OnStateChanged;
        if (_card != null) _card.ModelChanged += OnModelChanged;
    }

    public override void _ExitTree()
    {
        if (!_subscribed) return;
        _subscribed = false;
        ThirtyGProposalState.Changed -= OnStateChanged;
        if (_card != null) _card.ModelChanged -= OnModelChanged;
    }

    private void OnModelChanged(CardModel _) => Refresh(animate: false);

    private void OnStateChanged(CardModel changed)
    {
        CardModel model = GodotObject.IsInstanceValid(_card) ? _card.Model : null;
        if (model == null || changed == null) return;
        if (ReferenceEquals(changed, model) || ReferenceEquals(changed, model.DeckVersion)) Refresh(animate: true);
    }

    private void Refresh(bool animate)
    {
        if (!GodotObject.IsInstanceValid(_card) || !IsInsideTree()) return;
        if (_card.HasMeta(HoldMeta) || _card.Model == null) { Clear(); return; }
        Apply(ThirtyGProposalState.Get(_card.Model), animate);
    }

    internal void Clear()
    {
        _state = default;
        _revive = -1f;
        Visible = false;
        if (GodotObject.IsInstanceValid(_back)) _back.Visible = false;
        SetProcess(false);
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        Position = Vector2.Zero;
        Size = Vector2.Zero;
        // Behind the card: the 心动 glow. A direct child of the card body drawn behind the body itself.
        _back = new Control { Name = NodeName + "Back", ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore };
        _glow = Picture(Sprite("glow.png"), SheetPos * 1.04f, SheetSize * 1.04f);
        _back.AddChild(_glow);
        GetParent().AddChild(_back);
        // Over the card: grey (reads the card from the screen), scorch, 心动 rim, souls, seal, tags.
        _copy = new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Rect, Rect = new Rect2(-200, -262, 400, 524) };
        AddChild(_copy);
        _grey = Picture(Sprite("mask.png"), SheetPos, SheetSize);
        _grey.Material = new ShaderMaterial { Shader = GreyShader };
        AddChild(_grey);
        _scorch = Picture(Sprite("scorch.png"), SheetPos, SheetSize);
        AddChild(_scorch);
        _flash = Picture(Sprite("mask.png"), SheetPos, SheetSize);
        AddChild(_flash);
        _rim = Picture(Sprite("rim.png"), SheetPos, SheetSize);
        AddChild(_rim);
        for (int i = 0; i < _souls.Length; i++)
        {
            _souls[i] = Picture(Sprite("soul.png"), Vector2.Zero, SoulSize);
            AddChild(_souls[i]);
        }
        _stamp = Picture(Sprite("stamp.png"), Vector2.Zero, new Vector2(StampSize, StampSize));
        AddChild(_stamp);
        for (int i = 0; i < _marks.Length; i++)
        {
            _marks[i] = Picture(Sprite("heart.png"), Vector2.Zero, new Vector2(26.9f, 26.9f));
            AddChild(_marks[i]);
        }
        for (int i = 0; i < _floaters.Length; i++)
        {
            _floaters[i] = Picture(Sprite("heart.png"), Vector2.Zero, new Vector2(20f, 20f));
            AddChild(_floaters[i]);
        }
        _tag = new Label { Text = English ? "Retort! Revived" : "吐槽！回魂了", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _tag.AddThemeFontSizeOverride("font_size", 34);
        _tag.AddThemeColorOverride("font_color", Colors.White);
        _tag.AddThemeColorOverride("font_outline_color", new Color(27 / 255f, 21 / 255f, 48 / 255f));
        _tag.AddThemeConstantOverride("outline_size", 10);
        AddChild(_tag);
    }

    private void Apply(ThirtyGCardState state, bool animate)
    {
        ThirtyGCardState before = _state;
        _state = state;
        if (!state.Graduated && state.HeartfeltExtraPlays <= 0 && _revive < 0f && !(animate && before.Graduated && before.ReleaseOnSecondTurn))
        {
            Clear();
            return;
        }
        Build();
        if (animate && before.Graduated && !state.Graduated && before.ReleaseOnSecondTurn && _card.Model?.Owner?.Creature?.CombatState != null)
            _revive = 0f;
        // Every time a card starts showing 心动 (into the hand, a pile view opened) the glow fades in and the hearts pop.
        if (state.HeartfeltExtraPlays > 0 && before.HeartfeltExtraPlays <= 0) _heartIn = 0f;
        UpdatePill();
        Visible = true;
        _back.Visible = true;
        SetProcess(true);
        Render(0f);
    }

    private void UpdatePill()
    {
        (string text, Color fill) = _state.Graduated ? GraduatedTag(_state.WholeBattleNullified) : ("", default);
        string key = text + "|" + fill.ToHtml();
        if (key == _pillKey) return;
        _pillKey = key;
        if (GodotObject.IsInstanceValid(_pill)) { _pill.QueueFree(); _pill = null; }
        if (string.IsNullOrEmpty(text)) return;
        _pill = Pill(text, fill, 17f, 2f);
        AddChild(_pill);
        _pill.ResetSize();
        _pill.Position = new Vector2(-_pill.Size.X * 0.5f, 187f - _pill.Size.Y * 0.5f);
    }

    public override void _Process(double delta)
    {
        if (!Visible) { SetProcess(false); return; }
        try { Render((float)delta); }
        catch (Exception error)
        {
            GD.PushWarning("[HBRGOSakiMod][30G] Card face stopped: " + error.Message);
            Clear();
        }
    }

    private void Render(float delta)
    {
        _clock += delta;
        float t = (float)_clock;
        bool graduated = _state.Graduated;
        bool reviving = _revive >= 0f;
        if (reviving) _revive += delta;
        float r = reviving ? _revive : 0f;
        if (reviving && r >= 2.3f) { _revive = -1f; reviving = false; if (!graduated && _state.HeartfeltExtraPlays <= 0) { Clear(); return; } }
        bool zu = graduated || reviving;
        float colour = reviving ? 1f - EaseInOut(Seg(r, .95f, 1.25f)) : graduated ? 1f : 0f;
        _copy.Visible = zu && colour > 0f;
        _grey.Visible = _copy.Visible;
        ((ShaderMaterial)_grey.Material).SetShaderParameter("amount", colour);
        _scorch.Visible = zu && colour > 0f;
        _scorch.Modulate = new Color(1, 1, 1, colour);
        // Reaction colours are presentation only; all recipients use one soul.
        bool serious = _card?.Model != null && Serious.TryGetValue(_card.Model, out _);
        int souls = zu ? 1 : 0;
        for (int i = 0; i < _souls.Length; i++)
        {
            if (i >= souls) { _souls[i].Visible = false; continue; }
            _souls[i].Texture = Sprite(serious ? "soul_pink.png" : "soul.png");
            float phase = t + i * 0.7f;
            float offset = souls == 2 ? (i == 0 ? -58f : 58f) : 0f;
            float rise = souls == 2 ? (i == 0 ? 92f : 70f) : 85f;
            float alpha = 1f;
            if (reviving) { rise *= 1f - EaseInOut(Seg(r, .35f, .95f)); alpha = 1f - Seg(r, .85f, 1f); }
            Place(_souls[i], new Vector2(offset, -217f - rise + Mathf.Sin(phase * 5f) * 3f), 1f,
                -Mathf.DegToRad(Mathf.Sin(phase * 3f) * 6f), alpha);
        }
        if (zu)
        {
            float k = reviving ? 1f + 0.25f * Seg(r, .95f, 1.2f) : 1f;
            Place(_stamp, new Vector2(0, -35), k, Mathf.DegToRad(14f), reviving ? 1f - Seg(r, .95f, 1.2f) : 1f);
        }
        else _stamp.Visible = false;
        float flash = reviving && r > .92f && r < 1.3f ? 150f / 255f * (1f - Seg(r, .95f, 1.3f)) : 0f;
        _flash.Visible = flash > 0f;
        _flash.Modulate = new Color(1, 1, 1, flash);
        if (GodotObject.IsInstanceValid(_pill)) _pill.Visible = graduated && !reviving;
        float tagK = reviving ? Seg(r, 1.05f, 1.25f) * (1f - Seg(r, 1.9f, 2.2f)) : 0f;
        _tag.Visible = tagK > 0f;
        if (_tag.Visible)
        {
            _tag.ResetSize();
            float s = Mathf.Lerp(1.4f, 1f, OutBack(Seg(r, 1.05f, 1.25f), 2f));
            _tag.PivotOffset = _tag.Size * 0.5f;
            _tag.Position = new Vector2(0, -251) - _tag.Size * 0.5f;
            _tag.Scale = new Vector2(s, s);
            _tag.Modulate = new Color(1, 1, 1, tagK);
        }
        // 心动: glow behind, rim, one heart per extra play under the cost orb, a few hearts drifting up.
        int hearts = graduated ? 0 : Mathf.Clamp(_state.HeartfeltExtraPlays, 0, 2);
        if (hearts > 0 && _heartIn < 1f) _heartIn = Mathf.Min(1f, _heartIn + delta / 0.5f);
        float strength = hearts >= 2 ? 1f : 0.75f;
        float pulse = 0.85f + 0.15f * Mathf.Sin(t * 2.5f);
        _glow.Visible = hearts > 0;
        _glow.Modulate = new Color(1, 1, 1, strength * pulse * _heartIn);
        _rim.Visible = hearts > 0;
        _rim.Modulate = new Color(1, 1, 1, (hearts >= 2 ? 1f : 0.8f) * _heartIn);
        for (int j = 0; j < _marks.Length; j++)
        {
            if (j >= hearts) { _marks[j].Visible = false; continue; }
            float pop = OutBack(Seg(_heartIn, 0.3f + j * 0.15f, 0.8f + j * 0.15f), 2.4f);
            Place(_marks[j], new Vector2(-138f + j * 20f, -141f - j * 4f), pop, 0f, 1f);
        }
        for (int i = 0; i < _floaters.Length; i++)
        {
            if (hearts <= 0) { _floaters[i].Visible = false; continue; }
            float ph = (t * 0.6f + i / 3f) % 1f;
            Place(_floaters[i], new Vector2(-90f + i * 90f, -221f - ph * 80f), 1f, 0f, (1f - ph) * 0.8f * _heartIn * (hearts >= 2 ? 1f : 0.85f));
        }
        if (!zu && hearts <= 0 && !reviving) Clear();
    }

    internal static float Seg(float t, float a, float b) => b <= a ? (t >= a ? 1f : 0f) : Mathf.Clamp((t - a) / (b - a), 0f, 1f);
    internal static float EaseInOut(float x) => 3f * x * x - 2f * x * x * x;
    internal static float OutBack(float x, float s = 1.7f)
    {
        x -= 1f;
        return 1f + (s + 1f) * x * x * x + s * x * x;
    }
}
