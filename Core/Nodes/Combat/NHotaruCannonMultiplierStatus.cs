using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// A visual status slot only. It never applies a power to the invulnerable cannon.
public partial class NHotaruCannonMultiplierStatus : Control
{
    private const string WidgetName = "HBRGOHotaruCannonMultiplierStatus";
    private const string IconPath = "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    private static readonly AccessTools.FieldRef<NPowerContainer, Vector2?> OriginalPosition =
        AccessTools.FieldRefAccess<NPowerContainer, Vector2?>("_originalPosition");
    private static readonly AccessTools.FieldRef<NPowerContainer, List<NPower>> PowerNodes =
        AccessTools.FieldRefAccess<NPowerContainer, List<NPower>>("_powerNodes");
    private readonly List<NPower> _visiblePowers = new();
    private NCreature _cannon;
    private NPowerContainer _container;
    private TextureRect _icon;
    private MegaLabel _amount;
    private MegaLabel _level;
    private Vector2 _fallbackBaseline;
    private Millsage _relic;
    private HotaruCannonKind _kind;
    private int _lastPercent = -1;
    private int _lastMaximum = -1;
    private int _lastLevel = -1;
    private bool _hovered;

    internal static NHotaruCannonMultiplierStatus Refresh(NCreature cannon, NHotaruCannonMultiplierStatus status)
    {
        if (!Usable(cannon) || cannon.Entity?.Monster is not HotaruCannonModel model) return null;
        if (!Usable(status) || !Usable(status._container))
        {
            NPowerContainer container = cannon.GetNodeOrNull<NPowerContainer>("HealthBar/PowerContainer");
            if (!Usable(container)) return null;
            status = container.GetNodeOrNull<NHotaruCannonMultiplierStatus>(WidgetName);
            if (!Usable(status))
            {
                status = Create(cannon, container);
                container.AddChild(status);
            }
        }
        status._cannon = cannon;
        status._kind = model.Kind;
        status.RefreshState();
        status.LayoutFirstSlot();
        return status;
    }

    private static NHotaruCannonMultiplierStatus Create(NCreature cannon, NPowerContainer container)
    {
        // Copy only native visual children. An NPower root would subscribe to a
        // real PowerModel and give the decorative status gameplay side effects.
        Control template = ResourceLoader.Load<PackedScene>(SceneHelper.GetScenePath("combat/power"))
            .Instantiate<Control>();
        var status = new NHotaruCannonMultiplierStatus
        {
            Name = WidgetName,
            _cannon = cannon,
            _container = container,
            _fallbackBaseline = OriginalPosition(container) ?? container.Position,
            Size = template.Size,
            CustomMinimumSize = template.CustomMinimumSize,
            MouseFilter = MouseFilterEnum.Pass,
            FocusMode = FocusModeEnum.None
        };
        try
        {
            status._icon = (TextureRect)template.GetNode<TextureRect>("Icon").Duplicate();
            status._icon.UniqueNameInOwner = false;
            status._icon.MouseFilter = MouseFilterEnum.Ignore;
            status._icon.Texture = ResourceLoader.Load<Texture2D>(IconPath);
            status.AddChild(status._icon);
            status._amount = (MegaLabel)template.GetNode<MegaLabel>("AmountLabel")
                .Duplicate((int)Node.DuplicateFlags.Scripts);
            status._amount.UniqueNameInOwner = false;
            status._amount.AutoSizeEnabled = true;
            status._amount.MinFontSize = 12;
            status._amount.MaxFontSize = 18;
            status._amount.MouseFilter = MouseFilterEnum.Ignore;
            status._amount.SetAnchorsPreset(LayoutPreset.TopLeft);
            status._amount.OffsetLeft = 0f;
            status._amount.OffsetRight = 44f;
            status.AddChild(status._amount);
            status._level = (MegaLabel)status._amount.Duplicate((int)Node.DuplicateFlags.Scripts);
            status._level.Name = "UpgradeLevel";
            status._level.MaxFontSize = 16;
            status._level.UniqueNameInOwner = false;
            status._level.SetAnchorsPreset(LayoutPreset.TopLeft);
            status._level.OffsetLeft = -8f;
            status._level.OffsetRight = 56f;
            status._level.OffsetTop = -24f;
            status._level.OffsetBottom = -2f;
            status._level.HorizontalAlignment = HorizontalAlignment.Center;
            status._level.AddThemeFontSizeOverride("font_size", 16);
            status.AddChild(status._level);
        }
        finally { template.Free(); }
        return status;
    }

    public override void _Ready()
    {
        MouseEntered += OnHovered;
        MouseExited += OnUnhovered;
    }

    public override void _ExitTree() => OnUnhovered();

    private void RefreshState()
    {
        _relic = _cannon.Entity.PetOwner?.GetRelic<Millsage>();
        Visible = _relic != null && _cannon.Entity.IsAlive;
        bool selecting = NTargetManager.Instance?.IsInSelection == true;
        MouseFilter = selecting ? MouseFilterEnum.Ignore : MouseFilterEnum.Pass;
        if (!Visible || selecting || NHoverTipSet.shouldBlockHoverTips)
        {
            OnUnhovered();
            if (!Visible) return;
        }
        int percent = _relic.GetCannonDamagePercent(_kind);
        int maximum = _relic.GetMaximumCannonDamagePercent(_kind);
        int level = _relic.GetCannonUpgradeLevel(_kind);
        if (percent == _lastPercent && maximum == _lastMaximum && level == _lastLevel) return;
        _lastPercent = percent;
        _lastMaximum = maximum;
        _lastLevel = level;
        _amount.SetTextAutoSize(percent.ToString(CultureInfo.InvariantCulture) + "%");
        _level.SetTextAutoSize(_kind == HotaruCannonKind.Endgame
            ? _relic.EndgameSign < 0 ? "牺牲" : "救赎"
            : level.ToString(CultureInfo.InvariantCulture) + "级");
        if (_hovered) ShowHoverTip();
    }

    private void LayoutFirstSlot()
    {
        _visiblePowers.Clear();
        foreach (NPower power in PowerNodes(_container))
            if (Usable(power) && power.Visible) _visiblePowers.Add(power);
        float width = _visiblePowers.Count > 0 ? Mathf.Max(1f, _visiblePowers[0].Size.X) : 48f;
        int count = _visiblePowers.Count + 1;
        float columns = Mathf.Max(Mathf.CeilToInt(_container.Size.X / width), Mathf.CeilToInt(count / 2f));
        Position = Vector2.Zero;
        for (int index = 0; index < _visiblePowers.Count; index++)
        {
            int slot = index + 1;
            _visiblePowers[index].Position = new Vector2(width * (slot % columns), Mathf.Floor(slot / columns) * width);
        }
        float rowWidth = width * Mathf.Min(columns, count);
        _container.Position = (OriginalPosition(_container) ?? _fallbackBaseline) +
            Vector2.Left * Mathf.Max(0f, rowWidth - _container.Size.X) / 2f;
    }

    private void OnHovered()
    {
        _hovered = true;
        _icon.Scale = Vector2.One * 1.1f;
        ShowHoverTip();
    }

    private void OnUnhovered()
    {
        if (!_hovered) return;
        _hovered = false;
        if (Usable(_icon)) _icon.Scale = Vector2.One;
        NHoverTipSet.Remove(this);
    }

    private void ShowHoverTip()
    {
        if (!_hovered || _relic == null || !IsVisibleInTree() || !CombatManager.Instance.IsInProgress ||
            NTargetManager.Instance?.IsInSelection == true || NHoverTipSet.shouldBlockHoverTips) return;
        // Remove the entity's broader ability tip when reading this status.
        NHoverTipSet.Remove(_cannon.Hitbox);
        bool endgame = _kind == HotaruCannonKind.Endgame;
        LocString title = new("powers", endgame ? "HOTARU_FINAL_CANNON_SOUL.title" : "HOTARU_CANNON_DAMAGE_MULTIPLIER.title");
        LocString description = new("powers", endgame ? "HOTARU_FINAL_CANNON_SOUL.description" : "HOTARU_CANNON_DAMAGE_MULTIPLIER.description");
        description.Add("Current", _lastPercent);
        description.Add("Maximum", _lastMaximum);
        description.Add("Level", _lastLevel);
        NHoverTipSet.Remove(this);
        NHoverTipSet.CreateAndShow(this, new HoverTip(title, description), HoverTip.GetHoverTipAlignment(this));
    }

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
