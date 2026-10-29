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
        foreach (Type type in new[] { typeof(ElectronicSoldierManual), typeof(DeflectorProtection), typeof(MomentOfReign), typeof(BeyondProtectionPower), typeof(PhantomPower) })
            SavedPropertiesTypeCache.InjectTypeIntoCache(type);
        foreach (string name in new[] { "CreatureSetMaxHpInternalPatch", "MomentOfReignSetMaxHpPatch" })
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "production max-HP adapter installs " + name);
    }
    static async Task Banks()
    {
        var f = new Fixture();
        Equal(f.Buff.DamagePercent, 0, "new percentage bank is empty");
        foreach (var row in new (int Dp, decimal Percent)[] { (0, 0m), (-100, 0m), (1, .005m), (2, .015m), (997, 5m), (1000, 10m) })
        {
            Call(f.Buff, "AddConvertedDp", row.Dp); Equal(f.Buff.DamagePercent, row.Percent, "real AddConvertedDp accumulates five per mille " + row.Dp);
        }
        Equal(f.Buff.ConvertedDp, 2000, "bank preserves original converted DP rather than reverse-scaled percent");
        Equal(f.Buff.ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Move, f.Hero, null!), 1.1m, "2000 converted DP gives actual ten-percent damage factor");
        Equal(f.Buff.ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Unpowered, f.Hero, null!), 1m, "unpowered damage still excludes buff");
        Equal(f.Buff.ModifyDamageMultiplicative(f.Hero, 6000m, ValueProp.Move, Blank<Creature>(), null!), 1m, "another dealer still excludes buff");
        string bank = f.Buff.DamagePercentData;
        var loaded = Power<BeyondProtectionPower>(f.Hero); Property(loaded, "DamagePercentData", bank);
        Equal(loaded.DamagePercent, 10, "explicit saved percent reloads without applying per-mille twice");
        Property(loaded, "ConvertedDp", 9999); Equal(loaded.DamagePercent, 10, "explicit percent bank wins over legacy DP field");
        var legacy = Power<BeyondProtectionPower>(f.Hero); Property(legacy, "ConvertedDp", 1001);
        Equal(legacy.DamagePercent, 5.005m, "legacy ConvertedDp without exact bank uses new rate");
        Call(legacy, "AddConvertedDp", 1); Equal(legacy.DamagePercent, 5.01m, "legacy bank migrates on next real addition");
        Equal(legacy.ConvertedDp, 1002, "legacy migration retains original DP count");
        Property(loaded, "DamagePercentData", "2147483648"); Call(loaded, "AddConvertedDp", int.MaxValue);
        Equal(loaded.DamagePercent, 2147483648m, "exact damage bank saturates without overflow");
        Property(loaded, "ConvertedDp", int.MaxValue); Call(loaded, "AddConvertedDp", int.MaxValue);
        Equal(loaded.ConvertedDp, int.MaxValue, "DP compatibility field saturates without overflow");
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
        Equal(f.Buff.DamagePercent, 5, "real card OnPlay converts1000DP to5percent"); Equal(f.Hero.CurrentHp, 1500, "real card OnPlay heals500 actualHP"); Equal(Draws.Count, 0, "base card OnPlay draws no cards");
        Call(card, "OnUpgrade"); Equal(card.DynamicVars.Cards.BaseValue, 2, "real OnUpgrade retains two-card draw");
        Check(card.CanonicalKeywords.Contains(CardKeyword.Exhaust), "upgrade retains Exhaust");
        await (Task)Call(card, "OnPlay", new ThrowingPlayerChoiceContext(), null)!;
        Equal(f.Buff.DamagePercent, 5, "zeroDP replay grants no extra percentage"); Equal(Draws.Single(), 2, "real upgraded OnPlay sends draw2");
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
            limits = "Actual card OnPlay/OnUpgrade, exact percentage bank/save readers, relic DP conversion/end ledger, native SetMaxHp/HP setters, and production max-HP ownership/DP adapters execute. The 31O production RedirectMaxHpChange and living-phantom lookup execute. Heal command SFX/VFX/hook traversal is isolated while native HealInternal writes real HP; draw command is captured. Godot StringName, native logger and relic Flash are isolated. No rendered UI, automatic card movement, native save loading, gameplay, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} BeyondProtection checks");
        if (!ok) throw new Exception("BeyondProtection checks failed; inspect report.json");
    }
}
