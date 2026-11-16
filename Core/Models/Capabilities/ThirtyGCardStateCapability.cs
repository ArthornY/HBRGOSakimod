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

public sealed class ThirtyGCardStateCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private static string StateKeywordId(bool graduated) => ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", graduated ? "GRADUATED" : "HEARTFELT");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!ReferenceEquals(card, Owner)) return false;
        var state = ThirtyGProposalState.Get(card); bool changed = false;
        if (state.Graduated && ModKeywordRegistry.TryGetCardKeyword(StateKeywordId(true), out CardKeyword graduated)) changed |= keywords.Add(graduated);
        if (state.HeartfeltExtraPlays > 0 && ModKeywordRegistry.TryGetCardKeyword(StateKeywordId(false), out CardKeyword heartfelt)) changed |= keywords.Add(heartfelt);
        return changed;
    }
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (!ReferenceEquals(context.Card, Owner)) yield break;
        var state = ThirtyGProposalState.Get(Owner);
        if (state.Graduated) yield return new(new LocString("card_keywords", StateKeywordId(true) + ".cardText"), CardDescriptionFragmentPlacement.BeforeBase);
        if (state.HeartfeltExtraPlays > 0) yield return new(new LocString("card_keywords", StateKeywordId(false) + ".cardText"), CardDescriptionFragmentPlacement.BeforeBase);
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner)) yield break;
        var state = ThirtyGProposalState.Get(card);
        if (state.Graduated || state.HeartfeltExtraPlays > 0)
        {
            bool graduated = state.Graduated;
            string description = state.WholeBattleNullified ? ".miyaInstanceDescription" :
                state.PermanentHeartfelt ? ".permanentInstanceDescription" : ".instanceDescription";
            LocString text = new("card_keywords", StateKeywordId(graduated) + description);
            text.Add("ExtraPlays", state.HeartfeltExtraPlays);
            yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(StateKeywordId(graduated)), text, StateKeywordId(graduated));
        }
    }
}
