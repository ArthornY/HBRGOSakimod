using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace HBRGOSakiMod.Core.Patches;

internal static class ShadowDanceTargetingState
{
    internal static Creature Owner { get; set; }
}

[HarmonyPatch]
internal static class ShadowDanceCanPlayPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.GetDeclaredMethods(typeof(CardModel))
            .Single(method =>
            {
                if (method.Name != nameof(CardModel.CanPlay))
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(UnplayableReason).MakeByRefType();
            });
    }

    private static void Postfix(
        CardModel __instance,
        ref UnplayableReason reason,
        ref bool __result)
    {
        if (__instance is not ShadowDanceSongHbrgoSaki)
        {
            return;
        }

        reason &= ~UnplayableReason.NoLivingAllies;
        __result = reason == UnplayableReason.None;
    }
}

[HarmonyPatch]
internal static class ShadowDanceTargetingStartPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(NTargetManager),
            nameof(NTargetManager.StartTargeting),
            new[]
            {
                typeof(TargetType),
                typeof(Control),
                typeof(TargetMode),
                typeof(Func<bool>),
                typeof(Func<Node, bool>)
            });
    }

    private static void Prefix(Control control)
    {
        ShadowDanceTargetingState.Owner =
            control is NCard { Model: ShadowDanceSongHbrgoSaki card }
                ? card.Owner.Creature
                : null;
    }
}

[HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager.AllowedToTargetNode))]
internal static class ShadowDanceAllowedTargetPatch
{
    private static void Postfix(Node node, ref bool __result)
    {
        if (__result || ShadowDanceTargetingState.Owner is not { } owner)
        {
            return;
        }

        Creature creature = node switch
        {
            NCreature creatureNode => creatureNode.Entity,
            NMultiplayerPlayerState playerState => playerState.Player.Creature,
            _ => null
        };

        if (ReferenceEquals(creature, owner) && creature.IsAlive)
        {
            __result = true;
        }
    }
}

[HarmonyPatch]
internal static class ShadowDanceTargetingFinishPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(NTargetManager), "FinishTargeting");
    }

    private static void Postfix()
    {
        ShadowDanceTargetingState.Owner = null;
    }
}

[HarmonyPatch]
internal static class ShadowDanceControllerCandidatePatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(NControllerCardPlay)
            .GetNestedTypes(BindingFlags.NonPublic)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic))
            .Single(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                FieldInfo[] closureFields = method.DeclaringType?.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic) ?? Array.Empty<FieldInfo>();
                return method.Name.Contains("SingleCreatureTargeting", StringComparison.Ordinal) &&
                       method.ReturnType == typeof(bool) &&
                       parameters.Length == 1 &&
                       parameters[0].ParameterType == typeof(Creature) &&
                       closureFields.Any(field =>
                           typeof(NControllerCardPlay).IsAssignableFrom(field.FieldType)) &&
                       closureFields.Any(field =>
                           typeof(Creature).IsAssignableFrom(field.FieldType));
            });
    }

    private static void Postfix(object __instance, Creature c, ref bool __result)
    {
        if (__result || !c.IsHittable)
        {
            return;
        }

        FieldInfo cardPlayField = __instance.GetType()
            .GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .FirstOrDefault(field =>
                typeof(NControllerCardPlay).IsAssignableFrom(field.FieldType));

        if (cardPlayField?.GetValue(__instance) is not NControllerCardPlay cardPlay ||
            cardPlay.Holder.CardNode?.Model is not ShadowDanceSongHbrgoSaki card)
        {
            return;
        }

        if (ReferenceEquals(c, card.Owner.Creature))
        {
            __result = true;
        }
    }
}
