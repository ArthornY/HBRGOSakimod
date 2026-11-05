using System.Collections.Generic;
using System.Threading.Tasks;
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

// A phantom replays an attack intent's damage instead of running the move
// body. Mod monsters whose attacks also apply effects restore them here, once
// per replayed intent, against the phantom's valid opponents.
internal interface IHbrgoPhantomAttackEffects
{
    Task AfterPhantomAttackIntent(MoveState move, IReadOnlyList<Creature> targets);
}

public abstract class HbrgoStaticMonster : MonsterModel, IHbrgoMonster
{
    public const float DeathFadeSeconds = 1f;

    public override bool HasDeathSfx => false;

    public override bool ShouldFadeAfterDeath => false;

    public override float DeathAnimLengthOverride => DeathFadeSeconds;

    protected async Task PerformAttack(int damage, int hitCount = 1)
    {
        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.15f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    // Enemy DP shell or a player's Deflector Protection counts as DP.
    // (AbyssKnocker keeps its own private copy of this check.)
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
