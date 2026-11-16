using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Compatibility;
using MegaCrit.Sts2.Core.Saves.Runs;
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

// HBR 磐石异蟹, an elite pair: originals ShrimpCrabDefault_01 and _02, which
// fight together with different tables (MC04ExpKirigamine02_01_BOSS01). Few
// attack turns, so the single strike is heavy (capped at 1.25x the 11-05 high).
// Mirage Pulse seals and weakens; Steel Shield cuts damage taken by 75%.
public sealed class ShrimpCrab : HbrgoMoveTableMonster, IHbrAfterDeathListener
{
    internal const int FirstVariant = 0;
    internal const int SecondVariant = 1;

    internal const string PincerSkill = "ShrimpCrab_AttackSingle01";
    internal const string MiragePulseSkill = "ShrimpCrab_AttackSingle02";
    internal const string SteelShieldSkill = "ShrimpCrab_DefenseUp";
    internal const string HealSkill = "ShrimpCrab_Heal";

    internal const string PincerMoveId = "PINCER_MOVE";
    internal const string MiragePulseMoveId = "MIRAGE_PULSE_MOVE";
    internal const string SteelShieldMoveId = "STEEL_SHIELD_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int SteelShieldPercent = 75;

    // Original HealDp 15000 over the original maximum DP 21500.
    internal const decimal HealDpRatio = 15000m / 21500m;

    internal static readonly HbrgoMoveTables FirstTables = new(
        "ShrimpCrabDefault_01",
        Table("ShrimpCrabDefault_01",
            Step(PincerSkill),
            Step(MiragePulseSkill),
            Step(SteelShieldSkill),
            Step(PincerSkill),
            Step(SteelShieldSkill)),
        Table("ShrimpCrabDefault_01_break",
            Step(PincerSkill),
            Step(SteelShieldSkill),
            Step(PincerSkill),
            Step(MiragePulseSkill),
            Step(HealSkill)));

    internal static readonly HbrgoMoveTables SecondTables = new(
        "ShrimpCrabDefault_02",
        Table("ShrimpCrabDefault_02",
            Step(MiragePulseSkill),
            Pick((PincerSkill, 50), (SteelShieldSkill, 50)),
            Pick((PincerSkill, 50), (MiragePulseSkill, 50))),
        Table("ShrimpCrabDefault_02_break",
            Step(MiragePulseSkill),
            Pick((PincerSkill, 50), (SteelShieldSkill, 50)),
            Pick((PincerSkill, 50), (SteelShieldSkill, 50)),
            Step(MiragePulseSkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(PincerSkill, PincerMoveId, HbrgoMoveTarget.Single),
        new(MiragePulseSkill, MiragePulseMoveId, HbrgoMoveTarget.Single),
        new(SteelShieldSkill, SteelShieldMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            128,
            120);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            134,
            126);

    private int PincerDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            30,
            26);

    // Original AttackDown/DefenseDown 10% (20% on hard) for Mirage Pulse.
    private int MiragePulseDebuff =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            1);

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == SecondVariant ? SecondTables : FirstTables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            PincerMoveId,
            PincerMove,
            new SingleAttackIntent(() => PincerDamage));
        yield return new MoveState(
            MiragePulseMoveId,
            MiragePulseMove,
            new DebuffIntent(strong: true));
        yield return new MoveState(
            SteelShieldMoveId,
            SteelShieldMove,
            new DefendIntent());
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task PincerMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PincerDamage);
    }

    // No damage, so its seal attaches as is (proposal M8).
    private async Task MiragePulseMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature[] hit = HbrEnemyEffects.SingleTargets(Creature, targets).ToArray();
        await HbrEnemyEffects.ApplyImprison(hit, Creature);
        await HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            hit.Where(target => target.IsAlive).ToArray(),
            MiragePulseDebuff,
            Creature);
        await HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            hit.Where(target => target.IsAlive).ToArray(),
            MiragePulseDebuff,
            Creature);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool RunawayTriggered { get; private set; }
    public async Task AfterDeath(PlayerChoiceContext context, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || RunawayTriggered || !Creature.IsAlive || target == Creature ||
            target?.Monster is not ShrimpCrab || target.Side != Creature.Side ||
            !EnemyStatScalingHelper.IsEnemy(Creature) || !EnemyStatScalingHelper.IsEnemy(target) ||
            !HbrBattleStatsHelper.IsEnabled(Creature)) return;
        RunawayTriggered = true;
        await HbrEffectApplication.Apply<ShrimpRunawayPower>(context, Creature, 100m, Creature,
            profile: new(HbrEffectLimit.Only, HbrEffectExit.Eternal, 1));
    }

    private async Task SteelShieldMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await HbrEnemyEffects.ApplyTimedBuff<HbrDefenseUpPower>(Creature, SteelShieldPercent);
        if (EnemyStatScalingHelper.IsEnemy(Creature) && HbrBattleStatsHelper.IsEnabled(Creature))
            await HbrEffectApplication.Apply<HbrProvokePower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(HealDpRatio);
    }
}
