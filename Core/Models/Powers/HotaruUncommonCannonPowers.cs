using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public abstract class HotaruUncommonCannonPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
}

[CustomID("HOTARU_FINGERTIP_HABIT_POWER")]
public sealed class HotaruFingertipHabitPower : HotaruUncommonCannonPower
{
    [SavedProperty] public int OrdinaryTurn { get; private set; } = -1;
    [SavedProperty] public int OrdinaryMask { get; private set; }
    private Dictionary<object, int> _inserted = new(ReferenceEqualityComparer.Instance);
    protected override void AfterCloned() { base.AfterCloned(); _inserted = new(ReferenceEqualityComparer.Instance); }
    internal int Claim(Creature cannon)
    {
        if (Amount <= 0 || cannon?.Monster is not HBRGOSakiMod.Core.Models.Monsters.HotaruCannonModel model) return 0;
        int turn = Owner.Player.PlayerCombatState.TurnNumber;
        if (OrdinaryTurn != turn) { OrdinaryTurn = turn; OrdinaryMask = 0; _inserted.Clear(); }
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        int mask = token == null ? OrdinaryMask : _inserted.GetValueOrDefault(token);
        int bit = 1 << (int)model.Kind;
        if ((mask & bit) != 0 || System.Numerics.BitOperations.PopCount((uint)mask) >= 2) return 0;
        if (token == null) OrdinaryMask = mask | bit; else _inserted[token] = mask | bit;
        return Amount;
    }
}

[CustomID("HOTARU_FIRST_BEATS_POWER")]
public sealed class HotaruFirstBeatsPower : HotaruUncommonCannonPower
{
    public sealed class Grant
    {
        public int Turn { get; set; }
        public int Gain { get; set; }
        public int Mask { get; set; }
    }
    private List<Grant> _ordinary = new();
    private Dictionary<object, List<Grant>> _inserted = new(ReferenceEqualityComparer.Instance);
    [SavedProperty] public string GrantsData
    {
        get => JsonSerializer.Serialize(_ordinary);
        private set { try { _ordinary = JsonSerializer.Deserialize<List<Grant>>(value ?? "") ?? new(); } catch (JsonException) { _ordinary = new(); } }
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _ordinary = _ordinary.Select(g => new Grant { Turn = g.Turn, Gain = g.Gain, Mask = g.Mask }).ToList();
        _inserted = new(ReferenceEqualityComparer.Instance);
    }
    public static async Task Apply(PlayerChoiceContext context, Creature owner, int gain, CardModel source)
    {
        if (gain <= 0 || !HotaruCommonCannonHelper.CanAct(owner?.Player)) return;
        var power = owner.GetPower<HotaruFirstBeatsPower>();
        int before = power?.Amount ?? 0;
        await PowerCmd.Apply<HotaruFirstBeatsPower>(context, owner, gain, owner, source);
        power = owner.GetPower<HotaruFirstBeatsPower>();
        int actual = Math.Max(0, (power?.Amount ?? 0) - before);
        if (actual == 0) return;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner.Player);
        List<Grant> grants = power._ordinary;
        if (token != null && !power._inserted.TryGetValue(token, out grants)) power._inserted[token] = grants = new();
        grants.Add(new Grant { Turn = owner.Player.PlayerCombatState.TurnNumber, Gain = actual });
    }
    internal decimal Claim(Creature cannon)
    {
        if (Amount <= 0 || cannon?.Monster is not HBRGOSakiMod.Core.Models.Monsters.HotaruCannonModel model) return 0m;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        var grants = token == null ? _ordinary : _inserted.GetValueOrDefault(token);
        if (grants == null) return 0m;
        int bit = 1 << (int)model.Kind;
        decimal gain = 0m;
        foreach (Grant grant in grants)
        {
            if (grant.Turn != Owner.Player.PlayerCombatState.TurnNumber || (grant.Mask & bit) != 0) continue;
            grant.Mask |= bit;
            gain += Math.Max(0, grant.Gain);
        }
        return Math.Min(Math.Max(0, Amount), gain);
    }
    internal async Task Expire(PlayerChoiceContext context, object token)
    {
        List<Grant> grants;
        if (token == null) { grants = _ordinary; _ordinary = new(); }
        else if (!_inserted.Remove(token, out grants)) return;
        int remove = (int)Math.Min(Math.Max(0, Amount), grants.Sum(g => (long)Math.Max(0, g.Gain)));
        if (remove > 0) await PowerCmd.ModifyAmount(context, this, -remove, Owner, null);
    }
    internal async Task RemoveStale(PlayerChoiceContext context)
    {
        int turn = Owner.Player.PlayerCombatState.TurnNumber;
        var old = _ordinary.Where(g => g.Turn != turn).ToArray();
        _ordinary.RemoveAll(g => g.Turn != turn);
        int remove = (int)Math.Min(Math.Max(0, Amount), old.Sum(g => (long)Math.Max(0, g.Gain)));
        if (remove > 0) await PowerCmd.ModifyAmount(context, this, -remove, Owner, null);
    }
    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) =>
        Owner?.Player != null && side == Owner.Side && participants.Contains(Owner) &&
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) == null ? Expire(context, null) : Task.CompletedTask;
}

[CustomID("HOTARU_WAKING_COMMAND_POWER")]
public sealed class HotaruWakingCommandPower : HotaruUncommonCannonPower
{
    public sealed class Order
    {
        public int Energy { get; set; }
        public int Activations { get; set; }
    }
    private List<Order> _orders = new();
    [SavedProperty] public string OrdersData
    {
        get => JsonSerializer.Serialize(_orders);
        private set { try { _orders = JsonSerializer.Deserialize<List<Order>>(value ?? "") ?? new(); } catch (JsonException) { _orders = new(); } }
    }
    public override MegaCrit.Sts2.Core.Localization.LocString Description
    {
        get
        {
            var text = base.Description;
            var active = _orders.Take(Math.Max(0, Amount)).ToArray();
            int energy = IsMutable ? (int)Math.Min(int.MaxValue, active.Sum(o => (long)o.Energy)) : 1;
            int activations = IsMutable ? (int)Math.Min(int.MaxValue, active.Sum(o => (long)o.Activations)) : 1;
            string prefix = MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPrefix(this);
            string icon = $"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]";
            text.Add("EnergyIcons", string.Concat(Enumerable.Repeat(icon, energy)));
            text.Add("Activations", activations);
            return text;
        }
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _orders = _orders.Select(o => new Order { Energy = o.Energy, Activations = o.Activations }).ToList();
    }
    public static async Task Apply(PlayerChoiceContext context, Creature owner, int energy, int activations, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(owner?.Player)) return;
        var power = owner.GetPower<HotaruWakingCommandPower>();
        int before = power?.Amount ?? 0;
        await PowerCmd.Apply<HotaruWakingCommandPower>(context, owner, 1m, owner, source);
        power = owner.GetPower<HotaruWakingCommandPower>();
        int actual = Math.Max(0, (power?.Amount ?? 0) - before);
        for (int i = 0; i < actual; i++) power._orders.Add(new Order { Energy = Math.Max(0, energy), Activations = Math.Max(0, activations) });
    }
    internal async Task Resolve(PlayerChoiceContext context)
    {
        // Claim first. Removal/draw callbacks may enqueue a fresh next-turn order.
        var claimed = _orders.Take(Math.Max(0, Amount)).ToArray();
        _orders.RemoveRange(0, claimed.Length);
        if (claimed.Length == 0) return;
        var player = Owner.Player;
        var combat = Owner.CombatState;
        await PowerCmd.ModifyAmount(context, this, -claimed.Length, Owner, null);
        foreach (Order order in claimed)
        {
            if (!HotaruCommonCannonHelper.CanAct(player) || !ReferenceEquals(combat, player.Creature.CombatState)) return;
            await PlayerCmd.GainEnergy(order.Energy, player);
            for (int i = 0; i < order.Activations && HotaruCommonCannonHelper.CanAct(player); i++)
                await HotaruCommonCannonHelper.SelectAndTrigger(context, player);
        }
    }
}
