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

public sealed class ChieLitaCardCapability : CardCapability, ICardHoverTipContributor
{
    private static string KeywordId(bool dress) => ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", dress ? "DRESS_UP" : "LITA");
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!ReferenceEquals(card, Owner)) return false;
        var state = ChieLitaState.Get(card); bool changed = false;
        if (state.Lita > 0 && ModKeywordRegistry.TryGetCardKeyword(KeywordId(false), out CardKeyword lita)) changed |= keywords.Add(lita);
        if (state.Dressed && ModKeywordRegistry.TryGetCardKeyword(KeywordId(true), out CardKeyword dress)) changed |= keywords.Add(dress);
        return changed;
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner)) yield break;
        var state = ChieLitaState.Get(card);
        if (state.Lita > 0)
        {
            LocString text = new("card_keywords", KeywordId(false) + ".instanceDescription"); text.Add("Lita", state.Lita);
            yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId(false)), text, KeywordId(false));
        }
        if (state.Dressed)
        {
            LocString text = new("card_keywords", KeywordId(true) + ".instanceDescription");
            text.Add("LastJudgment", state.LastRoll <= 0 ? "尚未判定。" : "上次出目：" + state.LastRoll + "，" + (state.LastSuccess ? "成功。" : "失败。"));
            yield return HoverTipCompatibility.CreateKeyword(ModKeywordRegistry.GetTitle(KeywordId(true)), text, KeywordId(true));
        }
    }
}
