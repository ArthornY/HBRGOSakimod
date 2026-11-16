using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrEnemyStatusRuntime
{
    private static readonly ConditionalWeakTable<object, HashSet<(Creature Owner, Type Kind)>> Events = new();
    internal static bool TryOnce(object token, Creature owner, Type kind) => token != null && owner != null &&
        Events.GetOrCreateValue(token).Add((owner, kind));

    // The percentage has already resolved its complete damage budget. Spend
    // DP before sending only its overflow through the canonical HP-only route.
    internal static async Task PercentHpDamage(PlayerChoiceContext context, Creature owner,
        decimal percent, Creature applier)
    {
        if (owner?.IsAlive != true || percent <= 0m) return;
        decimal budget = decimal.Floor(DamageAmplificationLimits.MultiplySaturated(
            LargeCreatureHealthHelper.GetMaxHp(owner), percent / 100m));
        if (budget <= 0m) return;
        budget = await CapBudget(owner, budget, applier);
        if (budget <= 0m || !owner.IsAlive) return;
        decimal dpBudget = 0m;
        bool dpEmpty = true;
        if (owner.Player?.GetRelic<DeflectorProtection>() is { } protection)
        {
            protection.EnsureDpInitialized();
            dpBudget = Math.Min(budget, protection.Dp);
            if (protection.LoseDp((int)Math.Min(int.MaxValue, dpBudget)) > 0)
                await protection.ResolveMentalStrengthDpLossRewards(context);
            dpEmpty = protection.Dp <= 0;
        }
        else if (owner.GetPower<HighDimensionalShellPower>() is { CurrentDp: > 0m } shell)
        {
            dpBudget = Math.Min(budget, shell.CurrentDp);
            await shell.DamageDpOnly(context, dpBudget, applier);
            dpEmpty = shell.CurrentDp <= 0m;
        }
        if (dpEmpty && budget > dpBudget && owner.IsAlive)
            await HpOnlyDamageContext.Damage(context, owner, budget - dpBudget, applier, null);
    }

    private static async Task<decimal> CapBudget(Creature owner, decimal amount, Creature dealer)
    {
        var combat = owner.CombatState;
        var run = combat?.RunState;
        if (run == null) return amount;
        List<AbstractModel> changed = new();
        decimal lowest = decimal.MaxValue;
        foreach (AbstractModel model in run.IterateHookListeners(combat))
        {
            if (PhantomHelper.IsSummonerOwnedDamageModifier(model, dealer)) continue;
            decimal cap = System.Math.Max(0m, PhantomIncomingDamageHelper.ModifyDamageCap(model, owner,
                ValueProp.Unpowered | ValueProp.Unblockable, dealer, null, null));
            if (cap >= lowest) continue;
            lowest = cap;
            if (amount <= cap) continue;
            amount = cap;
            changed.Add(model);
        }
        if (changed.Count > 0) await Hook.AfterModifyingDamageAmount(run, combat, null, changed);
        return decimal.Floor(amount);
    }
}
