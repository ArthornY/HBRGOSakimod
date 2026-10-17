using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace HBRGOSakiMod.Core.Helpers;

// The combat bonus is a view over the original values. Keeping it out of the
// stored base values prevents clones, upgrades and save data from inheriting it.
internal static class HotaruMyHappinessCardValueHelper
{
    private sealed class DisplayState
    {
        internal decimal EnchantedIncludedBonus;
        internal decimal PreviewIncludedBonus;
    }

    internal sealed class Scope : IDisposable
    {
        private readonly bool _raw;
        private bool _disposed;

        internal Scope(bool raw)
        {
            _raw = raw;
            if (raw) RawValueDepth.Value++;
            else DisplayCalculationDepth.Value++;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_raw) RawValueDepth.Value--;
            else DisplayCalculationDepth.Value--;
        }
    }

    private const decimal NativeMaximum = 999_999_999m;
    private static readonly AccessTools.FieldRef<DynamicVar, AbstractModel> VariableOwner =
        AccessTools.FieldRefAccess<DynamicVar, AbstractModel>("_owner");
    private static readonly AccessTools.FieldRef<DynamicVar, decimal> StoredBaseValue =
        AccessTools.FieldRefAccess<DynamicVar, decimal>("_baseValue");
    private static readonly ConditionalWeakTable<DynamicVar, DisplayState> DisplayStates = new();
    private static readonly AsyncLocal<int> RawValueDepth = new();
    private static readonly AsyncLocal<int> DisplayCalculationDepth = new();

    internal static Scope ReadRawValues() => new(raw: true);
    internal static Scope CalculateDisplayValues() => new(raw: false);
    internal static decimal ReadStoredBaseValue(DynamicVar variable) => StoredBaseValue(variable);

    internal static decimal GetBonus(DynamicVar variable)
    {
        // Calculated variables already read the independently boosted input
        // variables. Adding another bonus to their result would count it twice.
        if (RawValueDepth.Value > 0 || variable is CalculatedVar or IComputedDynamicVar ||
            VariableOwner(variable) is not CardModel { IsMutable: true } card ||
            card.Owner?.Creature?.CombatState is not { } combat ||
            !ReferenceEquals(card.CombatState, combat) || card.Pile?.Type == PileType.Deck ||
            card.Owner.Deck.Cards.Contains(card)) return 0m;

        decimal bonus = Math.Max(0m, card.Owner.Creature.GetPower<HotaruMyHappinessPower>()
            ?.GetCardBaseValueBonus(card) ?? 0m);
        decimal stored = StoredBaseValue(variable);
        // A capped input contributes only the increase it can actually retain.
        // The display receipt must use that same increase at the native ceiling.
        return AddClamped(stored, bonus, NativeMaximum) - stored;
    }

    internal static decimal BaseValue(DynamicVar variable, decimal stored) =>
        AddClamped(stored, GetBonus(variable), NativeMaximum);

    internal static decimal EnchantedValue(DynamicVar variable, decimal stored)
    {
        decimal included = DisplayStates.TryGetValue(variable, out DisplayState state)
            ? state.EnchantedIncludedBonus : 0m;
        return AddClamped(stored, GetBonus(variable) - included, NativeMaximum);
    }

    internal static decimal PreviewValue(DynamicVar variable, decimal stored)
    {
        decimal included = DisplayStates.TryGetValue(variable, out DisplayState state)
            ? state.PreviewIncludedBonus : 0m;
        decimal maximum = variable is DamageVar or CalculatedDamageVar or OstyDamageVar
            ? DamageLimitHelper.MaximumSingleHitDamage : NativeMaximum;
        return AddClamped(stored, GetBonus(variable) - included, maximum);
    }

    internal static void RecordEnchantedWrite(DynamicVar variable) =>
        DisplayStates.GetValue(variable, static _ => new DisplayState()).EnchantedIncludedBonus =
            DisplayCalculationDepth.Value > 0 ? GetBonus(variable) : 0m;

    internal static void RecordPreviewWrite(DynamicVar variable) =>
        DisplayStates.GetValue(variable, static _ => new DisplayState()).PreviewIncludedBonus =
            DisplayCalculationDepth.Value > 0 ? GetBonus(variable) : 0m;

    private static decimal AddClamped(decimal value, decimal bonus, decimal maximum) =>
        Math.Min(maximum, DamageAmplificationLimits.AddSaturated(value, bonus));
}
