using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomPlayerBenefitContext
{
    private static readonly AsyncLocal<Scope> Current = new();

    internal static Creature CurrentPhantom => Current.Value?.Phantom;
    internal static IReadOnlyList<Player> Beneficiaries =>
        Current.Value?.Beneficiaries ?? Array.Empty<Player>();

    internal static Scope Begin(Creature phantom, IReadOnlyList<Creature> targets) =>
        Begin(phantom, targets, phantom.PetOwner == null
            ? Array.Empty<Player>() : new[] { phantom.PetOwner });

    internal static Scope Begin(Creature phantom, IReadOnlyList<Creature> targets,
        IEnumerable<Player> beneficiaries, bool multiplyByTargets = false) =>
        new(phantom, targets, beneficiaries, multiplyByTargets);

    internal static bool RecordStatus(Creature target, int count) =>
        Current.Value?.RecordStatus(target, count) == true;

    internal static bool RecordStatuses(IEnumerable<Creature> targets, int count)
    {
        Scope scope = Current.Value;
        if (scope == null)
            return false;
        bool recorded = false;
        foreach (Creature target in targets)
            recorded |= scope.RecordStatus(target, count);
        return recorded;
    }

    internal static void QueueDraw(int count) => Current.Value?.AddDraw(count);
    internal static void QueueEnergy(int count) => Current.Value?.AddEnergy(count);
    internal static void QueueHandLimit(int count) =>
        Current.Value?.AddPower<PhantomHandCapacityPower>(count);
    internal static void QueueDrawPerTurn(int count) =>
        Current.Value?.AddPower<PhantomDrawBenefitPower>(count);
    internal static void QueueMaxEnergy(int count) =>
        Current.Value?.AddPower<PhantomMaxEnergyBenefitPower>(count);
    internal static void QueueTurnBenefit(int turns) =>
        Current.Value?.AddPower<SkullFeatherPhantomTurnBenefitPower>(turns);
    internal static void QueuePower<T>(int count) where T : PowerModel, new() =>
        Current.Value?.AddPower<T>(count);

    internal sealed class Scope : IDisposable
    {
        private readonly Scope _previous;
        private readonly Player[] _beneficiaries;
        private readonly HashSet<Creature> _targets;
        private readonly Dictionary<Creature, int> _statusDraws = new();
        private readonly Dictionary<Type, (int count, Func<Player, int, Task> apply)> _powers = new();
        private readonly int _multiplier;
        private int _draw;
        private int _energy;
        private bool _flushed;

        internal Creature Phantom { get; }
        internal IReadOnlyList<Player> Beneficiaries => _beneficiaries;

        internal Scope(Creature phantom, IReadOnlyList<Creature> targets,
            IEnumerable<Player> beneficiaries, bool multiplyByTargets)
        {
            Phantom = phantom;
            _previous = Current.Value;
            _beneficiaries = beneficiaries.Where(player => player?.Creature != null &&
                    player.Creature.Side == phantom.Side &&
                    ReferenceEquals(player.Creature.CombatState, phantom.CombatState))
                .Distinct().ToArray();
            _targets = targets.Where(target => target != null &&
                    target.Side != phantom.Side &&
                    ReferenceEquals(target.CombatState, phantom.CombatState))
                .ToHashSet();
            _multiplier = multiplyByTargets ? _targets.Count : _targets.Count > 0 ? 1 : 0;
            Current.Value = this;
        }

        internal bool RecordStatus(Creature target, int count)
        {
            if (target == null || !_targets.Contains(target) ||
                target.Player != null || !target.IsAlive || count <= 0)
                return false;
            _statusDraws.TryGetValue(target, out int previous);
            _statusDraws[target] = SaturatingAdd(previous, count);
            return true;
        }

        internal void AddDraw(int count) => _draw = SaturatingAdd(_draw, count);
        internal void AddEnergy(int count) => _energy = SaturatingAdd(_energy, count);

        internal void AddPower<T>(int count) where T : PowerModel, new()
        {
            if (count <= 0)
                return;
            _powers.TryGetValue(typeof(T), out var previous);
            _powers[typeof(T)] = (SaturatingAdd(previous.count, count),
                async (player, amount) =>
                {
                    T power = await PowerCmd.Apply<T>(new ThrowingPlayerChoiceContext(),
                        player.Creature, amount, Phantom, null);
                    if (power is PhantomCardUpgradeBenefitPower upgrade)
                        upgrade.AddCaster(Phantom);
                });
        }

        internal async Task Flush()
        {
            if (_flushed)
                return;
            _flushed = true;
            // Per-target insertion loops describe the same effect for every
            // opponent. Count one recipient's total, including separate piles.
            int statusCount = _statusDraws.Count == 0 ? 0 : _statusDraws.Values.Max();
            int draw = Multiply(SaturatingAdd(_draw, statusCount));
            int energy = Multiply(_energy);
            Scope active = Current.Value;
            Current.Value = _previous;
            try
            {
                foreach (Player player in _beneficiaries.Where(player =>
                    player.Creature.IsAlive && player.PlayerCombatState != null))
                {
                    // Capacity increases must precede drawing cards.
                    foreach (var power in _powers.Values)
                        await power.apply(player, Multiply(power.count));
                    if (energy > 0)
                        await PlayerCmd.GainEnergy(energy, player);
                    if (draw > 0)
                        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), draw, player);
                }
            }
            finally
            {
                Current.Value = active;
            }
        }

        private int Multiply(int count) => (int)Math.Min(int.MaxValue,
            (long)Math.Max(0, count) * _multiplier);

        private static int SaturatingAdd(int current, int count) =>
            (int)Math.Min(int.MaxValue, (long)current + Math.Max(0, count));

        public void Dispose() => Current.Value = _previous;
    }
}
