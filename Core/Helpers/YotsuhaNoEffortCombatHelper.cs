using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class YotsuhaNoEffortCombatHelper
{
    private sealed class State
    {
        internal decimal Growth;
        internal bool Skip;
        internal readonly HashSet<(int Turn, object Extra)> Ends = new(), Shots = new();
    }
    private sealed class Battle { internal readonly ConditionalWeakTable<CardModel, State> Cards = new(); }
    private sealed record Payment(bool WasX, int Paid);
    private static readonly ConditionalWeakTable<CardPlay, Payment> Payments = new();
    private sealed class ActionScope : IDisposable
    {
        internal ActionScope Previous;
        internal AbsolutelyNoEffortHbrgoSaki Card;
        internal AttackCommand Command;
        internal decimal Coefficient;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<ActionScope> Current = new();
    private static State Get(AbsolutelyNoEffortHbrgoSaki card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return null;
        CardModel physical = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        return Battles.GetOrCreateValue(combat).Cards.GetOrCreateValue(physical);
    }
    private static (int Turn, object Extra) Turn(AbsolutelyNoEffortHbrgoSaki card) =>
        (card.Owner.PlayerCombatState?.TurnNumber ?? card.CombatState.RoundNumber,
            ThirtyGBondHelper.GetEndRepeatToken(card) ?? OverclockImmediateTurnCoordinator.GetTurnToken(card.Owner));
    private static bool InHand(AbsolutelyNoEffortHbrgoSaki card) => card?.IsMutable == true &&
        card.Owner?.Creature is { IsAlive: true, CombatState: not null } && HbrBattleStatsHelper.IsEnabled(card.Owner) &&
        !card.HasBeenRemovedFromState && card.Owner.PlayerCombatState?.Hand.Cards.Contains(card) == true;
    internal static void CapturePayment(AbsolutelyNoEffortHbrgoSaki observer, CardPlay play)
    {
        if (observer?.IsMutable != true || !observer.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.OneMoreShot) ||
            play?.Card is not { Type: CardType.Attack } source || source.Owner != observer.Owner) return;
        Payments.GetValue(play, _ => new(source.EnergyCost.CostsX, play.Resources.EnergySpent));
    }
    internal static bool WillSkip(AbsolutelyNoEffortHbrgoSaki card) => Get(card)?.Skip == true;
    internal static decimal AttackUpBasis(AbsolutelyNoEffortHbrgoSaki card) =>
        (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.DecisiveEve) ? 75m : 50m) + (Get(card)?.Growth ?? 0m);
    internal static decimal PreviewAttackUpBasis(AbsolutelyNoEffortHbrgoSaki card) =>
        (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.DecisiveEve) ? 75m : 50m) +
        Math.Min(50m, (Get(card)?.Growth ?? 0m) + (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.SleepIn) ? 5m : 0m));
    internal static async Task TurnEnd(PlayerChoiceContext context, AbsolutelyNoEffortHbrgoSaki card,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (!InHand(card) || side != card.Owner.Creature.Side || !participants.Contains(card.Owner.Creature) ||
            CombatManager.Instance.IsOverOrEnding) return;
        State state = Get(card);
        if (!state.Ends.Add(Turn(card))) return;
        if (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.SleepIn)) state.Growth = Math.Min(50m, state.Growth + 5m);
        bool alternate = card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.DecisiveEve);
        bool giveAttack = !alternate || !state.Skip;
        state.Skip = alternate && giveAttack;
        Creature owner = card.Owner.Creature;
        if (giveAttack) await HbrEffectApplication.Apply<HbrAttackUpPower>(context, owner, AttackUpBasis(card), owner, card,
            remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        if (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.DreamAwakening))
        {
            var counted = new HbrEffectProfile(HbrEffectLimit.Only, HbrEffectExit.Count, 1);
            await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, owner, 85m, owner, card, remaining: 1, profile: counted);
            await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, owner, 45m, owner, card, remaining: 1, profile: counted);
        }
        if (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.BubbleBurst))
            await HbrComboPower.Grant<HbrComboSmallPower>(context, owner, owner, card);
        if (card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.CandyBurst) && !CombatManager.Instance.IsOverOrEnding)
            await Attack(context, card, null, card.DynamicVars.Damage.BaseValue, 2, 1.4m);
        ThirtyOneEBondHelper.RecordArrival(card.Owner, ThirtyOneEMember.OshimaYotsuha);
    }
    internal static async Task AfterCardPlayed(PlayerChoiceContext context, AbsolutelyNoEffortHbrgoSaki card, CardPlay play)
    {
        if (!InHand(card) || !card.HasUpgradeEffect(AbsolutelyNoEffortHbrgoSaki.OneMoreShot) || play?.Card is not { } source ||
            source.Owner != card.Owner || source.Type != CardType.Attack ||
            CombatManager.Instance.IsOverOrEnding ||
            card.CombatState.CurrentSide != CombatSide.Player && OverclockImmediateTurnCoordinator.GetTurnToken(card.Owner) == null) return;
        Payment payment = Payments.TryGetValue(play, out Payment captured) ? captured : new(source.EnergyCost.CostsX, play.Resources.EnergySpent);
        if (payment.WasX || payment.Paid > 1) return;
        Creature target = play.Target;
        if (target == null && source.TargetType == TargetType.AllEnemies)
            target = card.CombatState.GetOpponentsOf(card.Owner.Creature).FirstOrDefault(enemy => enemy.IsAlive && enemy.IsHittable);
        if (target?.IsAlive != true || !target.IsHittable || target.Side == card.Owner.Creature.Side ||
            !ReferenceEquals(target.CombatState, card.CombatState)) return;
        if (!Get(card).Shots.Add(Turn(card))) return;
        await Attack(context, card, target, card.DynamicVars["FollowupDamage"].BaseValue, 5, 1.625m);
    }
    private static async Task Attack(PlayerChoiceContext context, AbsolutelyNoEffortHbrgoSaki card, Creature target,
        decimal units, int hits, decimal coefficient)
    {
        AttackCommand command = DamageCmd.Attack(units).FromCardCompat(card).WithHitCount(hits).WithHitFx("vfx/vfx_attack_shot");
        if (target == null) command.TargetingAllOpponents(card.CombatState); else command.Targeting(target);
        using ActionScope scope = new() { Previous = Current.Value, Card = card, Command = command, Coefficient = coefficient };
        Current.Value = scope;
        // No AutoPlay or CardModel wrapper: this is an attack action of the
        // owner, never another attack-card play or another sister relay.
        await command.Execute(context);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (source is not AbsolutelyNoEffortHbrgoSaki card || source.Owner?.Creature != dealer ||
            target == null || target.Side == dealer.Side || !props.IsPoweredAttack() ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return 1m;
        for (ActionScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, card) && HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command))
                return scope.Coefficient / HbrDestructionGainHelper.DefaultSkillCoefficient;
        return 1m;
    }
}
