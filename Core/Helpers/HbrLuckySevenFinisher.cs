using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

// Sequences Magic Sword Lucky Seven's finisher around the card's real resolution (design v3, section 6):
// the summon plays before the autoplay, the real hits draw the slashes, and 收刀 shows the combined total
// once the card has resolved. Presentation only. It never changes the damage, the hit count, the targets,
// any random number or the order of effects, and each machine picks its own show (a teammate's finisher
// is at most simplified), so peers resolve the same card whatever they see.
internal sealed class HbrLuckySevenFinisher : IDisposable
{
    private static HbrLuckySevenFinisher _active;

    private readonly CardModel _card;
    private readonly Player _owner;
    private readonly int _hits;
    private readonly HbrFinisherPresentation _mode;
    private readonly List<(CardDamageHistoryEntry Entry, decimal Total)> _before = new();
    private NHbrLuckySevenFinisher _view;
    private int _seenHits;
    private decimal _observed;
    private Vector2 _anchorSum;
    private int _anchorCount;
    private bool _disposed;

    private HbrLuckySevenFinisher(CardModel card, Player owner, int hits, HbrFinisherPresentation mode)
    {
        _card = card;
        _owner = owner;
        _hits = hits;
        _mode = mode;
    }

    internal HbrFinisherPresentation Mode => _mode;

    internal static HbrLuckySevenFinisher Begin(Creature owner, CardModel card, int hits)
    {
        Player player = owner?.Player;
        try
        {
            NCombatRoom room = NCombatRoom.Instance;
            // No show without a screen to show it on, or with nothing to show (zero hits deal no damage).
            bool interactive = !TestMode.IsOn && !NonInteractiveMode.IsActive && hits > 0 && player != null &&
                card != null && room != null && GodotObject.IsInstanceValid(room) && room.IsInsideTree();
            FastModeType fast = interactive ? SaveManager.Instance.PrefsSave.FastMode : FastModeType.Normal;
            HbrFinisherPresentation mode = HbrLuckySevenTimeline.Resolve(
                HBRGOSakiConfig.FinisherPresentation,
                interactive && LocalContext.IsMe(player),
                interactive,
                fast == FastModeType.Instant);
            HbrLuckySevenFinisher show = new(card, player, hits, mode);
            if (mode == HbrFinisherPresentation.Off) return show;

            show.SnapshotHistory();
            float speed = fast == FastModeType.Fast ? HbrLuckySevenTimeline.FastSpeed : 1f;
            show._view = NHbrLuckySevenFinisher.Create(mode, speed, CurrentPortrait(room, owner));
            room.AddChild(show._view);
            _active?.Dispose();
            _active = show;
            return show;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Lucky Seven finisher unavailable: {error.Message}");
            return new HbrLuckySevenFinisher(card, player, hits, HbrFinisherPresentation.Off);
        }
    }

    // 暗场 and 召剑. Only the full-screen show holds the card, at most MaxIntroWait.
    internal async Task BeforeHits()
    {
        if (!GodotObject.IsInstanceValid(_view)) return;
        if (_mode == HbrFinisherPresentation.Fullscreen)
            await WaitFor(() => _view.IntroDone, HbrLuckySevenTimeline.MaxIntroWait);
        if (GodotObject.IsInstanceValid(_view)) _view.BeginHits();
    }

    // 收刀 after the card resolved: the flash and the combined total. Only the full-screen show waits.
    internal async Task AfterHits()
    {
        if (!GodotObject.IsInstanceValid(_view)) return;
        try
        {
            decimal total = TotalDamage();
            Vector2? anchor = _anchorCount > 0 ? _anchorSum / _anchorCount : null;
            _view.Finish(total, anchor, _seenHits > 0 || total > 0m);
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Lucky Seven total failed: {error.Message}");
        }

        if (_mode == HbrFinisherPresentation.Fullscreen && GodotObject.IsInstanceValid(_view))
            await WaitFor(() => _view.FinishDone, HbrLuckySevenTimeline.MaxFinishWait);
    }

    // From the AfterDamageReceived observer: every hit of the card being shown draws its slash.
    internal static void ObserveHit(Creature target, DamageResult result, CardModel cardSource)
    {
        HbrLuckySevenFinisher show = _active;
        if (show == null || cardSource == null || !ReferenceEquals(show._card, cardSource)) return;
        try
        {
            show.OnHit(target, result);
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Lucky Seven slash failed: {error.Message}");
        }
    }

    private void OnHit(Creature target, DamageResult result)
    {
        int hit = _seenHits++;
        if (result != null)
            _observed = CardDamageHistoryHelper.AddDamage(_observed,
                Math.Max(0m, result.BlockedDamage) + Math.Max(0m, result.UnblockedDamage) + Math.Max(0m, result.OverkillDamage));
        if (!GodotObject.IsInstanceValid(_view)) return;
        Vector2? position = ScreenPosition(target);
        if (position is Vector2 point)
        {
            _anchorSum += point;
            _anchorCount++;
        }

        if (!HbrLuckySevenTimeline.StartsSlash(hit)) return;
        _view.Slash(position ?? FallbackPosition(), hit, HbrLuckySevenTimeline.IsBigSlash(_hits, hit));
    }

    // The card's own damage history: everything the card play dealt while the show ran, DP included. The
    // observed hits stand in when no history entry was opened for this autoplay.
    private void SnapshotHistory()
    {
        foreach (CardDamageHistoryEntry entry in CardDamageHistoryHelper.GetEntries(_owner))
            _before.Add((entry, entry.TotalDamage));
    }

    private decimal TotalDamage()
    {
        decimal fromHistory = 0m;
        foreach (CardDamageHistoryEntry entry in CardDamageHistoryHelper.GetEntries(_owner))
        {
            decimal before = 0m;
            foreach ((CardDamageHistoryEntry Entry, decimal Total) known in _before)
            {
                if (!ReferenceEquals(known.Entry, entry)) continue;
                before = known.Total;
                break;
            }
            fromHistory = CardDamageHistoryHelper.AddDamage(fromHistory, Math.Max(0m, entry.TotalDamage - before));
        }
        return fromHistory > 0m ? fromHistory : _observed;
    }

    private static Vector2? ScreenPosition(Creature target)
    {
        NCreature node = target == null ? null : NCombatRoom.Instance?.GetCreatureNode(target);
        if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) return null;
        // Canvas → viewport: the overlay is a canvas layer drawn in viewport coordinates.
        return node.GetCanvasTransform() * node.VfxSpawnPosition;
    }

    private Vector2 FallbackPosition()
    {
        Viewport viewport = _view?.GetViewport();
        Vector2 size = viewport?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);
        return new Vector2(size.X * 0.68f, size.Y * 0.45f);
    }

    // The texture 31O is drawn with right now (normal, bow, dual swords …), already in memory.
    private static Texture2D CurrentPortrait(NCombatRoom room, Creature owner)
    {
        try
        {
            NCreature node = room.GetCreatureNode(owner);
            return node?.Visuals?.GetNodeOrNull<Sprite2D>("%Visuals")?.Texture;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Lucky Seven portrait unavailable: {error.Message}");
            return null;
        }
    }

    // Waits on the show's own clock, frame by frame on the scene tree (which outlives any room), and never
    // longer than the limit, so no stalled or freed node can hold the card.
    private async Task WaitFor(Func<bool> done, float limitSeconds)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        ulong start = Time.GetTicksMsec();
        while (GodotObject.IsInstanceValid(_view) && !done() && Time.GetTicksMsec() - start < (ulong)(limitSeconds * 1000f))
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (ReferenceEquals(_active, this)) _active = null;
        if (GodotObject.IsInstanceValid(_view)) _view.Release();
        _view = null;
    }
}
