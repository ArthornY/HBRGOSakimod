using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
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

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCommonCannonHelper
{
    private sealed class TurnState
    {
        internal bool HadManualCannonTarget;
    }

    private sealed class PlayerState
    {
        internal ICombatState Combat;
        internal int OrdinaryTurn = int.MinValue;
        internal TurnState Ordinary;
        internal readonly Dictionary<object, TurnState> Overclock = new(ReferenceEqualityComparer.Instance);
    }

    private static readonly ConditionalWeakTable<Player, PlayerState> Players = new();

    internal static bool CanAct(Player player) => player?.Creature?.IsAlive == true &&
        player.Creature.CombatState != null && !CombatManager.Instance.IsOverOrEnding;

    internal static void ResetTurn(Player player, object token) => GetTurn(player, token);

    internal static bool HasManualCannonTargetThisTurn(Player player) =>
        GetTurn(player)?.HadManualCannonTarget == true;

    internal static void RecordManualCannonTarget(CardPlay play)
    {
        if (play == null || play.IsAutoPlay || !play.IsFirstInSeries ||
            play.Card?.Owner is not { } player || !Millsage.IsOwnedCannon(play.Target, player)) return;
        if (GetTurn(player) is { } turn) turn.HadManualCannonTarget = true;
    }

    private static TurnState GetTurn(Player player, object explicitToken = null)
    {
        ICombatState combat = player?.Creature?.CombatState;
        if (combat == null || player.PlayerCombatState == null) return null;
        PlayerState state = Players.GetValue(player, static _ => new());
        if (!ReferenceEquals(state.Combat, combat))
        {
            state.Combat = combat;
            state.Ordinary = null;
            state.OrdinaryTurn = int.MinValue;
            state.Overclock.Clear();
        }
        int turn = player.PlayerCombatState.TurnNumber;
        if (state.Ordinary == null || state.OrdinaryTurn != turn)
        {
            state.OrdinaryTurn = turn;
            // A restored ordinary turn can have a start receipt already.
            // Historical ownership remains meaningful after a cannon retires.
            state.Ordinary = new TurnState
            {
                HadManualCannonTarget = CombatManager.Instance.History.CardPlaysStarted.Any(entry =>
                    entry.HappenedThisTurn(combat) && ReferenceEquals(entry.Actor, player.Creature) &&
                    entry.CardPlay.IsFirstInSeries && !entry.CardPlay.IsAutoPlay &&
                    entry.CardPlay.Target?.PetOwner == player && entry.CardPlay.Target.Monster is HotaruCannonModel)
            };
        }
        // Always create/snapshot ordinary history before the first OD play;
        // OD shares the native turn number but has its own targeting history.
        object token = explicitToken ?? OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token == null) return state.Ordinary;
        if (!state.Overclock.TryGetValue(token, out TurnState overclock))
            state.Overclock[token] = overclock = new();
        return overclock;
    }

    internal static Creature[] GetCannons(Player player) => player?.Creature?.CombatState?.Allies
        .Where(creature => Millsage.IsOwnedCannon(creature, player))
        .OrderBy(creature => (int)((HotaruCannonModel)creature.Monster).Kind).ToArray()
        ?? Array.Empty<Creature>();

    internal static Task SelectAndTrigger(PlayerChoiceContext context, CardModel source, CardPlay play) =>
        SelectAndTrigger(context, source.Owner, source, play);

    internal static async Task SelectAndTrigger(PlayerChoiceContext context, Player player, CardModel source = null, CardPlay play = null)
    {
        Creature cannon = await SelectCannon(context, player);
        if (cannon != null) await player.GetRelic<Millsage>().TriggerAllEffects(cannon, context, source, play);
    }

    internal static async Task<Creature> SelectCannon(PlayerChoiceContext context, Player player)
    {
        var combat = player?.Creature?.CombatState;
        if (!CanAct(player) || player.GetRelic<Millsage>() == null) return null;
        Creature[] cannons = GetCannons(player);
        if (cannons.Length == 0) return null;
        HotaruCannonChoiceToken[] choices = new HotaruCannonChoiceToken[cannons.Length];
        try
        {
            for (int i = 0; i < cannons.Length; i++)
            {
                var cannon = (HotaruCannonModel)cannons[i].Monster;
                var choice = (HotaruCannonChoiceToken)ModelDb.Card<HotaruCannonChoiceToken>().ToMutable();
                choices[i] = choice;
                choice.Owner = player;
                choice.Configure(cannon.Title.GetFormattedText(), cannon.IntentDescription());
            }
            CardModel selected = (await CardSelectCmd.FromSimpleGrid(context, choices, player,
                new CardSelectorPrefs(new LocString("cards", "HOTARU_RESONANT_FIRE.selectionScreenPrompt"), 1)))
                .FirstOrDefault();
            int index = Array.FindIndex(choices, choice => ReferenceEquals(choice, selected));
            if (index < 0 || !CanAct(player) || !ReferenceEquals(combat, player.Creature.CombatState) ||
                !Millsage.IsOwnedCannon(cannons[index], player)) return null;
            return cannons[index];
        }
        finally
        {
            foreach (HotaruCannonChoiceToken choice in choices)
                if (choice is { HasBeenRemovedFromState: false }) choice.RemoveFromState();
        }
    }

    internal static async Task OnTurnStart(PlayerChoiceContext context, Player player, object token)
    {
        ResetTurn(player, token);
        HotaruWaitOpportunityPower waiting = player?.Creature?.GetPower<HotaruWaitOpportunityPower>();
        int queued = waiting?.ClaimPending() ?? 0;
        if (player?.Creature?.GetPower<HotaruSideBySidePower>() is { } together)
            await together.OnTurnStart(context, token);
        if (queued > 0) await waiting.ResolveClaimed(context, queued);
    }

    internal static Task OnTurnEnd(PlayerChoiceContext context, Player player, object token) =>
        player?.Creature?.GetPower<HotaruSideBySidePower>()?.OnTurnEnd(context, token)
        ?? Task.CompletedTask;
}
