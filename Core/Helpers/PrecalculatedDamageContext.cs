using System;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PrecalculatedDamageContext
{
    private static readonly AsyncLocal<State> _current = new();

    internal static IDisposable Begin(
        CardModel card,
        Creature target,
        decimal amount)
    {
        State previous = _current.Value;
        _current.Value = new State(card, target, amount);
        return new Scope(previous);
    }

    internal static bool Matches(
        CardModel card,
        Creature target,
        decimal amount)
    {
        State state = _current.Value;
        return state != null &&
            ReferenceEquals(state.Card, card) &&
            ReferenceEquals(state.Target, target) &&
            state.Amount == amount;
    }

    private sealed class Scope : IDisposable
    {
        private readonly State _previous;
        private bool _isDisposed;

        internal Scope(State previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _current.Value = _previous;
        }
    }

    private sealed record State(
        CardModel Card,
        Creature Target,
        decimal Amount);
}
