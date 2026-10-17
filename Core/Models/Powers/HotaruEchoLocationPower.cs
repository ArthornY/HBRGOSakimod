using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_ECHO_LOCATION_POWER")]
public sealed class HotaruEchoLocationPower : PowerModel, ICustomPower
{
    private const int Maximum = 999_999_999;
    private object _overclockToken;
    private int _overclockPoints;
    private Dictionary<CardModel, int> _ordinaryReservations = new();
    private Dictionary<CardModel, int> _overclockReservations = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryPoints { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryTurnNumber { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F8D36A");
    public override int DisplayAmount => IsMutable ? PendingPoints : 15;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Amount", IsMutable ? PendingPoints : 15);
            return text;
        }
    }

    private bool HasActiveTurn(out object token)
    {
        token = null;
        if (!IsMutable || Amount <= 0 || Owner?.IsAlive != true || Owner.CombatState == null ||
            Owner.Player?.PlayerCombatState == null || CombatManager.Instance.IsOverOrEnding) return false;
        token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        return token == null
            ? OrdinaryTurnNumber == Owner.Player.PlayerCombatState.TurnNumber
            : ReferenceEquals(token, _overclockToken);
    }

    private int PendingPoints
    {
        get
        {
            if (!HasActiveTurn(out object token)) return 0;
            int points = token == null ? OrdinaryPoints : _overclockPoints;
            Dictionary<CardModel, int> reservations = token == null ? _ordinaryReservations : _overclockReservations;
            long reserved = reservations.Values.Sum(value => (long)value);
            return (int)Math.Clamp((long)Math.Min(points, Amount) - reserved, 0L, Maximum);
        }
    }

    internal static int GetProjectedBonus(Player player) =>
        player?.Creature?.GetPower<HotaruEchoLocationPower>()?.PendingPoints ?? 0;

    internal int GetRollBonus(CardModel card)
    {
        if (card?.Owner?.Creature != Owner || !HasActiveTurn(out object token)) return 0;
        Dictionary<CardModel, int> reservations = token == null ? _ordinaryReservations : _overclockReservations;
        if (reservations.TryGetValue(card, out int reserved)) return reserved;
        int points = PendingPoints;
        if (points <= 0) return 0;
        reservations.Add(card, points);
        Flash();
        Refresh();
        return points;
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        // A card without a roll still occupies the next-card slot. The card
        // that grants this power has already passed this hook before OnPlay.
        GetRollBonus(play?.Card);
        return Task.CompletedTask;
    }

    internal static async Task Apply(PlayerChoiceContext context, Creature owner, decimal amount, CardModel source)
    {
        int points = (int)Math.Clamp(decimal.Floor(amount), 0m, Maximum);
        if (points <= 0 || owner?.IsAlive != true || owner.Player?.PlayerCombatState == null ||
            owner.CombatState == null || CombatManager.Instance.IsOverOrEnding) return;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner.Player);
        int turn = owner.Player.PlayerCombatState.TurnNumber;
        HotaruEchoLocationPower power = owner.GetPower<HotaruEchoLocationPower>();
        if (power != null)
        {
            // Also reconcile here in case another effect grants this power
            // before the regular turn-start callbacks have completed.
            await power.OnTurnStart(context, token);
            if (owner.IsAlive != true || CombatManager.Instance.IsOverOrEnding) return;
            power = owner.GetPower<HotaruEchoLocationPower>();
        }
        if (power == null)
        {
            power = (HotaruEchoLocationPower)ModelDb.Power<HotaruEchoLocationPower>().ToMutable();
            power.AddPoints(token, points, turn);
            try { await PowerCmd.Apply(context, power, owner, points, owner, source); }
            finally { power.AlignPoints(token, turn); }
        }
        else
        {
            power.AddPoints(token, points, turn);
            try { await PowerCmd.ModifyAmount(context, power, points, owner, source); }
            finally { power.AlignPoints(token, turn); }
        }
        power.Refresh();
    }

    private void AddPoints(object token, int points, int turn)
    {
        if (points <= 0) return;
        if (token == null)
        {
            if (OrdinaryTurnNumber != turn)
            {
                OrdinaryPoints = 0;
                _ordinaryReservations.Clear();
            }
            OrdinaryTurnNumber = turn;
            OrdinaryPoints = (int)Math.Min(Maximum, (long)OrdinaryPoints + points);
        }
        else
        {
            if (!ReferenceEquals(token, _overclockToken))
            {
                _overclockPoints = 0;
                _overclockReservations.Clear();
            }
            _overclockToken = token;
            _overclockPoints = (int)Math.Min(Maximum, (long)_overclockPoints + points);
        }
    }

    private void AlignPoints(object token, int turn)
    {
        long difference = Math.Max(0, Amount) - ((long)OrdinaryPoints + _overclockPoints);
        if (difference > 0)
        {
            AddPoints(token, (int)Math.Min(Maximum, difference), turn);
            return;
        }
        int remove = (int)Math.Min(Maximum, -difference);
        if (token != null && ReferenceEquals(token, _overclockToken))
        {
            int fromOd = Math.Min(remove, _overclockPoints);
            _overclockPoints -= fromOd;
            remove -= fromOd;
        }
        int fromOrdinary = Math.Min(remove, OrdinaryPoints);
        OrdinaryPoints -= fromOrdinary;
        _overclockPoints = Math.Max(0, _overclockPoints - (remove - fromOrdinary));
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source)
    {
        if (ReferenceEquals(power, this) && Owner?.Player?.PlayerCombatState != null)
            AlignPoints(OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player), Owner.Player.PlayerCombatState.TurnNumber);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play?.Card?.Owner?.Creature != Owner || !HasActiveTurn(out object token)) return;
        Dictionary<CardModel, int> reservations = token == null ? _ordinaryReservations : _overclockReservations;
        if (!reservations.Remove(play.Card, out int points)) return;
        if (token == null) OrdinaryPoints = Math.Max(0, OrdinaryPoints - points);
        else _overclockPoints = Math.Max(0, _overclockPoints - points);
        if (Amount > 0) await PowerCmd.ModifyAmount(context, this, -Math.Min(points, Amount), Owner, null);
        Refresh();
    }

    private async Task OnTurnStart(PlayerChoiceContext context, object token)
    {
        if (_overclockToken != null && !ReferenceEquals(token, _overclockToken))
            await OnTurnEnd(context, _overclockToken);
        int tracked = (int)Math.Min(Maximum, (long)OrdinaryPoints + _overclockPoints);
        if (Amount > tracked) await PowerCmd.ModifyAmount(context, this, tracked - Amount, Owner, null);
        if (token == null && OrdinaryPoints > 0 && OrdinaryTurnNumber != Owner.Player.PlayerCombatState.TurnNumber)
            await OnTurnEnd(context, null);
        Refresh();
    }

    private async Task OnTurnEnd(PlayerChoiceContext context, object token)
    {
        int expired;
        if (token == null)
        {
            expired = OrdinaryPoints;
            OrdinaryPoints = 0;
            _ordinaryReservations.Clear();
        }
        else
        {
            if (!ReferenceEquals(token, _overclockToken)) return;
            expired = _overclockPoints;
            _overclockPoints = 0;
            _overclockToken = null;
            _overclockReservations.Clear();
        }
        if (expired > 0 && Amount > 0)
            await PowerCmd.ModifyAmount(context, this, -Math.Min(expired, Amount), Owner, null);
        Refresh();
    }

    internal static Task TurnStart(PlayerChoiceContext context, Player player, object token) =>
        player?.Creature?.GetPower<HotaruEchoLocationPower>()?.OnTurnStart(context, token) ?? Task.CompletedTask;

    internal static Task TurnEnd(PlayerChoiceContext context, Player player, object token) =>
        player?.Creature?.GetPower<HotaruEchoLocationPower>()?.OnTurnEnd(context, token) ?? Task.CompletedTask;

    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Player != null && side == Owner.Side && participants.Contains(Owner) &&
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) == null
            ? OnTurnEnd(context, null) : Task.CompletedTask;

    private void Refresh()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(HotaruEchoLocationPower));
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _ordinaryReservations = new();
        _overclockReservations = new();
        _overclockToken = null;
        _overclockPoints = 0;
    }
}
