using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DeathSlugRevivalPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    private bool IsReviving =>
        Owner?.Monster is DeathSlug { IsReviving: true };

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented ||
            creature != Owner ||
            creature.Monster is not DeathSlug deathSlug ||
            !deathSlug.HasRemainingRevives)
        {
            return;
        }

        foreach (Creature player in creature.CombatState.PlayerCreatures)
        {
            DeathSlugRespawnPower existingPower =
                player.GetPower<DeathSlugRespawnPower>();
            if (existingPower != null)
            {
                await PowerCmd.Remove(existingPower);
            }
        }

        deathSlug.BeginRevive(
            creature.GetPowerAmount<StrengthPower>());
        await deathSlug.TriggerReviveState();
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        return creature != Owner || !IsReviving;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return Owner?.IsPrimaryEnemy == true;
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(
        Creature creature)
    {
        return creature != Owner;
    }

    public override bool ShouldPowerBeRemovedAfterOwnerDeath()
    {
        return false;
    }
}

public sealed class DeathSlugRespawnPower : PowerModel
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;
}
