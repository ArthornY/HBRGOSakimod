using System;
using Godot;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace HBRGOSakiMod.Core.Nodes.Screens;

internal enum MemoryGardenSlot { Obtain, Upgrade, Sealed, Complete, Ghost }

// Everything the garden adds to one card in its grid: the status pill, the seal over memories that cannot be
// recalled now (a silhouette for characters not met yet), and the card's reflection in the water. The pill and the
// reflection ride along with the card (hover lift included) by copying the card's transform, so they work in the
// card's own space: the frame spans (-150, -211)-(150, 211). The seal sits inside the card, under its energy icon,
// so the cost orb stays on top of the static (NMemoryGardenSeal). While the garden shows the card, the hidden
// characters of its name jump with the static (NHbrCardStyle.KeepTitleJumping).
public partial class NMemoryGardenCardDecor : Control
{
    // The silhouette of a character met since the garden last showed them drains away over this many seconds.
    internal const double MeetSeconds = 1.1;
    private const string ReflectionShader = "res://shaders/hbr_memory_garden_reflection.gdshader";
    private static readonly Rect2 Card = new(-150, -211, 300, 422);
    private readonly NMemoryGardenSeal _seal;
    private readonly BackBufferCopy _sealedCopy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Rect, Rect = Card };
    private readonly ColorRect _reflection = new() { Name = "Reflection", MouseFilter = MouseFilterEnum.Ignore };
    private readonly NMemoryGardenRibbon _ribbon = new();
    private CardModel _meetModel;
    private double _meetStart;

    internal NGridCardHolder Holder { get; set; }

    public NMemoryGardenCardDecor()
    {
        Name = "MemoryGardenDecor";
        MouseFilter = MouseFilterEnum.Ignore;
        _seal = new NMemoryGardenSeal { Decor = this };
        // The reflection mirrors the card's lower 157px (37%, as in the preview) just under its bottom edge.
        _reflection.Material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(ReflectionShader) };
        _reflection.Position = new Vector2(Card.Position.X, Card.End.Y + 2);
        _reflection.Size = new Vector2(Card.Size.X, 157);
        _ribbon.Position = new Vector2(0, Card.End.Y + 6);
        AddChild(_sealedCopy);
        AddChild(_reflection);
        AddChild(_ribbon);
    }

    internal void Display(MemoryGardenSlot slot, string label)
    {
        _ribbon.Display(slot, label);
        _seal.Display(slot);
        if (GodotObject.IsInstanceValid(Holder) && GodotObject.IsInstanceValid(Holder.CardNode))
            _seal.Attach(Holder.CardNode);
    }

    // The card's character was met since the garden last showed them: start as a silhouette and, after `delay`
    // seconds, let it drain away. Tied to the model shown now, so a holder reused for another card drops it.
    internal void PlayMeet(double delay)
    {
        if (!GodotObject.IsInstanceValid(Holder) || Holder.CardModel is not { } model) return;
        _meetModel = model;
        _meetStart = Now + delay;
        _seal.SetMeet(0f);
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(Holder) || !GodotObject.IsInstanceValid(Holder.CardNode)) return;
        NCard card = Holder.CardNode;
        PivotOffset = card.PivotOffset;
        Position = card.Position;
        Scale = card.Scale;
        Rotation = card.Rotation;
        // Holders can re-add their card; stay drawn after it.
        if (GetIndex() != Holder.GetChildCount() - 1) Holder.MoveChild(this, -1);
        _seal.Attach(card);
        NHbrCardStyle.Of(card)?.KeepTitleJumping();
        if (_meetModel == null) return;
        double progress = (Now - _meetStart) / MeetSeconds;
        if (!ReferenceEquals(Holder.CardModel, _meetModel) || progress >= 1)
        {
            _meetModel = null;
            _seal.SetMeet(null);
            return;
        }
        float t = (float)Math.Clamp(progress, 0, 1);
        _seal.SetMeet(t * t * (3 - 2 * t));
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    // Grid holders come from the native node pool and go back to it with their children when a grid releases
    // them, so leaving the tree means the garden is done with this holder. Undo what the garden set on it in
    // NMemoryGardenScreen.RefreshGridHolders, take the seal out of the card and free the decor. Otherwise the next
    // grid to reuse the holder or the card (the upgrade screen, say) shows a sealed card that cannot be clicked or
    // focused. Children leave the tree before their parent, so the card is still in the holder at this point.
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Holder))
        {
            Holder.Hitbox.SetEnabled(true);
            Holder.FocusMode = FocusModeEnum.All;
            Holder.FocusNeighborBottom = new NodePath();
            Holder.FocusNext = new NodePath();
            Holder.FocusPrevious = new NodePath();
        }
        if (GodotObject.IsInstanceValid(_seal)) _seal.Release();
        QueueFree();
    }
}
