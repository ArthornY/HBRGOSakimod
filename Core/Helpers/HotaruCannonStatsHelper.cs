using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCannonStatsHelper
{
    internal sealed class Intent
    {
        internal Creature Cannon;
        internal Creature Target;
        internal Creature Dealer;
        internal decimal Amount;
        internal CardModel Card;
        internal CardPlay Play;
        internal ValueProp Props;
        internal bool IsReaction;
        internal bool Active = true;
        internal bool Claimed;
        internal decimal DpSpent;
    }
    internal sealed class CommandScope
    {
        internal CommandScope Previous;
        internal Intent Intent;
        internal Creature Dealer;
        internal CardModel Card;
        internal CardPlay Play;
        internal ValueProp Props;
        internal bool Active = true;
        internal bool Capture;
        internal decimal PendingDpSpent;
        internal Creature PendingDpReceiver;
        internal readonly Dictionary<Creature, decimal> DpMaximums = new();
        internal readonly Dictionary<Creature, decimal> FinalAmounts = new();
        internal Creature Cannon => Intent?.Cannon;
        internal bool Matches(Creature target, Creature dealer, CardModel card, ValueProp props) =>
            Active && Intent?.Active == true && Intent.Props == props &&
            ReferenceEquals(target, Intent.Target) && ReferenceEquals(dealer, Intent.Dealer) &&
            ReferenceEquals(card, Intent.Card);
    }
    private sealed class ReceiptState
    {
        internal CannonHitReceipt Receipt;
        internal int Taken;
    }
    private static readonly AsyncLocal<Intent> Pending = new();
    private static readonly AsyncLocal<CommandScope> Current = new();
    private static readonly AsyncLocal<Creature> PreviewCannon = new();
    private static readonly AsyncLocal<Creature> PreviewTarget = new();
    private static readonly AsyncLocal<CardModel> PreviewCard = new();
    private static readonly AsyncLocal<HotaruCannonPhase?> PreviewPhase = new();
    internal static HotaruCannonPhase? CurrentPreviewPhase => PreviewPhase.Value;
    internal static bool IsDamagePreview => PreviewCannon.Value != null;
    internal static bool IsReactionDamage => !IsDamagePreview && Current.Value?.Intent?.IsReaction == true;
    private static readonly ConditionalWeakTable<DamageResult, ReceiptState> Receipts = new();

    internal static bool TryGetCurrentCannon(Creature target, Creature dealer, CardModel card,
        ValueProp props, out Creature cannon)
    {
        // A synchronous intent refresh can run inside another cannon's real hit.
        // Its pure preview must use its own cannon and must never fall back to
        // the matching outer command (same owner/target and null card source).
        if (PreviewCannon.Value is { } preview)
        {
            cannon = ReferenceEquals(preview.PetOwner?.Creature, dealer) &&
                ReferenceEquals(PreviewTarget.Value, target) && ReferenceEquals(PreviewCard.Value, card) &&
                props == ValueProp.Move ? preview : null;
            return cannon != null;
        }
        cannon = Current.Value?.Matches(target, dealer, card, props) == true ? Current.Value.Cannon : null;
        return cannon != null;
    }
    internal static decimal PreviewDamage(Creature cannon, decimal baseDamage,
        bool includeContestBonus = false, CardPlay cardPlay = null, HotaruCannonPhase previewPhase = HotaruCannonPhase.End) =>
        PreviewDamageRange(cannon, baseDamage, includeContestBonus, cardPlay, previewPhase).High;
    internal static string FormatDamagePreview(Creature cannon, decimal baseDamage,
        bool includeContestBonus = false, HotaruCannonPhase previewPhase = HotaruCannonPhase.End)
    {
        (decimal low, decimal high) = PreviewDamageRange(cannon, baseDamage, includeContestBonus, null, previewPhase);
        return low == high ? CardDamageNumberFormatter.Format(low) :
            CardDamageNumberFormatter.Format(low) + "-" + CardDamageNumberFormatter.Format(high);
    }
    private static (decimal Low, decimal High) PreviewDamageRange(Creature cannon, decimal baseDamage,
        bool includeContestBonus, CardPlay cardPlay, HotaruCannonPhase previewPhase)
    {
        Creature dealer = cannon?.PetOwner?.Creature;
        Creature[] targets = cannon?.CombatState?.Enemies
            .Where(enemy => Millsage.IsEnemy(enemy, cannon.PetOwner)).ToArray() ?? Array.Empty<Creature>();
        if (targets.Length == 0 || dealer?.CombatState?.RunState == null)
        {
            decimal amount = HotaruCannonDamageUpgradeHelper.PrepareBaseDamage(cannon, null, baseDamage);
            decimal fallback = Math.Max(1m, decimal.Floor(DamageAmplificationLimits.MultiplySaturated(
                Math.Max(0m, amount), GetCannonDamageMultiplier(cannon))));
            return (fallback, fallback);
        }
        Creature previous = PreviewCannon.Value;
        Creature previousTarget = PreviewTarget.Value;
        CardModel previousCard = PreviewCard.Value;
        HotaruCannonPhase? previousPhase = PreviewPhase.Value;
        PreviewCannon.Value = cannon;
        PreviewCard.Value = cardPlay?.Card;
        PreviewPhase.Value = previewPhase;
        try
        {
            decimal low = decimal.MaxValue;
            decimal high = decimal.MinValue;
            // Keep the target snapshot and native hook order, but aggregate the
            // range directly instead of allocating an array of damage values.
            foreach (Creature target in targets)
            {
                PreviewTarget.Value = target;
                decimal amount = baseDamage;
                if (includeContestBonus && cannon.Monster is HotaruContestCannon)
                    amount += SixSenseCombatHelper.TargetResistanceBreakCount(target) *
                        HotaruCannonSkillPowerHelper.ContestResistanceDamage *
                        ((HotaruCannonModel)cannon.Monster).PhaseMultiplier(HotaruCannonPhase.End);
                amount = HotaruCannonDamageUpgradeHelper.PrepareBaseDamage(cannon, target, amount,
                    ((HotaruCannonModel)cannon.Monster).PhaseMultiplier(previewPhase));
                decimal final = HookCompatibility.ModifyDamage(dealer.CombatState.RunState,
                    dealer.CombatState, target, dealer, amount, ValueProp.Move,
                    cardPlay?.Card, ModifyDamageHookType.All, CardPreviewMode.None, out _, cardPlay);
                decimal value = decimal.Floor(Math.Max(0m, final));
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
            return (low, high);
        }
        finally
        {
            PreviewCannon.Value = previous;
            PreviewTarget.Value = previousTarget;
            PreviewCard.Value = previousCard;
            PreviewPhase.Value = previousPhase;
        }
    }
    internal static Task<bool> Damage(Creature cannon, Creature target, decimal amount,
        PlayerChoiceContext context, CardModel cardSource, CardPlay cardPlay = null, int phaseMultiplier = 1) =>
        DamageCore(cannon, target, amount, context, cardSource, cardPlay, false, false, phaseMultiplier, false);
    internal static async Task ReactionDamage(PlayerChoiceContext context, Creature cannon,
        Creature target, decimal baseAmount, CardModel cardSource, CardPlay cardPlay = null, bool hpOnly = false, bool absoluteBase = false) =>
        await DamageCore(cannon, target, baseAmount, context, cardSource, cardPlay, true, hpOnly, 1, absoluteBase);
    private static async Task<bool> DamageCore(Creature cannon, Creature target, decimal amount,
        PlayerChoiceContext context, CardModel cardSource, CardPlay cardPlay, bool reaction, bool hpOnly, int phaseMultiplier, bool absoluteBase)
    {
        Creature dealer = cannon?.PetOwner?.Creature;
        if (dealer == null || !Millsage.IsOwnedCannon(cannon, cannon.PetOwner) ||
            !Millsage.IsEnemy(target, cannon.PetOwner)) return false;
        if (!reaction)
        {
            amount = HotaruCannonDamageUpgradeHelper.PrepareBaseDamage(cannon, target, amount, phaseMultiplier);
            HotaruCannonSupportUpgradeHelper.GetCannonRollMultiplier(cannon, CardType.Attack, resolve: true);
        }
        Intent previous = Pending.Value;
        Intent intent = new() { Cannon = cannon, Target = target, Dealer = dealer, Amount = amount,
            Card = cardSource, Play = cardPlay, IsReaction = reaction,
            Props = hpOnly ? ValueProp.Unblockable | ValueProp.Unpowered : ValueProp.Move };
        Pending.Value = intent;
        try
        {
            if (!reaction) HotaruCannonVigorHelper.MarkPrimaryCommand(cannon);
            using var absolute = absoluteBase && !hpOnly && HbrBattleStatsHelper.IsEnabled(dealer)
                ? HbrAbsoluteBaseDamageContext.Begin(dealer, cardSource, target, amount) : null;
            IEnumerable<DamageResult> results = hpOnly
                ? await HpOnlyDamageContext.Damage(context, target, amount, dealer, cardSource)
                : await CreatureCommandCompatibility.Damage(context, new[] { target }, amount,
                    ValueProp.Move, dealer, cardSource, cardPlay);
            return intent.DpSpent > 0m || results.Any(result => result.UnblockedDamage > 0);
        }
        finally
        {
            intent.Active = false;
            if (ReferenceEquals(Pending.Value, intent)) Pending.Value = previous;
        }
    }
    private static bool CapturesOwner(Creature dealer) => dealer?.Player?.GetRelic<Millsage>()
        ?.HasCannonUpgrade(HotaruCannonKind.Absolution, 6) == true;
    private static bool CapturesIncoming(Creature target) => target?.Player?.GetRelic<Millsage>() is { } relic &&
        (relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 2) || relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 4));
    internal static CommandScope Begin(ref IEnumerable<Creature> targets, decimal amount,
        ValueProp props, Creature dealer, CardModel card, CardPlay play = null)
    {
        Intent intent = Pending.Value;
        CommandScope previous = Current.Value;
        bool claim = intent?.Active == true && !intent.Claimed && props == intent.Props &&
            amount == intent.Amount && ReferenceEquals(dealer, intent.Dealer) && ReferenceEquals(card, intent.Card);
        bool owner = CapturesOwner(dealer);
        bool possibleIncoming = (dealer?.CombatState?.Players.Any(p => CapturesIncoming(p.Creature)) ?? false);
        IReadOnlyList<Creature> candidates = targets as IReadOnlyList<Creature>;
        if (claim || owner || possibleIncoming)
        {
            if (candidates == null && targets != null) targets = candidates = targets.ToArray();
        }
        bool incoming = candidates?.Any(CapturesIncoming) == true;
        if (previous == null && intent?.Active != true && !owner && !incoming) return null;
        if (claim) claim = candidates?.Count == 1 && ReferenceEquals(candidates[0], intent.Target);
        if (claim) intent.Claimed = true;
        CommandScope scope = new() { Previous = previous, Intent = claim ? intent : null,
            Dealer = dealer, Card = card, Play = claim ? intent.Play : play, Props = props,
            Capture = claim || owner || incoming };
        if (scope.Capture && candidates != null)
            foreach (Creature target in candidates)
                if (target != null) scope.DpMaximums[target] = target.GetPower<HighDimensionalShellPower>()?.MaxAmount ?? 0m;
        Current.Value = scope;
        return scope;
    }
    internal static bool IsCurrentDamage(Creature target, Creature dealer, CardModel card, ValueProp props) =>
        PreviewCannon.Value == null && Current.Value?.Matches(target, dealer, card, props) == true;
    private static decimal GetCannonDamageMultiplier(Creature cannon) =>
        cannon?.Monster is HotaruCannonModel model
            ? DamageAmplificationLimits.MultiplySaturated(
                cannon.PetOwner?.GetRelic<Millsage>()?.GetCannonDamageMultiplier(model.Kind) ?? 0.5m,
                HotaruSeeYouAgainHelper.GetCannonMultiplier(cannon))
            : 0.5m;
    internal static decimal GetFinalDamageScale(Creature target, Creature dealer, CardModel card, ValueProp props) =>
        GetFinalDamageScale(target, dealer, card, props, out _);
    internal static decimal GetFinalDamageScale(Creature target, Creature dealer, CardModel card, ValueProp props,
        out bool isCannonDamage)
    {
        isCannonDamage = TryGetCurrentCannon(target, dealer, card, props, out Creature cannon);
        return isCannonDamage ? DamageAmplificationLimits.MultiplySaturated(GetCannonDamageMultiplier(cannon),
            HotaruRareCannonHelper.FinalMultiplier(cannon)) : 1m;
    }
    internal static void ContributeCannonRollMultiplier(Creature target, Creature dealer, CardModel card, ValueProp props) =>
        HotaruCannonDamageUpgradeHelper.ContributeCannonRollMultiplier(target, dealer, card, props);
    internal static bool IsContestSource(Creature target, Creature applier, Millsage relic) =>
        Current.Value?.Matches(target, applier, Current.Value.Card, Current.Value.Intent?.Props ?? default) == true &&
        Current.Value.Cannon?.Monster is HotaruContestCannon && Millsage.IsOwnedCannon(Current.Value.Cannon, relic.Owner);
    internal static void RecordFinalDamage(Creature target, Creature dealer, CardModel card, decimal final)
    {
        if (PreviewCannon.Value != null) return;
        CommandScope scope = Current.Value;
        if (scope?.Active == true && scope.Capture && ReferenceEquals(scope.Dealer, dealer) && ReferenceEquals(scope.Card, card))
            scope.FinalAmounts[target] = Math.Max(0m, final);
    }
    internal static void RecordDpAbsorption(Creature receiver, decimal spent)
    {
        if (PreviewCannon.Value != null) return;
        CommandScope scope = Current.Value;
        if (spent <= 0m || scope?.Active != true || !scope.Capture) return;
        scope.PendingDpReceiver = receiver;
        scope.PendingDpSpent = DamageAmplificationLimits.AddSaturated(scope.PendingDpSpent, spent);
        if (scope.Intent?.Active == true && ReferenceEquals(receiver, scope.Intent.Target))
            scope.Intent.DpSpent = DamageAmplificationLimits.AddSaturated(scope.Intent.DpSpent, spent);
    }
    internal static void BindReceipts(IEnumerable<DamageResult> results)
    {
        if (PreviewCannon.Value != null) return;
        CommandScope scope = Current.Value;
        if (scope?.Active != true || !scope.Capture) return;
        DamageResult[] hits = results as DamageResult[] ?? results.ToArray();
        Creature target = scope.PendingDpReceiver ?? hits.FirstOrDefault()?.Receiver;
        decimal dp = scope.PendingDpSpent;
        scope.PendingDpReceiver = null;
        scope.PendingDpSpent = 0m;
        if (target == null || hits.Length == 0) return;
        decimal hp = hits.Where(r => ReferenceEquals(r.Receiver, target)).Sum(r => Math.Max(0m, r.UnblockedDamage));
        CannonHitReceipt receipt = new() { Cannon = scope.Cannon, Dealer = scope.Dealer, Target = target,
            ActualHpDamage = hp, ActualDpDamage = dp, ActiveDpMaximum = scope.DpMaximums.GetValueOrDefault(target),
            FinalDamage = scope.FinalAmounts.GetValueOrDefault(target), CardSource = scope.Card, CardPlay = scope.Play,
            DamageProps = scope.Props,
            IsReaction = scope.Intent?.IsReaction == true };
        ReceiptState state = new() { Receipt = receipt };
        foreach (DamageResult result in hits) { Receipts.Remove(result); Receipts.Add(result, state); }
    }
    internal static bool CapturesCurrentResults => PreviewCannon.Value == null &&
        Current.Value?.Active == true && Current.Value.Capture;
    internal static bool TryTakeReceipt(DamageResult result, out CannonHitReceipt receipt)
    {
        receipt = null;
        if (result == null || !Receipts.TryGetValue(result, out ReceiptState state) ||
            Interlocked.Exchange(ref state.Taken, 1) != 0) return false;
        receipt = state.Receipt;
        return true;
    }
    internal static Task NotifyDirectDpDamage(PlayerChoiceContext context, Creature dealer,
        Creature target, decimal actualDamage, decimal activeDpMaximum, CardModel cardSource,
        Creature cannon = null, CardPlay cardPlay = null)
    {
        if (actualDamage <= 0m) return Task.CompletedTask;
        // Derived DP damage has already been resolved from actual block. Feed
        // its receipt through the usual owner/cannon reactions without scaling
        // it again or treating it as a new primary attack.
        return FinishAfterDamageGiven(Task.CompletedTask, context, new CannonHitReceipt {
            Cannon = cannon, Dealer = dealer, Target = target,
            ActualDpDamage = actualDamage, ActiveDpMaximum = activeDpMaximum,
            FinalDamage = actualDamage, CardSource = cardSource,
            CardPlay = cardPlay, DamageProps = ValueProp.Unpowered, IsReaction = true
        });
    }

    internal static async Task FinishAfterDamageGiven(Task original, PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        await original;
        if (receipt == null) return;
        HotaruUncommonCannonHelper.OnDamage(receipt);
        await HotaruAbsolutionUpgradeHelper.OnOwnerDpDamage(context, receipt.Dealer, receipt.Target,
            receipt.ActualDpDamage, receipt.CardSource, receipt.DamageProps, receipt.IsPrimary);
        if (receipt.Cannon != null)
        {
            await HotaruCannonDamageUpgradeHelper.AfterCannonDamage(context, receipt);
            await HotaruCannonSupportUpgradeHelper.AfterCannonDamage(context, receipt);
            await HotaruEndgameEffectsHelper.OnCannonDamage(context, receipt);
            await HotaruAbsolutionUpgradeHelper.OnCannonDamage(context, receipt);
            if (receipt.Cannon.PetOwner?.Creature?.GetPower<HotaruMyCoffinPower>() is { } coffin)
                await coffin.OnCannonDamage(context, receipt);
        }
        if (CapturesIncoming(receipt.Target))
            await HotaruCannonSupportUpgradeHelper.AfterOwnerDamage(context, receipt);
    }
    internal static void RestoreCaller(CommandScope scope)
    { if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous; }
    internal static async Task<IEnumerable<DamageResult>> Finish(Task<IEnumerable<DamageResult>> task, CommandScope scope)
    { try { return await task; } finally { Close(scope); } }
    internal static void Close(CommandScope scope)
    { if (scope == null) return; scope.Active = false; RestoreCaller(scope); }
}
