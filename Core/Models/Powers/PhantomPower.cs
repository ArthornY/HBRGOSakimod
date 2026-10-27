using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActionsPerformed { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AttackIntentsPerformed { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BuffIntentsPerformed { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsSummonedMinion { get; private set; }

    // The rolled, unupgraded and unfused summon HP. Event HP adjustments and
    // temporary levels must never rewrite the reference used for body size.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SizeBaseMaxHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string SizeBaseMaxHpData { get; private set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SummonedMinionLevel { get; private set; } = 1;

    public decimal CombatDamageBonusPercent { get; private set; }

    // Keep the unscaled pool exact. Native power saved properties do not support
    // decimal, and this pool can exceed Int32 while still containing fractions.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string CombatDamageBonusData
    {
        get => CombatDamageBonusPercent.ToString(CultureInfo.InvariantCulture);
        private set => CombatDamageBonusPercent = decimal.TryParse(value,
            NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? ClampRawCombatDamageBonus(parsed) : 0m;
    }

    private static decimal MaximumRawCombatDamageBonus =>
        DamageAmplificationLimits.MaximumTemporaryPercent / 0.7m;

    private static decimal ClampRawCombatDamageBonus(decimal amount) =>
        Math.Clamp(amount, 0m, MaximumRawCombatDamageBonus);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasPendingVineShamblerEffect { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PlayerTurnsStarted { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActionsThisPlayerTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredTerrorEelFusion { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredTwoTailedRatFusion { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredOwlMagistrateFusion { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ExhaustPreventionsUsed { get; private set; }

    [SavedProperty]
    public string PendingCopiedIntentId { get; private set; } =
        string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingCopiedIntentCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingNativeThorns { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("E9A8C7");

    private bool UsesThunderActionDescription => IsMutable && !IsSummonedMinion &&
        BoostUpPhantomHelper.HasThunderInPermanentDeck(Owner?.PetOwner);

    public override LocString Description => new("powers", Id.Entry +
        (UsesThunderActionDescription ? ".thunderDescription" : ".description"));

    internal LocString CompactDescription => new("powers", Id.Entry +
        (UsesThunderActionDescription ? ".thunderCompactDescription" : ".compactDescription"));

    public override bool ShouldClearBlock(Creature creature)
    {
        // Creature.AfterTurnStart preserves a player's opening block, but a
        // phantom has PetOwner instead of Player. Apply the same first-turn
        // exception to block redirected to this phantom before combat starts.
        return creature != Owner ||
            creature.Side != CombatSide.Player ||
            creature.CombatState?.CurrentSide != CombatSide.Player ||
            creature.PetOwner?.PlayerCombatState?.TurnNumber != 1;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (props.HasFlag(ValueProp.Unpowered) ||
            Owner.PetOwner is not { } player ||
            player.Character is not HbrgoSaki30A)
        {
            return 1m;
        }

        decimal multiplier = 1m;
        if (dealer == Owner)
        {
            decimal permanentPercent = PhantomHelper.GetDamageBonusBreakdown(Owner).PermanentPercent;
            decimal temporaryPercent = GetTemporaryDamageBonusPercent();
            if (!IsSummonedMinion && props.IsPoweredAttack())
            {
                temporaryPercent += 15m * SixSenseCombatHelper.TargetResistanceBreakCount(target);
            }

            if (target != Owner)
            {
                // Temporary levels have their own hook. Submit only this power's
                // pool and dynamic bonuses here, leaving permanent growth outside
                // the shared temporary budget.
                decimal permanent = HbrgoSakiDamageMultiplierHelper.AddPermanent(
                    1m + permanentPercent / 100m, this);
                decimal temporary = HbrgoSakiDamageMultiplierHelper.Add(
                    1m + temporaryPercent / 100m, this);
                return Math.Max(0m, permanent + temporary - 1m);
            }

            multiplier += permanentPercent / 100m + temporaryPercent / 100m;
        }

        if (target == Owner)
        {
            multiplier -= PhantomHelper.GetDamageReductionPercent(Owner) / 100m;
        }

        return Math.Max(0m, multiplier);
    }

    internal decimal GetTemporaryDamageBonusPercent()
    {
        decimal classification = PhantomHelper.GetDamageBonusMultiplier(Owner);
        decimal percent = ClampRawCombatDamageBonus(CombatDamageBonusPercent) * classification;
        if (Owner?.GetPower<DoubleDamagePower>()?.Amount > 0)
            percent += 100m * classification;
        if (!IsSummonedMinion && PlayerTurnsStarted % 2 == 1)
            percent += PhantomFusionEffectHelper.GetEffect(
                PhantomHelper.GetFusionEffectData(Owner),
                PhantomFusionEffectType.Crusher, 25m, 15m) * classification;
        return percent;
    }

    internal bool TryPreventCardExhaust(CardModel card)
    {
        if (IsSummonedMinion ||
            !Owner.IsAlive ||
            Owner.PetOwner is not { } player ||
            card?.Owner != player)
        {
            return false;
        }

        int preventionCount = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                PhantomHelper.GetFusionEffectData(Owner),
                PhantomFusionEffectType.SpectralKnight,
                1m,
                1m));
        if (ExhaustPreventionsUsed >= preventionCount)
        {
            return false;
        }

        AssertMutable();
        ExhaustPreventionsUsed++;
        Flash();
        return true;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (IsSummonedMinion ||
            Owner.PetOwner is not { } player ||
            card?.Owner != player ||
            card.Type != CardType.Power ||
            originalCost <= 0m)
        {
            return false;
        }

        int reduction = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                PhantomHelper.GetFusionEffectData(Owner),
                PhantomFusionEffectType.GlobeHead,
                1m,
                1m));
        if (reduction <= 0)
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - reduction);
        return modifiedCost != originalCost;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        return IsSummonedMinion
            ? Task.CompletedTask
            : PerformTurnEndAction(side, participants);
    }

    public override Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // The late hook takes a fresh snapshot, including minions just summoned by the leader.
        return IsSummonedMinion
            ? PerformTurnEndAction(side, participants)
            : Task.CompletedTask;
    }

    private async Task PerformTurnEndAction(
        CombatSide side, IEnumerable<Creature> participants)
    {
        bool isPendingSceneBossRevive =
            PhantomHelper.IsPendingSceneBossRevive(Owner);
        if (Owner.PetOwner is not { } player ||
            side != player.Creature.Side ||
            !participants.Contains(player.Creature) ||
            (!Owner.IsAlive && !isPendingSceneBossRevive))
        {
            return;
        }

        await PhantomHelper.PerformCurrentIntent(Owner, isImmediateAction: false);
        if (!isPendingSceneBossRevive)
        {
            await PhantomHelper.ApplyFusionEffectsAfterPlayerTurnEnd(
                Owner,
                this);
        }
    }

    public override bool ShouldPowerBeRemovedAfterOwnerDeath()
    {
        return !PhantomHelper.HasNativeRevival(Owner) &&
            Owner?.Monster is not IHbrgoRevivingStaticMonster
        {
            ShouldSuppressDeathFade: true
        };
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (IsSummonedMinion ||
            Owner.PetOwner is not { } player ||
            side != player.Creature.Side ||
            !participants.Contains(player.Creature) ||
            !Owner.IsAlive)
        {
            return;
        }

        await BeginPlayerTurn();
    }

    internal async Task BeginPlayerTurn()
    {
        AssertMutable();
        PlayerTurnsStarted = Math.Min(
            int.MaxValue,
            PlayerTurnsStarted + 1);
        ActionsThisPlayerTurn = 0;
        await PhantomHelper.ApplyFusionEffectsAtPlayerTurnStart(
            Owner,
            this);

        if (!IsSummonedMinion &&
            BoostUpPhantomHelper.HasThunderInPermanentDeck(Owner.PetOwner))
        {
            await BoostUpPhantomHelper.PerformThunderStartAction(Owner);
        }
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        CurlUpPower curlUp = Owner.GetPower<CurlUpPower>();
        if (curlUp == null ||
            !command.DamageProps.IsPoweredAttack() ||
            !command.Results
                .SelectMany(results => results)
                .Any(result =>
                    result.Receiver == Owner &&
                    result.TotalDamage > 0))
        {
            return;
        }

        await CreatureCmd.GainBlock(
            Owner,
            HbrSkillBlockHelper.ResolveUnpowered(Owner, curlUp.Amount),
            ValueProp.Unpowered,
            null);
        if (Owner.Monster is LouseProgenitor louse)
        {
            louse.Curled = true;
        }

        await PowerCmd.Remove(curlUp);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel cardSource)
    {
        if (!IsSummonedMinion && dealer == Owner && props.IsPoweredAttack())
        {
            await SixSenseCombatHelper.ResolvePhantomAttack(
                choiceContext, Owner.PetOwner, target, cardSource);
        }

        await PhantomHelper.ApplyFusionEffectsAfterDamageGiven(
            Owner,
            this,
            dealer,
            result,
            target);
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return PhantomHelper.ApplyFusionEffectsAfterDamageReceived(
            Owner,
            this,
            target,
            result,
            props,
            dealer);
    }

    public override Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        return PhantomHelper.ApplyFusionEffectsAfterHpLoss(
            Owner,
            this,
            creature,
            delta);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await PhantomHelper.ApplyFusionEffectsAfterUnitDeath(
            Owner,
            this,
            creature,
            wasRemovalPrevented);

        if (!wasRemovalPrevented &&
            PhantomHelper.IsMinionOf(Owner, creature))
        {
            await PhantomHelper.ApplyFusionEffectsAfterMinionDeath(
                Owner,
                this);
        }

        if (creature != Owner || wasRemovalPrevented)
        {
            return;
        }

        Creature[] minions = Owner.CombatState?.Allies
            .Where(candidate =>
                candidate.IsAlive &&
                candidate.GetPower<
                    MegaCrit.Sts2.Core.Models.Powers.MinionPower>()?
                    .Applier == Owner)
            .ToArray() ?? Array.Empty<Creature>();
        if (minions.Length > 0)
        {
            await CreatureCmd.Kill(minions);
        }

    }

    internal void ConfigureAsRosterPhantom()
    {
        AssertMutable();
        IsSummonedMinion = false;
        SummonedMinionLevel = 1;
    }

    internal void InitializeSizeBaseMaxHp(decimal maxHp)
    {
        AssertMutable();
        if (SizeBaseMaxHp <= 0 && string.IsNullOrEmpty(SizeBaseMaxHpData))
        {
            decimal actual = Math.Max(1m, BigCombatNumbers.ClampStat(maxHp));
            SizeBaseMaxHpData = BigCombatNumbers.Save(actual);
            SizeBaseMaxHp = BigCombatNumbers.ToNativeSigned(actual);
        }
    }

    internal decimal GetSizeBaseMaxHp()
    {
        if (SizeBaseMaxHp > 0 || !string.IsNullOrEmpty(SizeBaseMaxHpData))
            return Math.Max(1m, BigCombatNumbers.ReadSigned(SizeBaseMaxHpData, SizeBaseMaxHp));
        // Old saves lack this field. Prefer the original roll if still available;
        // otherwise use the native upper bound without rolling or reading the
        // mutable event-payment HP cache. The approximation is saved once.
        using IDisposable scope = PhantomAscensionHelper.BeginPhantomEvaluation();
        int originalHp = Owner.MonsterMaxHpBeforeModification ??
            Owner.Monster?.MaxInitialHp ?? 2;
        PhantomRosterEntry? entry = Owner.PetOwner?.GetRelic<MomentOfReign>()?.GetActiveRosterEntry();
        InitializeSizeBaseMaxHp(entry?.GetBasePhantomHp(originalHp,
            Owner.Monster is RedCrimsonBossMonster, Owner.PetOwner) ??
            HbrBattleStatsHelper.GetPhantomBaseHp(Owner.PetOwner, Math.Max(1, originalHp / 2)));
        return Math.Max(1m, BigCombatNumbers.ReadSigned(SizeBaseMaxHpData, SizeBaseMaxHp));
    }

    internal void ConfigureAsSummonedMinion(int level)
    {
        AssertMutable();
        IsSummonedMinion = true;
        SummonedMinionLevel = Math.Max(1, level);
    }

    internal void AddCombatDamageBonus(decimal amount)
    {
        AssertMutable();
        decimal current = ClampRawCombatDamageBonus(CombatDamageBonusPercent);
        CombatDamageBonusPercent = amount > 0m && amount >= MaximumRawCombatDamageBonus - current
            ? MaximumRawCombatDamageBonus
            : ClampRawCombatDamageBonus(current + amount);
        Flash();
    }

    internal void SetPendingVineShamblerEffect(bool pending)
    {
        AssertMutable();
        HasPendingVineShamblerEffect = pending;
    }

    internal void MarkTerrorEelFusionTriggered()
    {
        AssertMutable();
        HasTriggeredTerrorEelFusion = true;
    }

    internal void MarkTwoTailedRatFusionTriggered()
    {
        AssertMutable();
        HasTriggeredTwoTailedRatFusion = true;
    }

    internal void MarkOwlMagistrateFusionTriggered()
    {
        AssertMutable();
        HasTriggeredOwlMagistrateFusion = true;
    }

    internal bool QueueCopiedIntent(string intentId)
    {
        if (string.IsNullOrWhiteSpace(intentId))
        {
            return false;
        }

        AssertMutable();
        if (PendingCopiedIntentCount > 0 &&
            !string.Equals(
                PendingCopiedIntentId,
                intentId,
                StringComparison.Ordinal))
        {
            return false;
        }

        PendingCopiedIntentId = intentId;
        PendingCopiedIntentCount = Math.Min(
            100,
            PendingCopiedIntentCount + 1);
        Flash();
        return true;
    }

    internal (string IntentId, int Count) ConsumeCopiedIntents()
    {
        AssertMutable();
        string intentId = PendingCopiedIntentId;
        int count = PendingCopiedIntentCount;
        PendingCopiedIntentId = string.Empty;
        PendingCopiedIntentCount = 0;
        return (intentId, count);
    }

    internal void RecordIntent(bool wasAttack, bool wasBuff)
    {
        AssertMutable();
        if (wasAttack)
        {
            AttackIntentsPerformed = Math.Min(
                int.MaxValue,
                AttackIntentsPerformed + 1);
        }

        if (wasBuff)
        {
            BuffIntentsPerformed = Math.Min(
                int.MaxValue,
                BuffIntentsPerformed + 1);
        }

        Flash();
    }

    internal void RecordNativeThornsGain(int amount)
    {
        AssertMutable();
        PendingNativeThorns = (int)Math.Min(int.MaxValue,
            (long)PendingNativeThorns + Math.Max(0, amount));
    }

    internal int ConsumeNativeThorns()
    {
        AssertMutable();
        int amount = PendingNativeThorns;
        PendingNativeThorns = 0;
        return amount;
    }

    internal void RecordAction()
    {
        AssertMutable();
        ActionsPerformed = Math.Min(
            int.MaxValue,
            ActionsPerformed + 1);
        ActionsThisPlayerTurn = Math.Min(
            int.MaxValue,
            ActionsThisPlayerTurn + 1);

        Flash();
    }
}
