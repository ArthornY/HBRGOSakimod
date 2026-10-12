using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
internal static class HotaruCannonNativeLayoutPatch
{
    private static void Prefix(ref List<NCreature> creatureNodes) =>
        creatureNodes = creatureNodes.Where(node => node.Entity?.Monster is not HotaruCannonModel).ToList();
    private static void Postfix() => NHotaruCannonLayout.Install(NCombatRoom.Instance);
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
internal static class HotaruCannonLayoutInstallPatch
{
    private static void Postfix(NCombatRoom __instance) => NHotaruCannonLayout.Install(__instance);
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class HotaruCannonSelectablePatch
{
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Monster is not HotaruCannonModel) return;
        __instance.ToggleIsInteractable(on: true);
        NHotaruCannonLayout.HideHealth(__instance);
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
internal static class HotaruCannonPetSelectablePatch
{
    private static void Postfix(NCombatRoom __instance, Creature creature)
    {
        if (creature?.PetOwner is not { } owner) return;

        // AddCreature disables every pet hitbox after its _Ready has completed.
        // Adding another pet also disables the existing cannons for that owner.
        foreach (NCreature node in __instance.CreatureNodes)
        {
            if (!Millsage.IsOwnedCannon(node.Entity, owner)) continue;
            node.ToggleIsInteractable(on: true);
            NHotaruCannonLayout.HideHealth(node);
        }
    }
}

[HarmonyPatch(typeof(CombatState), nameof(CombatState.GetOpponentsOf))]
internal static class HotaruCannonOpponentPatch
{
    private static void Postfix(ref IReadOnlyList<Creature> __result) =>
        __result = __result.Where(creature => creature.Monster is not HotaruCannonModel).ToArray();
}

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class HotaruCannonAttackTargetPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref IReadOnlyList<Creature> __result) =>
        __result = __result.Where(creature => creature?.Monster is not HotaruCannonModel).ToArray();
}

[HarmonyPatch]
internal static class HotaruCannonDirectDamagePatch
{
    private static MethodBase TargetMethod() => typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method =>
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length is 6 or 7 && parameters[0].ParameterType == typeof(PlayerChoiceContext) &&
                parameters[1].ParameterType == typeof(IEnumerable<Creature>) && parameters[2].ParameterType == typeof(decimal) &&
                parameters[3].ParameterType == typeof(ValueProp) && parameters[4].ParameterType == typeof(Creature) &&
                parameters[5].ParameterType == typeof(CardModel);
        })
        .OrderBy(method => method.GetParameters().Length).FirstOrDefault() ??
        throw new MissingMethodException(typeof(CreatureCmd).FullName, nameof(CreatureCmd.Damage));

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(ref IEnumerable<Creature> targets) =>
        targets = targets.Where(creature => creature?.Monster is not HotaruCannonModel).ToArray();
}

[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition")]
internal static class HotaruCannonDirectKillPatch
{
    private static bool Prefix(Creature creature, bool force, ref Task __result)
    {
        if (force || creature?.Monster is not HotaruCannonModel) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
