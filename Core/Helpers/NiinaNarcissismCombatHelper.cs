using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NiinaNarcissismCombatHelper
{
    private static readonly AsyncLocal<Receipt> Current = new();
    internal sealed class Receipt : IDisposable
    {
        internal NarcissismIsFineHbrgoSaki Card;
        internal Receipt Previous;
        internal decimal Amp, ResourceDamage;
        internal bool Active = true, Completed;
        internal readonly HashSet<DamageResult> Results = new();
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    internal static int BuffKinds(Creature owner) => owner?.Powers.Where(power => power.Amount != 0 &&
        power.TypeForCurrentAmount == PowerType.Buff).Select(power => power.GetType()).Distinct().Count() ?? 0;
    private static decimal Amp(NarcissismIsFineHbrgoSaki card) =>
        (card.HasUpgradeEffect(NarcissismIsFineHbrgoSaki.Narcissism) ? BuffKinds(card.Owner?.Creature) * 10m : 0m) +
        (card.HasUpgradeEffect(NarcissismIsFineHbrgoSaki.Opportunity) && card.Owner?.Creature?.CombatState?.GetOpponentsOf(card.Owner.Creature)
            .Any(enemy => enemy.IsAlive && enemy.GetPower<DestructionRatePower>() is { } rate &&
                DestructionRatePower.GetDamageMultiplier(enemy, rate.DisplayAmount) >= 2m) == true ? 25m : 0m);
    internal static Receipt Begin(NarcissismIsFineHbrgoSaki card)
    { Receipt scope = new() { Card = card, Amp = Amp(card), Previous = Current.Value }; Current.Value = scope; return scope; }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is NarcissismIsFineHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source);
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 0m;
        for (Receipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && ReferenceEquals(receipt.Card, source)) return receipt.Amp;
        return Amp((NarcissismIsFineHbrgoSaki)source);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? (((NarcissismIsFineHbrgoSaki)source).HasUpgradeEffect(NarcissismIsFineHbrgoSaki.PureLove)
            ? 21m : 4m) / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static decimal GetNextDestructionMultiplier(Creature dealer, CardModel source, ValueProp props)
    {
        if (dealer?.Player == null || !props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(dealer) ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return 1m;
        var native = HighHitDamageVisualHelper.GetCurrentAttackIdentity();
        var direct = HbrComboHelper.GetCurrentAttackIdentity();
        object token = direct.Command != null && (direct.Order > native.Order ||
            HbrComboHelper.CurrentDirectCovers(native.Command)) ? direct.Command : native.Command;
        return token == null ? 1m : 1m + dealer.Powers.OfType<NiinaNextDestructionPower>()
            .Where(power => power.Amount > 0 && ReferenceEquals(power.ReservedEffectAttackToken, token))
            .Sum(power => power.EffectPotency) / 100m;
    }
    internal static bool IsOwnAttack(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && ((NarcissismIsFineHbrgoSaki)source).HasUpgradeEffect(NarcissismIsFineHbrgoSaki.Sakura) &&
        Find(source) != null;
    private static Receipt Find(CardModel source)
    {
        for (Receipt scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && !scope.Completed && ReferenceEquals(scope.Card, source)) return scope;
        return null;
    }
    internal static void RecordDamage(CardModel source, DamageResult result)
    {
        Receipt receipt = Find(source);
        if (receipt != null && result != null && receipt.Results.Add(result)) receipt.ResourceDamage =
            DamageAmplificationLimits.AddSaturated(receipt.ResourceDamage, CombatAttackHitHelper.GetResourceDamage(result));
    }
    internal static async Task CompleteAttack(PlayerChoiceContext context, Receipt receipt)
    {
        if (!receipt.Active || receipt.Completed) return;
        receipt.Completed = true;
        if (!receipt.Card.HasUpgradeEffect(NarcissismIsFineHbrgoSaki.Sakura) || receipt.ResourceDamage <= 0m) return;
        await SeiraDpRecoveryHelper.RestoreAmount(context, FubukiChangeCombatHelper.DpTarget(receipt.Card.Owner),
            decimal.Floor(receipt.ResourceDamage * .3m), false, receipt.Card);
    }
}
