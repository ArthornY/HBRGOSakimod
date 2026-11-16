using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Read-only: lets the Lucky Seven finisher draw a slash on each real hit of the card it is showing. It reads
// the hook's arguments and changes nothing, the same way the run recorder observes damage.
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived))]
internal static class HbrLuckySevenFinisherHitPatch
{
    private static void Prefix(Creature target, DamageResult result, CardModel cardSource) =>
        HbrLuckySevenFinisher.ObserveHit(target, result, cardSource);
}
