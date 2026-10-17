using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruRareRollHelper
{
    private sealed class TurnProgress
    {
        internal int Critical;
        internal int Misses;
        internal readonly Queue<Reward> Pending = new();
        internal bool Resolving;
    }
    private sealed class PlayerProgress
    {
        internal ICombatState Combat;
        internal int Turn = int.MinValue;
        internal TurnProgress Ordinary;
        internal readonly Dictionary<object, TurnProgress> Overclock = new(ReferenceEqualityComparer.Instance);
    }
    private sealed class PlaySnapshot
    {
        internal Player Player;
        internal ICombatState Combat;
        internal TurnProgress Turn;
        internal bool Rolled;
        internal bool Ended;
        internal HotaruCertainVoicePower CertainVoice;
        internal HotaruBreathAfterMissPower Breath;
        internal HotaruConfiguredRollPower.RewardEntry[] BreathEntries;
        internal HotaruInheritAnswerPower Inherit;
        internal int InheritCopies;
        internal HotaruFarewellInertiaPower Inertia;
        internal HotaruConfiguredRollPower.RewardEntry[] InertiaEntries;
    }
    private sealed class Reward
    {
        internal PlaySnapshot Play;
        internal SoulRollBreakdown Roll;
        internal int CriticalOrdinal;
        internal int MissOrdinal;
    }
    private static readonly ConditionalWeakTable<Player, PlayerProgress> Players = new();
    private static readonly ConditionalWeakTable<object, PlaySnapshot> Plays = new();

    private static PlayerProgress GetPlayer(Player player)
    {
        if (player?.Creature?.CombatState is not { } combat || player.PlayerCombatState == null) return null;
        PlayerProgress state = Players.GetValue(player, static _ => new());
        if (!ReferenceEquals(state.Combat, combat))
        {
            state.Combat = combat;
            state.Turn = int.MinValue;
            state.Ordinary = null;
            state.Overclock.Clear();
        }
        return state;
    }

    private static IEnumerable<HotaruRollProgressPower> ProgressPowers(Player player)
    {
        if (player?.Creature?.GetPower<HotaruBreathAfterMissPower>() is { } breath) yield return breath;
        if (player?.Creature?.GetPower<HotaruInheritAnswerPower>() is { } inherit) yield return inherit;
        if (player?.Creature?.GetPower<HotaruFarewellInertiaPower>() is { } inertia) yield return inertia;
    }

    private static TurnProgress GetTurn(Player player, object explicitToken = null)
    {
        PlayerProgress state = GetPlayer(player);
        if (state == null) return null;
        int turn = player.PlayerCombatState.TurnNumber;
        if (state.Ordinary == null || state.Turn != turn)
        {
            state.Turn = turn;
            HotaruRollProgressPower[] saved = ProgressPowers(player).Where(p => p.ObservedTurn == turn).ToArray();
            state.Ordinary = new TurnProgress
            {
                Critical = saved.Select(p => p.ObservedCritical).DefaultIfEmpty().Max(),
                Misses = saved.Select(p => p.ObservedMisses).DefaultIfEmpty().Max()
            };
        }
        object token = explicitToken ?? OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token == null) return state.Ordinary;
        if (!state.Overclock.TryGetValue(token, out TurnProgress progress))
            state.Overclock[token] = progress = new();
        return progress;
    }

    internal static void SyncProgress(HotaruRollProgressPower power)
    {
        Player player = power?.Owner?.Player;
        if (player?.PlayerCombatState == null || OverclockImmediateTurnCoordinator.GetTurnToken(player) != null) return;
        TurnProgress turn = GetTurn(player);
        if (turn == null) return;
        power.ObservedTurn = player.PlayerCombatState.TurnNumber;
        power.ObservedCritical = turn.Critical;
        power.ObservedMisses = turn.Misses;
    }

    internal static void CapturePlayStart(CardModel card, bool isAutoPlay, object token)
    {
        if (isAutoPlay || token == null || card?.IsMutable != true || !HotaruCommonCannonHelper.CanAct(card.Owner)) return;
        if (Plays.TryGetValue(token, out _)) return;
        Creature owner = card.Owner.Creature;
        HotaruCertainVoicePower certain = owner.GetPower<HotaruCertainVoicePower>();
        var breath = owner.GetPower<HotaruBreathAfterMissPower>();
        var inherit = owner.GetPower<HotaruInheritAnswerPower>();
        var inertia = owner.GetPower<HotaruFarewellInertiaPower>();
        Plays.Add(token, new PlaySnapshot
        {
            Player = card.Owner, Combat = owner.CombatState, Turn = GetTurn(card.Owner),
            CertainVoice = certain?.Reserve(token) == true ? certain : null,
            Breath = breath, BreathEntries = breath?.Snapshot() ?? Array.Empty<HotaruConfiguredRollPower.RewardEntry>(),
            Inherit = inherit, InheritCopies = inherit?.Amount ?? 0,
            Inertia = inertia, InertiaEntries = inertia?.Snapshot() ?? Array.Empty<HotaruConfiguredRollPower.RewardEntry>()
        });
    }

    internal static SoulRollBreakdown ApplyCertainVoice(CardModel card, SoulRollBreakdown roll)
    {
        if (roll == null || card == null || CardPlayContextHelper.IsAutoPlay(card)) return roll;
        object token = CardPlayContextHelper.GetCurrentPlayToken(card);
        if (token == null || !Plays.TryGetValue(token, out PlaySnapshot snapshot) || snapshot.CertainVoice == null) return roll;
        return new SoulRollBreakdown(roll.BaseRoll, roll.FinalRoll, roll.DiceSource,
            roll.Bonuses.Concat(new[] { new SoulRollBonusDetail(0, "这一声，必定抵达：视为大成功") }))
        {
            IsForcedCritical = true,
            ResolvedTier = SoulRollTier.Critical
        };
    }

    internal static void RecordResolvedRoll(CardModel card, SoulRollBreakdown roll)
    {
        object token = CardPlayContextHelper.GetCurrentPlayToken(card);
        if (roll == null || token == null || !Plays.TryGetValue(token, out PlaySnapshot snapshot) || snapshot.Rolled) return;
        snapshot.Rolled = true;
        bool critical = RollKeywordRules.IsCriticalRoll(snapshot.Player, roll);
        int ordinal = critical
            ? snapshot.Turn.Critical = (int)Math.Min(int.MaxValue, (long)snapshot.Turn.Critical + 1)
            : snapshot.Turn.Misses = (int)Math.Min(int.MaxValue, (long)snapshot.Turn.Misses + 1);
        foreach (HotaruRollProgressPower power in ProgressPowers(snapshot.Player)) SyncProgress(power);
        snapshot.Turn.Pending.Enqueue(new Reward
        {
            Play = snapshot, Roll = roll, CriticalOrdinal = critical ? ordinal : 0, MissOrdinal = critical ? 0 : ordinal
        });
    }

    internal static int GetRollPercent(Player player) =>
        player?.Creature?.GetPower<HotaruOnlyThisBeatPower>()?.ActiveAmount ?? 0;

    private static bool Valid(PlaySnapshot play) => HotaruCommonCannonHelper.CanAct(play.Player) &&
        ReferenceEquals(play.Combat, play.Player.Creature.CombatState) && ReferenceEquals(play.Turn, GetTurn(play.Player));

    internal static async Task ResolvePending(PlayerChoiceContext context, Player player)
    {
        TurnProgress state = GetTurn(player);
        if (state == null || state.Resolving) return;
        state.Resolving = true;
        try
        {
            while (state.Pending.TryDequeue(out Reward reward))
            {
                PlaySnapshot play = reward.Play;
                if (!Valid(play)) continue;
                // Establish next-activation dice before any draws or block
                // callbacks can activate a cannon.
                if (reward.CriticalOrdinal == 1 && play.Inherit?.Amount > 0 &&
                    ReferenceEquals(player.Creature.GetPower<HotaruInheritAnswerPower>(), play.Inherit))
                {
                    for (int i = 0; i < play.InheritCopies; i++)
                        HotaruRareCannonHelper.QueueNextInheritedRoll(player, reward.Roll);
                    play.Inherit.ShowTriggered();
                }
                if (play.Breath?.Amount > 0 && ReferenceEquals(player.Creature.GetPower<HotaruBreathAfterMissPower>(), play.Breath))
                {
                    if (reward.MissOrdinal > 0)
                    {
                        var entries = play.BreathEntries.Where(e => reward.MissOrdinal <= e.Limit).ToArray();
                        int charge = Total(entries, e => e.SecondaryValue);
                        int block = Total(entries, e => e.Value);
                        if (charge > 0) await PowerCmd.Apply<ChargePower>(context, player.Creature, charge, player.Creature, null);
                        if (block > 0 && Valid(play))
                            await CreatureCmd.GainBlock(player.Creature, block, ValueProp.Unpowered, null);
                    }
                    else if (reward.CriticalOrdinal == 1)
                    {
                        int draw = Total(play.BreathEntries, e => e.Draw);
                        if (draw > 0) await CardPileCmd.Draw(context, draw, player);
                    }
                }
                if (reward.CriticalOrdinal <= 0 || !Valid(play) || play.Inertia?.Amount <= 0 ||
                    !ReferenceEquals(player.Creature.GetPower<HotaruFarewellInertiaPower>(), play.Inertia)) continue;
                int extra = Total(play.InertiaEntries.Where(e => reward.CriticalOrdinal <= e.Limit), e => e.SecondaryValue);
                if (extra <= 0) continue;
                Creature cannon = await HotaruCommonCannonHelper.SelectCannon(context, player);
                if (Valid(play) && cannon != null) player.GetRelic<Millsage>()?.Charge(cannon, extra);
            }
        }
        finally { state.Resolving = false; }
    }

    private static int Total(IEnumerable<HotaruConfiguredRollPower.RewardEntry> entries,
        Func<HotaruConfiguredRollPower.RewardEntry, int> value) =>
        (int)Math.Min(int.MaxValue, entries.Sum(entry => (long)Math.Max(0, value(entry)) * entry.Copies));

    internal static async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play?.Card == null || play.IsAutoPlay || !play.IsFirstInSeries) return;
        object token = CardPlayContextHelper.GetCurrentPlayToken(play.Card);
        if (token == null || !Plays.TryGetValue(token, out PlaySnapshot snapshot) || snapshot.Ended) return;
        snapshot.Ended = true;
        if (snapshot.CertainVoice != null) await snapshot.CertainVoice.Consume(context, token);
        await ResolvePending(context, snapshot.Player);
    }

    internal static async Task ChooseResistanceAndDiscount(PlayerChoiceContext context, CardModel source,
        Creature target, int resistanceLoss, int discount)
    {
        Player player = source.Owner;
        if (!HotaruCommonCannonHelper.CanAct(player) || !SixSenseCombatHelper.IsEligibleEnemy(target) ||
            target.GetPower<SixSenseResistancePower>() is not { } resistance) return;
        var combat = player.Creature.CombatState;
        var token = OverclockImmediateTurnCoordinator.GetTurnToken(player);
        int turn = player.PlayerCombatState.TurnNumber;
        SixSenseAttribute[] attributes = ElectronicSoldierManual.AllSixSenseAttributes.ToArray();
        HotaruCannonChoiceToken[] options = new HotaruCannonChoiceToken[attributes.Length];
        try
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                var option = (HotaruCannonChoiceToken)ModelDb.Card<HotaruCannonChoiceToken>().ToMutable();
                options[i] = option;
                option.Owner = player;
                LocString description = new("cards", "HOTARU_TRACE_OLD_SCAR.choiceDescription");
                description.Add("Resistance", resistance.GetResistance(attributes[i]));
                description.Add("ResistanceLoss", resistanceLoss);
                option.Configure(ElectronicSoldierManual.GetSenseTitle(attributes[i]), description);
            }
            CardModel selected = (await CardSelectCmd.FromSimpleGrid(context, options, player,
                new CardSelectorPrefs(new LocString("cards", "HOTARU_TRACE_OLD_SCAR.selectionScreenPrompt"), 1))).FirstOrDefault();
            int index = Array.FindIndex(options, option => ReferenceEquals(option, selected));
            if (index < 0 || !HotaruCommonCannonHelper.CanAct(player) || !ReferenceEquals(combat, player.Creature.CombatState) ||
                turn != player.PlayerCombatState.TurnNumber || !ReferenceEquals(token, OverclockImmediateTurnCoordinator.GetTurnToken(player))) return;
            await SixSenseCombatHelper.ReduceResistance(context, player, target, source, attributes[index], resistanceLoss);
            if (HotaruCommonCannonHelper.CanAct(player) && ReferenceEquals(combat, player.Creature.CombatState) &&
                turn == player.PlayerCombatState.TurnNumber && ReferenceEquals(token, OverclockImmediateTurnCoordinator.GetTurnToken(player)))
                await HotaruTurnLedgerPower.Grant<HotaruTraceOldScarPower>(context, player.Creature,
                    discount, int.MaxValue, source, (int)attributes[index]);
        }
        finally
        {
            foreach (var option in options)
                if (option is { HasBeenRemovedFromState: false }) option.RemoveFromState();
        }
    }

    internal static async Task OnTurnStart(PlayerChoiceContext context, Player player, object token)
    {
        GetTurn(player, token);
        if (player?.Creature?.GetPower<HotaruTraceOldScarPower>() is { } discount) await discount.OnTurnStart(context, token);
        if (player?.Creature?.GetPower<HotaruOnlyThisBeatPower>() is { } beat) await beat.OnTurnStart(context, token);
        if (player?.Creature?.GetPower<HotaruFarewellInertiaPower>() is not { Amount: > 0 } inertia ||
            !inertia.TryClaimTurnKey("start")) return;
        int charge = inertia.StartingCharge(player.GetRelic<Millsage>()?.RetiredCannonCount ?? 0);
        if (charge > 0) await PowerCmd.Apply<ChargePower>(context, player.Creature, charge, player.Creature, null);
    }

    internal static async Task OnTurnEnd(PlayerChoiceContext context, Player player, object token)
    {
        if (player?.Creature?.GetPower<HotaruTraceOldScarPower>() is { } discount) await discount.OnTurnEnd(context, token);
        if (player?.Creature?.GetPower<HotaruOnlyThisBeatPower>() is { } beat) await beat.OnTurnEnd(context, token);
    }
}
