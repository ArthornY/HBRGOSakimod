using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// Shows the mod's own cards in the HBR card style. A pre-rendered frame replaces the vanilla frame,
// title banner and portrait border; the portrait, title, description and type plaque move to the new layout.
// NCard instances are pooled and reused for vanilla cards, so everything moved here is put back for them.
public partial class NHbrCardStyle : Node
{
    public NCard Card { get; set; }

    internal static NHbrCardStyle Of(NCard card) =>
        GodotObject.IsInstanceValid(card) && HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
            card, HBRGOSakiCombatUiIntegration.CardStyleAttachmentId, out NHbrCardStyle style) ? style : null;

    private Control _container;
    private TextureRect _frame;
    private TextureRect _portrait;
    private TextureRect _portraitBorder;
    private TextureRect _titleBanner;
    private MegaLabel _title;
    private RichTextLabel _description;
    private NinePatchRect _typePlaque;
    private Label _typeLabel;
    private TextureRect _phantomGlow;
    private NHbrCardTypePlate _typePlate;
    private Original _original;
    private bool _styled;
    private HbrCardKind _kind;
    private HbrCardTheme _theme;
    // Memory card names (HbrMemoryTitleState): the garden keeps hidden characters jumping by renewing this lease
    // every frame; a decode belongs to the model it started on, so a pooled card moving on drops it.
    private double _jumpUntil;
    private long _lastJump = long.MinValue;
    private CardModel _decodeModel;
    private double _decodeStart;
    private double _decodeLength;
    private int _decodeFrom;

    // Vanilla values of everything this node moves, taken before the first change.
    private sealed record Original(HbrCardRect Portrait, HbrCardRect Title, HbrCardRect Description,
        HbrCardRect TypePlaque, HbrCardRect TypeLabel, Texture2D TypePlaqueTexture, Color DescriptionColor, Color TypeLabelColor,
        TextureRect.StretchModeEnum PortraitStretch);

    public override void _Ready()
    {
        Card ??= GetParent() as NCard;
        Refresh();
        // Only memory cards that are jumping or decoding need a frame callback.
        SetProcess(false);
    }

    internal void Refresh()
    {
        if (!GodotObject.IsInstanceValid(Card) || Card.IsQueuedForDeletion() || !Card.IsNodeReady() || !FindNodes())
            return;
        if (!TryResolve(Card.Model, out HbrCardTheme theme, out HbrCardTier tier, out HbrCardKind kind))
        {
            Restore();
            return;
        }
        _original ??= new Original(OffsetsOf(_portrait), OffsetsOf(_title), OffsetsOf(_description),
            OffsetsOf(_typePlaque), OffsetsOf(_typeLabel), _typePlaque.Texture,
            _description.GetThemeColor("default_color"), _typeLabel.GetThemeColor("font_color"), VanillaPortraitStretch);

        // Reload has just assigned the vanilla frame, banner and border for this model; replace them.
        _frame.Texture = HbrCardStyleTextures.Get(HbrCardStyleLayout.FramePath(theme, tier));
        _frame.Material = null;
        _portraitBorder.Visible = false;
        _titleBanner.Visible = false;
        SetOffsets(_portrait, HbrCardStyleLayout.Portrait);
        // The HBR art window has no portrait border to hide letterboxing behind: art of any shape
        // (the 1536x856 fist strike, square or tall portraits) fills it and is cropped instead.
        _portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        SetOffsets(_title, HbrCardStyleLayout.Title);
        SetOffsets(_description, HbrCardStyleLayout.Description);
        _description.AddThemeColorOverride("default_color", new Color(HbrCardStyleLayout.DescriptionColor));
        _typeLabel.AddThemeColorOverride("font_color", Colors.White);
        ShowPhantomGlow(theme == HbrCardTheme.Phantom);
        _kind = kind;
        _theme = theme;
        _styled = true;
        LayoutTypePlaque();
        DecorateTitle();
    }

    // Memory cards print their names in 「」, as everywhere in the garden, and the fourteen garden memories show only
    // the part of the name they have earned (HbrMemoryTitleState). Vanilla rewrites the title on every visual update,
    // so HbrCardStylePatch calls this again after it; the text is always rebuilt from the model, never from the label.
    internal void DecorateTitle()
    {
        if (!_styled || _theme != HbrCardTheme.Memory || !GodotObject.IsInstanceValid(_title)) return;
        if (Card.Model is { } model && Card.Visibility == ModelVisibility.Visible && HbrMemoryTitleState.Applies(model))
        {
            RenderMemoryTitle(model);
            return;
        }
        string text = _title.Text;
        if (!string.IsNullOrEmpty(text) && !text.StartsWith('「')) _title.SetTextAutoSize($"「{text}」");
    }

    // The garden decor calls this every frame: while it does, a few hidden characters jump with the static.
    internal void KeepTitleJumping()
    {
        if (!_styled || _theme != HbrCardTheme.Memory) return;
        _jumpUntil = Now + .25;
        SetProcess(true);
    }

    // Decodes the characters that came back since `fromParts`, one by one; returns how many seconds that takes.
    internal double PlayTitleReveal(int fromParts)
    {
        if (!_styled || _theme != HbrCardTheme.Memory || !GodotObject.IsInstanceValid(_title) ||
            Card.Model is not { } model || Card.Visibility != ModelVisibility.Visible)
            return 0;
        int count = HbrMemoryTitleState.DecodeCount(model, fromParts);
        if (count == 0) return 0;
        _decodeModel = model;
        _decodeStart = Now;
        _decodeFrom = fromParts;
        _decodeLength = HbrMemoryTitleReveal.DecodeDuration(count);
        SetProcess(true);
        RenderMemoryTitle(model);
        return _decodeLength;
    }

    public override void _Process(double delta)
    {
        CardModel model = GodotObject.IsInstanceValid(Card) ? Card.Model : null;
        double now = Now;
        bool decoding = _decodeModel != null && ReferenceEquals(_decodeModel, model) && now - _decodeStart < _decodeLength;
        bool decodeEnded = _decodeModel != null && !decoding;
        if (decodeEnded) _decodeModel = null;
        bool jumping = now < _jumpUntil;
        long jump = jumping ? (long)(now / HbrMemoryTitleReveal.JumpSeconds) : -1;
        if (model != null && (decoding || decodeEnded || jump != _lastJump) && _styled && _theme == HbrCardTheme.Memory &&
            GodotObject.IsInstanceValid(_title) && Card.Visibility == ModelVisibility.Visible &&
            HbrMemoryTitleState.Applies(model))
        {
            RenderMemoryTitle(model);
        }
        _lastJump = jump;
        if (!decoding && !jumping) SetProcess(false);
    }

    private void RenderMemoryTitle(CardModel model)
    {
        double now = Now;
        long jump = now < _jumpUntil ? (long)(now / HbrMemoryTitleReveal.JumpSeconds) : -1;
        bool decoding = ReferenceEquals(_decodeModel, model) && now - _decodeStart < _decodeLength;
        string text = $"「{HbrMemoryTitleState.Title(model, jump, decoding ? _decodeFrom : -1,
            decoding ? now - _decodeStart : double.MaxValue)}」";
        if (_title.Text != text) _title.SetTextAutoSize(text);
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    // Vanilla sizes the type plaque in a deferred call after Reload; HbrCardStylePatch calls this again after it.
    internal void LayoutTypePlaque()
    {
        if (!_styled || !GodotObject.IsInstanceValid(_typePlaque) || !GodotObject.IsInstanceValid(_typeLabel)) return;
        _typePlaque.Texture = null;
        _typePlaque.Material = null;
        Font font = _typeLabel.GetThemeFont("font");
        int fontSize = _typeLabel.GetThemeFontSize("font_size");
        // A pixel of slack keeps the auto-sizing label from shrinking text that fits exactly.
        float labelWidth = font.GetStringSize(_typeLabel.Text, HorizontalAlignment.Left, -1, fontSize).X + 2;
        float width = HbrCardStyleLayout.PlateWidth(labelWidth);
        _typePlaque.OffsetLeft = HbrCardStyleLayout.PlateLeft;
        _typePlaque.OffsetTop = HbrCardStyleLayout.PlateTop;
        _typePlaque.OffsetRight = HbrCardStyleLayout.PlateLeft + width;
        _typePlaque.OffsetBottom = HbrCardStyleLayout.PlateTop + HbrCardStyleLayout.PlateHeight;
        // The label keeps its centre anchors; place it between the icon and the slant.
        _typeLabel.OffsetLeft = HbrCardStyleLayout.PlateLabelLeft - width / 2;
        _typeLabel.OffsetRight = width / 2 - HbrCardStyleLayout.PlateLabelRight;
        if (!GodotObject.IsInstanceValid(_typePlate))
        {
            _typePlate = new NHbrCardTypePlate();
            _typePlaque.AddChild(_typePlate);
            _typePlaque.MoveChild(_typePlate, 0);
            _typePlate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        _typePlate.Display(new Color(HbrCardStyleLayout.KindAccent(_kind)));
    }

    // v0.111.0 moved the frame, title banner and portrait border assignment out of Reload into
    // UpdatePortrait, which UpdateVisuals also calls; without this the vanilla frame came back over a
    // styled card (its taller window and V-shaped lower edge showed as gaps around the art). The card
    // is resolved again so that a pooled card switching to a vanilla model mid-Reload is left alone.
    internal void ReapplyFrame()
    {
        if (!_styled || !GodotObject.IsInstanceValid(Card) || !GodotObject.IsInstanceValid(_frame) ||
            !TryResolve(Card.Model, out HbrCardTheme theme, out HbrCardTier tier, out _))
            return;
        _frame.Texture = HbrCardStyleTextures.Get(HbrCardStyleLayout.FramePath(theme, tier));
        _frame.Material = null;
        _portraitBorder.Visible = false;
        _titleBanner.Visible = false;
    }

    private void Restore()
    {
        _decodeModel = null;
        _jumpUntil = 0;
        if (!_styled) return;
        _styled = false;
        // Reload already put back the frame, banner and border; restore what it does not touch.
        SetOffsets(_portrait, _original.Portrait);
        _portrait.StretchMode = _original.PortraitStretch;
        SetOffsets(_title, _original.Title);
        SetOffsets(_description, _original.Description);
        SetOffsets(_typePlaque, _original.TypePlaque);
        SetOffsets(_typeLabel, _original.TypeLabel);
        _typePlaque.Texture = _original.TypePlaqueTexture;
        _description.AddThemeColorOverride("default_color", _original.DescriptionColor);
        _typeLabel.AddThemeColorOverride("font_color", _original.TypeLabelColor);
        if (GodotObject.IsInstanceValid(_typePlate)) _typePlate.Visible = false;
        ShowPhantomGlow(false);
    }

    private void ShowPhantomGlow(bool visible)
    {
        if (!visible)
        {
            if (GodotObject.IsInstanceValid(_phantomGlow)) _phantomGlow.Visible = false;
            return;
        }
        if (!GodotObject.IsInstanceValid(_phantomGlow))
        {
            _phantomGlow = new TextureRect
            {
                Name = "HbrPhantomGlow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale
            };
            _container.AddChild(_phantomGlow);
            // Behind every card layer except the drop shadow.
            _container.MoveChild(_phantomGlow, Math.Min(1, _container.GetChildCount() - 1));
            SetOffsets(_phantomGlow, HbrCardStyleLayout.PhantomGlow);
        }
        _phantomGlow.Texture = HbrCardStyleTextures.Get(HbrCardStyleLayout.PhantomGlowPath);
        _phantomGlow.Visible = true;
    }

    private bool FindNodes()
    {
        if (GodotObject.IsInstanceValid(_frame)) return true;
        _container = Card.GetNodeOrNull<Control>("%CardContainer");
        _frame = Card.GetNodeOrNull<TextureRect>("%Frame");
        _portrait = Card.GetNodeOrNull<TextureRect>("%Portrait");
        _portraitBorder = Card.GetNodeOrNull<TextureRect>("%PortraitBorder");
        _titleBanner = Card.GetNodeOrNull<TextureRect>("%TitleBanner");
        _title = Card.GetNodeOrNull<MegaLabel>("%TitleLabel");
        _description = Card.GetNodeOrNull<RichTextLabel>("%DescriptionLabel");
        _typePlaque = Card.GetNodeOrNull<NinePatchRect>("%TypePlaque");
        _typeLabel = Card.GetNodeOrNull<Label>("%TypeLabel");
        bool found = _container != null && _frame != null && _portrait != null && _portraitBorder != null &&
            _titleBanner != null && _title != null && _description != null && _typePlaque != null && _typeLabel != null;
        if (!found) _frame = null;
        return found;
    }

    internal static bool TryResolve(CardModel card, out HbrCardTheme theme, out HbrCardTier tier, out HbrCardKind kind)
    {
        theme = default;
        tier = default;
        kind = default;
        // Ancient cards keep the vanilla full-art frame.
        if (card == null || card.Rarity == CardRarity.Ancient || !HBRGOSakiContentPack.IsRegisteredCardType(card.GetType()))
            return false;
        theme = card is PhantomChoiceHbrgoSaki or PhantomFusionEffectChoiceHbrgoSaki or PhantomKnowledgeChoiceHbrgoSaki
            ? HbrCardTheme.Phantom
            : ThemeOf(card);
        tier = card.Rarity switch
        {
            CardRarity.Uncommon => HbrCardTier.Uncommon,
            CardRarity.Rare => HbrCardTier.Rare,
            _ => HbrCardTier.Common
        };
        kind = card.Type switch
        {
            CardType.Attack => HbrCardKind.Attack,
            CardType.Power => HbrCardKind.Power,
            _ => HbrCardKind.Skill
        };
        return true;
    }

    // Memory and Hotaru cards keep their own frames. Saki's cards follow the form that holds them, as
    // HbrgoSakiCardEnergyIconPatch does for the energy icon, so shared cards in a 30A deck wear the 30A frame.
    private static HbrCardTheme ThemeOf(CardModel card)
    {
        CardPoolModel pool;
        try
        {
            pool = card.VisualCardPool;
        }
        catch (InvalidProgramException)
        {
            pool = null; // generated cards that no pool lists
        }
        var owner = card.IsMutable ? card.Owner?.Character : null;
        if (pool is HBRGOSakiMemoryGardenCardPool) return HbrCardTheme.Memory;
        if (pool is HbrgoHotaruCardPool || (pool == null && owner is HbrgoHotaru)) return HbrCardTheme.Hotaru;
        if (owner is HbrgoSaki saki && pool is null or HBRGOSakiCardPool or HBRGOSaki31OCardPool or HBRGOSaki30ACardPool)
            return saki is HbrgoSaki30A ? HbrCardTheme.Saki30A : HbrCardTheme.Saki31O;
        return pool is HBRGOSaki30ACardPool ? HbrCardTheme.Saki30A : HbrCardTheme.Saki31O;
    }

    // card.tscn's portrait stretch (keep aspect, centred), which vanilla cards get back. Taken from the
    // scene rather than the live node, which NHbrgoStrikePortrait may already have switched to covered.
    private const TextureRect.StretchModeEnum VanillaPortraitStretch = TextureRect.StretchModeEnum.KeepAspectCentered;

    // Whether a card model is drawn in the HBR style; NHbrgoStrikePortrait asks before choosing its stretch.
    internal static bool IsStyled(CardModel card) => TryResolve(card, out _, out _, out _);

    private static HbrCardRect OffsetsOf(Control control) =>
        new(control.OffsetLeft, control.OffsetTop, control.OffsetRight, control.OffsetBottom);

    private static void SetOffsets(Control control, HbrCardRect rect)
    {
        control.OffsetLeft = rect.Left;
        control.OffsetTop = rect.Top;
        control.OffsetRight = rect.Right;
        control.OffsetBottom = rect.Bottom;
    }
}

internal static class HbrCardStyleTextures
{
    private static readonly Dictionary<string, Texture2D> Cache = new();

    internal static Texture2D Get(string path)
    {
        // Room cache cleanup can dispose a resource while pooled UI still holds its managed wrapper.
        if (!Cache.TryGetValue(path, out Texture2D texture) || !GodotObject.IsInstanceValid(texture))
            Cache[path] = texture = ResourceLoader.Load<Texture2D>(path);
        return texture;
    }
}
