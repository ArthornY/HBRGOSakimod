using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Explicitly audited effect damage carries native skill units through the
// canonical command. Native flags, attribution and before/after hooks stay
// unchanged; only this command's calculation may reinterpret those units.
internal static class HbrEffectSkillDamageContext
{
    private static readonly AsyncLocal<Scope> Pending = new();
    private static readonly AsyncLocal<CommandScope> CurrentCommand = new();
    private static readonly AsyncLocal<CalculationScope> CurrentCalculation = new();

    internal sealed class Scope : IDisposable
    {
        internal Scope Previous;
        internal Creature Target;
        internal Creature Dealer;
        internal Creature StatSource;
        internal CardModel Card;
        internal decimal RawUnits;
        internal ValueProp Props;
        internal bool Active = true;
        internal int Claimed;
        internal bool IsWorldAmount;
        internal bool IsEnvironmentalSelfDamage;

        internal bool Matches(Creature target, Creature dealer, CardModel card, ValueProp props) =>
            Active && (Target == null || ReferenceEquals(Target, target)) &&
            ReferenceEquals(Dealer, dealer) && ReferenceEquals(Card, card) && Props == props;

        public void Dispose()
        {
            if (!Active) return;
            Volatile.Write(ref Active, false);
            if (ReferenceEquals(Pending.Value, this)) Pending.Value = Previous;
        }
    }

    internal sealed class CommandScope
    {
        internal CommandScope Previous;
        internal Scope Intent;
        internal bool Enabled;
        internal bool Active = true;
        internal int? CriticalSample;
        internal readonly List<HbrEffectPower> CountedEffects = new();
    }

    internal sealed class CalculationScope : IDisposable
    {
        internal CalculationScope Previous;
        internal Scope Intent;
        internal CommandScope Command;
        internal decimal InputDamage;
        internal bool Enabled;
        internal bool BaseClaimed;
        internal bool CriticalResolved;
        internal bool WasCritical;
        internal bool Active = true;

        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            if (ReferenceEquals(CurrentCalculation.Value, this)) CurrentCalculation.Value = Previous;
        }
    }

    internal static Scope Begin(Creature target, Creature nativeDealer, CardModel card,
        decimal rawUnits, ValueProp nativeProps, Creature statSource = null)
    {
        Scope scope = new()
        {
            Previous = Pending.Value, Target = target, Dealer = nativeDealer,
            StatSource = statSource ?? nativeDealer, Card = card, RawUnits = rawUnits, Props = nativeProps
        };
        Pending.Value = scope;
        return scope;
    }

    // Reflections and resource-derived damage already use combat-world units.
    // Keep the native request intact; normalize once at the shared formula boundary.
    internal static Scope BeginWorld(Creature target, Creature nativeDealer, CardModel card,
        decimal worldAmount, ValueProp nativeProps, Creature statSource = null)
    {
        Scope scope = Begin(target, nativeDealer, card, worldAmount, nativeProps, statSource);
        scope.IsWorldAmount = true;
        return scope;
    }

    // The native card overload attributes a status tick to its owner's
    // creature. Preserve that attribution without borrowing its attributes,
    // outgoing buffs, attack charges or critical chance for the tick's math.
    internal static Scope BeginEnvironmental(Creature target, Creature nativeDealer, CardModel card,
        decimal rawUnits, ValueProp nativeProps)
    {
        Scope scope = Begin(target, nativeDealer, card, rawUnits, nativeProps);
        scope.StatSource = null;
        scope.IsEnvironmentalSelfDamage = true;
        return scope;
    }

    internal static bool IsCurrentEnvironmentalSelfDamage => IsCurrentEffectCalculation &&
        CurrentCalculation.Value.Intent.IsEnvironmentalSelfDamage;

    internal static bool IsCurrentWorldBasis => IsCurrentEffectCalculation &&
        CurrentCalculation.Value.Intent.IsWorldAmount;

    internal static CommandScope BeginCommand(ref IEnumerable<Creature> targets, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        Scope intent = Pending.Value;
        CommandScope previous = CurrentCommand.Value;
        if (intent?.Active != true && previous?.Active != true) return null;

        bool enabled = intent?.Active == true && Volatile.Read(ref intent.Claimed) == 0 &&
            intent.RawUnits == amount && intent.Props == props &&
            ReferenceEquals(intent.Dealer, dealer) && ReferenceEquals(intent.Card, cardSource);
        if (enabled && intent.Target != null)
        {
            IReadOnlyList<Creature> candidates = targets as IReadOnlyList<Creature>;
            if (candidates == null && targets != null) targets = candidates = targets.ToArray();
            enabled = candidates?.Count == 1 && ReferenceEquals(candidates[0], intent.Target);
        }
        if (enabled) enabled = Interlocked.CompareExchange(ref intent.Claimed, 1, 0) == 0 && intent.Active;
        // Even an unknown nested command shadows its parent's enabled scope.
        CommandScope command = new() { Previous = previous, Intent = intent, Enabled = enabled };
        if (enabled && intent.StatSource is { } source && HbrBattleStatsHelper.IsEnabled(source))
            foreach (HbrEffectPower effect in source.Powers.OfType<HbrEffectPower>())
                if (effect.TryReserveEffect(command, source)) command.CountedEffects.Add(effect);
        CurrentCommand.Value = command;
        return command;
    }

    internal static CalculationScope EnterCalculation(Creature target, Creature dealer,
        CardModel cardSource, decimal damage, ValueProp props)
    {
        CommandScope command = CurrentCommand.Value;
        bool liveCommand = command?.Active == true;
        Scope pending = Pending.Value;
        // A new explicit preview inside another command is its own source,
        // rather than an implicit descendant of that command's damage intent.
        bool directPreview = pending?.Active == true && Volatile.Read(ref pending.Claimed) == 0 &&
            (!liveCommand || !ReferenceEquals(pending, command.Intent));
        Scope intent = directPreview || !liveCommand ? pending : command.Intent;
        bool commandCalculation = liveCommand && !directPreview;
        CalculationScope previous = CurrentCalculation.Value;
        if (intent?.Active != true && !liveCommand && previous?.Active != true) return null;

        bool matches = intent?.Matches(target, dealer, cardSource, props) == true &&
            (commandCalculation
                ? command.Enabled && StrongestShieldDamagePatch.IsResolvingActualDamage
                : Volatile.Read(ref intent.Claimed) == 0 && intent.RawUnits == damage);
        bool nestedSameIntent = previous?.Active == true && ReferenceEquals(previous.Intent, intent);
        bool hbr = matches && (intent.StatSource != null
            ? HbrBattleStatsHelper.IsEnabled(intent.StatSource)
            : HbrBattleStatsHelper.IsEnabled(target));
        CalculationScope calculation = new()
        {
            Previous = previous, Intent = intent, Command = commandCalculation ? command : null,
            InputDamage = damage,
            Enabled = hbr && !nestedSameIntent
        };
        CurrentCalculation.Value = calculation;
        return calculation;
    }

    internal static bool IsCurrentEffectCalculation => CurrentCalculation.Value is
        { Active: true, Enabled: true, Intent: { Active: true } };

    internal static bool HasEffectCommand => CurrentCalculation.Value is
        { Active: true, Command: { Active: true, Enabled: true } };

    internal static bool TryGetStatSource(out Creature source)
    {
        source = IsCurrentEffectCalculation ? CurrentCalculation.Value.Intent.StatSource : null;
        return IsCurrentEffectCalculation;
    }

    internal static bool TryClaimBaseConversion(Creature target, Creature dealer, CardModel card,
        decimal damage, ValueProp props, out Creature source)
    {
        source = null;
        CalculationScope calculation = CurrentCalculation.Value;
        if (!IsCurrentEffectCalculation || calculation.BaseClaimed || calculation.InputDamage != damage ||
            !calculation.Intent.Matches(target, dealer, card, props)) return false;
        calculation.BaseClaimed = true;
        source = calculation.Intent.StatSource;
        return true;
    }

    internal static ValueProp GetPersistentEffectProps(ValueProp nativeProps) =>
        IsCurrentEffectCalculation && !IsCurrentEnvironmentalSelfDamage &&
        CurrentCalculation.Value.Intent.Props == nativeProps
            ? (nativeProps & ~ValueProp.Unpowered) | ValueProp.Move : nativeProps;

    // Arithmetic hooks may ask an existing source context to identify a hit.
    // Normalize only that scoped arithmetic view, never the real command.
    internal static ValueProp GetNativeEffectProps(ValueProp arithmeticProps)
    {
        if (!IsCurrentEffectCalculation) return arithmeticProps;
        ValueProp nativeProps = CurrentCalculation.Value.Intent.Props;
        return arithmeticProps == ((nativeProps & ~ValueProp.Unpowered) | ValueProp.Move)
            ? nativeProps : arithmeticProps;
    }

    internal static ValueProp GetModifierProps(AbstractModel model, ValueProp nativeProps)
    {
        // Native attack consumables (Vigor, Pen Nib and similar powers) must
        // see native flags just like their later consumption hooks do.
        return model != null && (model.GetType().Assembly == typeof(HbrEffectSkillDamageContext).Assembly ||
            model is WeakPower or VulnerablePower)
            ? GetPersistentEffectProps(nativeProps) : nativeProps;
    }

    internal static bool IsCritical(decimal luck, decimal enemyParameter)
    {
        CalculationScope calculation = CurrentCalculation.Value;
        if (!IsCurrentEffectCalculation || calculation.Command is not { Active: true, Enabled: true } command ||
            !ReferenceEquals(CurrentCommand.Value, command) ||
            !StrongestShieldDamagePatch.IsResolvingActualDamage || HotaruCannonStatsHelper.IsDamagePreview ||
            HotaruCannonStatsHelper.IsReactionDamage)
            return false;
        Creature source = calculation.Intent.StatSource;
        if (source == null || EnemyStatScalingHelper.IsEnemy(source)) return false;
        var run = source.CombatState?.RunState;
        if (run == null) return false;
        int sample;
        lock (command)
        {
            command.CriticalSample ??= run.Rng.Niche.NextInt(10000);
            sample = command.CriticalSample.Value;
        }
        decimal bonus = DamageAmplificationLimits.AddSaturated(
            ElectronicSoldierManual.GetEquipmentCriticalRate(source),
            HBRGOSakiMod.Core.Models.Powers.HbrCriticalRateUpPower.GetTotalPercent(source));
        bool critical = sample <
            HbrOriginalNumericFormula.CriticalProbability(luck, enemyParameter,
                bonus) * 10000m;
        calculation.CriticalResolved = true;
        calculation.WasCritical = critical;
        return critical;
    }

    internal static decimal GetCriticalDamageMultiplier(Creature target, Creature dealer,
        CardModel card, ValueProp props)
    {
        CalculationScope calculation = CurrentCalculation.Value;
        return IsCurrentEffectCalculation && calculation.CriticalResolved && calculation.WasCritical &&
            calculation.Intent.Matches(target, dealer, card, props)
                ? HBRGOSakiMod.Core.Models.Powers.HbrCriticalDamageUpPower.GetMultiplier(calculation.Intent.StatSource)
                : 1m;
    }

    internal static void RestoreCaller(CommandScope scope)
    {
        if (scope != null && ReferenceEquals(CurrentCommand.Value, scope)) CurrentCommand.Value = scope.Previous;
    }

    internal static void Close(CommandScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        foreach (HbrEffectPower effect in scope.CountedEffects) effect.ReleaseEffectAttack(scope);
        RestoreCaller(scope);
    }

    internal static async Task<IEnumerable<DamageResult>> Finish(Task<IEnumerable<DamageResult>> original,
        CommandScope scope)
    {
        try
        {
            IEnumerable<DamageResult> results = await original;
            if (scope is { Enabled: true, Active: true } && scope.CountedEffects.Count > 0)
            {
                DamageResult[] recorded = results?.ToArray() ?? Array.Empty<DamageResult>();
                foreach (HbrEffectPower effect in scope.CountedEffects)
                    await effect.CompleteEffectAttack(scope, recorded.Length > 0);
                return recorded;
            }
            return results;
        }
        finally { Close(scope); }
    }
}
