using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.GainBlockInternal))]
internal static class MomentOfReignGainBlockPatch
{
    private static bool Prefix(Creature __instance, decimal amount)
    {
        if (__instance.HasPower<PhantomPower>() ||
            __instance.Player?.GetRelic<MomentOfReign>() == null)
        {
            return true;
        }

        Creature phantom = PhantomHelper.GetLivingPhantom(
            __instance.Player);
        if (phantom == null || ReferenceEquals(phantom, __instance))
        {
            return true;
        }

        phantom.GainBlockInternal(amount);
        return false;
    }
}

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
        if (HpOnlyDamageContext.BypassesDpForBlockReceiver(__instance))
        {
            if (!PhantomDamageContext.IsRedirectingBlock)
                PhantomDamageContext.PopForBlockResolution();
            __result = 0m;
            return false;
        }
        if (!PhantomDamageContext.IsRedirectingBlock)
        {
            Creature phantom =
                PhantomDamageContext.PopForBlockResolution();
            if (phantom != null &&
                phantom != __instance &&
                phantom.PetOwner?.Creature == __instance)
            {
                PhantomDamageContext.IsRedirectingBlock = true;
                try
                {
                    __result = phantom.DamageBlockInternal(
                        amount,
                        props);
                }
                finally
                {
                    PhantomDamageContext.IsRedirectingBlock = false;
                }

                return false;
            }
        }

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

        // Direct HP-loss effects are usually marked Unblockable. They still
        // pass through DP so having DP cannot kill a low-HP character.
        __state = true;
        return true;
    }

    private static void Postfix(
        Creature __instance,
        decimal amount,
        ValueProp props,
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

        HighDimensionalShellPower shell =
            __instance.GetPower<HighDimensionalShellPower>();
        bool shellCanAbsorb = shell is not
                PhantomHighDimensionalShellPower ||
            PhantomHighDimensionalShellPower.CanAbsorb(props);
        if (shellCanAbsorb &&
            shell?.TryAbsorb(remainingDamage) == true)
        {
            HpOnlyDamageContext.RecordDpAbsorption(__instance, shell.LastAbsorbedDamage);
            __instance.PetOwner?.GetRelic<MomentOfReign>()?
                .RecordPhantomDamage(__instance, props);
            if (PhantomHelper.IsOwnedMainPhantom(__instance, __instance.PetOwner))
            {
                __instance.PetOwner.Creature.GetPower<ProofThroughPainPower>()?
                    .QueuePhantomDamage();
                __instance.PetOwner.Creature.GetPower<PainResonancePower>()?
                    .QueuePhantomDamage();
            }
            __result += remainingDamage;
            return;
        }

        DeflectorProtection deflectorProtection = __instance.Player?.GetRelic<DeflectorProtection>();
        if (deflectorProtection == null)
        {
            return;
        }

        // Buffer resolves in the later HP-loss hook. Let damage that would
        // reach DP continue there so Buffer can negate it before DP is spent.
        if (__instance.HasPower<BufferPower>())
        {
            return;
        }

        MagicSwordModePower magicSwordMode = __instance.GetPower<MagicSwordModePower>();
        if (magicSwordMode != null &&
            deflectorProtection.WouldDpBeDepletedBy(remainingDamage) &&
            deflectorProtection.TryUseMagicSwordBreakProtection())
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
            (int)Math.Min(decimal.Floor(amount), 999999999m));
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
