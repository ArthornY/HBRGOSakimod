using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class TeamCollectionRelics
{
    // Award the marker on the first permanent member card. It remains visible
    // at zero if the last member is removed, and is never a random relic reward.
    internal static async Task EnsureObtained(Player player)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() == null) return;
        if (ThirtyAKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyACollection>() == null)
            await RelicCmd.Obtain<ThirtyACollection>(player);
        if (ThirtyOneAKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneACollection>() == null)
            await RelicCmd.Obtain<ThirtyOneACollection>(player);
        if (CryChicKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<CryChicCollection>() == null)
            await RelicCmd.Obtain<CryChicCollection>(player);
        if (ThirtyOneBKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneBCollection>() == null)
            await RelicCmd.Obtain<ThirtyOneBCollection>(player);
        if (ThirtyOneCKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneCCollection>() == null)
            await RelicCmd.Obtain<ThirtyOneCCollection>(player);
        if (ThirtyOneDKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneDCollection>() == null)
            await RelicCmd.Obtain<ThirtyOneDCollection>(player);
        if (ThirtyOneEKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneECollection>() == null)
            await RelicCmd.Obtain<ThirtyOneECollection>(player);
        if (ThirtyOneFKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneFCollection>() == null)
            await RelicCmd.Obtain<ThirtyOneFCollection>(player);
        if (ThirtyOneXKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyOneXCollection>() == null)
            await RelicCmd.Obtain<ThirtyOneXCollection>(player);
        if (ThirtyGKeywordRules.GetPermanentDistinctCount(player) > 0 &&
            player.GetRelic<ThirtyGCollectionRelic>() == null)
            await RelicCmd.Obtain<ThirtyGCollectionRelic>(player);
        if (ThirtyOneBBondHelper.SnackKinds(player) > 0 &&
            player.GetRelic<TigersMouthSnackBond>() == null)
            await RelicCmd.Obtain<TigersMouthSnackBond>(player);
        if (ThirtyOneBBondHelper.SisterKinds(player) > 0 &&
            player.GetRelic<FamilyIsLikeThisBond>() == null)
            await RelicCmd.Obtain<FamilyIsLikeThisBond>(player);
        if (DemonKingAndFamiliarBondHelper.GetKinds(player) > 0 &&
            player.GetRelic<DemonKingAndFamiliarBond>() == null)
            await RelicCmd.Obtain<DemonKingAndFamiliarBond>(player);
        RefreshCounters(player);
        ChieLitaState.OnPermanentDeckChanged(player);
    }

    // Loading an older run must not open reward UI or require a running scene.
    // Reconstruct only the newly introduced relics; keep the existing saved
    // milestone receipts and rest-story readiness on the manual unchanged.
    internal static void Restore(Player player)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() == null) return;
        if (ThirtyAKeywordRules.GetPermanentDistinctCount(player) > 0)
            Restore<ThirtyACollection>(player);
        if (ThirtyOneAKeywordRules.GetPermanentDistinctCount(player) > 0)
            Restore<ThirtyOneACollection>(player);
        if (CryChicKeywordRules.GetPermanentDistinctCount(player) > 0)
            Restore<CryChicCollection>(player);
        RefreshCounters(player);
    }

    private static void Restore<T>(Player player) where T : TeamCollectionRelic
    {
        if (player.GetRelic<T>() != null) return;
        var relic = (T)ModelDb.Relic<T>().ToMutable();
        relic.FloorAddedToDeck = player.RunState.TotalFloor;
        player.AddRelicInternal(relic);
    }

    internal static void RefreshCounters(Player player)
    {
        player?.GetRelic<ThirtyACollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneACollection>()?.RefreshCounter();
        player?.GetRelic<CryChicCollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneBCollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneCCollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneDCollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneECollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneFCollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyOneXCollection>()?.RefreshCounter();
        player?.GetRelic<ThirtyGCollectionRelic>()?.RefreshCounter();
        player?.GetRelic<TigersMouthSnackBond>()?.RefreshCounter();
        player?.GetRelic<FamilyIsLikeThisBond>()?.RefreshCounter();
        player?.GetRelic<DemonKingAndFamiliarBond>()?.RefreshCounter();
        HBRGOSakiMod.Core.Map.HbrThirtyOneDScoutingHelper.EnsureScouted(player?.RunState);
    }
}
