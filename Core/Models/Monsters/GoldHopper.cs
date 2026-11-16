using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 黄金跳虫, act one: original CrestHopperDefaultGold01 (α; the β
// CrestHopperDefaultGold02 has the same table). It escapes on the original's
// timing: 60% on each of its first two turns, surely on the third; no hp
// table, so Break resumes the interrupted move. Killing it still adds the
// extra gold. As an enemy it has the original's absolute HP 1 and DP 10 (no
// scaling of any kind) and 99 native Intangible, so every hit deals 1: ten
// hits empty the DP (the stun delays the escape) and the eleventh kills it
// (docs/hbr-act-rosters.md, fourth batch). On the player side (a 30A phantom)
// it never escapes, has no Intangible and keeps the phantom HP/DP rules,
// which read the native HP range below.
public sealed class GoldHopper : HbrgoMoveTableMonster, IHbrgoFixedEnemyResources, IHbrAfterDeathListener
{
    internal const string AttackSkill = "CrestHopperDefaultGold_DefaultAttack";
    internal const string EscapeSkill = "CrestHopperDefaultGold_Escape";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string EscapeMoveId = "ESCAPE_MOVE";

    // CrestHopperDefaultGold01/02: HP 1, DP 10.
    internal const int OriginalHp = 1;
    internal const decimal OriginalDp = 10m;
    internal const int StartingIntangible = 99;

    internal static readonly HbrgoMoveTables Tables = new(
        "CrestHopperDefaultGold01",
        Table("CrestHopperDefaultGold",
            Pick((AttackSkill, 40), (EscapeSkill, 60)),
            Pick((AttackSkill, 40), (EscapeSkill, 60)),
            Step(EscapeSkill)),
        HbrgoMoveTable.None);

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(EscapeSkill, EscapeMoveId, HbrgoMoveTarget.Self),
    };

    private bool _useBetaVisuals;
    private bool _bonusRewardGranted;
    private bool _hasAppliedStartingIntangible;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            56,
            54);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            60,
            58);

    int IHbrgoFixedEnemyResources.FixedMaxHp => OriginalHp;

    decimal IHbrgoFixedEnemyResources.FixedMaxDp => OriginalDp;

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath(
            UseBetaVisuals
                ? "creature_visuals/gold_hopper_beta"
                : "creature_visuals/gold_hopper_alpha");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool UseBetaVisuals
    {
        get => _useBetaVisuals;
        private set
        {
            AssertMutable();
            _useBetaVisuals = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BonusRewardGranted
    {
        get => _bonusRewardGranted;
        private set
        {
            AssertMutable();
            _bonusRewardGranted = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasAppliedStartingIntangible
    {
        get => _hasAppliedStartingIntangible;
        private set
        {
            AssertMutable();
            _hasAppliedStartingIntangible = value;
        }
    }

    public void ConfigureVisuals(bool useBetaVisuals)
    {
        UseBetaVisuals = useBetaVisuals;
    }

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    // A phantom fights for its summoner: the escape rows are skipped, so it
    // attacks on steps 1-2 and step 3 rolls on to step 1.
    private protected override bool IsOptionAvailable(HbrgoMoveOption option) =>
        option.Skill != EscapeSkill || Creature?.Side != CombatSide.Player;

    // Once, as an enemy: a phantom (player side) gets none.
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (HasAppliedStartingIntangible ||
            !EnemyStatScalingHelper.IsEnemy(Creature))
        {
            return;
        }

        HasAppliedStartingIntangible = true;
        if (!Creature.HasPower<IntangiblePower>())
        {
            await PowerCmd.Apply<IntangiblePower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                StartingIntangible,
                Creature,
                null);
        }
    }

    public Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature ||
            wasRemovalPrevented ||
            BonusRewardGranted ||
            CombatState.RunState.CurrentRoom is not CombatRoom combatRoom)
        {
            return Task.CompletedTask;
        }

        BonusRewardGranted = true;
        foreach (var player in CombatState.Players)
        {
            combatRoom.AddExtraReward(
                player,
                new GoldReward(20, 25, player));
        }

        return Task.CompletedTask;
    }

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            AttackMoveId,
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        yield return new MoveState(
            EscapeMoveId,
            EscapeMove,
            new EscapeIntent());
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private async Task EscapeMove(IReadOnlyList<Creature> _)
    {
        NCombatRoom.Instance?
            .GetCreatureNode(Creature)?
            .ToggleIsInteractableCompat(false);
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0.15f);
        await CreatureCmd.Escape(Creature);
    }
}
