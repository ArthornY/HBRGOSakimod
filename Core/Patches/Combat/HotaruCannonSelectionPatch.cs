using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class HotaruCannonSelection
{
    private static readonly AccessTools.FieldRef<NTargetManager, TargetType> ValidTargets =
        AccessTools.FieldRefAccess<NTargetManager, TargetType>("_validTargetsType");
    private static readonly Func<NTargetManager, Node> HoveredNode =
        PrivateAccess.MethodDelegate<NTargetManager, Func<NTargetManager, Node>>("get_HoveredNode");

    internal static bool IsAvailable(Creature creature, NTargetManager manager)
    {
        if (creature?.IsHittable == true) return true;
        if (manager == null || !manager.IsInSelection || creature?.Monster is not HotaruCannonModel)
            return false;
        TargetType targetType = ValidTargets(manager);
        if (targetType != HotaruTargetTypes.OwnedCannon && targetType != HotaruTargetTypes.OwnedCannonOrEnemy)
            return false;
        // The active Ritsu card context supplies the correct owner. Recheck
        // its predicate and native node filter without making cannons hittable.
        return HoveredNode(manager) is NCreature node && ReferenceEquals(node.Entity, creature) &&
            manager.AllowedToTargetNode(node);
    }
}

[HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager._Process))]
internal static class HotaruCannonSelectionProcessPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo hittable = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.IsHittable));
        MethodInfo available = AccessTools.DeclaredMethod(typeof(HotaruCannonSelection),
            nameof(HotaruCannonSelection.IsAvailable));
        List<CodeInstruction> body = instructions.ToList();
        if (body.Count(instruction => instruction.Calls(hittable)) != 1)
            throw new InvalidOperationException("Target manager must expose exactly one hover availability check.");
        foreach (CodeInstruction instruction in body)
        {
            if (!instruction.Calls(hittable))
            {
                yield return instruction;
                continue;
            }
            var manager = new CodeInstruction(OpCodes.Ldarg_0);
            manager.labels.AddRange(instruction.labels);
            manager.blocks.AddRange(instruction.blocks.Where(block =>
                block.blockType != ExceptionBlockType.EndExceptionBlock));
            var check = new CodeInstruction(OpCodes.Call, available);
            check.blocks.AddRange(instruction.blocks.Where(block =>
                block.blockType == ExceptionBlockType.EndExceptionBlock));
            yield return manager;
            yield return check;
        }
    }
}