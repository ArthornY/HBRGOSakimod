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

public sealed class FormationKeywordCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private static readonly string KeywordId = ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "FORMATION");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        ReferenceEquals(card, Owner) && card is WitnessMySupremacyHbrgoSaki &&
        ModKeywordRegistry.TryGetCardKeyword(KeywordId, out CardKeyword keyword) && keywords.Add(keyword);
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (ReferenceEquals(context.Card, Owner) && Owner is WitnessMySupremacyHbrgoSaki)
            yield return new(new LocString("card_keywords", KeywordId + ".cardText"), CardDescriptionFragmentPlacement.BeforeBase);
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner) || card is not WitnessMySupremacyHbrgoSaki source) yield break;
        LocString text = new("card_keywords", KeywordId + ".instanceDescription");
        YingxiaFormationCombatHelper.AddDescriptionParameters(source, text);
        yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId), text, KeywordId);
    }
}
