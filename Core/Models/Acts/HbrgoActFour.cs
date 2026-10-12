using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Events;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Models.Acts;

public sealed class HbrgoActFour : ActModel
{
    public override int Index => 3;

    public override bool IsDefault => true;

    protected override int NumberOfWeakEncounters => 0;

    protected override int BaseNumberOfRooms => 3;

    public override string[] BgMusicOptions =>
        new[]
        {
            "event:/music/act3_a1_v1",
            "event:/music/act3_a2_v1"
        };

    public override string[] MusicBankPaths =>
        new[]
        {
            "res://banks/desktop/act3_a1.bank",
            "res://banks/desktop/act3_a2.bank"
        };

    public override string AmbientSfx =>
        "event:/sfx/ambience/act3_ambience";

    public override string ChestSpineResourcePath =>
        "res://animations/backgrounds/treasure_room/" +
        "chest_room_act_3_skel_data.tres";

    public override string ChestSpineSkinNameNormal => "act3";

    public override string ChestSpineSkinNameStroke => "act3_stroke";

    public override string ChestOpenSfx =>
        "event:/sfx/ui/treasure/treasure_act3";

    public override Color MapTraveledColor => new("1D1E2F");

    public override Color MapUntraveledColor => new("60717C");

    public override Color MapBgColor => new("819A97");

    public override IEnumerable<EncounterModel> BossDiscoveryOrder =>
        HbrgoActFourEncounterRegistry.GetUnlocked();

    public override IEnumerable<AncientEventModel> AllAncients =>
        new[]
        {
            ModelDb.AncientEvent<ChihayaAnonTogawaSakiko>()
        };

    public override IEnumerable<EventModel> AllEvents =>
        Array.Empty<EventModel>();

    public override IEnumerable<EncounterModel> GenerateAllEncounters()
    {
        return HbrgoActFourEncounterRegistry.GetUnlocked();
    }

    public override IEnumerable<AncientEventModel> GetUnlockedAncients(
        UnlockState unlockState)
    {
        return AllAncients;
    }

    internal void EnsureChihayaAnonTogawaSakikoAncient()
    {
        AncientEventModel ancient =
            ModelDb.AncientEvent<ChihayaAnonTogawaSakiko>();
        if (!_rooms.HasAncient || _rooms.Ancient.Id != ancient.Id)
        {
            _rooms.Ancient = ancient;
        }
    }

    protected override void ApplyActDiscoveryOrderModifications(
        UnlockState unlockState)
    {
    }

    public override bool IsUnlocked(UnlockState unlockState)
    {
        return true;
    }

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng)
    {
        return new MapPointTypeCounts(0, 2);
    }
}
