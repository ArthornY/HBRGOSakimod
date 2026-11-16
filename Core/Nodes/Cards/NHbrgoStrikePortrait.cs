using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Nodes.Cards;

public partial class NHbrgoStrikePortrait : Node
{
    public NCard Card { get; set; }

    private TextureRect _portrait;
    private TextureRect _ancientPortrait;
    private ColorRect _whiteBackground;
    private ColorRect _ancientWhiteBackground;
    private CardModel _model;
    private string _portraitPath;
    private bool _originalFlipH;
    private bool _originalFlipV;
    private bool _originalAncientFlipH;
    private bool _originalAncientFlipV;
    private TextureRect.StretchModeEnum _originalStretchMode;
    private TextureRect.StretchModeEnum _originalAncientStretchMode;

    internal static bool NeedsWhiteBackground(CardModel model) => HbrMemoryGardenPortraits.HasCutinFrame(model) ||
        model is HalfBodyHbrgoSaki or MyLegendBeginsHbrgoSaki or HelloWorldHbrgoSaki or SaviorGrandEntranceHbrgoSaki or WorldsFinestHbrgoSaki or MoreThanOneTruthHbrgoSaki or CrushYouLikeABugHbrgoSaki or SummerGrassDreamHbrgoSaki or BoostUpHbrgoSaki;

    public override void _Ready()
    {
        Card ??= GetParent() as NCard;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (Card?.IsVisibleInTree() == true)
        {
            Refresh();
        }
    }

    public override void _ExitTree()
    {
        RestoreOrientation();
        _portrait = null;
        _ancientPortrait = null;
    }

    internal void Refresh(bool force = false)
    {
        if (Card?.IsNodeReady() != true)
        {
            return;
        }
        CardModel model = Card.Model;
        bool white = NeedsWhiteBackground(model);
        if (!white && model is not (StrikeHbrgoSaki or DefendHbrgoSaki or CommunicationMeetingHbrgoSaki))
        {
            RestoreOrientation();
            return;
        }

        string path = model.PortraitPath;
        if (!force && _model == model && _portraitPath == path)
        {
            return;
        }

        _portrait ??= Card.GetNodeOrNull<TextureRect>("%Portrait");
        _ancientPortrait ??= Card.GetNodeOrNull<TextureRect>("%AncientPortrait");
        if (_portrait == null)
        {
            return;
        }
        if (_model == null)
        {
            _originalFlipH = _portrait.FlipH;
            _originalFlipV = _portrait.FlipV;
            _originalAncientFlipH = _ancientPortrait?.FlipH ?? false;
            _originalAncientFlipV = _ancientPortrait?.FlipV ?? false;
            _originalStretchMode = _portrait.StretchMode;
            _originalAncientStretchMode = _ancientPortrait?.StretchMode ?? TextureRect.StretchModeEnum.Scale;
        }

        Texture2D texture = model.Portrait;
        var mode = (model as StrikeHbrgoSaki)?.CurrentPortraitMode;
        // Flip the presentation only: both axes equal a 180-degree rotation, without resampling.
        bool flipH = mode is StrikeHbrgoSaki.StrikePortraitMode.DualSwords or StrikeHbrgoSaki.StrikePortraitMode.Bow;
        bool flipV = mode == StrikeHbrgoSaki.StrikePortraitMode.Bow;
        // Cards in the HBR card style always fill its art window (NHbrCardStyle), whatever the art's shape.
        bool cover = mode == StrikeHbrgoSaki.StrikePortraitMode.Fist || NHbrCardStyle.IsStyled(model) ||
            white || model is CommunicationMeetingHbrgoSaki;
        ApplyPortrait(_portrait, texture, flipH, flipV,
            cover ? TextureRect.StretchModeEnum.KeepAspectCovered : _originalStretchMode);
        ApplyPortrait(_ancientPortrait, texture, flipH, flipV,
            cover ? TextureRect.StretchModeEnum.KeepAspectCovered : _originalAncientStretchMode);
        // Memory portraits in the transparent slanted cut-in frame sit on white, like the cut-ins in HBR.
        SetWhiteBackground(_portrait, ref _whiteBackground, white);
        SetWhiteBackground(_ancientPortrait, ref _ancientWhiteBackground, white);
        _model = model;
        _portraitPath = path;
    }

    private static void ApplyPortrait(TextureRect portrait, Texture2D texture, bool flipH, bool flipV,
        TextureRect.StretchModeEnum stretchMode)
    {
        if (portrait == null)
        {
            return;
        }
        portrait.Texture = texture;
        portrait.FlipH = flipH;
        portrait.FlipV = flipV;
        portrait.StretchMode = stretchMode;
    }

    private static void SetWhiteBackground(TextureRect portrait, ref ColorRect background, bool visible)
    {
        if (portrait == null) return;
        if (visible && !GodotObject.IsInstanceValid(background))
        {
            // Keep the original transparent artwork untouched. The underlay
            // follows the portrait bounds and shares the native CanvasGroup
            // mask/blur, so it cannot expose the scene through transparent pixels.
            background = new ColorRect
            {
                Name = "HbrOpaquePortraitBackground",
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ShowBehindParent = true
            };
            portrait.AddChild(background);
            background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        if (GodotObject.IsInstanceValid(background)) background.Visible = visible;
    }

    private void RestoreOrientation()
    {
        if (GodotObject.IsInstanceValid(_whiteBackground)) _whiteBackground.Hide();
        if (GodotObject.IsInstanceValid(_ancientWhiteBackground)) _ancientWhiteBackground.Hide();
        if (_model == null)
        {
            return;
        }
        if (GodotObject.IsInstanceValid(_portrait))
        {
            _portrait.FlipH = _originalFlipH;
            _portrait.FlipV = _originalFlipV;
            _portrait.StretchMode = _originalStretchMode;
        }
        if (GodotObject.IsInstanceValid(_ancientPortrait))
        {
            _ancientPortrait.FlipH = _originalAncientFlipH;
            _ancientPortrait.FlipV = _originalAncientFlipV;
            _ancientPortrait.StretchMode = _originalAncientStretchMode;
        }
        _model = null;
        _portraitPath = null;
    }
}
