using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class CreatureDamageBlockInternalPatch
{
    private static bool Prefix(
        Creature __instance,
        decimal amount,
        ValueProp props,
        ref decimal __result,
        out bool __state)
    {
        __state = false;
        DamageTransferPower damageTransfer =
            __instance.GetPower<DamageTransferPower>();
        Creature transferTarget =
            damageTransfer?.GetLivingRedCrimson();
        if (transferTarget != null &&
            transferTarget != __instance)
        {
            __result = transferTarget.DamageBlockInternal(
                amount,
                props);
            return false;
        }

        Creature redCrimson =
            RedCrimsonDamageContext.Target;
        if (redCrimson != null &&
            redCrimson != __instance &&
            redCrimson.PetOwner?.Creature == __instance)
        {
            __result = redCrimson.DamageBlockInternal(
                amount,
                props);
            return false;
        }

        if (amount <= 0m)
        {
            return true;
        }

        if (props.HasFlag(ValueProp.Unblockable))
        {
            return true;
        }

        __state = true;
        return true;
    }

    private static void Postfix(
        Creature __instance,
        decimal amount,
        ref decimal __result,
        bool __state)
    {
        if (!__state)
        {
            return;
        }

        decimal remainingDamage = Math.Max(amount - __result, 0m);
        if (remainingDamage <= 0m)
        {
            return;
        }

        HighDimensionalShellPower shell = __instance.GetPower<HighDimensionalShellPower>();
        if (shell?.TryAbsorb(remainingDamage) == true)
        {
            __result += remainingDamage;
            return;
        }

        DeflectorProtection deflectorProtection = __instance.Player?.GetRelic<DeflectorProtection>();
        if (deflectorProtection == null)
        {
            return;
        }

        MagicSwordModePower magicSwordMode = __instance.GetPower<MagicSwordModePower>();
        if (magicSwordMode != null && deflectorProtection.WouldDpBeDepletedBy(remainingDamage))
        {
            DisabledPower.Clear(__instance);
            deflectorProtection.RestoreDpToAtLeastMaxPercent(0.5m);
            __result += remainingDamage;
            magicSwordMode.MarkDpBrokenByDamage();
            return;
        }

        decimal dpAbsorbed = deflectorProtection.AbsorbDp(remainingDamage);
        if (dpAbsorbed > 0m)
        {
            __result += remainingDamage;
        }
    }
}

internal static class RedCrimsonDamageContext
{
    internal static Creature Target { get; set; }

    internal static void Set(Creature target)
    {
        Target = target?.Monster is RedCrimson
            ? target
            : null;
    }

    internal static void Clear(Creature target)
    {
        if (Target == target)
        {
            Target = null;
        }
    }
}

[HarmonyPatch(
    typeof(MegaCrit.Sts2.Core.Hooks.Hook),
    nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforeDamageReceived))]
internal static class RedCrimsonBeforeDamageReceivedPatch
{
    private static void Prefix(Creature target)
    {
        RedCrimsonDamageContext.Set(target);
    }
}

[HarmonyPatch(
    typeof(MegaCrit.Sts2.Core.Hooks.Hook),
    nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterDamageReceived))]
internal static class RedCrimsonAfterDamageReceivedPatch
{
    private static void Prefix(Creature target)
    {
        RedCrimsonDamageContext.Clear(target);
    }
}

[HarmonyPatch(
    typeof(MegaCrit.Sts2.Core.Hooks.Hook),
    nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforeDeath))]
internal static class RedCrimsonBeforeDeathPatch
{
    private static void Prefix(Creature creature)
    {
        RedCrimsonDamageContext.Clear(creature);
        if (creature?.Monster is RedCrimson redCrimson)
        {
            redCrimson.OnBeforeDeath();
        }
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp))]
internal static class CreatureCmdGainMaxHpPatch
{
    private static bool Prefix(
        Creature creature,
        decimal amount,
        ref Task __result)
    {
        DeflectorProtection protection =
            creature.Player?.GetRelic<DeflectorProtection>();
        if (protection == null || amount <= 0m)
        {
            return true;
        }

        protection.GainMaxDp(
            Math.Min((int)amount, 999999999));
        __result = Task.CompletedTask;
        return false;
    }
}

[
    HarmonyPatch(
        typeof(Creature),
        nameof(Creature.SetMaxHpInternal))
]
internal static class CreatureSetMaxHpInternalPatch
{
    private static void Prefix(
        Creature __instance,
        out int __state)
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
