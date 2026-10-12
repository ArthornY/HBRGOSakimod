using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>Each stack reserves one extra turn, including its hand retention.</summary>
public sealed class BoostUpExtraTurnPower : PowerModel, ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsBoostTurnActive { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasPendingEnergy { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override LocString Description => HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);
    public override Color AmountLabelColor => new("6ED7FF");
    public string CustomPackedIconPath => ModelDb.Power<EnergyNextTurnPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override bool ShouldTakeExtraTurn(Player player) =>
        player == Owner.Player && Owner.IsAlive && Amount > 0 && !IsBoostTurnActive;

    public override bool ShouldFlush(Player player) =>
        player != Owner.Player || !Owner.IsAlive || Amount <= 0;

    public override Task AfterTakingExtraTurn(Player player)
    {
        // Native extra-turn sources share the same transition. Never activate
        // on another player's turn or consume an already active reservation.
        if (!ShouldTakeExtraTurn(player)) return Task.CompletedTask;

        AssertMutable();
        IsBoostTurnActive = true;
        HasPendingEnergy = true;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || !Owner.IsAlive ||
            !IsBoostTurnActive || !HasPendingEnergy) return;

        AssertMutable();
        HasPendingEnergy = false;
        // This hook follows ALL energy-reset hooks, including Overclock's
        // late SetEnergy. Its fixed allotment cannot erase this bonus.
        Flash();
        await PlayerCmd.GainEnergy(2m, player);
    }

    public override async Task AfterSideTurnEndLate(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (!IsBoostTurnActive || side != Owner.Side || !participants.Contains(Owner)) return;

        AssertMutable();
        IsBoostTurnActive = false;
        HasPendingEnergy = false;
        // AfterSideTurnEndLate runs after hand flushing. Keeping the active
        // stack until here retains both the entering hand and this turn's hand.
        // Stacks added during the extra turn remain queued for subsequent turns.
        await PowerCmd.ModifyAmount(choiceContext, this, -1m, null, null);
    }
}
