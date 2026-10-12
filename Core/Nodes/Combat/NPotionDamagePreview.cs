using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Refresh the existing potion tooltip without rebuilding its extra tips or
// sending hover/selection messages. Target/state changes refresh immediately;
// a short fallback interval covers effects that only notify their display state.
public partial class NPotionDamagePreview : Node
{
    internal PotionModel Potion { get; set; }
    internal Control OwnerControl { get; set; }
    internal HoverTipAlignment Alignment { get; set; }
    private NHoverTipSet _tips;
    private Control _textContainer;
    private Control _primaryTip;
    private NHoverTipCardContainer _cardContainer;
    private MegaRichTextLabel _description;
    private Creature _lastTarget;
    private bool _lastCanPreview;
    private bool _dirty = true;
    private double _refreshElapsed;

    public override void _Ready()
    {
        _tips = GetParent<NHoverTipSet>();
        _textContainer = _tips.GetNodeOrNull<Control>("textHoverTipContainer");
        _cardContainer = _tips.GetNodeOrNull<NHoverTipCardContainer>("cardHoverTipContainer");
        if (_textContainer?.GetChildCount() > 0)
        {
            _primaryTip = _textContainer.GetChild(0) as Control;
            _description = _primaryTip?.GetNodeOrNull<MegaRichTextLabel>("%Description");
        }
        CombatManager.Instance.StateTracker.CombatStateChanged += OnCombatStateChanged;
    }

    public override void _ExitTree()
    {
        CombatManager.Instance.StateTracker.CombatStateChanged -= OnCombatStateChanged;
    }

    private void OnCombatStateChanged(CombatState _) => _dirty = true;

    public override void _Process(double delta)
    {
        if (!PotionDamagePreviewUiHelper.IsUsable(_description) ||
            !PotionDamagePreviewUiHelper.IsUsable(OwnerControl) || !_tips.IsVisibleInTree()) return;
        Creature target = PotionDamagePreviewUiHelper.GetSelectedTarget(Potion);
        bool canPreview = PotionDamagePreviewHelper.CanPreview(Potion);
        _refreshElapsed += delta;
        if (!_dirty && ReferenceEquals(target, _lastTarget) && canPreview == _lastCanPreview &&
            (!canPreview || _refreshElapsed < 0.1)) return;
        _refreshElapsed = 0;
        _dirty = false;
        _lastTarget = target;
        _lastCanPreview = canPreview;
        string text = Potion.HoverTip.Description;
        if (_description.Text == text) return;
        _description.Text = text;
        float oldHeight = _primaryTip.Size.Y;
        _primaryTip.ResetSize();
        // Keep the native VFlow width and allocated height: ResetSize on the whole
        // container would force unrelated extra tips into additional columns.
        _textContainer.Size = new Vector2(_textContainer.Size.X,
            Mathf.Max(0f, _textContainer.Size.Y + _primaryTip.Size.Y - oldHeight));
        Reposition();
    }

    private void Reposition()
    {
        if (Alignment != HoverTipAlignment.Center)
        {
            _tips.SetAlignment(OwnerControl, Alignment);
            return;
        }

        Control anchor = OwnerControl is NPotionTargetDamagePreview targeting
            ? targeting.Holder : OwnerControl;
        _textContainer.Position = Vector2.Zero;
        _tips.GlobalPosition = anchor.GlobalPosition + Vector2.Down *
            anchor.Size.Y * Mathf.Max(1.5f, anchor.Scale.Y);
        if (PotionDamagePreviewUiHelper.IsUsable(_cardContainer))
        {
            // Native Center alignment adds the text height to the old position.
            // Repeated refreshes must place card tips from the same origin.
            _cardContainer.Position = Vector2.Down * _textContainer.Size.Y;
            _cardContainer.LayoutResizeAndReposition(_cardContainer.GlobalPosition, Alignment);
        }
        // None keeps the chosen anchor while applying native viewport overflow correction.
        _tips.SetAlignment(OwnerControl, HoverTipAlignment.None);
    }
}
