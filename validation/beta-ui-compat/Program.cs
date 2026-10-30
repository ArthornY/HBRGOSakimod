using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using Mono.Cecil;

static class UiChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly List<string> Passed = [];
    static readonly Dictionary<StringName, string> Names = new(ReferenceEqualityComparer.Instance);
    static readonly HashSet<string> PressedActions = new(StringComparer.Ordinal);
    static int ActionQueries;
    static Assembly Mod = null!;
    static readonly Harmony Patches = new("hbr.beta.ui.managed.checks");
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Passed.Add(label); }
    static T Blank<T>() { T value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static Type ModType(string name) => Mod.GetType("HBRGOSakiMod.Core." + name, true)!;
    static object? Call(Type type, string name, params object?[] args) => type.GetMethod(name, F)!.Invoke(null, args);
    static FieldInfo Field(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
            if (current.GetField(name, F) is FieldInfo field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static bool StringNameConstructor(StringName __instance, string __0)
    { GC.SuppressFinalize(__instance); Names[__instance] = __0; return false; }
    static bool QueryAction(StringName action, ref bool __result)
    { ActionQueries++; __result = Names.TryGetValue(action, out string? name) && PressedActions.Contains(name); return false; }
    static bool Skip() => false;
    static void Install()
    {
        Patches.Patch(typeof(StringName).GetConstructor([typeof(string)])!,
            prefix: new HarmonyMethod(typeof(UiChecks).GetMethod(nameof(StringNameConstructor), F)!));
        Patches.Patch(AccessTools.Method(typeof(InputEvent), nameof(InputEvent.IsActionPressed)),
            prefix: new HarmonyMethod(typeof(UiChecks).GetMethod(nameof(QueryAction), F)!));
    }
    static void InputCases()
    {
        Type helper = ModType("Helpers.LibraryInputCompatibility");
        FieldInfo confirmation = (FieldInfo)Call(helper, "ResolveConfirmationField", typeof(MegaInput))!;
        bool beta = typeof(MegaInput).GetField("confirm", F) != null;
        Check(confirmation.Name == (beta ? "confirm" : "accept") && confirmation.IsStatic &&
            confirmation.FieldType == typeof(StringName), "selects exact active runtime native confirmation field and StringName signature");
        InputEventAction input = Blank<InputEventAction>();
        string confirmAction = beta ? "ui_confirm" : "ui_accept";
        PressedActions.Add(confirmAction);
        Check((bool)Call(helper, "IsAdvancePressed", input)!, "native remappable confirmation action advances story");
        Check(ActionQueries == 1, "confirmation consumes one action query without a duplicate select dispatch");
        PressedActions.Clear(); PressedActions.Add("ui_select");
        Check((bool)Call(helper, "IsAdvancePressed", input)!, "native controller select action still advances story");
        PressedActions.Clear(); PressedActions.Add("mega_pause_and_back");
        Check(!(bool)Call(helper, "IsAdvancePressed", input)!, "unrelated pause/back action does not advance story");
        Check(!(bool)Call(helper, "IsAdvancePressed", [null])!, "null input is ignored");
        int before = Names.Count;
        PressedActions.Clear(); PressedActions.Add(confirmAction);
        for (int i = 0; i < 50; i++) Check((bool)Call(helper, "IsAdvancePressed", input)!, "cached confirmation remains responsive press " + i);
        Check(Names.Count == before, "subsequent presses allocate no new StringName or fallback physical-key binding");
        try { Call(helper, "ResolveConfirmationField", typeof(string)); throw new Exception("missing action accepted"); }
        catch (TargetInvocationException error) { Check(error.InnerException is MissingFieldException, "unsupported action API fails explicitly"); }
    }
    static void PotionCases()
    {
        Type helper = ModType("Helpers.PotionPopupCompatibility");
        var popup = Blank<NPotionPopup>();
        MemberInfo member = (MemberInfo?)typeof(NPotionPopup).GetProperty("IsMarkedForRemoval", F) ??
            typeof(NPotionPopup).GetField("IsMarkedForRemoval", F)!;
        foreach (bool removed in new[] { false, true, false, true })
        {
            if (member is PropertyInfo property) property.SetValue(popup, removed);
            else ((FieldInfo)member).SetValue(popup, removed);
            Check((bool)Call(helper, "IsMarkedForRemoval", popup)! == removed,
                "cached removal-state reader follows native " + member.MemberType + " value " + removed);
        }
    }
    static int ListenerCount(CardPile pile, string name) => ((Delegate?)Field(typeof(CardPile), name).GetValue(pile))?.GetInvocationList().Length ?? 0;
    static void DeckCleanupCases()
    {
        Type exit = ModType("Patches.DeckCountExitPatch"), predelete = ModType("Patches.DeckCountPredeletePatch");
        bool useExit = (bool)Call(exit, "Prepare")!, usePredelete = (bool)Call(predelete, "Prepare")!;
        Check(useExit != usePredelete, "exactly one native deck cleanup lifecycle is active");
        Type patch = useExit ? exit : predelete;
        MethodInfo target = (MethodInfo)Call(patch, "TargetMethod")!;
        Check(target.DeclaringType == typeof(NTopBarDeckButton) && !target.IsStatic && target.ReturnType == typeof(void) &&
            target.GetParameters().Select(p => p.ParameterType).SequenceEqual(useExit ? [] : new[] { typeof(int) }),
            "cleanup target uses declared void ExitTree or Notification(int), not inherited fallback");
        if (useExit)
        {
            MethodInfo nativeBaseExit = AccessTools.DeclaredMethod(typeof(NTopBarButton), "_ExitTree");
            Patches.Patch(nativeBaseExit, prefix: new HarmonyMethod(typeof(UiChecks).GetMethod(nameof(Skip), F)!));
        }
        Check(Patches.CreateClassProcessor(patch).Patch().Count > 0, "real Harmony installs active deck cleanup branch");
        var deck = Blank<NTopBarDeckButton>(); var pile = new CardPile(PileType.Deck);
        Field(typeof(NTopBarDeckButton), "_pile").SetValue(deck, pile);
        Action refresh = (Action)Call(ModType("Patches.DeckCountRefresh"), "Callback", deck)!;
        Action unrelated = () => { };
        pile.ContentsChanged += refresh; pile.ContentsChanged += unrelated;
        pile.CardAddFinished += refresh; pile.CardAddFinished += unrelated;
        pile.CardRemoveFinished += refresh; pile.CardRemoveFinished += unrelated;
        if (!useExit)
        {
            target.Invoke(deck, [0]);
            Check(new[] { "ContentsChanged", "CardAddFinished", "CardRemoveFinished" }.All(name => ListenerCount(pile, name) == 2),
                "non-predelete notification preserves all active listeners");
        }
        target.Invoke(deck, useExit ? [] : [1]);
        foreach (string name in new[] { "ContentsChanged", "CardAddFinished", "CardRemoveFinished" })
            Check(ListenerCount(pile, name) == 1, "real native lifecycle and production prefix remove only deck callback from " + name);
        target.Invoke(deck, useExit ? [] : [1]);
        Check(new[] { "ContentsChanged", "CardAddFinished", "CardRemoveFinished" }.All(name => ListenerCount(pile, name) == 1),
            "repeated cleanup preserves unrelated listeners without duplicates");
    }
    static void MetadataCases()
    {
        using AssemblyDefinition mod = AssemblyDefinition.ReadAssembly(Mod.Location);
        var references = mod.MainModule.GetMemberReferences().ToArray();
        Check(!references.OfType<FieldReference>().Any(r => r.DeclaringType.FullName == typeof(MegaInput).FullName && r.Name == "accept"),
            "candidate has no beta-missing direct MegaInput.accept field reference");
        Check(!references.OfType<FieldReference>().Any(r => r.DeclaringType.FullName == typeof(NPotionPopup).FullName && r.Name == "IsMarkedForRemoval"),
            "candidate has no beta-missing direct potion removal field reference");
        TypeDefinition input = mod.MainModule.GetType("HBRGOSakiMod.Core.Helpers.LibraryInputCompatibility");
        Check(!input.Methods.Single(m => m.Name == "IsAdvancePressed").Body.Instructions.Any(i =>
            i.Operand is MethodReference r && r.DeclaringType.Namespace == "System.Reflection"),
            "input hot path contains no per-event reflection calls");
        TypeDefinition potion = mod.MainModule.GetType("HBRGOSakiMod.Core.Helpers.PotionPopupCompatibility");
        Check(!potion.Methods.Single(m => m.Name == "IsMarkedForRemoval").Body.Instructions.Any(i =>
            i.Operand is MethodReference r && r.DeclaringType.Namespace == "System.Reflection"),
            "per-frame potion state hot path invokes only cached delegate");
    }
    public static int Run(Assembly candidate, string profile, string reportPath)
    {
        Mod = candidate; bool ok = false; string? error = null;
        try { Install(); MetadataCases(); InputCases(); PotionCases(); DeckCleanupCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
            ok, checks = Passed.Count, passed = Passed, error, profile,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MegaInput).Assembly.Location))),
            boundary = "Real cached production input action selection, native potion field/property reader, real active Harmony deck lifecycle patch and native CardPile listener removal execute. StringName native constructor and InputEvent.IsActionPressed are controlled leaves; stable base NTopBarButton exit is isolated while its derived native cleanup body executes. Uninitialized managed node objects are used, not actual Godot nodes. No rendered UI or game session, no packaging/Steam/save/network initialization. Beta uses verified isolated telemetry initializer boundary."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} UI runtime compatibility checks");
        return ok ? 0 : 1;
    }
}
