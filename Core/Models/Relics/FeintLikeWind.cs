using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class FeintLikeWind : RelicModel
{
    private int _pendingExtraTurns;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/feint_like_wind.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingExtraTurns
    {
        get => _pendingExtraTurns;
        private set
        {
            AssertMutable();
            _pendingExtraTurns = Math.Max(0, value);
        }
    }

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner && PendingExtraTurns > 0;
    }

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner)
        {
            TryConsumePendingExtraTurn();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        PendingExtraTurns = 0;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    internal void QueueExtraTurn()
    {
        if (!CombatManager.Instance.IsInProgress || Owner.Creature.IsDead)
        {
            return;
        }

        PendingExtraTurns++;
        Status = RelicStatus.Active;
        Flash();
    }

    private bool TryConsumePendingExtraTurn()
    {
        if (PendingExtraTurns <= 0)
        {
            return false;
        }

        PendingExtraTurns--;
        if (PendingExtraTurns == 0)
        {
            Status = RelicStatus.Normal;
        }

        return true;
    }
}
