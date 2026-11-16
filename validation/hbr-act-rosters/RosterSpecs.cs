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
        decimal Dp = 0m, int DpBonus0 = 0, int DpBonus10 = 0,
        // Move-table monsters (MoveTableChecks.cs): the original skill, its status
        // (with amounts at ascension 0/10), hard-mode debuff strength, DP heal share,
        // whether its target defends with spirit, and whether a chance-based status
        // always lands in the original (chance 1) and so does not wait for block.
        string Skill = "", string Status = "", int Status0 = 0, int Status10 = 0, int DebuffRaw10 = 0,
        decimal HealRatio = 0m, bool Spirit = false, bool Sure = false);
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
            new("ATTACK_MOVE", 6, 7, 1, 1, Skill: "Marionette_AttackSingle01"),
            new("STRONG_ATTACK_MOVE", 8, 9, 1, 1, Skill: "Marionette_AttackSingle02"),
            new("RECOVERY_MOVE", Skill: "Marionette_Buff01", HealRatio: 600m / 3600m),
        ]),
        new(1, typeof(CatHorn), "cat_horn", 30, 34, 32, 36, new("GetStaticDamageBonusPercent", 4m, 2m),
        [
            new("HORN_RUSH_MOVE", 6, 7, 1, 1, Skill: "CatHorn_Attack01"),
            new("CREEPY_BALL_MOVE", 8, 9, 1, 1, Skill: "CatHorn_Attack02", Status: "Imprison"),
            new("SWEEP_MOVE", 3, 4, 2, 2, Skill: "CatHorn_AttackAll01", Spirit: true),
            new("HEAL_MOVE", Skill: "CatHorn_Heal01", HealRatio: 700m / 5400m),
        ]),
        new(1, typeof(WireSlasher), "wire_slasher", 20, 24, 22, 26, new("FirstAttack", 1m, 1m),
        [
            new("ATTACK_MOVE", 6, 7, 1, 1, Skill: "WireSlasher_AttackSingle01_Dark"),
            new("COMBO_MOVE", 1, 1, 7, 7, Skill: "WireSlasher_AttackSingle02_Dark"),
            new("ALL_ATTACK_MOVE", 7, 9, 1, 1, Skill: "WireSlasher_AttackAll01_Dark"),
            new("RECOVERY_MOVE", Skill: "WireSlasher_Buff01", HealRatio: 700m / 7600m),
        ]),
        new(1, typeof(OctopusTail), "octopus_tail", 26, 30, 28, 32, new("GetExtraMaxDp", 2m, 1m),
        [
            new("TAIL_WHIP_MOVE", 6, 6, 1, 1, Skill: "OctopusTail_Attack01"),
            new("EROSION_SLICE_MOVE", 7, 9, 1, 1, Skill: "OctopusTail_Attack02", Status: "Virus",
                Status0: 2, Status10: 2),
            new("ALL_ATTACK_MOVE", 7, 8, 1, 1, Skill: "OctopusTail_AttackAll01", Spirit: true),
            new("HEAL_MOVE", Skill: "OctopusTail_Heal01", HealRatio: 700m / 8400m),
        ]),
        new(1, typeof(TargetWireFrame), "target_wire_frame", 24, 28, 26, 30, new("EveryThirdAttack", 4m, 2m),
        [
            new("COIL_SHOT_MOVE", 6, 6, 1, 1, Skill: "TargetWireFrame_Attack01"),
            new("ILLUSION_SPIN_MOVE", 8, 9, 1, 1, Skill: "TargetWireFrame_Attack02", Status: "Illusion",
                Status0: 1, Status10: 1),
            new("SCATTER_MOVE", 7, 8, 1, 1, Skill: "TargetWireFrame_AttackAll01", Spirit: true),
            new("HEAL_MOVE", Skill: "TargetWireFrame_Heal01", HealRatio: 700m / 6200m),
        ]),
        new(1, typeof(NeedleBird), "needle_bird", 82, 86, 86, 90, new("GetStaticDamageBonusPercent", 8m, 3m),
        [
            new("PECK_MOVE", 14, 17, 1, 1, Skill: "NeedleBird_AttackSingle01"),
            new("MURDER_BLADE_MOVE", 16, 19, 1, 1, Skill: "NeedleBird_AttackAll01", Spirit: true),
            new("CHARGE_MOVE", Skill: "NeedleBird_Charge01", Status: "Charge"),
            new("RUIN_BULLET_MOVE", Skill: "NeedleBird_AttackSingle02_Braek", Status: "DpBreak"),
            new("RECOVER_MOVE", Skill: "NeedleBird_Buff01", HealRatio: 700m / 27800m),
        ]),
        new(1, typeof(CrowHorse), "crow_horse", 150, 150, 160, 160, new("GetStaticDamageBonusPercent", 10m, 4m),
        [
            new("TRAMPLE_MOVE", 12, 14, 1, 1, Skill: "CrowHorse_Attack01"),
            new("DESTRUCTION_BALL_MOVE", 15, 18, 1, 1, Skill: "CrowHorse_Attack02", Status: "OdDown",
                Status0: 5, Status10: 10),
            new("EXTINCTION_RAY_MOVE", 13, 16, 1, 1, Skill: "CrowHorse_AttackAll01", Spirit: true),
            new("HEAL_MOVE", Skill: "CrowHorse_Heal01", HealRatio: 3000m / 21600m),
        ]),
        new(1, typeof(IvoryScooper), "ivory_scooper", 180, 180, 192, 192, new("GetBonusMaxHp", 8m, 4m),
        [
            new("SCOOP_MOVE", 7, 9, 1, 1, Skill: "IvoryScooper_Attack01"),
            new("ENERGY_DRAIN_MOVE", 3, 4, 3, 3, Skill: "IvoryScooper_Attack02", Status: "Absorb"),
            new("DESTROY_BASH_MOVE", 9, 10, 1, 1, Skill: "IvoryScooper_AttackAll01", Spirit: true),
        ]),
        // act 2
        new(2, typeof(MarionetteKiller), "marionette_killer", 44, 48, 47, 51, new("GetStaticDamageReductionPercent", 4m, 1m),
        [
            new("ATTACK_MOVE", 11, 13, 1, 1, Skill: "Marionette_AttackSingle01"),
            new("STRONG_ATTACK_MOVE", 13, 14, 1, 1, Skill: "Marionette_AttackSingle02"),
            new("RECOVERY_MOVE", Skill: "Marionette_Buff01", HealRatio: 600m / 1700m),
            new("CURSE_ATTACK_MOVE", 8, 8, 1, 1, Skill: "MarionetteKillerCurse_AttackSingle01"),
            new("CURSE_STAMP_MOVE", 9, 10, 1, 1, Skill: "MarionetteKillerCurse_AttackSingle02", Status: "Calamity",
                Status0: 20, Status10: 30),
            new("ATTACK_UP_MOVE", Skill: "MarionetteKillerCurse_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "MarionetteKillerCurse_Heal", HealRatio: 5000m / 2600m),
        ]),
        new(2, typeof(CrestHopperKiller), "crest_hopper_killer", 48, 52, 51, 55, new("GetStaticDamageBonusPercent", 6m, 2m),
        [
            new("ATTACK_MOVE", 6, 8, 1, 1, Skill: "CrestHopper_DefaultAttack"),
            new("COMBO_MOVE", 1, 1, 8, 8, Skill: "CrestHopper_SkillAttack"),
            new("REPAIR_MOVE", Skill: "CrestHopper_Heal1", HealRatio: 600m / 2400m),
        ]),
        new(2, typeof(DarkKnockerKiller), "dark_knocker_killer", 64, 68, 68, 72, new("EveryThirdAttack", 6m, 3m),
        [
            new("ATTACK_MOVE", 8, 8, 1, 1, Skill: "DarkKnocker_Attack01"),
            new("MULTI_ATTACK_MOVE", 5, 5, 2, 2, Skill: "DarkKnocker_Attack02"),
            new("ALL_ATTACK_MOVE", 11, 12, 1, 1, Skill: "DarkKnocker_AttackAll01"),
        ]),
        new(2, typeof(CatHornKiller), "cat_horn_killer", 58, 62, 62, 66, new("GetStaticDamageBonusPercent", 6m, 3m),
        [
            new("HORN_RUSH_MOVE", 9, 10, 1, 1, Skill: "CatHorn_Attack01"),
            new("CREEPY_BALL_MOVE", 12, 13, 1, 1, Skill: "CatHorn_Attack02", Status: "Imprison"),
            new("SWEEP_MOVE", 5, 6, 2, 2, Skill: "CatHorn_AttackAll01", Spirit: true),
            new("HEAL_MOVE", Skill: "CatHorn_Heal01", HealRatio: 700m / 4000m),
        ]),
        new(2, typeof(OctopusTailKiller), "octopus_tail_killer", 46, 50, 49, 53, new("GetExtraMaxDp", 3m, 1m),
        [
            new("TAIL_WHIP_MOVE", 8, 9, 1, 1, Skill: "OctopusTail_Attack01"),
            new("EROSION_SLICE_MOVE", 11, 12, 1, 1, Skill: "OctopusTail_Attack02", Status: "Virus",
                Status0: 2, Status10: 2),
            new("ALL_ATTACK_MOVE", 9, 10, 1, 1, Skill: "OctopusTail_AttackAll01_Killer", Spirit: true),
            new("HEAL_MOVE", Skill: "OctopusTail_Heal01", HealRatio: 700m / 6200m),
        ]),
        new(2, typeof(AbyssKnockerKiller), "abyss_knocker_killer", 150, 156, 160, 166, new("EveryThirdAttack", 8m, 4m),
        [
            new("ATTACK_MOVE", 13, 15, 1, 1, Skill: "AbyssKnocker_Attack01"),
            new("MULTI_ATTACK_MOVE", 8, 9, 2, 2, Skill: "AbyssKnocker_Attack02"),
            new("RAGE_WAVE_MOVE", 15, 17, 1, 1, Skill: "AbyssKnocker_AttackAll01", Spirit: true),
            new("RECOVERY_MOVE", Skill: "AbyssKnocker_Buff01", HealRatio: 3000m / 11000m),
        ]),
        new(2, typeof(TargetWireFrameKiller), "target_wire_frame_killer", 90, 94, 96, 100, new("FirstAttack", 1m, 1m),
        [
            new("COIL_SHOT_MOVE", 12, 13, 1, 1, Skill: "TargetWireFrame_Attack01"),
            new("ILLUSION_SPIN_MOVE", 16, 18, 1, 1, Skill: "TargetWireFrame_Attack02", Status: "Illusion",
                Status0: 1, Status10: 1),
            new("SCATTER_MOVE", 14, 16, 1, 1, Skill: "TargetWireFrame_AttackAll01", Spirit: true),
            new("HEAL_MOVE", Skill: "TargetWireFrame_Heal02", HealRatio: 5000m / 7000m),
        ]),
        new(2, typeof(PileGolemEdge), "pile_golem_edge", 170, 176, 180, 186, new("GetBonusMaxHp", 10m, 5m),
        [
            new("AVALANCHE_MOVE", 10, 11, 1, 1, Skill: "PileGolemEdge_AttackAll01"),
            new("TREMOR_MOVE", 11, 13, 1, 1, Skill: "PileGolemEdge_AttackAll02", Spirit: true),
            new("CONFUSION_SPIKE_MOVE", 12, 14, 1, 1, Skill: "PileGolemEdge_AttackSingle01", Status: "Confusion",
                Status0: 3, Status10: 3),
            new("ATTACK_UP_MOVE", Skill: "PileGolemEdge_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "PileGolemEdge_Heal", HealRatio: 15000m / 14000m),
        ]),
        new(2, typeof(DiamondEyeball), "diamond_eyeball", 320, 320, 340, 340, new("GetStaticDamageBonusPercent", 12m, 5m),
        [
            new("LASER_BIND_MOVE", 13, 15, 1, 1, Skill: "DiamondEyeball_AttackSingle01", Status: "Bind"),
            new("PRISM_RAY_MOVE", 17, 19, 1, 1, Debuff: "DefenseDown", DebuffRaw: 2, DebuffRaw10: 3,
                Skill: "DiamondEyeball_AttackSingle02"),
            new("DISASTER_RAY_MOVE", 3, 3, 5, 5, Skill: "DiamondEyeball_AttackAll01", Spirit: true),
            new("FACET_SHIELD_MOVE", Skill: "DiamondEyeball_Buff01", HealRatio: 700m / 32800m),
        ]),
        new(2, typeof(BarrelWalker), "barrel_walker", 360, 360, 380, 380, new("FirstAttack", 2m, 1m),
        [
            new("CANNON_MOVE", 14, 16, 1, 1, Skill: "BarrelWalker_AttackSingle01_Fire"),
            new("HEAVY_CANNON_MOVE", 17, 20, 1, 1, Skill: "BarrelWalker_AttackSingle02_Fire"),
            new("RAGE_MAGNUM_MOVE", 2, 2, 8, 8, Debuff: "DefenseDown", DebuffRaw: 1, DebuffRaw10: 2,
                Skill: "BarrelWalker_AttackAll01_Fire", Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "BarrelWalker_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "BarrelWalker_Heal", HealRatio: 15000m / 31000m),
        ]),
        // act 3
        new(3, typeof(BalbFlea), "balb_flea", 52, 56, 56, 60, new("GetStaticDamageReductionPercent", 4m, 2m),
        [
            new("RAM_MOVE", 9, 10, 1, 1, Skill: "BalbFlea_AttackSingle01"),
            new("FLASH_MOVE", 13, 14, 1, 1, Skill: "BalbFlea_AttackAll01", Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "BalbFlea_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "BalbFlea_Heal", HealRatio: 5000m / 3500m),
        ]),
        new(3, typeof(TriClaw), "tri_claw", 58, 62, 62, 66, new("FirstAttack", 1m, 1m),
        [
            new("DART_MOVE", 15, 17, 1, 1, Skill: "TriClaw_AttackSingle01"),
            new("CLAWS_MOVE", 4, 5, 5, 5, Skill: "TriClaw_AttackSingle02", Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "TriClaw_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "TriClaw_Heal", HealRatio: 5000m / 6400m),
        ]),
        new(3, typeof(BigotryGate), "bigotry_gate", 100, 106, 106, 112, new("GetExtraMaxDp", 4m, 2m),
        [
            new("IMPACT_MOVE", 4, 4, 3, 3, Skill: "BigotryGate_AttackSingle01"),
            new("DISASTER_SHOT_MOVE", 14, 16, 1, 1, Skill: "BigotryGate_AttackSingle02", Status: "Calamity",
                Status0: 20, Status10: 30),
            new("STRONG_IMPULSE_MOVE", 19, 22, 1, 1, Skill: "BigotryGate_AttackAll01", Status: "SpiritDown",
                Status0: 2, Status10: 2, Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "BigotryGate_AttackUp", Status: "AttackUp", Status0: 20, Status10: 20),
            new("HEAL_MOVE", Skill: "BigotryGate_Heal", HealRatio: 5000m / 5900m),
        ]),
        new(3, typeof(CorollaKnife), "corolla_knife", 78, 82, 83, 87, new("GetStaticDamageBonusPercent", 7m, 3m),
        [
            new("SLASH_MOVE", 10, 11, 1, 1, Skill: "CorollaKnife_AttackSingle01"),
            new("ROTATION_CUT_MOVE", 13, 15, 1, 1, Debuff: "DefenseDown", DebuffRaw: 2, DebuffRaw10: 3,
                Skill: "CorollaKnife_AttackSingle02"),
            new("RAMPAGE_SLICE_MOVE", 6, 7, 2, 2, Skill: "CorollaKnife_AttackAll01", Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "CorollaKnife_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
        ]),
        new(3, typeof(MolArms), "mol_arms", 120, 126, 128, 134, new("EveryThirdAttack", 10m, 5m),
        [
            new("DOUBLE_SMASH_MOVE", 2, 2, 5, 5, Skill: "MolArms_AttackSingle01"),
            new("DECEIVE_SHOT_MOVE", 4, 4, 3, 3, Skill: "MolArms_AttackSingle02", Status: "WisdomDown",
                Status0: 3, Status10: 3),
            new("ALL_ATTACK_MOVE", 12, 13, 1, 1, Skill: "MolArms_AttackAll01", Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "MolArms_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "MolArms_Heal", HealRatio: 5000m / 6000m),
        ]),
        new(3, typeof(ShrimpCrab), "shrimp_crab", 120, 126, 128, 134, new("GetStaticDamageReductionPercent", 5m, 2m),
        [
            new("PINCER_MOVE", 26, 30, 1, 1, Skill: "ShrimpCrab_AttackSingle01"),
            new("MIRAGE_PULSE_MOVE", Debuff: "AttackDown+DefenseDown", DebuffRaw: 1, DebuffRaw10: 2,
                Skill: "ShrimpCrab_AttackSingle02", Status: "Imprison"),
            new("STEEL_SHIELD_MOVE", Skill: "ShrimpCrab_DefenseUp", Status: "DefenseUp", Status0: 75, Status10: 75),
            new("HEAL_MOVE", Skill: "ShrimpCrab_Heal", HealRatio: 15000m / 21500m),
        ]),
        new(3, typeof(SlimySerpent), "slimy_serpent", 250, 260, 266, 276, new("GetBonusMaxHp", 12m, 6m),
        [
            new("TAIL_SLAM_MOVE", 7, 8, 2, 2, Skill: "SlimySerpent_AttackAll01_MC04B"),
            new("DESTRUCTION_RUSH_MOVE", 17, 19, 1, 1, Skill: "SlimySerpent_AttackAll02_MC04B", Status: "OdDown",
                Status0: 5, Status10: 10, Spirit: true),
            new("DISEASE_PLANT_MOVE", 15, 17, 1, 1, Debuff: "AttackDown", DebuffRaw: 2, DebuffRaw10: 3,
                Skill: "SlimySerpent_AttackSingle01_Default_MC04B"),
            new("ATTACK_UP_MOVE", Skill: "SlimySerpent_AttackUp02", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "SlimySerpent_Heal", HealRatio: 15000m / 76000m),
        ]),
        new(3, typeof(GiraffeHalo), "giraffe_halo", 240, 250, 256, 266, new("GetStaticDamageBonusPercent", 10m, 4m),
        [
            new("NECK_LASH_MOVE", 13, 15, 1, 1, Skill: "GiraffeHalo_AttackSingle01"),
            new("HALO_SHOTS_MOVE", 7, 8, 2, 2, Skill: "GiraffeHalo_AttackAll01"),
            new("JUDGMENT_RAY_MOVE", 3, 4, 5, 5, Skill: "GiraffeHalo_AttackAll02", Status: "Illusion",
                Status0: 2, Status10: 2, Sure: true),
            new("ATTACK_UP_MOVE", Skill: "GiraffeHalo_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "GiraffeHalo_Heal", HealRatio: 15000m / 7500m),
        ]),
        new(3, typeof(ArachneLine), "arachne_line", 420, 420, 450, 450, new("GetStaticDamageBonusPercent", 14m, 6m),
        [
            new("EIGHT_SLASH_MOVE", 13, 15, 2, 2, Skill: "ArachneLine_AttackSingle01"),
            new("ABSORB_WHIP_MOVE", 16, 18, 2, 2, Skill: "ArachneLine_AttackSingle02", Status: "Absorb"),
            new("PIERCING_FLASH_MOVE", 4, 4, 8, 8, Debuff: "DefenseDown", DebuffRaw: 2, DebuffRaw10: 3,
                Skill: "ArachneLine_AttackAll01", Spirit: true),
            new("ATTACK_UP_MOVE", Skill: "ArachneLine_AttackUp", Status: "AttackUp", Status0: 20, Status10: 20),
            new("HEAL_MOVE", Skill: "ArachneLine_Heal", HealRatio: 15000m / 41000m),
        ]),
        new(3, typeof(PillarGuardian), "pillar_guardian", 460, 460, 490, 490, new("GetExtraMaxDp", 12m, 6m),
        [
            new("PILLAR_STRIKE_MOVE", 8, 9, 2, 2, Skill: "PillarGuardian_AttackSingle01", Status: "ContinuousDp",
                Status0: 4, Status10: 4),
            new("DESTRUCTION_SPEAR_MOVE", 4, 5, 4, 4, Skill: "PillarGuardian_AttackSingle02"),
            new("PARALYSIS_BURST_MOVE", 4, 5, 5, 5, Skill: "PillarGuardian_AttackAll01", Status: "Stun"),
            new("ATTACK_UP_MOVE", Skill: "PillarGuardian_AttackUp", Status: "AttackUp", Status0: 100, Status10: 100),
            new("HEAL_MOVE", Skill: "PillarGuardian_Heal", HealRatio: 15000m / 21000m),
        ]),
    ];

    // The mod's original act-one monsters on their original move tables
    // (fourth batch, 2026-11-08). MoveTableChecks.cs reads them with the 28.
    sealed record TableMonsterSpec(Type Type, MoveSpec[] Moves);

    static readonly TableMonsterSpec[] FourthBatch =
    [
        new(typeof(SmallHopper),
        [
            new("ATTACK_MOVE", 3, 4, 1, 1, Skill: "CrestHopper_DefaultAttack"),
            new("COMBO_MOVE", 1, 1, 8, 8, Skill: "CrestHopper_SkillAttack"),
            new("REPAIR_MOVE", Skill: "CrestHopper_Heal1", HealRatio: 600m / 2400m),
        ]),
        new(typeof(CrestHopper),
        [
            new("ATTACK_MOVE", 6, 8, 1, 1, Skill: "CrestHopper_DefaultAttack"),
            new("COMBO_MOVE", 1, 1, 8, 8, Skill: "CrestHopper_SkillAttack"),
            new("REPAIR_MOVE", Skill: "CrestHopper_Heal1", HealRatio: 600m / 4000m),
        ]),
        new(typeof(HealHopper),
        [
            new("ATTACK_MOVE", 3, 4, 1, 1, Skill: "CrestHopper_DefaultAttack"),
            new("COMBO_MOVE", 1, 1, 8, 8, Skill: "CrestHopper_SkillAttack"),
            new("REPAIR_BETA_MOVE", Skill: "CrestHopper_HealAll1", HealRatio: 400m / 4000m),
            new("REPAIR_OMEGA_MOVE", Skill: "CrestHopper_HealAll2", HealRatio: 10000m / 2000m),
        ]),
        new(typeof(GoldHopper),
        [
            new("ATTACK_MOVE", 7, 8, 1, 1, Skill: "CrestHopperDefaultGold_DefaultAttack"),
            new("ESCAPE_MOVE", Skill: "CrestHopperDefaultGold_Escape", Status: "Escape"),
        ]),
        new(typeof(Knocker),
        [
            new("ATTACK_MOVE", 5, 6, 1, 1, Skill: "DarkKnocker_Attack01"),
            new("MULTI_ATTACK_MOVE", 3, 3, 2, 2, Skill: "DarkKnocker_Attack02"),
            new("ALL_ATTACK_MOVE", 7, 8, 1, 1, Skill: "DarkKnocker_AttackAll01"),
        ]),
        new(typeof(ClayKnocker),
        [
            new("ATTACK_MOVE", 6, 7, 1, 1, Skill: "DarkKnocker_Attack01"),
            new("MULTI_ATTACK_MOVE", 3, 4, 2, 2, Skill: "DarkKnocker_Attack02"),
            new("ALL_ATTACK_MOVE", 8, 9, 1, 1, Skill: "DarkKnocker_AttackAll01"),
        ]),
        new(typeof(AbyssKnocker),
        [
            new("ATTACK_MOVE", 7, 8, 1, 1, Skill: "AbyssKnocker_Attack01"),
            new("MULTI_ATTACK_MOVE", 4, 5, 2, 2, Skill: "AbyssKnocker_Attack02"),
            new("RAGE_WAVE_MOVE", 8, 9, 1, 1, Skill: "AbyssKnocker_AttackAll01", Spirit: true),
            new("RECOVERY_MOVE", Skill: "AbyssKnocker_Buff01", HealRatio: 3000m / 44200m),
        ]),
    ];

    // Every move-table monster: the 28 new ones and the fourth batch.
    static IEnumerable<(Type Type, MoveSpec[] Moves)> TableSpecs =>
        Monsters.Select(m => (m.Type, m.Moves)).Concat(FourthBatch.Select(m => (m.Type, m.Moves)));

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
