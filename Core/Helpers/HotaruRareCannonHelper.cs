using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

public sealed class HotaruCannonActionModifier
{
    public decimal BonusPercent { get; set; }
    public decimal FinalMultiplier { get; set; } = 1m;
    public bool AdvantageRoll { get; set; }
    public decimal RollAmplificationMultiplier { get; set; } = 1m;
    public SoulRollBreakdown InheritedRoll { get; set; }
}

public sealed class HotaruRareTurnState
{
    public int ActionMask { get; set; }
    public decimal ExecutedExtraActions { get; set; }
    public decimal AllActionsBonusPercent { get; set; }
    public decimal ActivationBonusPercent { get; set; }
}

public sealed class HotaruInheritedCannonRoll
{
    public int Base { get; set; }
    public int Final { get; set; }
    public bool Forced { get; set; }
    public SoulRollTier? Tier { get; set; }
    public string Source { get; set; }
    public SoulRollBreakdown ToRoll() => new(Base, Final, Source) { IsForcedCritical = Forced, ResolvedTier = Tier };
}

public sealed class HotaruRareCannonState
{
    public int TurnNumber { get; set; } = -1;
    public HotaruRareTurnState Ordinary { get; set; } = new();
    // Debuff kinds each enemy (by combat id) has gained this combat, for Six Sense
    // Remnants. They replaced the per-enemy broken resistance masks (2026-11-09).
    public Dictionary<uint, List<string>> FirstDebuffs { get; set; } = new();
    public List<HotaruInheritedCannonRoll> InheritedRolls { get; set; } = new();
    [JsonIgnore] public Dictionary<object, HotaruRareTurnState> Inserted { get; } = new(ReferenceEqualityComparer.Instance);
}

internal static class HotaruRareCannonHelper
{
    private static HotaruRareCannonState State(Player player) => player?.GetRelic<Millsage>()?.RareCannonState;
    private static HotaruRareTurnState Turn(Player player, object explicitToken = null)
    {
        var state = State(player);
        if (state == null || player.PlayerCombatState == null) return null;
        int number = player.PlayerCombatState.TurnNumber;
        if (state.TurnNumber != number)
        {
            state.TurnNumber = number;
            state.Ordinary = new();
            state.Inserted.Clear();
        }
        object token = explicitToken ?? OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token == null) return state.Ordinary ??= new();
        if (!state.Inserted.TryGetValue(token, out var turn)) state.Inserted[token] = turn = new();
        return turn;
    }

    internal static int PermanentRetiredCount(Player player) => player?.GetRelic<Millsage>()?.RetiredCannonCount ?? 0;
    internal static int ActionCount(Player player) => System.Numerics.BitOperations.PopCount((uint)(Turn(player)?.ActionMask ?? 0));
    internal static decimal ExecutedExtraActions(Player player) => Turn(player)?.ExecutedExtraActions ?? 0m;
    internal static void RecordExtraAction(Player player)
    {
        if (Turn(player) is not { } turn) return;
        turn.ExecutedExtraActions = DamageAmplificationLimits.AddSaturated(turn.ExecutedExtraActions, 1m);
        player.PlayerCombatState?.RecalculateCardValues();
    }
    internal static void RecordActions(Player player, IReadOnlyList<Creature> cannons)
    {
        if (Turn(player) is not { } turn) return;
        foreach (var cannon in cannons)
            if (cannon?.Monster is HotaruCannonModel model) turn.ActionMask |= 1 << (int)model.Kind;
    }
    internal static void AddTurnAllActionsBoost(Player player, decimal percent)
    {
        if (Turn(player) is { } turn) turn.AllActionsBonusPercent = DamageAmplificationLimits.AddSaturated(turn.AllActionsBonusPercent, Math.Max(0m, percent));
    }
    internal static void AddTurnActivationBoost(Player player, decimal percent)
    {
        if (Turn(player) is { } turn) turn.ActivationBonusPercent = DamageAmplificationLimits.AddSaturated(turn.ActivationBonusPercent, Math.Max(0m, percent));
    }
    internal static void QueueNextInheritedRoll(Player player, SoulRollBreakdown roll)
    {
        if (roll == null || State(player) is not { } state) return;
        state.InheritedRolls.Add(new HotaruInheritedCannonRoll { Base = roll.BaseRoll, Final = roll.FinalRoll,
            Forced = roll.IsForcedCritical, Tier = roll.ResolvedTier, Source = roll.DiceSource });
    }
    internal static SoulRollBreakdown ClaimInheritedRoll(Player player)
    {
        if (State(player) is not { } state || state.InheritedRolls.Count == 0) return null;
        var roll = state.InheritedRolls[0];
        state.InheritedRolls.RemoveAt(0);
        return roll.ToRoll();
    }
    internal static decimal BonusPercent(Creature cannon)
    {
        var turn = Turn(cannon?.PetOwner);
        if (turn == null) return 0m;
        var action = HotaruUncommonCannonHelper.CurrentAction(cannon);
        bool preview = HotaruCannonStatsHelper.IsDamagePreview || HotaruCannonBlockHelper.IsBlockPreview;
        return DamageAmplificationLimits.AddSaturated(turn.AllActionsBonusPercent,
            !preview && action?.IsActivation == true ? DamageAmplificationLimits.AddSaturated(turn.ActivationBonusPercent,
                action.Modifier?.BonusPercent ?? 0m) : 0m);
    }
    internal static decimal FinalMultiplier(Creature cannon) =>
        !HotaruCannonStatsHelper.IsDamagePreview && !HotaruCannonBlockHelper.IsBlockPreview
            ? HotaruUncommonCannonHelper.CurrentAction(cannon)?.Modifier?.FinalMultiplier ?? 1m : 1m;

    internal static async Task OnTurnStart(PlayerChoiceContext context, Player player, object token)
    {
        Turn(player, token);
        if (player?.Creature?.GetPower<HotaruBreathingShelterPower>() is { } shelter)
            await shelter.OnTurnStart(context);
    }

    internal static Task OnTurnEnd(PlayerChoiceContext context, Player player, object token)
    {
        var state = State(player);
        if (state == null) return Task.CompletedTask;
        var turn = token == null ? state.Ordinary : state.Inserted.GetValueOrDefault(token);
        if (turn != null)
        {
            turn.AllActionsBonusPercent = 0m;
            turn.ActivationBonusPercent = 0m;
            turn.ExecutedExtraActions = 0m;
        }
        return Task.CompletedTask;
    }

    // Six Sense Remnants rewards an enemy's first gain of each debuff kind this
    // combat, from any source (it rewarded each broken six-sense resistance before
    // 2026-11-09). Gains are recorded even before the power is held, so a kind an
    // enemy already had never counts later.
    internal static async Task OnEnemyDebuffGained(PlayerChoiceContext context, Player player,
        PowerModel power, decimal amount, CardModel source)
    {
        if (State(player) is not { } state || power?.Owner is not { } enemy || enemy.CombatId is not { } id ||
            power.Amount == 0 || amount == 0m || Math.Sign(power.Amount) != Math.Sign(amount) ||
            power.GetTypeForAmount(power.Amount) != PowerType.Debuff || !Millsage.IsEnemy(enemy, player)) return;
        state.FirstDebuffs ??= new();
        if (!state.FirstDebuffs.TryGetValue(id, out List<string> kinds) || kinds == null)
            state.FirstDebuffs[id] = kinds = new();
        string kind = power.GetType().FullName;
        if (kinds.Contains(kind)) return;
        kinds.Add(kind);
        if (player.Creature.GetPower<HotaruSixSenseRemnantsPower>() is { } remnants)
            await remnants.OnFirstEnemyDebuff(context, source);
    }
}
