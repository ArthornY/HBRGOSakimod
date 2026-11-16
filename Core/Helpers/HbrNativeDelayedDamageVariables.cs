using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrNativeDamagePreviewTarget { FutureEnemy }

internal sealed record HbrNativeDelayedDamageVariable(string Name, Type AppliedPowerType,
    ValueProp Props, HbrNativeDamagePreviewTarget Target = HbrNativeDamagePreviewTarget.FutureEnemy);

// These particular card variables become the damage of a later native power.
// Other PowerVars, summons, duration and resource costs remain ordinary counts.
internal static class HbrNativeDelayedDamageVariables
{
    private static readonly Dictionary<Type, HbrNativeDelayedDamageVariable[]> Cards = Build();
    internal static IEnumerable<KeyValuePair<Type, HbrNativeDelayedDamageVariable[]>> All => Cards;

    private static Dictionary<Type, HbrNativeDelayedDamageVariable[]> Build()
    {
        var result = new Dictionary<Type, HbrNativeDelayedDamageVariable[]>();
        void Add(string cardName, string powerName, ValueProp props, params string[] variables)
        {
            Type card = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards." + cardName, false);
            Type power = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers." + powerName, false);
            if (card == null || power == null) return;
            result.Add(card, Array.ConvertAll(variables, name => new HbrNativeDelayedDamageVariable(name, power, props)));
        }
        foreach (string name in new[] { "BlackHole", "ExplosiveArrows", "Hailstorm", "Inferno", "Juggernaut",
            "SerpentForm", "SleightOfFlesh", "Smokestack", "Speedster", "Thunder", "ViolentChord" })
            Add(name, name + "Power", ValueProp.Unpowered, name + "Power");
        Add("Caltrops", "ThornsPower", ValueProp.Unpowered | ValueProp.SkipHurtAnim, "ThornsPower");
        Add("FlameBarrier", "FlameBarrierPower", ValueProp.Unpowered, "DamageBack");
        Add("Panache", "PanachePower", ValueProp.Unpowered, "PanacheDamage");
        Add("TheBomb", "TheBombPower", ValueProp.Unpowered, "BombDamage");
        Add("RollingBoulder", "RollingBoulderPower", ValueProp.Unpowered, "RollingBoulderPower", "IncrementAmount");
        Add("Haunt", "HauntPower", ValueProp.Unblockable | ValueProp.Unpowered, "HpLoss");
        Add("GodByrd", "GodByrdPower", ValueProp.Unpowered, "Damage");
        Add("Cacophony", "CacophonyPower", ValueProp.Unpowered, "Damage");
        // Stable Outbreak grants a damage power; beta grants Poison stacks.
        // Inspect only metadata: constructing another CardModel after native
        // ModelDb registration throws DuplicateModelException, and requiring
        // a canonical lookup here would introduce an initialization-order
        // dependency while other canonical cards are still being registered.
        Type outbreak = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards.Outbreak", false);
        Type outbreakPower = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.OutbreakPower", false);
        if (outbreak != null && outbreakPower != null && DeclaresPowerVariable(outbreak, outbreakPower))
            Add("Outbreak", "OutbreakPower", ValueProp.Unpowered, "OutbreakPower");
        return result;
    }

    private static bool DeclaresPowerVariable(Type card, Type power)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        MethodInfo getter = card.GetProperty("CanonicalVars", flags)?.GetGetMethod(nonPublic: true);
        if (getter == null) return false;
        MethodInfo body = getter.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType
            .GetMethod("MoveNext", flags) ?? getter;
        foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(body))
            if (instruction.operand is ConstructorInfo constructor && constructor.DeclaringType is { IsConstructedGenericType: true } variable &&
                variable.GetGenericTypeDefinition() == typeof(PowerVar<>) && variable.GenericTypeArguments[0] == power)
                return true;
        return false;
    }

    internal static bool Contains(AbstractModel model) => model != null && Cards.ContainsKey(model.GetType());

    internal static bool TryGet(AbstractModel model, string name, out HbrNativeDelayedDamageVariable variable)
    {
        variable = null;
        if (model == null || !Cards.TryGetValue(model.GetType(), out var values)) return false;
        foreach (var value in values)
            if (value.Name == name) { variable = value; return true; }
        return false;
    }
}
