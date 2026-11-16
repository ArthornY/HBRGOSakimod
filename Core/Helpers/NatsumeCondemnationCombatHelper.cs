using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NatsumeCondemnationCombatHelper
{
    private sealed class Battle
    {
        internal readonly Dictionary<(Player Owner, int Number, object Token), long> Plays = new();
    }
    private sealed record Receipt(ICombatState Combat, Player Owner, bool First, decimal Units);
    private sealed class PlayScope : IDisposable
    {
        internal LegacyFlashHbrgoSaki Card;
        internal Receipt Receipt;
        internal AttackCommand Command;
        internal Creature Primary;
        internal PlayScope Previous;
        internal bool Active = true;
        internal bool Condemned;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    internal sealed class CondemnationScope : IDisposable
    {
        internal LegacyFlashHbrgoSaki Card;
        internal readonly HashSet<Creature> Targets;
        internal readonly HashSet<Creature> Confirmed = new();
        internal CondemnationScope Previous;
        internal bool Active = true;
        internal CondemnationScope(LegacyFlashHbrgoSaki card, IEnumerable<Creature> targets)
        { Card = card; Targets = targets.ToHashSet(); Previous = Condemnation.Value; Condemnation.Value = this; }
        public void Dispose() { Active = false; if (ReferenceEquals(Condemnation.Value, this)) Condemnation.Value = Previous; }
    }
    internal sealed class NativeKillScope : IDisposable
    {
        internal CondemnationScope Batch;
        internal NativeKillScope Previous;
        internal Creature Target;
        internal CardModel Card;
        internal bool Active = true, Observed, Prevented;
        public void Dispose() { Active = false; RestoreNativeKillCaller(this); }
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly ConditionalWeakTable<CardPlay, Receipt> Receipts = new();
    private static readonly AsyncLocal<PlayScope> Current = new();
    private static readonly AsyncLocal<CondemnationScope> Condemnation = new();
    private static readonly AsyncLocal<NativeKillScope> NativeKill = new();
    private static readonly Action<DamageResult, bool> MarkFinalKill = AccessTools.MethodDelegate<Action<DamageResult, bool>>(
        AccessTools.PropertySetter(typeof(DamageResult), nameof(DamageResult.WasTargetKilled)));
    private delegate Task KillWithSource(IReadOnlyCollection<Creature> creatures, CardModel source, bool force);
    private static readonly KillWithSource SourceKill = FindSourceKill();
    private static KillWithSource FindSourceKill()
    {
        MethodInfo method = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Kill),
            new[] { typeof(IReadOnlyCollection<Creature>), typeof(CardModel), typeof(bool) });
        return method == null ? null : AccessTools.MethodDelegate<KillWithSource>(method);
    }
    private static (Player, int, object) Turn(Player owner) => (owner, owner.PlayerCombatState?.TurnNumber ?? 0,
        OverclockImmediateTurnCoordinator.GetTurnToken(owner));
    private static long Count(Player owner) => owner?.Creature?.CombatState is { } combat &&
        Battles.TryGetValue(combat, out var battle) ? battle.Plays.GetValueOrDefault(Turn(owner)) : 0L;
    private static decimal LiveUnits(CardModel card)
    {
        int others = card is LegacyFlashHbrgoSaki { IsMutable: true } natsume && natsume.HasUpgradeEffect(LegacyFlashHbrgoSaki.Family)
            && card.Owner?.PlayerCombatState?.Hand is { } hand
                ? hand.Cards.Count(other => !ReferenceEquals(other, card) && ThirtyOneFKeywordRules.IsThirtyOneFCard(other)) : 0;
        return HbrSkillPower.DamageUnits(47m + 10m * others);
    }
    // One O(1) own-turn counter for all actual manual and automatic plays.
    // Preview reads never create a battle, increment a counter or spend RNG.
    internal static void OnCardPlayStarted(CardPlay play)
    {
        CardModel card = play?.Card;
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat ||
            !HbrBattleStatsHelper.IsEnabled(card.Owner) || Receipts.TryGetValue(play, out _)) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        var key = Turn(card.Owner);
        long before = battle.Plays.GetValueOrDefault(key);
        battle.Plays[key] = before == long.MaxValue ? long.MaxValue : before + 1L;
        if (card is LegacyFlashHbrgoSaki) Receipts.Add(play, new(combat, card.Owner, before == 0L, LiveUnits(card)));
    }
    internal static decimal GetAttackUnits(CardModel card) => card is not LegacyFlashHbrgoSaki ? 0m :
        Current.Value is { Active: true } scope && ReferenceEquals(scope.Card, card) ? scope.Receipt.Units : LiveUnits(card);
    internal static decimal GetThresholdPercent(CardModel card) => card is LegacyFlashHbrgoSaki natsume &&
        natsume.IsMutable && natsume.HasUpgradeEffect(LegacyFlashHbrgoSaki.MercifulBlade) ? 20m : 10m;
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (source is not LegacyFlashHbrgoSaki { IsMutable: true } || source.Owner?.Creature != dealer ||
            !props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(dealer) ||
            target != null && target.Side == dealer.Side || HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return false;
        PlayScope scope = Current.Value;
        if (scope?.Active == true)
            return ReferenceEquals(scope.Card, source) && HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command);
        return HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command == null;
    }
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && ((LegacyFlashHbrgoSaki)source).HasUpgradeEffect(LegacyFlashHbrgoSaki.UnwaveringHeart) &&
        (Current.Value is { Active: true } scope ? scope.Receipt.First : Count(source.Owner) == 0L) ? 50m : 0m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? 1.5m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static NativeKillScope BeginNativeKill(Creature target)
    {
        if (Condemnation.Value is not { Active: true } batch) return null;
        NativeKillScope previous = NativeKill.Value;
        bool nested = previous?.Active == true && ReferenceEquals(previous.Batch, batch);
        NativeKillScope scope = new()
        {
            Batch = batch, Target = target, Previous = previous,
            Card = !nested && batch.Targets.Contains(target) && target.IsAlive &&
                LargeCreatureHealthHelper.GetCurrentHp(target) > 0m ? batch.Card : null
        };
        NativeKill.Value = scope;
        return scope;
    }
    internal static void RestoreNativeKillCaller(NativeKillScope scope)
    { if (scope != null && ReferenceEquals(NativeKill.Value, scope)) NativeKill.Value = scope.Previous; }
    internal static void ObserveNativeDeath(Creature target, bool prevented)
    {
        if (NativeKill.Value is not { Active: true } scope || !ReferenceEquals(scope.Target, target)) return;
        scope.Observed = true; scope.Prevented |= prevented;
    }
    internal static async Task FinishNativeKill(Task original, NativeKillScope scope)
    {
        try
        {
            await original;
            if (scope.Card != null && scope.Observed && !scope.Prevented && scope.Batch.Active && scope.Target.IsDead &&
                LargeCreatureHealthHelper.GetCurrentHp(scope.Target) <= 0m && PhantomHelper.CountsAsFinalDefeat(scope.Target))
                scope.Batch.Confirmed.Add(scope.Target);
        }
        finally { scope.Dispose(); }
    }
    internal static CardModel GetCondemnationSource(Creature target) => NativeKill.Value is { Active: true } scope &&
        ReferenceEquals(scope.Target, target) && scope.Batch.Active ? scope.Card : null;
    private static bool Eligible(Creature target, Creature dealer, decimal threshold) => target?.IsAlive == true &&
        target.Side != dealer.Side && ReferenceEquals(target.CombatState, dealer.CombatState) &&
        !NararaNullificationHelper.IsCardNullified(target) && LargeCreatureHealthHelper.GetMaxHp(target) > 0m &&
        LargeCreatureHealthHelper.GetCurrentHp(target) <= LargeCreatureHealthHelper.GetMaxHp(target) * threshold / 100m;
    internal static async Task Condemn(LegacyFlashHbrgoSaki card, Creature primary, AttackCommand command)
    {
        Creature dealer = card.Owner.Creature;
        decimal threshold = GetThresholdPercent(card);
        Creature[] targets = (card.HasUpgradeEffect(LegacyFlashHbrgoSaki.InstantBlade)
                ? dealer.CombatState.HittableEnemies : new[] { primary })
            .Where(target => Eligible(target, dealer, threshold)).Distinct().ToArray();
        if (targets.Length == 0) return;
        using CondemnationScope scope = new(card, targets);
        // force:false retains BeforeDeath/ShouldDie/revival/phase transitions.
        if (SourceKill != null) await SourceKill(targets, card, false);
        else await CreatureCmd.Kill(targets, force: false);
        foreach (Creature target in targets)
        {
            if (!scope.Confirmed.Contains(target)) continue;
            DamageResult last = command.Results.SelectMany(row => row).LastOrDefault(result => ReferenceEquals(result.Receiver, target));
            if (last != null) MarkFinalKill(last, true);
            else
            {
                var credit = new DamageResult(target, ValueProp.Unpowered) { WasTargetKilled = true };
                List<DamageResult> finalHit = command.Results.LastOrDefault();
                if (finalHit != null) finalHit.Add(credit);
                else command.AddResultsInternal(new[] { credit });
            }
        }
    }
    internal static Task BeforeAfterAttack(AttackCommand command)
    {
        PlayScope scope = Current.Value;
        if (scope?.Active != true || scope.Condemned || !ReferenceEquals(scope.Command, command)) return Task.CompletedTask;
        scope.Condemned = true;
        return Condemn(scope.Card, scope.Primary, command);
    }
    internal static async Task Play(PlayerChoiceContext context, LegacyFlashHbrgoSaki card, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(card.Owner)) return;
        ArgumentNullException.ThrowIfNull(play.Target);
        OnCardPlayStarted(play);
        if (!Receipts.TryGetValue(play, out Receipt receipt)) return;
        using PlayScope scope = new() { Card = card, Receipt = receipt, Primary = play.Target, Previous = Current.Value };
        Current.Value = scope;
        if (card.HasUpgradeEffect(LegacyFlashHbrgoSaki.CoolDown))
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, card.Owner.Creature, 75m, card.Owner.Creature, card, remaining: 1);
        if (card.HasUpgradeEffect(LegacyFlashHbrgoSaki.Hope))
            await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, card.Owner.Creature, 40m, card.Owner.Creature, card,
                remaining: 1, profile: new(HbrEffectLimit.Only, HbrEffectExit.Count, 1));
        scope.Command = DamageCmd.Attack(receipt.Units).FromCardCompat(card, play).Targeting(play.Target)
            .WithHitCount(4).WithHitFx("vfx/vfx_attack_slash");
        await scope.Command.Execute(context);
        await BeforeAfterAttack(scope.Command);
    }
}
