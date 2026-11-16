using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

static class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(BeyondProtectionHbrgoSaki).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<(Creature Creature, decimal Amount)> Heals = [];
    static readonly List<decimal> Draws = [];
    static Fixture? ActiveFixture;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static FieldInfo Field(Type type, string name)
    {
        for (Type? cursor = type; cursor != null; cursor = cursor.BaseType)
            if (cursor.GetField(name, F) is { } found) return found;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object owner, string name, object? value) => Field(owner.GetType(), name).SetValue(owner, value);
    static T Blank<T>() { T value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static object? Call(object owner, string name, params object?[] args) => AccessTools.Method(owner.GetType(), name).Invoke(owner, args);
    static void Property(object owner, string name, object value) => owner.GetType().GetProperty(name, F)!.SetValue(owner, value);
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Passed.Add(label); }
    static void Equal(decimal actual, decimal expected, string label) => Check(actual == expected, $"{label}: {actual} = {expected}");
    static bool Skip() => false;
    static bool Heal(Creature creature, decimal amount, ref Task __result)
    {
        Heals.Add((creature, amount));
        // Keep the actual native health/clamping mutation; isolate command SFX,
        // VFX and post-heal hook traversal, not the amount supplied by the mod.
        creature.HealInternal(amount);
        __result = Task.CompletedTask; return false;
    }
    static bool Draw(decimal count, ref Task<IEnumerable<CardModel>> __result)
    { Draws.Add(count); __result = Task.FromResult(Enumerable.Empty<CardModel>()); return false; }
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    {
        __result = ActiveFixture == null ? Enumerable.Empty<AbstractModel>() :
            ActiveFixture.Hero.Powers.Cast<AbstractModel>().Concat(ActiveFixture.Player.Relics).ToArray();
        return false;
    }
    static HotaruDamageAmplificationPower Bank(Fixture fixture) =>
        fixture.Hero.Powers.OfType<HotaruDamageAmplificationPower>().Single();
    static Task ConvertBonus(Fixture fixture, int dp) =>
        (Task)Call(fixture.Buff, "AddConvertedDp", new ThrowingPlayerChoiceContext(), dp, fixture.Card())!;
    static void InitializeSavedPropertyCache(IEnumerable<Type> types)
    {
        Assembly native = typeof(Creature).Assembly;
        Type? legacy = native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache");
        if (legacy != null)
        {
            MethodInfo inject = AccessTools.Method(legacy, "InjectTypeIntoCache");
            foreach (Type type in types) inject.Invoke(null, new object[] { type });
            return;
        }
        // 0.113 moved saved-property registration into the native model-ID cache.
        native.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo", true)!.GetMethod("Init", F)!.Invoke(null, null);
        Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        cache.GetMethod("Init", F)!.Invoke(null, null);
        MethodInfo register = cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!;
        foreach (Type type in types) register.Invoke(null, new object[] { type });
    }
    static AbstractModel Canonical(Type type)
    {
        var content = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
        if (content.Values.FirstOrDefault(model => model.GetType() == type) is { } existing) return existing;
        var model = (AbstractModel)Activator.CreateInstance(type)!; content[model.Id] = model; return model;
    }
    static T Relic<T>(Player player) where T : RelicModel
    {
        var value = (T)((RelicModel)Canonical(typeof(T))).ToMutable(); Set(value, "_owner", player); return value;
    }
    static T Power<T>(Creature owner) where T : PowerModel
    {
        var value = (T)((PowerModel)Canonical(typeof(T))).ToMutable(); Set(value, "_owner", owner); Set(value, "_amount", 1);
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(owner)!).Add(value); return value;
    }
    sealed class Fixture
    {
        internal readonly Player Player = Blank<Player>();
        internal readonly Creature Hero = Blank<Creature>();
        internal readonly CombatState Combat = Blank<CombatState>();
        internal readonly DeflectorProtection Shield;
        internal readonly BeyondProtectionPower Buff;
        internal Fixture(int maxHp = 3500, int hp = 1000, int dp = 1000, bool enabled = true)
        {
            ActiveFixture = this;
            var run = Blank<RunState>();
            Set(Player, "<Creature>k__BackingField", Hero); Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki>()); Set(Player, "_runState", run);
            Set(run, "_players", new List<Player> { Player }); Set(Combat, "<RunState>k__BackingField", run);
            Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature>());
            Set(Hero, "<Player>k__BackingField", Player); Set(Hero, "<Side>k__BackingField", CombatSide.Player);
            Set(Hero, "_maxHp", maxHp); Set(Hero, "_currentHp", hp); Set(Hero, "_powers", new List<PowerModel>()); Hero.CombatState = Combat;
            var manual = Relic<ElectronicSoldierManual>(Player); Shield = Relic<DeflectorProtection>(Player);
            Set(Player, "_relics", new List<RelicModel> { manual, Shield });
            Property(manual, "HbrBattleProgressData", JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, HealthScaleApplied = true, Level = 1 }));
            Set(Shield, "_hasInitializedDp", true); Set(Shield, "_dp", dp);
            Buff = Power<BeyondProtectionPower>(Hero);
        }
        internal BeyondProtectionHbrgoSaki Card()
        {
            var card = (BeyondProtectionHbrgoSaki)((CardModel)Canonical(typeof(BeyondProtectionHbrgoSaki))).ToMutable();
            Set(card, "_owner", Player); return card;
        }
        internal (MomentOfReign Reign, Creature Phantom) AddPhantom()
        {
            var reign = Relic<MomentOfReign>(Player);
            ((List<RelicModel>)Field(typeof(Player), "_relics").GetValue(Player)!).Add(reign);
            var phantom = Blank<Creature>(); var monster = Blank<SmallHopper>();
            Set(phantom, "_petOwner", Player); Set(phantom, "<Side>k__BackingField", CombatSide.Player);
            Set(phantom, "<Monster>k__BackingField", monster); Set(monster, "_creature", phantom);
            Set(phantom, "_maxHp", 2000); Set(phantom, "_currentHp", 700); Set(phantom, "_powers", new List<PowerModel>()); phantom.CombatState = Combat;
            Power<PhantomPower>(phantom);
            ((List<Creature>)Field(typeof(CombatState), "_allies").GetValue(Combat)!).Add(phantom);
            return (reign, phantom);
        }
    }
    static void Install()
    {
        TestMode.IsOn = true;
        var harmony = new Harmony("hbr.beyond.protection.checks");
        void Patch(MethodBase method, string prefix = nameof(Skip)) => harmony.Patch(method,
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(prefix, F)!));
        Patch(typeof(StringName).GetConstructor([typeof(string)])!);
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor) Patch(editor);
        foreach (MethodInfo method in logger.GetMethods(F).Where(m => m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Patch(method);
        Patch(AccessTools.Method(typeof(RelicModel), "Flash"));
        Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal)), nameof(Heal));
        Patch(typeof(CardPileCmd).GetMethod(nameof(CardPileCmd.Draw), [typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)])!, nameof(Draw));
        Patch(AccessTools.Method(typeof(RunState), nameof(RunState.IterateHookListeners)), nameof(Listeners));
        Patch(AccessTools.Method(typeof(CombatState), nameof(CombatState.IterateHookListeners)), nameof(Listeners));
        // Recording history needs a full run/player history graph; keep only
        // that diagnostic leaf out of this resource/exact-bank fixture.
        Type history = typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory", true)!;
        Patch(AccessTools.Method(history, "PowerReceived"));
        InitializeSavedPropertyCache(new[] { typeof(ElectronicSoldierManual), typeof(DeflectorProtection), typeof(MomentOfReign), typeof(BeyondProtectionPower), typeof(HotaruDamageAmplificationPower), typeof(PhantomPower) });
        Canonical(typeof(HotaruDamageAmplificationPower));
        foreach (Type type in Mod.GetTypes().Where(type => type.IsDefined(typeof(HarmonyPatch), false) &&
                     (type.Name.StartsWith("ExactDamagePercent", StringComparison.Ordinal) ||
                      type.Name is "HighDimensionalShellStackAmountPatch" or "DamageAmplificationStackingPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "production unified exact-bank adapter installs " + type.Name);
        foreach (string name in new[] { "CreatureSetMaxHpInternalPatch", "MomentOfReignSetMaxHpPatch" })
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "production max-HP adapter installs " + name);
    }
    static async Task Banks()
    {
        var f = new Fixture();
        Equal(f.Buff.DamagePercent, 0, "new Beyond legacy input is empty");
        foreach (var row in new (int Dp, decimal Percent)[] { (0, 0m), (-100, 0m), (1, .005m), (2, .015m), (997, 5m), (1000, 10m) })
        {
            await ConvertBonus(f, row.Dp);
            Equal(f.Hero.Powers.OfType<HotaruDamageAmplificationPower>().SingleOrDefault()?.DamagePercent ?? 0m,
                row.Percent, "real AddConvertedDp deposits exact five-per-mille in unified bank " + row.Dp);
            Equal(f.Buff.DamagePercent, 0m, "Beyond retains no duplicate active damage percentage");
        }
        Equal(f.Buff.ConvertedDp, 0, "new conversions do not create legacy migration input");
        Equal(Bank(f).ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Move, f.Hero, null!), 1.1m,
            "the unified bank supplies the same ten-percent factor for 2000 converted DP");
        Equal(f.Buff.ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Move, f.Hero, null!), 1m,
            "Beyond's compatibility modifier adds no second damage factor");
        Equal(Bank(f).ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Unpowered, f.Hero, null!), 1m, "unpowered damage still excludes unified buff");
        Equal(Bank(f).ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Move, Blank<Creature>(), null!), 1m, "another dealer still excludes unified buff");
        await PowerCmd.Apply<HotaruDamageAmplificationPower>(new ThrowingPlayerChoiceContext(), f.Hero, 7.125m, f.Hero, null, true);
        Equal(Bank(f).DamagePercent, 17.125m, "another source joins the same exact bank without rounding");
        Check(f.Hero.Powers.OfType<HotaruDamageAmplificationPower>().Count() == 1, "converted DP and other sources use one visible amplification ability");

        var saved = new Fixture(); Property(saved.Buff, "DamagePercentData", "10"); Property(saved.Buff, "ConvertedDp", 9999);
        await saved.Buff.AfterApplied(saved.Hero, null!);
        Equal(Bank(saved).DamagePercent, 10, "old explicit percentage wins over old converted DP and migrates without a second conversion");
        await saved.Buff.BeforeCombatStart();
        Equal(Bank(saved).DamagePercent, 10, "repeated attachment and combat-start hooks cannot duplicate legacy credit");
        Check(saved.Buff.DamagePercentData == string.Empty && saved.Buff.ConvertedDp == 0,
            "successful migration clears old saved percentage and DP inputs");

        var legacy = new Fixture(); Property(legacy.Buff, "ConvertedDp", 1001);
        await ConvertBonus(legacy, 1);
        Equal(Bank(legacy).DamagePercent, 5.01m, "integer-only old bank migrates before the next exact addition");
        Equal(legacy.Buff.ConvertedDp, 0, "DP repayment stays in the relic ledger rather than a duplicate buff bank");
        var capped = new Fixture(); Property(capped.Buff, "DamagePercentData", "2147483648");
        await ConvertBonus(capped, int.MaxValue);
        Equal(Bank(capped).DamagePercent, 2147483648m, "merged exact bank saturates without overflow");
        await ConvertBonus(capped, int.MaxValue);
        Equal(Bank(capped).DamagePercent, 2147483648m, "later large additions retain the exact cap");
        await f.Buff.AfterCombatEnd(null!); Equal(f.Buff.DamagePercent, 0, "combat end clears percent");
        Equal(f.Buff.ConvertedDp, 0, "combat end clears old field"); Check(f.Buff.DamagePercentData == "", "combat end clears serialized exact bank");
    }
    static async Task Conversions()
    {
        foreach (bool enabled in new[] { true, false })
        foreach (var row in new (int Dp, int Gain)[] { (0, 0), (1, 0), (2, 1), (999, 499), (1000, 500), (1001, 500) })
        {
            var f = new Fixture(dp: row.Dp, enabled: enabled); Heals.Clear();
            int removed = await (Task<int>)Call(f.Shield, "ConvertDpToTemporaryMaxHp")!;
            string id = $"{(enabled ? "HBR" : "legacy")} DP{row.Dp}";
            Equal(removed, row.Dp, id + " reports actual removed DP"); Equal(f.Shield.Dp, 0, id + " empties DP without max-HP refill");
            Equal(f.Hero.MaxHp, 3500 + row.Gain, id + " actual maximum receives floor-half DP");
            Equal(f.Hero.CurrentHp, 1000 + row.Gain, id + " actual current HP receives same gain, not times100");
            Equal(Heals.Count, row.Gain == 0 ? 0 : 1, id + " emits only needed heal");
            if (row.Gain > 0) Equal(Heals.Single().Amount, row.Gain, id + " command amount matches actual max gain");
            Equal(f.Shield.BeyondProtectionDpToReturn, row.Dp, id + " ledger stores all removed DP including odd point");
            Equal(f.Shield.BeyondProtectionPlayerMaxHp, row.Gain, id + " ledger stores actual player maximum gain");
            await (Task)Call(f.Shield, "FinishBeyondProtection")!;
            Equal(f.Hero.MaxHp, 3500, id + " settlement revokes temporary maximum"); Equal(f.Shield.Dp, row.Dp, id + " settlement refunds original DP exactly");
            Equal(f.Hero.CurrentHp, 1000 + row.Gain, id + " settlement preserves earned HP below original cap");
            int hp = f.Hero.CurrentHp; await (Task)Call(f.Shield, "FinishBeyondProtection")!;
            Equal(f.Hero.MaxHp, 3500, id + " repeated settlement changes no maximum"); Equal(f.Shield.Dp, row.Dp, id + " repeated settlement gives no second refund");
            Equal(f.Hero.CurrentHp, hp, id + " repeated settlement changes no currentHP");
        }
        var capped = new Fixture(999999990, 10, 1000); Heals.Clear();
        await (Task<int>)Call(capped.Shield, "ConvertDpToTemporaryMaxHp")!;
        Equal(capped.Hero.MaxHp, 999999999, "native maximum cap accepts only nine HP"); Equal(capped.Hero.CurrentHp, 19, "capped growth heals actual nine rather than requested500");
        Equal(capped.Shield.BeyondProtectionPlayerMaxHp, 9, "capped ledger records only nine actualHP"); Equal(Heals.Single().Amount, 9, "capped command heal amount9");
        await (Task)Call(capped.Shield, "FinishBeyondProtection")!;
        Equal(capped.Hero.MaxHp, 999999990, "capped settlement returns original maximum"); Equal(capped.Shield.Dp, 1000, "cap does not reduce DP owed");
        var full = new Fixture(3500, 3500, 1000); await (Task<int>)Call(full.Shield, "ConvertDpToTemporaryMaxHp")!;
        Equal(full.Hero.CurrentHp, 4000, "full-health conversion fills gained maximum"); await (Task)Call(full.Shield, "FinishBeyondProtection")!;
        Equal(full.Hero.CurrentHp, 3500, "expiry clamps HP to original maximum without damage");
    }
    static async Task Phantom()
    {
        var f = new Fixture(); var (reign, phantom) = f.AddPhantom(); Heals.Clear();
        await (Task<int>)Call(f.Shield, "ConvertDpToTemporaryMaxHp")!;
        Equal(f.Hero.MaxHp, 3500, "31O redirect leaves summoner maximum unchanged"); Equal(f.Hero.CurrentHp, 1000, "31O redirect does not heal summoner");
        Equal(phantom.MaxHp, 2500, "31O actual phantom receives500 maximum"); Equal(phantom.CurrentHp, 1200, "31O actual phantom receives500 currentHP once");
        Equal(Heals.Count, 1, "31O redirect emits one heal only"); Check(Heals[0].Creature == phantom, "31O heal belongs to actual phantom"); Equal(Heals[0].Amount, 500, "31O redirected heal remains final units500");
        Equal(f.Shield.BeyondProtectionPlayerMaxHp, 0, "31O no player-max ledger"); Equal(f.Shield.BeyondProtectionPhantomMaxHp, 500, "31O redirected-max ledger500"); Equal(f.Shield.Dp, 0, "31O DP stays spent");
        await (Task)Call(f.Shield, "FinishBeyondProtection")!;
        Equal(phantom.MaxHp, 2000, "31O settlement revokes actual phantom maximum"); Equal(phantom.CurrentHp, 1200, "31O settlement leaves earned phantom HP below cap"); Equal(f.Shield.Dp, 1000, "31O refunds full original DP");
        Equal((decimal)reign.GetType().GetProperty("ExactPhantomMaxHpAdjustment", F)!.GetValue(reign)!, 0, "31O settlement clears exact global phantom adjustment");
        await (Task)Call(f.Shield, "FinishBeyondProtection")!; Equal(f.Shield.Dp, 1000, "31O duplicate end callbacks cannot double refund"); Equal(Heals.Count, 1, "31O settlement and repeated callback emit no new heal");
    }
    static async Task Card()
    {
        var f = new Fixture(); BeyondProtectionHbrgoSaki card = f.Card(); Draws.Clear();
        Check(card.CanonicalKeywords.Contains(CardKeyword.Exhaust), "real card canonical keywords includes Exhaust");
        Equal(card.DynamicVars.Cards.BaseValue, 0, "base card draw remains0");
        await (Task)Call(card, "OnPlay", new ThrowingPlayerChoiceContext(), null)!;
        Equal(Bank(f).DamagePercent, 5, "real card OnPlay converts1000DP to5percent in the unified bank"); Equal(f.Buff.DamagePercent, 0, "real card leaves no second Beyond damage factor"); Equal(f.Hero.CurrentHp, 1500, "real card OnPlay heals500 actualHP"); Equal(Draws.Count, 0, "base card OnPlay draws no cards");
        Call(card, "OnUpgrade"); Equal(card.DynamicVars.Cards.BaseValue, 2, "real OnUpgrade retains two-card draw");
        Check(card.CanonicalKeywords.Contains(CardKeyword.Exhaust), "upgrade retains Exhaust");
        await (Task)Call(card, "OnPlay", new ThrowingPlayerChoiceContext(), null)!;
        Equal(Bank(f).DamagePercent, 5, "zeroDP replay grants no extra unified percentage"); Equal(Draws.Single(), 2, "real upgraded OnPlay sends draw2");
    }
    public static async Task Run()
    {
        bool ok = false; string? error = null;
        try { Install(); await Banks(); await Conversions(); await Phantom(); await Card(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            limits = "Actual card OnPlay/OnUpgrade, native PowerCmd application and exact percentage adapters, exact save readers, relic DP conversion/end ledger, native SetMaxHp/HP setters, and production max-HP ownership/DP adapters execute. The 31O production RedirectMaxHpChange and living-phantom lookup execute. Hook listeners use the fixture's real powers/relics; only the diagnostic CombatHistory.PowerReceived leaf is skipped. Heal command SFX/VFX/hook traversal is isolated while native HealInternal writes real HP; draw command is captured. Godot StringName, native logger and relic Flash are isolated. No rendered UI, automatic card movement, full native save loading, gameplay, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} BeyondProtection checks");
        if (!ok) throw new Exception("BeyondProtection checks failed; inspect report.json");
    }
}
