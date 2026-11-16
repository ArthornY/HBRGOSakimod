using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

static class HotaruAncientDpChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Assembly Mod = typeof(DeflectorProtection).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> Rows = [];
    static readonly Type FixtureType = typeof(Checks).GetNestedType("Fixture", F)!;
    static readonly MethodInfo BeginEvent = typeof(EventModel).GetMethod(nameof(EventModel.BeginEvent), F)!;
    static readonly MethodInfo BeforeStarted = AccessTools.Method(typeof(AncientEventModel), "BeforeEventStarted");
    static Task Original = Task.CompletedTask;
    static int OriginalCalls;
    static T Blank<T>() { var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    { try { return method.Invoke(owner, args); } catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; } }
    static void Set(object owner, string field, object? value) => Invoke(typeof(Checks).GetMethod("Set", F)!, null, owner, field, value);
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, F)!.GetValue(owner)!;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Passed.Add(name); }
    static bool EntryLeaf(ref Task __result) { OriginalCalls++; __result = Original; return false; }
    static object World(Type character, int hp = 1)
    {
        var model = (CharacterModel)RuntimeHelpers.GetUninitializedObject(character); GC.SuppressFinalize(model);
        return FixtureType.GetConstructors(F).Single().Invoke([model, true, hp, character == typeof(HbrgoHotaru)]);
    }
    static DeflectorProtection Shield(object fixture, int dp)
    {
        var shield = (DeflectorProtection)Invoke(FixtureType.GetMethod("Shield", F)!, fixture)!;
        Set(shield, "_dp", dp); return shield;
    }
    static void Power<T>(object fixture, int amount) where T : PowerModel
    {
        Creature hero = Field<Creature>(fixture, "Hero"); var power = Blank<T>();
        Set(power, "<IsMutable>k__BackingField", true); Set(power, "<Id>k__BackingField", ModelDb.GetId(typeof(T)));
        Set(power, "_owner", hero); Set(power, "_amount", amount); Field<List<PowerModel>>(hero, "_powers").Add(power);
    }
    static Neow Ancient(Player? owner)
    {
        var ancient = Blank<Neow>(); Set(ancient, "<IsMutable>k__BackingField", true);
        PropertyInfo property = typeof(EventModel).GetProperty("Owner", F)!;
        if (property.SetMethod != null) property.SetValue(ancient, owner); else Set(ancient, "_owner", owner);
        return ancient;
    }
    static Task Entry(EventModel model, Player? owner, bool preFinished = false, Task? original = null)
    {
        Original = original ?? Task.CompletedTask;
        object?[] args = BeginEvent.GetParameters().Select(p => p.ParameterType == typeof(Player) ? owner :
            p.ParameterType == typeof(bool) ? (object)preFinished : null).ToArray();
        return (Task)Invoke(BeginEvent, model, args)!;
    }
    static void Ascension(object fixture, int level)
    {
        // Set native run state; the decimal AscensionHelper and its selection
        // of 1 / .8 remain native. Actual backing setup is shared by profiles.
        object run = Field<object>(fixture, "Run");
        Set(run, "<AscensionLevel>k__BackingField", level);
        Set(RunManager.Instance, "<State>k__BackingField", run);
        Type managerType = typeof(AscensionHelper).Assembly.GetTypes().Single(t => t.Name == "AscensionManager");
        ConstructorInfo? numeric = managerType.GetConstructors(F).SingleOrDefault(c =>
            c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(int));
        object manager;
        if (numeric != null) manager = numeric.Invoke([level]);
        else
        {
            ConstructorInfo constructor = managerType.GetConstructors(F).Single(c =>
                c.GetParameters().Length == 2 && c.GetParameters()[1].ParameterType == typeof(int));
            object standard = Enum.Parse(constructor.GetParameters()[0].ParameterType, "Standard");
            Set(run, "<GameMode>k__BackingField", standard); manager = constructor.Invoke([standard, level]);
        }
        Set(RunManager.Instance, "<AscensionManager>k__BackingField", manager);
    }
    static void Setup()
    {
        Invoke(typeof(Checks).GetMethod("Install", F)!, null);
        var harmony = new Harmony("hbr.hotaru-ancient-dp");
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.AncientEventDeflectorProtectionPatch", true)!;
        Check(harmony.CreateClassProcessor(patch).Patch().Count == 1, "production DP postfix installs on actual native BeginEvent");
        harmony.Patch(BeginEvent, prefix: new HarmonyMethod(typeof(HotaruAncientDpChecks).GetMethod(nameof(EntryLeaf), F)!));
        Check(Harmony.GetPatchInfo(BeginEvent)!.Postfixes.Any(p => p.PatchMethod.DeclaringType == patch),
            "native BeginEvent retains real production DP restoration postfix");
        Type guard = Mod.GetType("HBRGOSakiMod.Core.Patches.HotaruAncientHealthRecoveryPatch", true)!;
        Check(Harmony.GetPatchInfo(BeforeStarted)!.Prefixes.Any(p => p.PatchMethod.DeclaringType == guard),
            "native Ancient BeforeEventStarted retains real HP skip prefix");
    }
    static async Task Matrix()
    {
        foreach (int level in new[] { 0, (int)AscensionLevel.WearyTraveler })
        foreach (int hp in new[] { 3000, 1, 73 })
        foreach (int dp in new[] { 0, 1234, 6000 })
        {
            object fixture = World(typeof(HbrgoHotaru), hp); Ascension(fixture, level);
            var player = Field<Player>(fixture, "Player"); var hero = Field<Creature>(fixture, "Hero");
            var shield = Shield(fixture, dp); var ancient = Ancient(player);
            decimal percent = level == 0 ? 1m : .8m;
            Check(AscensionHelper.GetValueIfAscension(AscensionLevel.WearyTraveler, .8m, 1m) == percent,
                "native ascension selector resolves " + percent + " for level " + level);
            int expected = dp + (int)decimal.Floor((6000 - dp) * percent);
            await Entry(ancient, player);
            Check(shield.Dp == expected, $"Hotaru HP{hp} DP{dp} asc{level} restores own missing DP to {expected} (actual {shield.Dp})");
            Check(hero.CurrentHp == hp && hero.MaxHp == 3000 && shield.Dp <= shield.MaxDp,
                $"Hotaru HP{hp} DP{dp} asc{level} preserves HP and maximum DP ceiling");
            Rows.Add(new { hp, dp, ascension = level, maximumDp = shield.MaxDp, actual = shield.Dp, expected });
        }
    }
    static async Task Controls()
    {
        object fixture = World(typeof(HbrgoHotaru)); Ascension(fixture, 0);
        var player = Field<Player>(fixture, "Player"); var shield = Shield(fixture, 0); var ancient = Ancient(player);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task entry = Entry(ancient, player, original: completion.Task);
        Check(!entry.IsCompleted && shield.Dp == 0, "DP restoration waits until original BeginEvent task completes");
        completion.SetResult(); await entry;
        Check(shield.Dp == shield.MaxDp, "awaited completion triggers actual Deflector missing-DP restoration");
        Set(shield, "_dp", 1234); var fault = new InvalidOperationException("controlled-event-failure");
        try { await Entry(ancient, player, original: Task.FromException(fault)); throw new Exception("fault unexpectedly swallowed"); }
        catch (InvalidOperationException error) { Check(ReferenceEquals(error, fault) && shield.Dp == 1234, "faulted original propagates unchanged and restores no DP"); }
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try { await Entry(ancient, player, original: Task.FromCanceled(cancellation.Token)); throw new Exception("cancellation unexpectedly swallowed"); }
            catch (OperationCanceledException) { Check(shield.Dp == 1234, "canceled original restores no DP"); }
        }
        var original = new TaskCompletionSource().Task;
        Task preFinished = Entry(ancient, player, true, original);
        Check(ReferenceEquals(preFinished, original) && shield.Dp == 1234, "pre-finished ancient keeps original task and restores no DP");
        Type ordinaryType = typeof(EventModel).Assembly.GetTypes().First(t => !t.IsAbstract && typeof(EventModel).IsAssignableFrom(t) && !typeof(AncientEventModel).IsAssignableFrom(t));
        var ordinary = (EventModel)RuntimeHelpers.GetUninitializedObject(ordinaryType); GC.SuppressFinalize(ordinary);
        Task nonAncient = Entry(ordinary, player, original: original);
        Check(ReferenceEquals(nonAncient, original) && shield.Dp == 1234, "non-Ancient entry keeps original task and restores no DP");
        await Entry(Ancient(null), null);
        Check(shield.Dp == 1234, "ancient with null owner safely restores nothing");
        Set(player, "_relics", new List<RelicModel>()); await Entry(ancient, player);
        Check(shield.Dp == 1234, "ancient owner without Deflector safely restores nothing");
        fixture = World(typeof(HbrgoHotaru)); Ascension(fixture, 0); player = Field<Player>(fixture, "Player"); shield = Shield(fixture, 0);
        shield.LoseMaxDp(int.MaxValue); await Entry(Ancient(player), player);
        Check(shield.MaxDp == 0 && shield.Dp == 0, "zero maximum DP remains zero without invented restoration");
        fixture = World(typeof(HbrgoHotaru)); Ascension(fixture, (int)AscensionLevel.WearyTraveler); player = Field<Player>(fixture, "Player"); shield = Shield(fixture, 5999);
        await Entry(Ancient(player), player);
        Check(shield.Dp == 5999, "80percent of one missing DP floors to zero rather than rounding up");
        for (int i = 0; i < 5; i++) await Entry(Ancient(player), player);
        Check(shield.Dp == 5999 && shield.Dp <= shield.MaxDp, "repeat ancient recovery respects fractional floor and maximum ceiling");
        foreach (Type type in new[] { typeof(HbrgoSaki31O), typeof(HbrgoSaki30A) })
        foreach (int level in new[] { 0, (int)AscensionLevel.WearyTraveler })
        {
            fixture = World(type, 73); Ascension(fixture, level); player = Field<Player>(fixture, "Player"); shield = Shield(fixture, 123);
            int expected = 123 + (int)decimal.Floor((shield.MaxDp - 123) * (level == 0 ? 1m : .8m));
            await Entry(Ancient(player), player);
            Check(shield.Dp == expected, type.Name + " asc" + level + " retains original missing-DP recovery");
        }
    }
    static async Task HpGuard()
    {
        foreach (int hp in new[] { 3000, 1, 73 })
        {
            object fixture = World(typeof(HbrgoHotaru), hp); var player = Field<Player>(fixture, "Player"); var hero = Field<Creature>(fixture, "Hero");
            var shield = Shield(fixture, 1234); var ancient = Ancient(player);
            await (Task)Invoke(BeforeStarted, ancient, false)!;
            Check(hero.CurrentHp == hp && hero.MaxHp == 3000 && shield.Dp == 1234,
                "actual patched Neow BeforeEventStarted preserves HP" + hp + " and does not conflate HP skip with DP entry restore");
        }
        Type guard = Mod.GetType("HBRGOSakiMod.Core.Patches.HotaruAncientHealthRecoveryPatch", true)!;
        MethodInfo prefix = guard.GetMethod("Prefix", F)!;
        object hotaru = World(typeof(HbrgoHotaru)); var task = new TaskCompletionSource().Task;
        object?[] preFinished = [Ancient(Field<Player>(hotaru, "Player")), true, task];
        Check((bool)Invoke(prefix, null, preFinished)! && ReferenceEquals(preFinished[2], task), "HP prefix keeps pre-finished native path");
        object normal = World(typeof(HbrgoSaki31O)); object?[] other = [Ancient(Field<Player>(normal, "Player")), false, task];
        Check((bool)Invoke(prefix, null, other)! && ReferenceEquals(other[2], task), "HP prefix keeps non-Hotaru native HP path");
    }
    static async Task RecoveryModifiers()
    {
        foreach (int level in new[] { 0, (int)AscensionLevel.WearyTraveler })
        {
            object fixture = World(typeof(HbrgoHotaru)); Ascension(fixture, level);
            var player = Field<Player>(fixture, "Player"); var shield = Shield(fixture, 1234); Power<LastStandPower>(fixture, 1);
            await Entry(Ancient(player), player);
            Check(shield.Dp == 1234, "LastStand still prevents ancient DP restoration at asc" + level);
            foreach (int disabled in new[] { 1, 2 })
            {
                fixture = World(typeof(HbrgoHotaru)); Ascension(fixture, level); player = Field<Player>(fixture, "Player");
                shield = Shield(fixture, 1234); Power<DisabledPower>(fixture, disabled);
                int expected = 1234 + (int)decimal.Floor(4766 * (level == 0 ? 1m : .8m) * (1m - disabled * .5m));
                await Entry(Ancient(player), player);
                Check(shield.Dp == expected, "Disabled" + disabled + " recovery discount still applies at asc" + level + " (actual " + shield.Dp + ")");
            }
        }
    }
    static void TextCases()
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository != null && !File.Exists(Path.Combine(repository.FullName, "HBRGOSakiMod.csproj"))) repository = repository.Parent;
        if (repository == null) throw new Exception("Cannot find repository");
        string[] keys = ["HOTARU_ELECTRONIC_SOLDIER_MANUAL.description", "HOTARU_ELECTRONIC_SOLDIER_MANUAL.compactDescription",
            "HOTARU_ELECTRONIC_SOLDIER_MANUAL.hbrDescription", "HOTARU_ELECTRONIC_SOLDIER_MANUAL.hbrCompactDescription",
            "HOTARU_MANUAL_DP_RULES.description", "HOTARU_MANUAL_DP_RULES.hbrDescription"];
        foreach (string language in new[] { "zhs", "eng" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository.FullName, "HBRGOSakiMod/localization", language, "relics.json")));
            foreach (string key in keys)
                Check(document.RootElement.GetProperty(key).GetString()!.Contains("先古之民的治疗改为恢复DP。"),
                    language + " " + key + " manual matches ancient DP restoration");
        }
    }
    public static void Run(string path)
    {
        bool ok = false; string? error = null;
        try { Setup(); Matrix().GetAwaiter().GetResult(); Controls().GetAwaiter().GetResult(); HpGuard().GetAwaiter().GetResult(); RecoveryModifiers().GetAwaiter().GetResult(); TextCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(exception); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, rows = Rows, error, originalEntryCalls = OriginalCalls,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), nativeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            nativeSourceSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), telemetryIsolation = Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary = "Production Harmony postfix attached to actual native BeginEvent; original event synchronizer/UI body replaced by controlled Task only. Actual Deflector missing-DP recovery and native ascension selector run. Actual Harmony-guarded Neow BeforeEventStarted verifies HP skip. StringName/log/relic Flash leaves isolated. No Godot or complete event/UI gameplay loop." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Hotaru ancient DP checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
