using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_THIRD_KNOCK_POWER")]
public sealed class HotaruThirdKnockPower : HotaruUncommonCardPower { }

[CustomID("HOTARU_UNTIL_DAWN_POWER")]
public sealed class HotaruUntilDawnPower : HotaruUncommonCardPower
{
    public sealed class Order
    {
        public int Energy { get; set; }
        public int CreatedTurn { get; set; }
        public bool CreatedDuringOverclock { get; set; }
        [JsonIgnore] public object CreatedToken { get; set; }
    }

    private List<Order> _orders = new();
    private HashSet<object> _startedOverclockTurns = new(ReferenceEqualityComparer.Instance);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastOrdinaryStart { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string OrdersData
    {
        get => JsonSerializer.Serialize(_orders);
        private set
        {
            try { _orders = JsonSerializer.Deserialize<List<Order>>(value ?? "") ?? new(); }
            catch (JsonException) { _orders = new(); }
            _orders = _orders.Where(order => order != null && order.Energy > 0).ToList();
        }
    }

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            int energy = IsMutable ? (int)Math.Min(int.MaxValue,
                _orders.Take(Math.Max(0, Amount)).Sum(order => (long)order.Energy)) : 2;
            string prefix = EnergyIconHelper.GetPrefix(this);
            string icon = $"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]";
            text.Add("EnergyIcons", string.Concat(Enumerable.Repeat(icon, energy)));
            return text;
        }
    }

    internal static async Task Apply(PlayerChoiceContext context, Creature owner, int energy, CardModel source)
    {
        if (energy <= 0 || !HotaruCommonCannonHelper.CanAct(owner?.Player) || owner.Player.PlayerCombatState == null) return;
        var combat = owner.CombatState;
        int turn = owner.Player.PlayerCombatState.TurnNumber;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner.Player);
        var power = owner.GetPower<HotaruUntilDawnPower>();
        bool newlyCreated = power == null;
        int before = power?.Amount ?? 0;
        await PowerCmd.Apply<HotaruUntilDawnPower>(context, owner, 1, owner, source);
        power = owner.GetPower<HotaruUntilDawnPower>();
        int actual = Math.Max(0, (power?.Amount ?? 0) - before);
        if (actual <= 0 || !ReferenceEquals(combat, owner.CombatState)) return;
        for (int i = 0; i < actual; i++) power._orders.Add(new Order
        {
            Energy = energy, CreatedTurn = turn, CreatedDuringOverclock = token != null, CreatedToken = token
        });
        // A new order never belongs to the start of the turn in which it was
        // played, including automatic cards played by another start callback.
        if (newlyCreated)
        {
            power.LastOrdinaryStart = turn;
            if (token != null) power._startedOverclockTurns.Add(token);
        }
        owner.InvokePowerModified(power, 0, true);
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source)
    {
        // Removed native stacks cannot return as old orders when a later card
        // adds another stack to this same power instance.
        if (ReferenceEquals(power, this) && amount < 0 && _orders.Count > Math.Max(0, Amount))
            _orders.RemoveRange(Math.Max(0, Amount), _orders.Count - Math.Max(0, Amount));
        return Task.CompletedTask;
    }

    internal async Task OnTurnStart(PlayerChoiceContext context, object token)
    {
        if (!HotaruCommonCannonHelper.CanAct(Owner?.Player) || Owner.Player.PlayerCombatState == null) return;
        int turn = Owner.Player.PlayerCombatState.TurnNumber;
        if (token == null)
        {
            if (LastOrdinaryStart == turn) return;
            LastOrdinaryStart = turn;
        }
        else if (!_startedOverclockTurns.Add(token)) return;
        var claimed = _orders.Take(Math.Max(0, Amount)).Where(order => token == null
            ? order.CreatedDuringOverclock || order.CreatedTurn != turn
            : !ReferenceEquals(order.CreatedToken, token)).ToArray();
        foreach (Order order in claimed) _orders.Remove(order);
        if (claimed.Length == 0) return;
        var player = Owner.Player;
        var combat = Owner.CombatState;
        await PowerCmd.ModifyAmount(context, this, -claimed.Length, Owner, null);
        foreach (Order order in claimed)
        {
            if (!HotaruCommonCannonHelper.CanAct(player) || !ReferenceEquals(combat, player.Creature.CombatState)) return;
            await PlayerCmd.GainEnergy(order.Energy, player);
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _orders = _orders.Select(order => new Order
        {
            Energy = order.Energy, CreatedTurn = order.CreatedTurn,
            CreatedDuringOverclock = order.CreatedDuringOverclock, CreatedToken = order.CreatedToken
        }).ToList();
        _startedOverclockTurns = new(ReferenceEqualityComparer.Instance);
    }
}
