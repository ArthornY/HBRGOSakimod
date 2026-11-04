using System.Globalization;
using Expression = System.Linq.Expressions.Expression;
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
    static string Root
    {
        get
        {
            for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "HBRGOSakiMod.csproj"))) return directory.FullName;
            throw new DirectoryNotFoundException("Fixture must remain within its checkout");
        }
    }
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

        if (Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrHardenedHpLossFloorPatch") is { } floorPatch)
            Check(h.CreateClassProcessor(floorPatch).Patch().Count == 1, "production dynamic percentage HP-loss floor patch installs");
        if (Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrHardenedMaximumHpDisplayPatch") is { } displayPatch)
            Check(h.CreateClassProcessor(displayPatch).Patch().Count == 1, "production maximum-HP setter shell display patch installs");
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
    static object? Native(PowerModel power, string name, params object?[] args)
    {
        MethodInfo method = power.GetType().GetMethods(F).Where(m => m.Name == name)
            .OrderByDescending(m => m.DeclaringType == power.GetType()).ThenByDescending(m => m.GetParameters().Length).First();
        if (method.GetParameters().Length == args.Length + 1) args = [.. args, null];
        return method.Invoke(power, args);
    }
    static decimal Cap(PowerModel power, Creature target, ValueProp props, Creature? dealer = null) =>
        (decimal)Native(power, "ModifyDamageCap", target, props, dealer ?? Hero, null)!;
    static decimal Limit(HardenedShellPower power, Creature target, decimal amount, ValueProp props = ValueProp.Move) =>
        (decimal)Native(power, "ModifyHpLostBeforeOstyLate", target, amount, props, Hero, null)!;
    static Task Receive(HardenedShellPower power, Creature target, int amount, bool blocked = false)
    {
        var result = Blank<DamageResult>(); Set(result, "<Receiver>k__BackingField", target);
        Set(result, "<UnblockedDamage>k__BackingField", amount); Set(result, "<WasFullyBlocked>k__BackingField", blocked);
        return (Task)Native(power, "AfterDamageReceived", new ThrowingPlayerChoiceContext(), target, result, ValueProp.Move, Hero, null)!;
    }
    static Task TurnStart(HardenedShellPower power, Creature owner) =>
        (Task)Native(power, "BeforeSideTurnStart", new ThrowingPlayerChoiceContext(), owner.Side, new Creature[] { owner }, owner.CombatState)!;
    static (bool Found, int Percent) NativePercent(PowerModel power)
    {
        object[] args = [power, 0];
        bool found = (bool)Helper("HbrDamageCapHelper").GetMethod("TryGetNativePercent", F)!.Invoke(null, args)!;
        return (found, (int)args[1]);
    }
    static LocString Description(PowerModel power, int? amount = null)
    {
        LocString text = power.Description;
        AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription").Invoke(power, [text, amount]);
        return text;
    }
    static async Task MainCases()
    {
        foreach (Creature owner in new[] { Hero, Enemy, Phantom })
        {
            var hard = Power<HardToKillPower>(owner, 9);
            decimal maximum = owner == Hero ? 315m : 270m;
            Check(Read(hard) == (true, maximum), "native HBR HardToKill9 is nine percent of maximum HP for " + owner.Side + owner.IsPet);
            Check(NativePercent(hard) == (true, 9), "native HBR cap exposes raw9 percent without rewriting stacks");
            foreach (ValueProp props in new[] { ValueProp.Move, ValueProp.Unpowered, ValueProp.Unblockable, ValueProp.Move | ValueProp.Unblockable })
                Check(Cap(hard, owner, props) == maximum, "native HardToKill HP-loss/attack cap uses maximum HP percentage " + props + owner.Side + owner.IsPet);
            Check(Cap(hard, owner == Hero ? Enemy : Hero, ValueProp.Move) == decimal.MaxValue, "native HardToKill ignores other target");
            Check(hard.Amount == 9 && hard.DisplayAmount == maximum, "HardToKill keeps raw9 stacks and shows actual cap badge");
            LocString text = Description(hard);
            Check(Convert.ToDecimal(text.Variables["CapPercent"]) == 9m && Convert.ToDecimal(text.Variables["Amount"]) == maximum,
                "native-percent description binds raw percent and actual HP cap");
            Check(text.GetFormattedText().Contains(maximum.ToString(CultureInfo.InvariantCulture)) && !text.GetFormattedText().Contains('{'),
                "native SmartFormat renders completed health-percentage cap description");
            int originalMaximum = owner.MaxHp;
            Set(owner, "_currentHp", 1);
            Check(Read(hard).Maximum == maximum && Cap(hard, owner, ValueProp.Move) == maximum, "current HP loss never reduces maximum-HP-based cap");
            Set(owner, "_maxHp", originalMaximum + 1000);
            decimal grown = decimal.Floor((originalMaximum + 1000m) * .09m);
            Check(Read(hard).Maximum == grown && Cap(hard, owner, ValueProp.Move) == grown && hard.DisplayAmount == grown,
                "actual cap and badge update immediately when maximum HP grows");
            Set(owner, "_maxHp", originalMaximum); Set(owner, "_currentHp", originalMaximum);
        }
        var shell = Power<HardenedShellPower>(Enemy, 20);
        Check(Read(shell) == (true, 600m) && NativePercent(shell) == (true, 20) && shell.Amount == 20 && shell.DisplayAmount == 600,
            "native shell20 means20percent of maximum3000, with raw20 retained");
        Check(Limit(shell, Hero, 4500m) == 4500m && Limit(shell, Enemy, 0m) == 0m, "native shell wrong target and zero HP loss unchanged");
        await Receive(shell, Hero, 100); await Receive(shell, Enemy, 100, true);
        Check(shell.DisplayAmount == 600, "other-target damage and fully blocked hit consume no percentage shell budget");
        Check(Limit(shell, Enemy, 240m) == 240m, "native shell first240 hit fully allowed");
        await Receive(shell, Enemy, 240);
        Check(shell.DisplayAmount == 360 && Enemy.HpDisplay == HpDisplay.Normal, "native shell records actual240 HP without percentage conversion");
        Check(Limit(shell, Enemy, 5000m) == 360m && Limit(shell, Enemy, 5000m, ValueProp.Unblockable) == 360m,
            "remaining360 actual HP budget applies to attacks and HP-loss effects");
        Check(Convert.ToDecimal(Description(shell).Variables["Amount"]) == 600m, "description shows whole600 per-turn cap while badge shows360 remainder");
        Set(Enemy, "_maxHp", 4000);
        Check(Limit(shell, Enemy, 5000m) == 560m && shell.DisplayAmount == 560, "maxHP growth updates cap800 while native received240 ledger remains actual HP");
        Set(Enemy, "_maxHp", 3000);
        await Receive(shell, Enemy, 360);
        Check(shell.DisplayAmount == 0 && Enemy.HpDisplay == HpDisplay.InfiniteWithNumbers && Limit(shell, Enemy, 1m) == 0m,
            "native shell actual240 plus360 exhausts600 and enters immune HP display");
        await TurnStart(shell, Enemy);
        Check(shell.DisplayAmount == 600 && Enemy.HpDisplay == HpDisplay.Normal && Limit(shell, Enemy, 3000m) == 600m,
            "native side-start resets percentage cap600 and actual received ledger");
        var multiplayer = Power<HardenedShellPower>(Enemy, 40);
        Check(multiplayer.ShouldScaleInMultiplayer && Limit(multiplayer, Enemy, 9000m) == 1200m && multiplayer.DisplayAmount == 1200 && multiplayer.Amount == 40,
            "multiplayer-adjusted raw40 is40percent of maximum3000 rather than fixed4000");
        var clone = (HardenedShellPower)shell.MutableClone();
        Check(clone.Owner == null && clone.Amount == 20 && clone.DisplayAmount == 20, "native mutable clone clears owner and retains raw20 before binding");
        Set(clone, "_owner", Enemy);
        Check(clone.Amount == 20 && clone.DisplayAmount == 600 && Limit(clone, Enemy, 5000m) == 600m,
            "native mutable clone retains raw percentage with fresh actual-HP ledger");
        for (int index = 0; index < 5; index++)
            Check(Read(clone) == (true, 600m) && clone.Amount == 20 && Convert.ToDecimal(Description(clone).Variables["Amount"]) == 600m,
                "repeated cap and description reads never rewrite percentage stacks " + index);
        var recreated = Power<HardenedShellPower>(Enemy, clone.Amount);
        Check(recreated.Amount == 20 && recreated.DisplayAmount == 600, "raw amount recreation boundary preserves20percent interpretation");
    }
    static async Task DynamicBudgetCases()
    {
        var shell = Power<HardenedShellPower>(Enemy, 20);
        var powers = (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Enemy)!; powers.Add(shell);
        Enemy.HpDisplay = HpDisplay.Normal;
        await Receive(shell, Enemy, 100);
        Check((decimal)Helper("PhantomHealthDamageCapHelper").GetMethod("GetReceivedThisTurn", F)!.Invoke(null, [shell])! == 100m,
            "native percentage shell stores received100 in actual HP units");
        Enemy.SetMaxHpInternal(100);
        Check(Read(shell).Maximum == 20m && Limit(shell, Enemy, 1000m) == 0m && shell.DisplayAmount == 0,
            "maximum HP drop below received budget clamps remaining cap at0 instead of negative HP loss");
        Check((decimal)Helper("PhantomHealthDamageCapHelper").GetMethod("GetReceivedThisTurn", F)!.Invoke(null, [shell])! == 100m,
            "dynamic cap drop does not rewrite native received100 ledger");
        Enemy.SetMaxHpInternal(1500);
        Check(Read(shell).Maximum == 300m && Limit(shell, Enemy, 1000m) == 200m && shell.DisplayAmount == 200,
            "same-turn maximum HP increase reopens200 budget after actual received100");
        Enemy.SetMaxHpInternal(3000);
        await TurnStart(shell, Enemy); await Receive(shell, Enemy, 600);
        Check(Enemy.HpDisplay == HpDisplay.InfiniteWithNumbers && shell.DisplayAmount == 0, "native actual600 exhausts20percent cap and marks immune display");
        Enemy.SetMaxHpInternal(4000);
        Check(Limit(shell, Enemy, 1000m) == 200m && shell.DisplayAmount == 200 && Enemy.HpDisplay == HpDisplay.Normal,
            "maximum HP growth after real exhaustion restores remaining200 and normal HP display");
        powers.Remove(shell); Enemy.SetMaxHpInternal(3000); Enemy.HpDisplay = HpDisplay.Normal;
    }
    static (List<int> Badges, Action Stop) ObserveBadge(PowerModel power)
    {
        EventInfo changed = typeof(PowerModel).GetEvent("DisplayAmountChanged", F)!;
        var badges = new List<int>(); Action read = () => badges.Add(power.DisplayAmount);
        var parameters = changed.EventHandlerType!.GetMethod("Invoke")!.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
        Delegate handler = Expression.Lambda(changed.EventHandlerType, Expression.Invoke(Expression.Constant(read)), parameters).Compile();
        changed.GetAddMethod(true)!.Invoke(power, [handler]);
        return (badges, () => changed.GetRemoveMethod(true)!.Invoke(power, [handler]));
    }
    static async Task BadgeRefreshCases()
    {
        Mode(true); Enemy.SetMaxHpInternal(3000); Enemy.HpDisplay = HpDisplay.Normal;
        var powers = (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Enemy)!;
        var hard = Power<HardToKillPower>(Enemy, 9); var shell = Power<HardenedShellPower>(Enemy, 20);
        powers.Add(hard); powers.Add(shell); await Receive(shell, Enemy, 100);
        var hardWatch = ObserveBadge(hard); var shellWatch = ObserveBadge(shell);
        try
        {
            Enemy.SetMaxHpInternal(4000);
            Check(hardWatch.Badges.SequenceEqual(new[] { 360 }) && shellWatch.Badges.SequenceEqual(new[] { 700 }),
                "native maxHP setter notifies each cap badge exactly once and event callbacks read updated360 and700");
            Check(hard.Amount == 9 && shell.Amount == 20 && (decimal)Helper("PhantomHealthDamageCapHelper").GetMethod("GetReceivedThisTurn", F)!.Invoke(null, [shell])! == 100m,
                "badge notifications never rewrite raw percentages or native received100 ledger");
            HpDisplay special = Enum.GetValues<HpDisplay>().First(value => value != HpDisplay.Normal && value != HpDisplay.InfiniteWithNumbers);
            Enemy.HpDisplay = special; hardWatch.Badges.Clear(); shellWatch.Badges.Clear();
            Enemy.SetMaxHpInternal(4500);
            Check(Enemy.HpDisplay == special && hardWatch.Badges.SequenceEqual(new[] { 405 }) && shellWatch.Badges.SequenceEqual(new[] { 800 }),
                "special HP display stays unchanged while both actual badges still receive updated405 and800 notifications");
            Mode(false); hardWatch.Badges.Clear(); shellWatch.Badges.Clear();
            Enemy.SetMaxHpInternal(3000);
            Check(hardWatch.Badges.Count == 0 && shellWatch.Badges.Count == 0 && hard.Amount == 9 && shell.Amount == 20,
                "disabled HBR native caps receive no extra maxHP badge notifications or counter writes");
            Mode(true); powers.Remove(hard); powers.Remove(shell); hardWatch.Badges.Clear(); shellWatch.Badges.Clear();
            Enemy.SetMaxHpInternal(3500);
            Check(hardWatch.Badges.Count == 0 && shellWatch.Badges.Count == 0,
                "maxHP change with no attached managed cap notifies neither detached power");
        }
        finally
        {
            hardWatch.Stop(); shellWatch.Stop(); powers.Remove(hard); powers.Remove(shell);
            Mode(true); Enemy.SetMaxHpInternal(3000); Enemy.HpDisplay = HpDisplay.Normal;
        }
    }
    static void NativePercentEdgeCases()
    {
        int savedMaximum = Enemy.MaxHp, savedCurrent = Enemy.CurrentHp;
        Set(Enemy, "_maxHp", 1001); Set(Enemy, "_currentHp", 7);
        Check(Read(Power<HardToKillPower>(Enemy, 9)) == (true, 90m) && Read(Power<HardenedShellPower>(Enemy, 20)) == (true, 200m),
            "positive fractional maximum-HP percentages floor90point09 and200point2 at final cap");
        Set(Enemy, "_maxHp", 1); Set(Enemy, "_currentHp", 1);
        Check(Read(Power<HardToKillPower>(Enemy, 9)) == (true, 1m) && Read(Power<HardenedShellPower>(Enemy, 20)) == (true, 1m),
            "positive percentage cap has minimum1 at tiny maximum HP");
        foreach (int amount in new[] { 0, -3 })
            Check(Read(Power<HardToKillPower>(Enemy, amount)) == (true, 0m) && Read(Power<HardenedShellPower>(Enemy, amount)) == (true, 0m),
                "zero or negative native percent has cap0 rather than minimum1: " + amount);
        Set(Enemy, "_maxHp", savedMaximum); Set(Enemy, "_currentHp", savedCurrent);
        var large = Power<LargeHealthPower>(Enemy, 1); large.MaxHpData = "4000000000"; large.CurrentHpData = "1";
        var powers = (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Enemy)!; powers.Add(large);
        var hard = Power<HardToKillPower>(Enemy, 9); var shell = Power<HardenedShellPower>(Enemy, 20);
        Check(Read(hard) == (true, 360000000m) && Read(shell) == (true, 800000000m), "native9percent and20percent use exact maximum4e9 despite current HP1");
        Check(Cap(hard, Enemy, ValueProp.Move) == 360000000m && Limit(shell, Enemy, 2000000000m) == 800000000m,
            "real native cap methods preserve exact large ordinary percentage amounts");
        var bounded = Power<HardenedShellPower>(Enemy, 40);
        Check(Read(bounded) == (true, 1600000000m) && bounded.DisplayAmount == 999999999 && bounded.Amount == 40,
            "large percentage cap remains decimal exact while badge stays bounded and raw percentage remains40");
        Check(Convert.ToDecimal(Description(bounded).Variables["Amount"]) == 1600000000m && Convert.ToDecimal(Description(bounded).Variables["CapPercent"]) == 40m,
            "large percentage description retains exact amount and percentage beyond bounded badge");
        powers.Remove(large);
    }
    static void PercentDescriptionOverrideCases()
    {
        foreach (PowerModel power in new PowerModel[] { Power<HardToKillPower>(Hero, 9), Power<HardenedShellPower>(Hero, 20) })
        foreach (int amount in new[] { 7, 0, -3 })
        {
            int raw = power.Amount; decimal actual = Read(power).Maximum;
            LocString text = Description(power, amount);
            decimal expected = amount <= 0 ? 0m : 245m;
            Check(Convert.ToDecimal(text.Variables["CapPercent"]) == amount && Convert.ToDecimal(text.Variables["Amount"]) == expected,
                "native percent description override previews raw percent and calculated cap: " + power.GetType().Name + amount);
            Check(power.Amount == raw && Read(power).Maximum == actual, "description preview never mutates raw percent or real cap");
        }
        foreach (PowerModel power in new PowerModel[] { Power<HardToKillPower>(null, 1), Power<HardenedShellPower>(null, 1),
            Power<HardToKillPower>(null, 1, false), Power<HardenedShellPower>(null, 1, false) })
        {
            LocString text = Description(power, 7);
            Check(Convert.ToDecimal(text.Variables["Amount"]) == 7m && !text.Variables.ContainsKey("CapPercent"),
                "ownerless and canonical description override retains native7 without percent variables");
        }
    }
    static async Task LegacyCases()
    {
        Mode(false);
        var hard = Power<HardToKillPower>(Enemy, 9); var shell = Power<HardenedShellPower>(Enemy, 20);
        Check(!Read(hard).Found && Cap(hard, Enemy, ValueProp.Move) == 9 && hard.DisplayAmount == 9,
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
            Check(Cap(intangible, Hero, props, Enemy) == 1m &&
                Cap(intangible, Enemy, props, Hero) == decimal.MaxValue,
                "real native Intangible stays literal1 and preserves target identity " + props);
    }
    static async Task FusionCases()
    {
        FusionData = "67:1:0:0,49:1:0:0";
        var hard = Power<HardToKillPower>(Phantom, 900); var shell = Power<HardenedShellPower>(Phantom, 1800);
        Check(!NativePercent(hard).Found && !NativePercent(shell).Found, "small fusion HP-unit caps never reinterpret native Amount as percentage");
        Check(Read(hard, "PhantomHealthDamageCapHelper", "TryGetHealthBasedMaximum") == (true, 900m) &&
            Read(shell, "PhantomHealthDamageCapHelper", "TryGetHealthBasedMaximum") == (true, 1800m),
            "production fusion parser computes30percent and60percent of actual3000 HP");
        Check(!Read(hard).Found && !Read(shell).Found && Cap(hard, Phantom, ValueProp.Move) == 900m &&
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
        foreach (PowerModel fusion in new PowerModel[] { hard, shell })
        {
            Check(!NativePercent(fusion).Found, "large fusion origin stays ahead of native-percent interpretation");
            int raw = fusion.Amount; decimal actual = Read(fusion).Maximum;
            LocString preview = Description(fusion, 7);
            Check(Convert.ToDecimal(preview.Variables["Amount"]) == 7m && !preview.Variables.ContainsKey("CapPercent") && fusion.Amount == raw && Read(fusion).Maximum == actual,
                "large fusion description override retains native7 without changing actual exact HP cap");
        }
        Check(Read(hard) == (true, 1200000000m) && Read(shell) == (true, 2400000000m),
            "large fusion uses exact health1200000000/2400000000 caps rather than native badge or times100");
        Check(Cap(hard, Phantom, ValueProp.Move) == 1200000000m && Limit(shell, Phantom, 3000000000m) == 2400000000m,
            "real native cap methods preserve decimal exact large fusion amounts");
        Check(hard.DisplayAmount == 999999999 && shell.DisplayAmount == 999999999 && Describe(hard).Contains("1200000000") && Describe(shell).Contains("2400000000"),
            "large fusion badges remain bounded while descriptions retain exact maximum");
        await Receive(shell, Phantom, 1000000000);
        Check(Limit(shell, Phantom, 3000000000m) == 1400000000m && Phantom.HpDisplay == HpDisplay.Normal,
            "large shell threshold comparison uses actual2400000000 and does not stop at bounded raw999999999");
        await Receive(shell, Phantom, 1000000000); await Receive(shell, Phantom, 400000000);
        Check(shell.DisplayAmount == 0 && Limit(shell, Phantom, 100m) == 0 && Phantom.HpDisplay == HpDisplay.InfiniteWithNumbers,
            "large shell actual accumulated2400000000 fully exhausts decimal budget");
        await TurnStart(shell, Phantom);
        Check(Limit(shell, Phantom, 3000000000m) == 2400000000m && Phantom.HpDisplay == HpDisplay.Normal,
            "large shell resets exact ledger for next side turn");
        FusionData = "67:2:0:0,49:2:0:0";
        Check(Read(hard) == (true, 1000000000m) && Read(shell) == (true, 2000000000m),
            "saved repeated fusion counts use native25percent/50percent health ratios without fixed scaling");
    }
    public static async Task Run()
    {
        bool ok = false; string? error = null;
        try { Setup(); await MainCases(); await DynamicBudgetCases(); await BadgeRefreshCases(); NativePercentEdgeCases(); PercentDescriptionOverrideCases(); await LegacyCases(); await FusionCases(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(System.Environment.GetEnvironmentVariable("HBR_DAMAGE_CAP_REPORT_PATH") ?? Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PowerModel).Assembly.Location))),
            sourceNativeSha256 = System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), runtimeProfile = System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"), telemetryIsolation = System.Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            limits = "Executes real native HardToKill and HardenedShell methods, mutable cloning, native per-turn ledger, ownership checks, production cap/Harmony adapters, fusion parser and exact health helper, LocManager/LocTable and SmartFormat. Godot StringName/logging/energy leaves and read-only phantom roster lookup are isolated. No game process, rendered UI, save loading, packaging or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} native damage cap checks");
        if (!ok) throw new Exception("Damage cap checks failed; inspect report.json");
    }
}
