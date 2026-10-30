using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ValueProps;

// Reuse the existing native fixture without adding another setup or mocking the
// native Weak/Vulnerable hooks. The one Run call is kept in Program.cs.
static class ScarsChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type Fixture = typeof(Checks);
    static readonly Assembly Mod = typeof(FragilePower).Assembly;
    static object? Invoke(string name, params object?[] args) => Fixture.GetMethod(name, F)!.Invoke(null, args);
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static void Reset() => Invoke("Reset");
    static void Mode(bool enabled) => Invoke("Mode", enabled, 1);
    static void Set(object owner, string field, object? value) => Invoke("Set", owner, field, value);
    static void Check(bool valid, string text) => Invoke("Check", valid, "Scars final effect: " + text);
    static void Equal(decimal actual, decimal expected, string text) => Invoke("Equal", actual, expected, "Scars final effect: " + text);
    static T Power<T>(Creature owner, int amount = 2) where T : PowerModel =>
        (T)Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)).Invoke(null, [owner, amount])!;
    static T Blank<T>() => (T)Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)).Invoke(null, null)!;
    static List<PowerModel> Powers(Creature owner) => (List<PowerModel>)Invoke("Powers", owner)!;
    static HotaruUnfadingScarsPower Scars(int percent, int copies = 1, Creature? owner = null)
    {
        var power = Power<HotaruUnfadingScarsPower>(owner ?? State<Creature>("Hero"), copies);
        typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(power, percent);
        return power;
    }
    static decimal Damage(Creature target, Creature dealer, ValueProp props = ValueProp.Move) =>
        (decimal)Invoke("NativeDamage",target,dealer,1000m,props,null,ModifyDamageHookType.Multiplicative)!;
    static decimal NativeMultiplier(PowerModel power,Creature dealer,Creature target,ValueProp props) =>
        (decimal)Mod.GetType("HBRGOSakiMod.Core.Helpers.DamageModifierCompatibility",true)!
            .GetMethod("ModifyDamageMultiplicative",F)!.Invoke(null,[power,target,1000m,props,dealer,null,null])!;
    static decimal Weak(WeakPower power, Creature dealer, Creature target, ValueProp props = ValueProp.Move) =>
        NativeMultiplier(power,dealer,target,props);
    static decimal Vulnerable(VulnerablePower power, Creature dealer, Creature target, ValueProp props = ValueProp.Move) =>
        NativeMultiplier(power,dealer,target,props);
    static void WithRelic<T>(Action test) where T : RelicModel
    {
        var relic = Blank<T>();
        Set(relic, "<IsMutable>k__BackingField", true);
        Set(relic, "_owner", State<Player>("Player"));
        FieldInfo field = (FieldInfo)Fixture.GetMethod("Field", F)!.Invoke(null, [typeof(Player), "_relics"])!;
        var relics = (List<RelicModel>)field.GetValue(State<Player>("Player"))!;
        relics.Add(relic);
        try { test(); }
        finally { relics.Remove(relic); }
    }
    internal static void Run()
    {
        var h = new Harmony("hbr.fragile.scars.final-effect.checks");
        foreach (string name in new[] { "HotaruUnfadingScarsWeakPatch", "HotaruUnfadingScarsVulnerablePatch" })
        {
            Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            Check(h.CreateClassProcessor(patch).Patch().Count > 0, "installs real production " + name);
        }
        Creature hero = State<Creature>("Hero"), enemy = State<Creature>("Enemy");
        try
        {
            Reset();
            var weak = Power<WeakPower>(enemy);
            var vulnerable = Power<VulnerablePower>(enemy);
            Equal(Weak(weak, enemy, hero), .75m, "native Weak remains 25 percent without upgraded Scars");
            Equal(Vulnerable(vulnerable, hero, enemy), 1.5m, "native Vulnerable remains 50 percent without upgraded Scars");
            var scars = Scars(0);
            Equal(Weak(weak, enemy, hero), .75m, "unupgraded Scars adds no extra Weak reduction");
            Equal(Vulnerable(vulnerable, hero, enemy), 1.5m, "unupgraded Scars adds no extra Vulnerable damage");
            typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(scars, 25);
            Equal(Weak(weak, enemy, hero), .75m, "HBR Scars leaves native Weak at its original 25 percent reduction");
            Equal(Vulnerable(vulnerable, hero, enemy), 1.5m, "HBR Scars leaves native Vulnerable at its original 50 percent increase");
            Equal(Weak(weak, hero, enemy), 1m, "Weak still checks its dealer owner");
            Equal(Vulnerable(vulnerable, enemy, hero), 1m, "Vulnerable still checks its target owner");
            Equal(Weak(weak, enemy, hero, (ValueProp)0), 1m, "Weak does not affect unpowered damage");
            Equal(Vulnerable(vulnerable, hero, enemy, (ValueProp)0), 1m, "Vulnerable does not affect unpowered damage");
            Equal(Damage(hero, enemy), 750m, "native Weak retains its original value through the complete HBR damage hook");
            Equal(Damage(enemy, hero), 1500m, "native Vulnerable retains its original value through HBR defense-down aggregation");

            var fragile = (FragilePower)Invoke("Apply", 1, hero, enemy, null, false, true)!;
            Power<BreakPower>(enemy, 1);
            Equal(fragile.ModifyDamageMultiplicative(enemy, 1000m, ValueProp.Move, hero, null!), 1.375m,
                "30 percent Fragile becomes 37.5 percent rather than multiplying the whole 1.3 factor");
            Equal(Damage(enemy, hero), 2062m, "enhanced Fragile multiplies with the independent native Vulnerable bucket before final flooring");
            Invoke("Apply", 2, hero, enemy, null, false, true);
            Equal(Damage(enemy, hero), 2765m, "two additive Fragile instances each receive Scars once before unchanged native Vulnerable multiplies");
            PowerCmd.TickDownDuration(weak).GetAwaiter().GetResult();
            PowerCmd.TickDownDuration(vulnerable).GetAwaiter().GetResult();
            Check(weak.Amount == 1 && vulnerable.Amount == 1, "HBR Scars leaves native Weak and Vulnerable natural duration decay unchanged");
            PowerCmd.TickDownDuration(fragile).GetAwaiter().GetResult();
            Check(fragile.Amount == 2, "HBR Scars still protects Fragile natural duration decay");

            Reset(); Mode(false);
            Scars(25); weak = Power<WeakPower>(enemy); vulnerable = Power<VulnerablePower>(enemy);
            Power<DebilitatePower>(enemy, 1);
            Equal(Weak(weak, enemy, hero), .375m, "Scars scales final native Debilitate Weak effect");
            Equal(Vulnerable(vulnerable, hero, enemy), 2.25m, "Scars scales final native Debilitate Vulnerable effect");
            Equal(Damage(enemy, hero), 2250m, "Debilitate plus Scars Vulnerable is not collected twice");
            WithRelic<PaperKrane>(() =>
            {
                Equal(Weak(weak, enemy, hero), 0m, "PaperKrane and Debilitate resolve before final Scars multiplication");
                Equal(Damage(hero, enemy), 0m, "full Weak reduction is clamped at zero damage");
            });
            WithRelic<PaperPhrog>(() =>
            {
                Equal(Vulnerable(vulnerable, hero, enemy), 2.875m, "PaperPhrog and Debilitate resolve before final Scars multiplication");
                Equal(Damage(enemy, hero), 2875m, "native relic modified Vulnerable survives final shared damage route");
            });

            Reset(); Mode(false);
            Scars(25); vulnerable = Power<VulnerablePower>(enemy); Power<CrueltyPower>(hero, 20);
            Equal(Vulnerable(vulnerable, hero, enemy), 1.875m, "native Cruelty offset is included before the final Scars factor");
            Equal(Damage(enemy, hero), 1875m, "Cruelty and Scars are applied once through the shared hook");

            Reset(); Mode(false);
            Scars(50, 2); weak = Power<WeakPower>(enemy); vulnerable = Power<VulnerablePower>(enemy);
            Equal(Weak(weak, enemy, hero), .625m, "two pooled upgraded applications use 1.5 rather than 1.25 squared");
            Equal(Vulnerable(vulnerable, hero, enemy), 1.75m, "pooled 50 percent Scars enhances Vulnerable by exactly 50 percent");
            Equal(Damage(enemy, hero), 1750m, "pooled Scars reaches the pipeline without multiplying its Amount again");
            PowerCmd.TickDownDuration(weak).GetAwaiter().GetResult();
            PowerCmd.TickDownDuration(vulnerable).GetAwaiter().GetResult();
            Check(weak.Amount == 2 && vulnerable.Amount == 2, "legacy Scars retains native Weak and Vulnerable natural duration protection");

            Reset();
            Scars(25, owner: enemy); weak = Power<WeakPower>(hero); vulnerable = Power<VulnerablePower>(hero);
            Equal(Weak(weak, hero, enemy), .75m, "Scars does not enhance debuffs on the player side");
            Equal(Vulnerable(vulnerable, enemy, hero), 1.5m, "enemy owned Scars does not turn into an allied aura");
        }
        finally { Reset(); }
    }
}
