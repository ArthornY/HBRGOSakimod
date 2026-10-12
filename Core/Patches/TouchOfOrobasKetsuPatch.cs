using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(ArchaicTooth), "get_TranscendenceUpgrades")]
internal static class ArchaicToothHbrgoSakiTranscendencePatch
{
    private static void Postfix(Dictionary<ModelId, CardModel> __result)
    {
        if (__result == null)
        {
            return;
        }

        if (ModelDb.Contains(typeof(MagicSwordLuckySevenHbrgoSaki)) &&
            ModelDb.Contains(typeof(MagicSwordLuckySevenOpeningHbrgoSaki)))
        {
            __result[ModelDb.Card<MagicSwordLuckySevenHbrgoSaki>().Id] =
                ModelDb.Card<MagicSwordLuckySevenOpeningHbrgoSaki>();
        }

        if (ModelDb.Contains(typeof(StrengthAmplificationHbrgoSaki)) &&
            ModelDb.Contains(typeof(DistantPrayerHbrgoSaki)))
        {
            __result[ModelDb.Card<StrengthAmplificationHbrgoSaki>().Id] =
                ModelDb.Card<DistantPrayerHbrgoSaki>();
        }
    }
}

[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
internal static class ArchaicToothHbrgoSakiVariantPatch
{
    private static bool Prefix(Player player, ref bool __result)
    {
        if (player?.Character is HbrgoSaki and not HbrgoSaki31O and not HbrgoSaki30A)
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(TouchOfOrobas), "GetStarterRelic")]
internal static class TouchOfOrobasThirtyAStarterPatch
{
    private static bool Prefix(Player p, ref RelicModel __result)
    {
        if (p?.Character is not HbrgoSaki30A)
        {
            return true;
        }

        // 30A has multiple starters; the first is the manual, not the phantom relic.
        __result = p.Relics.FirstOrDefault(relic =>
            relic is MomentOfReign and not BecomeGodHbrgoSaki);
        return false;
    }
}

[HarmonyPatch(
    typeof(TouchOfOrobas),
    nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
internal static class TouchOfOrobasHbrgoStarterUpgradePatch
{
    private static void Postfix(
        RelicModel starterRelic,
        ref RelicModel __result)
    {
        if (starterRelic is ElectronicSoldierManual31O)
        {
            __result = ModelDb.Relic<Ketsu>();
        }
        else if (starterRelic is MomentOfReign and
                 not BecomeGodHbrgoSaki)
        {
            __result = ModelDb.Relic<BecomeGodHbrgoSaki>();
        }
    }
}

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Replace))]
internal static class ElectronicSoldierManualUpgradeStatePatch
{
    private static void Prefix(RelicModel original, RelicModel replace)
    {
        if (original is ElectronicSoldierManual source &&
            replace is ElectronicSoldierManual upgrade)
        {
            upgrade.InheritMemoryProgressFrom(source);
            Log.Info($"[HBRGOSakiMod][MemoryGarden] Preserved progress on manual replacement: " +
                $"{source.Id} -> {upgrade.Id}, characters=[{upgrade.MemoryGardenMetCharacters}]");
        }
    }
}

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Replace))]
internal static class MomentOfReignUpgradeStatePatch
{
    private static void Prefix(
        RelicModel original,
        RelicModel replace)
    {
        if (original is MomentOfReign source &&
            replace is BecomeGodHbrgoSaki upgrade)
        {
            upgrade.InheritStateFrom(source);
        }
    }
}

[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.AfterObtained))]
internal static class TouchOfOrobasConvertSixSensePatch
{
    private readonly record struct ConversionState(
        Player Owner,
        int RewardCount);

    private static void Prefix(
        TouchOfOrobas __instance,
        out ConversionState __state)
    {
        __state = default;
        Player owner = __instance.Owner;
        if (owner == null)
        {
            return;
        }

        RelicModel starterRelic = __instance.StarterRelic != null
            ? owner.Relics.FirstOrDefault(
                relic => relic.Id == __instance.StarterRelic)
            : owner.Relics.FirstOrDefault(
                relic => relic.Rarity == RelicRarity.Starter);
        if (starterRelic is not ElectronicSoldierManual31O manual)
        {
            return;
        }

        int rewardCount = (int)System.Math.Min(int.MaxValue,
            manual.SixSensePoints + ElectronicSoldierManual.AllSixSenseAttributes
                .Sum(attribute => (long)manual.GetSenseAmount(attribute)));
        __state = new ConversionState(owner, rewardCount);
    }

    private static void Postfix(
        ConversionState __state,
        ref Task __result)
    {
        if (__state.Owner != null)
        {
            __result = StoreConvertedRewards(__result, __state);
        }
    }

    private static async Task StoreConvertedRewards(
        Task original,
        ConversionState state)
    {
        await original;
        Ketsu ketsu = state.Owner.GetRelic<Ketsu>();
        if (ketsu == null)
        {
            return;
        }

        ketsu.SetConvertedSixSenseRewards(state.RewardCount);
        await ketsu.OfferConvertedSixSenseRewardsImmediately();
    }
}
