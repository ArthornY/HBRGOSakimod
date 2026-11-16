using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class MatchaPawPrintDamagePatch
{
    private static MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(object[] __args, CardModel cardSource)
    {
        if (cardSource is MatchaParfaitHbrgoSaki)
            RanaByakkoCombatHelper.BeforeMatchaDamage(cardSource, NativeDamageApiCompatibility.ReadTargets(__args));
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRewards))]
internal static class MatchaWildCatRewardPatch
{
    private static void Postfix(Player player, List<Reward> rewards, AbstractRoom room) =>
        RanaByakkoCombatHelper.AddWildCatRewards(player, rewards, room);
}
