using System;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// A temporary stat power already received the card's Roll multiplier. Its
// internal stat application must use that resolved amount without rolling again.
internal static class TemporaryStatRollScope
{
    private static readonly AsyncLocal<Scope> Current = new();

    internal sealed class Scope : IDisposable
    {
        internal Scope Previous;
        internal Type StatType;
        internal Creature Target;
        internal Creature Applier;
        internal CardModel CardSource;
        internal decimal Amount;

        public void Dispose()
        {
            if (ReferenceEquals(Current.Value, this))
                Current.Value = Previous;
        }
    }

    internal static Scope Begin(PowerModel power, Creature target,
        decimal amount, Creature applier, CardModel cardSource)
    {
        Type statType = power switch
        {
            TemporaryStrengthPower => typeof(StrengthPower),
            TemporaryDexterityPower => typeof(DexterityPower),
            TemporaryFocusPower => typeof(FocusPower),
            _ => null
        };
        if (statType == null) return null;

        Scope scope = new()
        {
            Previous = Current.Value,
            StatType = statType,
            Target = target,
            Applier = applier,
            CardSource = cardSource,
            Amount = power.Type == PowerType.Debuff ? -amount : amount
        };
        Current.Value = scope;
        return scope;
    }

    internal static bool IsInternalApplication(PowerModel power, Creature giver,
        decimal amount, Creature target, CardModel cardSource)
    {
        Scope scope = Current.Value;
        return scope != null && power?.GetType() == scope.StatType &&
               ReferenceEquals(target, scope.Target) &&
               ReferenceEquals(giver, scope.Applier) &&
               ReferenceEquals(cardSource, scope.CardSource) &&
               amount == scope.Amount;
    }
}
