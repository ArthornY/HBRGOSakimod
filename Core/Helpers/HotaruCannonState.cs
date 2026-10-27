using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Helpers;

public enum HotaruCannonKind { Happiness, Coffin, Contest, Longing, Absolution, Endgame }
public enum HotaruCannonPhase { Start, End }
public enum HotaruCannonRetirementChoice { None, Sacrifice, Redemption }

internal static class HotaruCannonKinds
{
    internal const int Count = 6;
    internal static readonly IReadOnlyList<HotaruCannonKind> Original = Array.AsReadOnly(new[]
    {
        HotaruCannonKind.Happiness, HotaruCannonKind.Coffin, HotaruCannonKind.Contest,
        HotaruCannonKind.Longing, HotaruCannonKind.Absolution
    });
    internal static bool IsOriginal(HotaruCannonKind kind) => (int)kind is >= 0 and < 5;
}

// Pure combat state: no Godot objects, RNG, or game commands are persisted.
// Saved damage/block powers stay in native compatibility units; percentage
// authoring and text never rescale a loaded ledger.
public sealed class HotaruCannonState
{
    public const decimal MaximumStoredValue = 18_446_744_073_709_551_616m;

    [JsonInclude] public decimal HappinessDamage { get; private set; }
    [JsonInclude] public decimal NextHappinessIncrease { get; private set; } = HotaruCannonSkillPowerHelper.HappinessGrowthStep;
    [JsonInclude] public decimal BaseDamageBonus { get; internal set; }
    [JsonInclude] public decimal BaseBlockBonus { get; internal set; }
    [JsonInclude] public decimal PendingReplyBlock { get; internal set; }
    [JsonInclude] public int PendingReplyDraw { get; internal set; }
    [JsonInclude] public int PendingResistanceLoss { get; internal set; }
    [JsonInclude] public int PendingLightVulnerable { get; internal set; }
    [JsonInclude] public int PendingLightFragile { get; internal set; }
    [JsonInclude] public List<int> PendingRollBonuses { get; internal set; } = new();
    [JsonInclude] public int PendingActionRepeats { get; private set; }
    [JsonInclude] public string StartRollData { get; private set; } = string.Empty;
    [JsonInclude] public string EndRollData { get; private set; } = string.Empty;
    // Revival belongs to this combat snapshot; permanent relic upgrades and
    // retirement choices never receive these temporary values.
    [JsonInclude] public int RecalledUpgradeLevel { get; private set; }
    [JsonInclude] public int RecalledSoulPercent { get; private set; }
    [JsonInclude] public int RecalledSoulMaximumBonus { get; private set; }
    [JsonIgnore] internal bool RecallPending { get; set; }

    internal void InitializeRecall(int upgradeLevel, int soulPercent)
    {
        RecalledUpgradeLevel = Math.Clamp(upgradeLevel, 1, 7);
        RecalledSoulPercent = Math.Max(0, soulPercent);
        RecalledSoulMaximumBonus = 0;
    }

    internal void GainRecalledSoulPower()
    {
        RecalledSoulMaximumBonus = (int)Math.Min(int.MaxValue, (long)RecalledSoulMaximumBonus + 1);
        RecalledSoulPercent = (int)Math.Min(int.MaxValue, (long)RecalledSoulPercent + 1);
    }

    internal string GetRollData(HotaruCannonPhase phase) =>
        phase == HotaruCannonPhase.Start ? StartRollData : EndRollData;

    internal void SetRollData(HotaruCannonPhase phase, string data)
    {
        if (phase == HotaruCannonPhase.Start) StartRollData = data ?? string.Empty;
        else EndRollData = data ?? string.Empty;
    }

    internal static bool HasEffect(HotaruCannonKind kind, HotaruCannonPhase phase) => phase switch
    {
        HotaruCannonPhase.Start => kind is HotaruCannonKind.Coffin or HotaruCannonKind.Longing or HotaruCannonKind.Absolution or HotaruCannonKind.Endgame,
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

    public decimal GrowHappiness(decimal baseValueBonus = 0m, decimal multiplier = 1m)
    {
        decimal increase = Math.Min(MaximumStoredValue,
            NextHappinessIncrease + Math.Clamp(baseValueBonus, 0m, MaximumStoredValue));
        increase = Math.Min(MaximumStoredValue, DamageAmplificationLimits.MultiplySaturated(increase, Math.Max(0m, multiplier)));
        HappinessDamage = Math.Min(MaximumStoredValue, HappinessDamage + increase);
        // Every repeated complete growth advances its own sequence step.
        NextHappinessIncrease = Math.Min(MaximumStoredValue, NextHappinessIncrease + HotaruCannonSkillPowerHelper.HappinessGrowthStep);
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
        BaseDamageBonus = Math.Clamp(BaseDamageBonus, 0m, MaximumStoredValue);
        BaseBlockBonus = Math.Clamp(BaseBlockBonus, 0m, MaximumStoredValue);
        PendingReplyBlock = Math.Clamp(PendingReplyBlock, 0m, MaximumStoredValue);
        PendingReplyDraw = Math.Max(0, PendingReplyDraw);
        PendingResistanceLoss = Math.Max(0, PendingResistanceLoss);
        PendingLightVulnerable = Math.Max(0, PendingLightVulnerable);
        PendingLightFragile = Math.Max(0, PendingLightFragile);
        PendingRollBonuses = (PendingRollBonuses ?? new()).Where(b => b > 0).ToList();
        HappinessDamage = Math.Clamp(HappinessDamage, 0m, MaximumStoredValue);
        NextHappinessIncrease = decimal.Floor(Math.Clamp(NextHappinessIncrease, 0m, MaximumStoredValue));
        PendingActionRepeats = Math.Clamp(PendingActionRepeats, 0, int.MaxValue - 1);
        RecalledUpgradeLevel = Math.Clamp(RecalledUpgradeLevel, 0, 7);
        RecalledSoulPercent = RecalledUpgradeLevel == 0 ? 0 : Math.Max(0, RecalledSoulPercent);
        RecalledSoulMaximumBonus = RecalledUpgradeLevel == 0 ? 0 : Math.Max(0, RecalledSoulMaximumBonus);
        StartRollData ??= string.Empty;
        EndRollData ??= string.Empty;
    }
}

public sealed class HotaruCannonCombatState
{
    public int PhaseVersion { get; set; } = 2;
    public HotaruRareCannonState Rare { get; set; } = new();
    public HotaruCannonState[] Cannons { get; set; } =
        Enumerable.Range(0, HotaruCannonKinds.Count).Select(_ => new HotaruCannonState()).ToArray();
    public int LastNaturalStartTurn { get; set; }
    public int LastNaturalEndTurn { get; set; }
    public bool EndRewardPending { get; set; }
    public List<HotaruDefensiveEcho> PendingDefensiveEchoes { get; set; } = new();

    public HotaruCannonState For(HotaruCannonKind kind) => Cannons[(int)kind];
    public string ToJson() => JsonSerializer.Serialize(this);

    public static HotaruCannonCombatState FromJson(string data)
    {
        HotaruCannonCombatState state;
        try { state = JsonSerializer.Deserialize<HotaruCannonCombatState>(data ?? string.Empty); }
        catch (JsonException) { state = null; }
        state ??= new HotaruCannonCombatState();
        state.Rare ??= new HotaruRareCannonState();
        state.Rare.BrokenResistanceMasks ??= new();
        state.Rare.InheritedRolls ??= new();
        HotaruCannonState[] saved = state.Cannons ?? Array.Empty<HotaruCannonState>();
        state.Cannons = Enumerable.Range(0, HotaruCannonKinds.Count)
            .Select(index => index < saved.Length ? saved[index] ?? new HotaruCannonState() : new HotaruCannonState())
            .ToArray();
        foreach (HotaruCannonState cannon in state.Cannons) cannon.Normalize();
        state.PendingDefensiveEchoes = (state.PendingDefensiveEchoes ?? new())
            .Where(echo => echo != null)
            .Select(echo => new HotaruDefensiveEcho {
                BaseValueBonus = Math.Clamp(echo.BaseValueBonus, 0m, HotaruCannonState.MaximumStoredValue),
                Overclock = echo.Overclock
            }).ToList();
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

public sealed class HotaruDefensiveEcho
{
    public decimal BaseValueBonus { get; set; }
    public bool Overclock { get; set; }
}
