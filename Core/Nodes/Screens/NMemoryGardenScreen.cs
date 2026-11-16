using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
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

// The memory garden in the HBR style (ui-concepts/memory-garden-20261104): a white space over shallow water,
// the memories in a native card grid with status pills and reflections, characters not met yet as sealed cards.
public partial class NMemoryGardenScreen : Control, IOverlayScreen
{
    private const int CardsPerRow = 6;
    private const float GridCardPadding = 40f;
    private const float GridSideMargin = 48f;
    private static readonly Rect2 GridArea = new(96, 250, 1728, 674);

    private readonly TaskCompletionSource<ModelId> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<MemoryGardenChoice> _choices = Array.Empty<MemoryGardenChoice>();
    private int _upgradePoints;
    private List<CardModel> _cards;
    private HashSet<CardModel> _ghosts = new();
    // Previews of characters met since the garden last showed them: their silhouettes drain away once, staggered,
    // after the screen has faded in.
    private const double MeetDelay = .45;
    private const double MeetStagger = .15;
    private HashSet<CardModel> _meetPending = new();
    private int _meetsStarted;
    private MemoryGardenChoice _selected;
    private NMemoryGardenBackdrop _backdrop;
    private Control _rig;
    private Control _body;
    private NCardGrid _grid;
    private Label _title;
    private Label _titleEn;
    private NinePatchRect _titleLine;
    private Label _subtitle;
    private HBoxContainer _chips;
    private NMemoryGardenRoundButton _back;
    private NMemoryGardenRoundButton _close;
    private Panel _footer;
    private Label _selectionTitle;
    private Label _selectionStatus;
    private Label _actionNotice;
    private Button _confirm;
    private Button _cancel;
    private Tween _fade;
    private bool _completed;
    private bool _sublimating;
    private bool _pointerInteracted;
    private bool _horizonPlaced;

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
        IReadOnlyList<CardModel> unmet = HbrMemoryGarden.GetUnmetCards(_choices);
        _ghosts = unmet.ToHashSet();
        _cards = _choices.Select(choice => choice.PreviewCard).Concat(unmet).ToList();
        // None of a name has come back before the card is obtained (HbrMemoryTitleState).
        foreach (MemoryGardenChoice choice in _choices.Where(choice => !choice.IsUpgrade))
            HbrMemoryTitleState.MarkUnowned(choice.PreviewCard);
        Player player = _choices.Select(choice => choice.PreviewCard.Owner).FirstOrDefault(owner => owner != null);
        IReadOnlySet<string> newlyMet = HbrMemoryGardenShownStore.TakeNewlyShown(player,
            HbrMemoryGarden.GetMetCharacterIds(player));
        _meetPending = _choices.Select(choice => choice.PreviewCard)
            .Where(card => HbrMemoryGarden.CharacterIdOf(card) is { } id && newlyMet.Contains(id)).ToHashSet();
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
            if (!_sublimating) Complete(null);
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
        _backdrop = new NMemoryGardenBackdrop();
        AddChild(_backdrop);
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rig = new Control { MouseFilter = MouseFilterEnum.Pass, Size = HbrMemoryGardenTheme.Rig };
        AddChild(_rig);

        _back = NMemoryGardenRoundButton.Back();
        _back.Pressed += () => { if (!_sublimating) Complete(null); };
        _rig.AddChild(_back);
        _title = HbrMemoryGardenTheme.Label(Text("title"), 48, HbrMemoryGardenTheme.Ink);
        _rig.AddChild(_title);
        _titleEn = HbrMemoryGardenTheme.Label("MEMORY GARDEN", 20, HbrMemoryGardenTheme.Ink2);
        _rig.AddChild(_titleEn);
        _titleLine = new NinePatchRect
        {
            Texture = HbrMemoryGardenTheme.Sprite("MordaTitleLineEdge"),
            PatchMarginLeft = 2, PatchMarginRight = 55,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _rig.AddChild(_titleLine);
        LocString subtitle = new("rest_site_ui", "HBR_MEMORY_GARDEN.subtitle");
        subtitle.Add("UpgradePoints", _upgradePoints);
        _subtitle = HbrMemoryGardenTheme.Label(subtitle.GetFormattedText(), 21, HbrMemoryGardenTheme.Ink2);
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _rig.AddChild(_subtitle);

        _chips = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _chips.AddThemeConstantOverride("separation", 12);
        var pointsIcon = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://images/ui/memory_garden.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _chips.AddChild(HbrMemoryGardenTheme.Chip(pointsIcon, "升级点", out Label points));
        points.Text = _upgradePoints.ToString();
        if (_upgradePoints == 0) points.AddThemeColorOverride("font_color", HbrMemoryGardenTheme.Ink3);
        _chips.AddChild(HbrMemoryGardenTheme.Chip(new NMemoryGardenFlowerIcon(), "记忆", out Label met));
        met.Text = $"{_choices.Count}/{HbrMemoryGarden.CharacterCount}";
        _rig.AddChild(_chips);
        _close = NMemoryGardenRoundButton.Close();
        _close.Pressed += () => { if (!_sublimating) Complete(null); };
        _rig.AddChild(_close);

        _body = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        _rig.AddChild(_body);
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

        _footer = new Panel { MouseFilter = MouseFilterEnum.Pass };
        _footer.AddThemeStyleboxOverride("panel", HbrMemoryGardenTheme.GlassBox(22));
        _rig.AddChild(_footer);
        _selectionTitle = HbrMemoryGardenTheme.Label(string.Empty, 26, HbrMemoryGardenTheme.Ink);
        _selectionTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _footer.AddChild(_selectionTitle);
        _selectionStatus = HbrMemoryGardenTheme.Label(string.Empty, 20, HbrMemoryGardenTheme.Ink2);
        _selectionStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _footer.AddChild(_selectionStatus);
        _actionNotice = HbrMemoryGardenTheme.Label(Text("actionNotice"), 17, HbrMemoryGardenTheme.Ink3);
        _actionNotice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _footer.AddChild(_actionNotice);
        _cancel = HbrMemoryGardenTheme.HbrButton(Text("cancel"), sub: true);
        _cancel.Pressed += () => { if (!_sublimating) Complete(null); };
        _footer.AddChild(_cancel);
        _confirm = HbrMemoryGardenTheme.HbrButton(Text("confirm"));
        _confirm.Pressed += Confirm;
        _footer.AddChild(_confirm);
    }

    // Everything sits on the preview's 1920x1080 rig, centred in the screen.
    private void LayoutPanel()
    {
        if (_rig == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        _rig.Position = HbrMemoryGardenTheme.Origin(viewport);
        _rig.Size = HbrMemoryGardenTheme.Rig;
        Place(_back, 40, 96, 104, 104);
        Place(_title, 168, 92, 700, 64);
        Place(_titleEn, 172, 158, 700, 28);
        Place(_titleLine, 168, 190, 720, 10);
        Place(_subtitle, 170, 210, 1180, 34);
        Vector2 chips = _chips.GetCombinedMinimumSize();
        Place(_chips, 1920 - 170 - chips.X, 112, chips.X, 52);
        Place(_close, 1920 - 40 - 104, 96, 104, 104);
        Place(_body, GridArea.Position.X, GridArea.Position.Y, GridArea.Size.X, GridArea.Size.Y);
        float scale = GridArea.Size.X / GridWidth;
        _grid.Scale = Vector2.One * scale;
        _grid.Size = new Vector2(GridWidth, GridArea.Size.Y / scale);
        Place(_footer, 60, 932, 1800, 136);
        Place(_selectionTitle, 28, 16, 1000, 36);
        Place(_selectionStatus, 28, 58, 1000, 32);
        Place(_actionNotice, 28, 96, 1000, 28);
        Place(_cancel, 1800 - 380 - 330, 2, 330, 132);
        Place(_confirm, 1800 - 30 - 330, 2, 330, 132);
        _actionNotice.Visible = _choices.Count > 0;
        _horizonPlaced = false;
        _backdrop.Horizon = _rig.Position.Y + 551;
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
            bool ghost = _ghosts.Contains(holder.CardModel);
            MemoryGardenChoice choice = ghost ? null : _choices.FirstOrDefault(item => ReferenceEquals(item.PreviewCard, holder.CardModel));
            NMemoryGardenCardDecor decor = Decor(holder);
            decor.Display(SlotOf(choice), ghost ? "尚未遇见" : SlotLabel(choice));
            if (_meetPending.Remove(holder.CardModel)) decor.PlayMeet(MeetDelay + MeetStagger * _meetsStarted++);
            // Sealed memories are covered by static instead of the old grey-out.
            holder.Modulate = Colors.White;
            // A character not met yet stays face down: no hover, details or focus.
            holder.Hitbox.SetEnabled(_pointerInteracted && !ghost);
            holder.FocusMode = ghost ? FocusModeEnum.None : FocusModeEnum.All;
            int index = _cards.IndexOf(holder.CardModel);
            if (index >= (_choices.Count - 1) / CardsPerRow * CardsPerRow)
                holder.FocusNeighborBottom = footerPath;
        }
        PlaceHorizon(holders[0]);

        // Start on Return, but keep the grid reachable with keyboard/controller.
        NGridCardHolder[] focusable = holders.Where(holder => !_ghosts.Contains(holder.CardModel)).ToArray();
        if (focusable.Length == 0) return;
        firstPath = focusable[0].GetPath();
        _cancel.FocusNeighborTop = firstPath;
        _cancel.FocusNext = firstPath;
        _confirm.FocusNeighborTop = firstPath;
        focusable[0].FocusPrevious = cancelPath;
        focusable[^1].FocusNext = footerPath;
    }

    // The horizon lines up with the feet of the first row once the grid has laid it out.
    private void PlaceHorizon(NGridCardHolder first)
    {
        if (_horizonPlaced || first.CardNode.GetNodeOrNull<Control>("%Frame") is not { } frame) return;
        _horizonPlaced = true;
        Rect2 rect = _backdrop.GetGlobalTransform().AffineInverse() * frame.GetGlobalRect();
        _backdrop.Horizon = rect.End.Y + 3;
    }

    private static NMemoryGardenCardDecor Decor(NGridCardHolder holder)
    {
        var decor = holder.GetNodeOrNull<NMemoryGardenCardDecor>("MemoryGardenDecor");
        if (decor != null && !decor.IsQueuedForDeletion()) return decor;
        // A decor frees itself when its holder leaves the tree. If the holder is back within the same frame,
        // take the old one off now so the new one gets its name.
        if (decor != null) holder.RemoveChild(decor);
        decor = new NMemoryGardenCardDecor { Holder = holder };
        holder.AddChild(decor);
        return decor;
    }

    private static MemoryGardenSlot SlotOf(MemoryGardenChoice choice) => choice switch
    {
        null => MemoryGardenSlot.Ghost,
        { IsComplete: true } => MemoryGardenSlot.Complete,
        { IsAvailable: false } => MemoryGardenSlot.Sealed,
        { IsUpgrade: true } => MemoryGardenSlot.Upgrade,
        _ => MemoryGardenSlot.Obtain
    };

    private static string SlotLabel(MemoryGardenChoice choice) => SlotOf(choice) switch
    {
        MemoryGardenSlot.Complete => "已全部升级",
        MemoryGardenSlot.Sealed => "升级点不足",
        MemoryGardenSlot.Upgrade => "可升级",
        _ => "可获得"
    };

    private void SelectCard(NCardHolder holder)
    {
        if (_completed || _sublimating || _ghosts.Contains(holder.CardModel)) return;
        MemoryGardenChoice choice = _choices.FirstOrDefault(item => ReferenceEquals(item.PreviewCard, holder.CardModel));
        if (choice == null) return;
        if (_selected != null) _grid.UnhighlightCard(_selected.PreviewCard);
        _selected = choice;
        _grid.HighlightCard(choice.PreviewCard);
        // Anon's memory brings cherry blossom; everyone else the nemophila of the present.
        _backdrop.Sakura = choice.PreviewCard is AnonAidHbrgoSaki;
        RefreshSelection();
    }

    private void InspectCard(NCardHolder holder)
    {
        int index = _cards.IndexOf(holder.CardModel);
        if (!_completed && !_sublimating && index >= 0 && !_ghosts.Contains(holder.CardModel))
            NGame.Instance?.GetInspectCardScreen().Open(_cards.Take(_choices.Count).ToList(), index);
    }

    private void RefreshSelection()
    {
        _confirm.Disabled = _selected?.IsAvailable != true;
        _confirm.Text = _selected == null ? Text("confirm") : Text(_selected.IsUpgrade ? "upgrade" : "obtain");
        _selectionTitle.Text = _selected == null ? Text("choosePrompt") :
            $"{_selected.CharacterName} · 「{HbrMemoryTitleState.Title(_selected.PreviewCard)}」";
        _selectionStatus.Text = _selected?.StatusText ?? string.Empty;
        if (_choices.Count == 0)
        {
            // With no one met yet, the old empty-list hint moves down here; the grid shows sealed cards.
            _selectionTitle.Text = Text("empty");
            _confirm.Visible = false;
            _actionNotice.Visible = false;
        }
    }

    private async void Confirm()
    {
        if (_sublimating || _selected?.IsAvailable != true) return;
        ModelId id = _selected.PreviewCard.Id;
        // A new card is received with white light and a rainbow; upgrades go on to the sublimation screen.
        if (!_selected.IsUpgrade && FindHolder(_selected.PreviewCard) is { } holder &&
            holder.CardNode.GetNodeOrNull<Control>("%Frame") is { } frame)
        {
            _sublimating = true;
            _confirm.Disabled = true;
            // The first part of the name decodes in the white light.
            HbrMemoryTitleState.MarkOwned(_selected.PreviewCard);
            NHbrCardStyle.Of(holder.CardNode)?.PlayTitleReveal(0);
            await NMemoryGardenSublimation.Play(this, holder.CardNode, frame, shatter: false);
            if (!GodotObject.IsInstanceValid(this)) return;
        }
        Complete(id);
    }

    private NGridCardHolder FindHolder(CardModel card) =>
        _grid?.CurrentlyDisplayedCardHolders.FirstOrDefault(holder => ReferenceEquals(holder.CardModel, card));

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
}
