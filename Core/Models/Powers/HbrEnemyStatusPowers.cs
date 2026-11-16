using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

// Original HBR enemy statuses used by the move-table monsters
// (docs/hbr-monster-mechanics-proposal.md, section 3.3). Each icon is the
// original BattleIcon texture; see images/powers/*.sources.json.
public abstract class HbrEnemyStatusPower : PowerModel, ICustomPower
{
    public abstract string CustomPackedIconPath { get; }

    public string CustomBigIconPath => CustomPackedIconPath;

    public string CustomBigBetaIconPath => CustomPackedIconPath;

    // A holder with the six HBR attributes (an HBR-mode character or its
    // phantom). Legacy characters, original characters and enemies have none.
    internal static bool UsesAttributes(Creature creature) =>
        creature != null && HbrBattleStatsHelper.IsEnabled(creature) &&
        !EnemyStatScalingHelper.IsEnemy(creature);
}

// Original Charge (IsEnemyCharge). A stun, a DP break (without a stun), or
// firing the charged move removes it.
[CustomID("HBR_ENEMY_CHARGE_POWER")]
public sealed class HbrEnemyChargePower : HbrEffectPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_enemy_charge_power.png";

    public override PowerType Type => PowerType.Buff;

    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1);

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner?.Monster == null || side != Owner.Side || !participants.Contains(Owner) ||
            Owner.Monster.NextMove?.Id != HbrEnemyEffects.StunnedMoveId)
        {
            return;
        }

        Flash();
        await PowerCmd.Remove(this);
    }
}

// Original percentage effects share saved potency, slot selection and exits.
public abstract class HbrTimedPercentBuffPower : HbrEffectPower
{
    internal const int InitialDuration = 2;
    [SavedProperty]
    public int RemainingTurns { get; protected set; } = InitialDuration;
    public override PowerType Type => PowerType.Buff;
    internal override int LegacyRemaining => RemainingTurns > 0 ? RemainingTurns : InitialDuration;
    internal override HbrEffectProfile LegacyProfile => new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, InitialDuration);
    internal override decimal ResolvePotency(decimal basis, Creature applier, Creature target) =>
        HbrEffectFormula.Buff(basis, applier, HbrEffectFormula.Intelligence);
    internal void RefreshDuration() { AssertMutable(); RemainingTurns = InitialDuration; SetAmount(InitialDuration); }
    protected bool IsFirst<T>() where T : HbrEffectPower => ReferenceEquals(this,
        Owner.Powers.OfType<T>().FirstOrDefault(power => power.Amount > 0));
    protected decimal Total<T>() where T : HbrEffectPower => Owner.Powers.OfType<T>()
        .Where(power => power.Amount > 0).Aggregate(0m, (sum, power) => DamageAmplificationLimits.AddSaturated(sum, power.EffectPotency));
}

[CustomID("HBR_ATTACK_UP_POWER")]
public class HbrAttackUpPower : HbrTimedPercentBuffPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
    internal decimal Multiplier => 1m + EffectPotency / 100m;
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack() && IsFirst<HbrAttackUpPower>()
            ? 1m + Total<HbrAttackUpPower>() / 100m : 1m;
}

[CustomID("HBR_DEFENSE_UP_POWER")]
public sealed class HbrDefenseUpPower : HbrTimedPercentBuffPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_defense_up_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
    internal decimal Multiplier => 1m - EffectPotency / 100m;
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && target == Owner && dealer != Owner && props.IsPoweredAttack() && IsFirst<HbrDefenseUpPower>()
            ? 1m - Total<HbrDefenseUpPower>() / 100m : 1m;
}

public abstract class HbrStatRateDownPower : HbrEffectPower
{
    internal const int DefaultDuration = 2;
    internal virtual int InitialDuration => DefaultDuration;
    internal abstract decimal BasePercent { get; }
    internal override decimal LegacyPotency => BasePercent;
    internal override int LegacyRemaining => Amount;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, InitialDuration);
    internal override decimal NativeApplicationBasis(decimal amount) => BasePercent;
    internal override int NativeApplicationRemaining(decimal amount) => (int)Math.Clamp(decimal.Truncate(amount), 1m, int.MaxValue);
    public override PowerType Type => PowerType.Debuff;
    protected static decimal Sum<T>(Creature creature) where T : HbrEffectPower => creature == null ? 0m :
        creature.Powers.OfType<T>().Where(power => power.Amount > 0)
            .Aggregate(0m, (sum, power) => DamageAmplificationLimits.AddSaturated(sum, power.EffectPotency));
}

[CustomID("HBR_SPIRIT_DOWN_POWER")]
public sealed class HbrSpiritDownPower : HbrStatRateDownPower
{
    internal const decimal SpiritFactor = 0.5m;
    internal const decimal IncomingDamageMultiplier = 1.25m;
    internal override decimal BasePercent => 50m;
    public override string CustomPackedIconPath => "res://images/powers/hbr_spirit_down_power.png";
    internal override decimal ResolvePotency(decimal basis, Creature applier, Creature target) =>
        HbrEffectFormula.Debuff(basis, applier, target, HbrEffectFormula.Intelligence);
    internal static decimal GetSpiritFactor(Creature creature) => 1m - Sum<HbrSpiritDownPower>(creature) / 100m;
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (Amount <= 0 || target != Owner || dealer == Owner || !props.IsPoweredAttack() || UsesAttributes(Owner)) return 1m;
        if (!HbrBattleStatsHelper.IsEnabled(Owner)) return IncomingDamageMultiplier;
        return ReferenceEquals(this, Owner.Powers.OfType<HbrSpiritDownPower>().FirstOrDefault(power => power.Amount > 0))
            ? 1m + Sum<HbrSpiritDownPower>(Owner) / 200m : 1m;
    }
}

[CustomID("HBR_WISDOM_DOWN_POWER")]
public sealed class HbrWisdomDownPower : HbrStatRateDownPower
{
    internal const decimal WisdomFactor = 0.75m;
    internal const int WisdomDownDuration = 3;
    internal override int InitialDuration => WisdomDownDuration;
    internal override decimal BasePercent => 80m;
    public override string CustomPackedIconPath => "res://images/powers/hbr_wisdom_down_power.png";
    internal static decimal GetWisdomFactor(Creature creature) => 1m - Sum<HbrWisdomDownPower>(creature) / 100m;
    public override decimal ModifyBlockMultiplicative(Creature target, decimal block,
        ValueProp props, CardModel cardSource, CardPlay cardPlay)
    {
        if (Amount <= 0 || target != Owner || block <= 0m || !props.IsPoweredCardOrMonsterMoveBlock() || UsesAttributes(Owner)) return 1m;
        if (!HbrBattleStatsHelper.IsEnabled(Owner)) return WisdomFactor;
        return ReferenceEquals(this, Owner.Powers.OfType<HbrWisdomDownPower>().FirstOrDefault(power => power.Amount > 0))
            ? 1m - Sum<HbrWisdomDownPower>(Owner) / 100m : 1m;
    }
}

// All applications below are independent saved framework instances. Native
// Amount is the remaining lifetime; potency is a separate saved snapshot.
public abstract class HbrInstancedEnemyStatusPower : HbrEffectPower
{
    public override PowerType Type => PowerType.Debuff;
    internal virtual int StatusDuration => 2;
    internal virtual decimal StatusPotency => 1m;
    internal override decimal LegacyPotency => StatusPotency;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, StatusDuration);
    internal override decimal NativeApplicationBasis(decimal amount) => StatusPotency;
    internal override int NativeApplicationRemaining(decimal amount) => StatusDuration;
    internal HbrEffectProfile ApplicationProfile(Creature target)
    {
        if (target?.IsPlayer != true) return EffectProfile;
        // These ailments count the player's own completed turns in both modes.
        // Enemy/phantom holders retain their original enemy-side profiles.
        if (this is HbrVirusPower or HbrIllusionPower)
            return new(HbrEffectLimit.None, HbrEffectExit.PlayerTurnEnd, 2);
        if (this is HbrConfusionPower)
            return new(HbrEffectLimit.None, HbrEffectExit.PlayerTurnEnd, 3);
        if (HbrBattleStatsHelper.IsEnabled(target)) return EffectProfile;
        return this switch
        {
            HbrImprisonPower => new(HbrEffectLimit.None, HbrEffectExit.PlayerTurnEnd, 1),
            HbrActionLockPower => new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1),
            HbrVirusPower or HbrIllusionPower => new(HbrEffectLimit.None, HbrEffectExit.PlayerTurnEnd, 2),
            HbrConfusionPower => new(HbrEffectLimit.None, HbrEffectExit.PlayerTurnEnd, 3),
            _ => EffectProfile
        };
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState) =>
        HbrEffectRules.TickStart(this, CurrentEffectProfile, side, participants);
}

[CustomID("HBR_IMPRISON_POWER")]
public sealed class HbrImprisonPower : HbrInstancedEnemyStatusPower
{
    internal const int MinimumBlockedCost = 2;
    internal override bool CanApplyTo(Creature target) => target?.IsPlayer == true;
    public override string CustomPackedIconPath => "res://images/powers/hbr_imprison_power.png";
    internal static bool IsSealed(CardModel card) => card?.EnergyCost is { } cost &&
        (cost.CostsX || cost.GetWithModifiers(CostModifiers.All) >= MinimumBlockedCost);
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        Amount <= 0 || autoPlayType != AutoPlayType.None || card?.Owner?.Creature != Owner || !IsSealed(card);
}

public abstract class HbrActionLockPower : HbrInstancedEnemyStatusPower
{
    internal const int EnergyLoss = 2;
    internal override bool CanApplyTo(Creature target) => target?.IsPlayer == true;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EnergySpent { get; protected set; }
    public override async Task AfterEnergyReset(Player player)
    {
        if (player == null || Owner?.Player != player || Amount <= 0 || EnergySpent) return;
        if (!HbrBattleStatsHelper.IsEnabled(Owner))
        {
            EnergySpent = true;
            await PlayerCmd.LoseEnergy(1m, player);
            await PowerCmd.Remove(this);
            return;
        }
        // Bind and Stun are one action-lock switch, irrespective of the number
        // of simultaneous copies. Mark every pending copy before awaiting.
        foreach (var copy in Owner.Powers.OfType<HbrActionLockPower>().Where(copy => copy.Amount > 0))
            copy.EnergySpent = true;
        Flash();
        await PlayerCmd.LoseEnergy(EnergyLoss, player);
    }
}

[CustomID("HBR_BIND_POWER")]
public sealed class HbrBindPower : HbrActionLockPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_bind_power.png";
}

[CustomID("HBR_STUN_POWER")]
public sealed class HbrStunPower : HbrActionLockPower
{
    internal override int StatusDuration => 1;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnStart, 1);
    public override string CustomPackedIconPath => "res://images/powers/hbr_stun_power.png";
}

// A periodic receipt counts owner starts, not the application turn's end:
// this preserves the existing four actual DP losses requested by the user.
[CustomID("HBR_CONTINUOUS_DP_DAMAGE_POWER")]
public sealed class HbrContinuousDpDamagePower : HbrInstancedEnemyStatusPower
{
    internal const int InitialDuration = 4;
    internal override int StatusDuration => InitialDuration;
    internal override decimal StatusPotency => 5m;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.OwnerTurnStart, InitialDuration);
    public override string CustomPackedIconPath => "res://images/powers/hbr_continuous_dp_damage_power.png";
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner == null || side != Owner.Side || !participants.Contains(Owner) || Amount <= 0 ||
            !ReferenceEquals(Owner.CombatState, combatState)) return;
        Flash();
        await HbrEnemyEffects.LoseDpShare(context, Owner, EffectPotency / 100m, Applier);
        if (Owner?.Powers.Contains(this) == true)
            await HbrEffectRules.TickStart(this, CurrentEffectProfile, side, participants);
    }
}

[CustomID("HBR_VIRUS_POWER")]
public sealed class HbrVirusPower : HbrInstancedEnemyStatusPower
{
    internal const int InitialDuration = 2;
    internal const decimal MaximumShare = 0.7m;
    internal override decimal StatusPotency => 30m;
    public override string CustomPackedIconPath => "res://images/powers/hbr_virus_power.png";
    private static decimal Factor(Creature creature) => Math.Max(0m, 1m -
        (creature?.Powers.OfType<HbrVirusPower>().Where(power => power.Amount > 0)
            .Sum(power => power.EffectPotency) ?? 0m) / 100m);
    internal static int LowerMaximum(Creature creature, int maximum) => (int)decimal.Floor(maximum * Factor(creature));
    internal static decimal LowerMaximum(Creature creature, decimal maximum) => decimal.Floor(maximum * Factor(creature));
    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        await base.AfterApplied(applier, cardSource);
        await HbrEnemyEffects.SyncMaximumDp(new ThrowingPlayerChoiceContext(), Owner);
    }
    public override Task AfterRemoved(Creature oldOwner) =>
        HbrEnemyEffects.SyncMaximumDp(new ThrowingPlayerChoiceContext(), oldOwner);
}

[CustomID("HBR_ILLUSION_POWER")]
public sealed class HbrIllusionPower : HbrInstancedEnemyStatusPower
{
    internal const int InitialDuration = 2;
    public int RemainingTurns => Amount;
    internal override decimal LegacyPotency => Amount;
    internal override decimal NativeApplicationBasis(decimal amount) => amount;
    public override string CustomPackedIconPath => "res://images/powers/hbr_illusion_power.png";
    internal void RefreshDuration() { AssertMutable(); SetAmount(InitialDuration); }
    internal void RefreshLegacy(int layers)
    {
        ConfigureEffect(Math.Max(EffectPotency, layers), CurrentEffectProfile);
        SetAmount(InitialDuration);
        Owner.InvokePowerModified(this, 0, true);
    }
    internal static int TotalLayers(Creature owner) => (int)Math.Min(int.MaxValue,
        owner?.Powers.OfType<HbrIllusionPower>().Where(power => power.Amount > 0)
            .Sum(power => Math.Max(0m, power.EffectPotency)) ?? 0m);
    private bool MissesFor(Creature dealer, ValueProp props, CardModel cardSource) =>
        Amount > 0 && EffectPotency > 0 && dealer == Owner && props.IsPoweredAttack() &&
        (Owner.IsPlayer ? cardSource?.Type == CardType.Attack : cardSource == null);
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) => MissesFor(dealer, props, cardSource) ? 0m : 1m;
    private async Task ConsumeOne(object token)
    {
        if (!HbrEnemyStatusRuntime.TryOnce(token, Owner, typeof(HbrIllusionPower))) return;
        HbrIllusionPower first = Owner.Powers.OfType<HbrIllusionPower>()
            .Where(power => power.Amount > 0 && power.EffectPotency > 0)
            .OrderBy(power => power.Amount).FirstOrDefault();
        if (first == null) return;
        first.Flash();
        if (first.EffectPotency <= 1m) await PowerCmd.Remove(first);
        else
        {
            first.ConfigureEffect(first.EffectPotency - 1m, first.CurrentEffectProfile);
            first.Owner.InvokePowerModified(first, 0, true);
        }
    }
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay) =>
        Amount > 0 && Owner.IsPlayer && cardPlay?.Card?.Type == CardType.Attack && cardPlay.Card.Owner?.Creature == Owner
            ? ConsumeOne(cardPlay) : Task.CompletedTask;
    public override Task AfterAttack(PlayerChoiceContext context, AttackCommand command) =>
        Amount > 0 && !Owner.IsPlayer && command?.Attacker == Owner ? ConsumeOne(command) : Task.CompletedTask;
}

// Original ConfusionRandom: actions may hit the actor. Amount is the remaining
// turns. A player's first single-target attack card each turn picks its target
// among the enemies and the player; hitting the player, the card's damage is
// dealt to the player as an enemy attack instead. An enemy's next attack hits
// itself half the time; the status then ends.
[CustomID("HBR_CONFUSION_POWER")]
public sealed class HbrConfusionPower : HbrInstancedEnemyStatusPower
{
    internal const int InitialDuration = 3;
    internal const int EnemySelfHitChance = 2;

    private CardModel _selfHitCard;
    private AttackCommand _selfHitAttack;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool UsedThisTurn { get; private set; }

    public override string CustomPackedIconPath => "res://images/powers/hbr_confusion_power.png";

    public override PowerType Type => PowerType.Debuff;

    internal override int StatusDuration => InitialDuration;

    internal static bool IsSingleTargetAttack(CardModel card) =>
        card?.Type == CardType.Attack && card.TargetType == TargetType.AnyEnemy;

    // Called before the card play is set up (HbrConfusionCardTargetPatch).
    internal Creature ChooseCardTarget(CardModel card, Creature target)
    {
        if (!IsMutable || Amount <= 0 || UsedThisTurn || !Owner.IsPlayer ||
            card?.Owner?.Creature != Owner || !IsSingleTargetAttack(card) ||
            target == null || target.Side == Owner.Side || Owner.CombatState is not { } combat)
        {
            return target;
        }

        foreach (var copy in Owner.Powers.OfType<HbrConfusionPower>()) copy.UsedThisTurn = true;
        Creature[] enemies = HbrProvokeTargetingHelper.ConfusionCandidates(card);
        int roll = Owner.Player.RunState.Rng.CombatTargets.NextInt(enemies.Length + 1);
        if (roll < enemies.Length)
        {
            return enemies[roll];
        }

        _selfHitCard = card;
        Flash();
        return target;
    }

    private bool SelfHitStopsDamage(Creature target, Creature dealer, CardModel cardSource) =>
        dealer == Owner && target != Owner &&
        ((_selfHitCard != null && cardSource == _selfHitCard) ||
            (_selfHitAttack != null && cardSource == null));

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        props.IsPoweredAttack() && SelfHitStopsDamage(target, dealer, cardSource) ? 0m : 1m;

    // The card hits its player: its printed damage, settled as an attack by
    // the enemy that confused them (or another living enemy).
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (_selfHitCard == null || cardPlay?.Card != _selfHitCard || cardPlay.PlayIndex != 0)
        {
            return;
        }

        decimal raw = _selfHitCard.DynamicVars.TryGetValue("Damage", out DynamicVar damage) ? damage.BaseValue : 0m;
        Creature dealer = Applier is { IsAlive: true } applier && applier.Side != Owner.Side
            ? applier
            : Owner.CombatState?.HittableEnemies.FirstOrDefault(enemy => enemy.IsAlive);
        if (raw > 0m && dealer != null)
        {
            await CreatureCommandCompatibility.Damage(new ThrowingPlayerChoiceContext(), Owner, raw,
                ValueProp.Move, dealer, null);
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_selfHitCard != null && cardPlay?.Card == _selfHitCard && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _selfHitCard = null;
        }

        return Task.CompletedTask;
    }

    // An enemy holder: its next attack hits itself half the time.
    public override Task BeforeAttack(AttackCommand command)
    {
        if (Amount > 0 && !Owner.IsPlayer && command?.Attacker == Owner && _selfHitAttack == null &&
            ReferenceEquals(this, Owner.Powers.OfType<HbrConfusionPower>().FirstOrDefault(copy => copy.Amount > 0)) &&
            Owner.CombatState?.RunState?.Rng?.CombatTargets is { } rng &&
            rng.NextInt(EnemySelfHitChance) == 0)
        {
            _selfHitAttack = command;
            Flash();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (Owner == null || Owner.IsPlayer || command?.Attacker != Owner)
        {
            return;
        }

        if (_selfHitAttack == command)
        {
            _selfHitAttack = null;
            decimal raw = Owner.Monster?.NextMove?.Intents.OfType<AttackIntent>()
                .Sum(intent => Math.Max(0m, intent.DamageCalc?.Invoke() ?? 0m) * Math.Max(1, intent.Repeats)) ?? 0m;
            if (raw > 0m && Owner.IsAlive)
            {
                bool hbr = HbrBattleStatsHelper.IsEnabled(Owner);
                using var skill = hbr ? HbrEffectSkillDamageContext.Begin(Owner, null, null,
                    raw, ValueProp.Unpowered, Owner) : null;
                await CreatureCommandCompatibility.Damage(new ThrowingPlayerChoiceContext(), Owner,
                    hbr ? raw : HbrResourceValueHelper.Fixed(Owner, raw), ValueProp.Unpowered, null, null);
            }
        }

        if (Owner.IsAlive && Owner.Powers.Contains(this))
        {
            foreach (var copy in Owner.Powers.OfType<HbrConfusionPower>().ToArray()) await PowerCmd.Remove(copy);
        }
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner != null && side == Owner.Side && participants.Contains(Owner))
        {
            UsedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants) =>
        base.AfterSideTurnEnd(choiceContext, side, participants);
}
