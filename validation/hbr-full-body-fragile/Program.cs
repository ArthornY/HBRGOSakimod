using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class FullBodyChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(MimicFullBodyShotHbrgoSaki).Assembly;
    static readonly Type Fixture = typeof(Checks);
    static readonly List<string> Passed = [];
    static readonly List<(Type Power, decimal Amount)> Requests = [];
    static readonly List<decimal> DamageRequests = [];
    static object? Invoke(string name, params object?[] args) => Fixture.GetMethod(name, F)!.Invoke(null, args);
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static void Set(object owner, string field, object? value) => Invoke("Set", owner, field, value);
    static T Blank<T>() => (T)Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)).Invoke(null, null)!;
    static T Power<T>(Creature owner, int amount) where T : PowerModel =>
        (T)Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)).Invoke(null, [owner, amount])!;
    static List<PowerModel> Powers(Creature owner) => (List<PowerModel>)Invoke("Powers", owner)!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string method, params object?[] args) => Helper(helper).GetMethod(method, F)!.Invoke(null, args);
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Passed.Add(name); }
    static void Equal(decimal actual, decimal expected, string name) => Check(Math.Abs(actual - expected) < .0000000000000000001m, name + $": {actual} = {expected}");
    static bool NoCombatEnd(ref bool __result) { __result = false; return false; }
    static bool HasRune(ref bool __result) { __result = true; return false; }
    static void ObservePower(PowerModel power, decimal amount) => Requests.Add((power.GetType(), amount));
    static void ObserveOffset(PowerModel power, decimal offset)
    {
        if (power is DestructionRatePower) Requests.Add((power.GetType(), offset));
    }
    static bool CaptureAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        FieldInfo field = (FieldInfo)Fixture.GetMethod("Field", F)!.Invoke(null, [typeof(AttackCommand), "_damagePerHit"])!;
        DamageRequests.Add((decimal)field.GetValue(__instance)!);
        __result = Task.FromResult(__instance); return false;
    }
    static MimicFullBodyShotHbrgoSaki Card(bool upgraded = false, bool zeroGrant = false)
    {
        var card = new MimicFullBodyShotHbrgoSaki();
        Set(card, "<IsMutable>k__BackingField", true); Set(card, "_owner", State<Player>("Player"));
        card.UpgradePreviewType = CardUpgradePreviewType.Combat;
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        if (zeroGrant) card.DynamicVars["FragilePower"].UpgradeValueBy(-card.DynamicVars["FragilePower"].BaseValue);
        return card;
    }
    static void Reset(bool bow = true)
    {
        Invoke("Reset"); Requests.Clear(); DamageRequests.Clear();
        if (!bow) return;
        var power = new ReptureBowPower();
        Set(power, "<IsMutable>k__BackingField", true); Set(power, "_owner", State<Creature>("Hero"));
        Set(power, "_amount", 1); Powers(State<Creature>("Hero")).Add(power);
    }
    static FragilePower Existing(decimal percent, int duration = 2)
    {
        var power = Power<FragilePower>(State<Creature>("Enemy"), duration);
        typeof(FragilePower).GetMethod("ConfigureHbr", F)!.Invoke(power, [percent, 1]); return power;
    }
    static async Task Play(CardModel card, Creature? target = null)
    {
        var play = Blank<CardPlay>(); Set(play, "<Card>k__BackingField", card);
        Set(play, "<Target>k__BackingField", target ?? State<Creature>("Enemy"));
        await (Task)typeof(MimicFullBodyShotHbrgoSaki).GetMethod("OnPlay", F)!.Invoke(card,
            [new ThrowingPlayerChoiceContext(), play])!;
    }
    static decimal[] GainRequests() => Requests.Where(row => row.Power == typeof(DestructionRatePower)).Select(row => row.Amount).ToArray();
    static decimal MultOnly(decimal raw) => Hook.ModifyDamage(State<RunState>("RunState"), State<CombatState>("Combat"),
        State<Creature>("Enemy"), State<Creature>("Hero"), raw, ValueProp.Move, null!,
        ModifyDamageHookType.Multiplicative, CardPreviewMode.None, out _);
    static async Task Cases()
    {
        Creature hero = State<Creature>("Hero"), enemy = State<Creature>("Enemy");
        foreach (bool upgraded in new[] { false, true })
        {
            Reset(); Power<BreakPower>(enemy, 1);
            CardModel card = Card(upgraded);
            Equal(card.DynamicVars["FragileDestructionThreshold"].BaseValue, upgraded ? 5m : 10m,
                "threshold is 10 percent or upgraded 5 percent");
            Equal(card.DynamicVars["FragilePower"].BaseValue, upgraded ? 6m : 3m, "authored Fragile grant retains original upgrade");
            await Play(card);
            Check(Requests.Where(row => row.Power == typeof(FragilePower)).Select(row => row.Amount)
                .SequenceEqual(new[] { upgraded ? 6m : 3m }), "actual OnPlay preserves its original matching-form Fragile grant");
            decimal percent = (decimal)Call("HbrFragilePowerHelper", "GetTotalEffectivePercent", enemy)!;
            Check(GainRequests().SequenceEqual(new[] { 1m, decimal.Floor(percent / (upgraded ? 5m : 10m)) }),
                "actual OnPlay grants baseline then floor of complete percentage intervals: received [" +
                    string.Join(',', GainRequests()) + "] for effective percent " + percent);
            Equal(DamageRequests.Single(), percent * .1m, "actual OnPlay sends one raw damage point per ten Fragile percentage points");
        }
        Reset(); Existing(20m); Existing(29.99m); Power<BreakPower>(enemy, 1); Power<DestructionRatePower>(enemy, 1);
        await Play(Card(zeroGrant: true));
        Check(GainRequests().SequenceEqual(new[] { 4m }), "49.99 percent gives four complete ten percent intervals");
        Equal(DamageRequests.Single(), 4.999m, "damage preserves fractional effective Fragile percentages");
        Reset(); Existing(20m); Existing(29.99m); Power<BreakPower>(enemy, 1); Power<DestructionRatePower>(enemy, 1);
        await Play(Card(upgraded: true, zeroGrant: true));
        Check(GainRequests().SequenceEqual(new[] { 9m }), "49.99 percent gives nine upgraded five percent intervals");

        Reset(); Existing(50m); Power<BreakPower>(enemy, 1); Power<DestructionRatePower>(enemy, 51);
        await Play(Card(zeroGrant: true));
        Equal(DamageRequests.Single(), 5m, "50 percent Fragile requests raw five, not DR-premultiplied raw seven point five");
        Equal(MultOnly(5000m), 11250m, "shared hook applies 50 percent Fragile and current 150 percent destruction exactly once each");
        Powers(enemy).RemoveAll(p => p is FragilePower);
        Equal(MultOnly(5000m), 7500m, "50 times 100 base damage at 150 percent destruction is 7500 before other bonuses");
        Power<DestructionRateCapIncreasePower>(enemy, 100);
        typeof(DestructionRatePower).GetProperty("HbrFractionalGainData", F)!.SetValue(enemy.GetPower<DestructionRatePower>(), "0.5");
        Equal(MultOnly(5000m), 7525m, "shared hook retains fractional 150.5 percent destruction rather than integer DisplayAmount only");

        Reset(); Existing(20m); Existing(30m); Power<BreakPower>(enemy, 1); Power<DestructionRatePower>(enemy, 1);
        var scars = Power<HotaruUnfadingScarsPower>(hero, 1);
        typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(scars, 25);
        await Play(Card(zeroGrant: true));
        Check(GainRequests().SequenceEqual(new[] { 6m }), "sum of independent Fragile instances includes final Scars multiplication before thresholding");
        Equal(DamageRequests.Single(), 6.25m, "Scars enhanced total 62.5 percent drives the new damage base");
        Check(enemy.Powers.OfType<FragilePower>().All(p => p.Amount == 2), "conversion never consumes or refreshes existing Fragile durations");

        Reset(); await Play(Card());
        Check(enemy.Powers.OfType<FragilePower>().Single().Amount == 2 && DamageRequests.Count == 0 && GainRequests().Length == 0,
            "unbroken chosen target still receives Fragile without destruction or damage");
        var other = (Creature)Invoke("Actor", Blank<SmallHopper>(), null, null)!;
        Set(State<CombatState>("Combat"), "_enemies", new List<Creature> { enemy, other }); Power<BreakPower>(other, 1);
        var rune = new Harmony("hbr.full-body.rune.boundary");
        Type loader = Mod.GetType("HBRGOSakiMod.Core.Integration.HbrgoSakiHextechCompatLoader", true)!;
        rune.Patch(loader.GetMethod("HasChangingSkyAspectRune", F)!, prefix: new HarmonyMethod(typeof(FullBodyChecks).GetMethod(nameof(HasRune), F)!));
        try
        {
            Check((bool)typeof(ReptureBowPower).GetMethod("AnyEnemyHasPower", F)!.MakeGenericMethod(typeof(BreakPower))
                .Invoke(null, [hero, enemy])!, "actual bow rune query sees other enemy Break in the fixture");
            Requests.Clear(); await Play(Card());
            Check(DamageRequests.Count == 0 && GainRequests().Length == 0, "HBR card requires the actual chosen target Break despite bow rune global query");
        }
        finally { rune.UnpatchAll(rune.Id); Set(State<CombatState>("Combat"), "_enemies", new List<Creature> { enemy }); }

        Reset(bow: false); Existing(50m); Power<BreakPower>(enemy, 1); Power<DestructionRatePower>(enemy, 1);
        await Play(Card(zeroGrant: true));
        Check(GainRequests().SequenceEqual(new[] { 2.5m }), "existing off-form halving still applies to complete interval gain");
        Equal(DamageRequests.Single(), 2.5m, "existing off-form halving still applies to requested damage");

        Reset(); Invoke("Mode", false, 1); Power<BreakPower>(enemy, 1); Power<DestructionRatePower>(enemy, 101);
        await Play(Card());
        Check(GainRequests().SequenceEqual(new[] { 3m }), "disabled HBR mode retains raw Fragile stack gain");
        Check(DamageRequests.Count == 1 && DamageRequests.Single() > 0m, "disabled HBR mode retains original attack branch");
        Invoke("Mode", true, 1); Reset();
    }
    public static async Task Run()
    {
        bool ok = false; string? error = null;
        try
        {
            Invoke("Setup");
            ModelDb.Inject(typeof(DestructionRatePower));
            Set(ModelDb.Power<DestructionRatePower>(), "<Id>k__BackingField", ModelDb.GetId(typeof(DestructionRatePower)));
            var h = new Harmony("hbr.full-body.actual-card.checks");
            h.Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), prefix: new HarmonyMethod(typeof(FullBodyChecks).GetMethod(nameof(NoCombatEnd), F)!));
            h.Patch(AccessTools.Method(typeof(AttackCommand), "Execute"), prefix: new HarmonyMethod(typeof(FullBodyChecks).GetMethod(nameof(CaptureAttack), F)!));
            MethodInfo apply = typeof(PowerCmd).GetMethods(F).Single(m => m.Name == "Apply" && !m.IsGenericMethod && m.GetParameters().Length == 7 && m.GetParameters()[1].ParameterType == typeof(PowerModel));
            h.Patch(apply, prefix: new HarmonyMethod(typeof(FullBodyChecks).GetMethod(nameof(ObservePower), F)!) { priority = Priority.First });
            h.Patch(AccessTools.Method(typeof(PowerCmd), "ModifyAmount"),
                prefix: new HarmonyMethod(typeof(FullBodyChecks).GetMethod(nameof(ObserveOffset), F)!) { priority = Priority.First });
            await Cases(); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            boundary = "Actual MimicFullBodyShot OnPlay and native PowerCmd.Apply, actual Fragile independent instances/Scars and destruction caps/fraction, shared native Hook damage route. AttackCommand.Execute is a leaf capture of raw damage requests; external rune availability alone is controlled to prove actual-target Break gating. Reuses native Fragile managed fixture for StringName/logging/listener leaves. No game process, rendered UI, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " FullBody Fragile checks");
        if (!ok) throw new Exception("FullBody Fragile checks failed; inspect report.json");
    }
}
