using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class SeparatedDebuffChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(FragilePower).Assembly;
    static readonly List<string> Passed = [];
    static Type Fixture => typeof(Checks);
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string method, params object?[] args) => Invoke(Fixture.GetMethod(method, F)!, null, args);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string method, params object?[] args) => Invoke(Helper(helper).GetMethod(method, F)!, null, args);
    static void Reset() => FixtureCall("Reset");
    static void Mode(bool enabled) => FixtureCall("Mode", enabled, 1);
    static void Sense(string name, int value) => FixtureCall("Sense", name, value);
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static List<PowerModel> Powers(Creature target) => (List<PowerModel>)FixtureCall("Powers", target)!;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Passed.Add(message); }
    static void Equal(decimal actual, decimal expected, string message) => Check(Math.Abs(actual - expected) < .00000000000000000001m, $"{message}: {actual} = {expected}");
    static T Card<T>() where T : CardModel, new() => (T)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(typeof(T)), null)!;
    static T Power<T>(Creature owner, int amount = 1) where T : PowerModel => (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, owner, amount)!;
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static T? Apply<T>(int raw, Creature? target = null, CardModel? source = null) where T : PowerModel => PowerCmd.Apply<T>(null!, target ?? Enemy, raw, Hero, source, true).GetAwaiter().GetResult();
    static void Tick(PowerModel power) => PowerCmd.TickDownDuration(power).GetAwaiter().GetResult();
    static void Turn(PowerModel power, CombatSide side, IEnumerable<Creature> actors) => power.AfterSideTurnEnd(null!, side, actors).GetAwaiter().GetResult();
    static T[] Active<T>(Creature? target = null) where T : PowerModel => (target ?? Enemy).Powers.OfType<T>().Where(p => p.Amount > 0).ToArray();
    static decimal Percent(PowerModel power) => (decimal)Call("HbrPercentDebuffHelper", "GetEffectPercent", power)!;
    static int Raw(PowerModel power) => (int)Call("HbrPercentDebuffHelper", "GetModifierAmount", power)!;
    static decimal Multiplier(PowerModel power, Creature source, Creature target) => (decimal)Invoke(Helper("DamageModifierCompatibility").GetMethod("ModifyDamageMultiplicative", F)!, null, power, target, 1000m, ValueProp.Move, source, null, null)!;
    static decimal Damage(Creature source, Creature target) => (decimal)FixtureCall("NativeDamage", target, source, 1000m, ValueProp.Move, null, ModifyDamageHookType.Multiplicative)!;
    static void Play<T>(Creature? target = null) where T : CardModel, new() => FixtureCall("Play", Card<T>(), target);

    static void Install()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.separated-debuffs.checks");
        foreach (Type type in Mod.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false) &&
            (t.Name.StartsWith("HbrPercentDebuff", StringComparison.Ordinal) || t.Name == "CardDescriptionLocPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "installs " + type.Name);
        foreach (Type type in new[] { typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower), typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower) })
        {
            ModelDb.Inject(type);
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
#if HBR_BETA_NATIVE
        typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo", true)!.GetMethod("Init", F)!.Invoke(null, null);
        typeof(ModelIdSerializationCache).GetMethod("Init", F)!.Invoke(null, null);
        foreach (Type type in new[] { typeof(FragilePower), typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower) })
            typeof(ModelIdSerializationCache).GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]);
#else
        foreach (Type type in new[] { typeof(FragilePower), typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower) }) SavedPropertiesTypeCache.InjectTypeIntoCache(type);
#endif
        InitializeLocalization(); Reset();
    }

    static string RepositoryRoot
    {
        get
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "HBRGOSakiMod.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Fixture must remain below its checkout");
        }
    }
    static void InitializeLocalization()
    {
        var manager = Blank<LocManager>();
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        Set(manager, "<Language>k__BackingField", "zhs"); Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture); Set(manager, "<StringComparer>k__BackingField", StringComparer.Ordinal);
        var tables = new Dictionary<string, LocTable>();
        string native = Environment.GetEnvironmentVariable("HBR_NATIVE_LOCALIZATION_DIRECTORY") ?? "D:/stsmod/sts/localization/zhs";
        foreach (string name in new[] { "cards", "powers", "card_keywords" })
        {
            var text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(native, name + ".json")))!;
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs", name + ".json")))!) text[pair.Key] = pair.Value;
            tables[name] = new LocTable(name, text, null!);
        }
        Set(manager, "_tables", tables); typeof(LocManager).GetMethod("LoadLocFormatters", F)!.Invoke(manager, null);
    }
    static string Render(CardModel card)
    {
        using var context = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F).Single(m => m.Name == "Begin" && m.GetParameters().Length == 4), null, card, false, false, null)!;
        var text = card.Description; card.DynamicVars.AddTo(text);
        AccessTools.Method(card.GetType(), "AddExtraArgsToDescription").Invoke(card, [text]);
        text.Add(new IfUpgradedVar(card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));
        text.Add("OnTable", false); text.Add("InCombat", false); text.Add("IsTargeting", false); text.Add("TargetType", card.TargetType.ToString()); text.Add("GainsBlock", card.GainsBlock);
        text.Add("IsOstyAlive", false); text.Add("energyPrefix", "energy"); text.Add("singleStarIcon", "[img]star.png[/img]");
        return text.GetFormattedText();
    }

    static void NativePreservation()
    {
        foreach (bool enabled in new[] { true, false })
        {
            Reset(); Mode(enabled);
            var weak = Apply<WeakPower>(2)!; var weakAgain = Apply<WeakPower>(3)!;
            Check(ReferenceEquals(weak, weakAgain) && weak.Amount == 5 && Active<WeakPower>().Length == 1 && weak.InstanceType == PowerInstanceType.None, $"native Weak stacks durations normally HBR={enabled}");
            Equal(Multiplier(weak, Enemy, Hero), .75m, $"native Weak keeps original25 percent HBR={enabled}");
            Check(!(bool)Call("HbrPercentDebuffHelper", "Supports", weak)!, "native Weak is never a percent-snapshot power");
            string weakText = weak.Description.GetFormattedText();
            Mode(!enabled); Check(weak.Description.GetFormattedText() == weakText, "native Weak description is invariant across HBR mode"); Mode(enabled);
            Reset(); Mode(enabled);
            var vulnerable = Apply<VulnerablePower>(2)!; var again = Apply<VulnerablePower>(3)!;
            Check(ReferenceEquals(vulnerable, again) && vulnerable.Amount == 5 && vulnerable.InstanceType == PowerInstanceType.None, $"native Vulnerable stacks durations normally HBR={enabled}");
            Equal(Multiplier(vulnerable, Hero, Enemy), 1.5m, $"native Vulnerable keeps original50 percent HBR={enabled}");
            Check(!(bool)Call("HbrPercentDebuffHelper", "Supports", vulnerable)!, "native Vulnerable is never a percent-snapshot power");
            string vulnerableText = vulnerable.Description.GetFormattedText();
            Mode(!enabled); Check(vulnerable.Description.GetFormattedText() == vulnerableText, "native Vulnerable description is invariant across HBR mode");
        }
        Reset();
        var bash = Card<Bash>(); var neutralize = Card<Neutralize>();
        string bashText = Render(bash), neutralizeText = Render(neutralize);
        Check(!bashText.Contains("37.5%") && bashText.Contains("2层"), "native Bash retains original authored-layer description in HBR mode");
        Check(!neutralizeText.Contains("30%") && neutralizeText.Contains("1层"), "native Neutralize retains original authored-layer description in HBR mode");
        Sense("Talent", 99); Check(Render(bash) == bashText && Render(neutralize) == neutralizeText, "native debuff card text ignores HBR caster attributes");
        Reset(); Apply<VulnerablePower>(2, source: bash); Check(Active<VulnerablePower>().Length == 1 && Active<HbrDefenseDownPower>().Length == 0, "native Bash source remains native through PowerCmd");
        Reset(); Apply<WeakPower>(2, source: Card<SoulIntimidationHbrgoSaki>()); Check(Active<WeakPower>().Length == 1 && Active<HbrAttackDownPower>().Length == 0, "unadapted native PowerCmd is never globally remapped even for a mod card");
    }

    static void NewPowerCases<T>() where T : PowerModel
    {
        Reset(); string name = typeof(T).Name;
        var first = Apply<T>(2)!; var second = Apply<T>(3)!;
        Check(first.Id != ModelDb.Power<WeakPower>().Id && first.Id != ModelDb.Power<VulnerablePower>().Id, name + " has a separate model ID");
        Check(first.InstanceType == PowerInstanceType.Instanced && first.Amount == 2 && second.Amount == 2 && !ReferenceEquals(first, second), name + " has two independent2-turn instances");
        Equal(Percent(first), 18.75m, name + " snapshots raw2 to18.75 percent"); Equal(Percent(second), 23.4375m, name + " snapshots raw3 without display rounding");
        Tick(first); Check(first.Amount == 1 && second.Amount == 2 && Raw(first) == 2, name + " raw strength and independent remaining time differ");
        Apply<T>(1); Check(Active<T>().Length == 2 && Powers(Enemy).Contains(first), name + " ordinary weaker third grant cannot replace");
        Apply<T>(2); Check(Active<T>().Length == 2 && !Powers(Enemy).Contains(first) && Powers(Enemy).Contains(second), name + " equal strength replaces weakest shortest duration");
        var strongest = Apply<T>(6)!; Check(Active<T>().Length == 2 && Powers(Enemy).Contains(strongest), name + " higher third grant replaces only lower slot");
        Turn(strongest, CombatSide.Player, [Enemy]); Check(strongest.Amount == 2, name + " ignores opposite side turn end");
        Turn(strongest, CombatSide.Enemy, [Hero]); Check(strongest.Amount == 2, name + " ignores absent owner participant");
        Turn(strongest, CombatSide.Enemy, [Enemy]); Check(strongest.Amount == 1, name + " ticks at owner turn end");
        Equal(Percent(strongest), 45.7763671875m, name + " duration never reduces stored percent");
        Turn(strongest, CombatSide.Enemy, [Enemy]); Check(!Powers(Enemy).Contains(strongest), name + " expires exactly after2ownerturnends");
        Reset(); Sense("Talent", 100); first = Apply<T>(1)!; Equal(Percent(first), 16.25m, name + " uses INT with two-thirds weight");
        Sense("Talent", 0); Sense("Simplicity", 100); Equal(Percent(first), 16.25m, name + " snapshot is stable after caster changes");
        Reset(); Sense("Simplicity", 100); Equal(Percent(Apply<T>(1)!), 15.25m, name + " uses LUCK with one-third weight");
        Reset(); var artifact = Power<ArtifactPower>(Enemy); Apply<T>(3);
        Check(Active<T>().Length == 0 && artifact.Amount == 0, name + " actual Artifact blocks new application");
        Reset();
    }

    static void DamageCases()
    {
        Reset(); var attack = Apply<HbrAttackDownPower>(1)!;
        Equal(Multiplier(attack, Enemy, Hero), .85m, "new attack-down applies its15 percent independently");
        Apply<HbrAttackDownPower>(1); Equal(Damage(Enemy, Hero), 722m, "two attack-down snapshots multiply remaining attack damage");
        Apply<WeakPower>(2); Equal(Damage(Enemy, Hero), 541m, "original Weak75 percent coexists multiplicatively with new attack-down");
        Reset(); var defense = Apply<HbrDefenseDownPower>(1)!;
        Equal(Multiplier(defense, Hero, Enemy), 1.15m, "new defense-down applies its own15 percent");
        Apply<HbrDefenseDownPower>(1); Equal(Damage(Hero, Enemy), 1300m, "two defense-down snapshots add in shared defense bucket");
        Apply<VulnerablePower>(2); Equal(Damage(Hero, Enemy), 1950m, "original Vulnerable150 percent coexists with new defense-down");
        Reset();
    }

    static void NativeItemCases()
    {
        var player = State<Player>("Player");
        var relics = (List<RelicModel>)typeof(Player).GetField("_relics", F)!.GetValue(player)!;
        void With<T>(Action check) where T : RelicModel
        {
            var relic = Blank<T>(); Set(relic, "<IsMutable>k__BackingField", true); Set(relic, "_owner", player);
            relics.Add(relic); try { check(); } finally { relics.Remove(relic); }
        }
        Reset(); var weak = Apply<WeakPower>(1)!; var attack = Apply<HbrAttackDownPower>(1)!;
        With<PaperKrane>(() =>
        {
            Equal(Multiplier(weak, Enemy, Hero), .6m, "original PaperKrane still modifies native Weak");
            Equal(Multiplier(attack, Enemy, Hero), .85m, "original PaperKrane never modifies independent attack-down");
        });
        Power<DebilitatePower>(Enemy);
        Equal(Multiplier(weak, Enemy, Hero), .5m, "original Debilitate retains native Weak multiplier");
        Equal(Multiplier(attack, Enemy, Hero), .85m, "original Debilitate never doubles independent attack-down");
        Reset(); var vulnerable = Apply<VulnerablePower>(1)!; var defense = Apply<HbrDefenseDownPower>(1)!;
        With<PaperPhrog>(() =>
        {
            Equal(Multiplier(vulnerable, Hero, Enemy), 1.75m, "original PaperPhrog still modifies native Vulnerable");
            Equal(Multiplier(defense, Hero, Enemy), 1.15m, "original PaperPhrog never modifies independent defense-down");
        });
        Power<DebilitatePower>(Enemy);
        Equal(Multiplier(vulnerable, Hero, Enemy), 2m, "original Debilitate retains native Vulnerable multiplier");
        Equal(Multiplier(defense, Hero, Enemy), 1.15m, "original Debilitate never doubles independent defense-down");
        Check(ModelDb.Power<HbrAttackDownPower>().CustomPackedIconPath.EndsWith("hbr_attack_down_power.png") &&
            ModelDb.Power<HbrDefenseDownPower>().CustomPackedIconPath.EndsWith("hbr_defense_down_power.png"), "independent powers expose their own HBR icon paths");
        Reset();
    }

    static void SaveCases<T>() where T : PowerModel
    {
        Reset(); Sense("Talent", 75); Sense("Simplicity", 75); var first = Apply<T>(3)!; Tick(first);
        var saved = SavedProperties.From(first) ?? throw new Exception("Missing saved properties for " + typeof(T));
        Check(saved.strings?.Any(s => s.name == "HbrEffectData") == true && saved.ints?.Any(s => s.name == "HbrModifierCount" && s.value == 3) == true, typeof(T).Name + " saves intrinsic declared fields");
        string json = JsonSerializer.Serialize(saved, new JsonSerializerOptions { IncludeFields = true });
        var loaded = JsonSerializer.Deserialize<SavedProperties>(json, new JsonSerializerOptions { IncludeFields = true })!;
        var restored = Power<T>(Enemy); loaded.FillInternal(restored);
        Equal(Percent(restored), 25.78125m, typeof(T).Name + " real JSON saved-property roundtrip preserves exact snapshot");
        Check(Raw(restored) == 3 && restored.Amount == 1, typeof(T).Name + " loaded metadata never confuses duration with raw strength");
        var writer = new PacketWriter(); saved.Serialize(writer); var reader = new PacketReader(); reader.Reset(writer.Buffer); var packet = new SavedProperties(); packet.Deserialize(reader);
        var packetRestored = Power<T>(Enemy); packet.FillInternal(packetRestored);
        Equal(Percent(packetRestored), 25.78125m, typeof(T).Name + " actual packet writer/reader preserves registered fields");
        Check(reader.BitPosition == writer.BitPosition, typeof(T).Name + " packet field registry consumes full payload");
        var clone = (T)first.ClonePreservingMutability(); Equal(Percent(clone), 25.78125m, typeof(T).Name + " native mutable clone retains intrinsic snapshot");
        Call("HbrPercentDebuffHelper", "Configure", clone, 99m, 9); Equal(Percent(first), 25.78125m, typeof(T).Name + " clone fields are independent");
        Reset();
    }

    static void GhostCoefficientSources()
    {
        Reset();
        var player = State<Player>("Player"); var combat = State<CombatState>("Combat");
        var ghost = (Creature)FixtureCall("Actor", Blank<TezukaSaki>(), null, player)!;
        Set(ghost.Monster!, "<NextMove>k__BackingField", new MoveState("FOCUSED_BREAKTHROUGH_MOVE", _ => Task.CompletedTask));
        Set(combat, "_allies", new List<Creature> { Hero, ghost }); Power<PhantomPower>(ghost);
        var state = Power<GhostStatePower>(ghost);
        decimal Bonus() => (decimal)Invoke(typeof(GhostStatePower).GetMethod("GetBonusUnits", F)!, state, Enemy, ValueProp.Move)!;
        try
        {
            Apply<VulnerablePower>(99);
            Equal(Bonus(), 0m, "Ghost coefficient ignores original native Vulnerable99turns");
            var defense = Apply<HbrDefenseDownPower>(1)!;
            Equal(Bonus(), .54m, "Ghost coefficient reads separate defense-down15percent as0.54legacyunits");
            Tick(defense); Equal(Bonus(), .54m, "Ghost percent coefficient ignores remaining duration");
            var scars = Power<HotaruUnfadingScarsPower>(Hero); typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(scars, 25);
            Equal(Bonus(), .675m, "Ghost separate defense-down coefficient receives Scars once");
            Check(Active<VulnerablePower>().Single().Amount == 99 && Active<HbrDefenseDownPower>().Single().Amount == 1,
                "Ghost coefficient query never changes native or independent duration");
        }
        finally { Set(combat, "_allies", new List<Creature> { Hero }); Reset(); }
    }

    static void SeaBreezeSnapshotCases()
    {
        Reset(); var oldDefense = Apply<HbrDefenseDownPower>(1)!;
        var savior = Power<SaviorGrandEntrancePower>(Hero); typeof(SaviorGrandEntrancePower).GetProperty("UpgradeEffects", F)!.SetValue(savior, new[] { 5 });
        var boosted = Apply<HbrDefenseDownPower>(1)!; var attack = Apply<HbrAttackDownPower>(1)!; var fragile = Apply<FragilePower>(1)!;
        Equal(Percent(oldDefense), 15m, "SeaBreeze never changes an older defense-down snapshot");
        Equal(Percent(boosted), 30m, "SeaBreeze doubles new defense-down15percent basis to30percent once");
        Equal(Percent(attack), 15m, "SeaBreeze leaves attack-down15percent basis unchanged");
        Equal(Percent(fragile), 30m, "Fragile retains30percent basis after separation");
        Play<DoublePowersHbrgoSaki>();
        Equal(Percent(oldDefense), 30m, "Double transforms old defense snapshot without applying SeaBreeze again");
        Equal(Percent(boosted), 60m, "Double transforms SeaBreeze snapshot without reboosting its basis");
        Reset(); Sense("Talent", 75); Sense("Simplicity", 75);
        savior = Power<SaviorGrandEntrancePower>(Hero); typeof(SaviorGrandEntrancePower).GetProperty("UpgradeEffects", F)!.SetValue(savior, new[] { 5 });
        Equal(Percent(Apply<HbrDefenseDownPower>(1)!), 33m, "SeaBreeze basis doubling precedes actual INT/LUCK/E attribute evaluation");
        Equal(Percent(Apply<HbrAttackDownPower>(1)!), 16.5m, "same attributes apply to attack-down15percent without SeaBreeze doubling");
        Equal(Percent(Apply<FragilePower>(1)!), 33m, "same equal attributes retain Fragile30percent formula");
        var native = Apply<VulnerablePower>(3)!; Equal(Multiplier(native, Hero, Enemy), 1.5m, "SeaBreeze does not replace original Vulnerable with a percent snapshot");
        Reset();
    }

    static void CardCases()
    {
        Reset(); Play<SoulIntimidationHbrgoSaki>(Enemy);
        Check(Active<HbrAttackDownPower>().Length == 1 && Active<WeakPower>().Length == 0, "actual HBR SoulIntimidation play grants independent attack-down");
        Equal(Percent(Active<HbrAttackDownPower>().Single()), 18.75m, "actual Soul authored raw2 uses18.75 percent");
        Reset(); Mode(false); Play<SoulIntimidationHbrgoSaki>(Enemy);
        Check(Active<WeakPower>().Length == 1 && Active<HbrAttackDownPower>().Length == 0 && Active<WeakPower>()[0].Amount == 2, "mod effect adapter retains native Weak in legacy mode");
        Reset();
        var attack = Apply<HbrAttackDownPower>(2)!; Tick(attack); var defense = Apply<HbrDefenseDownPower>(3)!; var fragile = Apply<FragilePower>(1)!;
        var nativeWeak = Apply<WeakPower>(2)!; var nativeVulnerable = Apply<VulnerablePower>(3)!;
        Play<DoublePowersHbrgoSaki>();
        Equal(Percent(attack), 37.5m, "actual Double doubles new attack-down effect"); Equal(Percent(defense), 46.875m, "actual Double doubles new defense-down effect"); Equal(Percent(fragile), 60m, "actual Double keeps Fragile synchronized");
        Check(attack.Amount == 2 && defense.Amount == 4 && fragile.Amount == 4, "actual Double doubles each new remaining time");
        Check(nativeWeak.Amount == 4 && nativeVulnerable.Amount == 6 && Active<WeakPower>().Length == 1 && Active<VulnerablePower>().Length == 1, "Double doubles original native counts without converting them");
        foreach (int count in new[] { 2, 4, 8 })
        {
            Play<HotaruReplayPain>();
            Check(Active<HbrAttackDownPower>().Length == count && Active<HbrDefenseDownPower>().Length == count && Active<FragilePower>().Length == count, $"Replay reaches{count} independent instances of each new kind");
            Check(Active<HbrAttackDownPower>().All(p => Percent(p) == 37.5m) && Active<HbrDefenseDownPower>().All(p => Percent(p) == 46.875m), "Replay preserves current snapshotted effects");
            Check(Active<WeakPower>().Length == 1 && Active<VulnerablePower>().Length == 1, "Replay never makes native Weak or Vulnerable instanced");
        }
        Check(Render(Card<SoulIntimidationHbrgoSaki>()).Contains("攻击力下降"), "HBR mod card description names separate attack-down");
        Check(!Active<HbrAttackDownPower>()[0].Description.GetFormattedText().Contains("25%"), "new power description uses its snapshot");
        Reset();
    }

    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); NativePreservation(); NewPowerCases<HbrAttackDownPower>(); NewPowerCases<HbrDefenseDownPower>(); DamageCases(); NativeItemCases(); GhostCoefficientSources(); SeaBreezeSnapshotCases(); CardCases(); SaveCases<HbrAttackDownPower>(); SaveCases<HbrDefenseDownPower>(); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual native power application, original Weak/Vulnerable mechanics/descriptions coexistence, separate HBR powers, Artifact, independent turn end/instances, full damage hooks, actual Double/Replay/Soul OnPlay, intrinsic saved properties with JSON+packet roundtrips, clone and SmartFormat. Shared fixture controls engine logging/StringName and hook-listener enumeration. No game process, Godot rendering, complete save loading or multiplayer session."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " separated-debuff checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
