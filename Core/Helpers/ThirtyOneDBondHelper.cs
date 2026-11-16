using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneDBondHelper
{
    private sealed class TurnLedger
    {
        internal ICombatState Combat;
        internal readonly HashSet<(int OwnTurn, object ExtraTurn)> Turns = new();
    }
    private static readonly ConditionalWeakTable<Player, TurnLedger> Ledgers = new();
    internal static void ResetBattle(Player player) { if (player != null) Ledgers.Remove(player); }
    internal static Task ClearIntel(Player player) => player?.Creature?.GetPower<HbrIntelPower>() is { } intel
        ? PowerCmd.Remove(intel) : Task.CompletedTask;
    internal static async Task TurnStart(PlayerChoiceContext context, ThirtyOneDCollection relic)
    {
        Player player = relic.Owner;
        if (relic.IsMelted || player?.Creature is not { IsAlive: true, CombatState: { } combat } owner ||
            !HbrBattleStatsHelper.IsEnabled(player)) return;
        TurnLedger ledger = Ledgers.GetOrCreateValue(player);
        if (!ReferenceEquals(ledger.Combat, combat)) { ledger.Combat = combat; ledger.Turns.Clear(); }
        if (!ledger.Turns.Add((player.PlayerCombatState?.TurnNumber ?? combat.RoundNumber,
                OverclockImmediateTurnCoordinator.GetTurnToken(player)))) return;
        int kinds = ThirtyOneDKeywordRules.GetPermanentDistinctCount(player);
        if (kinds < 2) { await ClearIntel(player); return; }
        HbrIntelPower intel = owner.GetPower<HbrIntelPower>();
        int previous = Math.Max(0, intel?.Amount ?? 0);
        int next = Math.Min(kinds, previous + 1);
        if (intel == null) await PowerCmd.Apply<HbrIntelPower>(context, owner, 1m, owner, null);
        else if (next != previous) await PowerCmd.ModifyAmount(context, intel, next - previous, owner, null);
        if (next != previous) relic.Flash();
        if (kinds < 6 || owner.GetPowerAmount<HbrIntelPower>() < 6 || !relic.TryReserveIntelReward()) return;
        // Reserve before the first await: power callbacks and draw hooks may
        // themselves start another action or an immediate personal turn.
        relic.Flash();
        await GrantTeamMorale(context, owner, 4m);
        await CardPileCmd.Draw(context, 2m, player);
    }
    internal static async Task GrantTeamMorale(PlayerChoiceContext context, Creature source, decimal levels)
    {
        if (levels <= 0m || source?.CombatState is not { } combat) return;
        Creature[] allies = combat.Creatures.Where(creature => creature.IsAlive && creature.Side == source.Side &&
            (creature.Player != null || creature.PetOwner != null && creature.HasPower<PhantomPower>())).Distinct().ToArray();
        // Morale levels are structural levels, not a card's dice-scaled amount.
        foreach (Creature ally in allies) await PowerCmd.Apply<HbrMoralePower>(context, ally, levels, source, null);
    }
    internal static decimal GetDamageMultiplier(Creature dealer, Creature target)
    {
        Player owner = dealer?.Player ?? dealer?.PetOwner;
        if (owner?.Creature == null || target == null || target.Side == owner.Creature.Side ||
            !HbrBattleStatsHelper.IsEnabled(owner) || owner.GetRelic<ThirtyOneDCollection>() is not { IsMelted: false }) return 1m;
        int kinds = ThirtyOneDKeywordRules.GetPermanentDistinctCount(owner);
        if (kinds < 2) return 1m;
        int amount = Math.Clamp(owner.Creature.GetPowerAmount<HbrIntelPower>(), 0, kinds);
        return 1m + amount * .05m;
    }
}
