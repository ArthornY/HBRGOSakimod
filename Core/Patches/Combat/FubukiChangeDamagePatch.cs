using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class FubukiChangeDamagePatch
{
    private static void Postfix(PlayerChoiceContext choiceContext, Creature dealer, DamageResult results,
        ValueProp props, Creature target, CardModel cardSource, ref Task __result)
    {
        if (FubukiChangeCombatHelper.IsOwnAttack(cardSource, dealer, target, props))
            __result = Finish(__result, choiceContext, cardSource, results);
    }
    private static async Task Finish(Task native, PlayerChoiceContext context, CardModel source, DamageResult result)
    { await native; FubukiChangeCombatHelper.RecordDamage(source, result); }
}
