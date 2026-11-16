using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HBRGOSakiMod.Core.Patches.Merchant;

// Lays 31O's fate fragment stand on the merchant's rug once the vanilla screen has filled its slots. It only adds
// a node: the vanilla inventory, its entries and their purchases are untouched.
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class HbrFateFragmentShelfPatch
{
    private static void Postfix(NMerchantInventory __instance) => NHbrFateFragmentShelf.Attach(__instance);
}
