using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrgoSakiDamageMultiplierHelper
{
    [ThreadStatic]
    private static Stack<AdditiveDamageContext> _contexts;

    internal static void Begin()
    {
        _contexts ??= new Stack<AdditiveDamageContext>();
        _contexts.Push(new AdditiveDamageContext());
    }

    internal static decimal Add(decimal multiplier, AbstractModel source)
    {
        if (_contexts == null || _contexts.Count == 0)
        {
            return multiplier;
        }

        if (multiplier != 1m)
        {
            _contexts.Peek().Add(multiplier, source);
        }

        return 1m;
    }

    internal static AdditiveDamageResult Complete()
    {
        if (_contexts == null || _contexts.Count == 0)
        {
            return AdditiveDamageResult.Empty;
        }

        AdditiveDamageContext context = _contexts.Pop();
        return new AdditiveDamageResult(
            context.MultiplierOffset,
            context.Sources.Distinct().ToArray());
    }

    internal static void Abort()
    {
        if (_contexts?.Count > 0)
        {
            _contexts.Pop();
        }
    }

    private sealed class AdditiveDamageContext
    {
        internal decimal MultiplierOffset { get; private set; }

        internal List<AbstractModel> Sources { get; } = new();

        internal void Add(decimal multiplier, AbstractModel source)
        {
            MultiplierOffset += multiplier - 1m;
            if (source != null)
            {
                Sources.Add(source);
            }
        }
    }
}

internal readonly struct AdditiveDamageResult
{
    internal static AdditiveDamageResult Empty { get; } =
        new(0m, Array.Empty<AbstractModel>());

    internal decimal MultiplierOffset { get; }

    internal IReadOnlyList<AbstractModel> Sources { get; }

    internal AdditiveDamageResult(
        decimal multiplierOffset,
        IReadOnlyList<AbstractModel> sources)
    {
        MultiplierOffset = multiplierOffset;
        Sources = sources ?? Array.Empty<AbstractModel>();
    }
}
