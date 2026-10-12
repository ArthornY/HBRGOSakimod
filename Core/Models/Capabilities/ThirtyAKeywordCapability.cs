using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public sealed class ThirtyAKeywordCapability :
    CardCapability,
    ICardDescriptionContributor,
    ICardHoverTipContributor
{
    private const string ModId = "HBRGOSakiMod";
    private const string LocTable = "card_keywords";

    private static readonly string KeywordId =
        ModContentRegistry.GetQualifiedKeywordId(ModId, "30A");

    // Separate from normal garden upgrades: each unlocked collection milestone
    // may be claimed only once by this permanent card, including after reloads.
    internal int CollectionUpgradesClaimed { get; private set; }

    internal void ClaimCollectionUpgrade(int tier)
    {
        int claimed = Math.Clamp(tier, 0, 2);
        if (claimed <= CollectionUpgradesClaimed) return;
        CollectionUpgradesClaimed = claimed;
        MarkDirty();
    }

    protected override JsonNode SaveAdditionalState() => new JsonObject
    {
        ["collectionUpgradesClaimed"] = CollectionUpgradesClaimed
    };

    protected override void LoadAdditionalState(JsonNode state, int schemaVersion)
    {
        CollectionUpgradesClaimed = Math.Clamp(
            state?["collectionUpgradesClaimed"]?.GetValue<int>() ?? 0, 0, 2);
    }

    public override bool TryModifyKeywordsInCombat(
        CardModel card,
        ISet<CardKeyword> keywords)
    {
        if (!ReferenceEquals(card, Owner) ||
            !ThirtyAKeywordRules.IsThirtyACard(card) ||
            !ModKeywordRegistry.TryGetCardKeyword(
                KeywordId,
                out CardKeyword keyword))
        {
            return false;
        }

        bool changed = keywords.Add(keyword);
        if (CryChicKeywordRules.IsCryChicCard(card) &&
            ModKeywordRegistry.TryGetCardKeyword(CryChicKeywordRules.KeywordId, out CardKeyword cryChic))
            changed |= keywords.Add(cryChic);
        return changed;
    }

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(
        CardDescriptionContext context)
    {
        if (!ReferenceEquals(context.Card, Owner) ||
            !ThirtyAKeywordRules.IsThirtyACard(Owner))
        {
            yield break;
        }

        yield return new CardDescriptionFragment(
            new LocString(LocTable, $"{KeywordId}.cardText"),
            CardDescriptionFragmentPlacement.BeforeBase);
        if (CryChicKeywordRules.IsCryChicCard(Owner))
            yield return new CardDescriptionFragment(
                new LocString(LocTable, $"{CryChicKeywordRules.KeywordId}.cardText"),
                CardDescriptionFragmentPlacement.BeforeBase);
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ReferenceEquals(card, Owner) ||
            !ThirtyAKeywordRules.IsThirtyACard(card))
        {
            yield break;
        }

        yield return new HoverTip(
            ModKeywordRegistry.GetTitle(KeywordId),
            new LocString(LocTable, $"{KeywordId}.description"))
        {
            Id = KeywordId
        };
        if (CryChicKeywordRules.IsCryChicCard(card))
            yield return new HoverTip(ModKeywordRegistry.GetTitle(CryChicKeywordRules.KeywordId),
                new LocString(LocTable, $"{CryChicKeywordRules.KeywordId}.description"))
                { Id = CryChicKeywordRules.KeywordId };
    }
}
