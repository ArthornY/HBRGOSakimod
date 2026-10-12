using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomAttackIntentPreviewHelper
{
    internal static bool TryGetSegments(
        Creature owner,
        AttackIntent intent,
        out IReadOnlyList<int> segments)
    {
        segments = Array.Empty<int>();
        if (owner == null || intent == null || !owner.HasPower<PhantomPower>() ||
            !ReferenceEquals(owner.Monster?.NextMove?.Intents
                .OfType<AttackIntent>().FirstOrDefault(), intent))
        {
            return false;
        }

        // The first attack icon represents the full phantom move, including
        // fusion and next-attack segments. Labels and tips must share its target.
        segments = PhantomHelper.GetCurrentAttackIntentPreviewSegments(owner);
        return segments.Count > 0;
    }

    internal static LocString CreateDescription(IReadOnlyList<int> segments)
    {
        string attacks = string.Join("\n", segments.GroupBy(damage => damage).Select(group =>
        {
            var attack = new LocString("intents", "ATTACK.description");
            attack.Add("Damage", group.Key);
            attack.Add("Repeat", group.Count());
            // Allied phantoms target enemies, even in multiplayer combat.
            attack.Add("IsMultiplayer", false);
            return attack.GetFormattedText();
        }));
        var description = new LocString("intents", "HBRGO_PHANTOM_ATTACK.description");
        description.Add("Attacks", attacks);
        return description;
    }
}
