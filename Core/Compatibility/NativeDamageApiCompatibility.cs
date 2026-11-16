using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Compatibility;

// One boundary per native command. In 0.113 the Creature overload only forwards
// to the async target-data overload, which native AttackCommand also calls
// directly. Selecting only that leaf avoids both missed and double scopes.
internal static class NativeDamageApiCompatibility
{
    private const string TargetDataName = "MegaCrit.Sts2.Core.Entities.Creatures.DamageTargetData";
    private static readonly Type TargetDataType = typeof(CreatureCmd).Assembly.GetType(TargetDataName);
    private static readonly Type TargetEnumerableType = TargetDataType == null
        ? typeof(IEnumerable<Creature>) : typeof(IEnumerable<>).MakeGenericType(TargetDataType);
    private static readonly FieldInfo TargetField = TargetDataType?.GetField("target");
    private static readonly ConstructorInfo TargetConstructor = TargetDataType?.GetConstructor([typeof(Creature)]);

    internal static readonly MethodInfo Command = FindCommand();
    internal static readonly MethodInfo MoveNext = Command
        .GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
        .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(CreatureCmd).FullName, "Damage async MoveNext");

    internal static bool UsesTargetData => TargetDataType != null;

    private static MethodInfo FindCommand()
    {
        if (TargetDataType != null && (!TargetDataType.IsValueType || TargetField == null ||
            TargetField.FieldType != typeof(Creature) || TargetConstructor == null))
            throw new MissingMemberException(TargetDataName, "value-type target field / Creature constructor");

        return typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(CreatureCmd.Damage))
            .Where(method => method.GetParameters() is var p && p.Length is >= 6 and <= 8 &&
                p[0].ParameterType == typeof(PlayerChoiceContext) &&
                p[1].ParameterType == TargetEnumerableType && p[2].ParameterType == typeof(decimal) &&
                p[3].ParameterType == typeof(ValueProp) && p[4].ParameterType == typeof(Creature) &&
                p[5].ParameterType == typeof(CardModel))
            .Where(method => method.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
            .OrderByDescending(method => method.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(typeof(CreatureCmd).FullName,
                "canonical async enumerable Damage overload");
    }

    // Reflection metadata is cached above. Target records are decoded only at
    // command entry; no target-data types are referenced in this assembly's API.
    // Materialize a lazy native query once, preserving its original VFX fields.
    internal static IEnumerable<Creature> ReadTargets(object[] arguments)
    {
        if (!UsesTargetData) return (IEnumerable<Creature>)arguments[1];
        var projection = new TargetProjection((IEnumerable)arguments[1]);
        if (arguments[1] is not IList)
            arguments[1] = projection.ToNative(projection.Creatures, null);
        return projection;
    }

    // A filtered/reordered or redirected list retains each matching original
    // record, including hitVfx and playVfxOnCreatureCenter. A redirected record
    // changes only its target; duplicate target occurrences keep their own data.
    internal static void WriteTargets(object[] arguments, IEnumerable<Creature> original,
        IEnumerable<Creature> replacement, Func<Creature, Creature> originalTargetMap = null)
    {
        if (ReferenceEquals(original, replacement)) return;
        arguments[1] = original is TargetProjection projection
            ? projection.ToNative(replacement, originalTargetMap)
            : replacement;
    }

    private sealed class TargetProjection : IEnumerable<Creature>
    {
        private readonly object[] _records;
        internal Creature[] Creatures { get; }

        internal TargetProjection(IEnumerable records)
        {
            _records = records.Cast<object>().ToArray();
            Creatures = _records.Select(record => (Creature)TargetField.GetValue(record)).ToArray();
        }

        internal Array ToNative(IEnumerable<Creature> replacement, Func<Creature, Creature> originalTargetMap)
        {
            Creature[] targets = replacement.ToArray();
            Array result = Array.CreateInstance(TargetDataType, targets.Length);
            var used = new bool[_records.Length];
            for (int output = 0; output < targets.Length; output++)
            {
                Creature target = targets[output];
                object record = null;
                for (int input = 0; input < _records.Length; input++)
                {
                    if (used[input]) continue;
                    Creature mapped = originalTargetMap == null ? Creatures[input] : originalTargetMap(Creatures[input]);
                    if (!ReferenceEquals(mapped, target)) continue;
                    used[input] = true;
                    // Array.GetValue boxes a value copy. Updating it cannot
                    // mutate a caller's original target-data collection.
                    record = RuntimeHelpers.GetObjectValue(_records[input]);
                    TargetField.SetValue(record, target);
                    break;
                }
                record ??= TargetConstructor.Invoke([target]);
                result.SetValue(record, output);
            }
            return result;
        }

        public IEnumerator<Creature> GetEnumerator() => ((IEnumerable<Creature>)Creatures).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
