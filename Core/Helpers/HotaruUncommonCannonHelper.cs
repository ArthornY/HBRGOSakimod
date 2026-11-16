using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruUncommonCannonHelper
{
    private static readonly AsyncLocal<ActionScope> Current = new();
    private static readonly AsyncLocal<int> TurnPhaseDepth = new();

    internal sealed class PhaseScope : IDisposable
    {
        private readonly int _previous = TurnPhaseDepth.Value;
        internal PhaseScope() { TurnPhaseDepth.Value++; }
        public void Dispose() { TurnPhaseDepth.Value = _previous; }
    }
    internal static PhaseScope BeginTurnPhase() => new();

    internal sealed class ActionScope : IDisposable
    {
        internal ActionScope Previous;
        internal Creature Cannon;
        internal CardModel Source;
        internal CardPlay Play;
        internal bool IsActivation;
        internal bool IsExtraAction;
        internal bool Executed;
        internal bool ExtraActionRecorded;
        internal HotaruCannonActionModifier Modifier;
        internal decimal BeforeGrowth;
        internal decimal AfterGrowth;
        internal decimal ReplyBlock;
        internal int ReplyDraw;
        internal int ResistanceLoss;
        internal int LightVulnerable;
        internal int LightFragile;
        internal int[] RollBonuses;
        internal bool DidDamage;
        internal bool Completed;
        internal bool Active = true;
        internal SoulRollBreakdown Winning;
        internal IReadOnlyList<SoulRollBreakdown> Discarded;
        internal bool Committed;
        internal bool RollWasNative;
        internal HotaruCannonPhase? BorrowedPhase;
        public void Dispose()
        {
            Active = false;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }

    internal static ActionScope CurrentAction(Creature cannon) =>
        Current.Value is { Active: true } scope && ReferenceEquals(scope.Cannon, cannon) ? scope : null;

    private static HotaruCannonState State(Creature cannon) => cannon?.Monster is HotaruCannonModel model &&
        Millsage.IsOwnedCannon(cannon, cannon.PetOwner) ? cannon.PetOwner.GetRelic<Millsage>().StateFor(model.Kind) : null;

    internal static decimal DamageBonus(Creature cannon) => State(cannon)?.BaseDamageBonus ?? 0m;
    internal static decimal BlockBonus(Creature cannon) => State(cannon)?.BaseBlockBonus ?? 0m;
    internal static void QueueLight(Creature cannon, int amount)
    {
        if (State(cannon) is { } state) state.PendingResistanceLoss = Add(state.PendingResistanceLoss, amount);
    }
    internal static void QueueReply(Creature cannon, decimal block, int cards)
    {
        if (State(cannon) is not { } state) return;
        state.PendingReplyBlock = Math.Min(HotaruCannonState.MaximumStoredValue, state.PendingReplyBlock + Math.Max(0m, block));
        state.PendingReplyDraw = Add(state.PendingReplyDraw, cards);
    }
    internal static void QueueRoll(Creature cannon, int bonus)
    {
        if (State(cannon) is { } state && bonus > 0) state.PendingRollBonuses.Add(bonus);
    }

    internal static void QueueLightDebuffs(Creature cannon, int vulnerable, int fragile)
    {
        if (State(cannon) is not { } state) return;
        state.PendingLightVulnerable = Add(state.PendingLightVulnerable, vulnerable);
        state.PendingLightFragile = Add(state.PendingLightFragile, fragile);
    }

    internal static void MarkExecuted(Creature cannon)
    {
        if (CurrentAction(cannon) is { } action) action.Executed = true;
    }

    internal static void RecordCompletedExtraAction(ActionScope scope)
    {
        if (scope is not { Executed: true, IsExtraAction: true, ExtraActionRecorded: false }) return;
        scope.ExtraActionRecorded = true;
        HotaruRareCannonHelper.RecordExtraAction(scope.Cannon.PetOwner);
    }
    private static int Add(int left, int right) => (int)Math.Clamp((long)left + Math.Max(0, right), 0, int.MaxValue);

    // Called synchronously before any awaited callbacks; nested actions cannot
    // consume this action's instructions, nor reuse its temporary die.
    internal static ActionScope BeginAction(Creature cannon, CardModel source = null, CardPlay play = null,
        bool isActivation = false, HotaruCannonActionModifier modifier = null, bool claimInherited = true,
        bool isExtraAction = false)
    {
        ActionScope scope = new() { Previous = Current.Value, Cannon = cannon, Source = source, Play = play,
            IsActivation = isActivation, IsExtraAction = isExtraAction, Modifier = new HotaruCannonActionModifier {
                BonusPercent = modifier?.BonusPercent ?? 0m, FinalMultiplier = modifier?.FinalMultiplier ?? 1m,
                AdvantageRoll = modifier?.AdvantageRoll ?? false,
                RollAmplificationMultiplier = modifier?.RollAmplificationMultiplier ?? 1m,
                InheritedRoll = modifier?.InheritedRoll
            } };
        if (isActivation && claimInherited && scope.Modifier.InheritedRoll == null)
            scope.Modifier.InheritedRoll = HotaruRareCannonHelper.ClaimInheritedRoll(cannon?.PetOwner);
        Current.Value = scope;
        if (State(cannon) is not { } state) return scope;
        scope.ReplyBlock = state.PendingReplyBlock;
        scope.ReplyDraw = state.PendingReplyDraw;
        scope.ResistanceLoss = state.PendingResistanceLoss;
        scope.LightVulnerable = state.PendingLightVulnerable;
        scope.LightFragile = state.PendingLightFragile;
        scope.RollBonuses = state.PendingRollBonuses.ToArray();
        state.PendingReplyBlock = 0m;
        state.PendingReplyDraw = 0;
        state.PendingResistanceLoss = 0;
        state.PendingLightVulnerable = 0;
        state.PendingLightFragile = 0;
        state.PendingRollBonuses.Clear();
        var owner = cannon.PetOwner.Creature;
        if (TurnPhaseDepth.Value == 0 && cannon.PetOwner.PlayerCombatState.Phase == MegaCrit.Sts2.Core.Combat.PlayerTurnPhase.Play)
            scope.BeforeGrowth = owner.GetPower<HotaruFingertipHabitPower>()?.Claim(cannon) ?? 0m;
        scope.AfterGrowth = owner.GetPower<HotaruFirstBeatsPower>()?.Claim(cannon) ?? 0m;
        return scope;
    }

    internal static Task BeforeAction(PlayerChoiceContext context, ActionScope scope) =>
        Grow(context, scope.Cannon, scope.BeforeGrowth, scope.Source, scope.Play);

    internal static async Task AfterAction(PlayerChoiceContext context, ActionScope scope)
    {
        if (scope.Completed) return;
        scope.Completed = true;
        // Remove action identity before rewards draw/trigger other cards.
        scope.Dispose();
        Player player = scope.Cannon?.PetOwner;
        if (!HotaruCommonCannonHelper.CanAct(player)) return;
        RecordCompletedExtraAction(scope);
        if (scope.Executed && (scope.LightVulnerable > 0 || scope.LightFragile > 0))
            foreach (Creature target in player.Creature.CombatState.Enemies.ToArray())
            {
                if (!HotaruCommonCannonHelper.CanAct(player)) return;
                if (!Millsage.IsEnemy(target, player)) continue;
                if (scope.LightVulnerable > 0)
                    await HbrPercentDebuffApplication.ApplyDefenseDown(context,
                        target, scope.LightVulnerable, scope.Cannon, null);
                if (scope.LightFragile > 0 && Millsage.IsEnemy(target, player))
                    await PowerCmd.Apply<FragilePower>(context, target, scope.LightFragile, scope.Cannon, null);
            }
        // Light Points: one missing debuff per queued point on every enemy (it cut
        // their highest six-sense resistance before 2026-11-09).
        if (scope.ResistanceLoss > 0)
            foreach (Creature target in player.Creature.CombatState.Enemies.ToArray())
            {
                if (!HotaruCommonCannonHelper.CanAct(player)) return;
                if (Millsage.IsEnemy(target, player))
                    await HbrMissingDebuffHelper.ApplyRandomMissing(context, target, scope.Cannon, scope.Source,
                        scope.ResistanceLoss);
            }
        if (!HotaruCommonCannonHelper.CanAct(player)) return;
        if (scope.DidDamage && scope.ReplyBlock > 0m)
            await CreatureCmd.GainBlock(player.Creature, scope.ReplyBlock, ValueProp.Move, null);
        else if (!scope.DidDamage && scope.ReplyDraw > 0)
            await CardPileCmd.Draw(context, scope.ReplyDraw, player);
        await Grow(context, scope.Cannon, scope.AfterGrowth, scope.Source, scope.Play);
    }

    internal static void OnDamage(CannonHitReceipt receipt)
    {
        if (receipt?.ActualDamage > 0m && CurrentAction(receipt.Cannon) is { } action)
            action.DidDamage = true;
    }

    internal static async Task Grow(PlayerChoiceContext context, Creature cannon, decimal amount,
        CardModel source = null, CardPlay play = null)
    {
        if (amount <= 0m || State(cannon) is not { } state || cannon.Monster is not HotaruCannonModel model) return;
        Millsage relic = cannon.PetOwner.GetRelic<Millsage>();
        decimal before = HotaruMyIndulgencePower.ModifyBaseDamage(cannon, model.CurrentDamage + state.BaseDamageBonus);
        state.BaseDamageBonus = Math.Min(HotaruCannonState.MaximumStoredValue, state.BaseDamageBonus + amount);
        state.BaseBlockBonus = Math.Min(HotaruCannonState.MaximumStoredValue, state.BaseBlockBonus + amount);
        decimal after = HotaruMyIndulgencePower.ModifyBaseDamage(cannon, model.CurrentDamage + state.BaseDamageBonus);
        await relic.NotifyCannonBaseDamageIncreased(context, cannon, after - before, source, play);
        await CombatPreviewRefreshHelper.RefreshCreatureIntent(cannon);
    }

    internal static async Task OnTurnStart(PlayerChoiceContext context, Player player, object token)
    {
        if (player?.Creature?.GetPower<HotaruFirstBeatsPower>() is { } beats) await beats.RemoveStale(context);
        if (player?.Creature?.GetPower<HotaruWakingCommandPower>() is { } waking) await waking.Resolve(context);
    }

    internal static Task OnTurnEnd(PlayerChoiceContext context, Player player, object token) =>
        player?.Creature?.GetPower<HotaruFirstBeatsPower>()?.Expire(context, token) ?? Task.CompletedTask;
}
