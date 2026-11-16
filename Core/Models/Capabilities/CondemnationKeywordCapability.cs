using System.Collections.Generic;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public sealed class CondemnationKeywordCapability : CardCapability, ICardHoverTipContributor
{
    private static readonly string KeywordId = ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "CONDEMNATION");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        ReferenceEquals(card, Owner) && card is LegacyFlashHbrgoSaki &&
        ModKeywordRegistry.TryGetCardKeyword(KeywordId, out CardKeyword keyword) && keywords.Add(keyword);
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner) || card is not LegacyFlashHbrgoSaki) yield break;
        LocString body = new("card_keywords", KeywordId + ".instanceDescription");
        body.Add("Threshold", NatsumeCondemnationCombatHelper.GetThresholdPercent(card));
        yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId), body, KeywordId);
    }
}
