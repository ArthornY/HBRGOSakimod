using System;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCannonBlockHelper
{
    private sealed class Intent
    {
        internal Creature Owner;
        internal decimal BaseAmount;
        internal CardPlay Play;
        internal Creature Cannon;
        internal bool Active = true;
        internal bool Claimed;
    }

    internal sealed class GainScope
    {
        internal GainScope Previous;
        internal Creature Owner;
        internal CardPlay Play;
        internal Creature Cannon;
        internal bool Enabled;
        internal bool Active = true;
    }

    private static readonly AsyncLocal<Intent> Pending = new();
    private static readonly AsyncLocal<GainScope> Current = new();
    private static readonly AsyncLocal<int> PreviewDepth = new();

    internal static decimal Preview(Creature owner, decimal baseAmount, CardPlay sourcePlay = null, Creature cannon = null)
    {
        if (owner?.CombatState == null) return FinalValue(baseAmount, cannon, false);
        // Preview observes the same live owner powers, card enchantment and
        // already resolved card Roll as the command; it never resolves a roll.
        int previous = PreviewDepth.Value;
        PreviewDepth.Value = previous + 1;
        try
        {
            return FinalValue(Hook.ModifyBlock(owner.CombatState, owner, baseAmount,
                ValueProp.Move, sourcePlay?.Card, sourcePlay, out _), cannon, false);
        }
        finally { PreviewDepth.Value = previous; }
    }

    internal static async Task<decimal> Gain(Creature owner, decimal baseAmount, CardPlay sourcePlay = null, Creature cannon = null)
    {
        if (owner == null || baseAmount <= 0m) return 0m;
        Intent previous = Pending.Value;
        Intent intent = new() { Owner = owner, BaseAmount = baseAmount, Play = sourcePlay, Cannon = cannon };
        // Resolve independent Skill dice only for the real cannon phase. The
        // original CardPlay remains unchanged for native source-card modifiers.
        if (cannon != null) HotaruCannonSupportUpgradeHelper.GetCannonRollMultiplier(cannon, CardType.Skill, resolve: true);
        Pending.Value = intent;
        try
        {
            // Pass the base value, not Preview's final value: native additive,
            // multiplicative and card-source hooks must run exactly once.
            return await CreatureCmd.GainBlock(owner, baseAmount, ValueProp.Move, sourcePlay);
        }
        finally
        {
            intent.Active = false;
            if (ReferenceEquals(Pending.Value, intent)) Pending.Value = previous;
        }
    }

    internal static GainScope Begin(Creature owner, decimal amount, ValueProp props, CardPlay play)
    {
        Intent intent = Pending.Value;
        GainScope parent = Current.Value;
        if (intent?.Active != true && parent?.Active != true) return null;
        bool enabled = intent?.Active == true && !intent.Claimed && props == ValueProp.Move &&
            ReferenceEquals(intent.Owner, owner) && intent.BaseAmount == amount && ReferenceEquals(intent.Play, play);
        if (enabled) intent.Claimed = true;
        GainScope scope = new() { Previous = parent, Owner = owner, Play = play,
            Cannon = enabled ? intent.Cannon : null, Enabled = enabled };
        Current.Value = scope;
        return scope;
    }

    internal static bool RoundsCurrentGain(Creature owner, ValueProp props, CardPlay play)
    {
        GainScope scope = Current.Value;
        return PreviewDepth.Value == 0 && scope?.Active == true && scope.Enabled && props == ValueProp.Move &&
            ReferenceEquals(scope.Owner, owner) && ReferenceEquals(scope.Play, play);
    }

    internal static decimal Floor(decimal amount) => decimal.Floor(Math.Max(0m, amount));

    internal static decimal FinalCurrentGain(decimal amount) => FinalValue(amount, Current.Value?.Cannon, false);

    private static decimal FinalValue(decimal amount, Creature cannon, bool resolve) => Floor(
        DamageAmplificationLimits.MultiplySaturated(amount,
            HotaruCannonSupportUpgradeHelper.GetBlockMultiplier(cannon) * 0.5m *
            HotaruCannonSupportUpgradeHelper.GetCannonRollMultiplier(cannon, CardType.Skill, resolve)));

    internal static void RestoreCaller(GainScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    internal static void Close(GainScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreCaller(scope);
    }

    internal static async Task<decimal> Finish(Task<decimal> original, GainScope scope)
    {
        try { return await original; }
        finally { Close(scope); }
    }
}
