using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly List<string> Passed = [];
    static Assembly Mod = null!;
    static Type Bridge = null!;
    static MethodInfo Command = null!, MoveNext = null!;
    static object?[] LastArguments = [];
    static int Entries;
    static bool Data;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Passed.Add(name); }
    static object? Call(string method, params object?[] args) => Bridge.GetMethod(method, F)!.Invoke(null, args);
    static Creature Actor() => (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
    static IEnumerable<Creature> Read(object?[] args) => (IEnumerable<Creature>)Call("ReadTargets", (object)args)!;
    static void Write(object?[] args, IEnumerable<Creature> source, IEnumerable<Creature> replacement,
        Func<Creature, Creature>? map = null) => Call("WriteTargets", args, source, replacement, map);

    static void Selection()
    {
        Bridge = Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!;
        Command = (MethodInfo)Bridge.GetField("Command", F)!.GetValue(null)!;
        MoveNext = (MethodInfo)Bridge.GetField("MoveNext", F)!.GetValue(null)!;
        Data = (bool)Bridge.GetProperty("UsesTargetData", F)!.GetValue(null)!;
        Check(Command.DeclaringType == typeof(CreatureCmd), "selected command belongs to the real native assembly");
        Check(Command.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == MoveNext.DeclaringType,
            "selected leaf and MoveNext belong to the same native async command");
        string element = Command.GetParameters()[1].ParameterType.GetGenericArguments()[0].FullName!;
        Check(element == (Data ? "MegaCrit.Sts2.Core.Entities.Creatures.DamageTargetData" : typeof(Creature).FullName),
            "leaf uses the current native target representation");

        string[] classes = ["HbrAbsoluteBaseDamageCommandPatch", "HbrEffectSkillDamageContextPatch",
            "HbrNumericCriticalScopePatch", "HighHitDamageCommandScopePatch", "HotaruCannonDamageContextPatch",
            "HotaruCannonDirectDamagePatch", "HpOnlyDamageContextPatch", "LegendAttackContextDamagePatch",
            "MoreThanOneTruthContextDamagePatch", "NararaForcedTurnDamageScopePatch", "PhantomDamageTargetPatch",
            "RollMultiHitDamagePatch", "SaviorGrandEntranceDamageScopePatch", "WorldsFinestDamageScopePatch"];
        foreach (string name in classes)
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            Check(Equals(type.GetMethod("TargetMethod", F)!.Invoke(null, null), Command), name + " targets the leaf once");
        }
        foreach (string name in new[] { "DamageResultAmountPatch", "ItsMygoIncomingDamageCapturePatch", "StrongestShieldDamagePatch" })
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            Check(Equals(type.GetMethod("TargetMethod", F)!.Invoke(null, null), MoveNext), name + " targets actual native async body");
            MethodInfo transpiler = type.GetMethod("Transpiler", F)!;
            var instructions = PatchProcessor.GetOriginalInstructions(MoveNext).ToArray();
            object?[] parameters = transpiler.GetParameters().Length == 1 ? [instructions] : [instructions, MoveNext];
            var changed = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, parameters)!).ToArray();
            Check(changed.Length != 0, name + " validates and rewrites the actual native IL");
        }
        using var pe = new PEReader(File.OpenRead(Bootstrap.ModPath));
        var metadata = pe.GetMetadataReader();
        Check(!metadata.TypeReferences.Select(metadata.GetTypeReference).Any(t => metadata.GetString(t.Name) == "DamageTargetData"),
            "published mod has no 0.113-only DamageTargetData TypeRef");
    }

    static object?[] Arguments(object targets, Creature dealer, CardModel card)
    {
        var args = new object?[Command.GetParameters().Length];
        args[1] = targets; args[2] = 123.75m; args[3] = ValueProp.Unpowered;
        args[4] = dealer; args[5] = card;
        for (int i = 6; i < args.Length; i++)
            args[i] = Command.GetParameters()[i].ParameterType.IsEnum
                ? Enum.ToObject(Command.GetParameters()[i].ParameterType, 0)
                : RuntimeHelpers.GetUninitializedObject(Command.GetParameters()[i].ParameterType);
        return args;
    }

    static void Projection()
    {
        Creature a = Actor(), b = Actor(), c = Actor();
        Type cardType = typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad", true)!;
        var card = (CardModel)RuntimeHelpers.GetUninitializedObject(cardType);
        var markers = Arguments(new[] { a, a, b }, c, card);
        object?[] originalArguments = (object?[])markers.Clone();
        if (!Data)
        {
            IEnumerable<Creature> source = Read(markers);
            Check(ReferenceEquals(source, markers[1]), "stable target sequence passes through without conversion");
            Write(markers, source, [b, a]);
            Check(((IEnumerable<Creature>)markers[1]!).SequenceEqual([b, a]), "stable target filtering/reordering is retained");
            Invocation(markers, c, card);
            return;
        }

        Type dataType = Command.GetParameters()[1].ParameterType.GetGenericArguments()[0];
        FieldInfo target = dataType.GetField("target")!, vfx = dataType.GetField("hitVfx")!, center = dataType.GetField("playVfxOnCreatureCenter")!;
        Array records = Array.CreateInstance(dataType, 3);
        foreach (var (creature, label, atCenter, index) in new[] { (a, "one", false, 0), (a, "two", true, 1), (b, "three", true, 2) })
        {
            object value = Activator.CreateInstance(dataType, creature)!;
            vfx.SetValue(value, label); center.SetValue(value, atCenter); records.SetValue(value, index);
        }
        var oneShot = new SingleUse(records.Cast<object>().ToArray());
        object lazy = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!.MakeGenericMethod(dataType).Invoke(null, [oneShot])!;
        markers[1] = lazy;
        IEnumerable<Creature> projection = Read(markers);
        Check(projection.SequenceEqual([a, a, b]), "real native records decode targets in order, preserving duplicates");
        Check(markers[1] is Array, "one-shot native target iterator becomes a repeatable native array");
        Check(Read(markers).SequenceEqual([a, a, b]) && oneShot.Enumerations == 1,
            "subsequent entry adapters do not re-enumerate a one-shot source");
        Write(markers, projection, [a, a, b]);
        Array duplicated = (Array)markers[1]!;
        Check((string?)vfx.GetValue(duplicated.GetValue(0)) == "one" && (string?)vfx.GetValue(duplicated.GetValue(1)) == "two",
            "duplicate targets keep separate native hit-VFX records");
        Check((bool)center.GetValue(duplicated.GetValue(1))!, "duplicate record retains native VFX-center flag");
        IEnumerable<Creature> beforeRedirect = Read(markers);
        Write(markers, beforeRedirect, [b, c], creature => ReferenceEquals(creature, a) ? c : creature);
        Array redirected = (Array)markers[1]!;
        Check(ReferenceEquals(target.GetValue(redirected.GetValue(0)), b) && ReferenceEquals(target.GetValue(redirected.GetValue(1)), c),
            "native target records support reordered filtering and redirection");
        Check((string?)vfx.GetValue(redirected.GetValue(0)) == "three" && (string?)vfx.GetValue(redirected.GetValue(1)) == "one",
            "redirection carries each selected original record's VFX metadata");
        Check(ReferenceEquals(target.GetValue(records.GetValue(0)), a), "redirection does not mutate caller-owned native record structs");
        foreach (int index in Enumerable.Range(0, markers.Length).Where(i => i != 1))
            Check(originalArguments[index]?.GetType().IsValueType == true
                    ? Equals(markers[index], originalArguments[index])
                    : ReferenceEquals(markers[index], originalArguments[index]),
                "target rewrite preserves non-target argument slot " + index);
        var emptyProjection = Read(markers); Write(markers, emptyProjection, []);
        Check(markers[1] is Array empty && empty.Length == 0 && empty.GetType().GetElementType() == dataType,
            "empty filtered result retains native enumerable element type");
        markers[1] = records;
        Invocation(markers, c, card);
    }

    static bool Probe(object?[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        Entries++; LastArguments = (object?[])__args.Clone();
        __result = Task.FromResult<IEnumerable<DamageResult>>([]);
        return false;
    }

    static void Invocation(object?[] args, Creature dealer, CardModel card)
    {
        var harmony = new Harmony("hbrgo.native-damage-api.fixture");
        harmony.Patch(Command, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Probe), F)));
        try
        {
            Entries = 0;
            ((Task)Command.Invoke(null, args)!).GetAwaiter().GetResult();
            Check(Entries == 1, "direct native leaf call enters one boundary");
            Check(ReferenceEquals(LastArguments[4], dealer) && ReferenceEquals(LastArguments[5], card),
                "leaf forwards original dealer and card ownership");
            Check(Equals(LastArguments[2], 123.75m) && Equals(LastArguments[3], ValueProp.Unpowered),
                "leaf forwards exact raw amount and original unpowered flags");
            if (Data)
            {
                MethodInfo wrapper = typeof(CreatureCmd).GetMethods().Single(m => m.Name == nameof(CreatureCmd.Damage) &&
                    m.GetParameters().Length == 8 && m.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>));
                object?[] forwarded = (object?[])args.Clone(); forwarded[1] = new[] { dealer };
                Entries = 0;
                ((Task)wrapper.Invoke(null, forwarded)!).GetAwaiter().GetResult();
                Check(Entries == 1, "native Creature wrapper forwards to one leaf boundary without a second scope");
                Check(Equals(LastArguments[7], args[7]), "wrapper retains explicit ModifyDamageHookType");
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    sealed class SingleUse(object[] items) : IEnumerable
    {
        internal int Enumerations;
        public IEnumerator GetEnumerator()
        {
            if (++Enumerations != 1) throw new Exception("Source target data enumerated more than once");
            return items.GetEnumerator();
        }
    }

    public static int Run()
    {
        string? failure = null;
        try
        {
            Mod = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "HBRGOSakiMod");
            Selection(); Projection();
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        string path = Bootstrap.ReportPath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            ok = failure == null, checks = Passed.Count, passed = Passed, error = failure,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Bootstrap.ModPath))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            usesTargetData = Data,
            boundary = "Real native metadata, production target selectors/target bridge, three transpilers on actual MoveNext IL, and direct native/wrapper dispatch through a Harmony leaf probe execute. The probe returns before native damage/visuals; no numerical damage, combat lifecycle, rendered VFX, game/native Godot, packaging or networking is asserted."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(failure == null ? "PASS" : "FAIL")} {Passed.Count} native damage API checks");
        return failure == null ? 0 : 1;
    }
}
