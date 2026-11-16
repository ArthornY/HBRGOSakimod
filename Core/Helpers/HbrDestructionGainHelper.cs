using System;
using System.Collections.Generic;
using System.Threading;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Full skill damage is divided between its original hits. A damage-dependent
// coefficient would make HBR-scale HP damage fill destruction in a single hit.
internal static class HbrDestructionGainHelper
{
    internal const decimal DefaultSkillCoefficient = 3m;

    private static readonly AsyncLocal<SkillScope> Current = new();
    private static readonly AsyncLocal<HitScope> CurrentHit = new();

    internal sealed class HitScope : IDisposable
    {
        private sealed class Share
        {
            internal decimal Coefficient;
            internal bool Claimed;
        }

        internal readonly Creature Dealer;
        internal readonly CardModel Card;
        internal readonly ValueProp Props;
        internal readonly HitScope Previous;
        internal readonly bool Enabled;
        internal bool Active = true;
        private readonly SkillScope _skill;

        internal SkillScope Skill => _skill;
        private readonly Dictionary<Creature, Share> _shares = new();

        internal HitScope(Creature dealer, CardModel card, ValueProp props)
        {
            Dealer = dealer;
            Card = card;
            Props = props;
            Previous = CurrentHit.Value;
            Enabled = HbrBattleStatsHelper.IsEnabled(dealer) && props.IsPoweredAttack() &&
                !HotaruCannonStatsHelper.IsReactionDamage;
            SkillScope skill = Current.Value;
            _skill = skill?.Active == true && ReferenceEquals(skill.Dealer, dealer) &&
                ReferenceEquals(skill.Card, card) ? skill : null;
        }

        private Share GetShare(Creature target)
        {
            if (!_shares.TryGetValue(target, out Share share))
            {
                share = new Share { Coefficient = _skill?.NextShare(target) ?? DefaultSkillCoefficient };
                _shares.Add(target, share);
            }
            return share;
        }

        internal void Observe(Creature target)
        {
            if (Active && Enabled && target != null && !HotaruCannonStatsHelper.IsReactionDamage)
                GetShare(target);
        }

        internal decimal Claim(Creature target, Creature dealer, CardModel card, ValueProp props)
        {
            if (!Active || !Enabled || target == null || !ReferenceEquals(Dealer, dealer) ||
                !ReferenceEquals(Card, card) || Props != props) return 0m;
            Share share = GetShare(target);
            if (share.Claimed) return 0m;
            share.Claimed = true;
            return share.Coefficient;
        }

        internal void RestoreCaller()
        {
            if (ReferenceEquals(CurrentHit.Value, this)) CurrentHit.Value = Previous;
        }

        public void Dispose()
        {
            Active = false;
            RestoreCaller();
        }
    }

    internal sealed class SkillScope : IDisposable
    {
        internal readonly Creature Dealer;
        internal readonly CardModel Card;
        internal readonly decimal TotalHits;
        internal readonly decimal Coefficient;
        internal readonly SkillScope Previous;
        internal bool Active = true;
        private readonly Dictionary<Creature, decimal> _resolvedHits = new();

        internal SkillScope(Creature dealer, CardModel card, decimal totalHits, decimal coefficient)
        {
            Dealer = dealer;
            Card = card;
            TotalHits = Math.Max(1m, decimal.Ceiling(totalHits));
            Coefficient = Math.Max(0m, coefficient);
            Previous = Current.Value;
        }

        internal decimal NextShare(Creature target)
        {
            decimal previous = _resolvedHits.GetValueOrDefault(target);
            decimal next = Math.Min(TotalHits, previous + 1m);
            _resolvedHits[target] = next;
            // Subtract cumulative portions so the last segment receives the
            // decimal remainder and an all-HP skill totals exactly its budget.
            return Coefficient * (next / TotalHits - previous / TotalHits);
        }

        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            RestoreCaller(this);
        }
    }

    internal sealed class Activation : IDisposable
    {
        private readonly SkillScope _scope;
        private readonly SkillScope _previous;
        internal Activation(SkillScope scope)
        {
            _scope = scope;
            _previous = Current.Value;
            Current.Value = scope;
        }

        internal void RestoreCaller()
        {
            if (ReferenceEquals(Current.Value, _scope)) Current.Value = _previous;
        }

        public void Dispose() => RestoreCaller();
    }

    internal static SkillScope BeginSkill(Creature dealer, CardModel card,
        decimal totalHits, decimal skillCoefficient = DefaultSkillCoefficient)
    {
        SkillScope scope = new(dealer, card, totalHits, skillCoefficient);
        Current.Value = scope;
        return scope;
    }

    // Harmony's synchronous caller must be restored as soon as it returns a
    // task. The invoked async operation retains its captured active scope.
    internal static void RestoreCaller(SkillScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    // Re-enter a skill in an async invocation while independently restoring its
    // synchronous caller. The skill owner decides when its budget is closed.
    internal static Activation EnterSkill(SkillScope scope) =>
        scope?.Active == true ? new Activation(scope) : null;

    internal static HitScope BeginHit(Creature dealer, CardModel card, ValueProp props)
    {
        if (CurrentHit.Value == null && !HbrBattleStatsHelper.IsEnabled(dealer)) return null;
        HitScope scope = new(dealer, card, props);
        CurrentHit.Value = scope;
        return scope;
    }

    internal static bool HasActiveHit => CurrentHit.Value is { Active: true, Enabled: true };

    internal static void ObserveHit(Creature target) => CurrentHit.Value?.Observe(target);

    // Hits of the attack now resolving for this dealer and card. Previews and
    // lone native hits have no skill scope and count as one hit.
    internal static decimal GetSkillHits(Creature dealer, CardModel card)
    {
        SkillScope scope = CurrentHit.Value is { Active: true, Skill: { } owned } ? owned : Current.Value;
        return scope?.Active == true && ReferenceEquals(scope.Dealer, dealer) && ReferenceEquals(scope.Card, card)
            ? scope.TotalHits : 1m;
    }

    internal static decimal GetGain(Creature target, Creature dealer,
        CardModel card, ValueProp props)
    {
        if (!props.IsPoweredAttack() || HotaruCannonStatsHelper.IsReactionDamage)
            return 0m;
        decimal skill;
        if (CurrentHit.Value is { Active: true } hit)
            skill = hit.Claim(target, dealer, card, props);
        else
        {
            SkillScope scope = Current.Value;
            skill = scope?.Active == true && ReferenceEquals(scope.Dealer, dealer) &&
                ReferenceEquals(scope.Card, card) ? scope.NextShare(target) : DefaultSkillCoefficient;
        }
        decimal coefficient = GetEnemyCoefficient(target);
        // Blast earrings and ancient chains raise the gain like the original
        // destruction-rate passives.
        return HbrOriginalNumericFormula.DestructionGain(coefficient, skill,
            ElectronicSoldierManual.GetEquipmentDestructionBonus(dealer, card)) *
            CrushYouLikeABugCombatHelper.GetBreakDestructionMultiplier(card, target) *
            RanaByakkoCombatHelper.GetDestructionMultiplier(card) *
            ThirtyOneBCardCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            KanzakiShowtimeCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            TengenBrilliantMoveCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            FubukiChangeCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            AkariAceCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            AinaDeepDiveCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            NiinaNarcissismCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            NiinaNarcissismCombatHelper.GetNextDestructionMultiplier(dealer, card, props) *
            MinoriDeliveryCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            YotsuhaNoEffortCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            IsuzuOpenTheLockCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            MuuaFaithCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            NoblesseObligeCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            KiryuExorcisingStringCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            ThirtyGBondHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            ThirtyOneFDefenseCombatHelper.GetDestructionGainMultiplier(target, dealer, props) *
            YanagiButlerCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            MaruyamaStyleCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            HanamuraOrchestraCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            MatsuokaBlowAwayCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            NatsumeCondemnationCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            KurosawaLeaderCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            CarolHeroCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            IreneExposeCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            MariaRepentanceCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            VritikaCalculationCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            YingxiaFormationCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            CharlottaLoveCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            MonakaWindForestCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            KuraSatomiCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            HisameCloseSlashCombatHelper.GetDestructionGainMultiplier(card, target, dealer, props) *
            ThirtyOneCMachinaHelper.DestructionMultiplier(dealer, card, props) *
            (card != null && card.Owner?.Creature == dealer &&
                !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, card, props, out _)
                ? YamawakiLaboratoryCombatHelper.GetDestructionGainMultiplier(card, target) : 1m) *
            (target?.Side != dealer?.Side
                ? MagicSwordModePower.GetDeckBonusMultiplier(dealer)
                : 1m);
    }

    internal static decimal GetEnemyCoefficient(Creature target)
    {
        // These are explicit original-game examples, not inferred STS stats.
        string type = target?.Monster?.GetType().Name;
        if (string.Equals(type, "FlatHand", StringComparison.Ordinal)) return 6m;
        if (string.Equals(type, "DeathSlug", StringComparison.Ordinal)) return 5m;
        RoomType room = target?.CombatState?.Encounter?.RoomType ??
            target?.CombatState?.RunState.CurrentRoom?.RoomType ?? RoomType.Monster;
        return room is RoomType.Elite or RoomType.Boss ? 5m : 2m;
    }
}
