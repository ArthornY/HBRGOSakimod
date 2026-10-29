using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

static class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static ElectronicSoldierManual Manual = null!;
    static Player Player = null!;
    static Creature Hero = null!, Enemy = null!, Phantom = null!;
    static string FusionData = "";
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static FieldInfo Field(Type type, string name)
    {
        for (Type? cursor = type; cursor != null; cursor = cursor.BaseType)
            if (cursor.GetField(name, F) is { } field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object owner, string name, object? value) => Field(owner.GetType(), name).SetValue(owner, value);
    static T Blank<T>() { T value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Passed.Add(label); }
    static bool Skip() => false;
    static bool Energy(ref string __result) { __result = "energy"; return false; }
    static bool ReadRoster(ref string __result) { __result = FusionData; return false; }
    static void Mode(bool enabled) => typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!
        .SetValue(Manual, JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, HealthScaleApplied = true, Level = 1 }));
    static void Patch(Harmony h, MethodBase target, string prefix = nameof(Skip)) => h.Patch(target,
        prefix: new HarmonyMethod(typeof(Checks).GetMethod(prefix, F)!));
    static (bool Found, decimal Maximum) Read(PowerModel power, string helper = "HbrDamageCapHelper", string method = "TryGetMaximum")
    {
        object[] args = [power, 0m];
        bool found = (bool)Helper(helper).GetMethod(method, F)!.Invoke(null, args)!;
        return (found, (decimal)args[1]);
    }
    static T Power<T>(Creature? owner, int amount, bool mutable = true) where T : PowerModel
    {
        T power = Blank<T>(); Set(power, "<IsMutable>k__BackingField", mutable);
        Set(power, "<Id>k__BackingField", ModelDb.GetId(typeof(T)));
        Set(power, "_owner", owner); Set(power, "_amount", amount);
        Set(power, "_internalData", AccessTools.Method(typeof(T), "InitInternalData").Invoke(power, null));
        return power;
    }
    static void Setup()
    {
        TestMode.IsOn = true;
        var h = new Harmony("hbr.damage.caps.native.checks");
        Patch(h, typeof(StringName).GetConstructor([typeof(string)])!);
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor) Patch(h, editor);
        foreach (MethodInfo method in logger.GetMethods(F).Where(m => m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Patch(h, method);
        Type energy = typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!;
        foreach (MethodInfo method in energy.GetMethods(F).Where(m => m.Name == "GetPrefix")) Patch(h, method, nameof(Energy));
        // Isolate only the roster lookup; fusion classification, saved-effect parsing,
        // main-phantom ownership and exact health accounting remain production code.
        Patch(h, Helper("PhantomHelper").GetMethod("GetFusionEffectData", F)!, nameof(ReadRoster));
        foreach (string name in new[] { "PhantomHealthHardToKillCapPatch", "PhantomHealthHardenedCapPatch",
            "PhantomHealthHardenedBadgePatch", "PhantomHealthDamageCapDescriptionPatch", "HbrHardToKillBadgePatch", "HbrDamageCapDescriptionPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "production Harmony adapter installs " + name);

        string runtime = System.Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY") ??
            "D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            string path = Path.Combine(runtime, name.Name + ".dll"); return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null; };
        var manager = Blank<LocManager>(); Set(manager, "<Language>k__BackingField", "zhs");
        Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture); Set(manager, "<StringComparer>k__BackingField", StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        var text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("D:/stsmod/sts/localization/zhs/powers.json"))!;
        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Root, "HBRGOSakiMod/localization/zhs/powers.json")))!) text[pair.Key] = pair.Value;
        Set(manager, "_tables", new Dictionary<string, LocTable> { ["powers"] = new LocTable("powers", text, null!) });
        typeof(LocManager).GetMethod("LoadLocFormatters", F)!.Invoke(manager, null);

        Player = Blank<Player>(); Hero = Blank<Creature>(); Manual = Blank<ElectronicSoldierManual>();
        Set(Hero, "<Player>k__BackingField", Player); Set(Hero, "<Side>k__BackingField", CombatSide.Player);
        Set(Hero, "_powers", new List<PowerModel>()); Set(Hero, "_maxHp", 3500); Set(Hero, "_currentHp", 3500);
        Set(Player, "<Creature>k__BackingField", Hero); Set(Manual, "<IsMutable>k__BackingField", true); Set(Manual, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { Manual }); Mode(true);
        var run = Blank<RunState>(); var combat = Blank<CombatState>(); Set(run, "_players", new List<Player> { Player });
        Set(combat, "<RunState>k__BackingField", run); Set(Player, "_runState", run); Hero.CombatState = combat;
        Enemy = Creature(CombatSide.Enemy); Enemy.CombatState = combat;
        Phantom = Creature(CombatSide.Player); Phantom.PetOwner = Player; Phantom.CombatState = combat;
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Phantom)!).Add(Power<PhantomPower>(Phantom, 1));
    }
    static Creature Creature(CombatSide side)
    {
        var creature = Blank<Creature>(); Set(creature, "_powers", new List<PowerModel>());
        Set(creature, "<Side>k__BackingField", side); Set(creature, "_maxHp", 3000); Set(creature, "_currentHp", 3000);
        Set(creature, "<Monster>k__BackingField", Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>()); return creature;
    }
    static string Describe(PowerModel power, int? overrideAmount = null)
    {
        LocString text = power.Description;
        AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription").Invoke(power, [text, overrideAmount]);
        return text.GetFormattedText();
    }
    static decimal Limit(HardenedShellPower power, Creature target, decimal amount, ValueProp props = ValueProp.Move) =>
        power.ModifyHpLostBeforeOstyLate(target, amount, props, Hero, null);
    static Task Receive(HardenedShellPower power, Creature target, int amount, bool blocked = false) =>
        power.AfterDamageReceived(new ThrowingPlayerChoiceContext(), target,
            new DamageResult(target, ValueProp.Move) { UnblockedDamage = amount, WasFullyBlocked = blocked }, ValueProp.Move, Hero, null);
    static async Task MainCases()
    {
        foreach (Creature owner in new[] { Hero, Enemy, Phantom })
        {
            var hard = Power<HardToKillPower>(owner, 9);
            Check(Read(hard) == (true, 900m), "fixed HardToKill9 resolves900 for " + owner.Side + (owner.IsPet ? " pet" : ""));
            foreach (ValueProp props in new[] { ValueProp.Move, ValueProp.Unpowered, ValueProp.Unblockable, ValueProp.Move | ValueProp.Unblockable })
                Check(hard.ModifyDamageCap(owner, props, Hero, null) == 900m, "native HardToKill preserves HP-loss/attack cap900 " + props + owner.Side + owner.IsPet);
            Check(hard.ModifyDamageCap(owner == Hero ? Enemy : Hero, ValueProp.Move, Hero, null) == decimal.MaxValue,
                "native HardToKill ignores other target " + owner.Side + owner.IsPet);
            Check(hard.Amount == 9 && hard.DisplayAmount == 900, "raw HardToKill9 and actual badge900 " + owner.Side + owner.IsPet);
            Check(Describe(hard).Contains("[blue]900[/blue]") && !Describe(hard).Contains("外骨骼虫"), "dynamic generic HardToKill description900 " + owner.Side + owner.IsPet);
        }
        var shell = Power<HardenedShellPower>(Enemy, 20);
        Check(Read(shell) == (true, 2000m) && shell.Amount == 20 && shell.DisplayAmount == 2000, "fixed shell20 starts cap and badge2000; raw20 retained");
        Check(Limit(shell, Hero, 4500m) == 4500m && Limit(shell, Enemy, 0m) == 0m,
            "native shell wrong target and zero HP loss unchanged");
        await Receive(shell, Hero, 1000); await Receive(shell, Enemy, 1000, true);
        Check(shell.DisplayAmount == 2000, "other-target damage and fully blocked hit consume no shell budget");
        Check(Limit(shell, Enemy, 1000m) == 1000m, "native shell first1000 hit fully allowed");
        await Receive(shell, Enemy, 1000);
        Check(shell.DisplayAmount == 1000 && Enemy.HpDisplay == HpDisplay.Normal,
            "native shell records actual1000; no early immune state at raw20");
        Check(Limit(shell, Enemy, 5000m) == 1000m && Limit(shell, Enemy, 5000m, ValueProp.Unblockable) == 1000m,
            "native shell remaining1000 applies to attacks and HP-loss effects");
        Check(Describe(shell).Contains("[blue]2000[/blue]"), "shell description shows whole per-turn cap while badge shows remainder");
        await Receive(shell, Enemy, 1000);
        Check(shell.DisplayAmount == 0 && Enemy.HpDisplay == HpDisplay.InfiniteWithNumbers && Limit(shell, Enemy, 1m) == 0m,
            "native shell1000 plus1000 exhausts2000 and enters immune HP display");
        await shell.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [Enemy], Enemy.CombatState!);
        Check(shell.DisplayAmount == 2000 && Enemy.HpDisplay == HpDisplay.Normal && Limit(shell, Enemy, 3000m) == 2000m,
            "native shell side-start resets actual budget to2000 and restores normal display");
        var multiplayer = Power<HardenedShellPower>(Enemy, 40);
        Check(multiplayer.ShouldScaleInMultiplayer && Limit(multiplayer, Enemy, 9000m) == 4000m && multiplayer.DisplayAmount == 4000 && multiplayer.Amount == 40,
            "native multiplayer-adjusted raw40 converts once to4000");
        var clone = (HardenedShellPower)shell.MutableClone();
        Check(clone.Owner == null && clone.Amount == 20 && clone.DisplayAmount == 20,
            "native mutable clone clears owner and keeps raw20 before binding");
        Set(clone, "_owner", Enemy);
        Check(clone.Amount == 20 && clone.DisplayAmount == 2000 && Limit(clone, Enemy, 5000m) == 2000m,
            "real mutable shell clone retains raw20 with fresh native per-turn ledger");
        for (int i = 0; i < 5; i++)
            Check(Read(clone) == (true, 2000m) && clone.Amount == 20 && Describe(clone).Contains("[blue]2000[/blue]"),
                "repeated helper-description reads never double-scale raw20 " + i);
        var recreated = Power<HardenedShellPower>(Enemy, clone.Amount);
        Check(recreated.Amount == 20 && recreated.DisplayAmount == 2000, "raw amount recreation/save boundary retains one-time conversion");
    }
    static async Task LegacyCases()
    {
        Mode(false);
        var hard = Power<HardToKillPower>(Enemy, 9); var shell = Power<HardenedShellPower>(Enemy, 20);
        Check(!Read(hard).Found && hard.ModifyDamageCap(Enemy, ValueProp.Move, Hero, null) == 9 && hard.DisplayAmount == 9,
            "disabled HBR mode retains native HardToKill9");
        Check(Limit(shell, Enemy, 100m) == 20 && shell.DisplayAmount == 20 && Describe(shell).Contains("[blue]20[/blue]"),
            "disabled HBR mode retains native shell20 and native description");
        await Receive(shell, Enemy, 10); Check(shell.DisplayAmount == 10, "legacy native shell records native damage10");
        Mode(true);
        foreach (PowerModel absent in new PowerModel[] { Power<HardToKillPower>(null, 1), Power<HardenedShellPower>(null, 1),
            Power<HardToKillPower>(null, 1, false), Power<HardenedShellPower>(null, 1, false) })
            Check(!Read(absent).Found && absent.Amount == 1 && absent.DisplayAmount == 1,
                "ownerless/canonical native1 unchanged " + absent.GetType().Name + absent.IsMutable);
        Check(!Read(Power<StrengthPower>(Hero, 5)).Found && Power<StrengthPower>(Hero, 5).DisplayAmount == 5,
            "unrelated power Strength5 unchanged");
        var intangible = Power<IntangiblePower>(Hero, 3);
        Check(!Read(intangible).Found && intangible.Amount == 3 && intangible.DisplayAmount == 3,
            "Intangible three-turn counter and badge remain3 in HBR mode");
        foreach (ValueProp props in new[] { ValueProp.Move, ValueProp.Unpowered, ValueProp.Unblockable })
            Check(intangible.ModifyDamageCap(Hero, props, Enemy, null) == 1m &&
                intangible.ModifyDamageCap(Enemy, props, Hero, null) == decimal.MaxValue,
                "real native Intangible stays literal1 and preserves target identity " + props);
    }
    static async Task FusionCases()
    {
        FusionData = "67:1:0:0,49:1:0:0";
        var hard = Power<HardToKillPower>(Phantom, 900); var shell = Power<HardenedShellPower>(Phantom, 1800);
        Check(Read(hard, "PhantomHealthDamageCapHelper", "TryGetHealthBasedMaximum") == (true, 900m) &&
            Read(shell, "PhantomHealthDamageCapHelper", "TryGetHealthBasedMaximum") == (true, 1800m),
            "production fusion parser computes30percent and60percent of actual3000 HP");
        Check(!Read(hard).Found && !Read(shell).Found && hard.ModifyDamageCap(Phantom, ValueProp.Move, Hero, null) == 900m &&
            Limit(shell, Phantom, 10000m) == 1800m && hard.DisplayAmount == 900 && shell.DisplayAmount == 1800,
            "small fusion caps already HP units retain native900/1800 without times100");
        Check(Describe(hard).Contains("[blue]9[/blue]") && Describe(shell).Contains("[blue]20[/blue]"),
            "small fusion keeps native dumb description selection rather than fixed-cap override");
        foreach (PowerModel fusion in new PowerModel[] { hard, shell })
        {
            LocString smart = fusion.SmartDescription; smart.Add("OwnerName", "幻影");
            AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription").Invoke(fusion, [smart, null]);
            Check(smart.GetFormattedText().Contains("[blue]" + fusion.Amount + "[/blue]"),
                "small fusion actual native smart description displays already-resolved cap " + fusion.GetType().Name);
        }
        var large = Power<LargeHealthPower>(Phantom, 1); large.MaxHpData = "4000000000"; large.CurrentHpData = large.MaxHpData;
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Phantom)!).Add(large);
        hard = Power<HardToKillPower>(Phantom, 999999999); shell = Power<HardenedShellPower>(Phantom, 999999999);
        Check(Read(hard) == (true, 1200000000m) && Read(shell) == (true, 2400000000m),
            "large fusion uses exact health1200000000/2400000000 caps rather than native badge or times100");
        Check(hard.ModifyDamageCap(Phantom, ValueProp.Move, Hero, null) == 1200000000m && Limit(shell, Phantom, 3000000000m) == 2400000000m,
            "real native cap methods preserve decimal exact large fusion amounts");
        Check(hard.DisplayAmount == 999999999 && shell.DisplayAmount == 999999999 && Describe(hard).Contains("1200000000") && Describe(shell).Contains("2400000000"),
            "large fusion badges remain bounded while descriptions retain exact maximum");
        await Receive(shell, Phantom, 1000000000);
        Check(Limit(shell, Phantom, 3000000000m) == 1400000000m && Phantom.HpDisplay == HpDisplay.Normal,
            "large shell threshold comparison uses actual2400000000 and does not stop at bounded raw999999999");
        await Receive(shell, Phantom, 1000000000); await Receive(shell, Phantom, 400000000);
        Check(shell.DisplayAmount == 0 && Limit(shell, Phantom, 100m) == 0 && Phantom.HpDisplay == HpDisplay.InfiniteWithNumbers,
            "large shell actual accumulated2400000000 fully exhausts decimal budget");
        await shell.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Phantom], Phantom.CombatState!);
        Check(Limit(shell, Phantom, 3000000000m) == 2400000000m && Phantom.HpDisplay == HpDisplay.Normal,
            "large shell resets exact ledger for next side turn");
        FusionData = "67:2:0:0,49:2:0:0";
        Check(Read(hard) == (true, 1000000000m) && Read(shell) == (true, 2000000000m),
            "saved repeated fusion counts use native25percent/50percent health ratios without fixed scaling");
    }
    public static async Task Run()
    {
        bool ok = false; string? error = null;
        try { Setup(); await MainCases(); await LegacyCases(); await FusionCases(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PowerModel).Assembly.Location))),
            limits = "Executes real native HardToKill and HardenedShell methods, mutable cloning, native per-turn ledger, ownership checks, production cap/Harmony adapters, fusion parser and exact health helper, LocManager/LocTable and SmartFormat. Godot StringName/logging/energy leaves and read-only phantom roster lookup are isolated. No game process, rendered UI, save loading, packaging or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} native damage cap checks");
        if (!ok) throw new Exception("Damage cap checks failed; inspect report.json");
    }
}
