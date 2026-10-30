using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class PercentDebuffChecks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Mod = typeof(FragilePower).Assembly;
    static readonly Type Fixture = typeof(Checks);
    static readonly List<string> Passed = [];
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static Player Player => State<Player>("Player");
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string method, params object?[] args) => Invoke(Fixture.GetMethod(method, F)!, null, args);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string name, params object?[] args) => Invoke(Helper("HbrPercentDebuffHelper").GetMethod(name, F)!, null, args);
    static decimal Percent(PowerModel power) => (decimal)Call("GetEffectPercent", power)!;
    static int Raw(PowerModel power) => Convert.ToInt32(Call("GetModifierAmount", power));
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static void Reset() => FixtureCall("Reset");
    static void Mode(bool enabled) => FixtureCall("Mode", enabled, 1);
    static void Sense(string name, int value) => FixtureCall("Sense", name, value);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Passed.Add(name); }
    static void Equal(decimal actual, decimal expected, string name) => Check(Math.Abs(actual - expected) <= .00000000000000000001m, $"{name}: {actual} = {expected}");
    static T Power<T>(Creature owner, int amount = 1) where T : PowerModel => (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, owner, amount)!;
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static List<PowerModel> Powers(Creature owner) => (List<PowerModel>)FixtureCall("Powers", owner)!;
    static PowerModel[] Active<T>(Creature? target = null) where T : PowerModel => (target ?? Enemy).Powers.OfType<T>().Where(p => p.Amount > 0).Cast<PowerModel>().ToArray();
    static T? Apply<T>(int raw, Creature? source = null, Creature? target = null) where T : PowerModel => PowerCmd.Apply<T>(null!, target ?? Enemy, raw, source, null, true).GetAwaiter().GetResult();
    static void Tick(PowerModel power) => PowerCmd.TickDownDuration(power).GetAwaiter().GetResult();
    static void Turn(PowerModel power, CombatSide side, IEnumerable<Creature> participants) => power.AfterSideTurnEnd(null!, side, participants).GetAwaiter().GetResult();
    static decimal Damage(Creature target, Creature source, ValueProp props = ValueProp.Move) =>
        (decimal)FixtureCall("NativeDamage", target, source, 1000m, props, null, ModifyDamageHookType.Multiplicative)!;
    static decimal Multiplier(PowerModel power, Creature source, Creature target, ValueProp props = ValueProp.Move) =>
        (decimal)Invoke(Helper("DamageModifierCompatibility").GetMethod("ModifyDamageMultiplicative", F)!, null, power, target, 1000m, props, source, null, null)!;
    static T Card<T>() where T : CardModel, new() => (T)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(typeof(T)), null)!;
    static void PlayDouble() => FixtureCall("Play", Card<DoublePowersHbrgoSaki>(), null);
    static void Transform(PowerModel power) => ((Task)Invoke(Helper("HbrPercentDebuffApplication").GetMethod("DoubleExisting", F)!, null,
        null, power, Percent(power), power.Amount, Hero, null, true, true)!).GetAwaiter().GetResult();
    static HotaruUnfadingScarsPower Scars(int percent)
    {
        var scars = Power<HotaruUnfadingScarsPower>(Hero);
        typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(scars, percent);
        return scars;
    }

    static void Install()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.native-percent-debuff.fixture");
        foreach (Type type in Mod.GetTypes().Where(t => t.Namespace == "HBRGOSakiMod.Core.Patches.Combat" && t.IsDefined(typeof(HarmonyPatch), false) &&
            (t.Name.StartsWith("HbrNativePercent", StringComparison.Ordinal) || t.Name is "HotaruUnfadingScarsWeakPatch" or "HotaruUnfadingScarsVulnerablePatch" or "HotaruContestVulnerabilityQueryPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "installs production " + type.Name);
        foreach (Type powerType in new[] { typeof(WeakPower), typeof(VulnerablePower) })
        {
            ModelDb.Inject(powerType);
            PowerModel canonical = powerType == typeof(WeakPower) ? ModelDb.Power<WeakPower>() : ModelDb.Power<VulnerablePower>();
            Set(canonical, "<Id>k__BackingField", ModelDb.GetId(powerType));
        }
        Check(ModelDb.Power<WeakPower>().GetType() == typeof(WeakPower) && ModelDb.Power<VulnerablePower>().GetType() == typeof(VulnerablePower), "canonical native power identities remain unchanged");
        // Match BaseLib's normal custom-model saved-property registration;
        // this is an actual declared property carrier, never an ad-hoc net ID.
#if HBR_BETA_NATIVE
        typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo", true)!.GetMethod("Init", F)!.Invoke(null, null);
        typeof(ModelIdSerializationCache).GetMethod("Init", F)!.Invoke(null, null);
        MethodInfo? cache = typeof(ModelIdSerializationCache).GetMethod("CacheSavedPropertiesForTypeDebug", F);
        if (cache != null) cache.Invoke(null, [typeof(FragilePower)]);
#else
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(FragilePower));
#endif
        ModelDb.Inject(typeof(StrengthPower));
        Reset();
    }

    static decimal ExpectedBase(int raw)
    {
        if (raw <= 0) return 0;
        decimal result = 30m;
        for (int i = 1; i < raw; i++) result *= 1.25m;
        return result;
    }

    static void Formula<T>() where T : PowerModel
    {
        foreach (int raw in new[] { 0, 1, 2, 3, 6 })
        foreach (int intelligenceSense in new[] { 0, 50, 150 })
        foreach (int luckSense in new[] { 0, 50, 150 })
        foreach (int act in new[] { 0, 1 })
        {
            Reset(); Sense("Talent", intelligenceSense); Sense("Simplicity", luckSense); FixtureCall("Act", act);
            decimal intelligence = 100 + 2 * intelligenceSense, luck = 100 + 2 * luckSense, enemy = 150 + 60 * act;
            decimal expected = ExpectedBase(raw) * (1m + Math.Max(0m, (2m * intelligence + luck) / 3m - enemy) * .001m);
            decimal actual = (decimal)Call("ResolvePercent", ModelDb.Power<T>(), (decimal)raw, Hero, Enemy)!;
            Equal(actual, expected, $"{typeof(T).Name} raw{raw} INT{intelligence} LUCK{luck} E{enemy}");
        }
        Reset(); Sense("Talent", 100);
        Equal(Percent(Apply<T>(1, Hero)!), 32.5m, typeof(T).Name + " INT receives weight two thirds");
        Reset(); Sense("Simplicity", 100);
        Equal(Percent(Apply<T>(1, Hero)!), 30.5m, typeof(T).Name + " LUCK receives weight one third");
        Reset();
    }

    static void Instances<T>() where T : PowerModel
    {
        string name = typeof(T).Name;
        Reset(); var first = Apply<T>(2, Hero)!;
        Check(first.GetType() == typeof(T) && first.Amount == 2 && Raw(first) == 2, name + " actual generic Apply retains native type with separate raw and duration");
        Check(first.InstanceType == PowerInstanceType.Instanced, name + " mutable HBR native instance reports Instanced");
        Equal(Percent(first), 37.5m, name + " snapshots the exponential base effect");
        var second = Apply<T>(3, Hero)!;
        Check(Active<T>().Length == 2 && !ReferenceEquals(first, second), name + " makes a second independent native instance");
        Check(Enemy.HasPower<T>() && Enemy.GetPower<T>() != null, name + " native type-based consumers still recognize the debuff");
        Tick(first); Check(first.Amount == 1 && second.Amount == 2, name + " durations tick independently");
        Equal(Percent(first), 37.5m, name + " duration tick never decays the effect");
        Apply<T>(1, Hero);
        Check(Active<T>().Length == 2 && Powers(Enemy).Contains(first) && first.Amount == 1, name + " weaker third application leaves both slots and durations intact");
        var replacement = Apply<T>(2, Hero)!;
        Check(Active<T>().Length == 2 && !Powers(Enemy).Contains(first) && Powers(Enemy).Contains(second) && replacement.Amount == 2, name + " equal effect replaces weakest and resets only replacement duration");
        var stronger = Apply<T>(6, Hero)!;
        Check(!Powers(Enemy).Contains(replacement) && Powers(Enemy).Contains(second) && stronger.Amount == 2, name + " stronger third replaces the lower effect");
        Equal(Percent(stronger), 91.552734375m, name + " stores full percentage precision");
        Tick(second); Tick(second);
        Check(!Powers(Enemy).Contains(second) && Active<T>().Length == 1 && stronger.Amount == 2, name + " expired instance alone is removed");
        Turn(stronger, CombatSide.Player, [Enemy]); Check(stronger.Amount == 2, name + " wrong side cannot tick");
        Turn(stronger, CombatSide.Enemy, [Hero]); Check(stronger.Amount == 2, name + " absent owner cannot tick");
        Turn(stronger, CombatSide.Enemy, [Enemy]); Check(stronger.Amount == 1, name + " owner turn end decrements once");
        Turn(stronger, CombatSide.Enemy, [Enemy]); Check(Active<T>().Length == 0, name + " second owner turn end removes instance");
        Reset(); var a = Apply<T>(2, Hero)!; Tick(a); var b = Apply<T>(2, Hero)!; var c = Apply<T>(2, Hero)!;
        Check(!Powers(Enemy).Contains(a) && Powers(Enemy).Contains(b) && Powers(Enemy).Contains(c), name + " equal-effect tie replaces shortest remaining duration");
        Reset();
        var direct = (T)ModelDb.Power<T>().ToMutable();
        PowerCmd.Apply(null!, direct, Enemy, 3, Hero, null, true).GetAwaiter().GetResult();
        Check(Active<T>().Length == 1 && ReferenceEquals(direct, Active<T>()[0]) && direct.Amount == 2, name + " non-generic native Apply uses the same conversion");
        Equal(Percent(direct), 46.875m, name + " direct Apply snapshots raw3 once");
        Reset(); first = Apply<T>(1, Hero)!;
        PowerCmd.Apply(null!, first, Enemy, 2, Enemy, null, true).GetAwaiter().GetResult();
        var reused = Active<T>();
        Check(reused.Length == 2 && reused.Distinct(ReferenceEqualityComparer.Instance).Count() == 2 && reused.Contains(first), name + " reusing attached instance in direct Apply creates a distinct slot");
        Equal(Percent(first), 30m, name + " attached instance keeps original effect after direct reuse");
        var newInstance = reused.Single(p => !ReferenceEquals(p, first));
        Equal(Percent(newInstance), 37.5m, name + " direct reuse calculates the new incoming strength rather than mutating old snapshot");
        Check(ReferenceEquals(first.Applier, Hero) && first.Amount == 2 && newInstance.Amount == 2, name + " direct reuse preserves original applier and independent counters");
        Reset(); Apply<T>(0, Hero); Apply<T>(-1, Hero);
        Check(Active<T>().Length == 0, name + " zero and negative initial strength create no instances");
        Reset();
    }

    static void SnapshotAndArtifact<T>() where T : PowerModel
    {
        string name = typeof(T).Name;
        Reset(); Sense("Talent", 75); Sense("Simplicity", 75);
        var power = Apply<T>(3, Hero)!;
        Equal(Percent(power), 51.5625m, name + " snapshots real INT250 LUCK250 against E150");
        Sense("Talent", 0); Sense("Simplicity", 0); FixtureCall("Act", 2);
        Equal(Percent(power), 51.5625m, name + " later source stats and enemy parameter never rescale stored effect");
        Reset(); var artifact = Power<ArtifactPower>(Enemy);
        Apply<T>(6, Hero);
        Check(Active<T>().Length == 0 && artifact.Amount == 0 && !Powers(Enemy).Contains(artifact), name + " real Artifact consumes once and blocks new effect");
        Reset(); var first = Apply<T>(2, Hero)!; var second = Apply<T>(3, Hero)!; Tick(first); artifact = Power<ArtifactPower>(Enemy);
        Apply<T>(6, Hero);
        Check(Active<T>().Length == 2 && Powers(Enemy).Contains(first) && Powers(Enemy).Contains(second) && first.Amount == 1 && second.Amount == 2, name + " Artifact is checked before replacement removes an occupied slot");
        Check(artifact.Amount == 0, name + " blocked third application consumes exactly one Artifact");
        Reset(); var incoming = Apply<T>(1, Enemy, Hero)!;
        Check(!incoming.SkipNextDurationTick && incoming.Amount == 2, name + " new fixed owner-end duration has no native first-tick skip");
        Turn(incoming, CombatSide.Enemy, [Hero]); Check(incoming.Amount == 2, name + " opposing side end cannot tick player duration");
        Turn(incoming, CombatSide.Player, [Hero]); Check(incoming.Amount == 1, name + " first actual player turn end consumes one duration");
        Turn(incoming, CombatSide.Player, [Hero]); Check(Active<T>(Hero).Length == 0, name + " second actual player turn end removes instance");
        Reset(); incoming = Apply<T>(1, Enemy, Hero)!; incoming.SkipNextDurationTick = true;
        Turn(incoming, CombatSide.Player, [Hero]); Check(incoming.Amount == 1 && !incoming.SkipNextDurationTick, name + " stale native skip marker cannot add an extra owner turn");
        Reset(); var noSource = Apply<T>(2)!;
        Equal(Percent(noSource), 37.5m, name + " missing source keeps authored base");
        Reset(); var enemySource = Apply<T>(2, Enemy)!;
        Equal(Percent(enemySource), 37.5m, name + " enemy source has no invented six-dimensional attributes");
        Reset();
    }

    static void DamageAndScars()
    {
        Reset(); var weak1 = Apply<WeakPower>(1, Hero)!; var weak2 = Apply<WeakPower>(1, Hero)!;
        Equal(Multiplier(weak1, Enemy, Hero), .7m, "one 30 percent native Weak reduces powered attack to70 percent");
        Equal(Damage(Hero, Enemy), 490m, "two Weak effects multiply independently to0.49 through shared native hook");
        Equal(Multiplier(weak1, Hero, Enemy), 1m, "Weak applies only to its dealer owner");
        Equal(Multiplier(weak1, Enemy, Hero, (ValueProp)0), 1m, "Weak does not reduce unpowered damage");
        Reset(); var vulnerable1 = Apply<VulnerablePower>(1, Hero)!; Apply<VulnerablePower>(1, Hero);
        Equal(Multiplier(vulnerable1, Hero, Enemy), 1.3m, "one 30 percent native Vulnerable gives1.3 without Break");
        Equal(Damage(Enemy, Hero), 1600m, "two Vulnerable effects add to1.6 in defense-down bucket");
        Equal(Multiplier(vulnerable1, Enemy, Hero), 1m, "Vulnerable applies only to target owner");
        Equal(Multiplier(vulnerable1, Hero, Enemy, (ValueProp)0), 1m, "Vulnerable does not amplify unpowered damage");
        Reset(); weak1 = Apply<WeakPower>(1, Hero)!; weak2 = Apply<WeakPower>(1, Hero)!; Scars(25);
        Equal(Multiplier(weak1, Enemy, Hero), .625m, "Scars multiplies each final Weak effect once:30 becomes37.5");
        Equal(Damage(Hero, Enemy), 390m, "two Scars enhanced Weak effects give0.625 squared with final floor");
        Turn(weak1, CombatSide.Enemy, [Enemy]); Turn(weak2, CombatSide.Enemy, [Enemy]);
        Check(weak1.Amount == 2 && weak2.Amount == 2, "Scars preserves both independent Weak durations");
        Reset(); vulnerable1 = Apply<VulnerablePower>(1, Hero)!; var vulnerable2 = Apply<VulnerablePower>(1, Hero)!; Scars(25);
        Equal(Multiplier(vulnerable1, Hero, Enemy), 1.375m, "Scars multiplies final Vulnerable effect once");
        Equal(Damage(Enemy, Hero), 1750m, "Scars enhanced Vulnerable offsets add exactly once");
        Turn(vulnerable1, CombatSide.Enemy, [Enemy]); Turn(vulnerable2, CombatSide.Enemy, [Enemy]);
        Check(vulnerable1.Amount == 2 && vulnerable2.Amount == 2, "Scars preserves both independent Vulnerable durations");
        Reset(); Apply<VulnerablePower>(1, Hero); var fragile = Power<FragilePower>(Enemy, 2);
        typeof(FragilePower).GetMethod("ConfigureHbr", F)!.Invoke(fragile, [30m, 1]); Power<BreakPower>(Enemy);
        Equal(Damage(Enemy, Hero), 1600m, "native Vulnerable and Fragile share one additive defense-down bucket");
        Reset();
    }

    static void WithRelic<T>(Action test) where T : RelicModel
    {
        var relic = Blank<T>(); Set(relic, "<IsMutable>k__BackingField", true); Set(relic, "_owner", Player);
        var field = (FieldInfo)FixtureCall("Field", typeof(Player), "_relics")!;
        var relics = (List<RelicModel>)field.GetValue(Player)!; relics.Add(relic);
        try { test(); } finally { relics.Remove(relic); }
    }

    static void NativeModifiers()
    {
        Reset(); var weak = Apply<WeakPower>(1, Hero)!;
        WithRelic<PaperKrane>(() => Equal(Multiplier(weak, Enemy, Hero), .55m, "PaperKrane retains native extra15 percentage point reduction"));
        Reset(); var vulnerable = Apply<VulnerablePower>(1, Hero)!;
        WithRelic<PaperPhrog>(() => Equal(Multiplier(vulnerable, Hero, Enemy), 1.55m, "PaperPhrog retains native extra25 percentage points"));
        Power<CrueltyPower>(Hero, 20);
        WithRelic<PaperPhrog>(() => Equal(Multiplier(vulnerable, Hero, Enemy), 1.75m, "Cruelty and PaperPhrog preserve native modifier order"));
        Scars(25);
        WithRelic<PaperPhrog>(() => Equal(Damage(Enemy, Hero), 1937m, "native relic and Cruelty effects enter Scars once before shared final floor"));
        Reset(); weak = Apply<WeakPower>(1, Hero)!; Power<DebilitatePower>(Enemy);
        Equal(Multiplier(weak, Enemy, Hero), .4m, "native Debilitate doubles the new30 percent Weak effect");
        Scars(25); Equal(Multiplier(weak, Enemy, Hero), .25m, "Scars scales final Debilitate Weak effect once");
        Reset(); vulnerable = Apply<VulnerablePower>(1, Hero)!; Power<DebilitatePower>(Enemy);
        Equal(Multiplier(vulnerable, Hero, Enemy), 1.6m, "native Debilitate doubles the new30 percent Vulnerable effect");
        Scars(25); Equal(Multiplier(vulnerable, Hero, Enemy), 1.75m, "Scars scales final Debilitate Vulnerable effect once");
        Reset();
    }

    static void SaveCloneAndDouble<T>() where T : PowerModel
    {
        string name = typeof(T).Name;
        Reset(); Sense("Talent", 75); Sense("Simplicity", 75); var first = Apply<T>(3, Hero)!; Tick(first);
        var saved = SavedProperties.From(first) ?? throw new Exception(name + " missing native saved-property bag");
        Check(saved.strings?.Any(s => s.name == "HbrEffectData") == true && saved.ints?.Any(s => s.name == "HbrModifierCount" && s.value == 3) == true,
            name + " native save bag uses actually declared and registered exact snapshot fields");
        string json = JsonSerializer.Serialize(saved, new JsonSerializerOptions { IncludeFields = true });
        var loadedBag = JsonSerializer.Deserialize<SavedProperties>(json, new JsonSerializerOptions { IncludeFields = true })!;
        var restored = Power<T>(Enemy, 1); loadedBag.FillInternal(restored);
        Equal(Percent(restored), 51.5625m, name + " native save bag JSON roundtrip restores exact full effect");
        Check(Raw(restored) == 3 && restored.Amount == 1, name + " restoring snapshot does not reinterpret remaining duration");
        var writer = new PacketWriter(); saved.Serialize(writer);
        var reader = new PacketReader(); reader.Reset(writer.Buffer);
        var packetBag = new SavedProperties(); packetBag.Deserialize(reader);
        var packetRestored = Power<T>(Enemy, 1); packetBag.FillInternal(packetRestored);
        Equal(Percent(packetRestored), 51.5625m, name + " real packet writer/reader restores exact percentage snapshot");
        Check(Raw(packetRestored) == 3 && reader.BitPosition == writer.BitPosition, name + " native packet name registry and bit width preserve payload shape");
        var clone = (T)first.ClonePreservingMutability();
        Check(clone.GetType() == typeof(T) && clone.IsMutable && clone.Owner == null, name + " native mutable clone preserves type and clears owner");
        Equal(Percent(clone), 51.5625m, name + " cloned CWT snapshot retains effect with no owner");
        Call("Configure", clone, 5m, 99);
        Equal(Percent(first), 51.5625m, name + " changing clone never mutates original snapshot");
        Check(Raw(first) == 3 && Raw(clone) == 99, name + " clone has independent authored-strength metadata");
        Sense("Talent", 0); Sense("Simplicity", 0);
        Equal(Percent(restored), 51.5625m, name + " restored effect is independent of changed source attributes");
        Reset(); first = Apply<T>(2, Hero)!; Tick(first); var second = Apply<T>(3, Hero)!;
        PlayDouble();
        Check(Active<T>().Length == 2 && Powers(Enemy).Contains(first) && Powers(Enemy).Contains(second), name + " actual Double card transforms existing instances without new slots");
        Equal(Percent(first), 75m, name + " actual Double doubles first effect"); Equal(Percent(second), 93.75m, name + " actual Double doubles second effect");
        Check(first.Amount == 2 && second.Amount == 4 && Raw(first) == 2 && Raw(second) == 3, name + " actual Double doubles remaining durations while preserving raw strength");
        Turn(second, CombatSide.Enemy, [Enemy]); Check(second.Amount == 3, name + " doubled four-turn instance does not reclamp to two on next turn");
        Reset(); first = Apply<T>(2, Hero)!; Tick(first); var artifact = Power<ArtifactPower>(Enemy); Transform(first);
        Check(first.Amount == 1 && Percent(first) == 37.5m && artifact.Amount == 0, name + " Artifact blocks Double atomically without mutating effect or duration");
        Reset();
    }

    static void Legacy<T>() where T : PowerModel
    {
        Reset(); Mode(false); string name = typeof(T).Name;
        var first = Apply<T>(2, Hero)!; var second = Apply<T>(3, Hero)!;
        Check(ReferenceEquals(first, second) && first.Amount == 5 && Active<T>().Length == 1, name + " legacy native applications retain authored amount stacking");
        Check(first.InstanceType == PowerInstanceType.None, name + " legacy native power remains single-instance");
        Equal(Multiplier(first, typeof(T) == typeof(WeakPower) ? Enemy : Hero, typeof(T) == typeof(WeakPower) ? Hero : Enemy), typeof(T) == typeof(WeakPower) ? .75m : 1.5m, name + " legacy base multiplier is unchanged");
        PlayDouble(); Check(first.Amount == 10 && Active<T>().Length == 1, name + " legacy Double preserves native counter doubling");
        Turn(first, CombatSide.Enemy, [Enemy]); Check(first.Amount == 9, name + " legacy native end-of-enemy-side decay remains unchanged");
        Reset();
    }

    static void CounterCompatibility()
    {
        Reset(); var first = Apply<VulnerablePower>(3, Hero)!; Apply<VulnerablePower>(5, Hero); Tick(first);
        Equal((int)Invoke(Helper("HbrNativePercentDamageHelper").GetMethod("VulnerableModifiers", F, null, [typeof(Creature)], null)!, null, Enemy)!, 8m,
            "authored native Vulnerable counter sums3+5 instead of remaining turns1+2");
        FixtureCall("Play", Card<MegaCrit.Sts2.Core.Models.Cards.Dominate>(), Enemy);
        Check(Hero.GetPowerAmount<StrengthPower>() == 8, "actual Dominate with both production transpilers counts total authored8 after weaker application is rejected");
        Check(Active<VulnerablePower>().Length == 2 && first.Amount == 1, "actual Dominate cannot refresh an existing slot with its weaker grant");
        Reset();
        WithRelic<Millsage>(() =>
        {
            var relic = Player.GetRelic<Millsage>()!; Set(relic, "_contestUpgradeLevel", 6);
            var fragile = Power<FragilePower>(Enemy, 2);
            typeof(FragilePower).GetMethod("ConfigureHbr", F)!.Invoke(fragile, [46.875m, 3]);
            var view = (PowerModel)Invoke(Helper("HotaruContestVulnerabilityHelper").GetMethod("GetVulnerable", F)!, null, Enemy)!;
            Check(view is VulnerablePower && !Powers(Enemy).Contains(view), "Contest conversion remains a native typed read-only view outside actual slots");
            Equal(Percent(view), 46.875m, "Contest view carries actual Fragile snapshot rather than duration-based potency");
            Equal((int)Invoke(Helper("HbrNativePercentDamageHelper").GetMethod("VulnerableModifiers", F, null, [typeof(PowerModel)], null)!, null, view)!, 3,
                "Contest virtual view exposes authored modifier strength to Dominate counter adapter");
        });
        Reset();
    }

    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Formula<WeakPower>(); Formula<VulnerablePower>(); Instances<WeakPower>(); Instances<VulnerablePower>();
            SnapshotAndArtifact<WeakPower>(); SnapshotAndArtifact<VulnerablePower>(); DamageAndScars(); NativeModifiers();
            SaveCloneAndDouble<WeakPower>(); SaveCloneAndDouble<VulnerablePower>(); Legacy<WeakPower>(); Legacy<VulnerablePower>(); CounterCompatibility(); MechanicChangeCases(); GhostShotCases(); DescriptionCases(); GhostDescriptionCases();
            ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE") ?? "unspecified",
            boundary = "Actual native Weak/Vulnerable identities, PowerCmd Apply/ModifyAmount/TickDownDuration/remove, Artifact, shared damage hooks, Double and Replay cards, real Narara card scope, Savior and Contest interaction, SavedProperties bags with JSON and packet roundtrips, and mutable clone execute. Reuses native Fragile fixture setup; engine StringName/logging and listener enumeration are controlled leaves. No complete game/save/network/render session."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " native percent-debuff checks");
        if (!ok) throw new Exception("Native percent-debuff checks failed; inspect " + reportPath);
    }
}
