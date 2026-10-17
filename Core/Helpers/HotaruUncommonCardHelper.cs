using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruUncommonCardHelper
{
    private sealed class TurnState
    {
        internal readonly HashSet<CardType> ManualTypes = new();
        internal readonly HashSet<Type> Debuffs = new();
        internal bool HighRollUsed;
        internal bool CannonChargeUsed;
    }

    private sealed class PlayerState
    {
        internal ICombatState Combat;
        internal int OrdinaryTurn = int.MinValue;
        internal TurnState Ordinary;
        internal readonly Dictionary<object, TurnState> Overclock = new(ReferenceEqualityComparer.Instance);
        internal readonly Dictionary<CardPlay, PlayReceipt> Pending = new(ReferenceEqualityComparer.Instance);
    }

    private sealed class RollReceipt { internal int FinalRoll; }

    private sealed class StartReceipt
    {
        internal HotaruThreeBreathsPower Breaths;
        internal int ThreeBreathsReward;
        internal int LuckyReward;
        internal HotaruBreathingRhythmPower Rhythm;
        internal List<HotaruTurnLedgerPower.GrantEntry> RhythmGrants;
        internal HotaruTrailingSilencePower Discount;
        internal List<HotaruTurnLedgerPower.GrantEntry> DiscountGrants;
    }

    private sealed class PlayReceipt
    {
        internal ICombatState Combat;
        internal TurnState Turn;
        internal object PlayToken;
        internal int ThreeBreathsReward;
        internal int LuckyReward;
        internal int Block;
        internal bool CompletesThreeTypes;
        internal HotaruBreathingRhythmPower Rhythm;
        internal HotaruTrailingSilencePower Discount;
    }

    private static readonly ConditionalWeakTable<Player, PlayerState> Players = new();
    private static readonly ConditionalWeakTable<object, RollReceipt> Rolls = new();
    private static readonly ConditionalWeakTable<object, StartReceipt> Starts = new();

    internal static void CapturePlayStart(CardModel card, bool isAutoPlay, object token)
    {
        if (isAutoPlay || token == null || card?.IsMutable != true || !HotaruCommonCannonHelper.CanAct(card.Owner)) return;
        Starts.GetValue(token, _ => Capture(card));
    }

    private static StartReceipt Capture(CardModel card)
    {
        Creature owner = card.Owner.Creature;
        HotaruThreeBreathsPower breaths = owner.GetPower<HotaruThreeBreathsPower>();
        HotaruBreathingRhythmPower rhythm = owner.GetPower<HotaruBreathingRhythmPower>();
        HotaruTrailingSilencePower discount = owner.GetPower<HotaruTrailingSilencePower>();
        return new StartReceipt
        {
            Breaths = breaths,
            ThreeBreathsReward = breaths?.Amount ?? 0,
            LuckyReward = owner.GetPower<HotaruLuckyFavorPower>()?.Amount ?? 0,
            Rhythm = rhythm,
            RhythmGrants = rhythm?.SnapshotGrants(),
            Discount = discount,
            DiscountGrants = discount?.SnapshotGrants()
        };
    }

    private static PlayerState GetState(Player player)
    {
        if (player?.Creature?.CombatState is not { } combat || player.PlayerCombatState == null) return null;
        PlayerState state = Players.GetValue(player, static _ => new());
        if (!ReferenceEquals(state.Combat, combat))
        {
            state.Combat = combat;
            state.OrdinaryTurn = int.MinValue;
            state.Ordinary = null;
            state.Overclock.Clear();
            state.Pending.Clear();
        }
        return state;
    }

    private static TurnState GetTurn(Player player, object explicitToken = null, CardPlay currentPlay = null)
    {
        PlayerState state = GetState(player);
        if (state == null) return null;
        int number = player.PlayerCombatState.TurnNumber;
        if (state.Ordinary == null || state.OrdinaryTurn != number)
        {
            state.OrdinaryTurn = number;
            state.Ordinary = new();
            // History includes cards played before the power was acquired and
            // survives a restored ordinary turn. Never include this callback's
            // current card when deciding whether its type is the first.
            foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
                if (entry.HappenedThisTurn(state.Combat) && ReferenceEquals(entry.Actor, player.Creature) &&
                    entry.CardPlay.IsFirstInSeries && !entry.CardPlay.IsAutoPlay &&
                    !ReferenceEquals(entry.CardPlay, currentPlay))
                    state.Ordinary.ManualTypes.Add(entry.CardPlay.Card.Type);
        }
        object token = explicitToken ?? OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token == null) return state.Ordinary;
        if (!state.Overclock.TryGetValue(token, out TurnState turn))
            state.Overclock[token] = turn = new();
        return turn;
    }

    // Called only when an actual card roll has been prepared. Keep the opening
    // roll: independent rolls of later damage segments do not make the same
    // manually played card another opportunity for Lucky Favor.
    internal static void RecordResolvedRoll(CardModel card, int finalRoll)
    {
        object token = CardPlayContextHelper.GetCurrentPlayToken(card);
        if (token != null && !Rolls.TryGetValue(token, out _))
            Rolls.Add(token, new RollReceipt { FinalRoll = finalRoll });
    }

    internal static bool TryGetOpeningRoll(CardModel card, out int finalRoll)
    {
        object token = CardPlayContextHelper.GetCurrentPlayToken(card);
        if (token != null && Rolls.TryGetValue(token, out RollReceipt receipt))
        {
            finalRoll = receipt.FinalRoll;
            return true;
        }
        finalRoll = 0;
        return false;
    }

    internal static void BeforeCardPlayed(CardPlay play)
    {
        if (play == null || play.IsAutoPlay || !play.IsFirstInSeries ||
            play.Card?.Owner is not { } player || !HotaruCommonCannonHelper.CanAct(player)) return;
        PlayerState state = GetState(player);
        if (state.Pending.ContainsKey(play)) return;
        TurnState turn = GetTurn(player, currentPlay: play);
        bool firstType = turn.ManualTypes.Add(play.Card.Type);
        object playToken = CardPlayContextHelper.GetCurrentPlayToken(play.Card);
        StartReceipt start = playToken != null && Starts.TryGetValue(playToken, out StartReceipt captured)
            ? captured : Capture(play.Card);
        PlayReceipt receipt = new()
        {
            Combat = state.Combat,
            Turn = turn,
            PlayToken = playToken,
            ThreeBreathsReward = firstType && (play.Card.Type is CardType.Attack or CardType.Skill or CardType.Power) &&
                start.Breaths?.TryClaimTurnKey(play.Card.Type.ToString()) == true ? start.ThreeBreathsReward : 0,
            LuckyReward = start.LuckyReward,
            CompletesThreeTypes = firstType &&
                (play.Card.Type is CardType.Attack or CardType.Skill or CardType.Power) &&
                turn.ManualTypes.Contains(CardType.Attack) && turn.ManualTypes.Contains(CardType.Skill) &&
                turn.ManualTypes.Contains(CardType.Power)
        };
        if (play.Card.Type == CardType.Attack)
        {
            receipt.Rhythm = start.Rhythm;
            receipt.Block = receipt.Rhythm?.Claim(start.RhythmGrants) ?? 0;
        }
        if (play.Card.Type == CardType.Skill && !play.Card.EnergyCost.CostsX)
        {
            receipt.Discount = start.Discount;
            receipt.Discount?.Claim(start.DiscountGrants);
        }
        state.Pending.Add(play, receipt);
    }

    internal static async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        await ResolveThreeTypes(context, play);
        await HotaruRareRollHelper.AfterCardPlayed(context, play);
        if (play?.Card?.Owner is not { } player || GetState(player) is not { } state ||
            !state.Pending.Remove(play, out PlayReceipt receipt)) return;
        bool lucky = receipt.PlayToken != null && Rolls.TryGetValue(receipt.PlayToken, out RollReceipt roll) &&
            roll.FinalRoll >= 91 && !receipt.Turn.HighRollUsed;
        // Reserve every once-per-turn result before any reward can trigger
        // nested automatic cards or an immediate OD turn.
        if (lucky) receipt.Turn.HighRollUsed = true;
        if (receipt.Discount != null) await receipt.Discount.FlushSpent(context);
        if (receipt.Rhythm != null) await receipt.Rhythm.FlushSpent(context);
        if (!CanResolve(player, receipt)) return;
        if (lucky && receipt.LuckyReward > 0 && player.Creature.GetPower<HotaruLuckyFavorPower>() is { Amount: > 0 } favor &&
            favor.TryClaimTurnKey("first"))
        {
            favor.ShowTriggered();
            await PlayerCmd.GainEnergy(receipt.LuckyReward, player);
        }
        if (!CanResolve(player, receipt)) return;
        if (receipt.Block > 0)
            await CreatureCmd.GainBlock(player.Creature, receipt.Block, ValueProp.Unpowered, null);
        if (!CanResolve(player, receipt) || receipt.ThreeBreathsReward <= 0 ||
            player.Creature.GetPower<HotaruThreeBreathsPower>() is not { Amount: > 0 } breaths) return;
        breaths.ShowTriggered();
        await CardPileCmd.Draw(context, receipt.ThreeBreathsReward, player);
        if (CanResolve(player, receipt))
            await PowerCmd.Apply<ChargePower>(context, player.Creature, receipt.ThreeBreathsReward, player.Creature, null);
    }

    private static async Task ResolveThreeTypes(PlayerChoiceContext context, CardPlay play)
    {
        if (play?.Card?.Owner is not { } player || GetState(player) is not { } state ||
            !state.Pending.TryGetValue(play, out PlayReceipt receipt) || !receipt.CompletesThreeTypes) return;
        // The completion event belongs to this play. Claim it before callbacks;
        // acquiring the power afterwards cannot replay an earlier completion.
        receipt.CompletesThreeTypes = false;
        if (!CanResolve(player, receipt) || player.Creature.GetPower<HotaruThirdKnockPower>() is not { Amount: > 0 } power)
            return;
        int activations = power.Amount;
        power.ShowTriggered();
        for (int i = 0; i < activations && CanResolve(player, receipt); i++)
        {
            Creature[] cannons = HotaruCommonCannonHelper.GetCannons(player);
            if (cannons.Length == 0 || player.GetRelic<Millsage>() is not { } relic) break;
            Creature cannon = cannons[player.RunState.Rng.Niche.NextInt(cannons.Length)];
            await relic.TriggerAllEffects(cannon, context);
        }
    }

    private static bool CanResolve(Player player, PlayReceipt receipt) =>
        HotaruCommonCannonHelper.CanAct(player) && ReferenceEquals(receipt.Combat, player.Creature.CombatState) &&
        ReferenceEquals(receipt.Turn, GetTurn(player));

    internal static Task OnDebuffApplied(PlayerChoiceContext context, HotaruConvergingFragmentsPower ownerPower,
        PowerModel power, decimal amount, Creature applier, CardModel source) =>
        OnDebuffApplied(context, ownerPower.Owner?.Player, power, amount, applier, source);

    internal static async Task OnDebuffApplied(PlayerChoiceContext context, Player player,
        PowerModel power, decimal amount, Creature applier, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(player) || power?.Owner == null ||
            power.Owner.Side == player.Creature.Side || power.Amount == 0 || amount == 0m ||
            Math.Sign(power.Amount) != Math.Sign(amount) || power.GetTypeForAmount(power.Amount) != PowerType.Debuff ||
            (applier != player.Creature && applier?.PetOwner != player && source?.Owner != player)) return;
        TurnState turn = GetTurn(player);
        if (!turn.Debuffs.Add(power.GetType()) ||
            player.Creature.GetPower<HotaruConvergingFragmentsPower>() is not { Amount: > 0 } ownerPower ||
            !ownerPower.TryClaimTurnKey(power.GetType().FullName)) return;
        ownerPower.ShowTriggered();
        await PlayerCmd.GainEnergy(ownerPower.Amount, player);
    }

    internal static async Task OnCannonExtraActionsGranted(PlayerChoiceContext context, Player player, CardModel source)
    {
        if (source?.Owner != player || !HotaruCommonCannonHelper.CanAct(player)) return;
        TurnState turn = GetTurn(player);
        if (turn.CannonChargeUsed) return;
        turn.CannonChargeUsed = true;
        if (player.Creature.GetPower<HotaruStillCoveredPower>() is not { Amount: > 0 } power ||
            !power.TryClaimTurnKey("first")) return;
        int block = power.Amount;
        power.ShowTriggered();
        await CreatureCmd.GainBlock(player.Creature, block, ValueProp.Unpowered, null);
    }

    internal static async Task OnTurnStart(PlayerChoiceContext context, Player player, object token)
    {
        GetTurn(player, token);
        if (player?.Creature?.GetPower<HotaruUntilDawnPower>() is { } dawn)
            await dawn.OnTurnStart(context, token);
        if (player?.Creature?.GetPower<HotaruTrailingSilencePower>() is { } discount)
            await discount.OnTurnStart(context, token);
        if (player?.Creature?.GetPower<HotaruBreathingRhythmPower>() is { } rhythm)
            await rhythm.OnTurnStart(context, token);
    }

    internal static async Task OnTurnEnd(PlayerChoiceContext context, Player player, object token)
    {
        if (player?.Creature?.GetPower<HotaruTrailingSilencePower>() is { } discount)
            await discount.OnTurnEnd(context, token);
        if (player?.Creature?.GetPower<HotaruBreathingRhythmPower>() is { } rhythm)
            await rhythm.OnTurnEnd(context, token);
    }
}
