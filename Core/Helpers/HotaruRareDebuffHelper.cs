using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruRareDebuffHelper
{
    private sealed class State
    {
        internal ICombatState Combat;
        internal int Turn = -1;
        internal readonly HashSet<Type> Ordinary = new();
        internal readonly Dictionary<object, HashSet<Type>> Inserted = new(ReferenceEqualityComparer.Instance);
    }
    private static readonly ConditionalWeakTable<Player, State> States = new();

    private static HashSet<Type> AppliedThisTurn(Player player)
    {
        State state = States.GetValue(player, _ => new());
        if (!ReferenceEquals(state.Combat, player.Creature.CombatState) || state.Turn != player.PlayerCombatState.TurnNumber)
        {
            state.Combat = player.Creature.CombatState;
            state.Turn = player.PlayerCombatState.TurnNumber;
            state.Ordinary.Clear();
            state.Inserted.Clear();
        }
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token == null) return state.Ordinary;
        if (!state.Inserted.TryGetValue(token, out HashSet<Type> kinds)) state.Inserted[token] = kinds = new();
        return kinds;
    }

    internal static async Task OnDebuffApplied(PlayerChoiceContext context, Player player,
        PowerModel power, decimal amount, Creature applier, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(player) || power?.Owner == null || power.Amount <= 0 || amount <= 0m ||
            !Millsage.IsEnemy(power.Owner, player) ||
            !IsLinkedDebuff(power) ||
            (applier != player.Creature && applier?.PetOwner != player && source?.Owner != player)) return;
        if (!AppliedThisTurn(player).Add(power.GetType()) ||
            player.Creature.GetPower<HotaruIncompleteChordPower>() is not { Amount: > 0 } chord ||
            !chord.TryClaimTurnKey(power.GetType().FullName)) return;
        chord.ShowTriggered();
        HotaruRareCannonHelper.AddTurnAllActionsBoost(player, chord.Amount);
        foreach (Creature cannon in HotaruCommonCannonHelper.GetCannons(player))
            await CombatPreviewRefreshHelper.RefreshCreatureIntent(cannon);
    }

    internal static bool IsLinkedDebuff(PowerModel power) => power is FragilePower ||
        (HbrBattleStatsHelper.IsEnabled(power?.Owner)
            ? power is HbrDefenseDownPower or HbrAttackDownPower
            : power is VulnerablePower or WeakPower);

    internal static bool HasTripleDebuff(Creature target) => target != null &&
        HotaruContestVulnerabilityHelper.HasDefenseDown(target) &&
        HotaruContestVulnerabilityHelper.HasAttackDown(target) &&
        (target.GetPower<FragilePower>()?.Amount ?? 0) > 0;

    internal static decimal GetDamageBonus(Creature dealer, Creature target)
    {
        Player player = dealer?.Player ?? (dealer?.Monster is HotaruCannonModel ? dealer.PetOwner : null);
        if (player == null || !Millsage.IsEnemy(target, player) || !HasTripleDebuff(target)) return 0m;
        return Math.Max(0, player.Creature.GetPower<HotaruTripleSilencePower>()?.Amount ?? 0) / 100m;
    }

    internal static decimal GetCannonBlockBonus(Creature cannon)
    {
        Player player = cannon?.PetOwner;
        if (player == null || !Millsage.IsOwnedCannon(cannon, player) ||
            player.Creature.CombatState?.Enemies.Any(enemy => Millsage.IsEnemy(enemy, player) && HasTripleDebuff(enemy)) != true) return 0m;
        return Math.Max(0, player.Creature.GetPower<HotaruTripleSilencePower>()?.Amount ?? 0) / 100m;
    }
}
