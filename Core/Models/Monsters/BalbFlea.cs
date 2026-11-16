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

// HBR 灯胆跳虫, act three: original BalbFleaDefault (Mt. Kumotori and
// Kirigamine fights). Attack Up is always followed by the all-unit flash.
// The 11-05 flash's attack down is not in the original
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class BalbFlea : HbrgoMoveTableMonster
{
    internal const string RamSkill = "BalbFlea_AttackSingle01";
    internal const string FlashSkill = "BalbFlea_AttackAll01";
    internal const string AttackUpSkill = "BalbFlea_AttackUp";
    internal const string HealSkill = "BalbFlea_Heal";

    internal const string RamMoveId = "RAM_MOVE";
    internal const string FlashMoveId = "FLASH_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;

    // Original HealDp 5000 over the original maximum DP 3500: a full heal.
    internal const decimal HealDpRatio = 5000m / 3500m;

    private static HbrgoMoveStep RamOrPowerUp() =>
        Pick((RamSkill, 50), (AttackUpSkill, 50));

    internal static readonly HbrgoMoveTables Tables = new(
        "BalbFleaDefault",
        Table("BalbFleaDefault",
            Step(RamSkill),
            Step(FlashSkill),
            RamOrPowerUp(),
            Step(FlashSkill),
            RamOrPowerUp(),
            Step(FlashSkill)),
        Table("BalbFleaDefault_break",
            RamOrPowerUp(),
            Step(FlashSkill),
            RamOrPowerUp(),
            Step(FlashSkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(RamSkill, RamMoveId, HbrgoMoveTarget.Single),
        new(FlashSkill, FlashMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            56,
            52);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            60,
            56);

    private int RamDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    private int FlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            13);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            RamMoveId,
            RamMove,
            new SingleAttackIntent(() => RamDamage));
        yield return new MoveState(
            FlashMoveId,
            FlashMove,
            new SingleAttackIntent(() => FlashDamage));
        yield return new MoveState(
            AttackUpMoveId,
            AttackUpMove,
            new BuffIntent());
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task RamMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(RamDamage);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task FlashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(FlashDamage, 1, HbrgoAttackTargets.AllUnits);
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
