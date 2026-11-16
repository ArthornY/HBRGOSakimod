using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Acts;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace HBRGOSakiMod.Core.Patches;

internal static class ShieldLockEncounterHelper
{
    internal static bool HasFlatHandActFourBoss(IRunState runState)
    {
        return runState?.Acts
            .OfType<HbrgoActFour>()
            .FirstOrDefault()
            ?.BossEncounter is FlatHandBoss;
    }
}

[HarmonyPatch(
    typeof(RewardsSet),
    nameof(RewardsSet.WithRewardsFromRoom))]
internal static class ShieldLockEliteRewardPatch
{
    private static void Postfix(
        AbstractRoom room,
        RewardsSet __instance,
        ref RewardsSet __result)
    {
        if (room?.RoomType != RoomType.Elite ||
            __instance?.Player == null ||
            !ShieldLockEncounterHelper.HasFlatHandActFourBoss(
                __instance.Player.RunState) ||
            __result.Rewards.OfType<RelicReward>()
                .Any(reward => reward.Relic is ShieldLock))
        {
            return;
        }

        __result.Rewards.Add(
            NativeRewardApiCompatibility.Relic(
                ModelDb.Relic<ShieldLock>().ToMutable(),
                __instance.Player));
    }
}

[HarmonyPatch(
    typeof(RelicCmd),
    nameof(RelicCmd.Obtain),
    new[]
    {
        typeof(RelicModel),
        typeof(Player),
        typeof(int)
    })]
internal static class ShieldLockDuplicateObtainPatch
{
    private static bool Prefix(
        RelicModel relic,
        Player player,
        ref Task<RelicModel> __result)
    {
        if (relic is not ShieldLock || player == null)
        {
            return true;
        }

        ShieldLock existing = player.Relics
            .OfType<ShieldLock>()
            .FirstOrDefault();
        if (existing == null)
        {
            return true;
        }

        __result = GainCounter(existing, relic, player);
        return false;
    }

    private static Task<RelicModel> GainCounter(
        ShieldLock existing,
        RelicModel incoming,
        Player player)
    {
        player.RunState.CurrentMapPointHistoryEntry?
            .GetEntry(player.NetId)
            .RelicChoices.Add(
                new ModelChoiceHistoryEntry(
                    incoming.Id,
                    wasPicked: true));
        existing.GainCounter(1);
        return Task.FromResult<RelicModel>(existing);
    }
}
