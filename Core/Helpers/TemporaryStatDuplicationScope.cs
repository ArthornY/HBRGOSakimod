using System;
using System.Threading;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Total Strength/Dexterity/Focus already includes its temporary powers. When
// both counters are copied, skip only that temporary power's internal stat
// application. An async scope also works with game versions without the old
// ITemporaryPower.IgnoreNextInstance API.
internal sealed class TemporaryStatDuplicationScope : IDisposable
{
    private static readonly AsyncLocal<TemporaryStatDuplicationScope> Current = new();

    private readonly TemporaryStatDuplicationScope _previous;
    private readonly PowerModel _power;
    private readonly CardModel _cardSource;

    private TemporaryStatDuplicationScope(PowerModel power, CardModel cardSource)
    {
        _previous = Current.Value;
        _power = power;
        _cardSource = cardSource;
        Current.Value = this;
    }

    internal static TemporaryStatDuplicationScope Begin(PowerModel power, CardModel cardSource) =>
        power is TemporaryStrengthPower or TemporaryDexterityPower or TemporaryFocusPower
            ? new(power, cardSource)
            : null;

    internal static bool IsCopiedCounter(PowerModel power, CardModel cardSource) =>
        Current.Value is { } scope && ReferenceEquals(power, scope._power) &&
        ReferenceEquals(cardSource, scope._cardSource);

    public void Dispose()
    {
        if (ReferenceEquals(Current.Value, this))
            Current.Value = _previous;
    }
}
