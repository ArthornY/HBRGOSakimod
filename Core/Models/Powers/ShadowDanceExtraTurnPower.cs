using Godot;
using System;
using HBRGOSakiMod.Core.Helpers;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ShadowDanceExtraTurnPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasQueuedExtraTurn { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override LocString Description => HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);

    public override Color AmountLabelColor => new("9B9DFF");

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        Player player = Owner?.Player;
        int count = Math.Max(0, Amount);
        if (player != null && count > 0) PersonalImmediateTurnCoordinator.Request(player, count);
        await PowerCmd.Remove(this);
    }
}
