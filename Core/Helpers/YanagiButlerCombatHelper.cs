using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class YanagiButlerCombatHelper
{
    private sealed record Preparation(Player Owner, int Turn, object Token, int Copies);
    private sealed record Inherited(ICombatState Combat, List<Preparation> Marks);
    private sealed class Battle
    {
        internal readonly HashSet<CardModel> HighBeamsUsed = new();
        internal readonly Dictionary<CardModel, List<Preparation>> Prepared = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly ConditionalWeakTable<CardModel, Inherited> Copies = new();
    internal sealed class CleaningScope : IDisposable
    {
        internal AsTheYoungLadyCommandsHbrgoSaki Card;
        internal AttackCommand Command;
        internal CleaningScope Previous;
        internal bool Active = true;
        internal decimal ResourceDamage;
        internal readonly HashSet<DamageResult> Results = new();
        public void Dispose() { Active = false; if (ReferenceEquals(Cleaning.Value, this)) Cleaning.Value = Previous; }
    }
    private static readonly AsyncLocal<CleaningScope> Cleaning = new();
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
    private static (int, object) Turn(Player player) => (player.PlayerCombatState?.TurnNumber ?? 0,
        OverclockImmediateTurnCoordinator.GetTurnToken(player));
    private static List<Preparation> ReadMarks(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return null;
        List<Preparation> marks = null;
        if (Battles.TryGetValue(combat, out var battle)) battle.Prepared.TryGetValue(Physical(card), out marks);
        if (marks == null && Copies.TryGetValue(card, out var copy) && ReferenceEquals(copy.Combat, combat)) marks = copy.Marks;
        // The common unprepared path must not ask native Pile to search every
        // battle pile. A permanent source is excluded only after a mark exists.
        if (marks == null || card.Owner.Deck?.Cards.Contains(card) == true) return null;
        return marks;
    }
    internal static decimal GetPreparedBonusPercent(CardModel source, Creature dealer, ValueProp props)
    {
        if (source?.IsMutable != true || dealer == null || source.Owner == null ||
            !HbrBattleStatsHelper.IsEnabled(source.Owner) || HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return 0m;
        if (!ReferenceEquals(source.Owner.Creature, dealer))
        {
            AttackCommand command = HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command;
            if (!ReferenceEquals(dealer.PetOwner, source.Owner) || !ReferenceEquals(command?.Attacker, dealer) ||
                !ReferenceEquals(command.ModelSource, source) || !HighHitDamageVisualHelper.IsDirectAttackDamage(command)) return 0m;
        }
        var turn = Turn(source.Owner);
        if (Completed(source.Owner, turn.Item1, turn.Item2)) return 0m;
        var marks = ReadMarks(source);
        if (marks == null) return 0m;
        int count = 0;
        foreach (var mark in marks)
            if (ReferenceEquals(mark.Owner, source.Owner) && mark.Turn == turn.Item1 && ReferenceEquals(mark.Token, turn.Item2)) count += mark.Copies;
        return count * 20m;
    }
    internal static void Prepare(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat ||
            !HbrBattleStatsHelper.IsEnabled(card.Owner) || card.HasBeenRemovedFromState) return;
        var turn = Turn(card.Owner);
        Battle battle = Battles.GetOrCreateValue(combat);
        CardModel key = Physical(card);
        if (!battle.Prepared.TryGetValue(key, out var marks))
            battle.Prepared.Add(key, marks = ReadMarks(card)?.ToList() ?? new());
        int index = marks.FindIndex(mark => ReferenceEquals(mark.Owner, card.Owner) && mark.Turn == turn.Item1 &&
            ReferenceEquals(mark.Token, turn.Item2));
        int count = index >= 0 ? Math.Min(2, marks[index].Copies + 1) : 1;
        Preparation next = new(card.Owner, turn.Item1, turn.Item2, count);
        if (index >= 0) marks[index] = next; else marks.Add(next);
        if (CombatManager.Instance?.StateTracker != null)
            CombatPreviewRefreshHelper.RefreshCombatState(card.Owner.Creature, nameof(Prepare));
    }
    internal static void Inherit(CardModel source, CardModel clone)
    {
        if (source?.IsMutable != true || source.Owner?.Creature?.CombatState is not { } combat || ReadMarks(source) is not { } marks) return;
        var turn = Turn(source.Owner);
        if (Completed(source.Owner, turn.Item1, turn.Item2)) return;
        var active = marks.Where(mark => ReferenceEquals(mark.Owner, source.Owner) && mark.Turn == turn.Item1 && ReferenceEquals(mark.Token, turn.Item2)).ToList();
        if (active.Count == 0) return;
        Copies.Remove(clone); Copies.Add(clone, new(combat, active));
    }
    internal static void OnTurnEnd(Player owner, object token = null)
    {
        if (owner?.Creature?.CombatState is not { } combat || !Battles.TryGetValue(combat, out var battle)) return;
        foreach (var marks in battle.Prepared.Values)
            marks.RemoveAll(mark => ReferenceEquals(mark.Owner, owner) && ReferenceEquals(mark.Token, token));
        // Inherited snapshots are read only until first preparation. Their
        // validity is additionally controlled by the completed-turn ledger.
        CompletedTurns.GetOrCreateValue(combat).Add((owner, owner.PlayerCombatState?.TurnNumber ?? 0, token));
    }
    private static readonly ConditionalWeakTable<ICombatState, HashSet<(Player, int, object)>> CompletedTurns = new();
    private static bool Completed(Player owner, int number, object token) => owner?.Creature?.CombatState is { } combat &&
        CompletedTurns.TryGetValue(combat, out var completed) && completed.Contains((owner, number, token));
    internal static bool IsEnergyTarget(AsTheYoungLadyCommandsHbrgoSaki source, Creature target) => source?.IsMutable == true &&
        target?.IsAlive == true && target.Player?.Creature == target && target.Side == source.Owner.Creature.Side &&
        ReferenceEquals(target.CombatState, source.Owner.Creature.CombatState);
    private static bool CostCanChange(CardModel card)
    {
        if (card.EnergyCost.CostsX || card.EnergyCost.Canonical < 0) return false;
        bool playable = card.CanPlay(out UnplayableReason reason, out _);
        return playable || reason != UnplayableReason.None &&
            (reason & ~(UnplayableReason.EnergyCostTooHigh | UnplayableReason.StarCostTooHigh)) == UnplayableReason.None;
    }
    private static bool IsCandidate(CardModel card, Player owner, bool pocket) => card?.IsMutable == true && card.Owner == owner &&
        !card.HasBeenRemovedFromState && (card.Pile?.Type == PileType.Draw || pocket && card.Pile?.Type == PileType.Discard);
    internal static async Task Fetch(PlayerChoiceContext context, AsTheYoungLadyCommandsHbrgoSaki source)
    {
        Player owner = source.Owner;
        if (owner?.Creature?.IsAlive != true || owner.PlayerCombatState == null || CombatManager.Instance.IsOverOrEnding) return;
        ICombatState combat = owner.Creature.CombatState;
        bool pocket = source.HasUpgradeEffect(AsTheYoungLadyCommandsHbrgoSaki.AlwaysAtHand);
        CardModel[] all = owner.PlayerCombatState.DrawPile.Cards.Concat(owner.PlayerCombatState.DiscardPile.Cards)
            .Where(card => IsCandidate(card, owner, true)).ToArray();
        CardModel[] ladies = source.HasUpgradeEffect(AsTheYoungLadyCommandsHbrgoSaki.LadyFirst)
            ? all.Where(card => HbrMemoryGarden.CharacterIdOf(card) == "kanata").ToArray() : Array.Empty<CardModel>();
        bool priority = ladies.Length > 0;
        CardModel selected;
        if (priority) selected = ladies.Length == 1 ? ladies[0] : ladies[owner.RunState.Rng.CombatCardSelection.NextInt(ladies.Length)];
        else
        {
            CardModel[] candidates = all.Where(card => IsCandidate(card, owner, pocket)).ToArray();
            if (candidates.Length == 0) return;
            var prefs = new CardSelectorPrefs(new LocString("cards", source.Id.Entry + ".selectionPrompt"), 1);
            selected = (pocket
                ? await CardSelectCmd.FromSimpleGrid(context, candidates, owner, prefs)
                : await CardSelectCmd.FromCombatPile(context, owner.PlayerCombatState.DrawPile, owner, prefs, card => candidates.Contains(card)))
                .FirstOrDefault();
        }
        if (selected == null || !ReferenceEquals(owner.Creature.CombatState, combat) || !IsCandidate(selected, owner, pocket || priority)) return;
        bool reduce = CostCanChange(selected);
        CardPileAddResult result = await CardPileCmd.Add(selected, PileType.Hand);
        if (!result.success || !ReferenceEquals(result.cardAdded, selected) || selected.Owner != owner || selected.HasBeenRemovedFromState ||
            !ReferenceEquals(owner.Creature.CombatState, combat)) return;
        if (reduce)
        {
            if (priority) selected.EnergyCost.SetThisTurn(0, reduceOnly: true);
            else selected.EnergyCost.AddThisTurn(-1, reduceOnly: true);
            selected.InvokeEnergyCostChanged();
        }
        if (source.HasUpgradeEffect(AsTheYoungLadyCommandsHbrgoSaki.Prepared)) Prepare(selected);
    }
    internal static bool IsOwnCleaning(CardModel source, Creature dealer, ValueProp props) =>
        Cleaning.Value is { Active: true } scope && ReferenceEquals(scope.Card, source) && dealer == scope.Card.Owner.Creature &&
        props.IsPoweredAttack() && !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source) &&
        HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command);
    internal static void RecordDamage(CardModel source, Creature dealer, DamageResult result, ValueProp props)
    {
        if (!IsOwnCleaning(source, dealer, props) || result?.Receiver is not { } target || target.Side == dealer.Side) return;
        CleaningScope scope = Cleaning.Value;
        if (scope.Results.Add(result)) scope.ResourceDamage = DamageAmplificationLimits.AddSaturated(scope.ResourceDamage,
            CombatAttackHitHelper.GetResourceDamage(result));
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        target != null && target.Side != dealer?.Side && IsOwnCleaning(source, dealer, props)
            ? 1.2m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static decimal ScaleRecovery(CardModel source, decimal amount) =>
        HbrCardEffectBonusHelper.ScaleRecovery(source, amount);
    // One fixed recovery budget is amplified before it is allocated to DP and
    // then HP. Neither the DP portion nor its HP remainder is amplified again.
    internal static async Task RestoreDpThenHp(Creature target, decimal amount, CardModel source)
    {
        if (target == null || amount <= 0m) return;
        decimal remaining = ScaleRecovery(source, amount);
        if (target.GetPower<HBRGOSakiMod.Core.Models.Powers.HighDimensionalShellPower>() is { } shell)
        {
            decimal restored = shell.Restore(remaining);
            remaining -= restored;
            MariaRepentanceCombatHelper.OnDpRestored(target, restored, source);
        }
        if (remaining > 0m) await CreatureCmd.Heal(target, remaining);
    }
    internal static async Task Play(PlayerChoiceContext context, AsTheYoungLadyCommandsHbrgoSaki card, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(card.Owner)) return;
        Player owner = card.Owner;
        if (card.HasUpgradeEffect(AsTheYoungLadyCommandsHbrgoSaki.Maintenance))
            await PlayerCmd.GainEnergy(1m, card.TargetType == TargetType.AnyAlly && IsEnergyTarget(card, play.Target) ? play.Target.Player : owner);
        Battle battle = Battles.GetOrCreateValue(owner.Creature.CombatState);
        if (card.HasUpgradeEffect(AsTheYoungLadyCommandsHbrgoSaki.HighBeams) && battle.HighBeamsUsed.Add(Physical(card)))
            foreach (Creature ally in HbrPlayerTeamTargets.Get(owner.Creature))
                await SeiraDpRecoveryHelper.RestoreWithCeiling(context, ally, .15m, true, card, 1.5m);
        if (card.HasUpgradeEffect(AsTheYoungLadyCommandsHbrgoSaki.ElegantCleaning))
        {
            using CleaningScope scope = new() { Card = card, Previous = Cleaning.Value };
            var command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
                .TargetingAllOpponents(owner.Creature.CombatState).WithHitCount(2).WithHitFx("vfx/vfx_attack_slash");
            scope.Command = command; Cleaning.Value = scope;
            await command.Execute(context);
            decimal recovery = decimal.Floor(scope.ResourceDamage * .3m);
            if (recovery > 0m) await SeiraDpRecoveryHelper.RestoreAmount(context, FubukiChangeCombatHelper.DpTarget(owner), recovery, false, card);
        }
        await Fetch(context, card);
    }
}
