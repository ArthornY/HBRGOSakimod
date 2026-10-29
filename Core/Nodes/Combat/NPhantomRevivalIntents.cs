using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Intents;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NPhantomRevivalIntents : Control
{
    private sealed class Display
    {
        internal MomentOfReign Relic;
        internal NIntent Node;
        internal PhantomRevivalIntent Intent;
        internal Vector2? OffsetFromOwner;
        internal int LastTurns = -1;
    }

    private readonly Dictionary<Player, Display> _displays = new();
    private NCombatRoom _room;
    private double _elapsed;

    public override void _Ready()
    {
        _room = GetParent() as NCombatRoom;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = (_room?.SceneContainer?.ZIndex ?? -10) + 1;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.05) return;
        _elapsed = 0;

        if (_room == null || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            HideAll();
            return;
        }

        var owners = new HashSet<Player>();
        foreach (NCreature ownerNode in _room.CreatureNodes.ToArray())
        {
            Player player = ownerNode.Entity?.Player;
            MomentOfReign relic = player?.GetRelic<MomentOfReign>();
            if (relic == null) continue;
            owners.Add(player);
            if (!_displays.TryGetValue(player, out Display display))
            {
                display = new Display { Relic = relic };
                _displays.Add(player, display);
            }
            Transform2D ownerCanvas = GetOwnerCanvasTransform(ownerNode);

            // Cache the intent's position while the phantom still exists. The
            // dead creature and its native intent are removed by the game.
            Creature phantom = PhantomHelper.GetLivingPhantom(player);
            NCreature phantomNode = phantom == null ? null : _room.GetCreatureNode(phantom);
            if (IsUsable(phantomNode?.IntentContainer))
            {
                Control container = phantomNode.IntentContainer;
                display.OffsetFromOwner = ownerCanvas.AffineInverse() *
                    (container.GetGlobalTransformWithCanvas() * (container.Size * 0.5f));
            }

            int? turns = relic.GetPhantomReviveTurnsRemaining();
            if (!turns.HasValue)
            {
                Hide(display);
                continue;
            }

            if (display.Node == null)
            {
                display.Intent = new PhantomRevivalIntent(relic);
                display.Node = NIntent.Create(0f);
                display.Node.Name = "PhantomRevivalIntent";
                AddChild(display.Node);
            }

            if (display.Relic != relic)
            {
                display.Relic = relic;
                display.Intent = new PhantomRevivalIntent(relic);
                display.LastTurns = -1;
            }

            if (display.LastTurns != turns.Value || !display.Node.Visible)
            {
                // Use the surviving summoner as the hover-tip anchor. No dead
                // creature, targetable placeholder, or saved UI state is needed.
                display.Node.UpdateIntent(display.Intent, Array.Empty<Creature>(), player.Creature);
                display.LastTurns = turns.Value;
            }

            Vector2 center;
            if (display.OffsetFromOwner is { } offset)
            {
                center = GetGlobalTransformWithCanvas().AffineInverse() * (ownerCanvas * offset);
            }
            else if (NSakiCornerCreatureLayout.IsCornerCreature(ownerNode))
            {
                center = GetGlobalTransformWithCanvas().AffineInverse() * (ownerCanvas *
                    (NPhantomCombatLayout.GetPlayerHopperAnchor(ownerNode) +
                        NPhantomCombatLayout.HopperIntentCenter));
            }
            else
            {
                center = ToLocalCanvas(ownerNode.IntentContainer, ownerNode.IntentContainer.Size * 0.5f +
                    new Vector2(Math.Max(180f, ownerNode.Hitbox.Size.X * 0.5f + 150f), 0f));
            }
            // A loaded battle may already be waiting to revive and have no
            // previous phantom node; the fallback places it beside its summoner.
            display.Node.Scale = ownerCanvas.Scale /
                GetGlobalTransformWithCanvas().Scale;
            display.Node.Position = center - display.Node.Size * display.Node.Scale * 0.5f;
            display.Node.Visible = true;
        }

        foreach (Player player in _displays.Keys.Where(player => !owners.Contains(player)).ToArray())
        {
            Display display = _displays[player];
            Hide(display);
            display.Node?.QueueFree();
            _displays.Remove(player);
        }
    }

    private Vector2 ToLocalCanvas(Control control, Vector2 point) =>
        GetGlobalTransformWithCanvas().AffineInverse() * (control.GetGlobalTransformWithCanvas() * point);

    private static Transform2D GetOwnerCanvasTransform(NCreature owner) =>
        // Keep the cache in the same battlefield space when ModUI toggles,
        // including a dead phantom waiting above a scene boss's custom floor.
        LocalContext.IsMe(owner.Entity) && owner.Visuals is NHBRGOSakiCreatureVisuals
            ? owner.GetGlobalTransformWithCanvas() * owner.GetGlobalTransform().AffineInverse() *
                NPhantomCombatLayout.GetPlayerAnchorTransform(owner)
            : owner.GetGlobalTransformWithCanvas();

    private static bool IsUsable(Control control) =>
        control != null && GodotObject.IsInstanceValid(control) && control.IsInsideTree();

    private void HideAll()
    {
        foreach (Display display in _displays.Values) Hide(display);
    }

    private static void Hide(Display display)
    {
        if (display.Node == null) return;
        display.Node.Visible = false;
        display.LastTurns = -1;
    }
}
