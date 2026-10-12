using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public sealed class HalfBodyAffiliationCapability : CardCapability,
    ICardDescriptionContributor, ICardHoverTipContributor
{
    private static readonly string[] HalfBodyKeywordIds =
    {
        ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "HEADQUARTERS"),
        CryChicKeywordRules.KeywordId
    };

    private static readonly string[] HeadquartersKeywordIds =
    {
        ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "HEADQUARTERS")
    };

    private static IEnumerable<string> GetKeywordIds(CardModel card) => card switch
    {
        HalfBodyHbrgoSaki => HalfBodyKeywordIds,
        SummerGrassDreamHbrgoSaki => HeadquartersKeywordIds,
        BoostUpHbrgoSaki => HeadquartersKeywordIds,
        _ => System.Array.Empty<string>()
    };

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!ReferenceEquals(card, Owner)) return false;
        bool changed = false;
        foreach (string id in GetKeywordIds(card))
            if (ModKeywordRegistry.TryGetCardKeyword(id, out CardKeyword keyword))
                changed |= keywords.Add(keyword);
        return changed;
    }

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (!ReferenceEquals(context.Card, Owner)) yield break;
        foreach (string id in GetKeywordIds(context.Card))
            yield return new CardDescriptionFragment(new LocString("card_keywords", id + ".cardText"),
                CardDescriptionFragmentPlacement.BeforeBase);
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner)) yield break;
        foreach (string id in GetKeywordIds(card))
            yield return new HoverTip(ModKeywordRegistry.GetTitle(id),
                new LocString("card_keywords", id + ".description")) { Id = id };
    }
}
