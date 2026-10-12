using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage))]
internal static class PhantomIncomingIntentPatch
{
    private static bool Prefix(AttackIntent __instance, Creature owner, ref int __result)
    {
        if (owner?.CombatState == null) return true;

        // Native intents preview damage against the local player even in a
        // multiplayer battle. Actual attacks redirect that player to their
        // living phantom before running the damage hooks; use the same defender.
        var player = LocalContext.GetMe(owner.CombatState);
        if (player?.Creature == null || owner.Side == player.Creature.Side)
            return true;

        Creature defender = PhantomHelper.GetDamageDefender(player);
        if (defender == null) return true;

        // Shield roll notifications can refresh intents synchronously while the
        // actual hit is still resolving. A preview must never roll/consume shields.
        StrongestShieldDamagePatch.BeginIntentPreview();
        try
        {
            decimal damage = HookCompatibility.ModifyDamage(
                player.RunState, owner.CombatState, defender, owner,
                __instance.DamageCalc(), ValueProp.Move, null,
                ModifyDamageHookType.All, CardPreviewMode.None, out _);
            __result = Math.Max(0, (int)damage);
        }
        finally
        {
            StrongestShieldDamagePatch.EndIntentPreview();
        }
        // Labels, multi-hit totals, tooltip text and attack icons all call this
        // method. Block and DP are resolved afterwards by the existing combat path.
        return false;
    }
}
