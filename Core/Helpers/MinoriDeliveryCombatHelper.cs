using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MinoriDeliveryCombatHelper
{
    private sealed class Battle
    {
        internal readonly HashSet<CardModel> Started = new();
        internal readonly Dictionary<CardModel, HashSet<(int Number, object Token)>> Turns = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card)
        ? card.DeckVersion ?? card : card;
    internal static bool TryFirstInTurn(CardModel card)
    {
        if (card?.Owner?.Creature?.CombatState is not { } combat) return false;
        Battle battle = Battles.GetOrCreateValue(combat); CardModel key = Physical(card);
        if (!battle.Turns.TryGetValue(key, out var turns)) battle.Turns.Add(key, turns = new());
        return turns.Add((card.Owner.PlayerCombatState?.TurnNumber ?? 0, OverclockImmediateTurnCoordinator.GetTurnToken(card.Owner)));
    }
    internal static Task OnCombatStart(ICombatState combat)
    {
        if (combat == null) return Task.CompletedTask;
        Battle battle = Battles.GetOrCreateValue(combat);
        foreach (Player player in combat.Players.Where(HbrBattleStatsHelper.IsEnabled))
            foreach (var card in player.Deck.Cards.OfType<YourCombatDeliveryIsHereHbrgoSaki>().Where(card => card.Owner == player &&
                !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(YourCombatDeliveryIsHereHbrgoSaki.Ollie)))
                if (battle.Started.Add(card)) player.GetRelic<Overclock>()?.GainLevelProgress(10m);
        return Task.CompletedTask;
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is YourCombatDeliveryIsHereHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && target != null && target.Side != dealer.Side &&
        !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source);
    internal static decimal GetConditionalDamageMultiplier(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && !RanaByakkoCombatHelper.HasDp(target) ? 1.3m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? (((YourCombatDeliveryIsHereHbrgoSaki)source).HasUpgradeEffect(YourCombatDeliveryIsHereHbrgoSaki.Hurricane)
            ? 2.2m : 1m) / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
}
