using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Original per-variant DP/HP shares, frozen in monster-req-20261114. The
// compatible total remains 900 per native HP; only its two pools move.
// Keep integer fractions until the resource boundary instead of rounded shares.
internal static class HbrEnemyTableResourceProfiles
{
    private static HbrEnemyResourceProfile Share(decimal dp, decimal hp) =>
        new(dp + hp, 900m * hp, 900m * dp);

    internal static HbrEnemyResourceProfile? Get(MonsterModel monster) => monster switch
    {
        Marionette => Share(3600m, 3600m),
        CatHorn => Share(5400m, 10800m),
        WireSlasher => Share(7600m, 6200m),
        OctopusTail => Share(8400m, 5600m),
        TargetWireFrame => Share(6200m, 7600m),
        NeedleBird => Share(27800m, 31600m),
        CrowHorse => Share(21600m, 32400m),
        IvoryScooper => Share(16000m, 50000m),
        CrestHopper => Share(4000m, 6600m),
        HealHopper => Share(4000m, 6600m),
        Knocker => Share(2000m, 5000m),
        ClayKnocker => Share(3600m, 9000m),
        AbyssKnocker => Share(44200m, 73600m),
        MarionetteKiller value => value.MoveTableVariant == MarionetteKiller.CurseVariant ? Share(2600m, 4300m) : Share(1700m, 1700m),
        CrestHopperKiller => Share(2400m, 4000m),
        DarkKnockerKiller => Share(2800m, 6700m),
        CatHornKiller => Share(4000m, 8000m),
        OctopusTailKiller => Share(6200m, 4100m),
        AbyssKnockerKiller => Share(11000m, 18400m),
        TargetWireFrameKiller => Share(7000m, 13000m),
        PileGolemEdge => Share(14000m, 29000m),
        DiamondEyeball => Share(32800m, 19500m),
        BarrelWalker => Share(31000m, 47000m),
        BalbFlea => Share(3500m, 5600m),
        TriClaw value => value.MoveTableVariant == TriClaw.DefaultVariant ? Share(3200m, 2000m) : Share(6400m, 3600m),
        BigotryGate value => value.MoveTableVariant == BigotryGate.DefaultVariant ? Share(7300m, 3500m) : Share(5900m, 2500m),
        CorollaKnife => Share(4400m, 6200m),
        MolArms => Share(6000m, 15000m),
        ShrimpCrab => Share(21500m, 8500m),
        SlimySerpent => Share(76000m, 20000m),
        GiraffeHalo => Share(7500m, 25000m),
        ArachneLine => Share(41000m, 56000m),
        PillarGuardian => Share(21000m, 60000m),
        _ => null
    };
}
