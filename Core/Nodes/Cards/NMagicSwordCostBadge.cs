using System;
using System.Globalization;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// A third, independent resource badge. The model's native energy/star values
// never change. Only live hand cards subscribe, and no frame polling is used.
public partial class NMagicSwordCostBadge : Node
{
    internal const string NodeName = "HbrMagicSwordCost";
    private static readonly MethodInfo UpdateEnergy = AccessTools.Method(typeof(NCard), "UpdateEnergyCostVisuals");
    private static readonly MethodInfo UpdateStars = AccessTools.Method(typeof(NCard), "UpdateStarCostVisuals");
    private static Texture2D _texture;

    private NCard _card;
    private PileType _displayedPile;
    private CardModel _model;
    private PlayerCombatState _resources;
    private Creature _creature;
    private TextureRect _badge;
    private TextureRect _starIcon;
    private MegaLabel _label;
    private Label _chaseTag;

    internal static void Update(NCard card, PileType pile)
    {
        NMagicSwordCostBadge node = card.GetNodeOrNull<NMagicSwordCostBadge>(NodeName);
        if (node == null)
        {
            if (pile != PileType.Hand || card.Model?.IsMutable != true ||
                card.Model.Owner?.Character is not HbrgoSaki31O || !LocalContext.IsMe(card.Model.Owner)) return;
            node = new NMagicSwordCostBadge { Name = NodeName, _card = card };
            card.AddChild(node);
        }
        node._displayedPile = pile;
        node.Bind();
        node.Apply();
    }

    internal static void UpdateLayout(NCard card) => card.GetNodeOrNull<NMagicSwordCostBadge>(NodeName)?.Layout();
    internal static void Reset(NCard card) => card.GetNodeOrNull<NMagicSwordCostBadge>(NodeName)?.ResetBinding();

    public override void _EnterTree()
    {
        _card ??= GetParent() as NCard;
        if (_card != null) _card.ModelChanged += OnModelChanged;
    }

    public override void _ExitTree()
    {
        if (_card != null) _card.ModelChanged -= OnModelChanged;
        ResetBinding();
    }

    private bool CanObserve(CardModel model) => _displayedPile == PileType.Hand &&
        _card.Visibility == ModelVisibility.Visible && model?.IsMutable == true &&
        model.Pile?.Type == PileType.Hand && model.Owner?.Character is HbrgoSaki31O &&
        model.Owner.PlayerCombatState != null && LocalContext.IsMe(model.Owner);

    private void Bind()
    {
        CardModel model = _card.Model;
        if (!CanObserve(model)) { Unbind(); return; }
        if (ReferenceEquals(_model, model) && ReferenceEquals(_resources, model.Owner.PlayerCombatState)) return;
        Unbind();
        _model = model;
        _resources = model.Owner.PlayerCombatState;
        _creature = model.Owner.Creature;
        _model.EnergyCostChanged += RefreshNative;
        _model.StarCostChanged += RefreshNative;
        _resources.EnergyChanged += OnResourceChanged;
        _resources.StarsChanged += OnResourceChanged;
        _creature.PowerApplied += OnPowerChanged;
        _creature.PowerRemoved += OnPowerChanged;
        _creature.PowerIncreased += OnPowerIncreased;
        _creature.PowerDecreased += OnPowerDecreased;
    }

    private void Unbind()
    {
        if (_model != null)
        {
            _model.EnergyCostChanged -= RefreshNative;
            _model.StarCostChanged -= RefreshNative;
        }
        if (_resources != null)
        {
            _resources.EnergyChanged -= OnResourceChanged;
            _resources.StarsChanged -= OnResourceChanged;
        }
        if (_creature != null)
        {
            _creature.PowerApplied -= OnPowerChanged;
            _creature.PowerRemoved -= OnPowerChanged;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
        }
        _model = null; _resources = null; _creature = null;
    }

    private void ResetBinding()
    {
        Unbind(); _displayedPile = PileType.None;
        if (GodotObject.IsInstanceValid(_badge)) _badge.Visible = false;
        if (GodotObject.IsInstanceValid(_chaseTag)) _chaseTag.Visible = false;
    }

    private void OnModelChanged(CardModel _) { Unbind(); RefreshNative(); }
    private void OnResourceChanged(int _, int __) => RefreshNative();
    private void OnPowerChanged(PowerModel power)
    {
        if (MagicSwordCostDisplayHelper.AffectsCost(power)) RefreshNative();
    }
    private void OnPowerIncreased(PowerModel power, int _, bool __) => OnPowerChanged(power);
    private void OnPowerDecreased(PowerModel power, bool _) => OnPowerChanged(power);

    private void RefreshNative()
    {
        if (!GodotObject.IsInstanceValid(_card) || !_card.IsNodeReady() || !_card.IsInsideTree() || _card.Model == null) return;
        UpdateEnergy.Invoke(_card, new object[] { _displayedPile });
        UpdateStars.Invoke(_card, new object[] { _displayedPile });
    }

    private void Apply()
    {
        ApplyChaseTag();
        if (!MagicSwordCostDisplayHelper.TryGetQuote(_card.Model, _displayedPile,
                _card.Visibility == ModelVisibility.Visible, out SwordheartPaymentQuote quote))
        {
            if (GodotObject.IsInstanceValid(_badge)) _badge.Visible = false;
            return;
        }
        if (!EnsureBadge()) return;
        // Native refresh has already applied the cost's normal colour/style.
        // Its number alone becomes the energy this payment will actually use.
        _card.GetNode<MegaLabel>("%EnergyLabel").SetTextAutoSize(quote.EnergyToSpend.ToString(CultureInfo.InvariantCulture));
        _badge.Visible = true;
        _badge.Modulate = quote.HasEnoughSwordheart ? Colors.White : new Color("FF8888");
        _label.AddThemeColorOverride("font_color", quote.HasEnoughSwordheart ? StsColors.cream : StsColors.red);
        _label.AddThemeColorOverride("font_outline_color", quote.HasEnoughSwordheart ? StsColors.defaultStarCostOutline : StsColors.unplayableEnergyCostOutline);
        _label.SetTextAutoSize(quote.SwordheartToSpend.ToString("0", CultureInfo.InvariantCulture));
        Layout();
    }

    private void ApplyChaseTag()
    {
        bool visible = CanObserve(_card.Model) &&
            DualSwordsChaseHelper.IsChaseAttack(_card.Model.Owner.Creature, _card.Model);
        if (!visible)
        {
            if (GodotObject.IsInstanceValid(_chaseTag)) _chaseTag.Visible = false;
            return;
        }
        if (!GodotObject.IsInstanceValid(_chaseTag))
        {
            Label native = _card.GetNodeOrNull<Label>("%TypeLabel");
            Control plaque = _card.GetNodeOrNull<Control>("%TypePlaque");
            if (native == null || plaque == null) return;
            _chaseTag = (Label)native.Duplicate();
            _chaseTag.Name = "HbrChaseTag";
            _chaseTag.UniqueNameInOwner = false;
            _chaseTag.MouseFilter = Control.MouseFilterEnum.Ignore;
            plaque.GetParent().AddChild(_chaseTag);
            _chaseTag.SetAnchorsPreset(Control.LayoutPreset.Center);
            _chaseTag.OffsetLeft = 62f;
            _chaseTag.OffsetRight = 136f;
            _chaseTag.OffsetTop = 28f;
            _chaseTag.OffsetBottom = 58f;
            _chaseTag.HorizontalAlignment = HorizontalAlignment.Right;
            _chaseTag.AddThemeFontSizeOverride("font_size", 22);
            _chaseTag.AddThemeColorOverride("font_color", new Color("F8D36A"));
        }
        _chaseTag.Text = new LocString("powers", "REPTURE_DUAL_SWORDS_POWER.chaseTitle").GetFormattedText();
        _chaseTag.Visible = true;
    }

    private bool EnsureBadge()
    {
        if (GodotObject.IsInstanceValid(_badge)) return true;
        _starIcon = _card.GetNodeOrNull<TextureRect>("%StarIcon");
        if (_starIcon == null) return false;
        // Resource unloading can dispose the native texture while this static
        // managed reference remains non-null. Reload only when a badge needs it.
        if (!GodotObject.IsInstanceValid(_texture))
            _texture = ResourceLoader.Load<Texture2D>("res://images/powers/swordheart_power.png");
        if (!GodotObject.IsInstanceValid(_texture)) return false;
        _badge = (TextureRect)_starIcon.Duplicate();
        _badge.Name = "HbrSwordheartCostIcon";
        _badge.UniqueNameInOwner = false;
        foreach (Node child in _badge.GetChildren()) child.UniqueNameInOwner = false;
        _label = _badge.GetNode<MegaLabel>("StarLabel");
        _label.Name = "SwordheartLabel";
        TextureRect blocked = _badge.GetNodeOrNull<TextureRect>("UnplayableStarIcon");
        if (blocked != null) { blocked.Visible = false; blocked.QueueFree(); }
        _badge.Texture = _texture;
        _badge.MouseFilter = Control.MouseFilterEnum.Ignore;
        _badge.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _starIcon.GetParent().AddChild(_badge);
        return true;
    }

    private void Layout()
    {
        if (!GodotObject.IsInstanceValid(_badge) || !_badge.Visible || !GodotObject.IsInstanceValid(_starIcon)) return;
        // Reuse native secondary-cost placement. A real star cost keeps its
        // own slot; Swordheart then occupies the next row rather than hiding it.
        _badge.Position = _starIcon.Position + (_starIcon.Visible ? new Vector2(0f, _starIcon.Size.Y * .78f) : Vector2.Zero);
        _badge.Size = _starIcon.Size;
    }
}
