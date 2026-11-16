using Godot;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The static over a memory the garden cannot recall now: sealed, not met yet, or fully upgraded (a light wash).
// A card not met yet shows a two-tone silhouette of its own art instead of a blank slate
// (ui-concepts/memory-garden-names-20261106); when its character has just been met, the silhouette drains away.
// NMemoryGardenCardDecor keeps it inside the card, right before the energy icon, so the cost orb, its number and
// the six-sense badge hanging off the orb stay on top while the art, frame, title, text and type plate are covered.
// Its children work in the card's own space: the frame spans (-150, -211)-(150, 211).
public partial class NMemoryGardenSeal : Control
{
    private const string SealShader = "res://shaders/hbr_memory_garden_seal.gdshader";
    private static readonly Rect2 Card = new(-150, -211, 300, 422);
    private readonly BackBufferCopy _cardCopy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Rect, Rect = Card };
    private readonly ColorRect _static = new() { Name = "Static", MouseFilter = MouseFilterEnum.Ignore };
    private MemoryGardenSlot? _slot;
    private (MemoryGardenSlot? Slot, bool Meeting)? _applied;
    private bool _shown;
    private float? _meet;

    internal NMemoryGardenCardDecor Decor { get; set; }

    public NMemoryGardenSeal()
    {
        Name = "MemoryGardenSeal";
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        var material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(SealShader) };
        material.SetShaderParameter("art_rect", Uv(HbrCardStyleLayout.ArtWindow, 0));
        material.SetShaderParameter("text_rect", Uv(HbrCardStyleLayout.Description, 4));
        _static.Material = material;
        _static.Position = Card.Position;
        _static.Size = Card.Size;
        AddChild(_cardCopy);
        AddChild(_static);
    }

    internal void Display(MemoryGardenSlot slot)
    {
        _slot = slot;
        _shown = slot is MemoryGardenSlot.Sealed or MemoryGardenSlot.Ghost or MemoryGardenSlot.Complete;
        Apply();
        Visible = Covering && Owned();
    }

    // A character met since the garden last showed them: the card starts as a silhouette and `progress` (0 to 1)
    // drains it away from the bottom up, into the slot's own cover (sealed, fully upgraded) or into nothing.
    // Null ends it and the card shows its slot alone.
    internal void SetMeet(float? progress)
    {
        _meet = progress is { } value ? Mathf.Clamp(value, 0f, 1f) : null;
        Apply();
        ((ShaderMaterial)_static.Material).SetShaderParameter("reveal", _meet ?? 0f);
        Visible = Covering && Owned();
    }

    private bool Covering => _shown || _meet != null;

    private void Apply()
    {
        (MemoryGardenSlot? Slot, bool Meeting) state = (_slot, _meet != null);
        if (_applied == state) return;
        _applied = state;
        var material = (ShaderMaterial)_static.Material;
        // The slot's own cover. A fully upgraded memory is only washed out a little: saturate(.8) brightness(1.04).
        bool cover = _slot is MemoryGardenSlot.Sealed or MemoryGardenSlot.Complete;
        bool full = _slot == MemoryGardenSlot.Complete;
        material.SetShaderParameter("saturation", full ? .8f : .3f);
        material.SetShaderParameter("brightness", full ? 1.04f : .9f);
        material.SetShaderParameter("noise_amount", full ? 0f : .45f);
        material.SetShaderParameter("scanlines", full ? 0f : 1f);
        material.SetShaderParameter("band", full ? 0f : 1f);
        // The silhouette, for a character not met yet or while a newly met one drains away.
        material.SetShaderParameter("ghost", _slot == MemoryGardenSlot.Ghost || state.Meeting);
        material.SetShaderParameter("target_cover", state.Meeting && cover);
    }

    // Puts the seal in the energy icon's parent (the card's CardContainer), right before the icon. The card face
    // (portrait, frame, text, title banner, type plaque, overlays) comes before %EnergyIcon in card.tscn, the same
    // in stable and v0.111.0; only the star cost, enchantment tab and sparkles come after it.
    internal void Attach(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card) || card.IsQueuedForDeletion() || !card.IsNodeReady()) return;
        Control energyIcon = card.GetNodeOrNull<Control>("%EnergyIcon");
        if (energyIcon?.GetParent() is not Control container) return;
        if (GetParent() != container)
        {
            GetParent()?.RemoveChild(this);
            container.AddChild(this);
        }
        int icon = energyIcon.GetIndex();
        int self = GetIndex();
        if (self + 1 != icon) container.MoveChild(this, self < icon ? icon - 1 : icon);
    }

    // The decor calls this when the garden lets go of its holder, before the card goes back to the node pool.
    internal void Release()
    {
        Visible = false;
        GetParent()?.RemoveChild(this);
        QueueFree();
    }

    public override void _EnterTree() => Guard();

    public override void _Process(double delta) => Guard();

    // Cards come from the native node pool. Should this one ever end up away from the decor's holder, keep the
    // seal hidden there, and free it once the decor is gone, instead of sealing a card on another screen.
    private void Guard()
    {
        if (!GodotObject.IsInstanceValid(Decor) || Decor.IsQueuedForDeletion())
        {
            Visible = false;
            QueueFree();
            return;
        }
        Visible = Covering && Owned();
    }

    private bool Owned() =>
        GodotObject.IsInstanceValid(Decor) && !Decor.IsQueuedForDeletion() &&
        GodotObject.IsInstanceValid(Decor.Holder) && Decor.Holder.CardNode is { } card &&
        GodotObject.IsInstanceValid(card) && card.IsAncestorOf(this);

    // A rect in card space as UVs of the seal, which spans the whole card; `pad` grows it on every side.
    private static Vector4 Uv(HbrCardRect rect, float pad) => new(
        (rect.Left - pad - Card.Position.X) / Card.Size.X, (rect.Top - pad - Card.Position.Y) / Card.Size.Y,
        (rect.Right + pad - Card.Position.X) / Card.Size.X, (rect.Bottom + pad - Card.Position.Y) / Card.Size.Y);
}
