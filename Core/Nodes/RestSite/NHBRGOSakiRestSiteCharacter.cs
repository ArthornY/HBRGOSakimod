#nullable enable

using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace HBRGOSakiMod.Core.Nodes.RestSite;

public partial class NHBRGOSakiRestSiteCharacter : NRestSiteCharacter
{
    private const string SelectionReticleScenePath =
        "res://scenes/ui/selection_reticle.tscn";

    public override void _Ready()
    {
        NSelectionReticle? selectionReticle =
            GetNodeOrNull<NSelectionReticle>("%SelectionReticle");
        if (selectionReticle == null)
        {
            Control controlRoot = GetNode<Control>("ControlRoot");
            PackedScene scene = ResourceLoader.Load<PackedScene>(
                SelectionReticleScenePath);
            selectionReticle = scene.Instantiate<NSelectionReticle>(
                PackedScene.GenEditState.Disabled);
            selectionReticle.Name = "SelectionReticle";
            controlRoot.AddChild(selectionReticle);
            selectionReticle.Owner = this;
            selectionReticle.UniqueNameInOwner = true;
        }

        selectionReticle.OffsetLeft = -170f;
        selectionReticle.OffsetTop = -530f;
        selectionReticle.OffsetRight = 170f;
        selectionReticle.OffsetBottom = 20f;

        base._Ready();
    }
}
