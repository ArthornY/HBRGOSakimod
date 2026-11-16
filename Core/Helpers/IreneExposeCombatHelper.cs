using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
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

internal static class IreneExposeCombatHelper
{
    private sealed class Battle
    {
        internal ICombatState Combat;
        internal long Dispelled;
        internal readonly Dictionary<(int, object), int> Energy = new();
    }
    private sealed class Receipt : IDisposable
    {
        internal ThereIsOnlyOneTruthHbrgoSaki Card;
        internal AttackCommand Command;
        internal bool Active = true;
        internal Receipt Previous;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<Player, Battle> Battles = new();
    private static readonly AsyncLocal<Receipt> Current = new();
    private static bool _lifecycle;
    private static void EnsureLifecycle()
    {
        if (_lifecycle) return;
        _lifecycle = true;
        CombatManager.Instance.CombatEnded += _ => Battles.Clear();
    }
    private static Battle Get(Player owner)
    {
        if (owner?.Creature?.CombatState is not { } combat) return null;
        Battle state = Battles.GetOrCreateValue(owner);
        if (!ReferenceEquals(state.Combat, combat))
        { state.Combat = combat; state.Dispelled = 0L; state.Energy.Clear(); }
        return state;
    }
    internal static long GetDispelledKindCount(Player owner) => owner?.Creature?.CombatState is { } combat &&
        Battles.TryGetValue(owner, out var state) && ReferenceEquals(state.Combat, combat) ? state.Dispelled : 0L;
    // Only explicit successful player dispels call this. Natural Remove/expiry
    // and allied cleansing never become evidence by merely changing a power.
    internal static void OnBuffKindDispelled(Player source, Creature enemy, ModelId kind)
    {
        if (!HbrBattleStatsHelper.IsEnabled(source) || source?.Creature is not { } actor || enemy == null || kind == null ||
            enemy.Side == actor.Side || !ReferenceEquals(enemy.CombatState, actor.CombatState)) return;
        EnsureLifecycle();
        Battle state = Get(source);
        if (state != null && state.Dispelled < long.MaxValue) state.Dispelled++;
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (source is not ThereIsOnlyOneTruthHbrgoSaki { IsMutable: true } || dealer == null || source.Owner?.Creature != dealer ||
            !HbrBattleStatsHelper.IsEnabled(dealer) || !props.IsPoweredAttack() || target != null && target.Side == dealer.Side ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return false;
        if (Current.Value is { Active: true } receipt && ReferenceEquals(receipt.Card, source) && receipt.Command != null)
            return HighHitDamageVisualHelper.IsDirectAttackDamage(receipt.Command);
        return HotaruCannonStatsHelper.IsDamagePreview || !StrongestShieldDamagePatch.IsResolvingActualDamage;
    }
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && ((ThereIsOnlyOneTruthHbrgoSaki)source).HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.Qed)
            ? GetDispelledKindCount(source.Owner) * 10m : 0m;
    internal static decimal GetDpDamageMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        target != null && Applies(source, dealer, target, props) && RanaByakkoCombatHelper.HasDp(target) ? 1.3m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? 1m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static bool ForcesCritical(Creature target, Creature dealer, CardModel card, ValueProp props) =>
        Applies(card, dealer, target, props) && ((ThereIsOnlyOneTruthHbrgoSaki)card).HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.PhantomThief);
    private static async Task GrantEnergy(Player owner, int removed)
    {
        if (removed <= 0) return;
        Battle state = Get(owner);
        if (state == null) return;
        var turn = (owner.PlayerCombatState?.TurnNumber ?? 0, OverclockImmediateTurnCoordinator.GetTurnToken(owner));
        int used = state.Energy.GetValueOrDefault(turn), amount = Math.Min(removed, Math.Max(0, 2 - used));
        if (amount == 0) return;
        state.Energy[turn] = used + amount;
        await PlayerCmd.GainEnergy(amount, owner);
    }
    internal static async Task Play(PlayerChoiceContext context, ThereIsOnlyOneTruthHbrgoSaki card, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(card.Owner)) return;
        EnsureLifecycle();
        Creature actor = card.Owner.Creature;
        Creature[] targets = card.HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.Insight)
            ? actor.CombatState.GetOpponentsOf(actor).Where(enemy => enemy.IsHittable).ToArray()
            : play.Target is { IsHittable: true } target ? new[] { target } : Array.Empty<Creature>();
        var countOne = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
        if (card.HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.StayAwake))
        {
            await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, actor, 35m, actor, card, profile: countOne);
            await HbrComboPower.Grant<HbrComboLargePower>(context, actor, actor, card);
        }
        if (card.HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.NoUnsolvableMystery))
        {
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, actor, 50m, actor, card, profile: countOne);
            await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, actor, 30m, actor, card, profile: countOne);
        }
        foreach (Creature enemy in targets)
        {
            int removed = await PowerDispelRules.RandomRemoveBuffKinds(card.Owner, enemy, card.ExposeKindCount);
            if (card.HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.CutTheKnot)) await GrantEnergy(card.Owner, removed);
        }
        if (targets.Length == 0) return;
        using Receipt receipt = new() { Card = card, Previous = Current.Value };
        Current.Value = receipt;
        AttackCommand command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
            .WithHitCount(1).WithHitFx("vfx/vfx_attack_pierce");
        receipt.Command = card.HasUpgradeEffect(ThereIsOnlyOneTruthHbrgoSaki.Insight)
            ? command.TargetingAllOpponents(actor.CombatState) : command.Targeting(targets[0]);
        await receipt.Command.Execute(context);
    }
}
