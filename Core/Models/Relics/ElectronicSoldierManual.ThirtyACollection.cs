using System;
using System.Collections.Generic;
using System.Text.Json;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    // Retain each kind's milestone receipts even while every copy is absent
    // from the deck, so acquiring it again cannot grant the same upgrade twice.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ThirtyACollectionUpgradeClaims { get; protected set; } = string.Empty;

    internal int GetThirtyACollectionUpgradeClaim(string cardEntry) =>
        ReadThirtyACollectionUpgradeClaims().TryGetValue(cardEntry, out int tier)
            ? Math.Clamp(tier, 0, 2)
            : 0;

    internal void ClaimThirtyACollectionUpgrade(string cardEntry, int tier)
    {
        AssertMutable();
        tier = Math.Clamp(tier, 0, 2);
        SortedDictionary<string, int> claims = ReadThirtyACollectionUpgradeClaims();
        if (tier == 0 || claims.TryGetValue(cardEntry, out int previous) && previous >= tier)
            return;

        claims[cardEntry] = tier;
        ThirtyACollectionUpgradeClaims = JsonSerializer.Serialize(claims);
    }

    private SortedDictionary<string, int> ReadThirtyACollectionUpgradeClaims()
    {
        if (!string.IsNullOrEmpty(ThirtyACollectionUpgradeClaims))
        {
            try
            {
                return JsonSerializer.Deserialize<SortedDictionary<string, int>>(
                    ThirtyACollectionUpgradeClaims) ?? new(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                // Older saves have no shared receipts. The helper also imports
                // the greatest claimed tier from surviving card capabilities.
            }
        }

        return new(StringComparer.Ordinal);
    }
}
