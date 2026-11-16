using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneACutinConditionHelper
{
    private sealed class Observations
    {
        internal readonly Dictionary<Creature, int> Phases = new();
        internal readonly Dictionary<Creature, int> PhaseChangedOnRound = new();
    }
    private static readonly ConditionalWeakTable<object, Observations> Seen = new();

    internal static void Observe(Creature owner)
    {
        var combat = owner?.CombatState;
        if (combat == null) return;
        Observations seen = Seen.GetOrCreateValue(combat);
        foreach (Creature enemy in combat.GetOpponentsOf(owner).Where(enemy => enemy.IsAlive && enemy.Monster is SkullFeather))
        {
            int phase = ((SkullFeather)enemy.Monster).Phase;
            if (phase == 2 && (!seen.Phases.TryGetValue(enemy, out int previous) || previous < phase))
                seen.PhaseChangedOnRound[enemy] = combat.RoundNumber;
            seen.Phases[enemy] = phase;
        }
    }
    // The external prose conditions are normalized in data. Unknown/unavailable
    // original-game encounters stay disabled rather than leaking generic lines.
    internal static bool Matches(Creature owner, string key)
    {
        var combat = owner?.CombatState;
        if (combat == null) return false;
        var enemies = combat.GetOpponentsOf(owner).Where(enemy => enemy.IsAlive).ToArray();
        return key switch
        {
            "skullfeather-present" => enemies.Any(enemy => enemy.Monster is SkullFeather),
            "skullfeather-high-boost" => enemies.Any(enemy => enemy.Monster is SkullFeather feather && feather.Phase == 1 &&
                feather.NextMove?.Id == "P1_HIGH_ATTACK_BOOST_MOVE"),
            "skullfeather-phase-two" => enemies.Any(enemy => enemy.Monster is SkullFeather feather && feather.Phase == 2 &&
                Seen.TryGetValue(combat, out Observations seen) && seen.PhaseChangedOnRound.TryGetValue(enemy, out int round) && round == combat.RoundNumber),
            "flathand-before-chain-break" => enemies.Any(enemy => enemy.Monster is FlatHand hand &&
                hand.Respawns == 2 && hand.NextMove?.Id == "SHIELD_LOCK_BROKEN_MOVE"),
            "boss-opening" => combat.RoundNumber <= 1 &&
                (combat.Encounter?.RoomType ?? combat.RunState.CurrentRoom?.RoomType) == RoomType.Boss,
            _ => false
        };
    }
}
