using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

// Potency is snapshotted separately from native Amount (remaining turns/uses).
// Shared by old status models and newly authored original-game effects.
public abstract class HbrEffectPower : HbrEnemyStatusPower, IHbrEffectInstance
{
    private decimal? _potency;
    private object _reservedAttack;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrPotencyData
    {
        get => _potency?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        protected set => _potency = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrExitOverride { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrLimitOverride { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrDefaultRemainingOverride { get; protected set; }
    internal abstract HbrEffectProfile EffectProfile { get; }
    internal HbrEffectProfile CurrentEffectProfile => IsMutable && !_potency.HasValue && Owner != null ? LegacyProfile : EffectProfile with
    {
        Exit = HbrExitOverride > 0 ? (HbrEffectExit)(HbrExitOverride - 1) : EffectProfile.Exit,
        Limit = HbrLimitOverride > 0 ? (HbrEffectLimit)(HbrLimitOverride - 1) : EffectProfile.Limit,
        DefaultRemaining = HbrDefaultRemainingOverride > 0 ? HbrDefaultRemainingOverride : EffectProfile.DefaultRemaining
    };
    internal decimal EffectPotency => _potency ?? (IsMutable ? LegacyPotency : 0m);
    internal int EffectRemaining => !IsMutable ? EffectProfile.DefaultRemaining : _potency.HasValue ? Amount : LegacyRemaining;
    internal virtual decimal LegacyPotency => Amount;
    internal virtual int LegacyRemaining => EffectProfile.DefaultRemaining;
    internal virtual HbrEffectProfile LegacyProfile => EffectProfile;
    decimal IHbrEffectInstance.EffectPotency => EffectPotency;
    int IHbrEffectInstance.Remaining => EffectRemaining;
    HbrEffectProfile IHbrEffectInstance.EffectProfile => CurrentEffectProfile;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    internal virtual decimal ResolvePotency(decimal basis, Creature applier, Creature target) => basis;
    internal virtual bool CanApplyTo(Creature target) => true;
    internal virtual decimal NativeApplicationBasis(decimal amount) => amount;
    internal virtual int NativeApplicationRemaining(decimal amount) => EffectProfile.DefaultRemaining;
    internal void ConfigureEffect(decimal potency, HbrEffectProfile? profile = null)
    {
        AssertMutable(); _potency = potency;
        HbrExitOverride = profile.HasValue ? (int)profile.Value.Exit + 1 : 0;
        HbrLimitOverride = profile.HasValue ? (int)profile.Value.Limit + 1 : 0;
        HbrDefaultRemainingOverride = profile?.DefaultRemaining ?? 0;
    }
    internal async Task DoubleExisting(PlayerChoiceContext context, decimal snapshotPotency,
        int snapshotRemaining, Creature applier, CardModel source)
    {
        MigrateLegacyEffect();
        if (Owner?.Powers.Contains(this) != true || Amount <= 0 || snapshotRemaining <= 0) return;
        int before = Amount;
        await PowerCmd.ModifyAmount(context, this, snapshotRemaining, applier, source);
        if (Owner?.Powers.Contains(this) != true || Amount <= before) return;
        ConfigureEffect(DamageAmplificationLimits.AddSaturated(EffectPotency, snapshotPotency), CurrentEffectProfile);
        Owner.InvokePowerModified(this, 0, true);
        await CombatPreviewRefreshHelper.RefreshCreatureIntent(Owner);
    }
    public override LocString Description
    {
        get
        {
            if (!IsMutable)
            {
                foreach (string suffix in new[] { ".referenceDescription", ".hbrReferenceDescription" })
                {
                    LocString reference = new("powers", Id.Entry + suffix);
                    if (reference.Exists()) return reference;
                }
            }
            LocString text = base.Description;
            text.Add("EffectPercent", EffectPotency.ToString("0.#", CultureInfo.InvariantCulture));
            text.Add("RemainingUses", IsMutable ? EffectRemaining : EffectProfile.DefaultRemaining);
            text.Add("RemainingTurns", IsMutable ? EffectRemaining : EffectProfile.DefaultRemaining);
            text.Add("Turns", IsMutable ? EffectRemaining : EffectProfile.DefaultRemaining);
            text.Add("Duration", HbrEffectProfileResolver.Duration(CurrentEffectProfile, EffectRemaining));
            text.Add("LifetimeRule", HbrEffectProfileResolver.LifetimeRule(CurrentEffectProfile, EffectRemaining));
            text.Add("SlotRule", HbrEffectProfileResolver.SlotRule(CurrentEffectProfile));
            return text;
        }
    }
    private void MigrateLegacyEffect()
    {
        if (_potency.HasValue || !IsMutable || Owner == null) return;
        decimal potency = LegacyPotency; int remaining = LegacyRemaining;
        ConfigureEffect(potency, LegacyProfile);
        SetAmount(remaining, silent: true);
    }
    public override Task AfterApplied(Creature applier, CardModel cardSource)
    { MigrateLegacyEffect(); return HbrEffectRules.Enforce(this, EffectPotency, CurrentEffectProfile); }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    { MigrateLegacyEffect(); SkipNextDurationTick = false; return HbrEffectRules.Tick(this, CurrentEffectProfile, side, participants); }
    internal bool HasEffectReservation => _reservedAttack != null;
    internal object ReservedEffectAttackToken => _reservedAttack;
    internal bool TryReserveEffect(object token, Creature source)
    {
        if (_reservedAttack != null || CurrentEffectProfile.Exit != HbrEffectExit.Count || Amount <= 0 || source != Owner) return false;
        _reservedAttack = token; return true;
    }
    internal void ReserveForEffectAttack(AttackCommand command)
    {
        if (_reservedAttack == null && CurrentEffectProfile.Exit == HbrEffectExit.Count && Amount > 0 &&
            command?.Attacker == Owner && command.DamageProps.IsPoweredAttack()) _reservedAttack = command;
    }
    internal void ReleaseEffectAttack(object command)
    { if (ReferenceEquals(command, _reservedAttack)) _reservedAttack = null; }
    internal async Task CompleteEffectAttack(object command, bool hadHit)
    {
        if (!ReferenceEquals(command, _reservedAttack)) return;
        _reservedAttack = null;
        if (hadHit && Owner?.Powers.Contains(this) == true && Amount > 0)
        {
            if (this is MindsEyePower eye && MindsEyeCombatHelper.DeferConsumption(eye, command)) return;
            await PowerCmd.Decrement(this);
        }
    }
    public override Task BeforeAttack(AttackCommand command)
    { MigrateLegacyEffect(); ReserveForEffectAttack(command); return Task.CompletedTask; }
    public override Task AfterAttack(PlayerChoiceContext context, AttackCommand command) =>
        CompleteEffectAttack(command, command.Results.Any(results => results.Any(result => result.Props.IsPoweredAttack())));
    protected override void AfterCloned()
    { base.AfterCloned(); _reservedAttack = null; }
}
