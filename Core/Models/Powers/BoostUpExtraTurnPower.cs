using System;
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

/// <summary>Each received stack immediately queues one personal inserted turn.</summary>
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

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        Player player = Owner?.Player;
        int count = Math.Max(0, Amount);
        if (player != null && count > 0) PersonalImmediateTurnCoordinator.Request(player, count);
        await PowerCmd.Remove(this);
    }
}
