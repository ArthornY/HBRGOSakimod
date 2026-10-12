using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Monsters;

public interface IHbrgoMonster
{
}

public interface IHbrgoRevivingStaticMonster
{
    bool HasRemainingRevives { get; }

    bool ShouldSuppressDeathFade { get; }
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
}
