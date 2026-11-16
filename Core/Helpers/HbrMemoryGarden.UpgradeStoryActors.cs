using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static partial class HbrMemoryGarden
{
    // Reuse the authoritative encounter/card catalog. Titles, effect choices,
    // physical copies and rendered previews cannot change the story's actor.
    private static readonly Lazy<IReadOnlyDictionary<Type, string>> UpgradeStoryActors =
        new(() => Characters.ToDictionary(entry => entry.CardType, entry => entry.Id));

    internal static bool TryGetUpgradeStoryActor(CardModel card, out string actorId)
    {
        actorId = null;
        return card != null && UpgradeStoryActors.Value.TryGetValue(card.GetType(), out actorId);
    }

    internal static bool TryGetUpgradeStoryPerspective(Player owner,
        out HbrMemoryUpgradeStoryPerspective perspective)
    {
        switch (owner?.Character)
        {
            case HbrgoSaki31O: perspective = HbrMemoryUpgradeStoryPerspective.ThirtyOneO; return true;
            case HbrgoSaki30A: perspective = HbrMemoryUpgradeStoryPerspective.ThirtyA; return true;
            case HbrgoHotaru: perspective = HbrMemoryUpgradeStoryPerspective.Hotaru; return true;
            default: perspective = default; return false;
        }
    }
}
