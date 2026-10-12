using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

internal static class RunPeakCardDamageHelper
{
    private sealed class Peak
    {
        internal decimal Amount;
    }

    // Real Saki runs persist through their hidden relic. Other actors keep a
    // player-scoped fallback without adding a relic or a non-Saki summary UI.
    private static readonly ConditionalWeakTable<Player, Peak> Peaks = new();

    internal static void Update(Player player, decimal totalDamage)
    {
        if (player == null || totalDamage <= 0m) return;
        Peak peak = Peaks.GetValue(player, static _ => new Peak());
        peak.Amount = Math.Max(peak.Amount, totalDamage);
        player.GetRelic<SoulOfAnchor>()?.RecordRunPeakTotalDamage(totalDamage);
    }

    internal static decimal Get(Player player)
    {
        if (player == null) return 0m;
        decimal persisted = player.GetRelic<SoulOfAnchor>()?.RunPeakTotalDamage ?? 0m;
        return Peaks.TryGetValue(player, out Peak peak)
            ? Math.Max(persisted, peak.Amount) : persisted;
    }

    internal static string Format(decimal damage) =>
        damage.ToString("#,0.############################", CultureInfo.InvariantCulture);
}
