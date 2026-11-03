using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// One roster entry drawn as an HBR divergence medallion: circular portrait, level badge,
// fusion level, name plate, experience bar and an optional selection check.
public partial class NPhantomMedallion : Control
{
    private const float Margin = 20f;
    private const float Top = 12f;
    private const float IdleFlameAlpha = 0.65f;
    private const float FlameCycle = 2.4f;

    private TextureRect _halo;
    private TextureRect _soulFlame;
    private float _flameTime;
    private TextureRect _frameActive;
    private TextureRect _cursor;
    private TextureRect _mark;
    private bool _active;
    private bool _hovered;
    private bool _selected;

    public event Action<int> Pressed;

    public int EntryId { get; private set; }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            RefreshState();
        }
    }

    internal static float WidthFor(float diameter) => diameter + Margin * 2f;

    internal static float HeightFor(float diameter) => diameter + 112f;

    internal static NPhantomMedallion Create(
        PhantomChoiceHbrgoSaki card,
        PhantomRosterEntry entry,
        float diameter,
        bool active,
        bool checkable,
        string footer,
        string tooltip)
    {
        NPhantomMedallion medallion = new()
        {
            EntryId = entry.InstanceId,
            _active = active,
            CustomMinimumSize = new Vector2(WidthFor(diameter), HeightFor(diameter)),
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = tooltip
        };
        medallion.Build(card, entry, diameter, checkable, footer);
        return medallion;
    }

    // Portrait inside the circular mask, shared with the larger detail and preview circles.
    internal static Control CreatePortraitCircle(Texture2D portrait, float diameter)
    {
        TextureRect mask = PhantomRosterArt.Image(PhantomRosterArt.CircleMask, Vector2.Zero, Vector2.One * diameter);
        mask.ClipChildren = ClipChildrenMode.Only;
        ColorRect backdrop = new()
        {
            Color = new Color(0.16f, 0.1f, 0.28f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        mask.AddChild(backdrop);
        TextureRect art = new()
        {
            Texture = portrait,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        art.SetAnchorsPreset(LayoutPreset.FullRect);
        mask.AddChild(art);
        // Every phantom burns with an azure soul-flame; the summoned one breathes (see _Process).
        TextureRect soulFlame = new()
        {
            Name = "SoulFlame",
            Texture = PhantomRosterArt.SoulFlame,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new Color(1f, 1f, 1f, IdleFlameAlpha),
            MouseFilter = MouseFilterEnum.Ignore
        };
        soulFlame.SetAnchorsPreset(LayoutPreset.FullRect);
        mask.AddChild(soulFlame);
        return mask;
    }

    private void Build(PhantomChoiceHbrgoSaki card, PhantomRosterEntry entry, float diameter, bool checkable, string footer)
    {
        float width = WidthFor(diameter);
        Vector2 center = new(width / 2f, Top + diameter / 2f);
        Vector2 Centered(float size) => center - Vector2.One * size / 2f;

        if (_active)
        {
            float haloSize = diameter * 1.3f;
            _halo = PhantomRosterArt.Image(PhantomRosterArt.Halo, Centered(haloSize), Vector2.One * haloSize);
            _halo.PivotOffset = Vector2.One * haloSize / 2f;
            _halo.Modulate = new Color(1f, 1f, 1f, 0.8f);
            AddChild(_halo);
        }

        Control portrait = CreatePortraitCircle(PhantomRosterArt.GetPortrait(entry.MonsterId), diameter);
        portrait.Position = Centered(diameter);
        AddChild(portrait);
        if (_active)
        {
            _soulFlame = portrait.GetNode<TextureRect>("SoulFlame");
        }
        AddChild(PhantomRosterArt.Image(PhantomRosterArt.Frame, Centered(diameter * 1.116f), Vector2.One * diameter * 1.116f));
        _frameActive = PhantomRosterArt.Image(PhantomRosterArt.FrameActive, Centered(diameter * 1.263f), Vector2.One * diameter * 1.263f);
        AddChild(_frameActive);
        if (checkable)
        {
            _cursor = PhantomRosterArt.Image(PhantomRosterArt.SelectCursor, Centered(diameter * 1.23f), Vector2.One * diameter * 1.23f);
            AddChild(_cursor);
        }

        float badge = diameter * 0.37f;
        Vector2 badgeCenter = center + Vector2.One * diameter * 0.36f;
        AddChild(PhantomRosterArt.Image(
            _active ? PhantomRosterArt.LevelFrameActive : PhantomRosterArt.LevelFrame,
            badgeCenter - Vector2.One * badge / 2f,
            Vector2.One * badge));
        Label lvCaption = PhantomRosterArt.MakeLabel("Lv", Mathf.RoundToInt(diameter * 0.07f), PhantomRosterArt.CrimsonLight, 3);
        lvCaption.HorizontalAlignment = HorizontalAlignment.Center;
        lvCaption.Position = badgeCenter + new Vector2(-badge / 2f, -badge * 0.42f);
        lvCaption.Size = new Vector2(badge, badge * 0.3f);
        AddChild(lvCaption);
        Label level = PhantomRosterArt.MakeLabel($"{card.PhantomLevel:0.##}", Mathf.RoundToInt(diameter * 0.135f), Colors.White);
        level.HorizontalAlignment = HorizontalAlignment.Center;
        level.Position = badgeCenter + new Vector2(-badge / 2f, -badge * 0.2f);
        level.Size = new Vector2(badge, badge * 0.6f);
        AddChild(level);

        if (card.PhantomUpgradeLevel > 0)
        {
            float icon = diameter * 0.23f;
            Vector2 upgradeCenter = center + new Vector2(-diameter * 0.38f, diameter * 0.36f);
            AddChild(PhantomRosterArt.Image(PhantomRosterArt.UpgradeBadge, upgradeCenter - Vector2.One * icon / 2f, Vector2.One * icon));
            Label upgrade = PhantomRosterArt.MakeLabel($"+{card.PhantomUpgradeLevel}", Mathf.RoundToInt(diameter * 0.125f), PhantomRosterArt.Gold);
            upgrade.Position = upgradeCenter + new Vector2(icon * 0.4f, -icon / 2f);
            upgrade.Size = new Vector2(icon * 1.4f, icon);
            AddChild(upgrade);
        }

        float plateTop = Top + diameter + 12f;
        Panel plate = new()
        {
            Position = new Vector2(10f, plateTop),
            Size = new Vector2(width - 20f, 40f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        plate.AddThemeStyleboxOverride("panel", PhantomRosterArt.Panel(
            new Color(0.04f, 0.024f, 0.086f, 0.86f), new Color(1f, 0.65f, 0.84f, 0.38f), 20));
        AddChild(plate);
        Label name = PhantomRosterArt.MakeLabel(card.Title.Split(" Lv.")[0], 21, Colors.White);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.ClipText = true;
        name.Position = new Vector2(16f, plateTop);
        name.Size = new Vector2(width - 32f, 40f);
        AddChild(name);

        ProgressBar experience = new()
        {
            MinValue = 0d,
            MaxValue = Math.Max(1, entry.ExperienceRequiredForNextLevel),
            Value = entry.ExperienceIntoLevel,
            ShowPercentage = false,
            Position = new Vector2(26f, plateTop + 50f),
            Size = new Vector2(width - 52f, 10f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        experience.AddThemeStyleboxOverride("background", PhantomRosterArt.Panel(
            new Color(1f, 1f, 1f, 0.14f), new Color(1f, 0.65f, 0.84f, 0.35f), 5));
        experience.AddThemeStyleboxOverride("fill", PhantomRosterArt.BarFill(5));
        AddChild(experience);

        Label footerLabel = PhantomRosterArt.MakeLabel(footer, 17, PhantomRosterArt.Muted, 4);
        footerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        footerLabel.Position = new Vector2(0f, plateTop + 64f);
        footerLabel.Size = new Vector2(width, 26f);
        AddChild(footerLabel);

        if (_active)
        {
            Label tag = PhantomRosterArt.MakeLabel("召唤中", 18, Colors.White, 3);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            tag.AddThemeStyleboxOverride("normal", PhantomRosterArt.SummonTag(16));
            tag.Position = new Vector2(-4f, -4f);
            AddChild(tag);
        }

        if (checkable)
        {
            AddChild(PhantomRosterArt.Image(PhantomRosterArt.CheckBox, new Vector2(width - 56f, 0f), new Vector2(52f, 46f)));
            _mark = PhantomRosterArt.Image(PhantomRosterArt.CheckMark, new Vector2(width - 50f, 4f), new Vector2(42f, 33f));
            AddChild(_mark);
        }

        MouseEntered += () => { _hovered = true; RefreshState(); };
        MouseExited += () => { _hovered = false; RefreshState(); };
        FocusEntered += RefreshState;
        FocusExited += RefreshState;
        RefreshState();
    }

    public override void _Process(double delta)
    {
        if (_halo != null)
        {
            _halo.Rotation += (float)delta * 0.2f;
        }

        if (_soulFlame != null)
        {
            _flameTime = (_flameTime + (float)delta) % FlameCycle;
            float breath = 0.5f - 0.5f * Mathf.Cos(_flameTime / FlameCycle * Mathf.Tau);
            _soulFlame.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(IdleFlameAlpha, 1f, breath));
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        bool released = @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false };
        if (released || @event.IsActionPressed("ui_accept"))
        {
            AcceptEvent();
            Pressed?.Invoke(EntryId);
        }
    }

    private void RefreshState()
    {
        if (_frameActive == null)
        {
            return;
        }

        _frameActive.Visible = _active || _selected || _hovered || HasFocus();
        if (_cursor != null)
        {
            _cursor.Visible = _selected;
        }

        if (_mark != null)
        {
            _mark.Visible = _selected;
        }
    }
}
