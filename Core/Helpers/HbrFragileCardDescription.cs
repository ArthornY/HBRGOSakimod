using System.Globalization;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Raw modifier counters remain in card variables, upgrades and clones. The
// percentage is presentation only; a selected enemy supplies the actual E.
internal static class HbrFragileCardDescription
{
    internal static void AddParameters(CardModel card, LocString text)
    {
        foreach (DynamicVar variable in card.DynamicVars.Values)
            if (variable is PowerVar<FragilePower>)
            {
                text.Add(variable.Name + "Percent", Format(card, variable.BaseValue, variable.WasJustUpgraded));
                text.Add(variable.Name + "Duration", HbrCardFaceDescription.Duration(
                    HbrEffectProfileResolver.ForType(typeof(FragilePower), card)));
            }
        text.Add("FragileDuration", HbrCardFaceDescription.Duration(
            HbrEffectProfileResolver.ForType(typeof(FragilePower), card)));

        if (card is MimicIceFlowerThousandArrowsHbrgoSaki)
        {
            DynamicVar normal = card.DynamicVars["FragilePower"];
            DynamicVar threshold = card.DynamicVars["ThresholdFragilePower"];
            text.Add("ThresholdFragileTotalPercent", Format(card,
                normal.BaseValue + threshold.BaseValue, normal.WasJustUpgraded || threshold.WasJustUpgraded));
        }
        if (card is SummerGrassDreamHbrgoSaki)
            text.Add("FixedFragileTwoPercent", Format(card, 2m));
    }

    internal static string Format(CardModel card, decimal modifiers, bool upgraded = false)
    {
        decimal basis = HbrFragilePowerHelper.BasePercent(modifiers);
        decimal value = basis;
        var target = HbrCardDescriptionContext.GetTarget(card);
        var owner = card.IsMutable ? card.Owner?.Creature : null;
        if (!HbrCardDescriptionContext.IsUnpowered(card) && target != null && owner != null &&
            target.Side != owner.Side && HbrBattleStatsHelper.TryGetStats(owner, out HbrBattleStats stats))
        {
            // Exactly these application sites halve their raw input while
            // switching weapon form, before the fragile coefficient resolves.
            decimal applied = card is MimicExorcismArrowHbrgoSaki or MimicThunderGaleHbrgoSaki or
                MimicFullBodyShotHbrgoSaki or MimicIceFlowerThousandArrowsHbrgoSaki or ArmorBreakSoulStrikeHbrgoSaki
                ? ModeTransitionHelper.GetAdjustedEffectAmount(card, modifiers) : modifiers;
            value = HbrFragilePowerHelper.EvaluatePercent(applied, stats.Intelligence, stats.Luck,
                HbrEnemyProfileHelper.GetEnemyParameter(target));
            if (decimal.Truncate(applied) > 0m)
                value = HotaruUnfadingScarsPower.EnhanceEffect(target, value);
        }
        string number = value.ToString("0.#", CultureInfo.InvariantCulture);
        return upgraded || value > basis ? $"[green]{number}[/green]" :
            value < basis ? $"[red]{number}[/red]" : number;
    }
}
