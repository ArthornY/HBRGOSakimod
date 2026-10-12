using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SkullFeatherRevivalPower : PowerModel
{
    private sealed class Data
    {
        public bool IsReviving;
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    private bool IsReviving => GetInternalData<Data>().IsReviving;

    protected override object InitInternalData()
    {
        return new Data();
    }

    internal void FinishRevive()
    {
        GetInternalData<Data>().IsReviving = false;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented ||
            creature != Owner ||
            creature.Monster is not SkullFeather skullFeather ||
            !skullFeather.HasRemainingRevives)
        {
            return;
        }

        GetInternalData<Data>().IsReviving = true;
        skullFeather.BeginRevive(
            creature.GetPowerAmount<StrengthPower>());
        await skullFeather.TriggerReviveState();
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
