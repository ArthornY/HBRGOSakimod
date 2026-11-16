using System;
using Godot;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Nodes.Merchant;

// One fragment on the merchant's fate fragment stand, behaving like a vanilla relic or potion slot: the
// fragment in its category ring over a gold price (cream, red when short), larger and with a hover tip while
// hovered or focused, bought with a left click or the controller's select, a wiggle when the gold is short,
// hidden once sold. Buying goes through the stand, which sends the same command as the manual page.
public partial class NHbrFateFragmentShelfSlot : Control
{
    private const string PriceFont = "res://themes/kreon_bold_glyph_space_two.tres";
    private const string Coin = "res://images/atlases/ui_atlas.sprites/top_bar/top_bar_gold.tres";

    private static readonly Color Cream = new("FFF6E2");
    private static readonly Color Short = new("FF5555");
    private static readonly Color PriceOutline = new(0.067f, 0f, 0f, 0.43f);

    private NHbrFateFragmentShelf _shelf;
    private HbrFateShopOffer _offer;
    private HbrFateFragment _fragment;
    private Control _visual;
    private Label _price;
    private bool _hovered;
    private bool _pressed;
    private Tween _hoverTween;
    private Tween _wiggleTween;

    internal HbrFateShopOffer Offer => _offer;

    internal bool Stocked => _offer != null && !_offer.Sold && _fragment != null;

    internal static NHbrFateFragmentShelfSlot Create(NHbrFateFragmentShelf shelf, HbrFateShopOffer offer, int index, int count)
    {
        var slot = new NHbrFateFragmentShelfSlot
        {
            Name = $"FateFragment{index}",
            _shelf = shelf,
            _offer = offer,
            _fragment = HbrFateDiceCatalog.Find(offer.Fragment)
        };
        var rect = HbrFateFragmentShelfLayout.Slot(index, count);
        slot.Position = new Vector2(rect.X, rect.Y);
        slot.Size = new Vector2(rect.Width, rect.Height);
        slot.PivotOffset = new Vector2(rect.Width / 2f, HbrFateFragmentShelfLayout.Icon / 2f);
        return slot;
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        float icon = HbrFateFragmentShelfLayout.Icon;
        _visual = new Control
        {
            Name = "Visual",
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2((Size.X - icon) / 2f, 0f),
            Size = new Vector2(icon, icon)
        };
        AddChild(_visual);
        NHbrFateSlotIcon ring = NHbrFateSlotIcon.ForFragment(_fragment, icon);
        _visual.AddChild(ring);
        ring.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var cost = new HBoxContainer
        {
            Name = "Cost",
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
            Position = new Vector2(0f, icon + HbrFateFragmentShelfLayout.PriceGap),
            Size = new Vector2(Size.X, HbrFateFragmentShelfLayout.PriceHeight)
        };
        cost.AddThemeConstantOverride("separation", 4);
        AddChild(cost);
        float coin = HbrFateFragmentShelfLayout.CoinSize;
        cost.AddChild(new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = HbrEquipmentArt.Load(Coin),
            CustomMinimumSize = new Vector2(coin, coin),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _price = new Label { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        if (ResourceLoader.Exists(PriceFont)) _price.AddThemeFontOverride("font", ResourceLoader.Load<Font>(PriceFont));
        _price.AddThemeFontSizeOverride("font_size", HbrFateFragmentShelfLayout.PriceFontSize);
        _price.AddThemeConstantOverride("outline_size", HbrFateFragmentShelfLayout.PriceOutline);
        _price.AddThemeColorOverride("font_outline_color", PriceOutline);
        cost.AddChild(_price);

        MouseEntered += OnHover;
        MouseExited += OnUnhover;
        FocusEntered += OnHover;
        FocusExited += OnUnhover;
        UpdateVisual(_shelf?.Gold ?? 0);
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnHover;
        MouseExited -= OnUnhover;
        FocusEntered -= OnHover;
        FocusExited -= OnUnhover;
        _hoverTween?.Kill();
        _wiggleTween?.Kill();
        NHoverTipSet.Remove(this);
    }

    internal void SetOffer(HbrFateShopOffer offer, int gold)
    {
        _offer = offer;
        UpdateVisual(gold);
    }

    internal void UpdateVisual(int gold)
    {
        if (_price == null) return;
        if (!Stocked)
        {
            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            NHoverTipSet.Remove(this);
            return;
        }
        _price.Text = _offer.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _price.AddThemeColorOverride("font_color", gold >= _offer.Price ? Cream : Short);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed) _pressed = true;
            else if (_pressed && _hovered)
            {
                _pressed = false;
                _shelf?.TryBuy(this);
            }
            AcceptEvent();
        }
        else if (input.IsActionPressed(MegaInput.select))
        {
            _shelf?.TryBuy(this);
            AcceptEvent();
        }
    }

    private void OnHover()
    {
        if (!Stocked) return;
        _hovered = true;
        _hoverTween?.Kill();
        Scale = Vector2.One * HbrFateFragmentShelfLayout.HoverScale;
        ShowTip();
        _shelf?.PointHand(this);
    }

    private void OnUnhover()
    {
        _hovered = false;
        _pressed = false;
        _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.TweenProperty(this, "scale", Vector2.One, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        NHoverTipSet.Remove(this);
        _shelf?.StopHand();
    }

    // The vanilla slots' wiggle when a purchase fails.
    internal void Wiggle()
    {
        if (_visual == null) return;
        float rest = (Size.X - HbrFateFragmentShelfLayout.Icon) / 2f;
        _wiggleTween?.Kill();
        _wiggleTween = CreateTween();
        _wiggleTween.TweenMethod(Callable.From<float>(progress =>
            {
                if (GodotObject.IsInstanceValid(_visual))
                    _visual.Position = new Vector2(rest + Mathf.Sin(progress * Mathf.Pi * 2f) * 10f, 0f);
            }), 0f, 2f, 0.4)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
    }

    private void ShowTip()
    {
        if (_fragment == null || NHoverTipSet.shouldBlockHoverTips) return;
        string text = $"命运碎片·{HbrFateDiceCatalog.CategoryName(_fragment.Category)}·" +
                      $"{HbrFateDiceCatalog.RarityName(_fragment.Rarity)}\n" +
                      $"{HbrFateDiceCatalog.Describe(_fragment, _shelf?.HbrNumerics == true)}\n" +
                      "买下后有空槽就直接装上，没有就放进背包；在手册「命运骰子」页装卸。";
        try
        {
            NHoverTipSet.Remove(this);
            NHoverTipSet.CreateAndShow(this,
                new HoverTip(HbrCompactText.Literal("title", _fragment.Name), HbrCompactText.Literal("text", text)),
                HoverTip.GetHoverTipAlignment(this));
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Fate fragment hover tip failed: {error.Message}");
        }
    }
}
