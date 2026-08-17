using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatPreviewRefreshHelper
{
    public static async Task RefreshOpponentIntents(Creature protectedCreature)
    {
        if (protectedCreature?.CombatState == null ||
            Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            return;
        }

        HashSet<Creature> opponents = protectedCreature.CombatState
            .GetOpponentsOf(protectedCreature)
            .Where(creature => creature.Monster != null)
            .ToHashSet();
        if (opponents.Count == 0)
        {
            return;
        }

        foreach (NCreature creatureNode in EnumerateDescendants(sceneTree.Root)
                     .OfType<NCreature>()
                     .Where(node =>
                         GodotObject.IsInstanceValid(node) &&
                         node.IsInsideTree() &&
                         node.Entity != null &&
                         opponents.Contains(node.Entity)))
        {
            await creatureNode.RefreshIntents();
        }
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        Stack<Node> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Node node = pending.Pop();
            yield return node;

            foreach (Node child in node.GetChildren())
            {
                pending.Push(child);
            }
        }
    }
}
