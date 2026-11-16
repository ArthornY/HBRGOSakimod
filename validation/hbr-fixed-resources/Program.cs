using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

static partial class Checks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<decimal> Damage = [], Heals = [], Maximum = [];
    static Player Player = null!;
    static Creature Hero = null!;
    static ElectronicSoldierManual Manual = null!;
    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static FieldInfo Field(Type type, string name)
    {
        for (Type? cursor = type; cursor != null; cursor = cursor.BaseType)
            if (cursor.GetField(name, F) is { } field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object owner, string name, object? value) => Field(owner.GetType(), name).SetValue(owner, value);
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Passed.Add(label);
    }
    static Type Helper => Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrNativeFixedResourceHelper", true)!;
    static decimal Read(DynamicVar variable) => (decimal)Helper.GetMethod("ReadBase", F)!.Invoke(null, [variable])!;
    static void Mode(bool enabled) => typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!
        .SetValue(Manual, JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, HealthScaleApplied = true, Level = 1 }));
    static T Card<T>() where T : CardModel
    {
        T card = Blank<T>(); Set(card, "<IsMutable>k__BackingField", true); Set(card, "_owner", Player); return card;
    }
    static T Relic<T>() where T : RelicModel
    {
        T relic = Blank<T>(); Set(relic, "<IsMutable>k__BackingField", true); Set(relic, "_owner", Player); return relic;
    }
    static bool Skip() => false;
    // Set while an event body runs only up to its HP command; its later UI leaf is not simulated.
    static bool StopAfterCommand;
    static bool CaptureDamage(decimal amount, ref Task<IEnumerable<DamageResult>> __result)
    {
        Damage.Add(amount); __result = Task.FromResult<IEnumerable<DamageResult>>([]);
        if (StopAfterCommand) throw new StopAtEventUi();
        return false;
    }
    static bool CaptureHeal(decimal amount, ref Task __result)
    {
        Heals.Add(amount); __result = Task.CompletedTask;
        if (StopAfterCommand) throw new StopAtEventUi();
        return false;
    }
    static bool CaptureGold(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool CaptureMaximum(decimal amount, ref Task __result)
    { Maximum.Add(amount); __result = Task.CompletedTask; return false; }
    static bool CaptureEnergy(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool CaptureDraw(ref Task<IEnumerable<CardModel>> __result)
    { __result = Task.FromResult<IEnumerable<CardModel>>([]); return false; }
    static void Patch(Harmony harmony, MethodBase method, string prefix) => harmony.Patch(method,
        prefix: new HarmonyMethod(typeof(Checks).GetMethod(prefix, F)!));
    static Task Effect(object model, string method, params object?[] args) =>
        (Task)AccessTools.Method(model.GetType(), method).Invoke(model, args)!;

    public static async Task Run()
    {
        var h = new Harmony("hbr.fixed.resources.native.checks");
        Patch(h, typeof(StringName).GetConstructor([typeof(string)])!, nameof(Skip));
        h.Patch(AccessTools.PropertySetter(typeof(DynamicVar), nameof(DynamicVar.BaseValue)), postfix:
            new HarmonyMethod(typeof(Checks).GetMethod(nameof(StopAfterEventStorage), F)!));
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        foreach (MethodInfo method in logger.GetMethods(F).Where(m => m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Patch(h, method, nameof(Skip));
        Player = Blank<Player>(); Hero = Blank<Creature>();
        Set(Hero, "<Player>k__BackingField", Player); Set(Hero, "_powers", new List<PowerModel>());
        Set(Hero, "_maxHp", 8000); Set(Hero, "_currentHp", 6000);
        Set(Player, "<Creature>k__BackingField", Hero);
        Manual = Relic<ElectronicSoldierManual>(); Set(Player, "_relics", new List<RelicModel> { Manual }); Mode(true);
        foreach (MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(m => m.Name == nameof(CreatureCmd.Damage) &&
            m.GetParameters().Any(p => p.Name == "amount" && p.ParameterType == typeof(decimal)))) Patch(h, method, nameof(CaptureDamage));
        Patch(h, AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal)), nameof(CaptureHeal));
        Patch(h, AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp)), nameof(CaptureMaximum));
        Patch(h, AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp)), nameof(CaptureLoseMaximum));
        Patch(h, AccessTools.Method(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy)), nameof(CaptureEnergy));
        Patch(h, AccessTools.Method(typeof(PlayerCmd), nameof(PlayerCmd.GainGold)), nameof(CaptureGold));
        Patch(h, typeof(CardPileCmd).GetMethods(F).Single(m => m.Name == nameof(CardPileCmd.Draw) &&
            m.GetParameters().Length == 4), nameof(CaptureDraw));
        foreach (MethodInfo method in typeof(SfxCmd).GetMethods(F).Where(m => m.Name == nameof(SfxCmd.Play))) Patch(h, method, nameof(Skip));
        foreach (string name in new[] { "HbrNativeFixedResourcePatch", "HbrNativeFixedResourceCommandPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "actual production Harmony patch installs " + name);
        foreach ((string name, int bodies) in new[] { ("SlipperyBridgeHpLossPatch", 2), ("ColossalFlowerHpLossPatch", 3),
            ("ColossalFlowerMinimumHpPatch", 1), ("TrialNobleGuiltyTextPatch", 1) })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Events." + name, true)!).Patch().Count == bodies,
                $"actual production Harmony patch installs {name} on {bodies} native bodies");

        CanonicalOwnerChecks();
        Feed feed = Card<Feed>(); DynamicVar hp = feed.DynamicVars.MaxHp;
        Check(hp.BaseValue == 3 && hp.IntValue == 3 && Read(hp) == 300, "Feed raw storage stays 3; selected consumption is 300");
        Check(hp.ToString() == "300" && ((IConvertible)hp).ToString(CultureInfo.InvariantCulture) == "300",
            "native string and SmartFormat conversion display scaled fixed amount");
        Check(hp.ToHighlightedString(false).Contains("300"), "native highlighted card description displays 300");
        hp.UpgradeValueBy(1);
        Check(hp.BaseValue == 4 && hp.PreviewValue == 4 && Read(hp) == 400, "native UpgradeValueBy adds one raw unit exactly once");
        DynamicVar clone = hp.Clone(); clone.UpgradeValueBy(1);
        Check(hp.BaseValue == 4 && clone.BaseValue == 5 && Read(clone) == 500 && Read(hp) == 400,
            "native Clone and ResetToBase preserve independent raw upgrade values");
        clone.ResetToBase(); clone.ResetToBase();
        Check(clone.PreviewValue == 5 && clone.EnchantedValue == 5 && Read(clone) == 500,
            "repeated ResetToBase cannot multiply resource values");
        decimal saved = clone.BaseValue;
        DynamicVar loaded = new MaxHpVar(saved); loaded.SetOwner(feed);
        Check(loaded.BaseValue == 5 && Read(loaded) == 500, "raw save/recreate round trip scales once on consumption");
        var ownerless = new MaxHpVar(3);
        Check(Read(ownerless) == 3 && ownerless.ToString() == "3", "unbound variable remains native units");
        Mode(false);
        Check(Read(hp) == 4 && hp.ToString() == "4", "disabled HBR mode retains native numeric and display values");
        Mode(true);

        LizardTail tail = Relic<LizardTail>();
        Check(Read(tail.DynamicVars.Heal) == 50 && tail.DynamicVars.Heal.ToString() == "50", "LizardTail heal percentage is excluded");
        RegalPillow pillow = Relic<RegalPillow>();
        Check(pillow.ModifyRestSiteHealAmount(Hero, 2400) == 3900,
            "RegalPillow adds fixed 1500 HP after native percentage rest calculation");
        BurningBlood blood = Relic<BurningBlood>();
        await blood.AfterCombatVictory(null!); Check(Heals[^1] == 600, "native BurningBlood fixed recovery reaches command as 600");
        Offering offering = Card<Offering>();
        await Effect(offering, "OnPlay", new ThrowingPlayerChoiceContext(), null);
        Check(Damage[^1] == 600 && offering.DynamicVars.HpLoss.BaseValue == 6,
            "native Offering loses 600 HP while stored cost stays 6");
        Burn burn = Card<Burn>();
        await Effect(burn, "OnTurnEndInHand", new ThrowingPlayerChoiceContext());
        Check(Damage[^1] == 2 && burn.DynamicVars.Damage.BaseValue == 2 && Read(burn.DynamicVars.Damage) == 2,
            $"native Burn retains raw skill units2 instead of resource scaling (actual {Damage[^1]})");
        StrikeIronclad strike = Card<StrikeIronclad>();
        Check(Read(strike.DynamicVars.Damage) == 6 && strike.DynamicVars.Damage.ToString() != "600",
            "ordinary Strike DamageVar is excluded from fixed-resource conversion");
        Mode(false); await Effect(burn, "OnTurnEndInHand", new ThrowingPlayerChoiceContext());
        Check(Damage[^1] == 2, "disabled-mode native Burn remains 2"); Mode(true);
        // Constructing all registered instance variables exposes typoed model
        // names and ensures named fixed fields exist without changing their raw data.
        int registeredVariables = 0;
        foreach (Type type in (IEnumerable<Type>)Helper.GetProperty("ModelTypes", F)!.GetValue(null)!)
        {
            var model = (AbstractModel)RuntimeHelpers.GetUninitializedObject(type);
            Set(model, "<IsMutable>k__BackingField", true);
            if (model is CardModel) Set(model, "_owner", Player);
            else if (model is RelicModel) Set(model, "_owner", Player);
            // Canonical Event variables can need initialized ModelDb assets;
            // their registration is validated by IL/manual audit, not a fake DB.
            else continue;
            DynamicVarSet variables = model switch { CardModel c => c.DynamicVars, RelicModel r => r.DynamicVars, _ => throw new Exception() };
            foreach (DynamicVar variable in variables.Values)
                if ((bool)Helper.GetMethod("IsFixedVariable", F)!.Invoke(null, [variable])!)
                { Check(Read(variable) == variable.BaseValue * 100, "registered fixed variable scales once " + type.Name + "." + variable.Name); registeredVariables++; }
        }
        Check(registeredVariables >= 40, "native fixed card/relic resource coverage excludes five migrated damage fields");
        await DescriptionChecks(h);
        await EventStorageChecks(h);
        await NativeEventChecks(h);
        await NativeModelChecks(h);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            checks = Passed.Count, passed = Passed, registeredVariables,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            gameSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CardModel).Assembly.Location))),
            boundary = "Actual native models, DynamicVar upgrades/clones/readers/formatting and production Harmony transpilers execute. Godot StringName/logging/audio and leaf damage/heal/energy/draw commands are isolated and captured. No game, Godot renderer, user saves, packaging or Steam."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS {Passed.Count} native fixed resource checks");
    }
}
