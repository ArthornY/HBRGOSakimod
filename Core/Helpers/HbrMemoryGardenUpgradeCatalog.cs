using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed record MemoryGardenUpgradeOption(
    int EffectId, string Title, string Description, CardModel PreviewCard);

internal sealed record MemoryGardenUpgradeSkill(
    int EffectId, string Title, string Description);

internal static class HbrMemoryGardenUpgradeCatalog
{
    // Candidate count is distinct from the seven upgrades a card may acquire.
    internal static int GetCandidateCount(CardModel card) => card is SaviorGrandEntranceHbrgoSaki ? 8 : 7;

    // CardCmd.Upgrade is synchronous. Authorize only the selected card while
    // applying a garden effect; unrelated upgrades triggered by hooks stay out.
    [ThreadStatic]
    private static CardModel _authorizedUpgradeCard;

    internal static bool IsUpgradeAuthorized(CardModel card) =>
        ReferenceEquals(card, _authorizedUpgradeCard);

    internal static IReadOnlyList<MemoryGardenUpgradeSkill> GetAllUpgradeSkills(CardModel card)
    {
        if (!HbrMemoryGardenCardCatalog.IsGardenCard(card))
            return Array.Empty<MemoryGardenUpgradeSkill>();

        return Enumerable.Range(0, GetCandidateCount(card)).Select(effectId =>
        {
            string title = GetEffectTitle(card, effectId);
            string description = GetEffectDescription(card, effectId.ToString());
            int? prerequisite = card switch
            {
                AnonAidHbrgoSaki when effectId == 5 => 4,
                SoyoAidHbrgoSaki when effectId == 1 => 0,
                _ => null
            };
            if (prerequisite is int required)
            {
                LocString notice = new("cards", "HBRGO_MEMORY_GARDEN.upgrade_requires");
                notice.Add("Skill", GetEffectTitle(card, required));
                description += "\n" + notice.GetFormattedText();
            }
            return new MemoryGardenUpgradeSkill(effectId, title, description);
        }).ToArray();
    }

    private static string GetEffectTitle(CardModel card, int effectId) =>
        new LocString("cards", card.Id.Entry + ".upgrade_effect_title." + effectId).GetFormattedText();

    internal static IReadOnlyList<int> GetAvailableEffectIds(CardModel card)
    {
        if (card == null || !HbrMemoryGardenCardCatalog.IsGardenCard(card) || !card.IsUpgradable)
            return Array.Empty<int>();
        IReadOnlyList<int> effects = card switch
        {
            HerSongIsMyScreamHbrgoSaki scream => scream.GetAvailableUpgradeEffects(),
            MatchaParfaitHbrgoSaki matcha => matcha.GetAvailableUpgradeEffects(),
            AnonAidHbrgoSaki anon => anon.GetAvailableUpgradeEffects(),
            SoyoAidHbrgoSaki soyo => soyo.GetAvailableUpgradeEffects(),
            LostStarSongHbrgoSaki tomori => tomori.GetAvailableUpgradeEffects(),
            HalfBodyHbrgoSaki mutsumi => mutsumi.GetAvailableUpgradeEffects(),
            MyLegendBeginsHbrgoSaki ruka => ruka.GetAvailableUpgradeEffects(),
            HelloWorldHbrgoSaki yuki => yuki.GetAvailableUpgradeEffects(),
            SaviorGrandEntranceHbrgoSaki megumi => megumi.GetAvailableUpgradeEffects(),
            WorldsFinestHbrgoSaki tama => tama.GetAvailableUpgradeEffects(),
            MoreThanOneTruthHbrgoSaki tsukasa => tsukasa.GetAvailableUpgradeEffects(),
            CrushYouLikeABugHbrgoSaki karen => karen.GetAvailableUpgradeEffects(),
            SummerGrassDreamHbrgoSaki saki => saki.GetAvailableUpgradeEffects(),
            BoostUpHbrgoSaki nanami => nanami.GetAvailableUpgradeEffects(),
            ByakkoRoarHbrgoSaki byakko => byakko.GetAvailableUpgradeEffects(),
            ThirtyOneBGardenCard member => member.GetAvailableUpgradeEffects(),
            _ => new[] { 0 }
        };
        return HbrMemoryGardenDampen.FilterAvailableEffects(card, effects);
    }

    internal static IReadOnlyList<MemoryGardenUpgradeOption> RollOffers(CardModel owned, Rng rng)
    {
        List<int> remaining = GetAvailableEffectIds(owned).ToList();
        List<MemoryGardenUpgradeOption> offers = new();
        while (offers.Count < 3 && remaining.Count > 0)
        {
            int index = remaining.Count == 1 ? 0 : rng.NextInt(remaining.Count);
            int effectId = remaining[index];
            remaining.RemoveAt(index);
            CardModel preview = (CardModel)owned.ClonePreservingMutability();
            preview.UpgradePreviewType = CardUpgradePreviewType.Deck;
            if (!PrepareUpgradeEffect(preview, effectId))
                throw new InvalidOperationException("Cannot preview an unavailable memory garden upgrade.");
            preview.UpgradeInternal();
            // Leave native upgrade preview flags intact so the selected variant
            // shows its cost/value changes. Cloning also preserves six-sense data.
            string suffix = owned is HerSongIsMyScreamHbrgoSaki or MatchaParfaitHbrgoSaki or AnonAidHbrgoSaki or SoyoAidHbrgoSaki or LostStarSongHbrgoSaki or HalfBodyHbrgoSaki or MyLegendBeginsHbrgoSaki or HelloWorldHbrgoSaki or SaviorGrandEntranceHbrgoSaki or WorldsFinestHbrgoSaki or MoreThanOneTruthHbrgoSaki or CrushYouLikeABugHbrgoSaki or SummerGrassDreamHbrgoSaki or BoostUpHbrgoSaki or ByakkoRoarHbrgoSaki or ThirtyOneBGardenCard
                ? effectId.ToString() : "legacy";
            offers.Add(new(effectId,
                new LocString("cards", owned.Id.Entry + ".upgrade_effect_title." + suffix).GetFormattedText(),
                GetEffectDescription(owned, suffix),
                preview));
        }
        return offers;
    }

    // Choice dialogs retain the granted keyword. The face already gets it from
    // native CardKeywordOrder, so only the four keyword-granting upgrade rows
    // omit that prefix when composed into the actual card description.
    internal static string GetCardEffectDescription(CardModel card, string suffix)
    {
        string text = GetEffectDescription(card, suffix);
        string prefix = (card, suffix) switch
        {
            (ItsNinjaShowtimeHbrgoSaki, "6") or (WindForestFireMountainHbrgoSaki, "5") => "[gold]保留[/gold]。",
            (AbsolutelyNoEffortHbrgoSaki, "1") or (ExorcisingStringResoundHbrgoSaki, "6") => "[gold]固有[/gold]。",
            _ => null
        };
        return prefix != null && text.StartsWith(prefix, StringComparison.Ordinal)
            ? text[prefix.Length..].TrimStart(' ', '\r', '\n') : text;
    }

    internal static string GetEffectDescription(CardModel card, string suffix)
    {
        if (card is BoostUpHbrgoSaki { UsesThirtyAEffects: true } && suffix is "0" or "5" or "6")
            suffix += ".30a";
        string key = card.Id.Entry + ".upgrade_effect." + suffix;
        if (HbrSkillCardDescription.IsEnabled(card) && new LocString("cards", key + ".hbr").Exists())
            key += ".hbr";
        LocString description = new("cards", key);
        description.Add("energyPrefix", EnergyIconHelper.GetPrefix(card));
        card.DynamicVars.AddTo(description);
        if (HbrSkillCardDescription.IsEnabled(card)) HbrSkillCardDescription.AddParameters(card, description);
        else HbrSkillCardDescription.AddLegacyParameters(card, description);
        HbrSkillCardDescription.AddSupplementalDamageParameters(card, description);
        HanamuraOrchestraCombatHelper.AddDescriptionParameters(card, description);
        MatsuokaBlowAwayCombatHelper.AddDescriptionParameters(card, description);
        return description.GetFormattedText();
    }

    internal static bool TryApplyUpgrade(CardModel card, int effectId)
    {
        HbrMemoryGardenProgression.Synchronize(card);
        if (!GetAvailableEffectIds(card).Contains(effectId)) return false;
        if (!PrepareUpgradeEffect(card, effectId)) return false;
        int previousLevel = card.CurrentUpgradeLevel;
        CardModel previousAuthorization = _authorizedUpgradeCard;
        try
        {
            _authorizedUpgradeCard = card;
            CardCmd.Upgrade(card);
            bool upgraded = card.CurrentUpgradeLevel == previousLevel + 1;
            if (upgraded) HbrMemoryGardenProgression.Publish(card);
            return upgraded;
        }
        finally
        {
            _authorizedUpgradeCard = previousAuthorization;
            // Native upgrade can decline during combat teardown. Never leave a
            // pending selection to leak into a later, unrelated upgrade.
            CancelPreparedUpgradeEffect(card);
        }
    }

    internal static bool PrepareUpgradeEffect(CardModel card, int effectId) => card switch
    {
        HerSongIsMyScreamHbrgoSaki scream => scream.PrepareUpgradeEffect(effectId),
        MatchaParfaitHbrgoSaki matcha => matcha.PrepareUpgradeEffect(effectId),
        AnonAidHbrgoSaki anon => anon.PrepareUpgradeEffect(effectId),
        SoyoAidHbrgoSaki soyo => soyo.PrepareUpgradeEffect(effectId),
        LostStarSongHbrgoSaki tomori => tomori.PrepareUpgradeEffect(effectId),
        HalfBodyHbrgoSaki mutsumi => mutsumi.PrepareUpgradeEffect(effectId),
        MyLegendBeginsHbrgoSaki ruka => ruka.PrepareUpgradeEffect(effectId),
        HelloWorldHbrgoSaki yuki => yuki.PrepareUpgradeEffect(effectId),
        SaviorGrandEntranceHbrgoSaki megumi => megumi.PrepareUpgradeEffect(effectId),
        WorldsFinestHbrgoSaki tama => tama.PrepareUpgradeEffect(effectId),
        MoreThanOneTruthHbrgoSaki tsukasa => tsukasa.PrepareUpgradeEffect(effectId),
        CrushYouLikeABugHbrgoSaki karen => karen.PrepareUpgradeEffect(effectId),
        SummerGrassDreamHbrgoSaki saki => saki.PrepareUpgradeEffect(effectId),
        BoostUpHbrgoSaki nanami => nanami.PrepareUpgradeEffect(effectId),
        ByakkoRoarHbrgoSaki byakko => byakko.PrepareUpgradeEffect(effectId),
        ThirtyOneBGardenCard member => member.PrepareUpgradeEffect(effectId),
        _ => effectId == 0
    };

    private static void CancelPreparedUpgradeEffect(CardModel card)
    {
        switch (card)
        {
            case HerSongIsMyScreamHbrgoSaki scream:
                scream.CancelPreparedUpgradeEffect();
                break;
            case MatchaParfaitHbrgoSaki matcha:
                matcha.CancelPreparedUpgradeEffect();
                break;
            case AnonAidHbrgoSaki anon:
                anon.CancelPreparedUpgradeEffect();
                break;
            case SoyoAidHbrgoSaki soyo:
                soyo.CancelPreparedUpgradeEffect();
                break;
            case LostStarSongHbrgoSaki tomori:
                tomori.CancelPreparedUpgradeEffect();
                break;
            case HalfBodyHbrgoSaki mutsumi:
                mutsumi.CancelPreparedUpgradeEffect();
                break;
            case MyLegendBeginsHbrgoSaki ruka:
                ruka.CancelPreparedUpgradeEffect();
                break;
            case HelloWorldHbrgoSaki yuki:
                yuki.CancelPreparedUpgradeEffect();
                break;
            case SaviorGrandEntranceHbrgoSaki megumi:
                megumi.CancelPreparedUpgradeEffect();
                break;
            case WorldsFinestHbrgoSaki tama:
                tama.CancelPreparedUpgradeEffect();
                break;
            case MoreThanOneTruthHbrgoSaki tsukasa:
                tsukasa.CancelPreparedUpgradeEffect();
                break;
            case CrushYouLikeABugHbrgoSaki karen:
                karen.CancelPreparedUpgradeEffect();
                break;
            case SummerGrassDreamHbrgoSaki saki:
                saki.CancelPreparedUpgradeEffect();
                break;
            case ThirtyOneBGardenCard member:
                member.CancelPreparedUpgradeEffect();
                break;
            case ByakkoRoarHbrgoSaki byakko:
                byakko.CancelPreparedUpgradeEffect();
                break;
            case BoostUpHbrgoSaki nanami:
                nanami.CancelPreparedUpgradeEffect();
                break;
        }
    }
}
