using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock), new[]
    { typeof(Creature), typeof(BlockVar), typeof(CardPlay), typeof(bool) })]
internal static class HbrNativeAnchorBlockSkillPatch
{
    private static bool Prefix(Creature creature, BlockVar blockVar, CardPlay cardPlay, bool fast,
        ref Task<decimal> __result)
    {
        if (!HbrNativeEffectSkillHelper.TryResolveNativeBlock(blockVar, creature, out decimal amount)) return true;
        // The native typed adapter only forwards BaseValue/Props. Keep the
        // complete decimal command, original flags, hooks and actual gain.
        __result = CreatureCmd.GainBlock(creature, amount, blockVar.Props, cardPlay, fast);
        return false;
    }
}
