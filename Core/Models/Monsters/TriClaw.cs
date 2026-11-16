using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 三爪镖, act three. Most fights copy 恶之回旋镖 TriClawSmall (Mt. Kumotori
// and Kirigamine); 巨肌魔臂与三爪镖 (MolArmsAndTriClawNormal,
// MC04BExpDay03Desert01_R05) sets 三角镖 TriClawDefault, whose attacks are the
// _MC04B skills. Both variants share the mod's moves. The 11-05 return hit
// came from the name only and is gone (docs/hbr-monster-mechanics-proposal.md).
public sealed class TriClaw : HbrgoMoveTableMonster
{
    internal const int SmallVariant = 0;
    internal const int DefaultVariant = 1;

    internal const string DartSkill = "TriClaw_AttackSingle01";
    internal const string ClawsSkill = "TriClaw_AttackSingle02";
    internal const string DefaultDartSkill = "TriClaw_AttackSingle01_MC04B";
    internal const string DefaultClawsSkill = "TriClaw_AttackSingle02_MC04B";
    internal const string AttackUpSkill = "TriClaw_AttackUp";
    internal const string HealSkill = "TriClaw_Heal";

    internal const string DartMoveId = "DART_MOVE";
    internal const string ClawsMoveId = "CLAWS_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int ClawsHits = 5;

    // Original HealDp 5000 over each variant's original maximum DP; 三角镖's
    // is a full heal.
    internal const decimal SmallHealDpRatio = 5000m / 6400m;
    internal const decimal DefaultHealDpRatio = 5000m / 3200m;

    internal static readonly HbrgoMoveTables SmallTables = new(
        "TriClawSmall",
        Table("TriClawSmall",
            Pick((DartSkill, 50), (AttackUpSkill, 50)),
            Pick((DartSkill, 50), (ClawsSkill, 50))),
        Table("TriClawSmall_break",
            Pick((DartSkill, 50), (AttackUpSkill, 50)),
            Pick((DartSkill, 50), (ClawsSkill, 50)),
            Step(HealSkill)));

    internal static readonly HbrgoMoveTables DefaultTables = new(
        "TriClawDefault",
        Table("TriClawDefault",
            PickOnce((AttackUpSkill, 50), (DefaultClawsSkill, 50)),
            PickOnce((DefaultDartSkill, 50), (DefaultClawsSkill, 50)),
            OnceStep(AttackUpSkill),
            PickOnce((DefaultDartSkill, 50), (DefaultClawsSkill, 50)),
            Step(AttackUpSkill),
            Pick((DefaultDartSkill, 30), (DefaultClawsSkill, 70)),
            Step(DefaultClawsSkill),
            Step(AttackUpSkill),
            Step(DefaultClawsSkill)),
        Table("TriClawDefault_break",
            Pick((AttackUpSkill, 50), (DefaultClawsSkill, 50)),
            Pick((DefaultDartSkill, 40), (DefaultClawsSkill, 60)),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(DartSkill, DartMoveId, HbrgoMoveTarget.Single),
        new(ClawsSkill, ClawsMoveId, HbrgoMoveTarget.Single, SpiritDefense: true),
        new(DefaultDartSkill, DartMoveId, HbrgoMoveTarget.Single),
        new(DefaultClawsSkill, ClawsMoveId, HbrgoMoveTarget.Single, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            62,
            58);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            66,
            62);

    private int DartDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            15);

    // 恶之回旋镖 4 (5 on hard) per hit; 三角镖 4 on both.
    private int ClawsDamage =>
        MoveTableVariant == DefaultVariant
            ? 4
            : AscensionHelper.GetValueIfAscension(
                AscensionLevel.DeadlyEnemies,
                5,
                4);

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == DefaultVariant ? DefaultTables : SmallTables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    private decimal HealDpRatio =>
        MoveTableVariant == DefaultVariant ? DefaultHealDpRatio : SmallHealDpRatio;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            DartMoveId,
            DartMove,
            new SingleAttackIntent(() => DartDamage));
        yield return new MoveState(
            ClawsMoveId,
            ClawsMove,
            new MultiAttackIntent(ClawsDamage, () => ClawsHits));
        yield return new MoveState(
            AttackUpMoveId,
            AttackUpMove,
            new BuffIntent());
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task DartMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(DartDamage);
    }

    private Task ClawsMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ClawsDamage, ClawsHits);
    }

    private async Task AttackUpMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await HbrEnemyEffects.ApplyTimedBuff<HbrAttackUpPower>(Creature, AttackUpPercent);
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(HealDpRatio);
    }
}
