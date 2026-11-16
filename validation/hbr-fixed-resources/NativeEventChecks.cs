using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

static partial class Checks
{
    // Exact English text read from the installed v0.111.0/v0.113.0 PCK, localization/eng/events.json.
    // These options write their fixed HP amount literally instead of through a variable.
    static readonly Dictionary<string, string> EventText = new()
    {
        // 0.113 validates an EventOption title before constructing its description.
        ["COLOSSAL_FLOWER.pages.INITIAL.options.EXTRACT_CURRENT_PRIZE_1.title"] = "Extract Nectar",
        ["COLOSSAL_FLOWER.pages.INITIAL.options.EXTRACT_CURRENT_PRIZE_1.description"] = "Gain [blue]{Prize1}[/blue] [gold]Gold[/gold].",
        ["COLOSSAL_FLOWER.pages.INITIAL.options.REACH_DEEPER_1.title"] = "Reach Deeper",
        ["TRIAL.pages.NOBLE.options.GUILTY.title"] = "DECIDE: Guilty",
        ["COLOSSAL_FLOWER.pages.INITIAL.options.REACH_DEEPER_1.description"] = "Enter deeper. Lose [red]5[/red] HP.",
        ["TRIAL.pages.NOBLE.options.GUILTY.description"] = "Heal [green]10[/green] HP."
    };

    static T Event<T>() where T : EventModel
    {
        T model = Blank<T>(); Set(model, "<IsMutable>k__BackingField", true);
        Set(model, "<Owner>k__BackingField", Player); return model;
    }
    static Task Native(object model, string method) => (Task)AccessTools.Method(model.GetType(), method).Invoke(model, null)!;
    // Runs a real event body up to its captured HP command, then stops before the following page/UI leaf.
    static async Task UntilCommand(object model, string method)
    {
        StopAfterCommand = true;
        try { await Native(model, method); throw new Exception(method + " did not reach its HP command"); }
        catch (StopAtEventUi) { }
        finally { StopAfterCommand = false; }
    }
    static MethodBase Body(Type type, string method)
    {
        MethodInfo outer = AccessTools.Method(type, method);
        Type? state = outer.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        return state == null ? outer : AccessTools.Method(state, "MoveNext");
    }
    static int Calls(MethodBase body, MethodInfo target) => PatchProcessor.GetCurrentInstructions(body).Count(i => i.Calls(target));

    static async Task NativeEventChecks(Harmony h)
    {
        // Options add the owner's character and multiplayer details to their text; the blank
        // player has no character or run, and none of the checked templates use those details.
        Patch(h, AccessTools.Method(typeof(EventOption), "AddLocVars"), nameof(Skip));
        Type flowerPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.Events.ColossalFlowerHpLossPatch", true)!;
        MethodInfo prizeOption = AccessTools.Method(flowerPatch, "ThatDoesPrizeDamage");
        MethodInfo prizeDamage = AccessTools.Method(flowerPatch, "ScalePrizeDamage");

        // Sunken Statue: HpLoss is a registered fixed variable, consumed after the gold gain. Its
        // canonical relic title needs ModelDb, so only the native Gold 111 and HpLoss 7 are seeded.
        var statue = Event<SunkenStatue>();
        var statueVariables = new DynamicVarSet(new DynamicVar[] { new GoldVar(111), new DynamicVar("HpLoss", 7m) });
        AccessTools.Method(typeof(DynamicVarSet), "InitializeWithOwner").Invoke(statueVariables, [statue]);
        Set(statue, "_dynamicVars", statueVariables);
        DynamicVar statueLoss = statue.DynamicVars["HpLoss"];
        Check(Read(statueLoss) == 700 && statueLoss.ToString() == "700", "SunkenStatue native HpLoss 7 is consumed and shown as 700");
        await UntilCommand(statue, "DiveIntoWater");
        Check(Damage[^1] == 700 && statueLoss.BaseValue == 7, "real Dive Into Water loses 700 HP while stored HpLoss stays 7");
        Mode(false); await UntilCommand(statue, "DiveIntoWater");
        Check(Damage[^1] == 7 && statueLoss.ToString() == "7", "disabled HBR Dive Into Water keeps native 7 HP and text"); Mode(true);

        // Colossal Flower: each dig loses _prizeDamage[NumberOfDigs] from a static table.
        int[] table = (int[])AccessTools.Field(typeof(ColossalFlower), "_prizeDamage").GetValue(null)!;
        Check(table.SequenceEqual(new[] { 5, 6, 7 }), "native Colossal Flower dig costs are 5, 6 and 7 HP");
        var flower = Event<ColossalFlower>();
        for (int digs = 0; digs < table.Length; digs++)
        {
            Set(flower, "_numberOfDigs", digs);
            await UntilCommand(flower, "DealReachDeeperDamage");
            Check(Damage[^1] == table[digs] * 100, $"real dig {digs + 1} loses {table[digs] * 100} HP");
        }
        Mode(false); Set(flower, "_numberOfDigs", 0); await UntilCommand(flower, "DealReachDeeperDamage");
        Check(Damage[^1] == 5, "disabled HBR first dig keeps native 5 HP"); Mode(true);

        var options = (IEnumerable<EventOption>)AccessTools.Method(typeof(ColossalFlower), "GenerateInitialOptions").Invoke(flower, null)!;
        EventOption dig = options.Single(option => option.WillKillPlayer != null);
        bool KillsAt(int hp) { Set(Hero, "_currentHp", hp); return dig.WillKillPlayer!(Player); }
        bool lethal = KillsAt(500), survives = !KillsAt(501); Set(Hero, "_currentHp", 6000);
        Check(lethal && survives, "real initial dig option's lethal check uses the scaled 500 HP");
        Check(dig.Description.GetFormattedText() == "Enter deeper. Lose [red]500[/red] HP.",
            "real initial dig option's literal text shows 500 HP");
        Mode(false);
        Check(dig.Description.GetFormattedText() == "Enter deeper. Lose [red]5[/red] HP.", "disabled HBR dig text keeps native 5");
        Mode(true);
        foreach ((MethodBase body, MethodInfo call, int count) in new (MethodBase, MethodInfo, int)[]
        {
            (AccessTools.Method(typeof(ColossalFlower), "GenerateInitialOptions"), prizeOption, 1),
            (Body(typeof(ColossalFlower), "ReachDeeper"), prizeOption, 2),
            (Body(typeof(ColossalFlower), "DealReachDeeperDamage"), prizeDamage, 1)
        })
            Check(Calls(body, call) == count, $"{body.DeclaringType!.Name}.{body.Name} routes {count} table reads through {call.Name}");

        // The flower appears only while each player has more HP than all three digs cost.
        MethodInfo allowed = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ColossalFlower), "IsAllowed"))
            .Where(i => i.opcode == System.Reflection.Emit.OpCodes.Ldftn).Select(i => (MethodInfo)i.operand)
            .Single(m => m.DeclaringType?.DeclaringType == typeof(ColossalFlower));
        object lambdas = AccessTools.Field(allowed.DeclaringType, "<>9").GetValue(null)!;
        bool AllowedAt(int hp) { Set(Hero, "_currentHp", hp); return (bool)allowed.Invoke(lambdas, [Player])!; }
        bool below = AllowedAt(1899), at = AllowedAt(1900);
        Mode(false); bool native = AllowedAt(19) && !AllowedAt(18); Mode(true); Set(Hero, "_currentHp", 6000);
        Check(!below && at && native, "Colossal Flower appears from 1900 HP in HBR and from native 19 HP otherwise");

        // Trial: Noble Guilty heals a literal 10, and Accept tags the option whose text writes it.
        var trial = Event<Trial>();
        await UntilCommand(trial, "NobleGuilty");
        Check(Heals[^1] == 1000, "real Noble Guilty heals 1000 HP");
        Mode(false); await UntilCommand(trial, "NobleGuilty"); Check(Heals[^1] == 10, "disabled HBR Noble Guilty heals native 10"); Mode(true);
        Type trialPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.Events.TrialNobleGuiltyTextPatch", true)!;
        MethodInfo tag = AccessTools.Method(trialPatch, "Tag");
        Check(Calls(AccessTools.Method(typeof(Trial), "Accept"), tag) == 1, "Trial.Accept tags exactly one created option");
        var guilty = new EventOption(trial, () => Task.CompletedTask, "TRIAL.pages.NOBLE.options.GUILTY", []);
        tag.Invoke(null, [guilty, trial]);
        Check(guilty.Description.GetFormattedText() == "Heal [green]1000[/green] HP.", "tagged Noble Guilty text shows 1000 HP");
        Mode(false);
        Check(guilty.Description.GetFormattedText() == "Heal [green]10[/green] HP.", "disabled HBR Noble Guilty text keeps native 10");
        Mode(true);
    }
}
