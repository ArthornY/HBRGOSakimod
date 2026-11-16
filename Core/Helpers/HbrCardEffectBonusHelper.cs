using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Source-owned card marks share one additive percentage and are applied once
// to each damage, block or recovery budget, before resource allocation.
internal static class HbrCardEffectBonusHelper
{
    [ThreadStatic] private static CardModel _preview;
    private sealed class PreviewScope : IDisposable
    {
        private readonly CardModel _previous = _preview;
        internal PreviewScope(CardModel card) => _preview = card;
        public void Dispose() => _preview = _previous;
    }
    internal static IDisposable BeginPreview(CardModel card) => card?.IsMutable == true &&
        card.Owner?.Creature?.HasPower<VritikaCalculationPower>() == true ? new PreviewScope(card) : null;
    internal static decimal GetPercent(CardModel source, Creature dealer, ValueProp props)
    {
        if (source?.IsMutable != true) return 0m;
        decimal calculation = VritikaCalculationCombatHelper.GetCalculationBonusPercent(source, dealer, props);
        if (calculation == 0m && source != null && ReferenceEquals(_preview, source))
            calculation = VritikaCalculationCombatHelper.GetPreviewCalculationBonusPercent(source);
        return DamageAmplificationLimits.AddSaturated(
            YanagiButlerCombatHelper.GetPreparedBonusPercent(source, dealer, props), calculation);
    }

    internal static decimal ScaleRecovery(CardModel source, decimal amount) => amount > 0m && source?.IsMutable == true
        ? DamageAmplificationLimits.MultiplySaturated(amount, 1m +
            GetPercent(source, source.Owner?.Creature, ValueProp.Unpowered) / 100m) : amount;
}
