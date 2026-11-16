using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class LargeCreatureHealthHelper
{
    private sealed record DeathDrain(decimal Amount);
    private static readonly ConditionalWeakTable<Creature, DeathDrain> DeathDrains = new();
    private static readonly Action<Creature, int> SetNativeCurrent =
        AccessTools.MethodDelegate<Action<Creature, int>>(
            AccessTools.PropertySetter(typeof(Creature), nameof(Creature.CurrentHp)));
    private static readonly Action<Creature, int> SetNativeMaximum =
        AccessTools.MethodDelegate<Action<Creature, int>>(
            AccessTools.PropertySetter(typeof(Creature), nameof(Creature.MaxHp)));

    internal static bool HasLargeHealth(Creature creature) =>
        creature?.Monster != null && creature.HasPower<LargeHealthPower>();

    internal static decimal GetCurrentHp(Creature creature) =>
        creature?.Monster != null && creature.GetPower<LargeHealthPower>() is { } health
            ? health.CurrentHp : (decimal)(creature?.CurrentHp ?? 0);

    internal static decimal GetMaxHp(Creature creature) =>
        creature?.Monster != null && creature.GetPower<LargeHealthPower>() is { } health
            ? health.MaxHp : (decimal)(creature?.MaxHp ?? 0);

    private static LargeHealthPower Ensure(Creature creature)
    {
        if (creature.GetPower<LargeHealthPower>() is { } existing) return existing;
        LargeHealthPower power = (LargeHealthPower)ModelDb.Power<LargeHealthPower>().ToMutable();
        power.Store(creature.CurrentHp, creature.MaxHp);
        power.ApplyInternal(creature, 1m, silent: true);
        return power;
    }

    internal static void SetMaxHp(Creature creature, decimal amount)
    {
        if (amount < 0m) throw new ArgumentException("amount must be non-negative.");
        decimal maximum = BigCombatNumbers.ClampStat(amount);
        decimal current = NararaLayeredHpHelper.BeforeMaximumWrite(
            creature, Math.Min(GetCurrentHp(creature), maximum), maximum, out bool broke);
        StoreAndProject(creature, current, maximum);
        NararaLayeredHpHelper.AfterHpWrite(creature, broke);
        HBRGOSakiMod.Core.Patches.Combat.TerrorEelHealthThreshold.Sync(creature);
        PhantomCeremonialBeastHelper.RefreshPlowThreshold(creature);
    }

    internal static void SetCurrentHp(Creature creature, decimal amount)
    {
        if (amount < 0m) throw new ArgumentException("Current HP must be positive", nameof(amount));
        decimal requested = Math.Min(BigCombatNumbers.ClampStat(amount), GetMaxHp(creature));
        decimal current = NararaLayeredHpHelper.BeforeHpWrite(creature, requested, out bool broke);
        StoreAndProject(creature, current, GetMaxHp(creature));
        NararaLayeredHpHelper.AfterHpWrite(creature, broke);
    }

    internal static void Heal(Creature creature, decimal amount) => creature.HealInternal(amount);

    internal static DamageResult LoseHp(Creature creature, decimal amount, ValueProp props)
    {
        decimal oldHp = GetCurrentHp(creature);
        decimal damage = decimal.Floor(Math.Clamp(amount, 0m,
            DamageLimitHelper.MaximumSingleHitDamage));
        decimal lost = Math.Min(oldHp, damage);
        SetCurrentHp(creature, oldHp - lost);
        return new DamageResult(creature, props)
        {
            UnblockedDamage = (int)lost,
            WasTargetKilled = oldHp > 0m && damage >= oldHp,
            OverkillDamage = (int)Math.Max(0m, damage - lost)
        };
    }

    private static void StoreAndProject(Creature creature, decimal current, decimal maximum)
    {
        if (creature?.Monster == null)
            throw new ArgumentException("Large health is only supported for monsters.", nameof(creature));
        LargeHealthPower power = Ensure(creature);
        power.Store(current, maximum);
        int proxyMax = (int)Math.Min(maximum, BigCombatNumbers.NativeStatCap);
        int proxyCurrent = maximum <= BigCombatNumbers.NativeStatCap
            ? (int)current
            : current <= 0m ? 0 : Math.Clamp((int)decimal.Ceiling(
                current / maximum * proxyMax), 1, proxyMax);
        // The original private setters retain their assertions and native events.
        // The authoritative ledger is committed first for reentrant observers.
        SetNativeMaximum(creature, proxyMax);
        SetNativeCurrent(creature, proxyCurrent);
    }

    // This adapter is injected only at the native death drain. A forced/native
    // death is not one card damage segment and must clear all real HP.
    internal static DamageResult LoseHpForKill(Creature creature, decimal amount, ValueProp props)
    {
        if (!HasLargeHealth(creature)) return creature.LoseHpInternal(amount, props);
        decimal oldHp = GetCurrentHp(creature);
        // Explicit native death/cleanup clears the whole ledger, rather than
        // recursively consuming one phase at a time through the damage shield.
        using (NararaLayeredHpHelper.BypassForKill()) SetCurrentHp(creature, 0m);
        DeathDrains.Remove(creature);
        DeathDrains.Add(creature, new DeathDrain(oldHp));
        return new DamageResult(creature, props)
        {
            UnblockedDamage = (int)Math.Min(oldHp, DamageLimitHelper.MaximumSingleHitDamage),
            WasTargetKilled = oldHp > 0m
        };
    }

    internal static Task AfterKillHpChanged(IRunState runState, ICombatState combatState,
        Creature creature, decimal delta)
    {
        if (DeathDrains.TryGetValue(creature, out DeathDrain drain))
        {
            DeathDrains.Remove(creature);
            delta = -drain.Amount;
        }
        return Hook.AfterCurrentHpChanged(runState, combatState, creature, delta);
    }

    // Used only by predicates whose complete expression is HP versus native
    // MaxHp/2. Int32 return keeps the rest of the original predicate unchanged.
    internal static int GetCurrentForHalfComparison(Creature creature) =>
        !HasLargeHealth(creature) ? creature.CurrentHp : creature.MaxHp / 2 +
            decimal.Compare(GetCurrentHp(creature), decimal.Floor(GetMaxHp(creature) / 2m));

    internal static int GetCurrentForShriekComparison(Creature creature) =>
        !HasLargeHealth(creature) ? creature.CurrentHp :
            (creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.ShriekPower>()?.Amount ?? 0) +
            decimal.Compare(GetCurrentHp(creature), decimal.Floor(GetMaxHp(creature) / 2m));

    internal static int GetCurrentForAbsoluteComparison(Creature creature) =>
        (int)Math.Min(GetCurrentHp(creature), int.MaxValue);

    internal static int GetCurrentForPlowComparison(Creature creature)
    {
        if (!HasLargeHealth(creature)) return creature.CurrentHp;
        int amount = creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.PlowPower>()?.Amount ?? 0;
        decimal threshold = PhantomCeremonialBeastHelper.IsPhantomBeast(creature)
            ? PhantomCeremonialBeastHelper.GetRealPlowThreshold(creature) : amount;
        return amount + decimal.Compare(GetCurrentHp(creature), threshold);
    }

    internal static decimal SubtractPunchStartingHp(Creature creature, int reduction) =>
        Math.Max(1m, GetCurrentHp(creature) - reduction);
}
