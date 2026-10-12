using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class WorldsFinestCombatHelper
{
    private sealed class Consumption { internal int Reserved; }
    private sealed class RevivalGuard { internal int Active; }
    private sealed class CombatStart { internal bool Applied; }
    internal sealed class DamageScope
    {
        internal DamageScope Previous;
        internal bool Active = true;
        internal readonly List<Revival> Pending = new();
        internal readonly List<Revival> Reservations = new();
    }
    internal sealed class Revival
    {
        internal Player Player;
        internal WorldsFinestHbrgoSaki Card;
        internal CardModel[] CombatCopies;
        internal int RecoveryTier;
        internal bool RecoverySelected;
        internal bool RestoreHp;
        internal bool RestoreDp;
        internal bool Committed;
    }
    private static readonly AsyncLocal<DamageScope> Current = new();
    private static readonly ConditionalWeakTable<CardModel, Consumption> Consumed = new();
    private static readonly ConditionalWeakTable<Player, RevivalGuard> Guards = new();
    private static readonly ConditionalWeakTable<ICombatState, CombatStart> Started = new();

    private static IEnumerable<WorldsFinestHbrgoSaki> PermanentCards(Player player) =>
        player?.Deck?.Cards.OfType<WorldsFinestHbrgoSaki>().Where(card => card.IsMutable &&
            card.Owner == player && !card.HasBeenRemovedFromState && card.Pile?.Type == PileType.Deck) ??
        Enumerable.Empty<WorldsFinestHbrgoSaki>();

    internal static decimal GetDamageMultiplier(Creature target)
    {
        SpiritTalismanPower talisman = target?.GetPower<SpiritTalismanPower>();
        if (talisman?.Amount <= 0 || talisman == null) return 1m;
        decimal factor = talisman.DamageMultiplier;
        if (target.CombatState?.Players.Any(player => player.Creature.Side != target.Side &&
            PermanentCards(player).Any(card => card.HasUpgradeEffect(5))) == true)
            factor *= 1.5m;
        return factor;
    }

    internal static async Task RestoreDp(PlayerChoiceContext context, CardModel card, decimal amount)
    {
        Player player = card?.Owner;
        if (player?.Creature?.IsAlive != true || amount <= 0m) return;
        Creature phantom = PhantomHelper.GetLivingPhantom(player);
        if (phantom != null)
            await PhantomHelper.RestoreDp(context, phantom, amount, card);
        else player.GetRelic<DeflectorProtection>()?.RestoreDp((int)Math.Min(amount, int.MaxValue));
    }

    internal static async Task StartCombat(ICombatState combatState)
    {
        if (combatState == null) return;
        CombatStart started = Started.GetOrCreateValue(combatState);
        if (started.Applied) return;
        started.Applied = true;
        Player[] owners = combatState.Players.Where(player =>
            PermanentCards(player).Any(card => card.HasUpgradeEffect(4))).ToArray();
        if (owners.Length == 0) return;
        Creature[] targets = combatState.Creatures.Where(creature => creature.IsAlive &&
            owners.Any(player => creature.Side != player.Creature.Side)).ToArray();
        foreach (Creature target in targets)
        {
            Player owner = owners.First(player => player.Creature.Side != target.Side);
            CardModel source = PermanentCards(owner).First(card => card.HasUpgradeEffect(4));
            await PowerCmd.Apply<SpiritTalismanPower>(new ThrowingPlayerChoiceContext(),
                target, 1m, owner.Creature, source);
        }
    }

    internal static DamageScope BeginDamage()
    {
        DamageScope scope = new() { Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }
    internal static void RestoreCaller(DamageScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }
    internal static async Task<IEnumerable<DamageResult>> FinishDamage(
        Task<IEnumerable<DamageResult>> task, DamageScope scope)
    {
        try { return await task; }
        finally { CloseDamage(scope); }
    }
    internal static void CloseDamage(DamageScope scope)
    {
        if (scope == null || !scope.Active) return;
        scope.Active = false;
        foreach (Revival revival in scope.Reservations)
        {
            // A failed pre-HP hook must not consume a memory without removing
            // its real permanent card, or leak protection into later commands.
            if (!revival.Committed && !revival.Card.HasBeenRemovedFromState)
                Volatile.Write(ref Consumed.GetOrCreateValue(revival.Card).Reserved, 0);
            Volatile.Write(ref Guards.GetOrCreateValue(revival.Player).Active, 0);
        }
        scope.Pending.Clear();
        RestoreCaller(scope);
    }

    internal static decimal PreventLethalHpLoss(Creature target, decimal amount, HpLossHookPhase phases)
    {
        DamageScope scope = Current.Value;
        Player player = target?.Player;
        decimal hp = LargeCreatureHealthHelper.GetCurrentHp(target);
        if (scope?.Active != true || !phases.HasFlag(HpLossHookPhase.AfterOsty) ||
            player?.Creature != target || hp <= 0m || target.MaxHp <= 0 || amount < hp)
            return amount;
        RevivalGuard guard = Guards.GetOrCreateValue(player);
        if (Volatile.Read(ref guard.Active) != 0) return amount;
        WorldsFinestHbrgoSaki card = PermanentCards(player).FirstOrDefault(candidate =>
            candidate.HasUpgradeEffect(0) && Volatile.Read(ref Consumed.GetOrCreateValue(candidate).Reserved) == 0);
        if (card == null || Interlocked.CompareExchange(ref guard.Active, 1, 0) != 0) return amount;
        if (Interlocked.CompareExchange(ref Consumed.GetOrCreateValue(card).Reserved, 1, 0) != 0)
        {
            Volatile.Write(ref guard.Active, 0);
            return amount;
        }
        Revival revival = new()
        {
            Player = player,
            Card = card,
            RecoveryTier = card.HasUpgradeEffect(2) ? 2 : card.HasUpgradeEffect(1) ? 1 : 0,
            CombatCopies = player.PlayerCombatState?.AllCards.Where(copy =>
                !copy.HasBeenRemovedFromState && ReferenceEquals(copy.DeckVersion, card)).ToArray() ??
                Array.Empty<CardModel>()
        };
        scope.Pending.Add(revival);
        scope.Reservations.Add(revival);
        return 0m;
    }

    internal static Revival[] TakePendingRevivals()
    {
        DamageScope scope = Current.Value;
        if (scope?.Active != true || scope.Pending.Count == 0) return Array.Empty<Revival>();
        Revival[] pending = scope.Pending.ToArray();
        scope.Pending.Clear();
        return pending;
    }
    internal static async Task FinishHpModifiers(Task task, Revival[] pending)
    {
        await task;
        foreach (Revival revival in pending)
        {
            // Remove through native commands once: this persists deck removal
            // and retains the normal history, removal hooks and pile events.
            await CardPileCmd.RemoveFromDeck(revival.Card, showPreview: false);
            foreach (CardModel copy in revival.CombatCopies)
                if (!copy.HasBeenRemovedFromState && copy.Pile?.IsCombatPile == true)
                    await CardPileCmd.RemoveFromCombat(copy, skipVisuals: true);
            if (!revival.RecoverySelected)
            {
                if (revival.RecoveryTier >= 2)
                    revival.RestoreHp = revival.RestoreDp = true;
                else if (revival.RecoveryTier == 1)
                {
                    // Use the saved run's deterministic stream, and select only
                    // after this permanent card has really been consumed.
                    revival.RestoreHp = revival.Player.RunState.Rng.Niche.NextInt(2) == 0;
                    revival.RestoreDp = !revival.RestoreHp;
                }
                revival.RecoverySelected = true;
            }
            if (revival.RestoreHp)
                await CreatureCmd.SetCurrentHp(revival.Player.Creature,
                    LargeCreatureHealthHelper.GetMaxHp(revival.Player.Creature));
            else if (revival.RecoveryTier == 0)
                await CreatureCmd.SetCurrentHp(revival.Player.Creature, 1m);
            if (revival.RestoreDp)
                revival.Player.GetRelic<DeflectorProtection>()?.RestoreDpAfterHalfBodyRevival();
            revival.Committed = true;
        }
    }
}
