using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomHealthDamageCapHelper
{
    private static readonly FieldInfo InternalData = AccessTools.Field(typeof(PowerModel), "_internalData");
    private static readonly FieldInfo ReceivedThisTurn = AccessTools.Field(
        typeof(HardenedShellPower).GetNestedType("Data", BindingFlags.NonPublic), "damageReceivedThisTurn");

    internal static bool TryGetMaximum(PowerModel power, out decimal maximum)
    {
        maximum = 0m;
        if (power is not (HardToKillPower or HardenedShellPower) || !power.IsMutable ||
            power.Owner is not { Side: CombatSide.Player, PetOwner: { } owner } phantom ||
            !PhantomHelper.IsOwnedMainPhantom(phantom, owner)) return false;
        string effects = PhantomHelper.GetFusionEffectData(phantom);
        PhantomFusionEffectType type = power is HardToKillPower
            ? PhantomFusionEffectType.Exoskeleton : PhantomFusionEffectType.SkulkingColony;
        if (!PhantomFusionEffectHelper.HasEffect(effects, type)) return false;
        // Read the actual saved direct/inherited count, including legacy bonuses.
        // Native Amount is a bounded badge and is not a scaling-ratio signal.
        decimal percent = PhantomFusionEffectHelper.GetEffect(effects, type,
            power is HardToKillPower ? 30m : 60m, power is HardToKillPower ? -5m : -10m);
        maximum = Math.Max(1m, BigCombatNumbers.ClampStat(
            LargeCreatureHealthHelper.GetMaxHp(phantom) * percent / 100m));
        return maximum > BigCombatNumbers.NativeStatCap;
    }

    internal static decimal GetHardenedMaximum(PowerModel power) =>
        TryGetMaximum(power, out decimal maximum) ? maximum : power.Amount;

    internal static decimal GetReceivedThisTurn(HardenedShellPower power) =>
        InternalData.GetValue(power) is { } data ? (decimal)ReceivedThisTurn.GetValue(data) : 0m;
}
