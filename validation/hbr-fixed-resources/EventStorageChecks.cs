using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

static partial class Checks
{
    sealed class StopAtEventUi : Exception;
    static DynamicVar? EventStoredVariable;
    static int RedirectedDp;
    static void StopAfterEventStorage(DynamicVar __instance)
    {
        // Complete the real native raw setter, then isolate the following event
        // option/UI refresh leaf. No fake increment or copied event algorithm.
        if (ReferenceEquals(__instance, EventStoredVariable)) throw new StopAtEventUi();
    }
    static bool CaptureMaxDp(int amount, ref int __result)
    { RedirectedDp = amount; __result = amount; return false; }
    static async Task EventStorageChecks(Harmony h)
    {
        var baths = Blank<AbyssalBaths>(); Set(baths, "<IsMutable>k__BackingField", true);
        Set(baths, "<Owner>k__BackingField", Player);
        DynamicVar damage = baths.DynamicVars.Damage;
        async Task Immerse()
        {
            EventStoredVariable = damage;
            try { await Effect(baths, "OnImmerse"); throw new Exception("native event did not reach isolated UI leaf"); }
            catch (StopAtEventUi) { }
            finally { EventStoredVariable = null; }
        }
        Mode(true);
        Check(damage.BaseValue == 3, "AbyssalBaths starts from native raw Damage 3");
        await Immerse();
        Check(Damage[^1] == 3 && damage.BaseValue == 4 && damage.PreviewValue == 4,
            "real first Immerse issues raw skill units3 and raw compound setter increments 3 to 4");
        await Immerse();
        Check(Damage[^1] == 4 && damage.BaseValue == 5,
            "real second Immerse issues raw skill units4 and raw compound setter increments 4 to 5");
        Check(Maximum[^1] == 200, "Immerse's separate max HP gain remains scaled exactly once to 200");
        damage.BaseValue = 3; Mode(false);
        await Immerse(); Check(Damage[^1] == 3 && damage.BaseValue == 4,
            "disabled HBR first Immerse retains native 3 damage and raw 4");
        await Immerse(); Check(Damage[^1] == 4 && damage.BaseValue == 5,
            "disabled HBR second Immerse retains native 4 damage and raw 5");
        Mode(true);

        Patch(h, AccessTools.Method(typeof(DeflectorProtection), nameof(DeflectorProtection.GainMaxDp)), nameof(CaptureMaxDp));
        var protection = Relic<DeflectorProtection>();
        Set(Player, "_relics", new List<RelicModel> { Manual, protection });
        var feed = Card<MegaCrit.Sts2.Core.Models.Cards.Feed>();
        decimal scaledGain = Read(feed.DynamicVars.MaxHp);
        object?[] args = [Hero, scaledGain, null];
        bool continueNative = (bool)Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.CreatureCmdGainMaxHpPatch", true)!
            .GetMethod("Prefix", F)!.Invoke(null, args)!;
        Check(!continueNative && RedirectedDp == 300 && scaledGain == 300 && feed.DynamicVars.MaxHp.BaseValue == 3,
            "actual max HP to DP redirection consumes scaled 300 once without a second multiplication");
        Set(Player, "_relics", new List<RelicModel> { Manual });
    }
}
