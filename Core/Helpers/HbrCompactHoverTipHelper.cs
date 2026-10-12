using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Presentation used only by card holders and combat power icons.</summary>
internal static class HbrCompactHoverTipHelper
{
    private static readonly string RollId =
        ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "ROLL");
    private static readonly string AbsoluteCriticalId =
        ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "ABSOLUTE_CRITICAL_SUCCESS");

    private static readonly Lazy<IReadOnlyDictionary<string, PowerModel>> CompactPowers = new(() =>
        new PowerModel[]
        {
            ModelDb.Power<ReptureDualSwordsPower>(),
            ModelDb.Power<ReptureBowPower>(),
            ModelDb.Power<BreakPower>(),
            ModelDb.Power<DestructionRatePower>(),
            ModelDb.Power<HighDimensionalShellPower>(),
            ModelDb.Power<NotOverYetPower>(),
            ModelDb.Power<PhantomPower>()
        }.ToDictionary(power => power.Id.ToString(), StringComparer.Ordinal));

    internal static IEnumerable<IHoverTip> ForCard(CardModel card) =>
        Rewrite(card.HoverTips, card, null);

    internal static IEnumerable<IHoverTip> ForPower(PowerModel power) =>
        Rewrite(power.HoverTips, null, power);

    private static IEnumerable<IHoverTip> Rewrite(
        IEnumerable<IHoverTip> tips, CardModel card, PowerModel powerContext)
    {
        foreach (IHoverTip original in tips)
        {
            if (original is not HoverTip tip)
            {
                yield return original;
                continue;
            }

            LocString title = null;
            LocString description = null;
            if (tip.Id == RollId || tip.Id == AbsoluteCriticalId)
            {
                description = LocString.GetIfExists("card_keywords", tip.Id + ".compactDescription");
                if (description != null)
                {
                    title = ModKeywordRegistry.GetTitle(tip.Id);
                    if (tip.Id == AbsoluteCriticalId)
                        description.Add("threshold", RollKeywordRules.GetAbsoluteCriticalMinimum(
                            card?.IsMutable == true ? card.Owner : null));
                }
            }
            else if (tip.Id != null && CompactPowers.Value.TryGetValue(tip.Id, out PowerModel canonical))
            {
                PowerModel power = canonical;
                if (powerContext?.Id == canonical.Id)
                    power = powerContext;
                else if (powerContext?.IsMutable == true)
                    power = powerContext.Owner?.Powers.FirstOrDefault(
                        candidate => candidate.Id == canonical.Id) ?? canonical;

                // Derived shields and permanent Break retain their own full rules.
                description = LocString.GetIfExists("powers", power.Id.Entry + ".compactDescription");
                if (description != null)
                {
                    title = power.Title;
                    description = power switch
                    {
                        HighDimensionalShellPower shell => shell.CompactDescription,
                        DestructionRatePower rate => rate.CompactDescription,
                        PhantomPower phantom =>
                            (card?.IsMutable == true
                                ? PhantomHelper.GetLivingPhantom(card.Owner)?
                                    .GetPower<PhantomPower>() ?? phantom
                                : phantom).CompactDescription,
                        NotOverYetPower when card is SoyoAidHbrgoSaki soyo =>
                            NotOverYetPower.GetCardCompactDescription(soyo),
                        NotOverYetPower chain =>
                            (card?.IsMutable == true
                                ? card.Owner?.Creature?.GetPower<NotOverYetPower>() ?? chain
                                : chain).CompactDescription,
                        ReptureDualSwordsPower => HbrgoSakiEnergyIconHelper.Add31OPrefix(description),
                        _ => description
                    };
                }
            }

            if (description == null)
            {
                yield return original;
                continue;
            }

            HoverTip compact = new(title, description, tip.Icon)
            {
                Id = tip.Id,
                IsSmart = tip.IsSmart,
                IsDebuff = tip.IsDebuff,
                IsInstanced = tip.IsInstanced,
                ShouldOverrideTextOverflow = tip.ShouldOverrideTextOverflow
            };
            if (tip.CanonicalModel != null)
                compact.SetCanonicalModel(tip.CanonicalModel);
            yield return compact;
        }
    }
}
