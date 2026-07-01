using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class CreatureDamageBlockInternalPatch
{
    private static void Prefix(Creature __instance, ref decimal amount, ValueProp props, out decimal __state)
    {
        __state = 0m;
        if (amount <= 0m)
        {
            return;
        }

        if (props.HasFlag(ValueProp.Unblockable))
        {
            return;
        }

        DeflectorProtection deflectorProtection = __instance.Player?.GetRelic<DeflectorProtection>();
        if (deflectorProtection == null)
        {
            return;
        }

        if (DodgePower.TrySpend(__instance, amount))
        {
            __state += amount;
            amount = 0m;
            return;
        }

        MagicSwordModePower magicSwordMode = __instance.GetPower<MagicSwordModePower>();
        if (magicSwordMode != null && deflectorProtection.WouldDpBeDepletedBy(amount))
        {
            DisabledPower.Clear(__instance);
            deflectorProtection.RestoreDpToAtLeastMaxPercent(0.5m);
            __state += amount;
            amount = 0m;
            magicSwordMode.MarkDpBrokenByDamage();
            return;
        }

        decimal incomingDamage = amount;
        decimal dpAbsorbed = deflectorProtection.AbsorbDp(incomingDamage);
        if (dpAbsorbed > 0m)
        {
            amount = 0m;
            __state += incomingDamage;
        }
    }

    private static void Postfix(ref decimal __result, decimal __state)
    {
        if (__state > 0m)
        {
            __result += __state;
        }
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class CreatureSetMaxHpInternalPatch
{
    private static void Prefix(Creature __instance, out int __state)
    {
        __state = __instance.MaxHp;
    }

    private static void Postfix(Creature __instance, int __state)
    {
        if (__instance.MaxHp == __state)
        {
            return;
        }

        __instance.Player?.GetRelic<DeflectorProtection>()?.SyncAfterMaxHpChanged(__state, __instance.MaxHp);
    }
}
