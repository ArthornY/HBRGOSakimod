using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MuuaFaithCombatHelper
{
    private sealed class CounterScope : IDisposable
    {
        internal AttackCommand Command;
        internal CardModel Source;
        internal CounterScope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<CounterScope> Current = new();
    internal sealed class RemovalScope
    {
        internal RemovalScope Previous;
        internal Creature Holder;
        internal decimal Dp;
        internal bool Active = true;
    }
    private static readonly AsyncLocal<RemovalScope> Removing = new();
    private static bool Active(MuuaIWillNotFallPower power) => power?.IsMutable == true && power.Amount > 0 &&
        power.Owner is { IsAlive: true, Player: not null, CombatState: not null } &&
        power.Owner.Powers.Contains(power) && HbrBattleStatsHelper.IsEnabled(power.Owner);

    internal static async Task ResolveHits(Creature attacker, IEnumerable<MuuaIWillNotFallPower> powers)
    {
        foreach (MuuaIWillNotFallPower power in powers.ToArray())
        {
            if (!Active(power) || CombatManager.Instance.IsOverOrEnding) continue;
            HbrFaithResource.Gain(power.Owner, 1);
            if (!power.Has(IWillNotFallHbrgoSaki.EcstaticPain) || attacker?.IsAlive != true ||
                CombatManager.Instance.IsOverOrEnding || !power.Owner.CombatState.GetOpponentsOf(power.Owner).Any(unit => unit.IsAlive)) continue;
            CardModel source = power.Source();
            AttackCommand command = DamageCmd.Attack(HbrSkillPower.DamageUnits(77m)).FromCardCompat(source)
                .TargetingAllOpponents(power.Owner.CombatState).WithHitCount(1).WithHitFx("vfx/vfx_attack_slash");
            using CounterScope scope = new() { Source = source, Command = command, Previous = Current.Value };
            Current.Value = scope;
            await command.Execute(new ThrowingPlayerChoiceContext());
        }
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (!props.IsPoweredAttack() || dealer?.Player == null || target?.Side == dealer.Side ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return 1m;
        CounterScope scope = Current.Value;
        return scope?.Active == true && ReferenceEquals(scope.Source, source) &&
            ReferenceEquals(scope.Command, HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command)
                ? 1.6m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    }
    internal static async Task TurnStart(PlayerChoiceContext context, MuuaIWillNotFallPower power)
    {
        if (!Active(power) || !power.ClaimTurn()) return;
        Creature owner = power.Owner;
        CardModel source = power.Source();
        if (power.Has(IWillNotFallHbrgoSaki.SummerGrace)) HbrFaithResource.Gain(owner, 1);
        // With no per-exchange commands, the unlimited resource need not run
        // an unlimited loop after the finite OD gauge is already full.
        if (!power.Has(IWillNotFallHbrgoSaki.DelightfulSprint) && !power.Has(IWillNotFallHbrgoSaki.NewDay))
        {
            BigInteger exchanges = HbrFaithResource.Get(owner) / 5;
            if (exchanges > 0 && HbrFaithResource.TrySpend(owner, exchanges * 5))
            {
                Overclock overclock = owner.Player.GetRelic<Overclock>();
                // Preserve each observable 50% step up to the native six-level
                // charge capacity, including enemy intent refreshes.
                for (int i = 0, count = (int)BigInteger.Min(exchanges, (Overclock.MaxLevel + (overclock?.ActiveLevel ?? 0)) * 2); i < count; i++)
                    overclock?.GainLevelProgress(50m);
            }
        }
        while (HbrFaithResource.TrySpend(owner, 5))
        {
            owner.Player.GetRelic<Overclock>()?.GainLevelProgress(50m);
            if (power.Has(IWillNotFallHbrgoSaki.DelightfulSprint))
                foreach (Creature ally in RisaRecoveryCombatHelper.Targets(owner.Player))
                {
                    await SeiraDpRecoveryHelper.Restore(context, ally, .1m, true, source);
                    await HbrEffectApplication.Apply<MindsEyePower>(context, ally, MindsEyePower.BasePercent,
                        owner, source, remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
                }
            if (power.Has(IWillNotFallHbrgoSaki.NewDay))
            {
                await PlayerCmd.GainEnergy(1m, owner.Player);
                await CardPileCmd.Draw(context, 1m, owner.Player);
            }
            if (!Active(power) || CombatManager.Instance.IsOverOrEnding) return;
        }
        if (power.Has(IWillNotFallHbrgoSaki.SweetHealing) && OverclockImmediateTurnCoordinator.IsActiveFor(owner.Player))
        {
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, owner, 75m, owner, source,
                remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
            await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, owner, 50m, owner, source,
                remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        }
    }
    private static MuuaIWillNotFallPower RescuePower(Creature changed)
    {
        var player = changed?.Player ?? changed?.PetOwner;
        if (player == null || !ReferenceEquals(changed, FubukiChangeCombatHelper.DpTarget(player)) ||
            FubukiChangeCombatHelper.Dp(player).Current > 0m || CombatManager.Instance.IsEnding) return null;
        MuuaIWillNotFallPower power = player.Creature.GetPower<MuuaIWillNotFallPower>();
        return Active(power) ? power : null;
    }
    internal static void OnDpZero(Creature changed, decimal previous)
    {
        for (RemovalScope removal = Removing.Value; removal != null; removal = removal.Previous)
            if (removal.Active && ReferenceEquals(removal.Holder, changed)) return;
        if (previous <= 0m || RescuePower(changed) is not { } power || !power.ClaimRescue()) return;
        // Callback order is explicit: a fresh recovery can never hide the
        // zero-DP transition from Makeup, regardless of Harmony patch order.
        NiinaMakeupCombatHelper.OnDpChanged(changed);
        if (!SeiraDpRecoveryHelper.TryRestoreExistingFromAbility(changed, .2m, false))
            throw new InvalidOperationException("A DP write must retain its existing resource pool until recovery completes.");
        HbrFaithResource.Gain(power.Owner, 3);
    }
    internal static async Task OnShellRemoved(Creature changed, decimal previous)
    {
        if (previous <= 0m || RescuePower(changed) is not { } power || !power.ClaimRescue()) return;
        NiinaMakeupCombatHelper.OnDpChanged(changed);
        await SeiraDpRecoveryHelper.RestoreFromAbility(new ThrowingPlayerChoiceContext(), changed, .2m, false);
        HbrFaithResource.Gain(power.Owner, 3);
    }
    internal static RemovalScope BeginRemoval(PowerModel power)
    {
        if (power is not HighDimensionalShellPower shell || !power.IsMutable || shell.CurrentDp <= 0m ||
            power.Owner is not { } holder) return null;
        var player = holder.Player ?? holder.PetOwner;
        MuuaIWillNotFallPower ability = player?.Creature.GetPower<MuuaIWillNotFallPower>();
        if (!Active(ability) || ability.DpRescueUsed || !ability.Has(IWillNotFallHbrgoSaki.NeverFall)) return null;
        RemovalScope scope = new() { Previous = Removing.Value, Holder = holder, Dp = shell.CurrentDp };
        Removing.Value = scope; return scope;
    }
    internal static void RestoreRemovalCaller(RemovalScope scope)
    { if (scope != null && ReferenceEquals(Removing.Value, scope)) Removing.Value = scope.Previous; }
    internal static void CloseRemoval(RemovalScope scope)
    { if (scope != null) { scope.Active = false; RestoreRemovalCaller(scope); } }
    internal static async Task FinishRemoval(Task native, RemovalScope scope)
    {
        try { await native; } finally { CloseRemoval(scope); }
        if (scope != null) await OnShellRemoved(scope.Holder, scope.Dp);
    }
    internal static void Clear(ICombatState combat)
    { if (combat != null) foreach (var player in combat.Players) HbrFaithResource.Clear(player.Creature); }
}
