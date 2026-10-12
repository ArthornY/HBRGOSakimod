using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NHbrgoCardRewardPager : Control
{
    internal const string NodeName = "HbrgoRewardPager";

    private static readonly FieldInfo CardTweenField =
        PrivateAccess.Field<NCardRewardSelectionScreen>("_cardTween");
    private static readonly FieldInfo LastFocusedControlField =
        PrivateAccess.Field<NCardRewardSelectionScreen>("_lastFocusedControl");

    private NCardRewardSelectionScreen _screen;
    private Control _ui;
    private Control _row;
    private NGridCardHolder[] _holders = [];
    private Button _previous;
    private Button _next;
    private Label _pageLabel;
    private CardRewardPageLayout _layout;
    private bool _ownsLayout;

    public override void _Ready()
    {
        _ui = GetParent<Control>();
        _screen = _ui.GetParent<NCardRewardSelectionScreen>();
        _row = _ui.GetNode<Control>("CardRow");
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(260f, 44f);
        _previous = CreateButton("<", "上一页", 0f);
        _next = CreateButton(">", "下一页", 208f);
        _pageLabel = new Label
        {
            Position = new Vector2(52f, 0f),
            Size = new Vector2(156f, 44f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pageLabel.AddThemeFontSizeOverride("font_size", 24);
        AddChild(_pageLabel);
        _previous.Pressed += () => ChangePage(-1);
        _next.Pressed += () => ChangePage(1);
        _ui.Resized += OnUiResized;
        Hide();
    }

    internal void Configure(IReadOnlyList<CardCreationResult> options)
    {
        // Focus moves holders to the front; preserve reward order, not scene-tree order.
        List<NGridCardHolder> remaining = _row.GetChildren()
            .OfType<NGridCardHolder>()
            .Where(holder => !holder.IsQueuedForDeletion())
            .ToList();
        List<NGridCardHolder> ordered = new();
        foreach (CardCreationResult option in options)
        {
            NGridCardHolder holder = remaining.FirstOrDefault(candidate =>
                ReferenceEquals(candidate.CardModel, option.Card));
            if (holder != null)
            {
                ordered.Add(holder);
                remaining.Remove(holder);
            }
        }

        _holders = ordered.ToArray();
        _layout = CardRewardPageLayout.Create(_holders.Length, _ui.Size.X, 0);
        _ownsLayout = false;
        Relayout();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(_screen))
        {
            return;
        }

        int direction = inputEvent switch
        {
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.Pagedown } => 1,
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.Pageup } => -1,
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } => 1,
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } => -1,
            _ => 0
        };
        if (direction != 0)
        {
            ChangePage(direction);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_ui))
        {
            _ui.Resized -= OnUiResized;
        }
    }

    private Button CreateButton(string text, string tooltip, float x)
    {
        Button button = new()
        {
            Text = text,
            TooltipText = tooltip,
            Position = new Vector2(x, 0f),
            Size = new Vector2(52f, 44f),
            FocusMode = FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 26);
        AddChild(button);
        return button;
    }

    private void ChangePage(int direction)
    {
        CardRewardPageLayout next = CardRewardPageLayout.Create(
            _holders.Length, _ui.Size.X, _layout.PageIndex + direction);
        if (next.PageIndex != _layout.PageIndex)
        {
            _layout = next;
            ApplyPage();
        }
    }

    private void OnUiResized()
    {
        Callable.From(() =>
        {
            if (IsInsideTree() && !IsQueuedForDeletion())
            {
                Relayout();
            }
        }).CallDeferred();
    }

    private void Relayout()
    {
        int firstIndex = _layout.StartIndex;
        CardRewardPageLayout next = CardRewardPageLayout.Create(_holders.Length, _ui.Size.X, 0);
        _layout = CardRewardPageLayout.Create(_holders.Length, _ui.Size.X, firstIndex / next.PageSize);
        if (_layout.PageCount <= 1 && !_ownsLayout)
        {
            Hide();
            return;
        }

        // Stop the vanilla single-row tween before it writes off-screen positions again.
        (CardTweenField.GetValue(_screen) as Tween)?.Kill();
        _ownsLayout = true;
        ApplyPage();
    }

    private void ApplyPage()
    {
        Visible = _layout.PageCount > 1;
        Position = new Vector2(_row.Position.X - Size.X * 0.5f, _row.Position.Y + 220f);
        _previous.Disabled = _layout.PageIndex == 0;
        _next.Disabled = _layout.PageIndex == _layout.PageCount - 1;
        _pageLabel.Text = $"{_layout.PageIndex + 1} / {_layout.PageCount}";
        Control focused = GetViewport().GuiGetFocusOwner();
        NGridCardHolder[] visible = _holders.Skip(_layout.StartIndex).Take(_layout.VisibleCount).ToArray();
        for (int i = 0; i < _holders.Length; i++)
        {
            NGridCardHolder holder = _holders[i];
            int slot = i - _layout.StartIndex;
            bool onPage = slot >= 0 && slot < _layout.VisibleCount;
            holder.Visible = onPage;
            holder.FocusMode = onPage ? FocusModeEnum.All : FocusModeEnum.None;
            if (!onPage)
            {
                continue;
            }

            holder.Position = new Vector2(_layout.GetCardX(slot), 0f);
            holder.Modulate = Colors.White;
            holder.FocusNeighborLeft = visible[(slot + visible.Length - 1) % visible.Length].GetPath();
            holder.FocusNeighborRight = visible[(slot + 1) % visible.Length].GetPath();
            holder.FocusPrevious = holder.FocusNeighborLeft;
            holder.FocusNext = holder.FocusNeighborRight;
            Control pageButton = !_next.Disabled ? _next : _previous;
            holder.FocusNeighborBottom = Visible ? pageButton.GetPath() : holder.GetPath();
            holder.FocusNeighborTop = holder.GetPath();
        }

        if (visible.Length == 0)
        {
            return;
        }

        NGridCardHolder defaultCard = visible[visible.Length / 2];
        LastFocusedControlField.SetValue(_screen, defaultCard);
        foreach (Button button in new[] { _previous, _next })
        {
            button.FocusNeighborTop = defaultCard.GetPath();
            button.FocusNeighborBottom = defaultCard.GetPath();
            button.FocusPrevious = defaultCard.GetPath();
            button.FocusNext = defaultCard.GetPath();
            Button other = button == _previous ? _next : _previous;
            button.FocusNeighborLeft = other.Disabled ? defaultCard.GetPath() : other.GetPath();
            button.FocusNeighborRight = button.FocusNeighborLeft;
        }

        if (focused != null &&
            (!focused.IsVisibleInTree() || focused is Button { Disabled: true }))
        {
            defaultCard.GrabFocus();
        }
    }
}
