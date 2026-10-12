using System;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatHudLayerHelper
{
    private const int StateDisplayLayer = -5;

    internal static void Apply(NCreatureStateDisplay state, bool raised = false)
    {
        if (state == null) return;
        // Native creature visuals inherit the combat scene's -10 layer. Keep
        // every status subtree above them, with its +1/+2 details below UI at 0.
        state.ZAsRelative = false;
        state.ZIndex = StateDisplayLayer + (raised ? 1 : 0);
    }

    internal static int GetDragLayer(int originalLayer) =>
        // Lift dragged art above other bodies without reaching status at -5.
        // Creature roots inherit -10, so a local layer of 4 draws at -6.
        Math.Min(4, originalLayer + 4);
}
