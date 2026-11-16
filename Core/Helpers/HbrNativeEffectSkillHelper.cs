using System;
using MegaCrit.Sts2.Core.Hooks;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.ValueProps;
using HBRGOSakiMod.Core.Patches.Combat;

namespace HBRGOSakiMod.Core.Helpers;

// Native values remain native units: six is one 100% skill coefficient. This
// registry declares effect sources, independently of their native attack flags.
internal static class HbrNativeEffectSkillHelper
{
    private static readonly AccessTools.FieldRef<DynamicVar, AbstractModel> VariableOwner =
        AccessTools.FieldRefAccess<DynamicVar, AbstractModel>("_owner");
    private static readonly HashSet<Type> Models = BuildRegistry();
    // These five native hand-end ticks are environmental HP-scale damage,
    // not an attack skill belonging to the card owner. Keep the native card
    // attribution; do not infer this policy for other self-targeting cards.
    private static readonly HashSet<Type> EnvironmentalSelfDamageCards = new(Array.ConvertAll(
        new[] { "Cards.Burn", "Cards.Decay", "Cards.Infection", "Cards.Toxic", "Cards.Wither" }, NativeType));
    private static readonly ConditionalWeakTable<LocString, Display> Displays = new();
    [ThreadStatic] private static MerchantDisplay _merchantDisplay;

    private sealed class Display
    {
        internal readonly Dictionary<string, SkillValue> Variables = new(StringComparer.Ordinal);
        // A static_hover_tips text replacing the whole native one, with one unit's coefficient.
        internal string TextKey;
        internal AbstractModel Model;
        internal Creature Source;
    }

    private sealed record SkillValue(decimal RawUnits, bool IsBlock, bool IsEnvironmental,
        ValueProp Props = ValueProp.Unpowered, bool UseSelectedTarget = true);

    private sealed class MerchantDisplay : IDisposable
    {
        internal RelicModel Relic;
        internal Player Player;
        internal MerchantDisplay Previous;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _merchantDisplay = Previous;
        }
    }

    private static HashSet<Type> BuildRegistry()
    {
        HashSet<Type> result = new();
        foreach (string name in new[] { "Cards.Burn", "Cards.Decay", "Cards.Infection", "Cards.Toxic",
                     "Cards.Wither", "Events.AbyssalBaths", "Events.RoomFullOfCheese",
                     "Events.RoundTeaParty", "Events.TheLegendsWereTrue", "Relics.Kusarigama",
                     "Relics.MiniatureCannon", "Relics.Anchor", "Relics.RippleBasin", "Powers.SkittishPower",
                     // Shared and event relics dealing fixed damage to enemies.
                     "Relics.FestivePopper", "Relics.ForgottenSoul", "Relics.LetterOpener",
                     "Relics.LostWisp", "Relics.MercuryHourglass", "Relics.MrStruggles",
                     "Relics.ParryingShield", "Relics.ScreamingFlagon", "Relics.StoneCalendar" })
            result.Add(NativeType(name));
        return result;
    }

    private static Type NativeType(string name) =>
        typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + name, true);

    // Damage over time deals its stacks as its applier's skill units, six to
    // 100%, read when each tick resolves; stacks and decay keep native units.
    internal static readonly Type[] DamageOverTimeTypes = Array.ConvertAll(new[] { "Powers.PoisonPower",
        "Powers.ConstrictPower", "Powers.DisintegrationPower", "Powers.StranglePower", "Powers.DemisePower" }, NativeType);

    // Texts that state those stacks as the HP they cost, not as a stack count.
    // Disintegration's holder applies it to itself, so it reads as a fixed loss.
    private static readonly Dictionary<Type, (string Variable, bool FixedLoss)> DamageOverTimeVariables = new()
    {
        [NativeType("Cards.Disintegration")] = ("DisintegrationPower", true),
        [NativeType("Cards.MadScience")] = ("ChokingDamage", false),
        [NativeType("Events.TinkerTime")] = ("ChokingDamage", false),
        [NativeType("Potions.PowderedDemise")] = ("Demise", false)
    };

    internal static bool IsSkillModel(AbstractModel model) => model != null &&
        (Models.Contains(model.GetType()) || HbrNativeDamageProfiles.IsSkillSource(model) || model is PotionModel &&
            model.GetType().Assembly == typeof(PotionModel).Assembly);

    internal static bool IsDamageOverTime(AbstractModel model) =>
        model != null && Array.IndexOf(DamageOverTimeTypes, model.GetType()) >= 0;

    // Stacks with no other applier, such as the Disintegration a hero chooses from
    // Knowledge Demon, are a fixed HP loss like an HP-cost card: a hundred per
    // stack. A holder's own skill is scaled for enemy HP and would far exceed it.
    internal static bool TryGetDamageOverTimeSource(PowerModel power, out Creature source, out bool fixedLoss)
    {
        source = null;
        fixedLoss = false;
        if (power?.IsMutable != true || !IsDamageOverTime(power) || !HbrBattleStatsHelper.IsEnabled(power.Owner))
            return false;
        source = power.Applier;
        fixedLoss = source == null || ReferenceEquals(source, power.Owner);
        return true;
    }

    internal static bool IsSkillVariable(DynamicVar variable) => variable is DamageVar &&
        !HbrNativeFixedResourceHelper.IsFixedVariable(variable) &&
        IsSkillModel(VariableOwner(variable));

    internal static bool IsEnvironmentalSelfDamageVariable(DynamicVar variable) => variable is DamageVar &&
        VariableOwner(variable) is CardModel card && EnvironmentalSelfDamageCards.Contains(card.GetType());

    internal static AbstractModel GetVariableSource(DynamicVar variable) =>
        variable == null ? null : VariableOwner(variable);

    // Native block potions and these relics use Unpowered BlockVars, bypassing
    // the card block adapter. Resolve only the audited owner's original value.
    private static bool IsNativeBlockVariable(DynamicVar variable) => variable is BlockVar &&
        VariableOwner(variable) is Anchor or RippleBasin or BlockPotion && variable.Name == "Block";

    internal static bool TryResolveNativeBlock(BlockVar variable, Creature target, out decimal amount)
    {
        amount = 0m;
        if (variable == null || variable.Props != ValueProp.Unpowered ||
            !IsNativeBlockVariable(variable) ||
            VariableOwner(variable) is not { IsMutable: true } owner ||
            !ReferenceEquals(GetModeOwner(owner), target) ||
            !ReferenceEquals(owner switch
            {
                RelicModel relic => relic.DynamicVars.Block,
                PotionModel potion => potion.DynamicVars.Block,
                _ => null
            }, variable) ||
            !HbrBattleStatsHelper.IsEnabled(target)) return false;
        amount = ResolveNativeBlockAmount(target, variable.BaseValue);
        return true;
    }

    internal static decimal ResolveNativeBlockAmount(Creature source, decimal rawUnits) =>
        EnemyStatScalingHelper.IsEnemy(source) && HbrBattleStatsHelper.IsEnabled(source)
            ? HbrSkillPower.EvaluateEnemyBlockFromUnits(rawUnits,
                HbrEnemyProfileHelper.GetEnemyParameter(source), HbrDamageSkillAdapter.OutgoingPowerUnit)
            : HbrBattleStatsHelper.TryGetStats(source, out HbrBattleStats stats)
                ? HbrSkillPower.EvaluateBlockFromUnits(rawUnits, stats.Intelligence)
                : HbrSkillPower.BlockBaseFromUnits(rawUnits);

    internal static Creature GetModeOwner(DynamicVar variable) =>
        GetModeOwner(variable == null ? null : VariableOwner(variable));

    internal static Creature GetModeOwner(AbstractModel model)
    {
        // Canonical templates are not allowed to read model Owner properties.
        if (model?.IsMutable != true) return null;
        if (HbrNativeDamageProfiles.IsSkillSource(model)) return HbrNativeDamageProfiles.ResolveSource(model);
        return model switch
        {
            CardModel card => card.Owner?.Creature,
            RelicModel relic => relic.Owner?.Creature,
            PotionModel potion => potion.Owner?.Creature,
            EventModel eventModel => eventModel.Owner?.Creature,
            PowerModel power => power.Owner,
            OrbModel orb => orb.Owner?.Creature,
            _ => null
        };
    }

    internal static bool IsEnabled(AbstractModel model) =>
        HbrBattleStatsHelper.IsEnabled(GetModeOwner(model)) ||
        model is RelicModel && (IsSkillModel(model) || HbrAdditiveSkillDamageSources.Supports(model)) &&
        ReferenceEquals(_merchantDisplay?.Relic, model) &&
        HbrBattleStatsHelper.IsEnabled(_merchantDisplay.Player);

    internal static IDisposable BeginMerchantDisplay(RelicModel relic, Player player)
    {
        MerchantDisplay scope = new() { Relic = relic, Player = player, Previous = _merchantDisplay };
        _merchantDisplay = scope;
        return scope;
    }

    internal static void TagDescription(AbstractModel model, LocString text)
    {
        if (text == null || model == null) return;
        bool hasDamageOverTime = DamageOverTimeVariables.TryGetValue(model.GetType(),
            out (string Variable, bool FixedLoss) damageOverTime);
        bool hasDelayedDamage = HbrNativeDelayedDamageVariables.Contains(model);
        if (!IsSkillModel(model) && !hasDamageOverTime && !hasDelayedDamage) return;
        Displays.Remove(text);
        if (!IsEnabled(model)) return;
        Display display = Displays.GetValue(text, _ => new Display());
        display.Model = model;
        display.Source = GetModeOwner(model);
        // Mr. Struggles deals the turn number, which its text words without one.
        if (model is MrStruggles)
        {
            Displays.GetValue(text, _ => new Display()).TextKey = "HBR_NATIVE_MR_STRUGGLES.description";
            TagDescription(text, "CurrentDamage", Math.Max(1, display.Source?.Player?.PlayerCombatState?.TurnNumber ?? 1));
            return;
        }
        if (model is OrbModel orb)
        {
            TagDescription(text, "Passive", orb.PassiveVal);
            TagDescription(text, "Evoke", orb.EvokeVal);
            return;
        }
        DynamicVarSet vars = model switch
        {
            CardModel card => card.DynamicVars,
            RelicModel relic => relic.DynamicVars,
            PotionModel potion => potion.DynamicVars,
            EventModel eventModel => eventModel.DynamicVars,
            PowerModel nativePowerVars => nativePowerVars.DynamicVars,
            _ => null
        };
        if (vars == null) return;
        foreach (DynamicVar variable in vars.Values)
            // A self-targeting power card has no eventual enemy yet. Do not
            // pretend that its current self target is the retaliation victim.
            if (hasDelayedDamage && HbrNativeDelayedDamageVariables.TryGet(model, variable.Name, out var delayed))
                TagSkillDescription(text, variable.Name, variable.BaseValue, false,
                    nativeProps: delayed.Props, useSelectedTarget: false);
            // A rider or potion's stacks are its own holder's skill, even on an event option.
            else if (hasDamageOverTime && variable.Name == damageOverTime.Variable)
                TagSkillDescription(text, variable.Name, variable.BaseValue, isBlock: false,
                    isEnvironmental: damageOverTime.FixedLoss);
            else if (!IsSkillModel(model)) continue;
            else if (IsNativeBlockVariable(variable))
                TagSkillDescription(text, variable.Name, variable.BaseValue, isBlock: true);
            else if (IsEnvironmentalSelfDamageVariable(variable))
                TagSkillDescription(text, variable.Name, variable.BaseValue, isBlock: false,
                    isEnvironmental: true, nativeProps: ((DamageVar)variable).Props);
            // Events have no dealer, so their declared damage is environmental:
            // an amount on the player HP scale rather than a skill coefficient.
            else if (model is EventModel && IsSkillVariable(variable))
                TagSkillDescription(text, variable.Name, variable.BaseValue, isBlock: false, isEnvironmental: true);
            else if (IsSkillVariable(variable) || model is MiniatureCannon && variable.Name == "ExtraDamage")
                TagSkillDescription(text, variable.Name, variable.BaseValue, false,
                    nativeProps: variable is DamageVar damage ? damage.Props : ValueProp.Unpowered);
        if (model is MegaCrit.Sts2.Core.Models.Powers.SkittishPower skittish)
            TagSkillDescription(text, "Amount", skittish.Amount, isBlock: true);
        if (model is PowerModel nativePower && HbrNativeDamageProfiles.TryGet(nativePower, out var profile) &&
            profile.UsesAmountAsDamage)
            TagDescription(text, "Amount", nativePower.Amount);
    }

    internal static void TagDescription(LocString text, string variable, decimal rawUnits)
        => TagSkillDescription(text, variable, rawUnits, isBlock: false);

    internal static void TagDescription(LocString text, string variable, decimal rawUnits, Creature statSource)
    {
        if (text == null) return;
        Displays.GetValue(text, _ => new Display()).Source = statSource;
        TagDescription(text, variable, rawUnits);
    }

    // A damage-over-time power states its stacks as a coefficient, or as the
    // fixed amount its holder loses to stacks it applied itself.
    internal static void TagDamageOverTime(PowerModel power, LocString text)
    {
        if (TryGetDamageOverTimeSource(power, out Creature source, out bool fixedLoss))
        {
            Display display = Displays.GetValue(text, _ => new Display());
            display.Model = power; display.Source = source;
            TagSkillDescription(text, "Amount", power.Amount, isBlock: false, isEnvironmental: fixedLoss);
        }
    }

    private static void TagSkillDescription(LocString text, string variable, decimal rawUnits, bool isBlock,
        bool isEnvironmental = false, ValueProp nativeProps = ValueProp.Unpowered, bool useSelectedTarget = true)
    {
        if (text == null) return;
        Displays.GetValue(text, _ => new Display()).Variables[variable] =
            new SkillValue(rawUnits, isBlock, isEnvironmental, nativeProps, useSelectedTarget);
    }

    internal static string FormatPercent(decimal rawUnits) =>
        HbrSkillPower.DamagePercent(rawUnits).ToString("0.##", CultureInfo.InvariantCulture);

    internal static string AdaptText(LocString text, string raw)
    {
        if (string.IsNullOrEmpty(raw) || !Displays.TryGetValue(text, out Display display)) return raw;
        if (display.TextKey != null && HbrLocalizedText.Local("static_hover_tips", display.TextKey) is { } replacement)
        {
            text.Add("HbrEffectSkillPower", FormatPercent(1m));
            raw = replacement;
        }
        // Replace the complete formatter, including diff/plural nested braces.
        // Feeding a percentage string into a native numeric formatter is invalid.
        foreach ((string variable, SkillValue value) in display.Variables)
        {
            string parameter = "HbrEffectDamageSkill" + variable;
            if (value.IsEnvironmental)
            {
                text.Add(parameter, HbrSkillPower.DamageBaseFromUnits(value.RawUnits,
                    HbrDamageSkillAdapter.EnvironmentalPowerUnit));
                raw = ReplaceVariable(raw, variable, parameter, keepUnit: true);
                continue;
            }
            decimal amount = ResolveDisplayAmount(display, value);
            string numeric = decimal.Floor(Math.Max(0m, amount)).ToString("0", CultureInfo.InvariantCulture);
            if (display.Model is CardModel inspect && HbrCardDescriptionContext.IsDetail(inspect))
                numeric += "（" + (value.IsBlock ? HbrSkillPower.BlockPercent(value.RawUnits)
                    : HbrSkillPower.DamagePercent(value.RawUnits)).ToString("0.##", CultureInfo.InvariantCulture) + "%）";
            text.Add(parameter, numeric);
            raw = ReplaceVariable(raw, variable, parameter, keepUnit: true);
        }
        return raw;
    }

    private static decimal ResolveDisplayAmount(Display display, SkillValue value)
    {
        Creature source = display.Source;
        if (value.IsBlock)
            return ResolveNativeBlockAmount(source, value.RawUnits);
        CardModel card = display.Model as CardModel;
        Creature target = card != null ? value.UseSelectedTarget ? HbrCardDescriptionContext.GetTarget(card) : null
            : display.Model is PowerModel power && power.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff ? power.Owner : null;
        bool world = display.Model != null && HbrNativeDamageProfiles.TryGet(display.Model, out var profile) &&
            profile.Basis == HbrNativeDamageBasis.WorldAmount;
        // An untargeted tooltip cannot invent an enemy's defenses. State the
        // actual skill basis until a card target or debuff recipient is known.
        if (source?.CombatState?.RunState == null || target == null)
            return world ? value.RawUnits : HbrSkillPower.DamageBaseFromUnits(value.RawUnits,
                EnemyStatScalingHelper.IsEnemy(source) ? HbrDamageSkillAdapter.IncomingPowerUnit
                    : HbrDamageSkillAdapter.OutgoingPowerUnit);
        Creature dealer = display.Model is PowerModel holder ? holder.Owner : source;
        StrongestShieldDamagePatch.BeginIntentPreview();
        try
        {
            using var declaration = world
                ? HbrEffectSkillDamageContext.BeginWorld(target, dealer, card, value.RawUnits, value.Props, source)
                : HbrEffectSkillDamageContext.Begin(target, dealer, card, value.RawUnits, value.Props, source);
            return DamageLimitHelper.CapSingleHit(HookCompatibility.ModifyDamage(source.CombatState.RunState,
                source.CombatState, target, dealer, value.RawUnits, value.Props, card,
                ModifyDamageHookType.All, CardPreviewMode.Normal, out _));
        }
        finally { StrongestShieldDamagePatch.EndIntentPreview(); }
    }

    private static string ReplaceVariable(string raw, string variable, string parameter, bool keepUnit)
    {
        for (int cursor = 0; cursor < raw.Length; cursor++)
        {
            if (raw[cursor] != '{') continue;
            int nameStart = cursor + 1;
            while (nameStart < raw.Length && char.IsWhiteSpace(raw[nameStart])) nameStart++;
            if (nameStart + variable.Length >= raw.Length ||
                string.CompareOrdinal(raw, nameStart, variable, 0, variable.Length) != 0) continue;
            int nameEnd = nameStart + variable.Length;
            if (raw[nameEnd] is not ('}' or ':' or '.' or '[') && !char.IsWhiteSpace(raw[nameEnd])) continue;
            int end = cursor + 1;
            int depth = 1;
            while (end < raw.Length && depth > 0)
            {
                if (raw[end] == '{') depth++;
                else if (raw[end] == '}') depth--;
                end++;
            }
            if (depth != 0) return raw;
            // Chinese native descriptions put a point unit after the formatter,
            // directly or after its closing color tags, which stay. A coefficient
            // is not a count of points, but an environmental amount is; "点生命"
            // becomes "的生命" ("lose a 100% coefficient's HP").
            if (!keepUnit)
            {
                int unit = end;
                while (unit + 1 < raw.Length && raw[unit] == '[' && raw[unit + 1] == '/')
                {
                    int close = raw.IndexOf(']', unit + 2);
                    if (close < 0) break;
                    unit = close + 1;
                }
                if (unit < raw.Length && raw[unit] == '点')
                    raw = raw.Remove(unit, 1).Insert(unit,
                        string.CompareOrdinal(raw, unit + 1, "生命", 0, 2) == 0 ? "的" : "");
            }
            string replacement = "{" + parameter + "}";
            raw = raw[..cursor] + replacement + raw[end..];
            cursor += replacement.Length - 1;
        }
        return raw;
    }
}
