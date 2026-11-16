using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Reuse the existing enemy-move scope: several commands from one move are one
// attack for this protection. Claims happen only after real ordinary block.
internal static class ThirtyOneFDefenseCombatHelper
{
    private sealed class Battle { internal readonly HashSet<Player> Used = new(); }
    private sealed class CounterScope : IDisposable
    {
        internal Creature Owner;
        internal AttackCommand Command;
        internal CounterScope Previous;
        internal bool Active = true;
        public void Dispose()
        {
            Active = false;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<CounterScope> Current = new();
    private static readonly FieldInfo AttackerField = typeof(AttackCommand).GetField("<Attacker>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo SourceField = typeof(AttackCommand).GetField("<ModelSource>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void ResetBattle(Player owner)
    {
        if (owner?.Creature?.CombatState is { } combat && Battles.TryGetValue(combat, out var battle))
            battle.Used.Remove(owner);
    }

    internal static bool TryProtectDp(Creature target, decimal remaining, ValueProp props)
    {
        if (remaining <= 0m || !props.IsPoweredAttack() ||
            ThirtyOneBEnemyActionHelper.CurrentAction is not { } action ||
            action.Attacker is not { Side: CombatSide.Enemy, PetOwner: null } ||
            !HpOnlyDamageContext.IsCurrentCommandFrom(action.Attacker) ||
            target?.Side == CombatSide.Enemy) return false;
        Player owner = target?.Player ?? target?.PetOwner;
        if (owner == null || !ReferenceEquals(target, FubukiChangeCombatHelper.DpTarget(owner)) ||
            !ReferenceEquals(HpOnlyDamageContext.CurrentPhysicalTarget, target)) return false;
        if (action.ButlerOwners?.Contains(owner) == true) return true;
        if (!ThirtyOneFBondHelper.HasBulwark(owner) || target.CombatState is not { } combat ||
            Battles.TryGetValue(combat, out var previous) && previous.Used.Contains(owner)) return false;

        var shell = target.GetPower<HighDimensionalShellPower>();
        bool lethal;
        if (shell != null)
        {
            if (shell is PhantomHighDimensionalShellPower && !PhantomHighDimensionalShellPower.CanAbsorb(props)) return false;
            lethal = shell.WouldBreakFromAttack(remaining);
        }
        else
        {
            // Native Buffer acts at HP-loss time and already prevents this DP
            // spend; it must not consume the once-per-battle butler claim.
            if (target.HasPower<BufferPower>()) return false;
            var protection = owner.GetRelic<DeflectorProtection>();
            protection?.EnsureDpInitialized();
            lethal = protection?.Dp > 0 && remaining >= protection.Dp;
        }
        if (!lethal) return false;
        if (!Battles.GetOrCreateValue(combat).Used.Add(owner)) return false;
        (action.ButlerOwners ??= new()).Add(owner);
        return true;
    }

    internal static async Task ResolveCounters(Creature attacker, IEnumerable<Player> owners)
    {
        foreach (Player owner in owners.ToArray())
        {
            if (attacker?.IsAlive != true || owner?.Creature?.IsAlive != true || CombatManager.Instance.IsOverOrEnding ||
                owner.GetRelic<ThirtyOneFCollection>() is not { } relic) continue;
            AttackCommand command = DamageCmd.Attack(HbrSkillPower.DamageUnits(432m));
            AttackerField.SetValue(command, owner.Creature);
            SourceField.SetValue(command, relic);
            command.Targeting(attacker).WithHitCount(1).WithNoAttackerAnim().WithHitFx("vfx/vfx_attack_slash");
            using CounterScope scope = new() { Owner = owner.Creature, Command = command, Previous = Current.Value };
            Current.Value = scope;
            await command.Execute(new ThrowingPlayerChoiceContext());
        }
    }

    internal static decimal GetDestructionGainMultiplier(Creature target, Creature dealer, ValueProp props)
    {
        for (CounterScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Owner, dealer) && target != null && target.Side != dealer.Side &&
                props.IsPoweredAttack() && HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command))
                return 2.5m / HbrDestructionGainHelper.DefaultSkillCoefficient;
        return 1m;
    }
}
