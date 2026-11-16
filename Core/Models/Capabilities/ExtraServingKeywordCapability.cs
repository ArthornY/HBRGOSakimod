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

// Any future card can opt into the same payment/keyword contract.
public interface IExtraServingCard { }
public sealed class ExtraServingKeywordCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private static readonly string KeywordId = ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "EXTRA_SERVING");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        ReferenceEquals(card, Owner) && card is IExtraServingCard &&
        ModKeywordRegistry.TryGetCardKeyword(KeywordId, out CardKeyword keyword) && keywords.Add(keyword);
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (ReferenceEquals(context.Card, Owner) && Owner is IExtraServingCard)
            yield return new CardDescriptionFragment(new LocString("card_keywords", KeywordId + ".cardText"), CardDescriptionFragmentPlacement.BeforeBase);
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (ReferenceEquals(card, Owner) && card is IExtraServingCard)
            yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId),
                new LocString("card_keywords", KeywordId + ".description"), KeywordId);
    }
}
