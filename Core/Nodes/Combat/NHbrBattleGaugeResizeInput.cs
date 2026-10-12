using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Observe only the resize gesture before GUI dispatch; the native bar keeps
// its existing hover, targeting, and mouse-filter behavior.
public partial class NHbrBattleGaugeResizeInput : Node
{
    private NCombatRoom _room;

    public override void _Ready() => _room = GetParent() as NCombatRoom;

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, AltPressed: true } mouse ||
            !IsUsable(_room) || !_room.IsVisibleInTree() ||
            ActiveScreenContext.Instance?.IsCurrent(_room) != true ||
            !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int direction = mouse.ButtonIndex switch
        {
            MouseButton.WheelUp => 1,
            MouseButton.WheelDown => -1,
            _ => 0
        };
        if (direction == 0) return;

        foreach (NCreature creature in _room.CreatureNodes)
        {
            NHealthBar bar = GetHealthBar(creature);
            if (bar == null || !HbrBattleGaugeSizing.HitTest(bar, mouse.Position)) continue;

            HbrBattleGaugeSizing.AdjustThickness(direction, mouse.Factor);
            // A single preference applies to monster gauges, including phantoms
            // and minions. Player bars are excluded by the sizing helper.
            foreach (NCreature activeCreature in _room.CreatureNodes)
            {
                NHealthBar activeBar = GetHealthBar(activeCreature);
                if (activeBar != null) HbrBattleGaugeSizing.Refresh(activeBar);
            }
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private static NHealthBar GetHealthBar(NCreature creature)
    {
        if (!IsUsable(creature)) return null;
        NCreatureStateDisplay state = creature.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        if (!IsUsable(state)) return null;
        NHealthBar bar = state.GetNodeOrNull<NHealthBar>("%HealthBar");
        return IsUsable(bar) ? bar : null;
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
