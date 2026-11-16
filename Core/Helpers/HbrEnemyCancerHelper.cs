using System;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Identity of the current HBR enemy roster, independent of combat stats or appearance.</summary>
public static class HbrEnemyCancerHelper
{
    // Exact model identities are intentional: humanoid encounters and friendly
    // summons also use MonsterModel. In particular RedCrimson is our summon,
    // whereas RedCrimsonBossMonster is the third-act enemy.
    private static readonly HashSet<Type> CancerTypes = new()
    {
        typeof(AbyssKnocker),
        typeof(AbyssKnockerKiller),
        typeof(ArachneLine),
        typeof(BalbFlea),
        typeof(BarrelWalker),
        typeof(BigotryGate),
        typeof(CatHorn),
        typeof(CatHornKiller),
        typeof(ClayKnocker),
        typeof(CorollaKnife),
        typeof(CrestHopper),
        typeof(CrestHopperKiller),
        typeof(CrowHorse),
        typeof(DarkKnockerKiller),
        typeof(DeathSlug),
        typeof(DiamondEyeball),
        typeof(FlatHand),
        typeof(GiraffeHalo),
        typeof(GoldHopper),
        typeof(HealHopper),
        typeof(IvoryScooper),
        typeof(Knocker),
        typeof(Marionette),
        typeof(MarionetteKiller),
        typeof(MolArms),
        typeof(NeedleBird),
        typeof(OctopusTail),
        typeof(OctopusTailKiller),
        typeof(PileGolemEdge),
        typeof(PillarGuardian),
        typeof(RedCrimsonBossMonster),
        typeof(ShrimpCrab),
        typeof(SkullFeather),
        typeof(SlimySerpent),
        typeof(SmallHopper),
        typeof(TargetWireFrame),
        typeof(TargetWireFrameKiller),
        typeof(TriClaw),
        typeof(WireSlasher)
    };

    public static bool IsCancer(Creature creature) =>
        creature?.Monster != null &&
        creature.Side == CombatSide.Enemy &&
        creature.Player == null &&
        creature.PetOwner == null &&
        CancerTypes.Contains(creature.Monster.GetType());
}
