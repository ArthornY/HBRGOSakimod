using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// 31O's magic sword switch on the battle HUD (design v3, sections 3 and 5). One image, three states: grey when
// it cannot open, its own colours with a breathing glow when it can, and a red frame (after the red Veiled
// Sword) while open; the swordheart pill below reads "kept/maximum". Keyboard and gamepad reach it from the
// hand, left of the leftmost card, and press it with accept or select. The switch's rules stay in
// HbrMagicSwordToggle; this node only shows them and sends the request.
public partial class NHbrMagicSwordButton : Control
{
    internal const string WidgetName = "HbrgoSakiMagicSwordButton";
    private const string SwordheartIcon = "res://images/powers/swordheart_power.png";
    private const float BreathPeriod = 2.2f;
    private const float HoverScale = 1.06f;
    private const float PressScale = 0.93f;
    private const float PressTime = 0.12f;
    private const float LinkInterval = 0.2f;
    private const int GreySize = 192;

    private static readonly Color OpenFrame = new("D9343A");
    private static readonly Color ReadyRing = new("C9A8FF");
    private static readonly Color IdleRing = new(0.55f, 0.55f, 0.58f, 0.75f);
    private static readonly Color FocusRing = new(1f, 1f, 1f, 0.9f);
    private static readonly Color CountColor = new("F8D36A");
    private static readonly Color PillFill = new(0.07f, 0.04f, 0.1f, 0.82f);

    private static ImageTexture _greyIcon;

    private Control _medal;
    private TextureRect _icon;
    private TextureRect _grey;
    private Label _count;
    private Player _player;
    private float _time;
    private float _linkTimer;
    private float _pressed;
    private readonly HbrMagicSwordHoverState _hover = new();
    private ButtonState _state;
    private NHoverTipSet _hoverTips;
    private string _tip;
    private NHandCardHolder _linked;

    private enum ButtonState
    {
        Hidden,
        Disabled,
        Ready,
        Open
    }

    public override void _Ready()
    {
        Name = WidgetName;
        AnchorLeft = 0f;
        AnchorRight = 0f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = HbrBattleHudLayout.MagicSwordLeft;
        OffsetRight = OffsetLeft + HbrBattleHudLayout.MagicSwordMedal;
        OffsetTop = -HbrBattleHudLayout.MagicSwordFromBottom;
        OffsetBottom = OffsetTop + HbrBattleHudLayout.MagicSwordMedal + HbrBattleHudLayout.MagicSwordPillGap +
            HbrBattleHudLayout.MagicSwordPillHeight;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ZIndex = 30;
        Visible = false;
        float medal = HbrBattleHudLayout.MagicSwordMedal;
        PivotOffset = new Vector2(medal / 2f, medal / 2f);

        // The square icon cut into a circle by the roster's round mask (children clip to its alpha).
        _medal = new TextureRect
        {
            Name = "Medal",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = ResourceLoader.Load<Texture2D>(HbrMagicSwordArt.CircleMask),
            Size = new Vector2(medal, medal),
            ClipChildren = ClipChildrenMode.Only,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Texture2D sword = ResourceLoader.Load<Texture2D>(HbrMagicSwordArt.SwordIcon);
        _icon = FullPicture("Icon", sword);
        _grey = FullPicture("Grey", GreyIcon(sword) ?? sword);
        _medal.AddChild(_icon);
        _medal.AddChild(_grey);
        AddChild(_medal);
        AddChild(BuildPill());

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
        FocusEntered -= OnFocusEntered;
        FocusExited -= OnFocusExited;
        _hover.Reset();
        HideHoverTip();
        Unlink();
    }

    private Control BuildPill()
    {
        float medal = HbrBattleHudLayout.MagicSwordMedal, height = HbrBattleHudLayout.MagicSwordPillHeight;
        Panel pill = new()
        {
            Name = "Swordheart",
            Position = new Vector2(0f, medal + HbrBattleHudLayout.MagicSwordPillGap),
            Size = new Vector2(medal, height),
            MouseFilter = MouseFilterEnum.Ignore
        };
        StyleBoxFlat fill = new() { BgColor = PillFill, BorderColor = new Color(CountColor, 0.45f) };
        fill.SetBorderWidthAll(1);
        fill.SetCornerRadiusAll((int)(height / 2f));
        pill.AddThemeStyleboxOverride("panel", fill);
        TextureRect icon = new()
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = ResourceLoader.Load<Texture2D>(SwordheartIcon),
            Position = new Vector2(4f, 2f),
            Size = new Vector2(height - 4f, height - 4f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        pill.AddChild(icon);
        _count = HbrBattleHudArt.Label("Count", string.Empty, 20, CountColor);
        HbrBattleHudArt.Outline(_count, new Color(0.18f, 0.05f, 0.02f, 0.9f), 4);
        _count.Position = new Vector2(height - 2f, 0f);
        _count.Size = new Vector2(medal - height, height);
        pill.AddChild(_count);
        return pill;
    }

    public override void _Process(double delta)
    {
        float step = (float)delta;
        _time += step;
        _pressed = Math.Max(0f, _pressed - step);
        _player = LocalPlayer();
        bool show = _player != null && HbrMagicSwordToggle.Applies(_player) && CombatManager.Instance?.IsInProgress == true;
        if (!show)
        {
            _hover.Reset();
            HideHoverTip();
            Visible = false;
            _state = ButtonState.Hidden;
            Unlink();
            return;
        }

        Visible = true;
        bool open = HbrMagicSwordToggle.IsOpen(_player);
        bool canOpen = HbrMagicSwordToggle.CanOpen(_player);
        _state = open ? ButtonState.Open : canOpen ? ButtonState.Ready : ButtonState.Disabled;
        _count.Text = $"{HbrMagicSwordToggle.Swordheart(_player)}/{SwordheartPower.GetMaxAmount(_player.Creature)}";
        _grey.Visible = _state == ButtonState.Disabled;
        _icon.Visible = !_grey.Visible;
        _medal.Modulate = _state == ButtonState.Disabled ? new Color(0.78f, 0.78f, 0.78f, 0.9f) : Colors.White;
        bool interactive = canOpen || HbrMagicSwordToggle.CanClose(_player);
        MouseDefaultCursorShape = interactive ? CursorShape.PointingHand : CursorShape.Arrow;
        float scale = (_hover.PointerInside || HasFocus()) && interactive ? HoverScale : 1f;
        if (_pressed > 0f) scale = PressScale;
        Scale = Vector2.One * scale;
        RefreshHoverTip();
        _linkTimer -= step;
        if (_linkTimer <= 0f)
        {
            _linkTimer = LinkInterval;
            LinkToHand();
        }
        QueueRedraw();
    }

    // Behind the medal: the breathing glow when it can open, the red frame while open, a focus ring for
    // keyboard and gamepad. All drawn in code from the same circle.
    public override void _Draw()
    {
        float medal = HbrBattleHudLayout.MagicSwordMedal, ring = HbrBattleHudLayout.MagicSwordRingWidth;
        Vector2 centre = new(medal / 2f, medal / 2f);
        float radius = medal / 2f;
        switch (_state)
        {
            case ButtonState.Ready:
            {
                float breath = 0.5f - 0.5f * Mathf.Cos(_time / BreathPeriod * Mathf.Tau);
                for (int layer = 0; layer < 4; layer++)
                    DrawCircle(centre, radius + 4f + layer * 5f, new Color(ReadyRing, (0.12f + 0.2f * breath) / (layer + 1)));
                DrawArc(centre, radius + 1f, 0f, Mathf.Tau, 64, new Color(ReadyRing, 0.55f + 0.35f * breath), 2.5f, true);
                break;
            }
            case ButtonState.Open:
            {
                float pulse = 0.5f - 0.5f * Mathf.Cos(_time / BreathPeriod * 2f * Mathf.Tau);
                for (int layer = 0; layer < 3; layer++)
                    DrawCircle(centre, radius + 6f + layer * 5f, new Color(OpenFrame, (0.16f + 0.12f * pulse) / (layer + 1)));
                DrawArc(centre, radius + ring / 2f, 0f, Mathf.Tau, 64, OpenFrame, ring, true);
                break;
            }
            case ButtonState.Disabled:
                DrawArc(centre, radius + 1f, 0f, Mathf.Tau, 64, IdleRing, 2f, true);
                break;
        }

        if (HasFocus())
            DrawArc(centre, radius + ring + 5f, 0f, Mathf.Tau, 64, FocusRing, 2f, true);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        bool click = inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false };
        bool confirm = HasFocus() && (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("ui_select"));
        if (!click && !confirm) return;
        AcceptEvent();
        Press();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!IsVisibleInTree()) return;
        if (inputEvent is InputEventMouseButton ||
            inputEvent is InputEventMouseMotion motion && motion.Relative.LengthSquared() > 0f)
            _hover.UsePointer();
        else
        {
            bool navigation = false;
            foreach (string action in HbrMagicSwordHoverState.NavigationActions)
            {
                if (!inputEvent.IsActionPressed(action)) continue;
                navigation = _hover.NavigationInput(action, HasFocus());
                break;
            }
            if (!navigation) return;
        }
        RefreshHoverTip();
    }

    private void Press()
    {
        if (_player == null) return;
        ElectronicSoldierManual manual = _player.GetRelic<ElectronicSoldierManual>();
        if (HbrMagicSwordToggle.CanOpen(_player)) HbrFateDiceNetActions.RequestSwitch(manual, open: true);
        else if (HbrMagicSwordToggle.CanClose(_player)) HbrFateDiceNetActions.RequestSwitch(manual, open: false);
        else return;
        _pressed = PressTime;
    }

    private void OnMouseEntered()
    {
        _hover.PointerEntered();
        RefreshHoverTip();
    }

    private void OnMouseExited()
    {
        _hover.PointerExited();
        RefreshHoverTip();
    }

    private void OnFocusEntered()
    {
        _hover.SetFocused(true);
        RefreshHoverTip();
    }

    private void OnFocusExited()
    {
        _hover.SetFocused(false);
        RefreshHoverTip();
    }

    // The mod's hover tips (the same box as the OD gauge), rebuilt only when the text changes.
    private void ShowHoverTip()
    {
        if (!IsVisibleInTree() || _player == null || !_hover.WantsTooltip || NHoverTipSet.shouldBlockHoverTips) return;
        _tip = TipText();
        try
        {
            NHoverTipSet.Remove(this);
            _hoverTips = NHoverTipSet.CreateAndShow(this,
                new HoverTip(HbrCompactText.Literal("title", "魔剑"), HbrCompactText.Literal("text", _tip)),
                HoverTip.GetHoverTipAlignment(this));
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Magic sword hover tip failed: {error.Message}");
        }
    }

    private void RefreshHoverTip()
    {
        if (!IsVisibleInTree() || _player == null || !_hover.WantsTooltip || NHoverTipSet.shouldBlockHoverTips)
        {
            HideHoverTip();
            return;
        }
        if (!GodotObject.IsInstanceValid(_hoverTips) || TipText() != _tip) ShowHoverTip();
    }

    private void HideHoverTip()
    {
        if (GodotObject.IsInstanceValid(_hoverTips)) _hoverTips.Hide();
        if (_hoverTips != null || _tip != null) NHoverTipSet.Remove(this);
        _hoverTips = null;
        _tip = null;
    }

    private string TipText()
    {
        int kept = HbrMagicSwordToggle.Swordheart(_player);
        int maximum = SwordheartPower.GetMaxAmount(_player.Creature);
        string swordheart = $"剑心 {kept}/{maximum}，跨战斗保留。";
        if (HbrMagicSwordToggle.IsOpen(_player))
        {
            int spent = _player.Creature?.GetPower<MagicSwordModePower>()?.ConsumedSwordheartAmount ?? 0;
            string close = HbrMagicSwordToggle.CanClose(_player) ? $"再按一次关掉，打出魔剑幸运七（{spent}段），结算完毕立刻结束当前回合。" : "只能在自己的出牌阶段关掉。";
            return $"魔剑开启中，这次已花掉{spent}层剑心。{close}\n{swordheart}";
        }

        string rule = "开启魔剑：不用牌，不花能量，开启时不加裂痕。开着时攻击牌掷骰出目+77，卡组每张牌使伤害与破坏率增长提高2%，能量不够时用剑心补；剑心花光或再按一次时，打出魔剑幸运七，段数等于这次花掉的剑心。终结技结算后加一道裂痕，随后结束当前回合；战斗结束时仍开着也加。紧急撤出不打终结技、不结束回合、不加裂痕。";
        string reason = HbrMagicSwordToggle.CanOpen(_player) ? string.Empty
            : kept < HbrMagicSwordToggle.MinimumSwordheart ? "\n至少要有1层剑心才能开启。"
            : "\n只能在自己的出牌阶段开启。";
        return $"{rule}{reason}\n{swordheart}";
    }

    // Keyboard and gamepad: left from the hand's leftmost card reaches the switch, right comes back. The hand
    // rewires its neighbours on every layout, so the link is renewed here and handed back when hidden.
    private void LinkToHand()
    {
        try
        {
            var holders = NCombatRoom.Instance?.Ui?.Hand?.ActiveHolders;
            if (holders == null || holders.Count == 0)
            {
                Unlink();
                NHbrMachinaButton buddy = GetParent()?.GetNodeOrNull<NHbrMachinaButton>(NHbrMachinaButton.WidgetName);
                NodePath own = GetPath();
                FocusNeighborLeft = FocusNeighborRight = buddy?.Visible == true ? buddy.GetPath() : own;
                FocusNeighborTop = FocusNeighborBottom = own;
                return;
            }

            NHandCardHolder leftmost = holders[0];
            NodePath self = GetPath();
            if (!ReferenceEquals(_linked, leftmost)) Unlink();
            _linked = leftmost;
            // NodePath has no value equality; compare the text.
            NHbrMachinaButton machina = GetParent()?.GetNodeOrNull<NHbrMachinaButton>(NHbrMachinaButton.WidgetName);
            NodePath handLeft = machina?.Visible == true ? machina.GetPath() : self;
            if (leftmost.FocusNeighborLeft?.ToString() != handLeft.ToString()) leftmost.FocusNeighborLeft = handLeft;
            NodePath first = leftmost.GetPath();
            FocusNeighborRight = machina?.Visible == true ? machina.GetPath() : first;
            FocusNeighborLeft = holders[holders.Count - 1].GetPath();
            FocusNeighborTop = self;
            FocusNeighborBottom = self;
            if (machina?.Visible == true) machina.FocusNeighborLeft = self;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Magic sword focus link failed: {error.Message}");
        }
    }

    private void Unlink()
    {
        if (_linked == null) return;
        try
        {
            // Back to the hand's own wrap (leftmost → rightmost) when it still points here.
            if (GodotObject.IsInstanceValid(_linked) && IsInsideTree() &&
                _linked.FocusNeighborLeft?.ToString() == GetPath().ToString())
            {
                var holders = NCombatRoom.Instance?.Ui?.Hand?.ActiveHolders;
                _linked.FocusNeighborLeft = holders != null && holders.Count > 0 ? holders[holders.Count - 1].GetPath() : new NodePath();
            }
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Magic sword focus unlink failed: {error.Message}");
        }
        _linked = null;
    }

    private TextureRect FullPicture(string name, Texture2D texture)
    {
        TextureRect picture = new()
        {
            Name = name,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Texture = texture,
            MouseFilter = MouseFilterEnum.Ignore
        };
        picture.SetAnchorsPreset(LayoutPreset.FullRect);
        return picture;
    }

    // The grey state from the same image: downscaled once, then each pixel set to its luma.
    private static Texture2D GreyIcon(Texture2D source)
    {
        if (GodotObject.IsInstanceValid(_greyIcon)) return _greyIcon;
        try
        {
            Image image = source?.GetImage();
            if (image == null) return null;
            if (image.IsCompressed()) image.Decompress();
            image.Convert(Image.Format.Rgba8);
            image.Resize(GreySize, GreySize, Image.Interpolation.Bilinear);
            byte[] data = image.GetData();
            for (int i = 0; i + 3 < data.Length; i += 4)
            {
                byte luma = (byte)Math.Clamp((int)Math.Round(0.2126 * data[i] + 0.7152 * data[i + 1] + 0.0722 * data[i + 2]), 0, 255);
                data[i] = data[i + 1] = data[i + 2] = luma;
            }
            using Image grey = Image.CreateFromData(GreySize, GreySize, false, Image.Format.Rgba8, data);
            _greyIcon = ImageTexture.CreateFromImage(grey);
            return _greyIcon;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Magic sword grey icon failed: {error.Message}");
            return null;
        }
    }

    private static Player LocalPlayer()
    {
        RunState run = RunManager.Instance?.DebugOnlyGetState();
        return run == null ? null : LocalContext.GetMe(run.Players);
    }
}
