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

public interface IOrchestraCard
{
    string OrchestraExcludedCharacterId { get; }
    decimal OrchestraStepPercent { get; }
    decimal OrchestraMaximumBonusPercent { get; }
}

public sealed class OrchestraKeywordCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private static readonly string KeywordId = ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "ORCHESTRA");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        ReferenceEquals(card, Owner) && card is IOrchestraCard &&
        ModKeywordRegistry.TryGetCardKeyword(KeywordId, out CardKeyword keyword) && keywords.Add(keyword);
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (ReferenceEquals(context.Card, Owner) && Owner is IOrchestraCard)
            yield return new(new LocString("card_keywords", KeywordId + ".cardText"), CardDescriptionFragmentPlacement.BeforeBase);
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner) || card is not IOrchestraCard orchestra) yield break;
        LocString body = new("card_keywords", KeywordId + ".instanceDescription");
        body.Add("Kinds", HanamuraOrchestraCombatHelper.GetOtherCharacterCount(card.IsMutable ? card.Owner : null, orchestra.OrchestraExcludedCharacterId));
        body.Add("Step", orchestra.OrchestraStepPercent);
        body.Add("Maximum", orchestra.OrchestraMaximumBonusPercent);
        body.Add("Bonus", HanamuraOrchestraCombatHelper.GetBonusPercent(card));
        yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId), body, KeywordId);
    }
}
