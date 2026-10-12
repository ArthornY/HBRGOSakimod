using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class FlatHand :
    HbrgoStaticMonster,
    IHbrgoRevivingStaticMonster
{
    private MoveState _reviveState;
    private MoveState _deathSpearState;
    private int _respawns;
    private int _retainedStrength;
    private int _shieldLockStrengthReduction;
    private bool _shieldLockInitialized;
    private bool _unlockedNararaShibaThisRun;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            1200,
            1000);

    public override int MaxInitialHp => MinInitialHp;

    public bool HasRemainingRevives => Respawns < 2;

    public bool ShouldSuppressDeathFade => HasRemainingRevives;

    private int HpPerRevive =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            500,
            400);

    private int DestructiveRayDamage => DeadlyValue(20, 17);

    private int AngerStabDamage => DeadlyValue(14, 12);

    private int AttackAllDamage => DeadlyValue(24, 20);

    private int PourDeathDamage => DeadlyValue(8, 7);

    private int ThornSpearHitCount => DeadlyValue(6, 5);

    private int InvitationDeathDamage => DeadlyValue(60, 50);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath("creature_visuals/flat_hand");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Respawns
    {
        get => _respawns;
        private set
        {
            AssertMutable();
            _respawns = Math.Clamp(value, 0, 2);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RetainedStrength
    {
        get => _retainedStrength;
        private set
        {
            AssertMutable();
            _retainedStrength = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ShieldLockStrengthReduction
    {
        get => _shieldLockStrengthReduction;
        private set
        {
            AssertMutable();
            _shieldLockStrengthReduction = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ShieldLockInitialized
    {
        get => _shieldLockInitialized;
        private set
        {
            AssertMutable();
            _shieldLockInitialized = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool UnlockedNararaShibaThisRun
    {
        get => _unlockedNararaShibaThisRun;
        internal set
        {
            AssertMutable();
            _unlockedNararaShibaThisRun = value;
        }
    }

    private MoveState ReviveState
    {
        get => _reviveState;
        set
        {
            AssertMutable();
            _reviveState = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        Creature.Died -= OnDied;
        Creature.Died += OnDied;
        if (HasRemainingRevives &&
            !Creature.HasPower<FlatHandRevivalPower>())
        {
            await PowerCmd.Apply<FlatHandRevivalPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null);
        }

        await InitializeShieldLock();
    }

    public override void BeforeRemovedFromRoom()
    {
        Creature.Died -= OnDied;
        base.BeforeRemovedFromRoom();
    }

    private void OnDied(Creature creature)
    {
        if (creature == Creature &&
            Respawns >= 2 &&
            Creature.PetOwner == null)
        {
            HbrgoSakiVariantPreference.UnlockSkullFeather();
            var localCharacter =
                (LocalContext.GetMe(CombatState.RunState.Players) ??
                    (CombatState.RunState.Players.Count == 1 ? CombatState.RunState.Players[0] : null))?.Character;
            if (localCharacter is HbrgoSaki31O)
            {
                HbrgoSakiVariantPreference.UnlockThirtyA();
            }

            if (localCharacter is HbrgoSaki30A)
            {
                bool wasAlreadyUnlocked =
                    HbrgoSakiVariantPreference.IsNararaShibaUnlocked();
                HbrgoSakiVariantPreference.UnlockNararaShiba();
                if (!wasAlreadyUnlocked &&
                    HbrgoSakiVariantPreference.IsNararaShibaUnlocked())
                {
                    UnlockedNararaShibaThisRun = true;
                }
            }
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        MoveState phaseOneAngerStab = Move(
            "ANGER_STAB_MOVE",
            AngerStabMove,
            new MultiAttackIntent(AngerStabDamage, 3));
        MoveState phaseOneRay = Move(
            "DESTRUCTIVE_RAY_MOVE",
            DestructiveRayMove,
            new SingleAttackIntent(DestructiveRayDamage));
        var phaseOneRandom = new RandomBranchState("PHASE_ONE_RANDOM");
        MoveState phaseOneRandomRay = Move(
            "P1_RANDOM_DESTRUCTIVE_RAY_MOVE",
            DestructiveRayMove,
            new SingleAttackIntent(DestructiveRayDamage));
        MoveState phaseOneRandomStab = Move(
            "P1_RANDOM_ANGER_STAB_MOVE",
            AngerStabMove,
            new MultiAttackIntent(AngerStabDamage, 3));
        MoveState phaseOneFinalRay = Move(
            "P1_FINAL_DESTRUCTIVE_RAY_MOVE",
            DestructiveRayMove,
            new SingleAttackIntent(DestructiveRayDamage));
        MoveState phaseOneHeal = Move(
            "P1_HEAL_MOVE",
            HealMove,
            new DefendIntent(),
            new HealIntent());

        phaseOneAngerStab.FollowUpState = phaseOneRay;
        phaseOneRay.FollowUpState = phaseOneRandom;
        phaseOneRandom.AddBranch(
            phaseOneRandomRay,
            MoveRepeatType.CanRepeatForever,
            0.7f);
        phaseOneRandom.AddBranch(
            phaseOneRandomStab,
            MoveRepeatType.CanRepeatForever,
            0.3f);
        phaseOneRandomRay.FollowUpState = phaseOneFinalRay;
        phaseOneRandomStab.FollowUpState = phaseOneFinalRay;
        phaseOneFinalRay.FollowUpState = phaseOneHeal;
        phaseOneHeal.FollowUpState = phaseOneAngerStab;

        MoveState phaseTwoPourDeath = Move(
            "POUR_DEATH_MOVE",
            PourDeathMove,
            new MultiAttackIntent(PourDeathDamage, 10),
            new DebuffIntent());
        MoveState phaseTwoRay = Move(
            "P2_FIRST_DESTRUCTIVE_RAY_MOVE",
            DestructiveRayMove,
            new SingleAttackIntent(DestructiveRayDamage));
        MoveState phaseTwoHeal = Move(
            "P2_FIRST_HEAL_MOVE",
            HealMove,
            new DefendIntent(),
            new HealIntent());
        var phaseTwoRandom = new RandomBranchState("PHASE_TWO_RANDOM");
        MoveState phaseTwoRandomAttackAll = Move(
            "P2_RANDOM_ATTACK_ALL_MOVE",
            AttackAllMove,
            new MultiAttackIntent(AttackAllDamage, 2));
        MoveState phaseTwoRandomPourDeath = Move(
            "P2_RANDOM_POUR_DEATH_MOVE",
            PourDeathMove,
            new MultiAttackIntent(PourDeathDamage, 10),
            new DebuffIntent());
        MoveState phaseTwoSecondRay = Move(
            "P2_SECOND_DESTRUCTIVE_RAY_MOVE",
            DestructiveRayMove,
            new SingleAttackIntent(DestructiveRayDamage));
        MoveState phaseTwoSecondHeal = Move(
            "P2_SECOND_HEAL_MOVE",
            HealMove,
            new DefendIntent(),
            new HealIntent());
        MoveState phaseTwoFinalAttackAll = Move(
            "P2_FINAL_ATTACK_ALL_MOVE",
            AttackAllMove,
            new MultiAttackIntent(AttackAllDamage, 2));

        phaseTwoPourDeath.FollowUpState = phaseTwoRay;
        phaseTwoRay.FollowUpState = phaseTwoHeal;
        phaseTwoHeal.FollowUpState = phaseTwoRandom;
        phaseTwoRandom.AddBranch(
            phaseTwoRandomAttackAll,
            MoveRepeatType.CanRepeatForever,
            0.7f);
        phaseTwoRandom.AddBranch(
            phaseTwoRandomPourDeath,
            MoveRepeatType.CanRepeatForever,
            0.3f);
        phaseTwoRandomAttackAll.FollowUpState = phaseTwoSecondRay;
        phaseTwoRandomPourDeath.FollowUpState = phaseTwoSecondRay;
        phaseTwoSecondRay.FollowUpState = phaseTwoSecondHeal;
        phaseTwoSecondHeal.FollowUpState = phaseTwoFinalAttackAll;
        phaseTwoFinalAttackAll.FollowUpState = phaseTwoPourDeath;

        MoveState phaseThreeCharge = Move(
            "CHARGE_MOVE",
            ChargeMove,
            new BuffIntent());
        MoveState phaseThreeInvitationDeath = Move(
            "INVITATION_DEATH_MOVE",
            InvitationDeathMove,
            new SingleAttackIntent(InvitationDeathDamage),
            new DebuffIntent(),
            new StatusIntent(3));
        MoveState phaseThreeShieldLock = Move(
            "SHIELD_LOCK_BROKEN_MOVE",
            ShieldLockBrokenMove,
            new BuffIntent());
        MoveState phaseThreeAttackAll = Move(
            "ATTACK_ALL_MOVE",
            AttackAllMove,
            new MultiAttackIntent(AttackAllDamage, 2));
        MoveState phaseThreeThornSpear = Move(
            "THORN_SPEAR_MOVE",
            ThornSpearMove,
            new MultiAttackIntent(12, ThornSpearHitCount),
            new DebuffIntent());
        MoveState phaseThreeHeal = Move(
            "P3_HEAL_MOVE",
            HealMove,
            new DefendIntent(),
            new HealIntent());
        MoveState phaseThreeSecondThornSpear = Move(
            "P3_SECOND_THORN_SPEAR_MOVE",
            ThornSpearMove,
            new MultiAttackIntent(12, ThornSpearHitCount),
            new DebuffIntent());
        MoveState phaseThreeDeathSpear = Move(
            "DEATH_SPEAR_MOVE",
            DeathSpearMove,
            new MultiAttackIntent(999, 2),
            new DebuffIntent());
        _deathSpearState = phaseThreeDeathSpear;

        phaseThreeCharge.FollowUpState = phaseThreeInvitationDeath;
        phaseThreeInvitationDeath.FollowUpState = phaseThreeShieldLock;
        phaseThreeShieldLock.FollowUpState = phaseThreeAttackAll;
        phaseThreeAttackAll.FollowUpState = phaseThreeThornSpear;
        phaseThreeThornSpear.FollowUpState = phaseThreeHeal;
        phaseThreeHeal.FollowUpState = phaseThreeSecondThornSpear;
        phaseThreeSecondThornSpear.FollowUpState = phaseThreeCharge;
        phaseThreeDeathSpear.FollowUpState = phaseThreeCharge;

        ReviveState = Move(
            "RESPAWN_MOVE",
            RespawnMove,
            new HealIntent(),
            new BuffIntent());
        ReviveState.MustPerformOnceBeforeTransitioning = true;
        var reviveBranch = new ConditionalBranchState("REVIVE_BRANCH");
        ReviveState.FollowUpState = reviveBranch;
        reviveBranch.AddState(
            phaseTwoPourDeath,
            () => Respawns == 1);
        reviveBranch.AddState(
            phaseThreeCharge,
            () => Respawns >= 2);

        states.AddRange(new MonsterState[]
        {
            phaseOneAngerStab,
            phaseOneRay,
            phaseOneRandom,
            phaseOneRandomRay,
            phaseOneRandomStab,
            phaseOneFinalRay,
            phaseOneHeal,
            phaseTwoPourDeath,
            phaseTwoRay,
            phaseTwoHeal,
            phaseTwoRandom,
            phaseTwoRandomAttackAll,
            phaseTwoRandomPourDeath,
            phaseTwoSecondRay,
            phaseTwoSecondHeal,
            phaseTwoFinalAttackAll,
            phaseThreeCharge,
            phaseThreeInvitationDeath,
            phaseThreeShieldLock,
            phaseThreeAttackAll,
            phaseThreeThornSpear,
            phaseThreeHeal,
            phaseThreeSecondThornSpear,
            phaseThreeDeathSpear,
            ReviveState,
            reviveBranch
        });
        return new MonsterMoveStateMachine(states, phaseOneAngerStab);
    }

    public async Task TriggerReviveState()
    {
        await CreatureCmd.TriggerAnim(Creature, "Dead", 0f);
        SetMoveImmediate(ReviveState, forceTransition: true);
    }

    internal void BeginRevive(int retainedStrength)
    {
        RetainedStrength =
            retainedStrength + ShieldLockStrengthReduction;
    }

    internal void ForceDeathSpearMove()
    {
        if (!Creature.IsAlive || Respawns < 2 || _deathSpearState == null)
        {
            return;
        }

        SetMoveImmediate(_deathSpearState, forceTransition: true);
    }

    private static MoveState Move(
        string id,
        Func<IReadOnlyList<Creature>, Task> action,
        params AbstractIntent[] intents)
    {
        return new MoveState(id, action, intents);
    }

    private int DeadlyValue(int ascended, int normal)
    {
        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            ascended,
            normal);
    }

    private Task DestructiveRayMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(DestructiveRayDamage);
    }

    private Task AngerStabMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AngerStabDamage, 3);
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        BreakPower breakPower = Creature.GetPower<BreakPower>();
        if (breakPower != null)
        {
            await PowerCmd.Remove(breakPower);
            return;
        }

        await CreatureCmd.GainBlock(
            Creature,
            30m,
            ValueProp.Move,
            null);
        Creature.GetPower<HighDimensionalShellPower>()?.Restore(40);
    }

    internal async Task BeforePhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        if (move?.Id != "DEATH_SPEAR_MOVE")
        {
            return;
        }

        foreach (Creature target in GetPhantomMoveTargets(targets))
        {
            IntangiblePower intangible = target.GetPower<IntangiblePower>();
            if (intangible != null)
            {
                await PowerCmd.Remove(intangible);
            }

            BufferPower buffer = target.GetPower<BufferPower>();
            if (buffer != null)
            {
                await PowerCmd.Remove(buffer);
            }
        }
    }

    internal Task AfterPhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        Creature[] opponents = GetPhantomMoveTargets(targets);
        var context = new ThrowingPlayerChoiceContext();
        if (move?.Id == "INVITATION_DEATH_MOVE" && opponents.Length > 0)
        {
            PhantomPlayerBenefitContext.QueueDraw(3);
        }
        return move?.Id switch
        {
            "POUR_DEATH_MOVE" or "P2_RANDOM_POUR_DEATH_MOVE" =>
                PowerCmd.Apply<FrailPower>(context, opponents, 2m, Creature, null),
            "INVITATION_DEATH_MOVE" =>
                PowerCmd.Apply<StrengthPower>(context, opponents, -3m, Creature, null),
            "THORN_SPEAR_MOVE" or "P3_SECOND_THORN_SPEAR_MOVE" =>
                PowerCmd.Apply<DexterityPower>(context, opponents, -3m, Creature, null),
            _ => Task.CompletedTask
        };
    }

    private Creature[] GetPhantomMoveTargets(IReadOnlyList<Creature> targets)
    {
        return targets?.Where(target =>
            CombatState != null &&
            PhantomHelper.IsValidPhantomTarget(target) &&
            target.Side != Creature.Side &&
            ReferenceEquals(target.CombatState, CombatState))
            .ToArray() ?? Array.Empty<Creature>();
    }

    private async Task PourDeathMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(PourDeathDamage, 10);
        await PowerCmd.Apply<FrailPower>(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            2m,
            Creature,
            null);
    }

    private Task AttackAllMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackAllDamage, 2);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            3m,
            Creature,
            null);
    }

    private async Task InvitationDeathMove(
        IReadOnlyList<Creature> targets)
    {
        await PerformAttack(InvitationDeathDamage);
        Creature[] livingTargets = targets
            .Where(target => target.IsAlive)
            .ToArray();
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            livingTargets,
            -3m,
            Creature,
            null);
        foreach (Creature target in livingTargets)
        {
            await CardPileCmd.AddToCombatAndPreview<Dazed>(
                target,
                PileType.Draw,
                3,
                target.Player,
                CardPilePosition.Random);
        }
    }

    private async Task ShieldLockBrokenMove(
        IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        ShieldLockPower shieldLock = Creature.GetPower<ShieldLockPower>();
        if (shieldLock != null)
        {
            await shieldLock.LoseLayers(1);
            return;
        }

        await ApplyPhaseWarpIfNeeded();
    }

    private async Task ThornSpearMove(
        IReadOnlyList<Creature> targets)
    {
        await PerformAttack(12, ThornSpearHitCount);
        await PowerCmd.Apply<DexterityPower>(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            -3m,
            Creature,
            null);
    }

    private async Task DeathSpearMove(
        IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.IsAlive))
        {
            IntangiblePower intangible =
                target.GetPower<IntangiblePower>();
            if (intangible != null)
            {
                await PowerCmd.Remove(intangible);
            }

            BufferPower buffer = target.GetPower<BufferPower>();
            if (buffer != null)
            {
                await PowerCmd.Remove(buffer);
            }
        }

        await PerformAttack(999, 2);
    }

    private async Task RespawnMove(IReadOnlyList<Creature> _)
    {
        Respawns++;
        FlatHandRevivalPower revival =
            Creature.GetPower<FlatHandRevivalPower>();

        decimal scaledHp = Creature.ScaleHpForMultiplayer(
            MinInitialHp + HpPerRevive * Respawns,
            CombatState.Encounter,
            CombatState.Players.Count,
            CombatState.RunState.CurrentActIndex);
        scaledHp = EnemyHealthScalingHelper.ScaleReplacementHealth(scaledHp, Creature);
        // Powers cannot be applied until the revival hit lock is released.
        revival?.FinishRevive();
        await CreatureCmd.SetMaxHp(Creature, scaledHp);
        await SaviorGrandEntranceCombatHelper.HealFromPhaseTransition(Creature, scaledHp);
        await HighDimensionalShellCombatStartPatch.GrantShell(
            Creature,
            replaceExisting: true);

        int currentStrength = Creature.GetPowerAmount<StrengthPower>();
        int targetStrength =
            RetainedStrength - ShieldLockStrengthReduction;
        int strengthDifference = targetStrength - currentStrength;
        if (strengthDifference != 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                strengthDifference,
                Creature,
                null);
        }

        if (Respawns == 1)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                3m,
                Creature,
                null);
            return;
        }

        await ResolveThirdPhaseEntrance();
        await LoseShieldLockLayers(3);
        if (revival != null)
        {
            await PowerCmd.Remove(revival);
        }
    }

    private async Task InitializeShieldLock()
    {
        if (ShieldLockInitialized)
        {
            return;
        }

        ShieldLockInitialized = true;
        int layers = CombatState.Players
            .SelectMany(player => player.Relics.OfType<ShieldLock>())
            .Sum(relic => relic.Counter);
        if (layers <= 0)
        {
            return;
        }

        ShieldLockStrengthReduction = layers;

        await PowerCmd.Apply<ShieldLockPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            layers,
            Creature,
            null);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            -layers,
            Creature,
            null);
    }

    private async Task LoseShieldLockLayers(int amount)
    {
        ShieldLockPower shieldLock = Creature.GetPower<ShieldLockPower>();
        if (shieldLock == null)
        {
            await ApplyPhaseWarpIfNeeded();
            return;
        }

        await shieldLock.LoseLayers(amount);
    }

    private async Task ApplyPhaseWarpIfNeeded()
    {
        if (Creature.HasPower<PhaseWarpPower>())
        {
            return;
        }

        await PowerCmd.Apply<PhaseWarpPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null);
    }

    private async Task ResolveThirdPhaseEntrance()
    {
        bool isAlliedPhantom = Creature.Side == CombatSide.Player ||
            Creature.HasPower<PhantomPower>();
        IEnumerable<Creature> targets = isAlliedPhantom
            ? CombatState.GetOpponentsOf(Creature)
                .Where(PhantomHelper.IsValidPhantomTarget)
            : CombatState.PlayerCreatures.Where(player => player.IsAlive);
        bool grantDraw = false;
        bool grantDrawPerTurn = false;
        foreach (Creature playerCreature in targets)
        {
            int roll = CombatState.RunState.Rng.Niche.NextInt(1, 101);
            foreach (SoulOfAnchor soul in
                     playerCreature.Player?.Relics.OfType<SoulOfAnchor>() ??
                     Enumerable.Empty<SoulOfAnchor>())
            {
                soul.RecordAbilityRoll(
                    roll,
                    (int?)null,
                    "FlatHand进入第三阶段时的判定");
            }

            if (roll < 100 && isAlliedPhantom)
            {
                grantDraw = true;
            }
            else if (roll < 100 && playerCreature.Player != null)
            {
                await CardPileCmd.AddToCombatAndPreview<Dazed>(
                    playerCreature,
                    PileType.Draw,
                    3,
                    playerCreature.Player,
                    CardPilePosition.Random);
            }

            if (roll < 75)
            {
                await PowerCmd.Apply<FrailPower>(
                    new ThrowingPlayerChoiceContext(),
                    playerCreature,
                    99m,
                    Creature,
                    null);
                await PowerCmd.Apply<VulnerablePower>(
                    new ThrowingPlayerChoiceContext(),
                    playerCreature,
                    99m,
                    Creature,
                    null);
            }

            if (roll < 50)
            {
                await PowerCmd.Apply<StrengthPower>(
                    new ThrowingPlayerChoiceContext(),
                    playerCreature,
                    -4m,
                    Creature,
                    null);
                await PowerCmd.Apply<DexterityPower>(
                    new ThrowingPlayerChoiceContext(),
                    playerCreature,
                    -4m,
                    Creature,
                    null);
            }

            if (roll < 25 && isAlliedPhantom)
            {
                grantDrawPerTurn = true;
            }
            else if (roll < 25 && playerCreature.Player != null)
            {
                await PowerCmd.Apply<MindRotPower>(
                    new ThrowingPlayerChoiceContext(),
                    playerCreature,
                    2m,
                    Creature,
                    null);
            }

            if (roll == 1)
            {
                await CreatureCmd.SetMaxHp(playerCreature, 1m);
            }
        }

        // The central context decides whether one move's benefit is scaled by
        // opponent count; do not count the same conversion again per recipient.
        if (grantDraw)
        {
            PhantomPlayerBenefitContext.QueueDraw(3);
        }
        if (grantDrawPerTurn)
        {
            PhantomPlayerBenefitContext.QueueDrawPerTurn(2);
        }
    }

}
