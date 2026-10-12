using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomChoiceSortHelper
{
    internal static IOrderedEnumerable<PhantomChoiceHbrgoSaki> OrderByPhantomGrowth(
        this IEnumerable<PhantomChoiceHbrgoSaki> cards) =>
        // Use the displayed level, including temporary combat levels. LINQ's
        // stable ordering keeps equally grown phantoms in their original order.
        cards.OrderByDescending(card => card.PhantomLevel)
            .ThenByDescending(card => card.PhantomUpgradeLevel);
}
