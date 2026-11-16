using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;
namespace HBRGOSakiMod.Core.Models.Capabilities;
public interface ILeadingChargeCard { decimal LeadingChargePercent { get; } }
public sealed class LeadingChargeKeywordCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private static readonly string KeywordId = ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "LEADING_CHARGE");
    public override bool TryModifyKeywordsInCombat(CardModel card,ISet<CardKeyword> keywords) => ReferenceEquals(card,Owner) && card is ILeadingChargeCard &&
        ModKeywordRegistry.TryGetCardKeyword(KeywordId,out CardKeyword keyword) && keywords.Add(keyword);
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (ReferenceEquals(context.Card,Owner) && Owner is ILeadingChargeCard)
            yield return new(new LocString("card_keywords",KeywordId+".cardText"),CardDescriptionFragmentPlacement.BeforeBase);
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (ReferenceEquals(card,Owner) && card is ILeadingChargeCard)
            yield return HBRGOSakiMod.Core.Helpers.HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId),new LocString("card_keywords",KeywordId+".description"),KeywordId);
    }
}
