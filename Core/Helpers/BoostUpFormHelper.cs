using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class BoostUpFormHelper
{
    internal static IReadOnlyList<ReptureCardMode> GetMostNumerousForms(Player player)
    {
        // Count permanent cards only. Copies and cards temporarily generated in
        // combat never change the winning forms; an empty category cannot win.
        return GetMostNumerousForms(player?.Deck?.Cards);
    }

    internal static IReadOnlyList<ReptureCardMode> GetMostNumerousForms(IEnumerable<CardModel> cards)
    {
        var counts = (cards ?? Array.Empty<CardModel>())
            .Select(ModeTransitionHelper.GetCardMode)
            .Where(mode => mode != ReptureCardMode.None)
            .GroupBy(mode => mode)
            .Select(group => (Mode: group.Key, Count: group.Count()))
            .ToArray();
        if (counts.Length == 0) return Array.Empty<ReptureCardMode>();
        int largest = counts.Max(group => group.Count);
        return counts.Where(group => group.Count == largest)
            .Select(group => group.Mode).OrderBy(mode => mode).ToArray();
    }

    internal static async Task ApplyDamageBonus(PlayerChoiceContext context, CardModel source, int percent)
    {
        Player player = source?.Owner;
        if (player?.Creature == null || percent <= 0) return;
        // The set is captured before applying the power, and is not recalculated
        // when the deck changes or the owner switches weapons later.
        IReadOnlyList<ReptureCardMode> forms = GetMostNumerousForms(player);
        if (forms.Count == 0) return;
        BoostUpFormDamagePower power = player.Creature.GetPower<BoostUpFormDamagePower>() ??
            await PowerCmd.Apply<BoostUpFormDamagePower>(context, player.Creature, 1m, player.Creature, source);
        power?.AddDamageBonus(forms, percent);
    }

    internal static bool TryModifyEnergyCost(BoostUpHbrgoSaki source, CardModel target,
        decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        Player player = source?.Owner;
        if (player == null || target?.Owner != player || originalCost <= 0m ||
            target.EnergyCost.CostsX || source.UsesThirtyAEffects || !source.HasUpgradeEffect(6) ||
            !player.Deck.Cards.Any(card => ReferenceEquals(card, source)))
            return false;

        ReptureCardMode form = ModeTransitionHelper.GetCardMode(target);
        if (form == ReptureCardMode.None || !GetMostNumerousForms(player).Contains(form))
            return false;

        modifiedCost = Math.Max(0m, originalCost - 1m);
        return modifiedCost != originalCost;
    }
}
