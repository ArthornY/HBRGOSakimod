using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NCardDamageHistoryScreen : Control, IOverlayScreen
{
    private const int PageSize = 50;
    private static NCardDamageHistoryScreen _active;
    private Player _player;
    private PanelContainer _panel;
    private VBoxContainer _rows;
    private ScrollContainer _scroll;
    private Label _summary;
    private Label _pageLabel;
    private Button _previous;
    private Button _next;
    private Button _close;
    private int _pageIndex;
    private CardDamageHistoryEntry _pendingEntry;
    private Label _pendingDamage;
    private decimal _pendingValue = -1m;

    public NetScreenType ScreenType => NetScreenType.Room;
    public bool UseSharedBackstop => true;
    public Control DefaultFocusedControl => (Control)_close ?? this;

    public static void Show(Player player)
    {
        if (player == null || NOverlayStack.Instance == null) return;
        if (GodotObject.IsInstanceValid(_active) && !_active.IsQueuedForDeletion() && _active.IsInsideTree())
        {
            _active._player = player;
            _active.RenderPage(0);
            return;
        }
        _active = new NCardDamageHistoryScreen { Name = "CardDamageHistoryScreen", _player = player };
        NOverlayStack.Instance.Push(_active);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi();
        LayoutPanel();
        CardDamageHistoryHelper.HistoryChanged += OnHistoryChanged;
        CardDamageHistoryHelper.DamageUpdated += OnDamageUpdated;
        RenderPage(0);
    }

    public override void _ExitTree()
    {
        CardDamageHistoryHelper.HistoryChanged -= OnHistoryChanged;
        CardDamageHistoryHelper.DamageUpdated -= OnDamageUpdated;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Process(double delta)
    {
        if (_pendingEntry != null && _pendingDamage != null && _pendingValue != _pendingEntry.TotalDamage)
        {
            _pendingValue = _pendingEntry.TotalDamage;
            HbrCardDamageHistoryVisuals.SetDamageText(_pendingDamage, NCardDamageHistoryWidget.FormatDamage(_pendingValue), 28);
        }
    }

    public void AfterOverlayOpened() { }
    public void AfterOverlayShown() => Visible = true;
    public void AfterOverlayHidden() => Visible = false;
    public void AfterOverlayClosed()
    {
        CardDamageHistoryHelper.HistoryChanged -= OnHistoryChanged;
        CardDamageHistoryHelper.DamageUpdated -= OnDamageUpdated;
        if (ReferenceEquals(_active, this)) _active = null;
        this.QueueFreeSafely();
    }

    private void BuildUi()
    {
        ColorRect backdrop = new() { Color = new Color(0.03f, 0.01f, 0.035f, 0.84f), MouseFilter = MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        _panel = new PanelContainer { Name = "CardDamageHistoryPanel", MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("160F1CF5"), BorderColor = new Color("C692BCE6"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 24f, ContentMarginRight = 24f, ContentMarginTop = 20f, ContentMarginBottom = 20f
        });
        AddChild(_panel);
        VBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(layout);
        Label title = HbrCardDamageHistoryVisuals.Label("每张牌总伤害", 28, new Color("FFCCF5"));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(title);
        _summary = HbrCardDamageHistoryVisuals.Label(string.Empty, 17, new Color("DEC9DF"));
        _summary.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(_summary);
        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        layout.AddChild(_scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(_rows);
        HBoxContainer navigation = new();
        navigation.AddThemeConstantOverride("separation", 8);
        layout.AddChild(navigation);
        PageButton(navigation, "最新", () => RenderPage(0));
        _previous = PageButton(navigation, "上一页", () => RenderPage(_pageIndex - 1));
        _pageLabel = HbrCardDamageHistoryVisuals.Label(string.Empty, 17, new Color("FFCCF5"));
        _pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _pageLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        navigation.AddChild(_pageLabel);
        _next = PageButton(navigation, "下一页", () => RenderPage(_pageIndex + 1));
        PageButton(navigation, "最早", () => RenderPage(int.MaxValue));
        PageButton(navigation, "刷新", () => RenderPage(_pageIndex));
        _close = PageButton(layout, "关闭", Close);
        _close.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _close.CustomMinimumSize = new Vector2(140f, 42f);
    }

    private static Button PageButton(Node parent, string text, Action action)
    {
        Button button = new() { Text = text, CustomMinimumSize = new Vector2(74f, 38f), FocusMode = FocusModeEnum.All };
        button.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeColorOverride("font_color", new Color("FFCCF5"));
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = new Color(state == "hover" ? "54324F" : "261B2B"),
                BorderColor = new Color("AB7CA4"), BorderWidthLeft = 1, BorderWidthRight = 1,
                BorderWidthTop = 1, BorderWidthBottom = 1,
                ContentMarginLeft = 8, ContentMarginRight = 8
            });
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void OnHistoryChanged(Player player)
    {
        if (ReferenceEquals(player, _player)) RenderPage(_pageIndex);
    }

    private void OnDamageUpdated(Player player, CardDamageHistoryEntry entry)
    {
        if (!ReferenceEquals(player, _player) || !ReferenceEquals(entry, _pendingEntry) ||
            _pendingDamage == null) return;
        _pendingValue = entry.TotalDamage;
        HbrCardDamageHistoryVisuals.SetDamageText(_pendingDamage,
            NCardDamageHistoryWidget.FormatDamage(_pendingValue), 28);
    }

    private void RenderPage(int requestedPage)
    {
        if (_rows == null) return;
        var entries = CardDamageHistoryHelper.GetEntries(_player);
        int pageCount = Math.Max(1, (int)((entries.Count + (long)PageSize - 1) / PageSize));
        _pageIndex = Math.Clamp(requestedPage, 0, pageCount - 1);
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFreeSafely();
        }
        _pendingEntry = null;
        _pendingDamage = null;
        int newestIndex = entries.Count - 1 - _pageIndex * PageSize;
        int oldestIndex = Math.Max(0, newestIndex - PageSize + 1);
        if (entries.Count == 0)
            _rows.AddChild(HbrCardDamageHistoryVisuals.Label("本场尚未打出卡牌", 20, new Color("DEC9DF")));
        else
            for (int index = newestIndex; index >= oldestIndex; index--) AddRow(entries[index]);
        _summary.Text = $"本场共 {entries.Count} 次打牌 · 包含 0 伤害记录 · 先显示最新记录";
        _pageLabel.Text = $"{_pageIndex + 1} / {pageCount}";
        _previous.Disabled = _pageIndex == 0;
        _next.Disabled = _pageIndex == pageCount - 1;
        _scroll.ScrollVertical = 0;
    }

    private void AddRow(CardDamageHistoryEntry entry)
    {
        HBoxContainer row = new() { CustomMinimumSize = new Vector2(0f, 74f) };
        row.AddThemeConstantOverride("separation", 12);
        _rows.AddChild(row);
        Label name = HbrCardDamageHistoryVisuals.Label($"{entry.Sequence}.  {entry.CardName}", 19, new Color("FFF1FA"));
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.TooltipText = entry.CardName;
        name.MouseFilter = MouseFilterEnum.Pass;
        row.AddChild(name);
        Control amount = new() { CustomMinimumSize = new Vector2(300f, 74f), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(amount);
        amount.AddChild(HbrCardDamageHistoryVisuals.Sprite("DamageDot", "DotTotalDamage", new(0f, 20f), new(39.1f, 40.8f)));
        amount.AddChild(HbrCardDamageHistoryVisuals.Sprite("DamageLine", "EffectLineTotalDamage", new(28f, 34f), new(260f, 13.6f)));
        Label total = HbrCardDamageHistoryVisuals.Label("TOTAL", 14, Colors.White, HbrCardDamageHistoryVisuals.CaptionFont);
        total.Position = new Vector2(16f, 0f); total.Size = new Vector2(90f, 24f);
        amount.AddChild(total);
        if (!entry.IsComplete)
        {
            Label pending = HbrCardDamageHistoryVisuals.Label("结算中", 14, new Color("FFCCF5"));
            pending.Position = new Vector2(120f, 0f); pending.Size = new Vector2(160f, 24f);
            pending.HorizontalAlignment = HorizontalAlignment.Right;
            amount.AddChild(pending);
        }
        Label damage = HbrCardDamageHistoryVisuals.Label(string.Empty, 28, Colors.White, HbrCardDamageHistoryVisuals.NumberFont);
        damage.Position = new Vector2(16f, 22f); damage.Size = new Vector2(280f, 48f);
        amount.AddChild(damage);
        HbrCardDamageHistoryVisuals.SetDamageText(damage, NCardDamageHistoryWidget.FormatDamage(entry.TotalDamage), 28);
        if (!entry.IsComplete && _pendingEntry == null)
        {
            _pendingEntry = entry;
            _pendingDamage = damage;
            _pendingValue = entry.TotalDamage;
        }
    }

    private void LayoutPanel()
    {
        if (_panel == null) return;
        Vector2 viewport = GetViewportRect().Size;
        _panel.Size = new Vector2(Math.Max(300f, Math.Min(940f, viewport.X - 64f)),
            Math.Max(300f, Math.Min(760f, viewport.Y - 64f)));
        _panel.Position = (viewport - _panel.Size) / 2f;
    }

    private void Close() => NOverlayStack.Instance?.Remove(this);
}
