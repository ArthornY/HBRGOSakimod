using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Entities.UI;

namespace HBRGOSakiMod.Core.Nodes.Cards;

public partial class NSixSenseEnergyIcon : Node
{
    public NCard Card { get; set; }
    private TextureRect _energyIcon;
    private Polygon2D _badge;
    private Line2D _outline;
    private Texture2D _atlas;
    private Vector2 _badgeSize;
    private SixSenseAttribute? _badgeAttribute;
    private Color _originalColor;
    private bool _replaced;

    public override void _Ready()
    {
        Card ??= GetParent() as NCard;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (GodotObject.IsInstanceValid(Card) && Card.IsVisibleInTree()) Refresh();
    }

    internal void Refresh()
    {
        if (!GodotObject.IsInstanceValid(Card) || Card.IsQueuedForDeletion() || !Card.IsNodeReady()) return;
        SixSenseAttribute? attribute = Card.Visibility == ModelVisibility.Visible
            ? SixSenseBadgeHelper.CardAttribute(Card.Model) : null;
        if (!attribute.HasValue) { Restore(); return; }
        if (!GodotObject.IsInstanceValid(_energyIcon))
        {
            Restore();
            _energyIcon = Card.GetNodeOrNull<TextureRect>("%EnergyIcon");
            _badge = null;
            _outline = null;
        }
        if (!GodotObject.IsInstanceValid(_energyIcon) || _energyIcon.IsQueuedForDeletion()) return;
        bool atlasChanged = !GodotObject.IsInstanceValid(_atlas);
        if (!SixSenseBadgeHelper.TryGetAtlas(ref _atlas) || _energyIcon.Size.X <= 0 || _energyIcon.Size.Y <= 0)
        {
            Restore();
            return;
        }
        if (!GodotObject.IsInstanceValid(_badge))
        {
            _badgeSize = Vector2.Zero;
            _badgeAttribute = null;
            _badge = new Polygon2D
            {
                Name = "SixSenseCostBackground",
                Texture = _atlas,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear
            };
            _energyIcon.AddChild(_badge);
            // Draw the replacement after the icon, but before its cost label and overlays.
            _energyIcon.MoveChild(_badge, 0);
        }
        if (!GodotObject.IsInstanceValid(_outline))
        {
            _outline = new Line2D
            {
                Name = "Rim",
                Closed = true,
                Width = .65f,
                DefaultColor = new Color("BEBEBE"),
                Antialiased = true
            };
            _badge.AddChild(_outline);
            _badgeSize = Vector2.Zero;
        }
        if (atlasChanged)
        {
            _badge.Texture = _atlas;
            // Rebind even when the card's attribute has not changed since the
            // resource was disposed; refresh UVs against the new texture too.
            _badgeAttribute = null;
        }
        if (_badgeSize != _energyIcon.Size)
        {
            _badgeSize = _energyIcon.Size;
            Vector2[] points = SixSenseBadgeHelper.Hexagon(_badgeSize);
            _badge.Polygon = points;
            _outline.Points = points;
        }
        if (_badgeAttribute != attribute)
        {
            Vector2[] uvs = SixSenseBadgeHelper.AtlasUvs(attribute.Value);
            Vector2 atlasSize = _atlas.GetSize();
            // Polygon2D takes pixel UVs, unlike CanvasItem.DrawPolygon's normalized UVs.
            for (int index = 0; index < uvs.Length; index++) uvs[index] *= atlasSize;
            _badge.UV = uvs;
            _badgeAttribute = attribute;
        }
        if (!_replaced)
        {
            _originalColor = _energyIcon.SelfModulate;
            _energyIcon.SelfModulate = new Color(_originalColor, 0);
            _badge.Modulate = _originalColor;
            _replaced = true;
        }
        // Only hide the old background after its replacement is ready. Vanilla owns cost/X/visibility.
        _badge.Visible = true;
    }

    private void Restore()
    {
        if (_replaced && GodotObject.IsInstanceValid(_energyIcon))
            _energyIcon.SelfModulate = _originalColor;
        if (GodotObject.IsInstanceValid(_badge)) _badge.Visible = false;
        _replaced = false;
    }

    public override void _ExitTree()
    {
        Restore();
        _atlas = null;
        _badgeAttribute = null;
    }
}
