using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.addons.mega_text;

static partial class EffectChecks
{
    public static void RunDisplayBody(string path)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Localization(); var harmony = new Harmony(ProductionId + ".31x-rich-body");
            harmony.Patch(AccessTools.Method(typeof(LocManager), "TriggerLocaleChange"),
                prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
            DisplayInstallRichResources(harmony);
            foreach (string name in new[] { "ThirtyOneXPrankFormatScopePatch", "ThirtyOneXPrankChineseRawPatch", "ThirtyOneXPrankDisplayPatch", "ThirtyOneXPrankRichClearPatch" })
                Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + name, true)!).Patch().Count > 0,
                    "installs actual rich-body production boundary " + name);
            DisplayInstallTerminals(harmony);
            Set(LocManager.Instance, "<Language>k__BackingField", "rus");
            Set(LocManager.Instance, "<ValidationErrors>k__BackingField", Array.AsReadOnly(Array.Empty<LocValidationError>()));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "data/thirty_one_x_character_glossary.json")));
            var words = json.RootElement.GetProperty("entries").EnumerateObject().ToDictionary(row => row.Name,
                row => row.Value.EnumerateArray().Select(value => value.GetString()!).ToArray());
            DisplayRichBodyCases(words, harmony); ok = true;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(exception); }
        finally { Call("ThirtyOneXLanguagePrankHelper", "Clear", false); }
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            boundary = "Focused real managed MegaRichTextLabel Text/SetTextAutoSize/ParseBbcode calls and production receipt/locale restore; only native engine resources, text storage, font, layout and redraw leaves controlled. Dictionary full-inventory validation is a separate strict full-host check, not this focused path." },
            new JsonSerializerOptions { WriteIndented = true }));
        if (!ok) throw new Exception("Inspect " + path);
        Console.WriteLine("PASS " + Passed.Count + " focused rich-body checks");
    }
    // MegaRichTextLabel initializes native TextParagraph/effect resources. They
    // are opaque engine leaves here; its actual managed text routing stays real.
    static IEnumerable<CodeInstruction> DisplayResourceConstructorBody(IEnumerable<CodeInstruction> instructions)
    {
        // Do not import the engine constructor's unmanaged function-pointer
        // signature into MonoMod's managed emitter, even behind a false prefix.
        yield return new(System.Reflection.Emit.OpCodes.Ldarg_0);
        yield return new(System.Reflection.Emit.OpCodes.Call, typeof(GC).GetMethod(nameof(GC.SuppressFinalize))!);
        yield return new(System.Reflection.Emit.OpCodes.Ret);
    }
    static void DisplayInstallRichResources(Harmony harmony)
    {
        MethodInfo skip = typeof(EffectChecks).GetMethod(nameof(DisplayResourceConstructorBody), F)!;
        foreach (Type type in typeof(MegaRichTextLabel).Assembly.GetTypes().Where(type =>
                     type.Namespace == "MegaCrit.Sts2.Core.RichTextTags" && !type.IsAbstract &&
                     typeof(RichTextEffect).IsAssignableFrom(type)).Append(typeof(TextParagraph)))
            if (type.GetConstructor(Type.EmptyTypes) is { } constructor)
                harmony.Patch(constructor, transpiler: new HarmonyMethod(skip));
        harmony.Patch(typeof(MegaRichTextLabel).GetMethod("InstallEffectsIfNeeded", F)!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
    }
    static bool DisplayTerminalParse(GodotObject __instance, string __0)
    {
        if (!DisplayTerminals.TryGetValue(__instance, out var terminal)) return true;
        terminal.Slots["Text"] = __0; return false;
    }
    static string DisplayRichEngineRead(RichTextLabel surface) => DisplayTerminals[surface].Slots.GetValueOrDefault("Text", string.Empty);
    static void DisplayRichEngineWrite(RichTextLabel surface, string text) => DisplayTerminals[surface].Slots["Text"] = text;
    static IEnumerable<CodeInstruction> DisplayRichEngineBindings(IEnumerable<CodeInstruction> instructions)
    {
        int count = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(RichTextLabel) &&
                method.Name is "get_Text" or "set_Text")
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = typeof(EffectChecks).GetMethod(method.Name == "get_Text" ? nameof(DisplayRichEngineRead) : nameof(DisplayRichEngineWrite), F)!;
                count++;
            }
            yield return instruction;
        }
        if (count != 2) throw new Exception("Expected exactly the native RichTextLabel get/set engine binding calls in MegaRich autosize, found " + count);
    }
    static void DisplayRichBodyCases(Dictionary<string, string[]> words, Harmony harmony)
    {
        // These thin Godot bindings can be inlined by the CLR before their
        // prefixes, which would initialize a native engine class in this host.
        // Replace only those two terminal calls, retaining the actual native
        // game autosize body and both production high-level prefix boundaries.
        harmony.Patch(typeof(MegaRichTextLabel).GetMethod(nameof(MegaRichTextLabel.SetTextAutoSize))!,
            transpiler: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(DisplayRichEngineBindings), F)!));
        harmony.Patch(typeof(RichTextLabel).GetMethod(nameof(RichTextLabel.ParseBbcode))!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(DisplayTerminalParse), F)!) { priority = Priority.Last });
        Type helper = Helper("ThirtyOneXLanguagePrankHelper");
        var english = new Dictionary<string, LocTable> { ["rich_body"] = new("rich_body", new()
        {
            ["body"] = "Deal [color=#ffcc44]{n}[/color] damage. [img]res://icons/光.png[/img] {tag}",
            ["help"] = "Gain {n} block."
        }, null!) };
        var chinese = new Dictionary<string, LocTable> { ["rich_body"] = new("rich_body", new()
        {
            ["body"] = "造成[color=#ffcc44]{n}[/color]点伤害。[img]res://icons/光.png[/img] {tag}",
            ["help"] = "获得{n}点格挡。"
        }, null!) };
        Set(LocManager.Instance, "_tables", english); Set(LocManager.Instance, "_engTables", english);
        var body = new LocString("rich_body", "body"); body.Add("n", 17); body.Add("tag", "{opaque}");
        var help = new LocString("rich_body", "help"); help.Add("n", 8);
        string rawBody = body.GetFormattedText(), rawHelp = help.GetFormattedText();
        var card = DisplayFake<MegaRichTextLabel>(72001, "[center][font_size=40]" + rawBody + "[/font_size][/center]");
        var hover = DisplayFake<MegaRichTextLabel>(72002, rawHelp);
        DisplaySession(words, chinese);
        object translator = helper.GetField("_session", F)!.GetValue(null)!.GetType().GetField("Translator", F)!
            .GetValue(helper.GetField("_session", F)!.GetValue(null))!;
        string Expected(ulong id, string text) => (string)Invoke(DisplayTranslatorType.GetMethod("Translate", F)!, translator,
            (ulong)Invoke(helper.GetMethod("SurfaceKey", F)!, null, id, "Text")!, text)!;
        Call("ThirtyOneXLanguagePrankHelper", "RefreshNode", card);
        string chineseBody = body.GetFormattedText(), chineseHelp = help.GetFormattedText();
        string wrapped = "[center][font_size=40]" + chineseBody + "[/font_size][/center]";
        Check(card.Text == Expected(72001, wrapped), "existing native card's nested BBCode-wrapped description finds Chinese provenance and converts at real MegaRich boundary");
        Check(card.Text.Contains("[color=#ffcc44]17[/color]") && card.Text.Contains("[img]res://icons/光.png[/img]") && card.Text.Contains("{opaque}"),
            "rich card body retains numeric color tags, image path and opaque placeholder bytes");
        hover.Text = chineseHelp;
        Check(hover.Text == Expected(72002, chineseHelp), "actual MegaRich hidden Text setter routes hover body through translator exactly once");
        string before = hover.Text;
        for (int i = 0; i < 15; i++) { hover.SetTextAutoSize(hover.Text); hover.ParseBbcode(hover.Text); }
        Check(hover.Text == before, "nested native autosize, Godot setter and ParseBbcode readbacks cannot recursively translate output");
        body.Add("n", 18); chineseBody = body.GetFormattedText(); wrapped = "[center]" + chineseBody + "[/center]";
        card.SetTextAutoSize(wrapped);
        Check(card.Text == Expected(72001, wrapped) && card.Text.Contains("[color=#ffcc44]18[/color]"),
            "actual NCard SetTextAutoSize entry refreshes body numbers while preserving BBCode and source");
        var parsed = DisplayFake<RichTextLabel>(72003, string.Empty);
        parsed.ParseBbcode(chineseHelp);
        Check(parsed.Text == Expected(72003, chineseHelp), "direct Godot BBCode replacement receives the same Text translation receipt");

        bool testing = TestMode.IsOn; TestMode.IsOn = false;
        try
        {
            LocManager.Instance.StartOverridingLanguageAsEnglish();
            try
            {
                string englishHelp = help.GetFormattedText();
                hover.SetTextAutoSize(englishHelp);
                Check(hover.Text == "Gain 8 block.", "native temporary English formatting remains original English at actual MegaRich display boundary");
                card.AppendText(" tail");
                Check(card.Text == "[center]" + rawBody.Replace("17", "18") + "[/center] tail",
                    "English-mode append restores the prior body prefix before appending, without leaving mixed remnants");
            }
            finally { LocManager.Instance.StopOverridingLanguageAsEnglish(); }
        }
        finally { TestMode.IsOn = testing; }
        hover.Text = help.GetFormattedText();
        Check(hover.Text == before, "next ordinary body refresh resumes the same battle's stable language choices after English override");
        Call("ThirtyOneXLanguagePrankHelper", "Clear", false);
        Check(hover.Text == rawHelp && parsed.Text == rawHelp, "battle cleanup restores exact original-locale hover and parsed bodies");
        Check(card.Text == "[center]" + rawBody.Replace("17", "18") + "[/center] tail", "cleanup preserves the superseding English append and its full wrapped original");
        Check(LocManager.Instance.Language == "rus", "body display repair does not change the player's real language setting");
    }
}
