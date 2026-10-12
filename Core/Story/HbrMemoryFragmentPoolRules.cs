using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Story;

/// <summary>Pool selection rules independent of rendering, players' individual hooks, and wall-clock RNG.</summary>
internal static class HbrMemoryFragmentPoolRules
{
    internal const string PoolId = "memoryFragments";
    internal const float Chance = 0.3f;

    internal static bool IsEnabled(IEnumerable<string> partyCharacters) =>
        partyCharacters.Any(character => character is "HbrgoSaki31O" or "HbrgoSaki30A" or "HbrgoHotaru");

    internal static T Choose<T>(IEnumerable<string> partyCharacters, IEnumerable<T> events,
        Func<T, bool> isEligible, Func<T, string> getId, IEnumerable<string> visitedIds,
        Func<float> nextFloat) where T : class
    {
        if (!IsEnabled(partyCharacters)) return null;
        T[] pool = events.ToArray();
        T[] candidates = pool.Where(isEligible).ToArray();
        // An unavailable/empty pool must not consume a roll or suppress a normal event.
        if (candidates.Length == 0 || nextFloat() >= Chance) return null;
        return ChooseWeighted(pool, candidates, getId, visitedIds, nextFloat);
    }

    internal static T ChooseGuaranteed<T>(IEnumerable<string> partyCharacters, IEnumerable<T> events,
        Func<T, bool> isEligible, Func<T, string> getId, IEnumerable<string> visitedIds,
        Func<float> nextFloat) where T : class
    {
        if (!IsEnabled(partyCharacters)) return null;
        T[] pool = events.ToArray();
        T[] candidates = pool.Where(isEligible).ToArray();
        // Dedicated map nodes use the same repeat weights without the 30% gate.
        return candidates.Length == 0 ? null : ChooseWeighted(pool, candidates, getId, visitedIds, nextFloat);
    }

    internal static IReadOnlyDictionary<string, double> GetWeights(IEnumerable<string> eventIds,
        IEnumerable<string> visitedIds)
    {
        Dictionary<string, double> weights = eventIds.Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, _ => 1d, StringComparer.Ordinal);
        if (weights.Count <= 1) return weights;
        string[] ids = weights.Keys.ToArray();
        foreach (string visited in visitedIds)
        {
            if (!weights.TryGetValue(visited, out double weight)) continue;
            double redistributed = weight / 2d / (ids.Length - 1);
            weights[visited] = weight / 2d;
            foreach (string id in ids)
                if (id != visited) weights[id] += redistributed;
        }
        return weights;
    }

    private static T ChooseWeighted<T>(T[] pool, T[] candidates, Func<T, string> getId,
        IEnumerable<string> visitedIds, Func<float> nextFloat)
    {
        // Replay the shared, saved history instead of mutating per-player state.
        // Each occurrence matters; a visited-event set loses both order and count.
        IReadOnlyDictionary<string, double> weights = GetWeights(pool.Select(getId), visitedIds);
        double roll = nextFloat() * candidates.Sum(candidate => weights[getId(candidate)]);
        foreach (T candidate in candidates)
        {
            roll -= weights[getId(candidate)];
            if (roll < 0d) return candidate;
        }
        return candidates[^1];
    }

    internal static void RemoveFromOrdinaryQueue<T>(List<T> events, ref int visited, Predicate<T> isMemory)
    {
        if (events.Count == 0) return;
        int oldCount = events.Count;
        int oldPosition = visited % oldCount;
        int removedBeforePosition = 0;
        for (int index = 0; index < oldPosition; index++)
        {
            if (isMemory(events[index])) removedBeforePosition++;
        }
        if (events.RemoveAll(isMemory) == 0) return;
        // Keep the next ordinary event in place, including a saved queue that has
        // already wrapped. A pool migration must not restart the whole event queue.
        visited = events.Count == 0 ? 0
            : visited / oldCount * events.Count + oldPosition - removedBeforePosition;
    }
}
