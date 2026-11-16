using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HanamuraOrchestraCombatHelper
{
    private sealed class DeckCharacters
    {
        internal bool Dirty = true;
        internal int Count = -1;
        internal Player Owner;
        internal readonly HashSet<string> Characters = new(StringComparer.Ordinal);
    }
    private sealed class AriaScope : IDisposable
    {
        internal ThisIsMyStageHbrgoSaki Card;
        internal AttackCommand Command;
        internal AriaScope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(CurrentAria.Value, this)) CurrentAria.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<CardPile, DeckCharacters> Decks = new();
    private static readonly AsyncLocal<AriaScope> CurrentAria = new();

    internal static int GetOtherCharacterCount(Player player, string excludedCharacterId)
    {
        if (player?.Deck is not { } deck) return 0;
        DeckCharacters cache = Decks.GetValue(deck, pile =>
        { DeckCharacters entry = new(); pile.ContentsChanged += () => entry.Dirty = true; return entry; });
        if (cache.Dirty || cache.Count != deck.Cards.Count || !ReferenceEquals(cache.Owner, player))
        {
            cache.Characters.Clear();
            foreach (CardModel card in deck.Cards)
                if (card.Owner == player && !card.HasBeenRemovedFromState && HbrMemoryGardenCardCatalog.IsGardenCard(card) &&
                    HbrMemoryGarden.CharacterIdOf(card) is { Length: > 0 } id) cache.Characters.Add(id);
            cache.Count = deck.Cards.Count; cache.Owner = player; cache.Dirty = false;
        }
        return cache.Characters.Count - (excludedCharacterId != null && cache.Characters.Contains(excludedCharacterId) ? 1 : 0);
    }
    internal static decimal GetBonusPercent(CardModel card) => card is IOrchestraCard orchestra
        ? Math.Min(Math.Max(0m, orchestra.OrchestraMaximumBonusPercent),
            GetOtherCharacterCount(card.IsMutable ? card.Owner : null, orchestra.OrchestraExcludedCharacterId) * Math.Max(0m, orchestra.OrchestraStepPercent)) : 0m;
    internal static decimal GetAttackUpBasis(ThisIsMyStageHbrgoSaki card)
    {
        decimal value = (card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.GuidingBaton) ? 75m : 50m) + GetBonusPercent(card);
        return card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.SymphonyForLittleBirds) ? Math.Max(65m, value) : value;
    }
    internal static void AddDescriptionParameters(CardModel source, LocString text)
    {
        if (source is not ThisIsMyStageHbrgoSaki card || text == null) return;
        text.Add("HanamuraAttackUp", HbrCardFaceDescription.Percent(card, "AttackUp", GetAttackUpBasis(card)));
        text.Add("HanamuraRecipients", new LocString("cards", card.Id.Entry + (card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.SymphonyForLittleBirds)
            ? ".recipients.all" : ".recipients.single")).GetFormattedText());
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        for (AriaScope scope = CurrentAria.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, source) && dealer != null && source.Owner?.Creature == dealer && target != null && target.Side != dealer.Side && props.IsPoweredAttack() && !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source) &&
                HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command))
                return 1.6m / HbrDestructionGainHelper.DefaultSkillCoefficient;
        return 1m;
    }
    private static async Task Aria(PlayerChoiceContext context, ThisIsMyStageHbrgoSaki card, CardPlay play)
    {
        if (!card.CombatState.GetOpponentsOf(card.Owner.Creature).Any(enemy => enemy.IsHittable)) return;
        AttackCommand command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
            .TargetingAllOpponents(card.CombatState).WithHitCount(3).WithHitFx("vfx/vfx_attack_slash");
        using AriaScope scope = new() { Card = card, Command = command, Previous = CurrentAria.Value };
        CurrentAria.Value = scope;
        await command.Execute(context);
    }
    private static async Task Invite(PlayerChoiceContext context, ThisIsMyStageHbrgoSaki source)
    {
        var player = source.Owner; var combat = player.Creature.CombatState;
        int turn = player.PlayerCombatState.TurnNumber;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(player);
        bool Eligible(CardModel card) => card != null && !ReferenceEquals(card, source) && card.Owner == player &&
            card.Pile == player.PlayerCombatState.Hand && !card.HasBeenRemovedFromState && HbrMemoryGardenCardCatalog.IsGardenCard(card);
        CardModel[] choices = player.PlayerCombatState.Hand.Cards.Where(Eligible).ToArray();
        if (choices.Length == 0) return;
        CardModel selected = (await CardSelectCmd.FromHand(context, player,
            new CardSelectorPrefs(new LocString("cards", source.Id.Entry + ".selectionPrompt"), 1), Eligible, source)).FirstOrDefault(Eligible);
        if (selected == null || !ReferenceEquals(combat, player.Creature.CombatState) || !player.Creature.IsAlive ||
            turn != player.PlayerCombatState.TurnNumber || !ReferenceEquals(token, OverclockImmediateTurnCoordinator.GetTurnToken(player))) return;
        // Use native relative end-of-turn modification, preserving its normal
        // ordering with other local/global cost effects and zero floor.
        selected.EnergyCost.AddThisTurn(-1);
        selected.InvokeEnergyCostChanged();
    }
    internal static async Task Play(PlayerChoiceContext context, ThisIsMyStageHbrgoSaki card, CardPlay play)
    {
        Creature owner = card.Owner?.Creature;
        if (owner?.IsAlive != true || owner.CombatState == null || !HbrBattleStatsHelper.IsEnabled(owner)) return;
        decimal basis = GetAttackUpBasis(card);
        Creature[] choices = RisaRecoveryCombatHelper.Targets(card.Owner);
        Creature selected = play.Target is { } target && choices.Contains(target) ? target : owner;
        if (card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.EvilAria)) await Aria(context, card, play);
        if (!owner.IsAlive) return;
        Creature[] recipients = card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.SymphonyForLittleBirds)
            ? RisaRecoveryCombatHelper.Targets(card.Owner) : selected.IsAlive ? new[] { selected } : Array.Empty<Creature>();
        foreach (Creature recipient in recipients)
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, recipient, basis, owner, card,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        if (card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.StellarSymphony) && RisaRecoveryCombatHelper.Targets(card.Owner).Contains(selected))
            await HbrComboPower.Grant<HbrComboMediumPower>(context, selected, owner, card);
        if (card.HasUpgradeEffect(ThisIsMyStageHbrgoSaki.ComeToMyRoom)) await Invite(context, card);
    }
}
