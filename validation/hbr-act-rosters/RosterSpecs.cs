using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;

// Expected numbers for the three act rosters (docs/hbr-act-rosters.md).
// Act-one rows were written by hand; act-two and act-three rows were emitted
// from the same roster table that generated those monster classes, then
// reviewed against the document.
static partial class ActRosterChecks
{
    sealed record MoveSpec(string Id, int Damage0 = 0, int Damage10 = 0, int Hits0 = 0, int Hits10 = 0,
        int Block0 = 0, int Block10 = 0, int SelfStrength = 0, string Debuff = "", int DebuffRaw = 0,
        decimal Dp = 0m, int DpBonus0 = 0, int DpBonus10 = 0);
    // Method names the PhantomFusionEffectHelper aggregate the fusion feeds;
    // FirstAttack / EveryThirdAttack are read by PhantomHelper's segment builder.
    sealed record FusionSpec(string Method, decimal First, decimal Next);
    sealed record MonsterSpec(int Act, Type Type, string Art, int Min0, int Max0, int Min10, int Max10,
        FusionSpec Fusion, MoveSpec[] Moves);
    // Encounter -> act, room, weak flag, ordered lineup, native HP budget band (ascension 0).
    sealed record EncounterSpec(int Act, Type Type, RoomType Room, bool Weak, Type[] Lineup, int Low, int High);
    // Mod registry and patch word, the native act whose pools it joins, and that act's pool sizes.
    sealed record ActSpec(int Act, string Word, Type NativeAct, int Weak, int Normal, int Elite, int Boss);

    static readonly ActSpec[] Acts =
    [
        new(1, "One", typeof(Overgrowth), 4, 12, 3, 3),
        new(2, "Two", typeof(Hive), 4, 10, 3, 3),
        new(3, "Three", typeof(Glory), 3, 9, 3, 3),
    ];

    static readonly MonsterSpec[] Monsters =
    [
        // act 1
        new(1, typeof(Marionette), "marionette", 24, 28, 26, 30, new("GetStaticDamageReductionPercent", 3m, 1m),
        [
            new("STRING_PULL_MOVE", 5, 6, 1, 1, Debuff: "AttackDown", DebuffRaw: 1),
            new("ATTACK_MOVE", 7, 8, 1, 1),
        ]),
        new(1, typeof(CatHorn), "cat_horn", 30, 34, 32, 36, new("GetStaticDamageBonusPercent", 4m, 2m),
        [
            new("HORN_RUSH_MOVE", 4, 5, 2, 2),
            new("SWEEP_MOVE", 9, 10, 1, 1),
            new("ROAR_MOVE", SelfStrength: 2),
        ]),
        new(1, typeof(WireSlasher), "wire_slasher", 20, 24, 22, 26, new("FirstAttack", 1m, 1m),
        [
            new("SPIN_SLASH_MOVE", 6, 7, 1, 1),
            new("WIRE_LAUNCH_MOVE", 2, 2, 3, 4),
        ]),
        new(1, typeof(OctopusTail), "octopus_tail", 26, 30, 28, 32, new("GetExtraMaxDp", 2m, 1m),
        [
            new("TENTACLE_WRAP_MOVE", 4, 5, 1, 1, Debuff: "DefenseDown", DebuffRaw: 1),
            new("TAIL_WHIP_MOVE", 9, 10, 1, 1),
        ]),
        new(1, typeof(TargetWireFrame), "target_wire_frame", 24, 28, 26, 30, new("EveryThirdAttack", 4m, 2m),
        [
            new("LOCK_ON_MOVE", Block0: 6, Block10: 8),
            new("COIL_SHOT_MOVE", 11, 12, 1, 1),
        ]),
        new(1, typeof(NeedleBird), "needle_bird", 82, 86, 86, 90, new("GetStaticDamageBonusPercent", 8m, 3m),
        [
            new("PECK_MOVE", 3, 3, 4, 5),
            new("CHARGE_MOVE", Block0: 10, Block10: 12, SelfStrength: 3),
            new("BREAK_PIERCE_MOVE", 15, 17, 1, 1, DpBonus0: 6, DpBonus10: 8),
        ]),
        new(1, typeof(CrowHorse), "crow_horse", 150, 150, 160, 160, new("GetStaticDamageBonusPercent", 10m, 4m),
        [
            new("TRAMPLE_MOVE", 13, 15, 1, 1),
            new("CROW_FLOCK_MOVE", 3, 4, 5, 5),
            new("NEIGH_MOVE", SelfStrength: 3, Debuff: "DefenseDown", DebuffRaw: 2),
            new("STAMPEDE_MOVE", 24, 27, 1, 1),
        ]),
        new(1, typeof(IvoryScooper), "ivory_scooper", 180, 180, 192, 192, new("GetBonusMaxHp", 8m, 4m),
        [
            new("SCOOP_MOVE", 12, 14, 1, 1),
            new("ENTANGLE_MOVE", 7, 8, 1, 1, Debuff: "AttackDown", DebuffRaw: 2),
            new("GROUND_BREAK_MOVE", 5, 6, 3, 3),
            new("IVORY_GUARD_MOVE", Block0: 14, Block10: 18, Dp: 25m),
        ]),
        // act 2
        new(2, typeof(MarionetteKiller), "marionette_killer", 44, 48, 47, 51, new("GetStaticDamageReductionPercent", 4m, 1m),
        [
            new("CURSE_PULL_MOVE", 7, 8, 1, 1, Debuff: "AttackDown", DebuffRaw: 1),
            new("ATTACK_MOVE", 10, 11, 1, 1),
        ]),
        new(2, typeof(CrestHopperKiller), "crest_hopper_killer", 48, 52, 51, 55, new("GetStaticDamageBonusPercent", 6m, 2m),
        [
            new("ATTACK_MOVE", 7, 8, 1, 1),
            new("COMBO_MOVE", 3, 3, 3, 4),
        ]),
        new(2, typeof(DarkKnockerKiller), "dark_knocker_killer", 64, 68, 68, 72, new("EveryThirdAttack", 6m, 3m),
        [
            new("ATTACK_MOVE", 10, 11, 1, 1),
            new("CHARGE_MOVE", SelfStrength: 3),
            new("HEAVY_ATTACK_MOVE", 18, 20, 1, 1),
        ]),
        new(2, typeof(CatHornKiller), "cat_horn_killer", 58, 62, 62, 66, new("GetStaticDamageBonusPercent", 6m, 3m),
        [
            new("HORN_RUSH_MOVE", 6, 7, 2, 2),
            new("SWEEP_MOVE", 14, 15, 1, 1),
            new("ROAR_MOVE", SelfStrength: 3),
        ]),
        new(2, typeof(OctopusTailKiller), "octopus_tail_killer", 46, 50, 49, 53, new("GetExtraMaxDp", 3m, 1m),
        [
            new("TENTACLE_WRAP_MOVE", 6, 7, 1, 1, Debuff: "DefenseDown", DebuffRaw: 1),
            new("TAIL_WHIP_MOVE", 13, 14, 1, 1),
        ]),
        new(2, typeof(AbyssKnockerKiller), "abyss_knocker_killer", 150, 156, 160, 166, new("EveryThirdAttack", 8m, 4m),
        [
            new("ATTACK_MOVE", 12, 13, 1, 1),
            new("MULTI_ATTACK_MOVE", 8, 9, 2, 2),
            new("RECOVERY_MOVE", Block0: 14, Block10: 16, Dp: 20m),
            new("RAGE_WAVE_MOVE", 22, 25, 1, 1, DpBonus0: 8, DpBonus10: 10),
        ]),
        new(2, typeof(TargetWireFrameKiller), "target_wire_frame_killer", 90, 94, 96, 100, new("FirstAttack", 1m, 1m),
        [
            new("LOCK_ON_MOVE", Block0: 12, Block10: 14),
            new("COIL_SHOT_MOVE", 20, 22, 1, 1),
            new("SCATTER_MOVE", 5, 6, 3, 3),
        ]),
        new(2, typeof(PileGolemEdge), "pile_golem_edge", 170, 176, 180, 186, new("GetBonusMaxHp", 10m, 5m),
        [
            new("ROCK_FIST_MOVE", 14, 16, 1, 1),
            new("HARDEN_MOVE", Block0: 20, Block10: 24, SelfStrength: 2),
            new("AVALANCHE_MOVE", 7, 8, 3, 3),
        ]),
        new(2, typeof(DiamondEyeball), "diamond_eyeball", 320, 320, 340, 340, new("GetStaticDamageBonusPercent", 12m, 5m),
        [
            new("GAZE_MOVE", 10, 11, 1, 1, Debuff: "DefenseDown", DebuffRaw: 2),
            new("PRISM_RAY_MOVE", 24, 27, 1, 1),
            new("FACET_SHIELD_MOVE", Block0: 20, Block10: 24, Dp: 30m),
            new("BEAM_VOLLEY_MOVE", 6, 7, 4, 4),
        ]),
        new(2, typeof(BarrelWalker), "barrel_walker", 360, 360, 380, 380, new("FirstAttack", 2m, 1m),
        [
            new("CANNON_MOVE", 14, 16, 1, 1),
            new("VOLLEY_MOVE", 5, 6, 4, 4),
            new("RELOAD_MOVE", Block0: 15, Block10: 18, SelfStrength: 3),
            new("HEAVY_CANNON_MOVE", 30, 34, 1, 1),
        ]),
        // act 3
        new(3, typeof(BalbFlea), "balb_flea", 52, 56, 56, 60, new("GetStaticDamageReductionPercent", 4m, 2m),
        [
            new("FLASH_MOVE", 9, 10, 1, 1, Debuff: "AttackDown", DebuffRaw: 1),
            new("RAM_MOVE", 14, 15, 1, 1),
        ]),
        new(3, typeof(TriClaw), "tri_claw", 58, 62, 62, 66, new("FirstAttack", 1m, 1m),
        [
            new("TRIPLE_CLAW_MOVE", 6, 7, 3, 3),
            new("DART_MOVE", 16, 18, 1, 1),
        ]),
        new(3, typeof(BigotryGate), "bigotry_gate", 100, 106, 106, 112, new("GetExtraMaxDp", 4m, 2m),
        [
            new("DOOR_GUARD_MOVE", Block0: 14, Block10: 17),
            new("RAM_MOVE", 18, 20, 1, 1),
            new("ECHO_MOVE", 7, 8, 2, 2, Debuff: "DefenseDown", DebuffRaw: 1),
        ]),
        new(3, typeof(CorollaKnife), "corolla_knife", 78, 82, 83, 87, new("GetStaticDamageBonusPercent", 7m, 3m),
        [
            new("SLASH_MOVE", 15, 17, 1, 1),
            new("PETAL_DANCE_MOVE", 5, 6, 4, 4),
            new("WHET_MOVE", SelfStrength: 3),
        ]),
        new(3, typeof(MolArms), "mol_arms", 120, 126, 128, 134, new("EveryThirdAttack", 10m, 5m),
        [
            new("HEAVY_PUNCH_MOVE", 17, 19, 1, 1),
            new("DOUBLE_SMASH_MOVE", 8, 9, 2, 2),
            new("ROAR_MOVE", Block0: 12, Block10: 14, SelfStrength: 3),
        ]),
        new(3, typeof(ShrimpCrab), "shrimp_crab", 120, 126, 128, 134, new("GetStaticDamageReductionPercent", 5m, 2m),
        [
            new("PINCER_MOVE", 16, 18, 1, 1),
            new("HARD_SHELL_MOVE", Block0: 16, Block10: 20, Dp: 20m),
            new("CRUSH_MOVE", 7, 8, 3, 3),
        ]),
        new(3, typeof(SlimySerpent), "slimy_serpent", 250, 260, 266, 276, new("GetBonusMaxHp", 12m, 6m),
        [
            new("CONSTRICT_MOVE", 12, 14, 1, 1, Debuff: "AttackDown", DebuffRaw: 2),
            new("TAIL_SLAM_MOVE", 26, 29, 1, 1),
            new("SHED_MOVE", Block0: 18, Block10: 22, SelfStrength: 2),
            new("FANG_FLURRY_MOVE", 6, 7, 4, 4),
        ]),
        new(3, typeof(GiraffeHalo), "giraffe_halo", 240, 250, 256, 266, new("GetStaticDamageBonusPercent", 10m, 4m),
        [
            new("HALO_MOVE", 10, 12, 1, 1, Debuff: "DefenseDown", DebuffRaw: 2),
            new("NECK_LASH_MOVE", 24, 27, 1, 1),
            new("HALO_SHOTS_MOVE", 5, 6, 5, 5),
            new("PRAYER_MOVE", Block0: 18, Block10: 22, SelfStrength: 3),
        ]),
        new(3, typeof(ArachneLine), "arachne_line", 420, 420, 450, 450, new("GetStaticDamageBonusPercent", 14m, 6m),
        [
            new("SILK_BIND_MOVE", 12, 14, 1, 1, Debuff: "AttackDown", DebuffRaw: 2),
            new("EIGHT_SLASH_MOVE", 6, 7, 6, 6),
            new("WEB_WEAVE_MOVE", Block0: 25, Block10: 30, Dp: 40m),
            new("SEVER_MOVE", 36, 40, 1, 1),
        ]),
        new(3, typeof(PillarGuardian), "pillar_guardian", 460, 460, 490, 490, new("GetExtraMaxDp", 12m, 6m),
        [
            new("PILLAR_STRIKE_MOVE", 20, 23, 1, 1),
            new("RESONANCE_MOVE", SelfStrength: 4, Debuff: "DefenseDown", DebuffRaw: 2),
            new("QUAKE_MOVE", 8, 9, 4, 4),
            new("GUARD_MOVE", Block0: 30, Block10: 36, Dp: 50m),
            new("COLLAPSE_MOVE", 40, 45, 1, 1),
        ]),
    ];

    static readonly EncounterSpec[] Encounters =
    [
        // act 1
        new(1, typeof(MarionetteWeak), RoomType.Monster, true, [typeof(Marionette)], 20, 60),
        new(1, typeof(SmallHoppersNormal), RoomType.Monster, false, [typeof(SmallHopper), typeof(SmallHopper), typeof(SmallHopper)], 36, 110),
        new(1, typeof(CrestHopperSwarmNormal), RoomType.Monster, false, [typeof(CrestHopper), typeof(CrestHopper), typeof(CrestHopper)], 36, 110),
        new(1, typeof(CatHornsNormal), RoomType.Monster, false, [typeof(CatHorn), typeof(CatHorn)], 36, 110),
        new(1, typeof(MarionettesWithHealerNormal), RoomType.Monster, false, [typeof(Marionette), typeof(HealHopper), typeof(Marionette)], 36, 110),
        new(1, typeof(OctopusTailSquadNormal), RoomType.Monster, false, [typeof(CrestHopper), typeof(OctopusTail), typeof(HealHopper)], 36, 110),
        new(1, typeof(TargetWireFrameSquadNormal), RoomType.Monster, false, [typeof(CrestHopper), typeof(TargetWireFrame), typeof(HealHopper)], 36, 110),
        new(1, typeof(KnockersWithHealerNormal), RoomType.Monster, false, [typeof(Knocker), typeof(HealHopper), typeof(Knocker)], 36, 110),
        new(1, typeof(MarionetteAndCatHornsNormal), RoomType.Monster, false, [typeof(CatHorn), typeof(Marionette), typeof(CatHorn)], 36, 110),
        new(1, typeof(OctopusTailsNormal), RoomType.Monster, false, [typeof(OctopusTail), typeof(OctopusTail)], 36, 110),
        new(1, typeof(TargetWireFramesNormal), RoomType.Monster, false, [typeof(TargetWireFrame), typeof(CrestHopper), typeof(TargetWireFrame)], 36, 110),
        new(1, typeof(WireSlashersNormal), RoomType.Monster, false, [typeof(WireSlasher), typeof(WireSlasher), typeof(WireSlasher)], 36, 110),
        new(1, typeof(NeedleBirdElite), RoomType.Elite, false, [typeof(NeedleBird)], 80, 130),
        new(1, typeof(CrowHorseBoss), RoomType.Boss, false, [typeof(CrowHorse)], 100, 320),
        new(1, typeof(IvoryScooperBoss), RoomType.Boss, false, [typeof(IvoryScooper)], 100, 320),
        // act 2
        new(2, typeof(MarionetteKillerWeak), RoomType.Monster, true, [typeof(MarionetteKiller)], 40, 110),
        new(2, typeof(CrestHopperKillerWeak), RoomType.Monster, true, [typeof(CrestHopperKiller)], 40, 110),
        new(2, typeof(DarkKnockerKillerWeak), RoomType.Monster, true, [typeof(DarkKnockerKiller)], 40, 110),
        new(2, typeof(MarionetteKillersWeak), RoomType.Monster, true, [typeof(MarionetteKiller), typeof(MarionetteKiller)], 40, 110),
        new(2, typeof(CrestHopperKillerAndMarionettesNormal), RoomType.Monster, false, [typeof(MarionetteKiller), typeof(CrestHopperKiller), typeof(MarionetteKiller)], 80, 190),
        new(2, typeof(KillerTrainingSquadNormal), RoomType.Monster, false, [typeof(MarionetteKiller), typeof(HealHopper), typeof(CrestHopperKiller)], 80, 190),
        new(2, typeof(DarkKnockerKillerSquadNormal), RoomType.Monster, false, [typeof(DarkKnockerKiller), typeof(HealHopper), typeof(CrestHopperKiller)], 80, 190),
        new(2, typeof(CatHornKillerWithHealersNormal), RoomType.Monster, false, [typeof(HealHopper), typeof(CatHornKiller), typeof(HealHopper)], 80, 190),
        new(2, typeof(CrestHopperKillersNormal), RoomType.Monster, false, [typeof(CrestHopperKiller), typeof(CrestHopperKiller), typeof(CrestHopperKiller)], 80, 190),
        new(2, typeof(DarkKnockerKillersNormal), RoomType.Monster, false, [typeof(DarkKnockerKiller), typeof(DarkKnockerKiller)], 80, 190),
        new(2, typeof(DarkKnockerKillerAndMarionettesNormal), RoomType.Monster, false, [typeof(MarionetteKiller), typeof(DarkKnockerKiller), typeof(MarionetteKiller)], 80, 190),
        new(2, typeof(CatHornKillersNormal), RoomType.Monster, false, [typeof(CatHornKiller), typeof(CatHornKiller)], 80, 190),
        new(2, typeof(OctopusTailKillersNormal), RoomType.Monster, false, [typeof(OctopusTailKiller), typeof(CrestHopperKiller), typeof(OctopusTailKiller)], 80, 190),
        new(2, typeof(MarionetteKillerAndCatHornsNormal), RoomType.Monster, false, [typeof(CatHornKiller), typeof(MarionetteKiller), typeof(CatHornKiller)], 80, 190),
        new(2, typeof(AbyssKnockerKillerElite), RoomType.Elite, false, [typeof(AbyssKnockerKiller)], 140, 220),
        new(2, typeof(TargetWireFrameKillerElite), RoomType.Elite, false, [typeof(CrestHopperKiller), typeof(TargetWireFrameKiller), typeof(CrestHopperKiller)], 140, 220),
        new(2, typeof(PileGolemEdgeElite), RoomType.Elite, false, [typeof(PileGolemEdge)], 140, 220),
        new(2, typeof(DiamondEyeballBoss), RoomType.Boss, false, [typeof(DiamondEyeball)], 280, 420),
        new(2, typeof(BarrelWalkerBoss), RoomType.Boss, false, [typeof(BarrelWalker)], 280, 420),
        // act 3
        new(3, typeof(BalbFleaAndTriClawWeak), RoomType.Monster, true, [typeof(BalbFlea), typeof(TriClaw)], 80, 170),
        new(3, typeof(BigotryGateWeak), RoomType.Monster, true, [typeof(BigotryGate)], 80, 170),
        new(3, typeof(CorollaKnifeAndTriClawWeak), RoomType.Monster, true, [typeof(CorollaKnife), typeof(TriClaw)], 80, 170),
        new(3, typeof(TriClawAndBalbFleasNormal), RoomType.Monster, false, [typeof(BalbFlea), typeof(TriClaw), typeof(BalbFlea)], 140, 280),
        new(3, typeof(BigotryGateSquadNormal), RoomType.Monster, false, [typeof(BalbFlea), typeof(BigotryGate), typeof(TriClaw)], 140, 280),
        new(3, typeof(CorollaKnifeSquadNormal), RoomType.Monster, false, [typeof(CorollaKnife), typeof(TriClaw), typeof(BalbFlea)], 140, 280),
        new(3, typeof(BigotryGatesNormal), RoomType.Monster, false, [typeof(BigotryGate), typeof(BigotryGate)], 140, 280),
        new(3, typeof(CorollaKnivesNormal), RoomType.Monster, false, [typeof(CorollaKnife), typeof(CorollaKnife)], 140, 280),
        new(3, typeof(BigotryGateAndCorollaKnivesNormal), RoomType.Monster, false, [typeof(CorollaKnife), typeof(BigotryGate), typeof(CorollaKnife)], 140, 280),
        new(3, typeof(MolArmsAndTriClawNormal), RoomType.Monster, false, [typeof(MolArms), typeof(TriClaw)], 140, 280),
        new(3, typeof(MolArmsPairNormal), RoomType.Monster, false, [typeof(MolArms), typeof(MolArms)], 140, 280),
        new(3, typeof(BigotryGateAndMarionetteKillersNormal), RoomType.Monster, false, [typeof(MarionetteKiller), typeof(BigotryGate), typeof(MarionetteKiller)], 140, 280),
        new(3, typeof(ShrimpCrabsElite), RoomType.Elite, false, [typeof(ShrimpCrab), typeof(ShrimpCrab)], 200, 320),
        new(3, typeof(SlimySerpentElite), RoomType.Elite, false, [typeof(SlimySerpent)], 200, 320),
        new(3, typeof(GiraffeHaloElite), RoomType.Elite, false, [typeof(GiraffeHalo)], 200, 320),
        new(3, typeof(ArachneLineBoss), RoomType.Boss, false, [typeof(ArachneLine)], 350, 560),
        new(3, typeof(PillarGuardianBoss), RoomType.Boss, false, [typeof(PillarGuardian)], 350, 560),
    ];
}
