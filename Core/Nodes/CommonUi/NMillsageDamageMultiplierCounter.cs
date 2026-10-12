using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Nodes.CommonUi;

// Kept as a cleanup hook for existing UI nodes. The per-cannon multiplier now
// lives in that cannon's first status slot, while Millsage retains Layers.
public partial class NMillsageDamageMultiplierCounter : Node
{
    private const string WidgetName = "HBRGOMillsageDamageMultiplierCounter";
    private const string LabelName = "HBRGOMillsageDamagePercentLabel";

    internal static void Refresh(NRelicInventoryHolder holder, MegaLabel nativeCounter)
    {
        if (!Usable(nativeCounter)) return;
        Node parent = nativeCounter.GetParent();
        if (!Usable(parent)) return;
        Control label = parent.GetNodeOrNull<Control>(LabelName);
        if (Usable(label))
        {
            label.Hide();
            label.QueueFree();
        }
        Node counter = parent.GetNodeOrNull<Node>(WidgetName);
        if (Usable(counter)) counter.QueueFree();
    }

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
