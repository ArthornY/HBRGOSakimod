using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class KuraSatomiCombatHelper
{
    static KuraSatomiCombatHelper() => ThirtyGProposalState.Changed += card => Invalidate(card?.Owner);
    private sealed class Flags { internal bool Dirty = true, Parry; internal Player Owner; internal int Count = -1; }
    private static readonly ConditionalWeakTable<CardPile, Flags> Decks = new();
    private sealed class Battle
    {
        internal readonly HashSet<CardModel> Started = new();
        internal readonly Dictionary<CardModel, HashSet<(int, object)>> Returns = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    internal sealed class Receipt : IDisposable
    {
        internal HarvestBountifulGrainsHbrgoSaki Card;
        internal AttackCommand Command;
        internal bool Festival, Harvest, Active = true, Completed;
        internal readonly HashSet<Creature> Kills = new();
        internal Receipt Previous;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<Receipt> Current = new();
    internal static void Invalidate(Player player)
    { if (player?.Deck is { } deck && Decks.TryGetValue(deck, out var flags)) flags.Dirty = true; }
    internal static bool HasParry(Player player)
    {
        if (!HbrBattleStatsHelper.IsEnabled(player) || player.Deck is not { } deck) return false;
        Flags flags = Decks.GetValue(deck, pile => { var entry = new Flags(); pile.ContentsChanged += () => entry.Dirty = true; return entry; });
        if (flags.Dirty || flags.Count != deck.Cards.Count || !ReferenceEquals(flags.Owner, player))
        {
            flags.Parry = deck.Cards.OfType<HarvestBountifulGrainsHbrgoSaki>().Any(card => card.Owner == player &&
                !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(HarvestBountifulGrainsHbrgoSaki.Parry));
            flags.Owner = player; flags.Count = deck.Cards.Count; flags.Dirty = false;
        }
        return flags.Parry;
    }
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
    internal static Receipt Begin(HarvestBountifulGrainsHbrgoSaki card, Creature target)
    {
        Receipt scope = new() { Card = card, Festival = Festival(card, target), Previous = Current.Value };
        Current.Value = scope; return scope;
    }
    private static bool Festival(CardModel source, Creature target) => source is HarvestBountifulGrainsHbrgoSaki card &&
        card.HasUpgradeEffect(HarvestBountifulGrainsHbrgoSaki.HarvestFestival) && target?.HasPower<BreakPower>() == true;
    private static Receipt Find(CardModel card)
    {
        var scope = Current.Value;
        return scope?.Active == true && !scope.Completed && ReferenceEquals(scope.Card, card) ? scope : null;
    }
    internal static decimal GetAttackUnits(CardModel source, Creature target) =>
        Find(source) is { } scope ? scope.Festival ? HbrSkillPower.DamageUnits(133m) : source.DynamicVars.Damage.BaseValue
            : Festival(source, target) ? HbrSkillPower.DamageUnits(133m) : source?.DynamicVars.Damage.BaseValue ?? 0m;
    internal static int GetHitCount(CardModel source, Creature target) => (Find(source)?.Festival ?? Festival(source, target)) ? 6 : 4;
    private static bool OwnAttack(Receipt receipt) => receipt?.Active == true && !receipt.Completed && receipt.Command != null &&
        ReferenceEquals(receipt.Command, HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command) &&
        !HotaruCannonStatsHelper.IsCannonAttackSource(receipt.Card.Owner.Creature, receipt.Card);
    internal static Receipt CaptureDeath(Creature target) => target?.Side == CombatSide.Enemy && OwnAttack(Current.Value) ? Current.Value : null;
    internal static void OnFinalDeath(Receipt receipt, Creature target)
    { if (receipt?.Active == true && !receipt.Completed && target != null) { receipt.Kills.Add(target); receipt.Harvest = true; } }
    internal static void OnEnemyBroken(Creature dealer, Creature target, CardModel source, ValueProp props, bool alreadyBroken)
    {
        Receipt scope = Find(source);
        if (!alreadyBroken && target?.Side != dealer?.Side && props.IsPoweredAttack() && OwnAttack(scope) &&
            dealer == scope.Card.Owner.Creature) scope.Harvest = true;
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        source is HarvestBountifulGrainsHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        target != null && target.Side != dealer.Side && props.IsPoweredAttack() && !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)
            ? ((Find(source)?.Festival ?? Festival(source, target)) ? 21m : 5m) / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static async Task GenerateRiceBall(HarvestBountifulGrainsHbrgoSaki source)
    {
        var rice = source.Owner.Creature.CombatState.CreateCard<KuraRiceBallHbrgoSaki>(source.Owner);
        rice.ForWholeTeam = source.HasUpgradeEffect(HarvestBountifulGrainsHbrgoSaki.TropicalSushi);
        await CardPileCmd.AddGeneratedCardToCombat(rice, PileType.Hand, source.Owner);
    }
    internal static async Task OnCombatStart(ICombatState combat)
    {
        if (combat == null) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        foreach (Player player in combat.Players.Where(HbrBattleStatsHelper.IsEnabled))
            foreach (var card in player.Deck.Cards.OfType<HarvestBountifulGrainsHbrgoSaki>().Where(card => card.Owner == player &&
                !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(HarvestBountifulGrainsHbrgoSaki.LovingRiceBall)))
                if (battle.Started.Add(card)) await GenerateRiceBall(card);
    }
    internal static async Task CompleteAttack(PlayerChoiceContext context, Receipt receipt)
    {
        if (!receipt.Active || receipt.Completed) return;
        receipt.Completed = true;
        var card = receipt.Card;
        if (card.HasUpgradeEffect(HarvestBountifulGrainsHbrgoSaki.Vanguard))
            foreach (Creature _ in receipt.Kills) await ThirtyOneDBondHelper.GrantTeamMorale(context, card.Owner.Creature, 1m);
        if (receipt.Harvest) await GenerateRiceBall(card);
        if (receipt.Kills.Count == 0 || !card.HasUpgradeEffect(HarvestBountifulGrainsHbrgoSaki.DivineCatch)) return;
        Battle battle = Battles.GetOrCreateValue(card.Owner.Creature.CombatState);
        CardModel physical = Physical(card);
        if (!battle.Returns.TryGetValue(physical, out var turns)) battle.Returns.Add(physical, turns = new());
        if (turns.Add((card.Owner.PlayerCombatState?.TurnNumber ?? 0, OverclockImmediateTurnCoordinator.GetTurnToken(card.Owner))) &&
            card.Pile != card.Owner.PlayerCombatState.Hand) await CardPileCmd.Add(card, PileType.Hand);
    }
}
