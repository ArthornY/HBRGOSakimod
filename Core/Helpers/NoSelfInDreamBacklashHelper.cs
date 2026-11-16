using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// This budget belongs to the creature's combat turn, not the replaceable power.
// Returning from an inserted turn restores the ordinary turn's spent budget.
internal static class NoSelfInDreamBacklashHelper
{
    private readonly record struct TurnKey(int Round, CombatSide Side, int Number, object Extra);
    internal readonly record struct Backlash(Creature Target, decimal Amount);
    private sealed class Turn
    {
        internal readonly Dictionary<Creature, decimal> Spent = new(ReferenceEqualityComparer.Instance);
        internal readonly HashSet<DamageResult> Results = new(ReferenceEqualityComparer.Instance);
    }
    private sealed class Holder
    {
        internal readonly Dictionary<TurnKey, Turn> Turns = new();
    }
    private sealed class Battle
    {
        internal readonly ConditionalWeakTable<Creature, Holder> Holders = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();

    internal static Backlash[] Reserve(Creature owner, DamageResult result, decimal requested,
        IReadOnlyList<Creature> targets)
    {
        if (owner?.CombatState is not { } combat || result == null || requested < 1m || targets.Count == 0)
            return Array.Empty<Backlash>();

        var player = owner.Player ?? owner.PetOwner;
        var key = new TurnKey(combat.RoundNumber, combat.CurrentSide,
            player?.PlayerCombatState?.TurnNumber ?? combat.RoundNumber,
            player == null ? null : OverclockImmediateTurnCoordinator.GetTurnToken(player));
        Holder holder = Battles.GetOrCreateValue(combat).Holders.GetOrCreateValue(owner);
        lock (holder)
        {
            if (!holder.Turns.TryGetValue(key, out Turn turn)) holder.Turns.Add(key, turn = new());
            // A repeated hook, or replacing/reapplying the ability during the
            // same hit, must not charge a second copy of that hit's backlash.
            if (!turn.Results.Add(result)) return Array.Empty<Backlash>();
            var reserved = new List<Backlash>(targets.Count);
            // Match the command's physical-target projection before splitting
            // unequal amounts. A player and its phantom are one native hit.
            foreach (Creature target in PhantomDamageTargetPatch.RedirectTargets(targets))
            {
                if (target?.IsAlive != true || !ReferenceEquals(target.CombatState, combat) || target.Side != owner.Side)
                    continue;
                decimal amount = decimal.Floor(requested);
                // Query the same pure native recipient hook used after block
                // and DP. Keep the physical target on the command so its own
                // BeforeDamageReceived, block and DP still resolve normally.
                // Reserve all possible recipients: redirected overkill can
                // return to the physical target, and several allies can share
                // one Red Crimson or Feint recipient.
                Creature recipient = Hook.ModifyUnblockedDamageTarget(
                    combat, target, amount, ValueProp.Unpowered, null) ?? target;
                var recipients = new HashSet<Creature>(ReferenceEqualityComparer.Instance)
                    { target, recipient, GetBlockRecipient(target) };
                AddLivingRedirectFallbacks(target, recipients);
                foreach (Creature receiver in recipients)
                {
                    decimal cap = decimal.Floor(Math.Max(0m,
                        LargeCreatureHealthHelper.GetMaxHp(receiver)) * 0.25m);
                    amount = Math.Min(amount, Math.Max(0m, cap - turn.Spent.GetValueOrDefault(receiver)));
                }
                if (amount <= 0m) continue;
                // Reserve every target before the first awaited damage hook.
                // Block, DP and mitigation keep their normal behavior and do
                // not refund budget, so nested reactions cannot exceed it.
                foreach (Creature receiver in recipients)
                    turn.Spent[receiver] = turn.Spent.GetValueOrDefault(receiver) + amount;
                reserved.Add(new(target, amount));
            }
            return reserved.ToArray();
        }
    }

    private static Creature GetBlockRecipient(Creature target)
    {
        // Native pet hits resolve block on the owner. The existing Phantom
        // context redirects that call back to the phantom; DamageTransfer and
        // RedCrimson context then select their normal physical DP receiver.
        Creature receiver = target.HasPower<PhantomPower>()
            ? target : target.PetOwner?.Creature ?? target;
        return receiver.GetPower<DamageTransferPower>()?.GetLivingRedCrimson() ??
            (target.Monster is RedCrimson && target.PetOwner?.Creature == receiver
                ? target : receiver);
    }

    private static void AddLivingRedirectFallbacks(Creature target, HashSet<Creature> recipients)
    {
        // An earlier awaited hit may kill the currently selected redirector.
        // Reserve only existing live fallback routes, before any command can
        // yield. Phantom's native postfix always retains its own HP target.
        if (!target.HasPower<PhantomPower>())
            foreach (Creature ally in target.CombatState.Allies)
                if (ally.IsAlive && ally.Side == target.Side && ally.HasPower<FeintDamageRedirectPower>())
                    recipients.Add(ally);

        Creature blockSource = target.HasPower<PhantomPower>()
            ? target : target.PetOwner?.Creature ?? target;
        foreach (Creature source in recipients.Append(blockSource).ToArray())
        {
            if (source.Player == null || !source.HasPower<DamageTransferPower>()) continue;
            foreach (Creature ally in target.CombatState.Allies)
                if (ally.IsAlive && ally.PetOwner == source.Player && ally.Monster is RedCrimson)
                    recipients.Add(ally);
        }
    }
}
