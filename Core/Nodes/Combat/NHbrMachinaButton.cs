using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Presentation only. The synchronized action validates phase and spends uses;
// the client never mutates the relic or executes a local-only attack.
public partial class NHbrMachinaButton : Control
{
    internal const string WidgetName = "HbrgoSakiMachinaButton";
    private const string ArtRoot = "res://images/ui/thirty_one_c/";
    private Texture2D _active, _hover, _disabled;
    private TextureRect _picture;
    private Label _remaining, _servants;
    private Player _player;
    private NHandCardHolder _linked;
    private NHoverTipSet _tips;
    private string _tip;
    private bool _hovered, _canActivate;
    private float _refresh;

    public override void _Ready()
    {
        Name = WidgetName;
        AnchorTop = AnchorBottom = 1f;
        OffsetLeft = HbrBattleHudLayout.MachinaLeft;
        OffsetRight = OffsetLeft + HbrBattleHudLayout.MachinaSize;
        OffsetTop = -HbrBattleHudLayout.MachinaFromBottom;
        OffsetBottom = OffsetTop + HbrBattleHudLayout.MachinaSize + HbrBattleHudLayout.MachinaFooterGap +
            HbrBattleHudLayout.MachinaFooterHeight;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ZIndex = 30;
        Visible = false;
        _active = ResourceLoader.Load<Texture2D>(ArtRoot + "machina_button_active.png");
        _hover = ResourceLoader.Load<Texture2D>(ArtRoot + "machina_button_small.png");
        _disabled = ResourceLoader.Load<Texture2D>(ArtRoot + "machina_button_disabled.png");
        float size = HbrBattleHudLayout.MachinaSize;
        _picture = Picture("Button", _disabled, Vector2.Zero, new Vector2(size, size));
        AddChild(_picture);
        _remaining = HbrBattleHudArt.Label("Remaining", string.Empty, 24, new Color("FFF0BF"));
        HbrBattleHudArt.Outline(_remaining, Colors.Black, 5);
        _remaining.HorizontalAlignment = HorizontalAlignment.Right;
        _remaining.Position = new Vector2(size - 40f, size - 32f);
        _remaining.Size = new Vector2(34f, 30f);
        _remaining.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_remaining);
        float footerY = size + HbrBattleHudLayout.MachinaFooterGap;
        AddChild(Picture("ServantIcon", ResourceLoader.Load<Texture2D>(ArtRoot + "yamawaki_servant_icon.png"),
            new Vector2(24f, footerY), new Vector2(26f, 26f)));
        _servants = HbrBattleHudArt.Label("Servants", string.Empty, 21, Colors.White);
        HbrBattleHudArt.Outline(_servants, Colors.Black, 4);
        _servants.Position = new Vector2(54f, footerY);
        _servants.Size = new Vector2(38f, HbrBattleHudLayout.MachinaFooterHeight);
        _servants.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_servants);
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        FocusEntered += ShowTip;
        FocusExited += HideTip;
    }

    public override void _Process(double delta)
    {
        _refresh -= (float)delta;
        if (_refresh > 0f) return;
        _refresh = .1f;
        RunState run = RunManager.Instance?.DebugOnlyGetState();
        _player = run == null ? null : LocalContext.GetMe(run.Players);
        bool show = _player != null && CombatManager.Instance?.IsInProgress == true &&
            ThirtyOneCMachinaHelper.IsVisible(_player);
        if (!show)
        {
            if (Visible) HideTip();
            Visible = false;
            Unlink();
            return;
        }
        Visible = true;
        _canActivate = ThirtyOneCMachinaHelper.CanActivate(_player);
        _remaining.Text = ThirtyOneCMachinaHelper.RemainingUses(_player).ToString();
        _servants.Text = ThirtyOneCMachinaHelper.ServantCount(_player).ToString();
        RefreshPicture();
        LinkToHand();
        if ((_hovered || HasFocus()) && GodotObject.IsInstanceValid(_tips) && _tip != TipText()) ShowTip();
    }

    private void RefreshPicture()
    {
        if (_picture == null) return;
        _picture.Texture = !_canActivate ? _disabled : _hovered || HasFocus() ? _hover : _active;
        MouseDefaultCursorShape = _canActivate ? CursorShape.PointingHand : CursorShape.Arrow;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (HasFocus()) DrawRect(new Rect2(-4f, -4f, HbrBattleHudLayout.MachinaSize + 8f,
            HbrBattleHudLayout.MachinaSize + 8f), new Color("FFF0BF"), false, 2f);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        bool click = inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false };
        bool confirm = HasFocus() && (inputEvent.IsActionPressed("ui_accept") || inputEvent.IsActionPressed("ui_select"));
        if (!click && !confirm) return;
        AcceptEvent();
        if (_player != null && ThirtyOneCMachinaHelper.CanActivate(_player))
            ThirtyOneCMachinaHelper.RequestActivate(_player);
    }

    private string TipText() => ThirtyOneCMachinaHelper.HoverText(_player);
    private void OnMouseEntered() { _hovered = true; RefreshPicture(); ShowTip(); }
    private void OnMouseExited() { _hovered = false; RefreshPicture(); if (!HasFocus()) HideTip(); }
    private void ShowTip()
    {
        RefreshPicture();
        if (!Visible || _player == null || NHoverTipSet.shouldBlockHoverTips) return;
        _tip = TipText();
        try
        {
            NHoverTipSet.Remove(this);
            _tips = NHoverTipSet.CreateAndShow(this,
                new HoverTip(HbrCompactText.Literal("title", "魔界骑兵启动！"), HbrCompactText.Literal("text", _tip)),
                HoverTip.GetHoverTipAlignment(this));
        }
        catch (Exception error) { Log.Warn($"[HBRGOSakiMod] Machina hover tip failed: {error.Message}"); }
    }
    private void HideTip()
    {
        if (GodotObject.IsInstanceValid(_tips)) _tips.Hide();
        NHoverTipSet.Remove(this);
        _tips = null;
        _tip = null;
        RefreshPicture();
    }

    // Hand → Machina → sword → last card when moving left. Both buttons write
    // the same chain, so periodic native hand layout cannot make them compete.
    private void LinkToHand()
    {
        var holders = NCombatRoom.Instance?.Ui?.Hand?.ActiveHolders;
        if (holders == null || holders.Count == 0)
        {
            Unlink();
            NHbrMagicSwordButton buddy = GetParent()?.GetNodeOrNull<NHbrMagicSwordButton>(NHbrMagicSwordButton.WidgetName);
            NodePath own = GetPath();
            FocusNeighborLeft = FocusNeighborRight = buddy?.Visible == true ? buddy.GetPath() : own;
            FocusNeighborTop = FocusNeighborBottom = own;
            return;
        }
        NHandCardHolder first = holders[0];
        if (!ReferenceEquals(_linked, first)) Unlink();
        _linked = first;
        NodePath self = GetPath();
        first.FocusNeighborLeft = self;
        FocusNeighborRight = first.GetPath();
        NHbrMagicSwordButton sword = GetParent()?.GetNodeOrNull<NHbrMagicSwordButton>(NHbrMagicSwordButton.WidgetName);
        FocusNeighborLeft = sword?.Visible == true ? sword.GetPath() : holders[holders.Count - 1].GetPath();
        FocusNeighborTop = FocusNeighborBottom = self;
        if (sword?.Visible == true) sword.FocusNeighborRight = self;
    }

    private void Unlink()
    {
        if (_linked == null) return;
        if (GodotObject.IsInstanceValid(_linked) && IsInsideTree() &&
            _linked.FocusNeighborLeft?.ToString() == GetPath().ToString())
        {
            var holders = NCombatRoom.Instance?.Ui?.Hand?.ActiveHolders;
            NHbrMagicSwordButton sword = GetParent()?.GetNodeOrNull<NHbrMagicSwordButton>(NHbrMagicSwordButton.WidgetName);
            _linked.FocusNeighborLeft = sword?.Visible == true ? sword.GetPath() :
                holders != null && holders.Count > 0 ? holders[holders.Count - 1].GetPath() : new NodePath();
        }
        _linked = null;
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
        FocusEntered -= ShowTip;
        FocusExited -= HideTip;
        HideTip();
        Unlink();
    }

    private static TextureRect Picture(string name, Texture2D texture, Vector2 position, Vector2 size) => new()
    {
        Name = name, Texture = texture, Position = position, Size = size,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };
}
