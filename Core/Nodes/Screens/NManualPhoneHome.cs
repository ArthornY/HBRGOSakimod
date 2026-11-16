using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The phone's home screen: battle level and 兼具 progress, the garden and six-sense point capsules,
// the portrait window into battle stats, one row of terminal buttons, the six-sense overview radar
// and the battle stat dock. Laid out in canvas coordinates, clipped by the phone screen.
public partial class NManualPhoneHome : Control
{
    private static readonly Color Cyan = HbrManualPhoneArt.Cyan;
    private static readonly Color Muted = HbrManualPhoneArt.Muted;
    private static readonly Color Pink = HbrManualPhoneArt.Pink;

    internal Action<HbrManualHomeButton> OpenButton { get; set; }
    internal Action OpenSenses { get; set; }
    internal Action<int> OpenStat { get; set; }

    private Label _rank;
    private Label _unlock;
    private Control _gaugeFill;
    private Label _garden;
    private Label _points;
    private TextureRect _face;
    private Label _caption;
    private NManualOmega _omega;
    private Control _buttonRow;
    private Label _badge;
    private Control _badgeRoot;
    private NManualSenseRadar _mini;
    private readonly Label[] _miniLabels = new Label[6];
    private readonly List<(Label Value, Label Source, Label Pending)> _dock = new();
    private Label _dockNote;
    private string _buttonsKey;
    private Button _firstButton;

    internal Control DefaultFocusedControl => GodotObject.IsInstanceValid(_firstButton) ? _firstButton : this;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(HbrManualPhoneLayout.CanvasWidth, HbrManualPhoneLayout.CanvasHeight);

        HbrPhoneRect burst = HbrManualPhoneLayout.Burst;
        AddChild(new NManualBurst
        {
            Name = "Burst", Position = new Vector2(burst.X, burst.Y), Size = new Vector2(burst.Width, burst.Height)
        });
        HbrPhoneRect omega = HbrManualPhoneLayout.Omega;
        _omega = new NManualOmega
        {
            Name = "Omega", Position = new Vector2(omega.X, omega.Y), Size = new Vector2(omega.Width, omega.Height),
            Modulate = new Color(1f, 1f, 1f, HbrManualPhoneLayout.OmegaAlpha)
        };
        AddChild(_omega);

        Label humans = HbrManualPhoneArt.Label("Humans",
            $"{HbrManualPhoneLayout.HumansLabel}  {HbrManualPhoneLayout.HumansCount}", 17, Muted);
        humans.TooltipText = "钟楼上的剩余人类计量仪";
        humans.MouseFilter = MouseFilterEnum.Pass;
        HbrManualPhoneArt.Place(humans, HbrManualPhoneLayout.Humans);
        AddChild(humans);

        // "等级" sits against the slash, where the wider "总等级" used to end.
        Label rankLabel = HbrManualPhoneArt.Label("RankLabel", HbrManualPhoneLayout.RankText, 30, Cyan, glow: true,
            align: HorizontalAlignment.Right);
        HbrManualPhoneArt.Place(rankLabel, HbrManualPhoneLayout.RankLabel);
        AddChild(rankLabel);
        AddChild(HbrManualPhoneArt.Sprite("Slash", "LineSlash", HbrManualPhoneLayout.Slash));
        _rank = HbrManualPhoneArt.Label("Rank", HbrManualPhoneLayout.NoRank, 84, Colors.White, glow: true);
        _rank.MouseFilter = MouseFilterEnum.Pass;
        HbrManualPhoneArt.Place(_rank, HbrManualPhoneLayout.RankNumber);
        AddChild(_rank);

        HbrPhoneRect row = HbrManualPhoneLayout.UnlockRow;
        Label unlockText = HbrManualPhoneArt.Label("UnlockText", HbrManualPhoneLayout.UnlockText, 26, Cyan);
        HbrManualPhoneArt.Place(unlockText, row with { Width = 60f });
        unlockText.TooltipText = "六感达到 30 级即为兼具";
        unlockText.MouseFilter = MouseFilterEnum.Pass;
        AddChild(unlockText);
        _unlock = HbrManualPhoneArt.Label("Unlock", "0 / 6", 30, Colors.White);
        HbrManualPhoneArt.Place(_unlock, row with { X = row.X + 66f, Width = 120f });
        AddChild(_unlock);
        Label from = HbrManualPhoneArt.Label("UnlockFrom", HbrManualPhoneLayout.UnlockFrom, 20, Muted,
            align: HorizontalAlignment.Right);
        HbrManualPhoneArt.Place(from, row);
        AddChild(from);

        HbrPhoneRect gauge = HbrManualPhoneLayout.Gauge;
        Panel gaugeShadow = new() { Name = "GaugeShadow", MouseFilter = MouseFilterEnum.Ignore };
        gaugeShadow.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color(0.27f, 0.42f, 0.4f, 0.5f), 8f));
        HbrManualPhoneArt.Place(gaugeShadow, gauge with
        {
            X = gauge.X + HbrManualPhoneLayout.GaugeShadow, Y = gauge.Y + HbrManualPhoneLayout.GaugeShadow
        });
        AddChild(gaugeShadow);
        Panel gaugeBack = new() { Name = "Gauge", MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        gaugeBack.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color(0.46f, 0.64f, 0.6f, 0.8f), 8f));
        HbrManualPhoneArt.Place(gaugeBack, gauge);
        AddChild(gaugeBack);
        Panel fill = new() { Name = "Fill", MouseFilter = MouseFilterEnum.Ignore };
        fill.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color("c7fcff"), 8f));
        fill.Size = new Vector2(0f, gauge.Height);
        gaugeBack.AddChild(fill);
        _gaugeFill = fill;

        _garden = Capsule("Garden", HbrManualPhoneLayout.GardenTop, "res://images/ui/memory_garden.png", HbrManualPhoneLayout.GardenText,
            "庭园升级点", null);
        _points = Capsule("Points", HbrManualPhoneLayout.PointsTop, "IconHc", HbrManualPhoneLayout.PointsText,
            "未分配的六感点数，点击前往六感", () => OpenSenses?.Invoke());

        BuildPortrait();

        _buttonRow = new Control { Name = "Buttons", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_buttonRow);

        HbrPhoneRect mini = HbrManualPhoneLayout.MiniRadar;
        Button miniButton = Hotspot("MiniRadar", mini, 150f, "六感总览：点击分配六感点数", () => OpenSenses?.Invoke());
        _mini = new NManualSenseRadar
        {
            Name = "Radar", Compact = true, Size = new Vector2(mini.Width, mini.Height),
            Centre = new Vector2(mini.Width, mini.Height) / 2f, Radius = HbrManualPhoneLayout.MiniRadius,
            HalfRadius = HbrManualPhoneLayout.MiniHalfRadius, Rings = HbrManualPhoneLayout.MiniRings.Select(v => (float)v).ToArray(),
            Dashed = HbrManualPhoneLayout.Threshold
        };
        miniButton.AddChild(_mini);
        for (int i = 0; i < 6; i++)
        {
            _miniLabels[i] = HbrManualPhoneArt.Label($"Label{i}", "", 18,
                new Color(HbrManualPhoneLayout.TierColours[i]));
            _miniLabels[i].AddThemeColorOverride("font_outline_color", new Color("061420"));
            _miniLabels[i].AddThemeConstantOverride("outline_size", 6);
            miniButton.AddChild(_miniLabels[i]);
        }

        AddChild(HbrManualPhoneArt.Sprite("Dock", "DenchoHeader", HbrManualPhoneLayout.Dock));
        for (int i = 0; i < 6; i++) BuildDockItem(i);
        _dockNote = HbrManualPhoneArt.Label("DockNote", HbrManualPhoneLayout.NoNumerics, 26, Muted,
            align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(_dockNote, HbrManualPhoneLayout.Dock);
        AddChild(_dockNote);
    }

    private Label Capsule(string name, float top, string icon, string text, string tip, Action press)
    {
        HbrPhoneRect rect = new(HbrManualPhoneLayout.CapsuleLeft, top, HbrManualPhoneLayout.CapsuleWidth,
            HbrManualPhoneLayout.CapsuleHeight);
        float shadow = HbrManualPhoneLayout.CapsuleShadow;
        Panel back = new() { Name = name + "Shadow", MouseFilter = MouseFilterEnum.Ignore };
        back.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color(0.1f, 0.16f, 0.19f, 0.5f), 26f));
        HbrManualPhoneArt.Place(back, rect with { X = rect.X + shadow, Y = rect.Y + shadow });
        AddChild(back);

        Button capsule = new() { Name = name, TooltipText = tip };
        HbrManualPhoneArt.Bare(capsule);
        StyleBoxFlat idle = HbrManualPhoneArt.Rounded(new Color(0.37f, 0.66f, 0.73f, 0.6f), 26f);
        capsule.AddThemeStyleboxOverride("normal", idle);
        capsule.AddThemeStyleboxOverride("disabled", idle);
        StyleBoxFlat lit = HbrManualPhoneArt.Rounded(press == null ? new Color(0.37f, 0.66f, 0.73f, 0.6f) :
            new Color(0.47f, 0.78f, 0.84f, 0.75f), 26f);
        capsule.AddThemeStyleboxOverride("hover", lit);
        capsule.AddThemeStyleboxOverride("pressed", lit);
        capsule.AddThemeStyleboxOverride("focus", press == null ? new StyleBoxEmpty() :
            HbrManualPhoneArt.Rounded(new Color(0f, 0f, 0f, 0f), 26f, HbrManualPhoneArt.Cyan, 2));
        if (press == null)
        {
            capsule.FocusMode = FocusModeEnum.None;
            capsule.MouseDefaultCursorShape = CursorShape.Arrow;
        }
        else
        {
            capsule.Pressed += press;
        }
        HbrManualPhoneArt.Place(capsule, rect);
        AddChild(capsule);
        capsule.AddChild(HbrManualPhoneArt.Sprite("Icon", icon, new HbrPhoneRect(-2f, -4f, 60f, 60f)));
        Label key = HbrManualPhoneArt.Label("Key", text, 21, new Color("e3fbfc"));
        HbrManualPhoneArt.Place(key, new HbrPhoneRect(62f, 0f, 150f, rect.Height));
        capsule.AddChild(key);
        Label value = HbrManualPhoneArt.Label("Value", "0", 36, Colors.White, align: HorizontalAlignment.Right);
        HbrManualPhoneArt.Place(value, new HbrPhoneRect(0f, -2f, rect.Width - 22f, rect.Height));
        capsule.AddChild(value);
        return value;
    }

    private void BuildPortrait()
    {
        HbrPhoneRect rect = HbrManualPhoneLayout.Portrait;
        Button portrait = new() { Name = "Portrait", TooltipText = "查看战斗属性" };
        HbrManualPhoneArt.Bare(portrait);
        portrait.AddThemeStyleboxOverride("focus", HbrManualPhoneArt.Rounded(new Color(0f, 0f, 0f, 0f), 170f, HbrManualPhoneArt.Cyan, 2));
        HbrManualPhoneArt.Place(portrait, rect);
        portrait.Pressed += () => OpenStat?.Invoke(-1);
        AddChild(portrait);

        portrait.AddChild(HbrManualPhoneArt.Sprite("Shadow", "Lotteryshadow", HbrManualPhoneLayout.PortraitShadow));
        // The face is a circle: a clipping disc with the head crop inside.
        NManualPortraitMask mask = new() { Name = "Face", Size = new Vector2(rect.Width, rect.Height) };
        portrait.AddChild(mask);
        _face = HbrManualPhoneArt.Sprite("Image", null, new HbrPhoneRect(0f, 0f, rect.Width, rect.Height));
        _face.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        mask.AddChild(_face);
        // box-shadow: inset 0 0 0 3px rgba(220,255,255,.55).
        Panel edge = new() { Name = "Edge", MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(rect.Width, rect.Height) };
        edge.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color(0f, 0f, 0f, 0f), rect.Width / 2f,
            new Color(0.86f, 1f, 1f, 0.55f), 3));
        portrait.AddChild(edge);

        TextureRect ring = HbrManualPhoneArt.Sprite("Ring", "DenchoBannerActive", HbrManualPhoneLayout.PortraitRing,
            new Color(1f, 1f, 1f, 0.76f));
        portrait.AddChild(ring);
        // breathe: opacity .76 -> .5 -> .76 over 3.2s.
        Tween breathe = CreateTween().SetLoops();
        breathe.TweenProperty(ring, "modulate:a", 0.5f, HbrManualPhoneLayout.RingBreathPeriod / 2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        breathe.TweenProperty(ring, "modulate:a", 0.76f, HbrManualPhoneLayout.RingBreathPeriod / 2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        TextureRect add = HbrManualPhoneArt.Sprite("Additive", "BtnDenchoLotteryAdditive",
            new HbrPhoneRect(0f, 0f, rect.Width, rect.Height), new Color(1f, 1f, 1f, 0f));
        add.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        portrait.AddChild(add);
        portrait.MouseEntered += () => add.Modulate = new Color(1f, 1f, 1f, 0.16f);
        portrait.MouseExited += () => add.Modulate = new Color(1f, 1f, 1f, 0f);

        // No title or level over the face: the portrait stays the way into battle stats (its tooltip
        // says so), and the battle level sits at the top left.
        HbrPhoneRect caption = HbrManualPhoneLayout.PortraitCaption;
        portrait.AddChild(HbrManualPhoneArt.Patch("Caption", "DenchoGachaTextBox", caption, HbrManualPhoneLayout.CaptionCap));
        _caption = HbrManualPhoneArt.Label("CaptionText", "", 22, new Color("eaf8f9"), align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(_caption, caption with { Y = caption.Y - 2f });
        portrait.AddChild(_caption);
    }

    private Button Hotspot(string name, HbrPhoneRect rect, float radius, string tip, Action press)
    {
        Button button = new() { Name = name, TooltipText = tip };
        HbrManualPhoneArt.Bare(button);
        HbrManualPhoneArt.Place(button, rect);
        NManualHighlight highlight = new() { Name = "Highlight", Size = new Vector2(rect.Width, rect.Height), Radius = radius };
        button.AddChild(highlight);
        button.MouseEntered += () => highlight.Hovered = true;
        button.MouseExited += () => highlight.Hovered = false;
        button.FocusEntered += () => highlight.Hovered = true;
        button.FocusExited += () => highlight.Hovered = false;
        button.Pressed += press;
        AddChild(button);
        return button;
    }

    private void BuildDockItem(int index)
    {
        float centre = HbrManualPhoneLayout.DockItemCentre(index);
        HbrPhoneRect rect = new(centre - HbrManualPhoneLayout.DockItemWidth / 2f, HbrManualPhoneLayout.DockItemTop,
            HbrManualPhoneLayout.DockItemWidth, HbrManualPhoneLayout.DockItemHeight);
        SixSenseAttribute sense = HbrManualPhoneData.StatSenses[index];
        string name = HbrBattleStatsHelper.GetBattleStatTitle(sense);
        Button item = Hotspot($"Stat{index}", rect, 18f,
            $"{name}：{HbrManualPhoneData.StatUses[index]}（点击查看战斗属性详情）", () => OpenStat?.Invoke(index));
        Label title = HbrManualPhoneArt.Label("Name", name, 24, Cyan, align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(title, new HbrPhoneRect(0f, 12f, rect.Width, 34f));
        item.AddChild(title);
        Label value = HbrManualPhoneArt.Label("Value", "0", 52, new Color("effeff"), glow: true, align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(value, new HbrPhoneRect(-10f, 46f, rect.Width + 20f, 60f));
        item.AddChild(value);
        Label source = HbrManualPhoneArt.Label("Source", "", 18, Muted, align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(source, new HbrPhoneRect(-8f, 110f, rect.Width + 16f, 26f));
        item.AddChild(source);
        Label pending = HbrManualPhoneArt.Label("Pending", "", 19, Pink, align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(pending, new HbrPhoneRect(-8f, 136f, rect.Width + 16f, 26f));
        item.AddChild(pending);
        _dock.Add((value, source, pending));
    }

    // One row of terminal buttons; rebuilt only when the set changes (equipment, cannons).
    private void BuildButtons(IReadOnlyList<HbrManualHomeButton> buttons)
    {
        string key = string.Join(",", buttons);
        if (key == _buttonsKey) return;
        _buttonsKey = key;
        foreach (Node child in _buttonRow.GetChildren()) child.QueueFree();
        _badgeRoot = null;
        _badge = null;
        float[] centres = HbrManualPhoneLayout.RingButtonCentres(buttons.Count);
        float scale = HbrManualPhoneLayout.RingButtonScale(buttons.Count);
        for (int i = 0; i < buttons.Count; i++) BuildRingButton(buttons[i], centres[i], scale);
    }

    private void BuildRingButton(HbrManualHomeButton kind, float centreX, float scale)
    {
        float size = HbrManualPhoneLayout.RingButtonSize;
        Button button = new()
        {
            Name = kind.ToString(), TooltipText = HbrManualPhoneLayout.HomeButtonTip(kind),
            Position = new Vector2(centreX - size / 2f, HbrManualPhoneLayout.RingButtonTop - size / 2f),
            Size = new Vector2(size, size), PivotOffset = new Vector2(size, size) / 2f, Scale = Vector2.One * scale
        };
        HbrManualPhoneArt.Bare(button);
        _buttonRow.AddChild(button);
        if (kind == HbrManualHomeButton.Senses) _firstButton = button;
        button.AddChild(HbrManualPhoneArt.Sprite("Shadow", "BtnDenchoshadow", new HbrPhoneRect(10f, 20f, 173f, 155f)));

        Action<bool> hover;
        if (kind == HbrManualHomeButton.Senses)
        {
            // BtnDenchoStatusNormal's circle is centred at (88, 106) of the 178x195 sprite.
            TextureRect normal = HbrManualPhoneArt.Sprite("Normal", "BtnDenchoStatusNormal", new HbrPhoneRect(0f, -17f, 178f, 195f));
            TextureRect active = HbrManualPhoneArt.Sprite("Active", "BtnDenchoStatusActive", new HbrPhoneRect(-29f, -29f, 236f, 236f),
                new Color(1f, 1f, 1f, 0f));
            button.AddChild(normal);
            button.AddChild(active);
            hover = on =>
            {
                normal.Modulate = new Color(1f, 1f, 1f, on ? 0f : 1f);
                active.Modulate = new Color(1f, 1f, 1f, on ? 1f : 0f);
            };
        }
        else
        {
            NManualFrost frost = new() { Name = "Frost", Size = new Vector2(size, size) };
            button.AddChild(frost);
            string icon = kind switch
            {
                HbrManualHomeButton.Equipment => "icon_equipment",
                HbrManualHomeButton.Shop => "icon_shop",
                HbrManualHomeButton.Cannon => "icon_cannon",
                // The fate dice as a white glyph like the icons beside it, tinted 31O blue below.
                HbrManualHomeButton.FateDice => HbrMagicSwordArt.FateDiceGlyph,
                _ => "icon_glossary"
            };
            HbrPhoneRect iconRect = kind == HbrManualHomeButton.Cannon
                ? new HbrPhoneRect(19f, 34f, 140f, 56f)
                : new HbrPhoneRect(47f, 20f, 84f, 84f);
            button.AddChild(HbrManualPhoneArt.Sprite("Icon", icon, iconRect,
                kind == HbrManualHomeButton.FateDice ? new Color("7799CC") : null));
            if (kind != HbrManualHomeButton.Cannon)
            {
                // A faint flipped reflection of the icon's lower half.
                Control reflection = new()
                {
                    Name = "Reflection", Position = new Vector2(47f, 96f), Size = new Vector2(84f, 42f),
                    ClipContents = true, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0.18f)
                };
                TextureRect flipped = HbrManualPhoneArt.Sprite("Icon", icon, new HbrPhoneRect(0f, 0f, 84f, 84f));
                flipped.FlipV = true;
                reflection.AddChild(flipped);
                button.AddChild(reflection);
            }
            hover = on => frost.Hovered = on;
        }

        Label label = HbrManualPhoneArt.Label("Label", HbrManualPhoneLayout.HomeButtonTitle(kind), 30, Colors.White,
            glow: true, align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(label, new HbrPhoneRect(-20f, 120f, size + 40f, 40f));
        button.AddChild(label);

        if (kind == HbrManualHomeButton.Senses)
        {
            _badgeRoot = new Control { Name = "Badge", Position = new Vector2(130f, 16f), Size = new Vector2(44f, 44f),
                MouseFilter = MouseFilterEnum.Ignore };
            _badgeRoot.AddChild(HbrManualPhoneArt.Sprite("Back", "IconBatchNumber", new HbrPhoneRect(0f, 0f, 44f, 44f)));
            _badge = HbrManualPhoneArt.Label("Count", "0", 22, Colors.White, align: HorizontalAlignment.Center);
            HbrManualPhoneArt.Place(_badge, new HbrPhoneRect(0f, 0f, 44f, 44f));
            _badgeRoot.AddChild(_badge);
            button.AddChild(_badgeRoot);
        }

        button.MouseEntered += () => hover(true);
        button.MouseExited += () => hover(false);
        button.FocusEntered += () => hover(true);
        button.FocusExited += () => hover(false);
        button.ButtonDown += () => button.Scale = Vector2.One * scale * 0.96f;
        button.ButtonUp += () => button.Scale = Vector2.One * scale;
        button.Pressed += () => OpenButton?.Invoke(kind);
    }

    internal void Refresh(ElectronicSoldierManual manual, SixSenseAllocationDraft draft, IReadOnlyList<HbrManualHomeButton> buttons)
    {
        if (_rank == null || manual == null || draft == null) return;
        BuildButtons(buttons);
        var senses = ElectronicSoldierManual.AllSixSenseAttributes.ToArray();
        int[] levels = senses.Select(manual.GetSenseAmount).ToArray();
        int unlocked = levels.Count(level => level >= HbrManualPhoneLayout.Threshold);
        // Runs without HBR numerics have no battle level.
        _rank.Text = manual.IsHbrNumericsEnabled ? manual.HbrBattleLevel.ToString() : HbrManualPhoneLayout.NoRank;
        _rank.TooltipText = manual.IsHbrNumericsEnabled
            ? $"战斗等级：普通战、精英战、Boss 战胜利分别提升 1、2、3 级，每级六维各 +{HbrBattleStatsHelper.StatGainPerLevel}"
            : HbrManualPhoneLayout.NoNumerics;
        _unlock.Text = $"{unlocked} / 6";
        _gaugeFill.Size = new Vector2(HbrManualPhoneLayout.Gauge.Width * unlocked / 6f, HbrManualPhoneLayout.Gauge.Height);
        _garden.Text = manual.MemoryGardenUpgradePoints.ToString();
        _points.Text = draft.Remaining.ToString();
        _points.AddThemeColorOverride("font_color", draft.Remaining > 0 ? Pink : Colors.White);
        if (_badgeRoot != null)
        {
            _badgeRoot.Visible = draft.Remaining > 0;
            _badge.Text = draft.Remaining > 99 ? "99+" : draft.Remaining.ToString();
        }

        bool hotaru = HbrManualPhoneData.IsHotaru(manual), thirtyA = HbrManualPhoneData.IsThirtyA(manual);
        _face.Texture = HbrManualPhoneArt.Texture(HbrManualPhoneData.FaceTexture(manual));
        _caption.Text = HbrManualPhoneLayout.Chant(hotaru, thirtyA);
        _omega.Visible = !hotaru && !thirtyA;

        _mini.Values = levels.Select(level => (float)level).ToArray();
        _mini.Pending = senses.Select(sense => (float)draft.Level(sense)).ToArray();
        _mini.Drafted = senses.Select(sense => draft.Added(sense) > 0).ToArray();
        _mini.QueueRedraw();
        for (int i = 0; i < 6; i++) PlaceMiniLabel(i, senses[i], levels[i], draft);

        bool numerics = manual.IsHbrNumericsEnabled;
        _dockNote.Visible = !numerics;
        HbrBattleStats stats = HbrManualPhoneData.Stats(manual);
        for (int i = 0; i < _dock.Count; i++)
        {
            var (value, source, pending) = _dock[i];
            value.GetParent<Control>().Visible = numerics;
            if (!numerics) continue;
            SixSenseAttribute sense = HbrManualPhoneData.StatSenses[i];
            // Whole part only; the stats page keeps the decimals. Display only, combat uses the full value.
            value.Text = HbrManualPhoneLayout.DockValueText(HbrManualPhoneData.StatValue(stats, i));
            value.AddThemeFontSizeOverride("font_size", HbrManualPhoneLayout.DockValueFontSize(value.Text));
            source.Text = $"{ElectronicSoldierManual.GetSenseTitle(sense)} +{HbrManualPhoneData.Number(HbrManualPhoneData.SenseBonus(manual, sense))}";
            decimal gain = HbrManualPhoneData.PendingGain(manual, draft, sense);
            pending.Text = gain > 0 ? $"{HbrManualPhoneLayout.PendingText} +{HbrManualPhoneData.Number(gain)}" : string.Empty;
        }
    }

    private void PlaceMiniLabel(int index, SixSenseAttribute sense, int level, SixSenseAllocationDraft draft)
    {
        Label label = _miniLabels[index];
        int added = draft.Added(sense);
        label.Text = $"{ElectronicSoldierManual.GetSenseTitle(sense)} {draft.Level(sense)}" + (added > 0 ? $" +{added}" : "");
        label.TooltipText = $"{ElectronicSoldierManual.GetSenseTitle(sense)}：{HbrManualPhoneLayout.Leanings[index]}";
        HbrPhoneRect mini = HbrManualPhoneLayout.MiniRadar;
        var (x, y) = HbrManualPhoneLayout.Point(mini.Width / 2f, mini.Height / 2f, index, HbrManualPhoneLayout.MiniLabelRadius);
        int side = HbrManualPhoneLayout.MiniLabelSide(index);
        float width = label.GetThemeFont("font").GetStringSize(label.Text, HorizontalAlignment.Left, -1, 18).X + 8f;
        float left = side == 0 ? x - width / 2f : side > 0 ? x + 4f : x - 4f - width;
        label.Position = new Vector2(left, y - 12f);
        label.Size = new Vector2(width, 24f);
        label.HorizontalAlignment = HorizontalAlignment.Center;
    }
}
