using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class Checks
{
    // Exact English text read from installed SlayTheSpire2.pck, localization/eng
    // (sts2.dll A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52).
    // The native LocTable, LocManager.SmartFormat and LocString format paths run;
    // filesystem locale loading and locale switching UI do not run.
    static readonly Dictionary<string, string> PowerText = new()
    {
        ["REGEN_POWER.smartDescription"] = "At the end of your turn, heal [blue]{Amount}[/blue] HP, then reduce [gold]Regen[/gold] by [blue]1[/blue].",
        ["PAPER_CUTS_POWER.smartDescription"] = "Whenever [gold]{OwnerName}[/gold] deals unblocked attack damage to you, you lose [blue]{Amount}[/blue] [gold]Max HP[/gold]."
    };
    static readonly Dictionary<string, string> ModifierText = new()
    {
        ["TERMINAL.description"] = "Whenever you enter a new room, lose [red]1[/red] Max HP. Start each combat with [blue]5[/blue] [gold]Plating[/gold].",
        ["NIGHT_TERRORS.description"] = "Resting at [gold]Rest Sites[/gold] heals all of your HP, but costs [red]5[/red] Max HP."
    };
    static readonly Dictionary<string, string> CookText = new()
    {
        ["OPTION_COOK.description"] = "[gold]Remove[/gold] [blue]{Cards}[/blue] cards from your [gold]Deck[/gold] and gain [green]{MaxHp}[/green] Max HP."
    };
    static bool True(ref bool __result) { __result = true; return false; }
    static bool PrefixString(ref string __result) { __result = "energy"; return false; }
    static bool CaptureLoseMaximum(decimal amount, ref Task __result)
    { Maximum.Add(-amount); __result = Task.CompletedTask; return false; }
    static T Power<T>(int amount) where T : PowerModel
    {
        var power = Blank<T>(); Set(power, "<IsMutable>k__BackingField", true);
        Set(power, "_owner", Hero); Set(power, "_amount", amount); return power;
    }
    static void NativeLocalization()
    {
        string nativeRuntime = Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY") ??
            "D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(nativeRuntime, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        var manager = Blank<LocManager>();
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture);
        Assembly smart = Assembly.Load("SmartFormat");
        object formatter = smart.GetType("SmartFormat.Smart", true)!.GetMethod("CreateDefaultSmartFormat")!
            .Invoke(null, [null])!;
        typeof(LocManager).GetField("_smartFormatter", F)!.SetValue(null, formatter);
        Set(manager, "_tables", new Dictionary<string, LocTable>
        {
            ["powers"] = new LocTable("powers", PowerText, null!),
            ["modifiers"] = new LocTable("modifiers", ModifierText, null!),
            ["rest_site_ui"] = new LocTable("rest_site_ui", CookText, null!)
        });
    }
    static LocString PowerDescription(PowerModel power, string key, int? amount = null)
    {
        var text = new LocString("powers", key);
        AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription").Invoke(power, [text, amount]);
        text.Add("OwnerName", "Test source"); return text;
    }
    static async Task DescriptionChecks(Harmony h)
    {
        NativeLocalization();
        Type energy = typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!;
        foreach (MethodInfo method in energy.GetMethods(F).Where(m => m.Name == "GetPrefix")) Patch(h, method, nameof(PrefixString));
        Patch(h, AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.Decrement)), nameof(CaptureEnergy));
        Patch(h, AccessTools.PropertyGetter(typeof(CookRestSiteOption), "IsEnabled"), nameof(True));
        foreach (string name in new[] { "HbrNativeFixedPowerDescriptionPatch", "HbrNativeFixedCookDescriptionPatch",
            "HbrNativeFixedModifierDescriptionPatch", "HbrNativeFixedModifierTextPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "actual native fixed description patch installs " + name);

        RegenPower regen = Power<RegenPower>(5);
        LocString regenText = PowerDescription(regen, "REGEN_POWER.smartDescription");
        Check(regenText.GetFormattedText().Contains("[blue]500[/blue] HP") &&
            regenText.GetFormattedText().Contains("by [blue]1[/blue]") && regen.Amount == 5,
            "actual native Regen smart description shows 500 HP; 5 stacks and 1-stack decay stay unchanged");
        await regen.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]);
        Check(Heals[^1] == 500, "actual native 5-stack Regen issues fixed 500 heal");
        PaperCutsPower cuts = Power<PaperCutsPower>(1);
        LocString cutsText = PowerDescription(cuts, "PAPER_CUTS_POWER.smartDescription");
        Check(cutsText.GetFormattedText().Contains("[blue]100[/blue]") && cuts.Amount == 1,
            "actual native PaperCuts smart description shows 100 max HP while counter stays 1");
        var hit = Blank<DamageResult>(); Set(hit, "<UnblockedDamage>k__BackingField", 1);
        await cuts.AfterDamageGiven(new ThrowingPlayerChoiceContext(), Hero, hit, ValueProp.Move, Hero, null!);
        Check(Maximum[^1] == -100, "actual native PaperCuts 1-stack hit loses fixed 100 max HP");
        var cook = Blank<CookRestSiteOption>(); Set(cook, "<Owner>k__BackingField", Player);
        LocString cookText = (LocString)AccessTools.PropertyGetter(typeof(CookRestSiteOption), "Description").Invoke(cook, null)!;
        Check(cookText.GetFormattedText().Contains("[green]900[/green]") && cookText.GetFormattedText().Contains("[blue]2[/blue]"),
            "native Cook description shows 900 max HP and retains 2 card removals");

        var run = Blank<RunState>(); Set(run, "_players", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player });
        var terminal = Blank<Terminal>(); Set(terminal, "_runState", run);
        var night = Blank<NightTerrors>(); Set(night, "_runState", run);
        Set(terminal, "<Id>k__BackingField", new ModelId("MODIFIER", "TERMINAL"));
        Set(night, "<Id>k__BackingField", new ModelId("MODIFIER", "NIGHT_TERRORS"));
        LocString terminalText = terminal.Description;
        LocString nightText = night.Description;
        Check(terminalText.GetFormattedText().Contains("[red]100[/red]") && terminalText.GetFormattedText().Contains("[blue]5[/blue]"),
            "native Terminal description changes only fixed HP to 100, retaining 5 Plating");
        Check(nightText.GetFormattedText().Contains("[red]500[/red]"), "native NightTerrors description shows 500 max HP cost");
        Check(new LocString("modifiers", "TERMINAL.description").GetFormattedText().Contains("[red]1[/red]"),
            "unmarked native LocString remains untouched even with identical key");
        string original = ModifierText["TERMINAL.description"];
        ModifierText["TERMINAL.description"] = "[color=#000001][img=1]asset1[/img][/color] Lose [red]1[/red] Max HP.";
        string guarded = terminalText.GetFormattedText();
        Check(guarded.Contains("#000001") && guarded.Contains("[img=1]") && guarded.Contains("asset1") && guarded.Contains("[red]100[/red]"),
            "tag attributes, color digits and resource IDs are skipped by fixed numeric substitution");
        ModifierText["TERMINAL.description"] = "Lose 1 HP and 1 other resource.";
        Check(terminalText.GetRawText() == "Lose 1 HP and 1 other resource.", "ambiguous repeated native number preserves original text");
        ModifierText["TERMINAL.description"] = "Lose 2 HP.";
        Check(terminalText.GetRawText() == "Lose 2 HP.", "changed unknown native number preserves original text");
        ModifierText["TERMINAL.description"] = original;
        Mode(false);
        Check(terminalText.GetFormattedText().Contains("[red]1[/red]") && nightText.GetFormattedText().Contains("[red]5[/red]"),
            "existing modifier LocStrings revert to native values when HBR is disabled");
        Check(PowerDescription(regen, "REGEN_POWER.smartDescription").GetFormattedText().Contains("[blue]5[/blue] HP"),
            "disabled HBR power description retains native 5 HP");
        Mode(true);
    }
}
