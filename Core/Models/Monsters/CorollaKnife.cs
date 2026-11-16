using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 花冠利刃, act three: original CorollaKnifeSmall_01; when two knives fight
// together (CorollaKnivesNormal, BigotryGateAndCorollaKnivesNormal) the second
// is CorollaKnifeSmall_02, set by the encounter. Neither has an hp table or a
// heal, so Break keeps walking the dp table
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class CorollaKnife : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const int FirstVariant = 0;
    internal const int SecondVariant = 1;

    internal const string SlashSkill = "CorollaKnife_AttackSingle01";
    internal const string RotationCutSkill = "CorollaKnife_AttackSingle02";
    internal const string RampageSliceSkill = "CorollaKnife_AttackAll01";
    internal const string AttackUpSkill = "CorollaKnife_AttackUp";

    internal const string SlashMoveId = "SLASH_MOVE";
    internal const string RotationCutMoveId = "ROTATION_CUT_MOVE";
    internal const string RampageSliceMoveId = "RAMPAGE_SLICE_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int RampageSliceHits = 2;

    private static HbrgoMoveStep EitherSingle(int slash, int cut) =>
        Pick((SlashSkill, slash), (RotationCutSkill, cut));

    internal static readonly HbrgoMoveTables FirstTables = new(
        "CorollaKnifeSmall_01",
        Table("CorollaKnifeSmall_01",
            EitherSingle(70, 30),
            EitherSingle(60, 40),
            EitherSingle(30, 70),
            Step(AttackUpSkill),
            Step(RampageSliceSkill)),
        HbrgoMoveTable.None);

    internal static readonly HbrgoMoveTables SecondTables = new(
        "CorollaKnifeSmall_02",
        Table("CorollaKnifeSmall_02",
            Step(AttackUpSkill),
            Step(RampageSliceSkill),
            EitherSingle(70, 30),
            EitherSingle(60, 40),
            EitherSingle(30, 70)),
        HbrgoMoveTable.None);

    private static readonly HbrgoTableMove[] Moves =
    {
        new(SlashSkill, SlashMoveId, HbrgoMoveTarget.Single),
        new(RotationCutSkill, RotationCutMoveId, HbrgoMoveTarget.Single),
        new(RampageSliceSkill, RampageSliceMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            83,
            78);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            87,
            82);

    private int SlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11,
            10);

    private int RotationCutDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            13);

    // Original DefenseDown 20% (30% on hard): 2 modifier layers, 3 on hard.
    private int RotationCutDefenseDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    private int RampageSliceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == SecondVariant ? SecondTables : FirstTables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            SlashMoveId,
            SlashMove,
            new SingleAttackIntent(() => SlashDamage));
        yield return new MoveState(
            RotationCutMoveId,
            RotationCutMove,
            new SingleAttackIntent(() => RotationCutDamage),
            new DebuffIntent());
        yield return new MoveState(
            RampageSliceMoveId,
            RampageSliceMove,
            new MultiAttackIntent(RampageSliceDamage, () => RampageSliceHits));
        yield return new MoveState(
            AttackUpMoveId,
            AttackUpMove,
            new BuffIntent());
    }

    private Task SlashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(SlashDamage);
    }

    private async Task RotationCutMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(RotationCutDamage);
        await ApplyRotationCut(targets);
    }

    private Task ApplyRotationCut(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            RotationCutDefenseDown,
            Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task RampageSliceMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(RampageSliceDamage, RampageSliceHits, HbrgoAttackTargets.AllUnits);
    }

    private async Task AttackUpMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await HbrEnemyEffects.ApplyTimedBuff<HbrAttackUpPower>(Creature, AttackUpPercent);
    }

    Task IHbrgoPhantomAttackResults.AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage)
    {
        return move?.Id == RotationCutMoveId
            ? ApplyRotationCut(attackedTargets)
            : Task.CompletedTask;
    }
}
