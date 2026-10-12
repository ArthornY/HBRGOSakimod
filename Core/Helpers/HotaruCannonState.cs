using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Helpers;

public enum HotaruCannonKind { Happiness, Coffin, Contest, Longing, Absolution }
public enum HotaruCannonPhase { Start, End }

// Pure combat state: no Godot objects, RNG, or game commands are persisted.
public sealed class HotaruCannonState
{
    public const decimal MaximumStoredValue = 18_446_744_073_709_551_616m;

    [JsonInclude] public decimal HappinessDamage { get; private set; }
    [JsonInclude] public decimal NextHappinessIncrease { get; private set; } = 1m;
    [JsonInclude] public int PendingActionRepeats { get; private set; }
    [JsonInclude] public string StartRollData { get; private set; } = string.Empty;
    [JsonInclude] public string EndRollData { get; private set; } = string.Empty;

    internal string GetRollData(HotaruCannonPhase phase) =>
        phase == HotaruCannonPhase.Start ? StartRollData : EndRollData;

    internal void SetRollData(HotaruCannonPhase phase, string data)
    {
        if (phase == HotaruCannonPhase.Start) StartRollData = data ?? string.Empty;
        else EndRollData = data ?? string.Empty;
    }

    internal static bool HasEffect(HotaruCannonKind kind, HotaruCannonPhase phase) => phase switch
    {
        HotaruCannonPhase.Start => kind is HotaruCannonKind.Coffin or HotaruCannonKind.Longing or HotaruCannonKind.Absolution,
        HotaruCannonPhase.End => kind != HotaruCannonKind.Coffin,
        _ => false
    };

    public void Charge(HotaruCannonKind kind, int extraTriggers = 1)
    {
        if (extraTriggers <= 0 || !Enum.IsDefined(kind)) return;
        PendingActionRepeats = (int)Math.Min(int.MaxValue - 1L,
            (long)PendingActionRepeats + extraTriggers);
    }

    public int TakeActionCount()
    {
        int count = PendingActionRepeats + 1;
        // Claim once before any command can await. A charge gained during this
        // action belongs to the next action, rather than extending this loop.
        PendingActionRepeats = 0;
        return count;
    }

    public decimal GrowHappiness(decimal baseValueBonus = 0m)
    {
        decimal increase = Math.Min(MaximumStoredValue,
            NextHappinessIncrease + Math.Clamp(baseValueBonus, 0m, MaximumStoredValue));
        HappinessDamage = Math.Min(MaximumStoredValue, HappinessDamage + increase);
        // Every repeated complete growth advances its own sequence step.
        NextHappinessIncrease = Math.Min(MaximumStoredValue, NextHappinessIncrease + 1m);
        return increase;
    }

    public void HalveHappinessDamage() => HappinessDamage /= 2m;

    public void HalveHappinessIncreaseAfterDamage() =>
        NextHappinessIncrease = decimal.Floor(NextHappinessIncrease / 2m);

    internal void InitializeHappinessIncrease(decimal amount) =>
        NextHappinessIncrease = decimal.Floor(Math.Clamp(amount, 0m, MaximumStoredValue));

    internal void AddHappinessIncrease(decimal amount) =>
        NextHappinessIncrease = Math.Min(MaximumStoredValue, NextHappinessIncrease + Math.Max(0m, amount));

    internal void MigrateLegacyCharges(int startCharges, int endCharges) =>
        PendingActionRepeats = Math.Clamp(Math.Max(PendingActionRepeats,
            Math.Max(startCharges, endCharges)), 0, int.MaxValue - 1);

    internal void Normalize()
    {
        HappinessDamage = Math.Clamp(HappinessDamage, 0m, MaximumStoredValue);
        NextHappinessIncrease = decimal.Floor(Math.Clamp(NextHappinessIncrease, 0m, MaximumStoredValue));
        PendingActionRepeats = Math.Clamp(PendingActionRepeats, 0, int.MaxValue - 1);
        StartRollData ??= string.Empty;
        EndRollData ??= string.Empty;
    }
}

public sealed class HotaruCannonCombatState
{
    public int PhaseVersion { get; set; } = 2;
    public HotaruCannonState[] Cannons { get; set; } =
        Enumerable.Range(0, 5).Select(_ => new HotaruCannonState()).ToArray();
    public int LastNaturalStartTurn { get; set; }
    public int LastNaturalEndTurn { get; set; }
    public bool EndRewardPending { get; set; }

    public HotaruCannonState For(HotaruCannonKind kind) => Cannons[(int)kind];
    public string ToJson() => JsonSerializer.Serialize(this);

    public static HotaruCannonCombatState FromJson(string data)
    {
        HotaruCannonCombatState state;
        try { state = JsonSerializer.Deserialize<HotaruCannonCombatState>(data ?? string.Empty); }
        catch (JsonException) { state = null; }
        state ??= new HotaruCannonCombatState();
        HotaruCannonState[] saved = state.Cannons ?? Array.Empty<HotaruCannonState>();
        state.Cannons = Enumerable.Range(0, 5)
            .Select(index => index < saved.Length ? saved[index] ?? new HotaruCannonState() : new HotaruCannonState())
            .ToArray();
        foreach (HotaruCannonState cannon in state.Cannons) cannon.Normalize();
        try
        {
            using JsonDocument document = JsonDocument.Parse(data ?? string.Empty);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(nameof(Cannons), out JsonElement cannons) &&
                cannons.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement cannon in cannons.EnumerateArray())
                {
                    if (index >= state.Cannons.Length) break;
                    if (cannon.ValueKind == JsonValueKind.Object &&
                        !cannon.TryGetProperty(nameof(HotaruCannonState.PendingActionRepeats), out _))
                    {
                        int start = ReadLegacyCount(cannon, "PendingStartCharges");
                        int end = ReadLegacyCount(cannon, "PendingEndCharges");
                        state.Cannons[index].MigrateLegacyCharges(start, end);
                    }
                    index++;
                }
            }
        }
        catch (JsonException) { }
        state.PhaseVersion = 2;
        state.LastNaturalStartTurn = Math.Max(0, state.LastNaturalStartTurn);
        state.LastNaturalEndTurn = Math.Max(0, state.LastNaturalEndTurn);
        return state;
    }

    private static int ReadLegacyCount(JsonElement cannon, string name) =>
        cannon.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out int count)
            ? Math.Max(0, count) : 0;
}
