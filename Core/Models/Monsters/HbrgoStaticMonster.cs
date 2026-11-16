using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Models.Monsters;

public interface IHbrgoMonster
{
}

public interface IHbrgoRevivingStaticMonster
{
    bool HasRemainingRevives { get; }

    bool ShouldSuppressDeathFade { get; }
}

// An enemy whose maximum HP and DP are the original's absolute values: no
// x700, enemy HP/DP settings, act growth, multiplayer or difficulty scaling,
// in both numeric modes (GoldHopper). Only an enemy-side creature; a 30A
// phantom of it keeps the phantom rules, which read its native HP range.
internal interface IHbrgoFixedEnemyResources
{
    int FixedMaxHp { get; }

    decimal FixedMaxDp { get; }
}

// A phantom replays an attack intent's damage instead of running the move
// body. Mod monsters whose attacks also apply effects restore them here, once
// per replayed intent, against the phantom's valid opponents.
internal interface IHbrgoPhantomAttackEffects
{
    Task AfterPhantomAttackIntent(MoveState move, IReadOnlyList<Creature> targets);
}

// Who a monster attack hits. Players: every player creature, as native
// monster attacks do. AllUnits: also the player side's hittable summons (30A
// phantoms, Osty). MinimumDp: the living player with the least DP.
internal enum HbrgoAttackTargets
{
    Players,
    AllUnits,
    MinimumDp
}

public abstract class HbrgoStaticMonster : MonsterModel, IHbrgoMonster
{
    public const float DeathFadeSeconds = 1f;

    public override bool HasDeathSfx => false;

    public override bool ShouldFadeAfterDeath => false;

    public override float DeathAnimLengthOverride => DeathFadeSeconds;

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected Task PerformAttack(int damage, int hitCount = 1)
    {
        return PerformAttackOn(damage, hitCount, HbrgoAttackTargets.Players);
    }

    // The native attack command; HbrAttackTargetingPatch widens or narrows its
    // monster target list for the original all-unit and lowest-DP skills.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected async Task PerformAttackOn(int damage, int hitCount, HbrgoAttackTargets targets)
    {
        var attack = DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.15f)
            .WithHitFx("vfx/vfx_attack_blunt");
        HbrAttackTargeting.Set(attack, targets);
        await attack.Execute(null);
    }

    // Enemy DP shell or a player's Deflector Protection counts as DP.
    protected static bool HasActiveDp(Creature target)
    {
        HighDimensionalShellPower shell =
            target.GetPower<HighDimensionalShellPower>();
        if (shell != null && shell.CurrentDp != 0m)
        {
            return true;
        }

        DeflectorProtection protection =
            target.Player?.GetRelic<DeflectorProtection>();
        return protection != null && protection.Dp != 0;
    }
}
