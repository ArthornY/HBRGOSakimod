using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Presentation used only by card holders, combat powers and creatures, and
/// top-bar relics. Hover tips show the short text (current values and effects);
/// holding the full-text key shows the native tips unchanged.
/// </summary>
internal static class HbrCompactHoverTipHelper
{
    private const string ModId = "HBRGOSakiMod";

    private static readonly string RollId = Keyword("ROLL");
    private static readonly string CriticalId = Keyword("CRITICAL_SUCCESS");
    private static readonly string AbsoluteCriticalId = Keyword("ABSOLUTE_CRITICAL_SUCCESS");

    // A card already prints its team; the tag tooltip only repeats it.
    private static readonly HashSet<string> TeamTagIds = new(StringComparer.Ordinal)
    {
        Keyword("30A"), Keyword("31A"), Keyword("HEADQUARTERS"), Keyword("CRYCHIC")
    };

    private static readonly Lazy<HashSet<string>> DebuffIds = new(() => new HashSet<string>(StringComparer.Ordinal)
    {
        ModelDb.Power<HbrDefenseDownPower>().Id.ToString(),
        ModelDb.Power<HbrAttackDownPower>().Id.ToString(),
        ModelDb.Power<FragilePower>().Id.ToString()
    });

    private static readonly Lazy<IReadOnlyDictionary<string, PowerModel>> CompactPowers = new(() =>
        new PowerModel[]
        {
            ModelDb.Power<ReptureDualSwordsPower>(),
            ModelDb.Power<ReptureBowPower>(),
            ModelDb.Power<BreakPower>(),
            ModelDb.Power<DestructionRatePower>(),
            ModelDb.Power<HighDimensionalShellPower>(),
            ModelDb.Power<NotOverYetPower>(),
            ModelDb.Power<PhantomPower>()
        }.ToDictionary(power => power.Id.ToString(), StringComparer.Ordinal));

    private static string Keyword(string name) => ModContentRegistry.GetQualifiedKeywordId(ModId, name);

    internal static IEnumerable<IHoverTip> ForCard(CardModel card) =>
        Build(() => card.HoverTips, card, null, null, tips =>
        {
            bool changed = MergeRoll(tips);
            changed |= MergeDebuffs(tips);
            changed |= tips.RemoveAll(tip => tip is HoverTip hover && TeamTagIds.Contains(HoverTipCompatibility.KeywordId(hover))) > 0;
            return changed;
        });

    internal static IEnumerable<IHoverTip> ForPower(PowerModel power) =>
        Build(() => power.HoverTips, null, power, null, null);

    internal static IEnumerable<IHoverTip> ForCreature(Creature creature) =>
        Build(() => creature.HoverTips, null, null, creature, null);

    internal static IEnumerable<IHoverTip> ForRelic(RelicModel relic) =>
        Build(() => relic.HoverTips, null, null, null, null);

    private static IEnumerable<IHoverTip> Build(Func<IEnumerable<IHoverTip>> source, CardModel card,
        PowerModel powerContext, Creature owner, Func<List<IHoverTip>, bool> merge)
    {
        HbrHoverDetail.NoteBuild();
        if (!HbrCompactText.IsAvailable)
            return Rewrite(source(), card, powerContext, owner, out _);
        if (HbrHoverDetail.IsHeld()) return source();
        try
        {
            List<IHoverTip> tips;
            bool changed;
            using (HbrCompactText.Enter())
            {
                // Materialize inside the scope: tips format their text when built.
                tips = Rewrite(source(), card, powerContext, owner, out changed).ToList();
                changed |= HbrCompactText.Swapped > 0;
                if (merge != null) changed |= merge(tips);
            }
            if (changed) AppendHint(tips);
            return tips;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Compact hover tips failed: {error.Message}");
            return source();
        }
    }

    private static IEnumerable<IHoverTip> Rewrite(IEnumerable<IHoverTip> tips, CardModel card,
        PowerModel powerContext, Creature owner, out bool changed)
    {
        List<IHoverTip> result = new();
        changed = false;
        foreach (IHoverTip original in tips)
        {
            if (original is not HoverTip tip || tip.Id == null ||
                !CompactPowers.Value.TryGetValue(tip.Id, out PowerModel canonical))
            {
                result.Add(original);
                continue;
            }

            PowerModel power = canonical;
            if (powerContext?.Id == canonical.Id)
                power = powerContext;
            else if (powerContext?.IsMutable == true)
                power = powerContext.Owner?.Powers.FirstOrDefault(candidate => candidate.Id == canonical.Id) ?? canonical;
            else if (owner != null)
                power = owner.Powers.FirstOrDefault(candidate => candidate.Id == canonical.Id) ?? canonical;

            // Derived shields and permanent Break retain their own full rules.
            LocString description = LocString.GetIfExists("powers", power.Id.Entry + ".compactDescription");
            if (description == null)
            {
                result.Add(original);
                continue;
            }
            description = power switch
            {
                CountedRollPower counted => counted.CompactDescription,
                SynergyPower synergy => synergy.CompactDescription,
                BeyondProtectionPower beyond => beyond.CompactDescription,
                HighDimensionalShellPower shell => shell.CompactDescription,
                DestructionRatePower rate => rate.CompactDescription,
                PhantomPower phantom =>
                    (card?.IsMutable == true
                        ? PhantomHelper.GetLivingPhantom(card.Owner)?.GetPower<PhantomPower>() ?? phantom
                        : phantom).CompactDescription,
                NotOverYetPower when card is SoyoAidHbrgoSaki soyo =>
                    NotOverYetPower.GetCardCompactDescription(soyo),
                NotOverYetPower chain =>
                    (card?.IsMutable == true
                        ? card.Owner?.Creature?.GetPower<NotOverYetPower>() ?? chain
                        : chain).CompactDescription,
                ReptureDualSwordsPower => HbrgoSakiEnergyIconHelper.Add31OPrefix(description),
                _ => description
            };
            result.Add(Copy(tip, tip.Title, description.GetFormattedText()));
            changed = true;
        }
        return result;
    }

    // Roll, critical and absolute critical success become one box.
    private static bool MergeRoll(List<IHoverTip> tips)
    {
        int rollIndex = IndexOf(tips, RollId);
        if (rollIndex < 0) return false;
        HoverTip roll = (HoverTip)tips[rollIndex];
        List<string> lines = new() { roll.Description };
        foreach (string id in new[] { CriticalId, AbsoluteCriticalId })
        {
            int index = IndexOf(tips, id);
            if (index < 0) continue;
            HoverTip tip = (HoverTip)tips[index];
            lines.Add(HbrCompactText.Ui("line", ("title", tip.Title), ("text", tip.Description)));
            tips.RemoveAt(index);
            if (index < rollIndex) rollIndex--;
        }
        if (lines.Count == 1) return false;
        tips[rollIndex] = Copy(roll, roll.Title, string.Join("\n", lines));
        return true;
    }

    // Merge effect summaries only; duration and slot rules depend on the source.
    private static bool MergeDebuffs(List<IHoverTip> tips)
    {
        List<int> indexes = new();
        for (int i = 0; i < tips.Count; i++)
            if (tips[i] is HoverTip hover && hover.Id != null && DebuffIds.Value.Contains(hover.Id)) indexes.Add(i);
        if (indexes.Count == 0) return false;
        List<HoverTip> group = indexes.Select(i => (HoverTip)tips[i]).ToList();
        string title = string.Join(HbrCompactText.Ui("titleSeparator"), group.Select(tip => tip.Title));
        string body = group.Count == 1
            ? group[0].Description
            : string.Join("\n", group.Select(tip => HbrCompactText.Ui("line", ("title", tip.Title), ("text", tip.Description))));
        HoverTip merged = Copy(group[0], title, body);
        for (int i = indexes.Count - 1; i >= 0; i--) tips.RemoveAt(indexes[i]);
        tips.Insert(Math.Min(indexes[0], tips.Count), merged);
        return true;
    }

    // One grey line at the end of the last text box.
    private static void AppendHint(List<IHoverTip> tips)
    {
        int index = tips.FindLastIndex(tip => tip is HoverTip);
        if (index < 0) return;
        HoverTip tip = (HoverTip)tips[index];
        string hint = HbrCompactText.Ui(HbrHoverDetail.IsUsingController() ? "holdController" : "holdKeyboard");
        tips[index] = Copy(tip, tip.Title, tip.Description + "\n" + hint);
    }

    private static int IndexOf(List<IHoverTip> tips, string id) =>
        tips.FindIndex(tip => tip is HoverTip hover && HoverTipCompatibility.KeywordId(hover) == id);

    private static HoverTip Copy(HoverTip tip, string title, string description)
    {
        return HoverTipCompatibility.Copy(tip, title, description);
    }
}
