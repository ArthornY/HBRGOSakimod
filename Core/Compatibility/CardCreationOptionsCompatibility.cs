using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class CardCreationOptionsCompatibility
{
    internal static CardCreationOptions WithCardPools(
        CardCreationOptions options,
        IEnumerable<CardPoolModel> pools)
    {
        return Create(options, pools, options.RarityOdds, options.CardPoolFilter);
    }

    internal static CardCreationOptions Create(
        CardCreationOptions options,
        IEnumerable<CardPoolModel> pools,
        CardRarityOddsType rarityOdds,
        Func<CardModel, bool> filter)
    {
        // Stable and public-beta share this constructor, but not WithCardPools' signature.
        CardCreationOptions result = new(pools, options.Source, rarityOdds, filter);
        result = result.WithFlags(options.Flags);
        if (options.RngOverride != null)
        {
            result = result.WithRngOverride(options.RngOverride);
        }

        return result;
    }
}
