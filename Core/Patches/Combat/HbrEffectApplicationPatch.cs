using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[] {
    typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal),
    typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class HbrEffectApplicationPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power, Creature target,
        decimal amount, Creature applier, CardModel cardSource, bool silent, ref Task __result)
    {
        if (power is not HbrEffectPower effect) return true;
        if (amount <= 0m) { __result = Task.CompletedTask; return false; }
        __result = HbrEffectApplication.Apply(choiceContext, effect, target, effect.NativeApplicationBasis(amount),
            applier, cardSource, effect.NativeApplicationRemaining(amount), silent: silent);
        return false;
    }
}
