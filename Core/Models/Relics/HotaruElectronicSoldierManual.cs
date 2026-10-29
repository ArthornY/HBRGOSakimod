using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

[CustomID("HOTARU_ELECTRONIC_SOLDIER_MANUAL")]
public sealed class HotaruElectronicSoldierManual : ElectronicSoldierManual
{
    private bool _awaitingOpeningAncient;
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/electronic_soldier_manual.png");
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => Array.Empty<IHoverTip>();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool StartingHealthInitialized { get; private set; }

    public override Task AfterObtained()
    {
        InitializeStartingHealth();
        return Task.CompletedTask;
    }

    internal void InitializeStartingHealth()
    {
        if (StartingHealthInitialized || Owner?.Character is not HbrgoHotaru) return;
        StartingHealthInitialized = true;
        _awaitingOpeningAncient = true;
        Owner.Creature.SetCurrentHpInternal(HbrgoHotaru.InitialScaledCurrentHp);
    }

    internal void CompleteStartingHealthAfterAncient(AncientEventModel ancient)
    {
        if (!_awaitingOpeningAncient || ancient is not Neow ||
            !ReferenceEquals(ancient.Owner, Owner) || Owner.RunState.CurrentActIndex != 0) return;
        RunManager manager = RunManager.Instance;
        if (!ReferenceEquals(manager.DebugOnlyGetState(), Owner.RunState) ||
            Owner.RunState.CurrentRoom is not EventRoom { CanonicalEvent: Neow } ||
            manager.EventSynchronizer?.Events.Any(current => ReferenceEquals(current, ancient)) != true) return;
        // Neow sets HP to zero and heals it to maximum after run setup. Finish
        // this fresh run's initial HP only after that awaited opening completes.
        _awaitingOpeningAncient = false;
        Owner.Creature.SetCurrentHpInternal(HbrgoHotaru.InitialScaledCurrentHp);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _awaitingOpeningAncient = false;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        // A run that skips Neow must not retain a later HP-reset opportunity.
        if (room is not EventRoom { CanonicalEvent: Neow }) _awaitingOpeningAncient = false;
        return base.AfterRoomEntered(room);
    }

    // Old saves can lack the receipt. Loading preserves their current HP.
    internal void PreserveLoadedHealth()
    {
        StartingHealthInitialized = true;
        _awaitingOpeningAncient = false;
    }
}
