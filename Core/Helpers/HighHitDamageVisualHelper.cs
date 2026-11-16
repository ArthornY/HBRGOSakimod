using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Existing action boundaries merge presentation and, in HBR numeric mode,
// divide a skill's fixed destruction budget between its real hits. Native
// damage commands, hooks, targeting, RNG and history still run for every hit.
internal static class HighHitDamageVisualHelper
{
    internal const int MergeThreshold = 10;

    private static readonly AsyncLocal<AttackScope> CurrentAttack = new();
    private static readonly AsyncLocal<CardBatchScope> CurrentCard = new();
    private static readonly AsyncLocal<DamageScope> CurrentDamage = new();
    private static readonly AsyncLocal<DirectDamageBatchScope> CurrentDirectBatch = new();
    private static readonly AsyncLocal<int> DeferredNumberReplay = new();
    private static long NextScopeOrder;
    private static readonly FieldInfo NumberText = AccessTools.Field(typeof(NDamageNumVfx), "_text");
    private static readonly FieldInfo LuckySevenHits = AccessTools.Field(
        typeof(MagicSwordLuckySevenHbrgoSaki), "_magicSwordModeHitCount");

    internal sealed class Total
    {
        internal decimal Damage;
        internal Vector2? Position;
    }

    internal sealed class CardBatchScope
    {
        internal CardModel Card;
        internal CardBatchScope Previous;
        internal bool Active = true;
        internal bool Enabled;
        internal long Order;
        internal bool CountUnknownHits;
        internal long StartedHits;
        internal long ExpectedLoopHits;
        internal decimal DestructionLoopsStarted;
        internal readonly Dictionary<Creature, Total> Totals = new();
        internal readonly List<DirectDamageBatchScope> Contexts = new();
        internal readonly List<DeferredNumber> DeferredNumbers = new();
    }

    internal sealed class AttackScope
    {
        internal AttackCommand Command;
        internal AttackScope Previous;
        internal bool Active = true;
        internal bool Enabled;
        internal CardBatchScope Card;
        internal HbrDestructionGainHelper.SkillScope DestructionSkill;
        internal HbrDestructionGainHelper.Activation DestructionActivation;
        internal bool OwnsDestructionSkill;
        internal long Order;
        internal readonly Dictionary<Creature, Total> Totals = new();
    }

    internal sealed class DirectDamageBatchScope : IDisposable
    {
        internal DirectDamageBatchScope Previous;
        internal Creature Dealer;
        internal CardModel Card;
        internal CardBatchScope CardBatch;
        internal ValueProp Props;
        internal bool Active = true;
        internal bool Enabled;
        internal bool CountUnknownHits;
        internal bool MatchAnyMoveProps;
        internal long StartedHits;
        internal long Order;
        internal readonly Dictionary<Creature, Total> Totals = new();
        internal readonly List<DeferredNumber> DeferredNumbers = new();

        public void Dispose() => CloseDirectDamageBatch(this);
    }

    internal sealed record DeferredNumber(Creature Target, DamageResult Result, bool Blocked);

    internal sealed class DamageScope
    {
        internal HbrDestructionGainHelper.HitScope DestructionHit;
        internal AttackScope Attack;
        internal DirectDamageBatchScope DirectBatch;
        internal DamageScope Previous;
        internal bool Active = true;
        internal bool IsDirectAttackDamage;
        internal bool IsDirectBatchDamage;
        internal Creature Dealer;
    }

    internal static bool IsDirectBatchDamage(DirectDamageBatchScope batch) =>
        batch?.Active == true && CurrentDamage.Value is { Active: true, IsDirectBatchDamage: true } scope &&
        ReferenceEquals(scope.DirectBatch, batch);

    // The card boundary counts Execute calls, not hits within a command. These
    // cards now use one multi-hit Execute; its resolved count owns the splitting.
    internal static CardBatchScope BeginCardBatch(CardModel card) =>
        BeginCardBatch(card, GetExpectedLoopHits(card));

    private static long GetExpectedLoopHits(CardModel card) => card switch
    {
        DragonArrowHbrgoSaki => CardCopyContextHelper.PeekXEnergySpent(card,
            card.EnergyCost.CapturedXValue) > 0 ? 1 : 0,
        MagicSwordLuckySevenHbrgoSaki => (int)(LuckySevenHits.GetValue(card) ?? 0) > 0 ? 1 : 0,
        BladeDanceHbrgoSaki => card.DynamicVars.Repeat.IntValue > 0 ? 1 : 0,
        _ => 0
    };

    internal static CardBatchScope BeginCardBatch(CardModel card, long expectedHits)
    {
        CardBatchScope scope = new()
        {
            Card = card,
            Order = AllocateAttackOrder(),
            Previous = CurrentCard.Value,
            Enabled = expectedHits > MergeThreshold,
            CountUnknownHits = expectedHits > 1,
            ExpectedLoopHits = expectedHits
        };
        CurrentCard.Value = scope;
        return scope;
    }

    internal static void RestoreCardCaller(CardBatchScope scope)
    {
        if (scope != null && ReferenceEquals(CurrentCard.Value, scope))
            CurrentCard.Value = scope.Previous;
    }

    internal static async Task FinishCard(Task original, CardBatchScope scope)
    {
        try { await original; }
        finally { CloseCard(scope); }
    }

    internal static void CloseCard(CardBatchScope scope)
    {
        if (scope?.Active != true) return;
        scope.Active = false;
        RestoreCardCaller(scope);
        foreach (DirectDamageBatchScope context in scope.Contexts) CloseDirectDamageBatch(context);
        if (scope.CountUnknownHits && !scope.Enabled)
            ShowDeferredNumbers(scope.DeferredNumbers);
        else
            ShowTotals(scope.Totals);
        scope.DeferredNumbers.Clear();
    }

    internal static AttackScope BeginAttack(AttackCommand command)
    {
        CardBatchScope card = CurrentCard.Value;
        if (card?.Active == true && ReferenceEquals(card.Card, command.ModelSource))
        {
            // Some loop counts are populated by BeforeCardPlayed after the
            // wrapper started. Read their now-resolved values without replaying
            // any modifier, RNG or card effect.
            long loops = GetExpectedLoopHits(card.Card);
            card.ExpectedLoopHits = loops;
            card.CountUnknownHits |= loops > 1;
            if (loops > MergeThreshold) EnableCardBatch(card);
        }
        AttackScope scope = new()
        {
            Command = command,
            Order = Interlocked.Increment(ref NextScopeOrder),
            Previous = CurrentAttack.Value,
            // Automatic/copy plays retain their real card scope; a live fast
            // ancestor owns the one combined presentation for the whole chain.
            Card = card?.Active == true &&
                ReferenceEquals(card.Card, command.ModelSource) ? card : null
        };
        scope.Enabled = scope.Card?.Enabled == true;
        CurrentAttack.Value = scope;
        return scope;
    }

    internal static bool IsResolvingAttack(AttackCommand command) =>
        CurrentAttack.Value is { Active: true } scope && ReferenceEquals(scope.Command, command);

    internal static long AllocateAttackOrder() => Interlocked.Increment(ref NextScopeOrder);
    internal static (AttackCommand Command, long Order) GetCurrentAttackIdentity() =>
        CurrentAttack.Value is { Active: true } scope ? (scope.Command, scope.Order) : (null, 0L);

    // Replaces only the native call site after BeforeAttack. This sees the final
    // hook-modified count instead of invoking modifiers twice for a preview.
    internal static decimal ResolveHitCount(ICombatState combatState,
        AttackCommand command, int originalHitCount)
    {
        decimal hits = Hook.ModifyAttackHitCount(combatState, command, originalHitCount);
        SetResolvedHitCount(command, hits);
        return hits;
    }

    internal static void SetResolvedHitCount(AttackCommand command, decimal hits)
    {
        AttackScope scope = CurrentAttack.Value;
        if (scope?.Active == true && ReferenceEquals(scope.Command, command))
        {
            scope.Enabled |= hits > MergeThreshold;
            if (scope.Enabled) EnableCardBatch(scope.Card ?? CurrentCard.Value);
            BeginDestructionSkill(scope, command, hits);
        }
    }

    private static void BeginDestructionSkill(AttackScope scope, AttackCommand command, decimal hits)
    {
        if (scope.DestructionSkill != null || hits <= 0m ||
            !command.DamageProps.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(command.Attacker)) return;
        CardModel source = command.ModelSource as CardModel;
        CardBatchScope card = scope.Card;
        bool loopCard = card?.Active == true && card.ExpectedLoopHits > 0;
        if (loopCard)
        {
            decimal loops = card.ExpectedLoopHits;
            decimal previous = Math.Min(loops, card.DestructionLoopsStarted);
            decimal next = Math.Min(loops, previous + 1m);
            card.DestructionLoopsStarted = next;
            // Each outer loop gets an equal portion. Its own resolved count
            // divides that portion, so one-shot extra hits on the first loop
            // cannot change the budget of later native Execute calls.
            decimal coefficient = next / loops - previous / loops;
            scope.DestructionSkill = HbrDestructionGainHelper.BeginSkill(
                command.Attacker, source, hits, HbrDestructionGainHelper.DefaultSkillCoefficient * coefficient);
        }
        else
        {
            scope.DestructionSkill = HbrDestructionGainHelper.BeginSkill(command.Attacker, source, hits);
        }
        HbrDestructionGainHelper.RestoreCaller(scope.DestructionSkill);
        scope.OwnsDestructionSkill = true;
        scope.DestructionActivation = HbrDestructionGainHelper.EnterSkill(scope.DestructionSkill);
    }

    internal static void RestoreAttackCaller(AttackScope scope)
    {
        scope?.DestructionActivation?.RestoreCaller();
        if (scope != null && ReferenceEquals(CurrentAttack.Value, scope))
            CurrentAttack.Value = scope.Previous;
    }

    internal static async Task<AttackCommand> FinishAttack(Task<AttackCommand> original,
        AttackScope scope)
    {
        try { return await original; }
        catch
        {
            HbrComboHelper.CancelAttack(scope?.Command);
            throw;
        }
        finally { CloseAttack(scope); }
    }

    internal static void CloseAttack(AttackScope scope)
    {
        if (scope?.Active != true) return;
        scope.Active = false;
        RestoreAttackCaller(scope);
        scope.DestructionActivation?.Dispose();
        if (scope.OwnsDestructionSkill) scope.DestructionSkill?.Dispose();
        if (scope.Card?.Active == true && scope.Card.Enabled) return;
        ShowTotals(scope.Totals);
    }

    // Non-builder attacks (cannons and phantoms) retain all their original
    // damage calls. This scope supplies only the presentation batch boundary.
    internal static DirectDamageBatchScope BeginDirectDamageBatch(long expectedHits,
        Creature dealer, CardModel card, ValueProp props)
    {
        DirectDamageBatchScope scope = CreateDirectDamageBatch(expectedHits, dealer, card, props);
        scope.CardBatch = CurrentCard.Value?.Active == true ? CurrentCard.Value : null;
        if (scope.Enabled) EnableCardBatch(scope.CardBatch);
        scope.Previous = CurrentDirectBatch.Value;
        CurrentDirectBatch.Value = scope;
        return scope;
    }

    private static DirectDamageBatchScope CreateDirectDamageBatch(long expectedHits,
        Creature dealer, CardModel card, ValueProp props) => new()
    {
        Dealer = dealer,
        Card = card,
        Props = props,
        Enabled = expectedHits > MergeThreshold,
        Order = Interlocked.Increment(ref NextScopeOrder)
    };

    internal static DirectDamageBatchScope RegisterAttackContext(CardBatchScope cardScope,
        Creature dealer, CardModel card, ValueProp props)
    {
        if (cardScope?.Active != true || !ReferenceEquals(cardScope.Card, card)) return null;
        DirectDamageBatchScope scope = CreateDirectDamageBatch(GetExpectedLoopHits(card), dealer, card, props);
        // Unknown contexts buffer their first numbers without re-evaluating
        // gameplay. The eleventh call promotes all their receipts into the
        // card total; shorter contexts replay individual numbers at disposal.
        scope.CountUnknownHits = !scope.Enabled;
        scope.MatchAnyMoveProps = true;
        scope.CardBatch = cardScope;
        cardScope.Contexts.Add(scope);
        if (scope.Enabled) EnableCardBatch(cardScope);
        return scope;
    }

    internal static CardBatchScope CurrentCardBatch => CurrentCard.Value;

    internal static void CloseDirectDamageBatch(DirectDamageBatchScope scope)
    {
        if (scope?.Active != true) return;
        scope.Active = false;
        if (ReferenceEquals(CurrentDirectBatch.Value, scope)) CurrentDirectBatch.Value = scope.Previous;
        if (scope.CountUnknownHits && !scope.Enabled)
            ShowDeferredNumbers(scope.DeferredNumbers);
        else
            ShowTotals(scope.Totals);
        scope.DeferredNumbers.Clear();
    }

    private static DirectDamageBatchScope GetCurrentDirectBatch()
    {
        DirectDamageBatchScope direct = CurrentDirectBatch.Value;
        CardBatchScope card = CurrentCard.Value;
        if (card?.Active == true)
            for (int index = card.Contexts.Count - 1; index >= 0; index--)
            {
                DirectDamageBatchScope candidate = card.Contexts[index];
                if (candidate.Active && (direct?.Active != true || candidate.Order > direct.Order))
                    direct = candidate;
            }
        return direct?.Active == true ? direct : null;
    }

    private static void GetCurrentPresentation(out AttackScope attack, out DirectDamageBatchScope direct)
    {
        attack = CurrentAttack.Value;
        if (attack?.Active != true) attack = null;
        direct = GetCurrentDirectBatch();
        if (attack != null && direct != null)
        {
            if (attack.Order > direct.Order) direct = null;
            else attack = null;
        }
    }

    internal static object CurrentFastPresentationBatch => FindPresentationOwner(false);

    private static object FindPresentationOwner(bool includeUnknown)
    {
        object owner = null;
        long oldest = long.MaxValue;
        void Consider(object scope, long order, bool active, bool enabled)
        {
            if (!active || !enabled || order >= oldest) return;
            owner = scope;
            oldest = order;
        }
        for (CardBatchScope card = CurrentCard.Value; card != null; card = card.Previous)
        {
            Consider(card, card.Order, card.Active,
                card.Enabled || includeUnknown && card.CountUnknownHits);
            if (!card.Active) continue;
            foreach (DirectDamageBatchScope context in card.Contexts)
                Consider(context, context.Order, context.Active,
                    context.Enabled || includeUnknown && context.CountUnknownHits);
        }
        for (AttackScope attack = CurrentAttack.Value; attack != null; attack = attack.Previous)
            Consider(attack, attack.Order, attack.Active, attack.Enabled);
        for (DirectDamageBatchScope direct = CurrentDirectBatch.Value; direct != null; direct = direct.Previous)
            Consider(direct, direct.Order, direct.Active,
                direct.Enabled || includeUnknown && direct.CountUnknownHits);
        return owner;
    }

    private static Dictionary<Creature, Total> OwnerTotals(object owner) => owner switch
    {
        CardBatchScope card => card.Totals,
        AttackScope attack => attack.Totals,
        DirectDamageBatchScope direct => direct.Totals,
        _ => null
    };

    private static void EnableCardBatch(CardBatchScope card)
    {
        if (card?.Active != true) return;
        card.Enabled = true;
        // An automatic/copy play can discover the count inside its parent's
        // reaction. Keep the whole causing card as the one display boundary.
        for (CardBatchScope parent = card.Previous; parent != null; parent = parent.Previous)
            if (parent.Active) parent.Enabled = true;
        Dictionary<Creature, Total> totals = OwnerTotals(CurrentFastPresentationBatch);
        if (totals == null) return;
        // Unknown contexts have retained their initial receipts and numbers.
        // Promote those receipts too, so crossing ten never loses the first hits.
        for (CardBatchScope current = CurrentCard.Value; current != null; current = current.Previous)
        {
            if (!current.Active) continue;
            if (!ReferenceEquals(current.Totals, totals))
            {
                foreach (var entry in current.Totals)
                {
                    Add(totals, entry.Key, entry.Value.Damage);
                    if (!totals[entry.Key].Position.HasValue) totals[entry.Key].Position = entry.Value.Position;
                }
                current.Totals.Clear();
            }
            current.DeferredNumbers.Clear();
            foreach (DirectDamageBatchScope context in current.Contexts)
            {
                if (!context.Active || ReferenceEquals(context.Totals, totals)) continue;
                foreach (var entry in context.Totals)
                {
                    Add(totals, entry.Key, entry.Value.Damage);
                    if (!totals[entry.Key].Position.HasValue) totals[entry.Key].Position = entry.Value.Position;
                }
                context.Totals.Clear();
                context.DeferredNumbers.Clear();
            }
        }
    }

    internal static bool ShouldSkipPresentationWait()
    {
        // A high-hit resolution includes its damage reactions and copied
        // effects. Keeping a timer for every low-hit reaction would still make
        // that batch slow. Floating-number ownership remains isolated below;
        // Both timing and floating numbers inherit the live batch boundary.
        return CurrentFastPresentationBatch != null;
    }

    internal static bool IsDirectAttackDamage(AttackCommand command) =>
        CurrentDamage.Value is { Active: true, IsDirectAttackDamage: true, Attack: { Active: true } attack } &&
        ReferenceEquals(attack.Command, command);

    internal static DamageScope BeginDamage(ValueProp props, Creature dealer, CardModel card)
    {
        GetCurrentPresentation(out AttackScope attack, out DirectDamageBatchScope direct);
        DamageScope previous = CurrentDamage.Value;
        DamageScope scope = new()
        {
            Attack = attack,
            DirectBatch = direct,
            Previous = previous,
            Dealer = dealer,
            DestructionHit = HbrDestructionGainHelper.BeginHit(dealer, card, props),
            IsDirectAttackDamage = attack?.Active == true &&
                ReferenceEquals(attack.Command.Attacker, dealer) &&
                ReferenceEquals(attack.Command.ModelSource as CardModel, card) &&
                attack.Command.DamageProps == props &&
                !(previous?.Active == true && ReferenceEquals(previous.Attack, attack)),
            IsDirectBatchDamage = direct?.Active == true &&
                ReferenceEquals(direct.Dealer, dealer) && ReferenceEquals(direct.Card, card) &&
                (direct.Props == props || direct.MatchAnyMoveProps && props.HasFlag(ValueProp.Move)) &&
                !(previous?.Active == true && ReferenceEquals(previous.DirectBatch, direct))
        };
        if (scope.IsDirectBatchDamage && direct.CountUnknownHits)
        {
            if (direct.StartedHits < long.MaxValue) direct.StartedHits++;
            direct.Enabled |= direct.StartedHits > MergeThreshold;
            if (direct.Enabled) EnableCardBatch(direct.CardBatch);
        }
        if (scope.IsDirectAttackDamage && attack.Card is { Active: true, CountUnknownHits: true } loopCard)
        {
            if (loopCard.StartedHits < long.MaxValue) loopCard.StartedHits++;
            if (loopCard.StartedHits > MergeThreshold) EnableCardBatch(loopCard);
        }
        CurrentDamage.Value = scope;
        return scope;
    }

    internal static void RestoreDamageCaller(DamageScope scope)
    {
        scope?.DestructionHit?.RestoreCaller();
        if (scope != null && ReferenceEquals(CurrentDamage.Value, scope))
            CurrentDamage.Value = scope.Previous;
    }

    internal static async Task<IEnumerable<DamageResult>> FinishDamage(
        Task<IEnumerable<DamageResult>> original, DamageScope scope)
    {
        try { return await original; }
        finally { CloseDamage(scope); }
    }

    internal static void CloseDamage(DamageScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreDamageCaller(scope);
        scope.DestructionHit?.Dispose();
    }

    private static Dictionary<Creature, Total> GetActiveTotals()
    {
        if (CurrentDamage.Value?.Active != true) return null;
        // All reactions inherit the live batch's display owner, even when their
        // dealer, card source or damage properties differ from the primary hit.
        return OwnerTotals(FindPresentationOwner(true));
    }

    internal static bool TryMergeNumber(Creature target, DamageResult result)
    {
        if (DeferredNumberReplay.Value != 0) return false;
        Dictionary<Creature, Total> totals = GetActiveTotals();
        if (totals == null || target == null || result == null) return false;
        // Results are recorded once at the per-original-target append boundary.
        // This factory boundary only suppresses the individual floating number.
        Add(totals, target, 0m);
        if (CurrentFastPresentationBatch == null && FindPresentationOwner(true) is DirectDamageBatchScope pending)
            pending.DeferredNumbers.Add(new DeferredNumber(target, result, false));
        else if (CurrentFastPresentationBatch == null && FindPresentationOwner(true) is CardBatchScope pendingCard)
            pendingCard.DeferredNumbers.Add(new DeferredNumber(target, result, false));
        return true;
    }

    internal static void RecordHit(IReadOnlyList<DamageResult> results)
    {
        Dictionary<Creature, Total> totals = GetActiveTotals();
        if (results == null || totals == null && !HbrDestructionGainHelper.HasActiveHit) return;
        for (int index = 0; index < results.Count; index++)
        {
            Creature receiver = results[index]?.Receiver;
            if (receiver == null) continue;
            HbrDestructionGainHelper.ObserveHit(receiver);
            if (totals != null) Add(totals, receiver, CardDamageHistoryHelper.GetDamage(results, index));
        }
    }

    internal static void RecordDpDamage(Creature receiver, decimal amount)
    {
        object owner = FindPresentationOwner(true);
        Dictionary<Creature, Total> totals = OwnerTotals(owner);
        DamageScope damage = CurrentDamage.Value;
        Creature attacker = damage?.Active == true ? damage.Dealer : null;
        attacker ??= owner switch
        {
            CardBatchScope card => card.Card?.Owner?.Creature,
            AttackScope attack => attack.Command.Attacker,
            DirectDamageBatchScope direct => direct.Dealer,
            _ => null
        };
        if (totals != null && receiver != null && amount > 0m && attacker != null &&
            receiver.Side != attacker.Side &&
            ReferenceEquals(receiver.CombatState, attacker.CombatState))
            Add(totals, receiver, amount);
    }

    internal static bool TryMergeBlocked(Creature target)
    {
        if (DeferredNumberReplay.Value != 0) return false;
        Dictionary<Creature, Total> totals = GetActiveTotals();
        if (totals == null || target == null) return false;
        Add(totals, target, 0m);
        if (CurrentFastPresentationBatch == null && FindPresentationOwner(true) is DirectDamageBatchScope pending)
            pending.DeferredNumbers.Add(new DeferredNumber(target, null, true));
        else if (CurrentFastPresentationBatch == null && FindPresentationOwner(true) is CardBatchScope pendingCard)
            pendingCard.DeferredNumbers.Add(new DeferredNumber(target, null, true));
        return true;
    }

    private static void ShowDeferredNumbers(List<DeferredNumber> numbers)
    {
        if (numbers.Count == 0 || TestMode.IsOn) return;
        DeferredNumberReplay.Value++;
        try
        {
            var room = NCombatRoom.Instance;
            if (room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;
            foreach (DeferredNumber pending in numbers)
            {
                Node number = pending.Blocked
                    ? NDamageBlockedVfx.Create(pending.Target)
                    : NDamageNumVfx.Create(pending.Target, pending.Result);
                if (number != null) room.CombatVfxContainer.AddChildSafely(number);
            }
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Deferred damage presentation failed: {error.Message}");
        }
        finally { DeferredNumberReplay.Value--; }
    }

    private static void Add(Dictionary<Creature, Total> totals, Creature target, decimal amount)
    {
        if (!totals.TryGetValue(target, out Total total))
            totals.Add(target, total = new Total());
        total.Damage = CardDamageHistoryHelper.AddDamage(total.Damage, Math.Max(0m, amount));
        if (total.Position.HasValue || TestMode.IsOn) return;
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(target);
            if (node != null && GodotObject.IsInstanceValid(node))
                total.Position = node.VfxSpawnPosition + new Vector2(0f, -100f);
        }
        catch (Exception error)
        {
            // Presentation failures cannot break the synchronized card effect.
            Log.Warn($"[HBRGOSakiMod] Damage total position unavailable: {error.Message}");
        }
    }

    private static void ShowTotals(Dictionary<Creature, Total> totals)
    {
        if (totals.Count == 0 || TestMode.IsOn) return;
        try
        {
            var room = NCombatRoom.Instance;
            if (room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;
            foreach (var entry in totals)
            {
                NDamageNumVfx number = entry.Value.Position is Vector2 position
                    ? NDamageNumVfx.Create(position, 0)
                    : NDamageNumVfx.Create(entry.Key, 0);
                if (number == null) continue;
                // Native Create takes Int32. The text is set before _Ready so a
                // total above that limit is still displayed exactly.
                NumberText.SetValue(number, CardDamageNumberFormatter.Format(entry.Value.Damage));
                room.CombatVfxContainer.AddChildSafely(number);
            }
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Damage total presentation failed: {error.Message}");
        }
    }
}
