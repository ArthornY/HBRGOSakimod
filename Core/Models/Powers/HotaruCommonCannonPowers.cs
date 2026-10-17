using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_WAIT_OPPORTUNITY_POWER")]
public sealed class HotaruWaitOpportunityPower : PowerModel, ICustomPower
{
    private int _observedAmount;
    private bool _hasObservedAmount;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ClaimedActivations { get; private set; }

    public int PendingActivations
    {
        get
        {
            ObserveNativeAmount();
            return Math.Max(0, Amount - ClaimedActivations);
        }
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override int DisplayAmount => IsMutable ? PendingActivations : 1;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("Amount", IsMutable ? PendingActivations : 1);
            return description;
        }
    }

    // The native counter is authoritative: prevented or amplified power
    // applications create exactly the requests the engine actually granted.
    public static Task Apply(PlayerChoiceContext context, Creature owner, int count, CardModel source) =>
        HotaruCommonCannonHelper.CanAct(owner?.Player) && count > 0
            ? PowerCmd.Apply<HotaruWaitOpportunityPower>(context, owner, count, owner, source)
            : Task.CompletedTask;

    private void ObserveNativeAmount()
    {
        if (_hasObservedAmount && Amount < _observedAmount)
            ClaimedActivations = Math.Max(0, ClaimedActivations - (_observedAmount - Amount));
        ClaimedActivations = Math.Clamp(ClaimedActivations, 0, Math.Max(0, Amount));
        _observedAmount = Amount;
        _hasObservedAmount = true;
    }

    internal int ClaimPending()
    {
        if (!HotaruCommonCannonHelper.CanAct(Owner?.Player)) return 0;
        int count = PendingActivations;
        ClaimedActivations += count;
        return count;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this)) ObserveNativeAmount();
        return Task.CompletedTask;
    }

    internal Task ResolveNextTurn(PlayerChoiceContext context) => ResolveClaimed(context, ClaimPending());

    internal async Task ResolveClaimed(PlayerChoiceContext context, int count)
    {
        if (count <= 0 || !HotaruCommonCannonHelper.CanAct(Owner?.Player) ||
            Owner.Player.GetRelic<Millsage>() is not { } relic) return;
        var combat = Owner.CombatState;
        ObserveNativeAmount();
        int remove = Math.Min(count, ClaimedActivations);
        if (remove > 0)
        {
            try { await PowerCmd.ModifyAmount(context, this, -remove, Owner, null); }
            finally { ObserveNativeAmount(); }
        }
        // A claimed batch stays separate from requests created by removal
        // hooks or by its own cannon actions. Those wait for a later turn.
        for (int i = 0; i < count && HotaruCommonCannonHelper.CanAct(Owner.Player) &&
             ReferenceEquals(combat, Owner.CombatState); i++)
        {
            Creature[] cannons = HotaruCommonCannonHelper.GetCannons(Owner.Player);
            if (cannons.Length == 0) break;
            Creature cannon = cannons[Owner.Player.RunState.Rng.Niche.NextInt(cannons.Length)];
            await relic.TriggerAllEffects(cannon, context);
        }
    }
}

[CustomID("HOTARU_SIDE_BY_SIDE_POWER")]
public sealed class HotaruSideBySidePower : PowerModel, ICustomPower
{
    private object _overclockToken;
    private int _overclockPercent;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ExpiringPercent { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryPercent { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryTurnNumber { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override int DisplayAmount => IsMutable ? ActivePercent : 10;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal int ActivePercent
    {
        get
        {
            if (!IsMutable || Amount <= 0 || Owner?.Player?.PlayerCombatState == null ||
                !HotaruCommonCannonHelper.CanAct(Owner.Player)) return 0;
            object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
            int percent = token == null
                ? OrdinaryTurnNumber == Owner.Player.PlayerCombatState.TurnNumber ? OrdinaryPercent : 0
                : ReferenceEquals(token, _overclockToken) ? _overclockPercent : 0;
            return Math.Clamp(percent, 0, Amount);
        }
    }

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("Amount", IsMutable ? ActivePercent : 10);
            return description;
        }
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        ActivePercent > 0 && HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer,
            cardSource, props, out Creature cannon) && Millsage.IsOwnedCannon(cannon, Owner?.Player)
            ? HbrgoSakiDamageMultiplierHelper.Add(1m + ActivePercent / 100m, this) : 1m;

    public static async Task Apply(PlayerChoiceContext context, Creature owner, int percent, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(owner?.Player) || percent <= 0) return;
        var power = owner.GetPower<HotaruSideBySidePower>();
        bool fresh = power == null;
        power ??= (HotaruSideBySidePower)ModelDb.Power<HotaruSideBySidePower>().ToMutable();
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner.Player);
        int tracked = (int)Math.Min(999_999_999L,
            (long)power.OrdinaryPercent + power._overclockPercent + power.ExpiringPercent);
        int increase = (int)Math.Min(percent, 999_999_999L - tracked);
        if (increase <= 0) return;
        if (token == null)
        {
            power.OrdinaryTurnNumber = owner.Player.PlayerCombatState.TurnNumber;
            power.OrdinaryPercent += increase;
        }
        else
        {
            if (!ReferenceEquals(token, power._overclockToken)) power._overclockPercent = 0;
            power._overclockToken = token;
            power._overclockPercent += increase;
        }
        try
        {
            if (fresh) await PowerCmd.Apply(context, power, owner, increase, owner, source);
            else await PowerCmd.ModifyAmount(context, power, increase, owner, source);
        }
        finally { power.AlignPercent(token, owner.Player.PlayerCombatState.TurnNumber, expiringFirst: false); }
        await power.Refresh();
    }

    private void AlignPercent(object token, int turn, bool expiringFirst)
    {
        long difference = Math.Max(0, Amount) -
            ((long)OrdinaryPercent + _overclockPercent + ExpiringPercent);
        if (difference > 0)
        {
            int increase = (int)Math.Min(999_999_999L, difference);
            if (token == null)
            {
                OrdinaryTurnNumber = turn;
                OrdinaryPercent = (int)Math.Min(999_999_999L, (long)OrdinaryPercent + increase);
            }
            else
            {
                if (!ReferenceEquals(token, _overclockToken)) _overclockPercent = 0;
                _overclockToken = token;
                _overclockPercent = (int)Math.Min(999_999_999L, (long)_overclockPercent + increase);
            }
            return;
        }
        int remove = (int)Math.Min(int.MaxValue, -difference);
        if (expiringFirst)
        {
            int expiring = Math.Min(remove, ExpiringPercent);
            ExpiringPercent -= expiring;
            remove -= expiring;
        }
        if (token != null && ReferenceEquals(token, _overclockToken))
        {
            int overclock = Math.Min(remove, _overclockPercent);
            _overclockPercent -= overclock;
            remove -= overclock;
        }
        int ordinary = Math.Min(remove, OrdinaryPercent);
        OrdinaryPercent -= ordinary;
        remove -= ordinary;
        int remainingOd = Math.Min(remove, _overclockPercent);
        _overclockPercent -= remainingOd;
        remove -= remainingOd;
        ExpiringPercent = Math.Max(0, ExpiringPercent - remove);
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this) && Owner?.Player?.PlayerCombatState != null)
            AlignPercent(OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player),
                Owner.Player.PlayerCombatState.TurnNumber, expiringFirst: amount < 0m);
        return Task.CompletedTask;
    }

    internal async Task OnTurnStart(PlayerChoiceContext context, object token)
    {
        if (_overclockToken != null && !ReferenceEquals(token, _overclockToken))
            await OnTurnEnd(context, _overclockToken);
        // OD sessions are not serialized. Trim their orphaned native proxy on
        // load without losing the saved suspended ordinary-turn bonus.
        int tracked = (int)Math.Min(999_999_999L,
            (long)OrdinaryPercent + _overclockPercent + ExpiringPercent);
        if (Amount > tracked) await PowerCmd.ModifyAmount(context, this, tracked - Amount, Owner, null);
        if (token == null && OrdinaryPercent > 0 &&
            OrdinaryTurnNumber != Owner.Player.PlayerCombatState.TurnNumber)
            await OnTurnEnd(context, null);
        await Refresh();
    }

    internal async Task OnTurnEnd(PlayerChoiceContext context, object token)
    {
        int expired;
        if (token == null)
        {
            expired = OrdinaryPercent;
            OrdinaryPercent = 0;
        }
        else
        {
            if (!ReferenceEquals(token, _overclockToken)) return;
            expired = _overclockPercent;
            _overclockPercent = 0;
            _overclockToken = null;
        }
        if (expired > 0 && Amount > 0)
        {
            // Keep the expiring native portion accounted for while removal
            // hooks enqueue a fresh buff. It never contributes to damage.
            ExpiringPercent = (int)Math.Min(999_999_999L, (long)ExpiringPercent + expired);
            try { await PowerCmd.ModifyAmount(context, this, -Math.Min(expired, Amount), Owner, null); }
            finally
            {
                AlignPercent(OverclockImmediateTurnCoordinator.GetTurnToken(Owner?.Player),
                    Owner?.Player?.PlayerCombatState?.TurnNumber ?? 0, expiringFirst: true);
            }
        }
        await Refresh();
    }

    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Player != null && side == Owner.Side && participants.Contains(Owner) &&
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) == null
            ? OnTurnEnd(context, null) : Task.CompletedTask;

    private async Task Refresh()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(HotaruSideBySidePower));
        if (Owner?.Player == null) return;
        foreach (Creature cannon in HotaruCommonCannonHelper.GetCannons(Owner.Player))
            await CombatPreviewRefreshHelper.RefreshCreatureIntent(cannon);
    }
}
