using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// A world-space friendly hit: only native block/DP/HP resource handling applies.
// It is not a new attack and cannot acquire attributes, dice, crit or debuffs.
internal static class MonakaFriendlyFireHelper
{
    private sealed class Scope : IDisposable
    {
        internal Scope Previous;
        internal CardModel Source;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<Scope> Current = new();
    internal static bool IsFixed(Creature target, Creature dealer, CardModel source, ValueProp props)
    {
        if (dealer != null || source == null || !props.HasFlag(ValueProp.Unpowered)) return false;
        for (Scope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Source, source) && target?.Side == source.Owner?.Creature.Side) return true;
        return false;
    }
    internal static async Task DamageTeam(PlayerChoiceContext context, CardModel source)
    {
        Creature owner = source?.Owner?.Creature;
        if (owner?.CombatState is not { } combat || combat.Players.Any(player =>
            player.Creature.IsAlive && player.Creature.Side == owner.Side && KuraSatomiCombatHelper.HasParry(player))) return;
        decimal fraction = combat.Players.Any(player => player.Creature.IsAlive && player.Creature.Side == owner.Side &&
            HisameCloseSlashCombatHelper.HasMastersAdvice(player)) ? .125m : .25m;
        var hits = combat.Creatures.Where(target => target.IsAlive && target.Side == owner.Side)
            .Select(target => (Target: target, Amount: decimal.Floor(LargeCreatureHealthHelper.GetMaxHp(target) * fraction))).ToArray();
        using Scope scope = new() { Source = source, Previous = Current.Value };
        Current.Value = scope;
        foreach (var hit in hits)
            if (hit.Target.IsAlive && combat.ContainsCreature(hit.Target) && hit.Amount > 0m)
                await CreatureCommandCompatibility.Damage(context, hit.Target, hit.Amount, ValueProp.Unpowered, (Creature)null, source);
    }
}
