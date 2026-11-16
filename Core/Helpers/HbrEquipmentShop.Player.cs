using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Equipment;

internal static partial class HbrEquipmentShop
{
    internal static HbrShopQuote Quote(ElectronicSoldierManual manual, HbrShopOffer offer) =>
        manual?.Owner == null ? default : Quote(manual.Equipment, offer,
            HbrSatsukiShopPolicy.Get(manual.Owner), manual.Owner.RunState.CurrentActIndex);
    internal static bool CanReforge(ElectronicSoldierManual manual) => manual?.IsHbrNumericsEnabled == true &&
        !manual.IsEquipmentPending && HbrEquipmentPurchases.CanReforge(manual.Equipment,
            HbrSatsukiShopPolicy.Get(manual.Owner), ElectronicSoldierManual.IsEquipmentLoadoutUnlocked);
    internal static bool CanChooseReforge(ElectronicSoldierManual manual, int index) =>
        manual?.IsHbrNumericsEnabled == true && !manual.IsEquipmentPending &&
        HbrEquipmentPurchases.CanChoose(manual.Equipment, index, ElectronicSoldierManual.IsEquipmentLoadoutUnlocked);
    internal static int CharmCraftChance(ElectronicSoldierManual manual)
    {
        HbrSatsukiShopPolicy policy = HbrSatsukiShopPolicy.Get(manual?.Owner);
        if (manual == null) return 0;
        int act = Math.Max(0, manual.Owner.RunState.CurrentActIndex);
        return policy.Enabled && policy.FirstAlchemyHighest && !manual.Equipment.AlchemyActs.Contains(act)
            ? 100 : policy.HighCharmChance(HbrEquipmentGenerator.HighCharmChance(manual.EquipmentTier));
    }
}
