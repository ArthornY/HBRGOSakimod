using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
internal static class YanagiEnergyTargetPatch
{
    private static void Postfix(CardModel __instance, Creature __0, ref bool __result)
    {
        if (__result && __instance is AsTheYoungLadyCommandsHbrgoSaki { IsMutable: true, TargetType: TargetType.AnyAlly } card)
            __result = YanagiButlerCombatHelper.IsEnergyTarget(card, __0);
    }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class YanagiPreparedClonePatch
{
    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    { if (__instance is CardModel source && __result is CardModel clone) YanagiButlerCombatHelper.Inherit(source, clone); }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class YanagiCleaningDamagePatch
{
    private static void Postfix(Creature dealer, DamageResult results, ValueProp props, CardModel cardSource, ref Task __result)
    {
        if (YanagiButlerCombatHelper.IsOwnCleaning(cardSource, dealer, props))
            __result = Finish(__result, cardSource, dealer, results, props);
    }
    private static async Task Finish(Task original, CardModel source, Creature dealer, DamageResult result, ValueProp props)
    { await original; YanagiButlerCombatHelper.RecordDamage(source, dealer, result, props); }
}

[HarmonyPatch]
internal static class YanagiPreparedTurnEndPatch
{
    private static MethodInfo TargetMethod() => PhantomTurnParticipants.ResolveHook(typeof(IEnumerable<Creature>),
        "AfterTurnEnd", "AfterSideTurnEnd");
    private static void Postfix(CombatSide side, IEnumerable<Creature> participants, ref Task __result)
    {
        if (side != CombatSide.Player) return;
        Player[] owners = participants.Select(creature => creature.Player ?? creature.PetOwner)
            .Where(player => player != null).Distinct().ToArray();
        if (owners.Length > 0) __result = Finish(__result, owners);
    }
    private static async Task Finish(Task original, Player[] owners)
    {
        await original;
        foreach (Player owner in owners)
            if (OverclockImmediateTurnCoordinator.GetTurnToken(owner) == null) YanagiButlerCombatHelper.OnTurnEnd(owner);
    }
}
