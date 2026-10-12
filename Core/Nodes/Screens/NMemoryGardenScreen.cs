using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NMemoryGardenScreen : Control, IOverlayScreen
{
    private const int CardsPerRow = 6;
    private const float GridCardPadding = 40f;
    private const float GridSideMargin = 48f;

    private readonly TaskCompletionSource<ModelId> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<MemoryGardenChoice> _choices = Array.Empty<MemoryGardenChoice>();
    private int _upgradePoints;
    private List<CardModel> _cards;
    private MemoryGardenChoice _selected;
    private Panel _panel;
    private Control _body;
    private NCardGrid _grid;
    private Label _title;
    private Label _subtitle;
    private Label _selectionTitle;
    private Label _selectionStatus;
    private Label _actionNotice;
    private Button _confirm;
    private Button _cancel;
    private Tween _fade;
    private bool _completed;
    private bool _pointerInteracted;

    // NCardGrid derives its column count from its unscaled scroll-container width.
    // Scale the whole grid so hover animations retain the same six-column layout.
    private static float GridWidth => CardsPerRow * NCard.defaultSize.X * NCardHolder.smallScale.X
        + (CardsPerRow - 1) * GridCardPadding + 2f * GridSideMargin;

    internal static IEnumerable<string> AssetPaths =>
        new[] { SceneHelper.GetScenePath("cards/card_grid") };

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => true;
    public Control DefaultFocusedControl => (Control)_cancel ?? this;

    internal static Task<ModelId> Choose(IReadOnlyList<MemoryGardenChoice> choices, int upgradePoints = 0)
    {
        if (NOverlayStack.Instance == null)
            return Task.FromResult<ModelId>(null);

        NMemoryGardenScreen screen = new()
        {
            Name = "MemoryGardenScreen",
            _choices = choices ?? Array.Empty<MemoryGardenChoice>(),
            _upgradePoints = Math.Max(0, upgradePoints)
        };
        NOverlayStack.Instance.Push(screen);
        return screen._completion.Task;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _cards = _choices.Select(choice => choice.PreviewCard).ToList();
        BuildUi();
        LayoutPanel();
        RefreshSelection();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Process(double delta)
    {
        // Vanilla grids rebuild/reuse their holders on resize and while scrolling.
        if (!_completed && IsVisibleInTree()) RefreshGridHolders();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (!_pointerInteracted && (input is InputEventMouseMotion motion && motion.Relative.LengthSquared() > 0f
            || input is InputEventMouseButton { Pressed: true }))
        {
            // Screen changes synthesize zero-motion mouse events. They must not
            // open card details before the player actually moves or clicks.
            _pointerInteracted = true;
            RefreshGridHolders();
        }
        if (input.IsActionPressed("ui_cancel") ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Complete(null);
        }
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(this) && input is InputEventKey)
            GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        _fade?.Kill();
        CancelPendingSelection();
    }

    public void AfterOverlayOpened()
    {
        NHoverTipSet.Clear();
        Modulate = Colors.Transparent;
        _fade = CreateTween();
        _fade.TweenProperty(this, "modulate:a", 1f, 0.18)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void AfterOverlayClosed()
    {
        _fade?.Kill();
        CancelPendingSelection();
        NHoverTipSet.Clear();
        this.QueueFreeSafely();
    }

    public void AfterOverlayShown() => Visible = true;
    public void AfterOverlayHidden() => Visible = false;

    private void CancelPendingSelection()
    {
        if (_completed) return;
        _completed = true;
        // Run cleanup is not the player's Return action. Cancellation must
        // propagate through deck-add hooks and stop the old rest-site action.
        _completion.TrySetCanceled();
    }

    private void BuildUi()
    {
        ColorRect background = new()
        {
            Color = new Color(0.018f, 0.022f, 0.028f, 0.90f),
            MouseFilter = MouseFilterEnum.Stop
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _panel = new Panel { ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", Box(new Color("171922"), new Color("554354"), 14));
        AddChild(_panel);
        _title = MakeLabel(Text("title"), 30, new Color("F0D8E9"));
        _panel.AddChild(_title);
        LocString subtitle = new("rest_site_ui", "HBR_MEMORY_GARDEN.subtitle");
        subtitle.Add("UpgradePoints", _upgradePoints);
        _subtitle = MakeLabel(subtitle.GetFormattedText(), 17, new Color("B9B6C1"));
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _panel.AddChild(_subtitle);

        _body = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        _panel.AddChild(_body);
        if (_cards.Count == 0)
        {
            Label empty = MakeLabel(Text("empty"), 22, new Color("CBC3D0"));
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            empty.OffsetLeft = 28;
            empty.OffsetRight = -28;
            _body.AddChild(empty);
        }
        else
        {
            _grid = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("cards/card_grid"))
                .Instantiate<NCardGrid>(PackedScene.GenEditState.Disabled);
            _grid.ClipContents = true;
            _grid.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            _grid.Size = new Vector2(GridWidth, 1f);
            Control scroll = _grid.GetNode<Control>("%ScrollContainer");
            scroll.SetAnchorAndOffset(Side.Left, 0f, GridSideMargin);
            scroll.SetAnchorAndOffset(Side.Right, 1f, -GridSideMargin);
            Control scrollbar = _grid.GetNode<Control>("Scrollbar");
            scrollbar.OffsetLeft = -36f;
            scrollbar.OffsetRight = -12f;
            _body.AddChild(_grid);
            _grid.Connect(NCardGrid.SignalName.HolderPressed, Callable.From<NCardHolder>(SelectCard));
            _grid.Connect(NCardGrid.SignalName.HolderAltPressed, Callable.From<NCardHolder>(InspectCard));
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(_grid) || !_grid.IsInsideTree() || _completed) return;
                _grid.SetCards(_cards, PileType.None, new List<SortingOrders> { SortingOrders.Ascending });
                RefreshGridHolders();
                if (ActiveScreenContext.Instance.IsCurrent(this)) _cancel.GrabFocus();
            }).CallDeferred();
        }

        _selectionTitle = MakeLabel(string.Empty, 20, new Color("F0D8E9"));
        _selectionTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _panel.AddChild(_selectionTitle);
        _selectionStatus = MakeLabel(string.Empty, 17, new Color("C6BFCB"));
        _selectionStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _panel.AddChild(_selectionStatus);
        _actionNotice = MakeLabel(Text("actionNotice"), 15, new Color("9D95A3"));
        _actionNotice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _panel.AddChild(_actionNotice);
        _cancel = MakeButton(Text("cancel"), false);
        _cancel.Pressed += () => Complete(null);
        _panel.AddChild(_cancel);
        _confirm = MakeButton(Text("confirm"), true);
        _confirm.Pressed += Confirm;
        _panel.AddChild(_confirm);
    }

    private void LayoutPanel()
    {
        if (_panel == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        Vector2 panelSize = new(Math.Max(1f, Math.Min(1200f, viewport.X - 32f)),
            Math.Max(1f, Math.Min(820f, viewport.Y - 48f)));
        _panel.Position = (viewport - panelSize) / 2f;
        _panel.Size = panelSize;
        float inset = panelSize.X < 640f ? 16f : 28f;
        float contentWidth = Math.Max(1f, panelSize.X - 2f * inset);
        bool narrow = panelSize.X < 760f;
        float footerHeight = narrow ? 190f : 150f;
        float bodyTop = narrow ? 122f : 110f;
        float bodyHeight = Math.Max(1f, panelSize.Y - bodyTop - footerHeight);
        Place(_title, inset, 20f, contentWidth, 40f);
        Place(_subtitle, inset, 66f, contentWidth, narrow ? 48f : 36f);
        Place(_body, inset, bodyTop, contentWidth, bodyHeight);
        if (_grid != null)
        {
            float scale = contentWidth / GridWidth;
            _grid.Scale = Vector2.One * scale;
            _grid.Size = new Vector2(GridWidth, bodyHeight / scale);
        }
        float footerTop = bodyTop + bodyHeight + 10f;
        Place(_selectionTitle, inset, footerTop, contentWidth, 28f);
        Place(_selectionStatus, inset, footerTop + 32f, contentWidth, narrow ? 48f : 40f);
        float buttonWidth = Math.Min(150f, (contentWidth - 12f) / 2f);
        float buttonY = Math.Max(footerTop + (narrow ? 110f : 82f), panelSize.Y - 62f);
        Place(_confirm, panelSize.X - inset - buttonWidth, buttonY, buttonWidth, 42f);
        Place(_cancel, panelSize.X - inset - 2f * buttonWidth - 12f, buttonY, buttonWidth, 42f);
        _actionNotice.Visible = _cards.Count > 0;
        Place(_actionNotice, inset, narrow ? footerTop + 82f : buttonY + 1f,
            narrow ? contentWidth : Math.Max(1f, contentWidth - 2f * buttonWidth - 32f), narrow ? 24f : 42f);
    }

    private void RefreshGridHolders()
    {
        if (_grid == null || _cancel == null || _confirm == null) return;
        NGridCardHolder[] holders = _grid.CurrentlyDisplayedCardHolders
            .Where(holder => holder.Visible && holder.CardNode != null && !holder.IsQueuedForDeletion()).ToArray();
        if (holders.Length == 0) return;

        NodePath firstPath = holders[0].GetPath();
        NodePath cancelPath = _cancel.GetPath();
        Control footer = _confirm.Disabled ? _cancel : _confirm;
        NodePath footerPath = footer.GetPath();
        foreach (NGridCardHolder holder in holders)
        {
            MemoryGardenChoice choice = _choices.FirstOrDefault(item => ReferenceEquals(item.PreviewCard, holder.CardModel));
            holder.Modulate = choice?.IsAvailable == false ? new Color(1f, 1f, 1f, 0.58f) : Colors.White;
            holder.Hitbox.SetEnabled(_pointerInteracted);
            int index = _cards.IndexOf(holder.CardModel);
            if (index >= (_cards.Count - 1) / CardsPerRow * CardsPerRow)
                holder.FocusNeighborBottom = footerPath;
        }

        // Start on Return, but keep the grid reachable with keyboard/controller.
        _cancel.FocusNeighborTop = firstPath;
        _cancel.FocusNext = firstPath;
        _confirm.FocusNeighborTop = firstPath;
        holders[0].FocusPrevious = cancelPath;
        holders[^1].FocusNext = footerPath;
    }

    private void SelectCard(NCardHolder holder)
    {
        if (_completed) return;
        MemoryGardenChoice choice = _choices.FirstOrDefault(item => ReferenceEquals(item.PreviewCard, holder.CardModel));
        if (choice == null) return;
        if (_selected != null) _grid.UnhighlightCard(_selected.PreviewCard);
        _selected = choice;
        _grid.HighlightCard(choice.PreviewCard);
        RefreshSelection();
    }

    private void InspectCard(NCardHolder holder)
    {
        int index = _cards.IndexOf(holder.CardModel);
        if (!_completed && index >= 0)
            NGame.Instance?.GetInspectCardScreen().Open(_cards, index);
    }

    private void RefreshSelection()
    {
        _confirm.Disabled = _selected?.IsAvailable != true;
        _confirm.Text = _selected == null ? Text("confirm") : Text(_selected.IsUpgrade ? "upgrade" : "obtain");
        _selectionTitle.Text = _selected == null ? Text("choosePrompt") :
            _selected.CharacterName + " · " + _selected.PreviewCard.Title;
        _selectionStatus.Text = _selected?.StatusText ?? string.Empty;
        if (_cards.Count == 0)
        {
            _selectionTitle.Text = string.Empty;
            _confirm.Visible = false;
            _actionNotice.Visible = false;
        }
    }

    private void Confirm()
    {
        if (_selected?.IsAvailable == true) Complete(_selected.PreviewCard.Id);
    }

    private void Complete(ModelId selectedId)
    {
        if (_completed) return;
        _completed = true;
        NHoverTipSet.Clear();
        NOverlayStack.Instance?.Remove(this);
        _completion.TrySetResult(selectedId);
    }

    private static string Text(string key) =>
        new LocString("rest_site_ui", "HBR_MEMORY_GARDEN." + key).GetFormattedText();

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(1f, width), Math.Max(1f, height));
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeButton(string text, bool accent)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        Color edge = accent ? new Color("C879AF") : new Color("5E5568");
        button.AddThemeFontSizeOverride("font_size", 20);
        button.AddThemeColorOverride("font_color", new Color("F4EAF2"));
        button.AddThemeStyleboxOverride("normal", Box(accent ? new Color("54344F") : new Color("282430"), edge, 7));
        button.AddThemeStyleboxOverride("hover", Box(new Color("69465F"), new Color("E5ACD1"), 7));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("422C42"), edge, 7));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0f, 0f, 0f, 0f), new Color("EDD1E6"), 7));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("211F28"), new Color("3C3743"), 7));
        return button;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };
}
