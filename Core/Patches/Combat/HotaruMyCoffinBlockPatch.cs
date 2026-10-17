using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers
{
    internal static class HotaruMyCoffinBlockContext
    {
        private sealed class Intent
        {
            internal Creature Owner;
            internal decimal Amount;
            internal bool Active = true;
            internal bool Claimed;
        }

        internal sealed class Scope
        {
            internal Scope Previous;
            internal Creature Owner;
            internal bool Enabled;
            internal bool Active = true;
            internal bool Rounded;
        }

        private static readonly AsyncLocal<Intent> Pending = new();
        private static readonly AsyncLocal<Scope> Current = new();

        internal static async Task<decimal> Gain(Creature owner, decimal amount)
        {
            if (owner == null || amount <= 0m) return 0m;
            Intent previous = Pending.Value;
            Intent intent = new() { Owner = owner, Amount = amount };
            Pending.Value = intent;
            try { return await CreatureCmd.GainBlock(owner, amount, ValueProp.Unpowered, null); }
            finally
            {
                intent.Active = false;
                if (ReferenceEquals(Pending.Value, intent)) Pending.Value = previous;
            }
        }

        internal static Scope Begin(Creature owner, decimal amount, ValueProp props, CardPlay play)
        {
            Intent intent = Pending.Value;
            Scope previous = Current.Value;
            if (intent?.Active != true && previous?.Active != true) return null;
            bool enabled = intent?.Active == true && !intent.Claimed && ReferenceEquals(owner, intent.Owner) &&
                amount == intent.Amount && props == ValueProp.Unpowered && play == null;
            if (enabled) intent.Claimed = true;
            Scope scope = new() { Previous = previous, Owner = owner, Enabled = enabled };
            Current.Value = scope;
            return scope;
        }

        internal static decimal Round(Creature owner, ValueProp props, CardPlay play, decimal amount)
        {
            Scope scope = Current.Value;
            if (scope?.Active != true || !scope.Enabled || scope.Rounded ||
                !ReferenceEquals(owner, scope.Owner) || props != ValueProp.Unpowered || play != null) return amount;
            scope.Rounded = true;
            decimal room = Math.Max(0m, 999_999_999m - owner.Block);
            return decimal.Floor(Math.Clamp(amount, 0m, room));
        }

        internal static void Restore(Scope scope)
        {
            if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
        }

        internal static void Close(Scope scope)
        {
            if (scope == null) return;
            scope.Active = false;
            Restore(scope);
        }

        internal static async Task<decimal> Finish(Task<decimal> task, Scope scope)
        {
            try { return await task; }
            finally { Close(scope); }
        }
    }
}

namespace HBRGOSakiMod.Core.Patches.Combat
{
    [HarmonyPatch]
    internal static class HotaruMyCoffinBlockScopePatch
    {
        private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(CreatureCmd),
            nameof(CreatureCmd.GainBlock), new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool) })
            ?? throw new MissingMethodException(typeof(CreatureCmd).FullName, "GainBlock decimal overload");

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Creature creature, decimal amount, ValueProp props, CardPlay cardPlay,
            out Helpers.HotaruMyCoffinBlockContext.Scope __state) =>
            __state = Helpers.HotaruMyCoffinBlockContext.Begin(creature, amount, props, cardPlay);

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Helpers.HotaruMyCoffinBlockContext.Scope __state, ref Task<decimal> __result)
        {
            if (__state == null) return;
            try { __result = Helpers.HotaruMyCoffinBlockContext.Finish(__result, __state); }
            finally { Helpers.HotaruMyCoffinBlockContext.Restore(__state); }
        }

        private static Exception Finalizer(Exception __exception, Helpers.HotaruMyCoffinBlockContext.Scope __state)
        {
            if (__exception != null) Helpers.HotaruMyCoffinBlockContext.Close(__state);
            else Helpers.HotaruMyCoffinBlockContext.Restore(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
    internal static class HotaruMyCoffinBlockBoundaryPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Creature target, ValueProp props, CardPlay cardPlay, ref decimal __result) =>
            __result = Helpers.HotaruMyCoffinBlockContext.Round(target, props, cardPlay, __result);
    }
}
