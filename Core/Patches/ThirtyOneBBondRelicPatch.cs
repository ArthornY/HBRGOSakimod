using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain),
    new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
internal static class TigersMouthSnackObtainPatch
{
    internal sealed class Receipt
    {
        internal TigersMouthSnackBond Bond;
        internal RelicModel Food;
        internal Player Owner;
    }
    private static void Prefix(RelicModel relic, Player player, out Receipt __state)
    {
        __state = player?.GetRelic<TigersMouthSnackBond>() is { IsActive: true } bond &&
            TigersMouthSnackBond.IsFood(relic) && !player.Relics.Contains(relic)
            ? new Receipt { Bond = bond, Food = relic, Owner = player } : null;
    }
    private static void Postfix(Receipt __state, ref Task<RelicModel> __result)
    {
        if (__state != null) __result = Finish(__result, __state);
    }
    private static async Task<RelicModel> Finish(Task<RelicModel> original, Receipt receipt)
    {
        RelicModel obtained = await original;
        if (ReferenceEquals(obtained, receipt.Food) && receipt.Food.Owner == receipt.Owner &&
            receipt.Owner.Relics.Contains(receipt.Food) && receipt.Owner.Relics.Contains(receipt.Bond))
            receipt.Bond.GrantFoodBonus();
        return obtained;
    }
}

[HarmonyPatch]
internal static class ThirtyOneBBondDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
    }
    private static void Postfix(RelicModel __instance, LocString __result)
    {
        switch (__instance)
        {
            case TigersMouthSnackBond snack: snack.AddDescriptionVariables(__result); break;
            case ThirtyOneBCollection squad: squad.AddDescriptionVariables(__result); break;
            case FamilyIsLikeThisBond sisters: sisters.AddDescriptionVariables(__result); break;
        }
    }
}
