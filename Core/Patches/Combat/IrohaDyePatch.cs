using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class IrohaDyeClonePatch
{
    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    { if (__instance is CardModel source && __result is CardModel copy) IrohaDyeCombatHelper.Inherit(source, copy); }
}
[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
internal static class IrohaDyeUpgradePatch
{
    private static void Postfix(CardModel __instance)
    { if (__instance is HBRGOSakiMod.Core.Models.Cards.PaintTheWorldMyColorHbrgoSaki card) IrohaDyeCombatHelper.RefreshFeatures(card.Owner); }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
internal static class IrohaDyeDrawPatch
{
    private static void Postfix(CardModel card, bool fromHandDraw, ref Task __result) => __result = Finish(__result, card, fromHandDraw);
    private static async Task Finish(Task native, CardModel card, bool drawn)
    { await native; IrohaDyeCombatHelper.OnDrawn(card, drawn); }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class IrohaDyePlayedPatch
{
    private static void Postfix(CardPlay cardPlay, ref Task __result) => __result = Finish(__result, cardPlay);
    private static async Task Finish(Task native, CardPlay play)
    { await native; IrohaDyeCombatHelper.AfterPlay(play); }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class IrohaDyeEndPatch
{ private static void Prefix(ICombatState combatState) => IrohaDyeCombatHelper.Clear(combatState); }
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class IrohaDyeResetPatch
{ private static void Prefix(CombatManager __instance) => IrohaDyeCombatHelper.Clear(__instance.DebugOnlyGetState()); }
