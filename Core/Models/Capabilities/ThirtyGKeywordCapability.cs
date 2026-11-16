using System.Collections.Generic;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public sealed class ThirtyGKeywordCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private static readonly string KeywordId = ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "30G");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        ReferenceEquals(card, Owner) && ThirtyGKeywordRules.IsThirtyGCard(card) &&
        ModKeywordRegistry.TryGetCardKeyword(KeywordId, out CardKeyword keyword) && keywords.Add(keyword);
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (ReferenceEquals(context.Card, Owner) && ThirtyGKeywordRules.IsThirtyGCard(Owner))
            yield return new CardDescriptionFragment(new LocString("card_keywords", KeywordId + ".cardText"), CardDescriptionFragmentPlacement.BeforeBase);
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (ReferenceEquals(card, Owner) && ThirtyGKeywordRules.IsThirtyGCard(card))
            yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId),
                new LocString("card_keywords", KeywordId + ".description"), KeywordId);
    }
}
