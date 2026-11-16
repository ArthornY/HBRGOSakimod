using System.Reflection;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

static partial class FrameworkChecks
{
    static void BossDebuffCases()
    {
        foreach (string kind in new[] { "ApplyAttackDown", "ApplyDefenseDown" })
        foreach (int turns in new[] { 1, 2, 3, 4, 99 })
        foreach (bool playerTarget in new[] { true, false })
        {
            Reset(); Creature target = playerTarget ? Hero : Enemy;
            MethodInfo apply = Helper("HbrBossDebuffApplication").GetMethods(F).Single(method =>
                method.Name == kind && method.GetParameters()[1].ParameterType == typeof(Creature));
            ((Task)Invoke(apply, null, null, target, (decimal)turns, playerTarget ? Enemy : Hero, null)!).GetAwaiter().GetResult();
            PowerModel power = Powers(target).Single();
            Check(power is HbrAttackDownPower or HbrDefenseDownPower,
                $"boss {kind}/{turns}/{playerTarget} uses the HBR effect, not a native replacement");
            Equal((int)Call("HbrPercentDebuffHelper", "GetModifierAmount", power)!, 1m,
                "boss duration argument never becomes exponential strength modifiers");
            decimal potency = (decimal)Call("HbrPercentDebuffHelper", "GetEffectPercent", power)!;
            // Enemy casts now retain native Weak/Vulnerable's 25%/50% base;
            // a friendly phantom replay retains the authored 15% base.
            decimal basis = playerTarget ? kind == "ApplyAttackDown" ? 25m : 50m : 15m;
            object weights = Helper("HbrEffectFormula").GetField("IntelligenceLuck", F)!.GetValue(null)!;
            decimal expected = (decimal)Call("HbrEffectFormula", "Debuff", basis,
                playerTarget ? Enemy : Hero, target, weights)!;
            Equal(potency, expected, "boss strength uses its authored base and ordinary stat formula, never duration");
            Equal(power.Amount, turns == 99 ? 1m : turns + (playerTarget ? 1 : 0),
                "player-target native first-end skip is encoded separately; phantom enemy target keeps original duration");
            object profile = Call("HbrEffectProfileResolver", "Resolve", power, null)!;
            Check(profile.GetType().GetProperty("Limit", F)!.GetValue(profile)!.ToString() == "Default",
                "special boss retains Default slots instead of table-monster unlimited policy");
            if (turns == 99)
            {
                Tick(power, CombatSide.Enemy, Enemy); Tick(power, CombatSide.Player, Hero);
                Equal(power.Amount, 1m, "boss99 becomes Eternal and survives both side ends");
                continue;
            }
            int duration = turns + (playerTarget ? 1 : 0);
            for (int i = 1; i < duration; i++) Tick(power, CombatSide.Enemy, Enemy);
            Check(Powers(target).Contains(power) && power.Amount == 1,
                "boss debuff survives all intended enemy boundaries before its final expiration");
            Tick(power, CombatSide.Enemy, Enemy);
            Check(!Powers(target).Contains(power), "boss debuff expires on its authored final boundary");
        }
        Reset();
        MethodInfo scalar = Helper("HbrBossDebuffApplication").GetMethods(F).Single(method =>
            method.Name == "ApplyAttackDown" && method.GetParameters()[1].ParameterType == typeof(Creature));
        for (int i = 0; i < 3; i++) ((Task)Invoke(scalar, null, null, Hero, 4m, Enemy, null)!).GetAwaiter().GetResult();
        Check(Existing<HbrAttackDownPower>().Length == 2, "three special-boss applications retain the shared two-instance cap");
        Reset();
    }
}
