using System;
using System.Threading;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed class ThirtyOneXAuthoredAttackScope : IDisposable
{
    private static readonly AsyncLocal<ThirtyOneXAuthoredAttackScope> Current = new();
    private readonly ThirtyOneXAuthoredAttackScope _previous;
    private readonly CardModel _source;
    private readonly AttackCommand _command;
    private bool _active = true;
    internal ThirtyOneXAuthoredAttackScope(CardModel source, AttackCommand command)
    { _source = source; _command = command; _previous = Current.Value; Current.Value = this; }
    internal static bool Applies(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (source?.Owner?.Creature != dealer || target == null || dealer == null || target.Side == dealer.Side ||
            !props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(dealer) || HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return false;
        if (HotaruCannonStatsHelper.IsDamagePreview) return true;
        for (var scope = Current.Value; scope != null; scope = scope._previous)
            if (scope._active && ReferenceEquals(scope._source, source))
                return HighHitDamageVisualHelper.IsDirectAttackDamage(scope._command);
        return false;
    }
    public void Dispose() { _active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = _previous; }
}
