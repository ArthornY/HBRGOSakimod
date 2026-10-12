using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// Use the native label's hidden scroll range so BBCode, inline icons, automatic
// font sizing, alignment and the card's existing clipping area stay intact.
public partial class NHbrCardDescriptionScroll : Node
{
    private static readonly AccessTools.FieldRef<NCardHolder, bool> IsHovered =
        AccessTools.FieldRefAccess<NCardHolder, bool>("_isHovered");
    private static readonly AccessTools.FieldRef<NGridCardHolder, bool> IsPreviewingUpgrade =
        AccessTools.FieldRefAccess<NGridCardHolder, bool>("_isPreviewingUpgrade");
    private static readonly AccessTools.FieldRef<NCardGrid, List<CardModel>> HighlightedCards =
        AccessTools.FieldRefAccess<NCardGrid, List<CardModel>>("_highlightedCards");

    public NCard Card { get; set; }

    private CardModel _model;
    private bool _supported;
    private MegaRichTextLabel _description;
    private VScrollBar _scrollBar;
    private Node _readingContext;
    private string _text;
    private Vector2 _labelSize;
    private bool _scrolling;
    private double _originalStep;
    private double _maxOffset;
    private double _elapsed;

    public override void _Ready() => Card ??= GetParent() as NCard;

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(Card) || Card.IsQueuedForDeletion() ||
            !Card.IsNodeReady() || !Card.IsVisibleInTree() ||
            Card.Visibility != ModelVisibility.Visible)
        {
            StopReading();
            return;
        }

        if (!ReferenceEquals(_model, Card.Model))
        {
            StopReading();
            _model = Card.Model;
            _supported = _model != null && HBRGOSakiContentPack.IsRegisteredCardType(_model.GetType());
        }
        if (!_supported)
            return;

        Node context = FindReadingContext();
        if (context == null)
        {
            StopReading();
            return;
        }

        if (!GodotObject.IsInstanceValid(_description))
        {
            StopReading();
            _description = Card.GetNodeOrNull<MegaRichTextLabel>("%DescriptionLabel");
            if (_description == null)
                return;
            _scrollBar = _description.GetVScrollBar();
        }

        string text = _description.Text;
        Vector2 labelSize = _description.Size;
        if (_readingContext != context || _text != text || _labelSize != labelSize)
        {
            ResetScroll();
            _readingContext = context;
            _text = text;
            _labelSize = labelSize;
            // MegaRichTextLabel adjusts the font in a deferred call. Let that
            // finish before deciding whether the final rendered text overflows.
            return;
        }
        if (!_description.IsFinished() || labelSize.Y <= 0)
            return;

        double maxOffset = Math.Max(0, _scrollBar.MaxValue - _scrollBar.Page - _scrollBar.MinValue);
        if (maxOffset <= 1)
        {
            ResetScroll();
            return;
        }
        if (!_scrolling || Math.Abs(_maxOffset - maxOffset) > 0.5)
        {
            ResetScroll();
            _originalStep = _scrollBar.Step;
            _scrollBar.Step = 0;
            _maxOffset = maxOffset;
            _scrolling = true;
            return;
        }

        // Do not jump past a reading pause after a stalled frame or tab switch.
        _elapsed += Math.Clamp(delta, 0, 0.1);
        _scrollBar.Value = _scrollBar.MinValue + CardDescriptionScrollTiming.GetOffset(_elapsed, _maxOffset);
    }

    private Node FindReadingContext()
    {
        NCardHolder holder = null;
        for (Node ancestor = Card.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor is NInspectCardScreen inspect)
                return inspect.MouseFilter == Control.MouseFilterEnum.Ignore ? null : inspect;
            if (ancestor is NMemoryGardenUpgradeScreen)
                return ancestor;
            if (ancestor is NCardHolder cardHolder)
            {
                holder = cardHolder;
                if (holder is NPreviewCardHolder or NSelectedHandCardHolder || IsHovered(holder) ||
                    holder.HasFocus() || (holder is NGridCardHolder gridHolder && IsPreviewingUpgrade(gridHolder)))
                    return holder;
                // While aiming a hand card, its holder is moved directly under
                // NPlayerHand and may no longer retain mouse/controller focus.
                if (holder is NHandCardHolder && holder.GetParent() is NPlayerHand hand && hand.InCardPlay)
                    return holder;
            }
            if (ancestor is NCardGrid grid)
                return holder != null && HighlightedCards(grid).Contains(holder.CardModel) ? holder : null;
        }
        return null;
    }

    private void ResetScroll()
    {
        if (_scrolling && GodotObject.IsInstanceValid(_scrollBar))
        {
            _scrollBar.Value = _scrollBar.MinValue;
            _scrollBar.Step = _originalStep;
        }
        _scrolling = false;
        _elapsed = 0;
        _maxOffset = 0;
    }

    private void StopReading()
    {
        ResetScroll();
        _readingContext = null;
        _text = null;
    }

    public override void _ExitTree()
    {
        // Card and holder nodes are pooled and can be reparented without _Ready
        // running again. Never carry an offset or reading timer into their reuse.
        StopReading();
        _model = null;
        _supported = false;
    }
}

internal static class CardDescriptionScrollTiming
{
    internal const double PauseSeconds = 3;
    internal const double PixelsPerSecond = 10;

    internal static double GetOffset(double elapsed, double distance)
    {
        if (distance <= 0 || elapsed <= 0)
            return 0;
        double cycleSeconds = PauseSeconds * 2 + distance / PixelsPerSecond;
        double cycleTime = elapsed % cycleSeconds;
        return Math.Clamp((cycleTime - PauseSeconds) * PixelsPerSecond, 0, distance);
    }
}
