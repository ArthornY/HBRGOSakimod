using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NSixSenseResistanceRow : Control
{
    private readonly Dictionary<SixSenseAttribute, NSixSenseBadge> _badges = new();
    private readonly Dictionary<SixSenseAttribute, MegaLabel> _labels = new();
    private readonly Dictionary<SixSenseAttribute, int> _values = new();
    private Creature _creature;
    private Control _hpBar;
    private NHbrDestructionRateCaption _destructionRate;
    private SixSenseResistancePower _power;
    private NararaNullificationPower _nullification;
    private bool _subscribed;
    private double _elapsed;
    private SixSenseAttribute? _hoveredSense;
    private decimal _shownConversion = -1m;

    internal bool IsHoveringResistance => _hoveredSense.HasValue &&
        SixSenseBadgeHelper.CanShowResistanceHover(_creature, _hoveredSense.Value);

    public void SetCreature(Creature creature, Control hpBar)
    {
        HideBadgeHover();
        Unsubscribe();
        _creature = creature;
        _hpBar = hpBar;
        _destructionRate = null;
        Bind();
        Refresh();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Visible = false;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (Visible) SyncLayout();
        _elapsed += delta;
        if (_elapsed < .1) return;
        _elapsed = 0;
        Bind();
        Refresh();
    }

    public override void _ExitTree()
    {
        HideBadgeHover();
        Unsubscribe();
    }

    public void Refresh()
    {
        Bind();
        SixSenseAttribute[] active = SixSenseBadgeHelper.VisibleResistances(_creature);
        Visible = active.Length > 0;
        if (!Visible) { HideBadgeHover(); return; }
        foreach (SixSenseAttribute sense in SixSenseBadgeHelper.DisplayOrder)
        {
            int value = _power?.GetResistance(sense) ?? 0;
            bool visible = Array.IndexOf(active, sense) >= 0;
            if (!_badges.TryGetValue(sense, out NSixSenseBadge badge))
            {
                if (!visible) continue;
                badge = CreateBadge(sense);
            }
            badge.Visible = visible;
            // The locked pair can change while all six resistance values stay
            // identical, so update appearance before the numeric cache shortcut.
            badge.IsLocked = _nullification?.IsResistanceLocked(sense) == true;
            if (_values.TryGetValue(sense, out int previous) && previous == value) continue;
            _values[sense] = value;
            _labels[sense].SetTextAutoSize(value.ToString());
        }
        SyncLayout();
        RefreshBadgeHover();
    }

    private NSixSenseBadge CreateBadge(SixSenseAttribute sense)
    {
        var badge = new NSixSenseBadge { Name = sense.ToString(), Attribute = sense,
            MouseFilter = MouseFilterEnum.Pass };
        badge.MouseEntered += () => OnBadgeMouseEntered(sense);
        badge.MouseExited += () => OnBadgeMouseExited(sense);
        var label = new MegaLabel
        {
            Name = "ResistanceValue", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true, MinFontSize = 8, MaxFontSize = 22
        };
        // MegaLabel asserts a non-null, explicit font override in _Ready. A missing
        // resource must not leave the label relying only on an inherited theme.
        label.AddThemeFontOverride(ThemeConstants.Label.Font, ResolveValueFont());
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 22);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, Colors.White);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, new Color("19151C"));
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 4);
        badge.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _badges.Add(sense, badge);
        _labels.Add(sense, label);
        AddChild(badge);
        return badge;
    }

    private Font ResolveValueFont()
    {
        const string path = "res://themes/kreon_bold_glyph_space_one.tres";
        Font font = ResourceLoader.Exists(path) ? ResourceLoader.Load<Font>(path) : null;
        if (font != null) return font;

        if (GodotObject.IsInstanceValid(_hpBar))
            font = _hpBar.GetThemeFont(ThemeConstants.Label.Font, "Label");
        return font ?? GetThemeFont(ThemeConstants.Label.Font, "Label") ??
            ThemeDB.FallbackFont ?? new SystemFont();
    }

    private void OnBadgeMouseEntered(SixSenseAttribute sense)
    {
        HideBadgeHover();
        if (!SixSenseBadgeHelper.CanShowResistanceHover(_creature, sense)) return;
        _hoveredSense = sense;
        RefreshBadgeHover();
    }

    private void OnBadgeMouseExited(SixSenseAttribute sense)
    {
        if (_hoveredSense == sense) HideBadgeHover();
    }

    private void RefreshBadgeHover()
    {
        if (!_hoveredSense.HasValue) return;
        if (!IsHoveringResistance || !IsVisibleInTree() ||
            NCombatRoom.Instance?.Ui?.Hand?.InCardPlay == true)
        {
            HideBadgeHover();
            return;
        }
        if (_shownConversion == _power.DamageConversion) return;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(_creature);
        if (creatureNode != null) NHoverTipSet.Remove(creatureNode.Hitbox);
        NSixSenseBadge badge = _badges[_hoveredSense.Value];
        NHoverTipSet.Remove(badge);
        var title = new LocString("card_keywords", $"HBRGO_SIX_SENSE.{_hoveredSense.Value}.title");
        NHoverTipSet.CreateAndShow(badge, new HoverTip(title, SixSenseBadgeHelper.ResistanceDescription(_power)),
            HoverTip.GetHoverTipAlignment(badge));
        _shownConversion = _power.DamageConversion;
    }

    private void HideBadgeHover()
    {
        if (_hoveredSense.HasValue && _badges.TryGetValue(_hoveredSense.Value, out NSixSenseBadge badge))
            NHoverTipSet.Remove(badge);
        _hoveredSense = null;
        _shownConversion = -1m;
    }

    private void SyncLayout()
    {
        if (!GodotObject.IsInstanceValid(_hpBar)) return;
        // Reserve six slots for sizing so breaking a resistance never enlarges the other badges.
        float scale = Math.Min(1f, Math.Max(1f, _hpBar.Size.X) / 230f);
        Vector2 badgeSize = new(34f * scale, 40f * scale);
        float x = 0;
        foreach (SixSenseAttribute sense in SixSenseBadgeHelper.DisplayOrder)
        {
            if (!_badges.TryGetValue(sense, out NSixSenseBadge badge) || !badge.Visible) continue;
            badge.Position = new Vector2(x, 0);
            badge.Size = badgeSize;
            _labels[sense].MaxFontSize = Math.Max(8, (int)(22 * scale));
            x += badgeSize.X + 4f * scale;
        }
        Size = new Vector2(_hpBar.Size.X, badgeSize.Y);
        float top = NDeflectorProtectionDpBar.GetHudTop(_creature);
        if (NHbrDestructionRateCaption.ShouldDisplay(_creature))
        {
            top = NHbrDestructionRateCaption.GetTopOffset(_creature);
            if (!GodotObject.IsInstanceValid(_destructionRate))
                _destructionRate = _hpBar.GetNodeOrNull<NHbrDestructionRateCaption>("HbrDestructionRate");
            if (GodotObject.IsInstanceValid(_destructionRate) &&
                _destructionRate.TryGetContentTopOffset(out float contentTop))
                top = contentTop;
        }
        GlobalPosition = _hpBar.GlobalPosition + new Vector2(0,
            (top - badgeSize.Y - 6f) * _hpBar.GetGlobalTransform().Scale.Y);
    }

    private void Bind()
    {
        if (!_subscribed && _creature != null)
        {
            _creature.PowerApplied += OnPowerChanged;
            _creature.PowerRemoved += OnPowerChanged;
            _subscribed = true;
        }
        SixSenseResistancePower next = _creature?.GetPower<SixSenseResistancePower>();
        if (!ReferenceEquals(next, _power))
        {
            if (_power != null) _power.DisplayAmountChanged -= Refresh;
            _power = next;
            if (_power != null) _power.DisplayAmountChanged += Refresh;
        }
        NararaNullificationPower nextNullification = _creature?.GetPower<NararaNullificationPower>();
        if (ReferenceEquals(nextNullification, _nullification)) return;
        if (_nullification != null) _nullification.DisplayAmountChanged -= Refresh;
        _nullification = nextNullification;
        if (_nullification != null) _nullification.DisplayAmountChanged += Refresh;
    }

    private void OnPowerChanged(PowerModel power) { Bind(); Refresh(); }

    private void Unsubscribe()
    {
        if (_subscribed && _creature != null)
        {
            _creature.PowerApplied -= OnPowerChanged;
            _creature.PowerRemoved -= OnPowerChanged;
        }
        if (_power != null) _power.DisplayAmountChanged -= Refresh;
        if (_nullification != null) _nullification.DisplayAmountChanged -= Refresh;
        _power = null;
        _nullification = null;
        _subscribed = false;
    }
}
