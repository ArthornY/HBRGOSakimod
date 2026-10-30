using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

static partial class Checks
{
    static readonly List<(decimal Units, Creature? Target, int Hits)> Attacks = [];
    static List<PowerModel> Powers(Creature creature) => (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(creature)!;
    static T Power<T>(Creature owner, int amount) where T : PowerModel
    {
        T power = Blank<T>(); Set(power, "<IsMutable>k__BackingField", true);
        Set(power, "_owner", owner); Set(power, "_amount", amount); Powers(owner).Add(power); return power;
    }
    static FragilePower Fragile(Creature target, decimal percent, int turns, int raw)
    {
        FragilePower power = Power<FragilePower>(target, turns);
        Call(typeof(FragilePower), "ConfigureHbr", power, percent, raw); return power;
    }
    static decimal Total(Creature target) => (decimal)Call(Helper("HbrFragilePowerHelper"), "GetTotalEffectivePercent", null, target)!;
    static CardModel AttackCard(Type type)
    { CardModel card = Card(type); card.UpgradePreviewType = CardUpgradePreviewType.Combat; return card; }
    static decimal Units(CardModel card, Creature? target = null) => (decimal)Call(card.GetType(), "GetDamageUnits", card,
        card is PiercingShotHbrgoSaki ? [target] : [])!;
    static void Equal(decimal actual, decimal expected, string label) => Check(Math.Abs(actual - expected) < 0.000000000000000001m,
        label + $": {actual} = {expected}");
    static bool NeverEnd(ref bool __result) { __result = false; return false; }
    static bool CaptureAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        Attacks.Add(((decimal)Field(typeof(AttackCommand), "_damagePerHit").GetValue(__instance)!,
            (Creature?)Field(typeof(AttackCommand), "_singleTarget").GetValue(__instance),
            (int)Field(typeof(AttackCommand), "_hitCount").GetValue(__instance)!));
        __result = Task.FromResult(__instance); return false;
    }
    static async Task Play(CardModel card)
    {
        Attacks.Clear(); var play = Blank<CardPlay>(); Set(play, "<Card>k__BackingField", card);
        await (Task)Call(card.GetType(), "OnPlay", card, new ThrowingPlayerChoiceContext(), play)!;
    }
    static async Task FragileAttackCases()
    {
        var h = new Harmony("hbr.fragile.attacks.real.play");
        h.Patch(AccessTools.Method(typeof(CombatState), "IterateHookListeners"),
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(EmptyListeners), F)!));
        h.Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"),
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(NeverEnd), F)!));
        h.Patch(AccessTools.Method(typeof(AttackCommand), "Execute"),
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(CaptureAttack), F)!));
        Creature other = Blank<Creature>(); Set(other, "_powers", new List<PowerModel>());
        Set(other, "<Side>k__BackingField", CombatSide.Enemy); Set(other, "_maxHp", 10000); Set(other, "_currentHp", 10000);
        other.CombatState = Hero.CombatState;
        var enemies = (List<Creature>)Field(typeof(CombatState), "_enemies").GetValue(Hero.CombatState)!;
        enemies.Add(other); Power<ReptureBowPower>(Hero, 1);
        try
        {
            Equal(Total(Enemy), 0m, "no Fragile has zero effective percentage");
            FragilePower first = Fragile(Enemy, 30m, 2, 999);
            FragilePower second = Fragile(Enemy, 37.5m, 1, 1);
            Fragile(Enemy, 1000m, 0, 1000);
            Equal(Total(Enemy), 67.5m, "active exact percentages sum, duration and raw modifier counters do not contribute");
            Check(!Enemy.HasPower<BreakPower>(), "attack coefficient reads existing Fragile before Break");
            Fragile(other, 90m, 2, 1);
            var piercing = AttackCard(typeof(PiercingShotHbrgoSaki)); var strong = AttackCard(typeof(StrongShotHbrgoSaki));
            Equal(Units(piercing, Enemy), 7.43m, "Piercing own67point5 adds40point5 skill percentage points, raw5 plus2point43");
            Equal(Units(piercing, other), 8.24m, "Piercing other90 uses that target alone");
            Equal(Units(piercing), 5m, "Piercing no selected target keeps base coefficient");
            Equal(Units(strong), 6.24m, "Strong uses greatest summed enemy percentage90 per hit");
            await Play(piercing);
            Check(Attacks.Count == 2 && Attacks[0].Target == Enemy && Attacks[1].Target == other,
                "actual Piercing OnPlay produces one independently calculated attack per enemy");
            Equal(Attacks[0].Units, 7.43m, "actual Piercing first target raw units");
            Equal(Attacks[1].Units, 8.24m, "actual Piercing second target raw units");
            await Play(strong);
            Check(Attacks.Count == 1 && Attacks[0].Target == null && Attacks[0].Hits == 3, "actual Strong requests one all-target command containing three hits");
            Check(Attacks.All(a => a.Units == 6.24m), "actual Strong all three hits use same strongest Fragile bonus");
            string text = Render(piercing, true, target: Enemy);
            Check(text.Contains("（123.83%）") && text.Contains("6‰"), "selected Piercing detail displays adjusted skill coefficient and6permille rule");
            Check(Render(piercing, true).Contains("（83.33%）"), "unselected Piercing detail invents no target percentage");
            Check(Render(strong, true).Contains("（104%）"), "Strong detail includes greatest enemy percentage in coefficient");
            var scars = Power<HotaruUnfadingScarsPower>(Hero, 1);
            typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(scars, 25);
            Equal(Total(Enemy), 84.375m, "Scars final multiplier enhances exact sum once");
            Equal(Units(piercing, Enemy), 8.0375m, "Piercing uses effective84point375 without display rounding");
            Equal(Units(strong), 7.05m, "Strong includes final Scars enhancement on maximum90to112point5");
            Powers(Hero).Remove(scars);
            Power<BreakPower>(Enemy, 1);
            Equal(Units(piercing, Enemy), 7.43m, "Break activation does not change the coefficient measurement");
            Upgrade(piercing); Upgrade(strong);
            Equal(Units(piercing, Enemy), 9.86m, "upgraded Piercing retains doubled bonus12permille");
            Equal(Units(strong), 10.48m, "upgraded Strong retains base raw4 and doubled bonus12permille");
            Check(Render(piercing, true, target: Enemy).Contains("12‰"), "Piercing upgrade description binds12permille");
            Check(Render(strong, true).Contains("12‰"), "Strong upgrade description binds12permille");
            await Play(piercing); Equal(Attacks[0].Units, 9.86m, "actual upgraded Piercing uses12permille");
            await Play(strong); Check(Attacks.Count == 1 && Attacks[0].Hits == 3 && Attacks[0].Units == 10.48m, "actual upgraded Strong retains3hits and12permille in one command");
            Set(other, "_currentHp", 0);
            Equal(Units(strong), 8.86m, "Strong excludes unhittable former strongest target"); Set(other, "_currentHp", 10000);
            Powers(Enemy).Clear(); Powers(other).Clear();
            Fragile(Enemy, 12.3456789m, 1, 999);
            Equal(Units(Card(typeof(PiercingShotHbrgoSaki)), Enemy), 5.4444444404m, "full precision survives coefficient conversion without early flooring");
            Powers(Enemy).Clear(); Fragile(Enemy, decimal.MaxValue, 2, 1); Fragile(Enemy, decimal.MaxValue, 2, 1);
            Equal(Total(Enemy), decimal.MaxValue, "pathological independent percentages saturate safely");
            Check(Units(piercing, Enemy) > 0m, "large Fragile coefficient does not overflow");
            Powers(Enemy).Clear(); Powers(other).Clear();
            Fragile(Enemy, 30m, 5, 999); Mode(false);
            piercing = AttackCard(typeof(PiercingShotHbrgoSaki)); strong = AttackCard(typeof(StrongShotHbrgoSaki));
            await Play(piercing); Equal(Attacks[0].Units, 10m, "legacy Piercing retains flat1 per native stack");
            await Play(strong); Check(Attacks.Count == 1 && Attacks[0].Hits == 3 && Attacks[0].Units == 8m, "legacy Strong retains3hits and flat1 per highest native stack");
            Upgrade(piercing); Upgrade(strong);
            await Play(piercing); Equal(Attacks[0].Units, 15m, "legacy upgraded Piercing retains flat2 per stack");
            await Play(strong); Check(Attacks.All(a => a.Units == 14m), "legacy upgraded Strong retains raw4 plus flat2perstack");
            Check(Render(piercing, false).Contains("每有1层脆弱") && !Render(piercing, false).Contains("‰"), "legacy description stays unchanged");
        }
        finally { Mode(true); Powers(Hero).Clear(); Powers(Enemy).Clear(); enemies.Remove(other); h.UnpatchAll(h.Id); }
    }
    public static async Task RunFragileAttacks()
    {
        bool ok = false; string? error = null;
        try { Install(); Localization(); World(); await FragileAttackCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new {
            ok, checks = Passed.Count, passed = Passed, rows = Rows, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            localizationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,"HBRGOSakiMod/localization/zhs/cards.json")))),
            limits = "Real production percentage summation, card GetDamageUnits, actual Piercing/Strong OnPlay, upgrades, contextual description and native SmartFormat execute. AttackCommand.Execute is a controlled leaf capturing raw per-hit units and target; no actual damage or rendered Godot UI is claimed. Source-only validation, no packaging or Steam upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} Fragile percentage attack checks");
        if (!ok) throw new Exception("Fragile attack checks failed; inspect report.json");
    }
}
