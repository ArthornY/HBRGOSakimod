using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class LargeCombatSnapshotHelper
{
    internal const string Protocol = "HBRGO_EXACT_COMBAT";
    private const int ProtocolVersion = 1;
    private const string NumberFormat = "0.############################";
    private sealed record FrozenSnapshot(string Json);
    private static readonly ConditionalWeakTable<NetFullCombatState, FrozenSnapshot> Snapshots = new();

    // FromRun is synchronous. Each nested capture restores its caller's scope;
    // no async execution context or live Creature survives in a frozen snapshot.
    [ThreadStatic] private static CaptureScope _capture;

    internal sealed class ExactPower
    {
        public int PowerIndex { get; set; }
        public string PowerId { get; set; }
        public string CurrentDp { get; set; }
        public string MaximumDp { get; set; }
    }

    internal sealed class ExactCreature
    {
        public int Index { get; set; }
        public int CombatIndex { get; set; }
        public uint? CombatId { get; set; }
        public int? PlayerIndex { get; set; }
        public int? PetOwnerIndex { get; set; }
        public string MonsterId { get; set; }
        public string CurrentHp { get; set; }
        public string MaxHp { get; set; }
        public List<ExactPower> DpPowers { get; set; } = new();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ExactDamageCap> DamageCaps { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ExactTemporaryDamagePool> TemporaryDamagePools { get; set; }
    }

    internal sealed class ExactDamageCap
    {
        public int PowerIndex { get; set; }
        public string PowerId { get; set; }
        public string MaximumDamageCap { get; set; }
        public string ReceivedThisTurn { get; set; }
    }

    internal sealed class ExactTemporaryDamagePool
    {
        public int PowerIndex { get; set; }
        public string PowerId { get; set; }
        public List<ExactTemporaryDamageValue> Values { get; set; } = new();
    }

    internal sealed class ExactTemporaryDamageValue
    {
        public string Name { get; set; }
        public string Percent { get; set; }
    }

    internal sealed class Envelope
    {
        [JsonRequired]
        public string Protocol { get; set; } = LargeCombatSnapshotHelper.Protocol;
        [JsonRequired]
        public int Version { get; set; } = ProtocolVersion;
        [JsonRequired]
        public List<ExactCreature> Creatures { get; set; } = new();
    }

    internal sealed class CapturedCreature
    {
        internal Creature Creature;
        internal ExactCreature Exact;
        internal int CurrentProxy;
        internal int MaximumProxy;
        internal bool MaximumCaptured;
        internal int PowerCount;
    }

    internal sealed class CaptureScope
    {
        internal CaptureScope Previous;
        internal Player[] Players;
        internal List<CapturedCreature> Creatures = new();
    }

    internal static CaptureScope BeginCapture(IRunState run)
    {
        CaptureScope scope = new() { Previous = _capture, Players = run.Players.ToArray() };
        _capture = scope;
        return scope;
    }

    internal static void EndCapture(CaptureScope scope)
    {
        if (scope != null && ReferenceEquals(_capture, scope)) _capture = scope.Previous;
    }

    internal static int CaptureCurrentHp(Creature creature)
    {
        CaptureScope scope = _capture ?? throw new InvalidOperationException("Missing exact combat capture scope.");
        int proxy = creature.CurrentHp;
        ExactCreature exact = IdentifyCreature(scope, creature, scope.Creatures.Count, scope.Creatures.Count);
        exact.CurrentHp = Format(LargeCreatureHealthHelper.GetCurrentHp(creature));
        scope.Creatures.Add(new CapturedCreature { Creature = creature, Exact = exact, CurrentProxy = proxy });
        return proxy;
    }

    internal static int CaptureMaxHp(Creature creature)
    {
        CapturedCreature captured = CurrentCreature();
        if (!ReferenceEquals(captured.Creature, creature) || captured.MaximumCaptured)
            throw new InvalidOperationException("Unexpected creature HP capture order.");
        int proxy = creature.MaxHp;
        captured.Exact.MaxHp = Format(LargeCreatureHealthHelper.GetMaxHp(creature));
        captured.MaximumProxy = proxy;
        captured.MaximumCaptured = true;
        return proxy;
    }

    internal static int CapturePowerAmount(PowerModel power)
    {
        CapturedCreature captured = CurrentCreature();
        if (!captured.MaximumCaptured)
            throw new InvalidOperationException("Power captured before creature HP.");
        int index = captured.PowerCount++;
        if (power is HighDimensionalShellPower shell)
            captured.Exact.DpPowers.Add(CaptureDp(shell, index));
        CaptureDamageCap(captured.Exact, power, index);
        CaptureTemporaryDamage(captured.Exact, power, index);
        return power.Amount;
    }

    private static CapturedCreature CurrentCreature() =>
        _capture?.Creatures.LastOrDefault() ?? throw new InvalidOperationException("Missing captured creature.");

    internal static void FinishCapture(CaptureScope scope, NetFullCombatState state)
    {
        if (!ReferenceEquals(scope, _capture) || scope.Creatures.Count != state.Creatures.Count)
            throw new InvalidOperationException("Native and exact combat creature counts differ.");
        Envelope envelope = new();
        HashSet<Creature> seen = new(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < scope.Creatures.Count; index++)
        {
            CapturedCreature captured = scope.Creatures[index];
            NetFullCombatState.CreatureState native = state.Creatures[index];
            if (!captured.MaximumCaptured || native.currentHp != captured.CurrentProxy ||
                native.maxHp != captured.MaximumProxy || native.powers.Count != captured.PowerCount ||
                native.monsterId?.ToString() != captured.Exact.MonsterId ||
                native.playerId != captured.Creature.Player?.NetId)
                throw new InvalidOperationException("Native and exact combat creature order differs.");
            envelope.Creatures.Add(captured.Exact);
            seen.Add(captured.Creature);
        }
        // Combat.Creatures normally includes these, but capture every player's
        // creature and every pet even outside combat or while a pet is detached.
        foreach (Player player in scope.Players)
        {
            AddExtra(player.Creature);
            foreach (Creature pet in player.Creature.Pets) AddExtra(pet);
        }
        Attach(state, envelope);

        void AddExtra(Creature creature)
        {
            if (creature == null || !seen.Add(creature)) return;
            ExactCreature exact = IdentifyCreature(scope, creature, envelope.Creatures.Count, -1);
            exact.CurrentHp = Format(LargeCreatureHealthHelper.GetCurrentHp(creature));
            exact.MaxHp = Format(LargeCreatureHealthHelper.GetMaxHp(creature));
            int powerIndex = 0;
            foreach (PowerModel power in creature.Powers)
            {
                if (power is HighDimensionalShellPower shell) exact.DpPowers.Add(CaptureDp(shell, powerIndex));
                CaptureDamageCap(exact, power, powerIndex);
                CaptureTemporaryDamage(exact, power, powerIndex);
                powerIndex++;
            }
            envelope.Creatures.Add(exact);
        }
    }

    private static ExactCreature IdentifyCreature(CaptureScope scope, Creature creature, int index, int combatIndex) =>
        new()
        {
            Index = index,
            CombatIndex = combatIndex,
            CombatId = creature.CombatId,
            // Run-order indexes carry ownership without copying private NetIds.
            // Anonymization therefore preserves the exact JSON unchanged.
            PlayerIndex = FindPlayer(scope, creature.Player),
            PetOwnerIndex = FindPlayer(scope, creature.PetOwner),
            MonsterId = creature.Monster?.Id.ToString()
        };

    private static int? FindPlayer(CaptureScope scope, Player player)
    {
        if (player == null) return null;
        int index = Array.IndexOf(scope.Players, player);
        if (index < 0) throw new InvalidOperationException("Creature owner is absent from the captured run.");
        return index;
    }

    private static ExactPower CaptureDp(HighDimensionalShellPower shell, int powerIndex) =>
        new()
        {
            PowerIndex = powerIndex,
            PowerId = shell.Id.ToString(),
            CurrentDp = Format(shell.CurrentDp),
            MaximumDp = Format(shell.MaximumDp)
        };

    private static void CaptureDamageCap(ExactCreature creature, PowerModel power, int powerIndex)
    {
        if (!PhantomHealthDamageCapHelper.TryGetMaximum(power, out decimal maximum)) return;
        (creature.DamageCaps ??= new()).Add(new ExactDamageCap
        {
            PowerIndex = powerIndex,
            PowerId = power.Id.ToString(),
            MaximumDamageCap = Format(maximum),
            ReceivedThisTurn = Format(power is HardenedShellPower hardened
                ? PhantomHealthDamageCapHelper.GetReceivedThisTurn(hardened) : 0m)
        });
    }

    private static void CaptureTemporaryDamage(ExactCreature creature, PowerModel power, int powerIndex)
    {
        ExactTemporaryDamagePool pool = null;
        switch (power)
        {
            case PhantomPower phantom:
                Add(nameof(PhantomPower.CombatDamageBonusPercent), phantom.CombatDamageBonusPercent);
                break;
            case PhantomDamageInheritancePower inheritance:
                Add(nameof(PhantomDamageInheritancePower.DamagePercent), inheritance.DamagePercent);
                break;
            case BoostUpFormDamagePower forms:
                Add(nameof(BoostUpFormDamagePower.ExactBowPercent), forms.ExactBowPercent);
                Add(nameof(BoostUpFormDamagePower.ExactDualSwordsPercent), forms.ExactDualSwordsPercent);
                Add(nameof(BoostUpFormDamagePower.ExactScythePercent), forms.ExactScythePercent);
                break;
            case LingeringLifePower lingering:
                Add(nameof(LingeringLifePower.InheritedDamageBonusPercent), lingering.InheritedDamageBonusPercent);
                break;
            case BeyondProtectionPower beyond:
                Add(nameof(BeyondProtectionPower.DamagePercent), beyond.DamagePercent);
                break;
            case LetMeBearItPower bear:
                MomentOfReign relic = bear.IsMutable ? bear.Owner?.Player?.GetRelic<MomentOfReign>() : null;
                Add(nameof(MomentOfReign.LastPhantomDamageBonusPercent), relic?.LastPhantomDamageBonusPercent ?? 0m);
                Add(nameof(MomentOfReign.LastPhantomDamageReductionPercent), relic?.LastPhantomDamageReductionPercent ?? 0m);
                break;
        }
        if (pool != null) (creature.TemporaryDamagePools ??= new()).Add(pool);

        void Add(string name, decimal percent)
        {
            pool ??= new ExactTemporaryDamagePool { PowerIndex = powerIndex, PowerId = power.Id.ToString() };
            // Preserve the bank rather than its current effective request. The
            // phantom raw envelope can exceed the combined percentage limit;
            // active weapon state and later aggregate clipping are separate.
            pool.Values.Add(new ExactTemporaryDamageValue { Name = name, Percent = Format(percent) });
        }
    }

    internal static string Format(decimal amount) => amount.ToString(NumberFormat, CultureInfo.InvariantCulture);

    internal static void Attach(NetFullCombatState state, Envelope envelope)
    {
        Validate(state, envelope);
        Store(state, JsonSerializer.Serialize(envelope));
    }

    private static void Store(NetFullCombatState state, string json)
    {
        lock (Snapshots)
        {
            Snapshots.Remove(state);
            Snapshots.Add(state, new FrozenSnapshot(json));
        }
    }

    internal static string GetFrozenJson(NetFullCombatState state) =>
        Snapshots.TryGetValue(state, out FrozenSnapshot snapshot) ? snapshot.Json :
            throw new InvalidOperationException("Exact combat snapshot is missing. Capture through NetFullCombatState.FromRun; matching HBRGOSakiMod protocol versions are required.");

    internal static void Serialize(NetFullCombatState state, PacketWriter writer) =>
        writer.WriteString(GetFrozenJson(state));

    internal static void Deserialize(NetFullCombatState state, PacketReader reader)
    {
        Envelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(reader.ReadString());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Missing or incompatible HBRGO exact combat snapshot extension.", exception);
        }
        Attach(state, envelope);
    }

    internal static void Copy(NetFullCombatState source, NetFullCombatState target) =>
        Store(target, GetFrozenJson(source));

    internal static string Describe(NetFullCombatState state) =>
        Snapshots.TryGetValue(state, out FrozenSnapshot snapshot)
            ? "HBRGO exact HP/DP: " + snapshot.Json
            : "HBRGO exact HP/DP: snapshot unavailable";

    private static void Validate(NetFullCombatState state, Envelope envelope)
    {
        if (envelope == null || envelope.Protocol != Protocol || envelope.Version != ProtocolVersion ||
            envelope.Creatures == null || envelope.Creatures.Count < state.Creatures.Count)
            throw new InvalidDataException("Incompatible HBRGO exact combat snapshot protocol.");
        for (int index = 0; index < envelope.Creatures.Count; index++)
        {
            ExactCreature exact = envelope.Creatures[index];
            bool combat = index < state.Creatures.Count;
            if (exact == null || exact.Index != index || exact.CombatIndex != (combat ? index : -1) ||
                exact.PlayerIndex is < 0 || exact.PetOwnerIndex is < 0 || exact.DpPowers == null)
                throw new InvalidDataException("Invalid exact creature snapshot order.");
            ValidateNumber(exact.CurrentHp);
            ValidateNumber(exact.MaxHp);
            int previousPowerIndex = -1;
            foreach (ExactPower power in exact.DpPowers)
            {
                if (power == null || power.PowerIndex <= previousPowerIndex || string.IsNullOrEmpty(power.PowerId))
                    throw new InvalidDataException("Invalid exact DP snapshot order.");
                previousPowerIndex = power.PowerIndex;
                ValidateNumber(power.CurrentDp);
                ValidateNumber(power.MaximumDp);
                if (combat && (power.PowerIndex >= state.Creatures[index].powers.Count ||
                    state.Creatures[index].powers[power.PowerIndex].id.ToString() != power.PowerId))
                    throw new InvalidDataException("Native and exact DP snapshot identities differ.");
            }
            if (combat && state.Creatures[index].monsterId?.ToString() != exact.MonsterId)
                throw new InvalidDataException("Native and exact creature snapshot identities differ.");
            previousPowerIndex = -1;
            foreach (ExactDamageCap cap in exact.DamageCaps ?? Enumerable.Empty<ExactDamageCap>())
            {
                if (cap == null || cap.PowerIndex <= previousPowerIndex || string.IsNullOrEmpty(cap.PowerId))
                    throw new InvalidDataException("Invalid exact damage-cap snapshot order.");
                previousPowerIndex = cap.PowerIndex;
                ValidateNumber(cap.MaximumDamageCap);
                ValidateNumber(cap.ReceivedThisTurn);
                if (combat && (cap.PowerIndex >= state.Creatures[index].powers.Count ||
                    state.Creatures[index].powers[cap.PowerIndex].id.ToString() != cap.PowerId))
                    throw new InvalidDataException("Native and exact damage-cap snapshot identities differ.");
            }
            previousPowerIndex = -1;
            foreach (ExactTemporaryDamagePool pool in exact.TemporaryDamagePools ?? Enumerable.Empty<ExactTemporaryDamagePool>())
            {
                if (pool == null || pool.PowerIndex <= previousPowerIndex || string.IsNullOrEmpty(pool.PowerId) ||
                    pool.Values == null || pool.Values.Count == 0)
                    throw new InvalidDataException("Invalid exact temporary-damage snapshot order.");
                previousPowerIndex = pool.PowerIndex;
                HashSet<string> names = new(StringComparer.Ordinal);
                foreach (ExactTemporaryDamageValue value in pool.Values)
                {
                    if (value == null || string.IsNullOrEmpty(value.Name) || !names.Add(value.Name))
                        throw new InvalidDataException("Invalid exact temporary-damage bank name.");
                    ValidateNumber(value.Percent);
                }
                if (combat && (pool.PowerIndex >= state.Creatures[index].powers.Count ||
                    state.Creatures[index].powers[pool.PowerIndex].id.ToString() != pool.PowerId))
                    throw new InvalidDataException("Native and exact temporary-damage snapshot identities differ.");
            }
        }
    }

    private static void ValidateNumber(string text)
    {
        if (text == null || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal amount) || amount < 0m || Format(amount) != text)
            throw new InvalidDataException("Exact combat amounts must be canonical nonnegative decimal strings.");
    }
}
