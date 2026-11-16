using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Detached read-only power views are scoped to one synchronous card preview.
// They never enter Creature.Powers, native apply hooks, saves or reservations.
internal static class HbrCardEffectPreview
{
    internal sealed class Scope : IDisposable
    {
        internal Scope Previous;
        internal CardModel Card;
        internal Creature Target;
        internal PowerModel[] Views;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<Scope> Current = new();
    internal sealed class ResourceScope : IDisposable
    {
        internal ResourceScope Previous;
        internal CardModel Card;
        internal Player Player;
        internal decimal Dp, Maximum;
        internal bool MakeupLost, Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Resources.Value, this)) Resources.Value = Previous; }
    }
    private static readonly AsyncLocal<ResourceScope> Resources = new();
    internal static bool TryGetDp(Player player, out decimal current, out decimal maximum)
    {
        for (ResourceScope scope = Resources.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Player, player))
            { current = scope.Dp; maximum = scope.Maximum; return true; }
        current = maximum = 0m; return false;
    }
    internal static bool IsMakeupSuppressed(Player player)
    {
        for (ResourceScope scope = Resources.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Player, player)) return scope.MakeupLost;
        return false;
    }
    internal static ResourceScope BeginDpCost(CardModel card)
    {
        if (card is not (DpSoulStrikeHbrgoSaki or MakeAChangeKillYourselfHbrgoSaki) ||
            !card.IsMutable || card.Owner?.Creature is not { CombatState: not null } owner ||
            !HbrSkillCardDescription.IsEnabled(card) || HbrCardDescriptionContext.IsUnpowered(card)) return null;
        for (ResourceScope active = Resources.Value; active != null; active = active.Previous)
            if (active.Active && ReferenceEquals(active.Card, card)) return null;
        Player player = card.Owner;
        Creature holder = card is DpSoulStrikeHbrgoSaki ? owner : FubukiChangeCombatHelper.DpTarget(player);
        if (holder == null) return null;
        decimal current, maximum;
        if (holder.Player != null)
        { DeflectorProtection pool = player.GetRelic<DeflectorProtection>(); current = pool?.Dp ?? 0m; maximum = pool?.MaxDp ?? 0m; }
        else
        { var pool = holder.GetPower<HighDimensionalShellPower>(); current = pool?.CurrentDp ?? 0m; maximum = pool?.MaxAmount ?? 0m; }
        ResourceScope scope = new() { Previous = Resources.Value, Card = card, Player = player, Dp = current, Maximum = maximum };
        bool rescueUsed = false;
        void Pay(decimal requested)
        {
            decimal before = scope.Dp;
            scope.Dp -= Math.Min(scope.Dp, Math.Max(0m, decimal.Floor(requested)));
            if (before <= 0m || scope.Dp > 0m) return;
            scope.MakeupLost = true;
            MuuaIWillNotFallPower ability = owner.GetPower<MuuaIWillNotFallPower>();
            if (rescueUsed || ability?.Amount <= 0 || ability == null || ability.DpRescueUsed ||
                !ability.Has(IWillNotFallHbrgoSaki.NeverFall) ||
                !ReferenceEquals(holder, FubukiChangeCombatHelper.DpTarget(player))) return;
            rescueUsed = true;
            if (holder.HasPower<LastStandPower>() || holder.GetPower<HighDimensionalShellPower>() is { CanRestoreDp: false }) return;
            decimal multiplier = DisabledPower.GetDpRecoveryMultiplier(holder) *
                ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(holder.Player ?? holder.PetOwner);
            scope.Dp = Math.Min(maximum, BigCombatNumbers.ClampStat(BigCombatNumbers.MultiplyPositive(maximum,
                DamageAmplificationLimits.MultiplySaturated(.2m, multiplier))));
        }
        if (card is MakeAChangeKillYourselfHbrgoSaki fubuki)
        {
            Pay(maximum * (fubuki.HasUpgradeEffect(MakeAChangeKillYourselfHbrgoSaki.Flash) ? .05m : .1m));
            if (fubuki.HasUpgradeEffect(MakeAChangeKillYourselfHbrgoSaki.Compensation)) Pay(scope.Dp / 2m);
        }
        else Pay(HbrResourceValueHelper.Fixed(owner, card.DynamicVars["DpLoss"].BaseValue));
        Resources.Value = scope; return scope;
    }
    private static readonly AccessTools.FieldRef<PowerModel, Creature> PowerOwner = AccessTools.FieldRefAccess<PowerModel, Creature>("_owner");
    private static readonly AccessTools.FieldRef<PowerModel, int> PowerAmount = AccessTools.FieldRefAccess<PowerModel, int>("_amount");
    internal static T View<T>(Creature target, int amount) where T : PowerModel
    {
        T view = (T)ModelDb.Power<T>().ToMutable();
        PowerOwner(view) = target; PowerAmount(view) = amount;
        return view;
    }
    internal static Scope Begin(CardModel card, Creature target, IEnumerable<PowerModel> projected)
    {
        Scope scope = new() { Previous = Current.Value, Card = card, Target = target, Views = projected.ToArray() };
        Current.Value = scope; return scope;
    }
    internal static Scope FullBodyShot(MimicFullBodyShotHbrgoSaki card, Creature target, out decimal rawDamage, out decimal destructionGain)
    {
        rawDamage = destructionGain = 0m;
        if (target == null || !target.HasPower<BreakPower>() || card.IsMutable != true || card.Owner?.Creature is not { } source)
            return null;
        List<PowerModel> fragiles = target.Powers.OfType<FragilePower>().Where(power => power.Amount > 0).Cast<PowerModel>().ToList();
        var incoming = View<FragilePower>(target, 1);
        decimal modifiers = card.DynamicVars["FragilePower"].BaseValue;
        modifiers = ModeTransitionHelper.GetAdjustedEffectAmount(card, modifiers);
        modifiers = Hook.ModifyPowerAmountGiven(card.CombatState, incoming, source, modifiers, target, card, out _);
        modifiers = Hook.ModifyPowerAmountReceived(card.CombatState, incoming, target, modifiers, source, out _);
        modifiers = IntegralStatAmountHelper.Normalize(modifiers, incoming);
        if (modifiers > 0m && target.CanReceivePowers && target.GetPowerAmount<ArtifactPower>() <= 0)
        {
            decimal basis = HbrFragilePowerHelper.BasePercent(modifiers);
            if (source.GetPowerAmount<ThirtyOneAYukiDebuffPower>() > 0) basis *= 1.3m;
            decimal potency = HbrPercentDebuffHelper.ResolveBasis(incoming, basis, source, target);
            HbrEffectProfile profile = HbrEffectProfileResolver.ForType(typeof(FragilePower), card);
            incoming.ConfigureHbr(potency, (int)Math.Min(int.MaxValue, decimal.Truncate(modifiers)));
            incoming.ConfigureProfile(profile); PowerAmount(incoming) = profile.DefaultRemaining;
            HbrEffectSlotDecision slot = HbrEffectRules.Select(incoming, target, potency, profile);
            if (slot.Accepted) { if (slot.Replaced != null) fragiles.Remove(slot.Replaced); fragiles.Add(incoming); }
        }
        decimal fragileBasis = 0m;
        foreach (FragilePower power in fragiles)
            fragileBasis = DamageAmplificationLimits.AddSaturated(fragileBasis, power.HbrEffectPercent);
        decimal fragile = HotaruUnfadingScarsPower.EnhanceEffect(target, fragileBasis);
        rawDamage = ModeTransitionHelper.GetAdjustedEffectAmount(card, fragile * .1m);
        destructionGain = ModeTransitionHelper.GetAdjustedEffectAmount(card,
            decimal.Floor(fragile / card.DynamicVars["FragileDestructionThreshold"].BaseValue));
        var destructionGrant = View<DestructionRatePower>(target, 0);
        destructionGain = Hook.ModifyPowerAmountGiven(card.CombatState, destructionGrant,
            source, destructionGain, target, card, out _);
        destructionGain = Hook.ModifyPowerAmountReceived(card.CombatState, destructionGrant,
            target, destructionGain, source, out _);
        destructionGain = Math.Max(0m, decimal.Truncate(
            IntegralStatAmountHelper.Normalize(destructionGain, destructionGrant)));
        int previous = target.GetPower<DestructionRatePower>()?.Amount ?? DestructionRatePower.BaselineAmount;
        var destruction = View<DestructionRatePower>(target,
            (int)Math.Clamp(previous + destructionGain, DestructionRatePower.BaselineAmount, int.MaxValue));
        fragiles.Add(destruction);
        // Contest Lv.6 derives its read-only defense-down view from Fragile.
        // Project that view too, while preserving any real defense-down slots.
        if (fragileBasis > 0m && !target.Powers.OfType<HbrDefenseDownPower>().Any() &&
            target.CombatState?.GetOpponentsOf(target).Any(opponent =>
                opponent.Player?.GetRelic<Millsage>()?.HasCannonUpgrade(HotaruCannonKind.Contest, 6) == true) == true)
        {
            var defense = View<HbrDefenseDownPower>(target, 2);
            HbrPercentDebuffHelper.Configure(defense,
                HbrPercentDebuffHelper.ApplyVulnerableBaseBoost(target, fragileBasis),
                (int)Math.Min(int.MaxValue, fragiles.OfType<FragilePower>().Sum(power => (long)power.ModifierAmount)));
            fragiles.Add(defense);
        }
        return Begin(card, target, fragiles);
    }
    internal static IEnumerable<AbstractModel> EnumerateDamageModifiers(IEnumerable<AbstractModel> original, Creature target, CardModel cardSource)
    {
        for (Scope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, cardSource) && ReferenceEquals(scope.Target, target))
                return Project(original, target, scope);
        return original;
    }
    private static IEnumerable<AbstractModel> Project(IEnumerable<AbstractModel> original, Creature target, Scope scope)
    {
        foreach (AbstractModel model in original)
        {
            if (scope.Active && model is PowerModel { IsMutable: true } power && ReferenceEquals(power.Owner, target) &&
                (power is FragilePower or DestructionRatePower ||
                    power is HbrDefenseDownPower && !target.Powers.Contains(power))) continue;
            yield return model;
        }
        if (scope.Active)
            foreach (PowerModel view in scope.Views) yield return view;
    }
}
