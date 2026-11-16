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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class KiryuExorcisingStringCombatHelper
{
    static KiryuExorcisingStringCombatHelper() => ThirtyGProposalState.Changed += card => Invalidate(card?.Owner);
    private sealed class Flags { internal bool Dirty = true, Weakening; internal Player Owner; internal int Count = -1; }
    private static readonly ConditionalWeakTable<CardPile, Flags> Decks = new();
    private sealed class Battle { internal readonly Dictionary<Player, Dictionary<Creature, (PowerModel Defense, PowerModel Recovery)>> Eternal = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private sealed class AttackScope : IDisposable
    {
        internal AttackCommand Command;
        internal CardModel Source;
        internal decimal Destruction;
        internal AttackScope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<AttackScope> Current = new();
    internal static void Invalidate(Player player)
    { if (player?.Deck is { } deck && Decks.TryGetValue(deck, out var state)) state.Dirty = true; }
    internal static bool HasPermanentWeakening(Player player)
    {
        if (!HbrBattleStatsHelper.IsEnabled(player) || player.Deck is not { } deck) return false;
        Flags flags = Decks.GetValue(deck, pile => { var value = new Flags(); pile.ContentsChanged += () => value.Dirty = true; return value; });
        if (flags.Dirty || flags.Count != deck.Cards.Count || !ReferenceEquals(flags.Owner, player))
        {
            flags.Weakening = deck.Cards.OfType<ExorcisingStringResoundHbrgoSaki>().Any(card => card.Owner == player &&
                !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) &&
                card.HasUpgradeEffect(ExorcisingStringResoundHbrgoSaki.Weakening));
            flags.Owner = player; flags.Count = deck.Cards.Count; flags.Dirty = false;
        }
        return flags.Weakening;
    }
    internal static async Task StartInsertedTurn(PlayerChoiceContext context, Player player)
    {
        if (player?.Creature?.IsAlive != true) return;
        foreach (var power in player.Creature.Powers.OfType<ExorcisingStringPower>().ToArray()) await TurnStart(context, power);
    }
    internal static async Task TurnStart(PlayerChoiceContext context, ExorcisingStringPower power)
    {
        if (power?.IsMutable != true || power.Amount <= 0 || power.Owner is not { IsAlive: true, Player: not null, CombatState: { } combat } owner ||
            !owner.Powers.Contains(power) || !HbrBattleStatsHelper.IsEnabled(owner) || !power.ClaimTurn()) return;
        Creature[] enemies = combat.GetOpponentsOf(owner).Where(enemy => enemy.IsHittable).ToArray();
        if (enemies.Length == 0) return;
        Creature[] fresh = enemies.Where(enemy => !enemy.HasPower<FragilePower>()).ToArray();
        Creature[] candidates = fresh.Length > 0 ? fresh : enemies;
        Creature selected = candidates[owner.Player.RunState.Rng.CombatTargets.NextInt(0, candidates.Length)];
        Creature[] targets = power.Has(ExorcisingStringResoundHbrgoSaki.GrassArrow) ? enemies : new[] { selected };
        var source = power.Source();
        var duration = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
        using (HbrDebuffApplicationScope.BeginBatch(owner))
            foreach (var target in targets) await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, target, 42m, owner, source, duration);
        if (power.Has(ExorcisingStringResoundHbrgoSaki.NoRather))
        {
            bool defense = owner.Player.RunState.Rng.CombatTargets.NextInt(0, 2) == 0;
            using var batch = HbrDebuffApplicationScope.BeginBatch(owner);
            foreach (var target in targets.Where(target => target.IsAlive))
                if (defense) await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, target, 20m, owner, source, duration);
                else await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, target, 20m, owner, source, duration);
        }
        if (power.Has(ExorcisingStringResoundHbrgoSaki.CursedArrow) && selected.IsHittable)
            await Attack(context, source, selected, 144m, 1, 1.25m);
    }
    private static async Task Attack(PlayerChoiceContext context, ExorcisingStringResoundHbrgoSaki source,
        Creature target, decimal percent, int hits, decimal destruction)
    {
        AttackCommand command = DamageCmd.Attack(HbrSkillPower.DamageUnits(percent)).FromCardCompat(source)
            .WithHitCount(hits).WithHitFx("vfx/vfx_attack_slash");
        if (target == null) command.TargetingAllOpponents(source.Owner.Creature.CombatState);
        else command.Targeting(target);
        using var scope = new AttackScope { Command = command, Source = source, Destruction = destruction, Previous = Current.Value };
        Current.Value = scope;
        await command.Execute(context);
    }
    internal static async Task IceFlowerAttack(PlayerChoiceContext context, ExorcisingStringResoundHbrgoSaki source)
    {
        await Attack(context, source, null, 107m, 5, 2.6m);
        Creature owner = source.Owner.Creature;
        var combat = owner.CombatState;
        Battle battle = Battles.GetOrCreateValue(combat);
        if (!battle.Eternal.TryGetValue(source.Owner, out var recorded)) battle.Eternal.Add(source.Owner, recorded = new());
        using var batch = HbrDebuffApplicationScope.BeginBatch(owner);
        foreach (var enemy in combat.GetOpponentsOf(owner).Where(enemy => enemy.IsAlive).ToArray())
        {
            if (recorded.TryGetValue(enemy, out var previous))
            {
                if (previous.Defense != null && enemy.Powers.Contains(previous.Defense)) await PowerCmd.Remove(previous.Defense);
                if (previous.Recovery != null && enemy.Powers.Contains(previous.Recovery)) await PowerCmd.Remove(previous.Recovery);
            }
            var before = enemy.Powers.ToHashSet();
            var eternal = new HbrEffectProfile(HbrEffectLimit.None, HbrEffectExit.Eternal, 1);
            await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m, owner, source, eternal);
            await HbrEffectApplication.Apply<HbrRecoveryDownPower>(context, enemy, 100m, owner, source,
                profile: eternal, potencyOverride: 100m);
            recorded[enemy] = (enemy.Powers.OfType<HbrDefenseDownPower>().FirstOrDefault(power => !before.Contains(power)),
                enemy.Powers.OfType<HbrRecoveryDownPower>().FirstOrDefault(power => !before.Contains(power)));
        }
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (dealer?.Player == null || !props.IsPoweredAttack() || target == null || target.Side == dealer.Side ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return 1m;
        var scope = Current.Value;
        return scope?.Active == true && ReferenceEquals(scope.Source, source) && source.Owner?.Creature == dealer &&
            ReferenceEquals(scope.Command, HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command)
                ? scope.Destruction / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    }
}
