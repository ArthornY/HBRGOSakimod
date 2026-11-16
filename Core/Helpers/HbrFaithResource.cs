using System.Numerics;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Common resource API for current and future cards. Fixed resource gains do
// not run percentage-buff scaling or inherit the applying card's dice roll.
public static class HbrFaithResource
{
    public static BigInteger Get(Creature holder) => holder?.GetPower<HbrFaithPower>()?.ExactAmount ?? BigInteger.Zero;
    public static void Gain(Creature holder, BigInteger amount)
    {
        if (amount <= 0 || holder?.Player == null || holder.CombatState == null || !holder.IsAlive || !HbrBattleStatsHelper.IsEnabled(holder)) return;
        HbrFaithPower power = holder.GetPower<HbrFaithPower>();
        if (power != null) { power.SetExact(power.ExactAmount + amount); return; }
        power = (HbrFaithPower)ModelDb.Power<HbrFaithPower>().ToMutable();
        power.ApplyExact(holder, amount);
    }
    public static bool TrySpend(Creature holder, BigInteger amount)
    {
        HbrFaithPower power = holder?.GetPower<HbrFaithPower>();
        if (amount <= 0 || power == null || power.ExactAmount < amount) return false;
        power.SetExact(power.ExactAmount - amount); return true;
    }
    public static void Clear(Creature holder) => holder?.GetPower<HbrFaithPower>()?.RemoveInternal();
}
