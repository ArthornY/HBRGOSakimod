using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

static partial class Checks
{
    static Type Entry => Mod.GetType("HBRGOSakiMod.Core.Models.Relics.PhantomRosterEntry", true)!;
    static object EntryValue(int id = 1, decimal inheritedHp = 0, string fusion = "") =>
        Entry.GetConstructors(F).Single(constructor => constructor.GetParameters().Length == 10)
            .Invoke([id, Canonical(typeof(SmallHopper)).Id, 0, 0, 1, 0m, 0m, inheritedHp, fusion, 0m]);
    static object Levels(object entry, int amount) => Call(Entry, "AddLevels", entry, amount)!;
    static decimal EntryNumber(object entry, string name) => (decimal)Entry.GetProperty(name, F)!.GetValue(entry)!;
    static Array Entries(params object[] entries)
    {
        Array result = Array.CreateInstance(Entry, entries.Length);
        for (int i = 0; i < entries.Length; i++) result.SetValue(entries[i], i);
        return result;
    }
    static bool HpGainLeaf(Creature creature, decimal amount, ref Task __result)
    {
        creature.SetMaxHpInternal(creature.MaxHp + amount);
        creature.SetCurrentHpInternal(Math.Min(creature.MaxHp, creature.CurrentHp + amount));
        __result = Task.CompletedTask; return false;
    }
    static bool HpSetLeaf(Creature creature, decimal amount, ref Task __result)
    {
        creature.SetMaxHpInternal(amount); __result = Task.CompletedTask; return false;
    }
    static bool TaskLeaf(ref Task __result) { __result = Task.CompletedTask; return false; }
    static void AddPower(Creature creature, PowerModel power)
    {
        Set(power, "<IsMutable>k__BackingField", true); Set(power, "_owner", creature);
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(creature)!).Add(power);
    }
    static void PhantomGrowthCases()
    {
        Fixture enabled = new(), old = new(enabled: false);
        object initial = EntryValue(), level2 = Levels(initial, 1);
        Equal(EntryNumber(level2, "BonusMaxHp"), 5, "phantom level metadata retains raw5HP");
        Equal((decimal)Call(Entry, "GetBonusMaxHp", level2, enabled.Player)!, 500, "phantom level2 resolves raw5 into500HP");
        Equal((decimal)Call(Entry, "GetBonusMaxHp", level2, old.Player)!, 5, "legacy phantom level2 resolves raw5 unchanged");
        Type fusion = Helper("PhantomFusionEffectHelper");
        ModelId leaf = Canonical(typeof(LeafSlimeS)).Id;
        string once = (string)Call(fusion, "AddDirectEffect", null, "", leaf)!;
        string twice = (string)Call(fusion, "AddDirectEffect", null, once, leaf)!;
        object first = EntryValue(fusion: once), second = EntryValue(fusion: twice);
        Equal(EntryNumber(first, "BonusMaxHp"), 2, "first leaf fusion persists raw2HP");
        Equal(EntryNumber(second, "BonusMaxHp"), 3, "second leaf fusion adds raw1 without multiplying stored metadata");
        Equal((decimal)Call(Entry, "GetBonusMaxHp", first, enabled.Player)!, 200, "first leaf fusion resolves200HP");
        Equal((decimal)Call(Entry, "GetBonusMaxHp", second, enabled.Player)!, 300, "second leaf fusion resolves300HP rather than scaling prior result again");
        Equal((decimal)Call(Entry, "GetBonusMaxHp", second, enabled.Player)! -
            (decimal)Call(Entry, "GetBonusMaxHp", first, enabled.Player)!, 100, "fusion actual growth difference is100HP");
        string hpDescription = string.Join(" ", (IReadOnlyList<string>)Call(fusion, "GetDescriptionLines", null, twice, enabled.Hero)!);
        string oldDescription = string.Join(" ", (IReadOnlyList<string>)Call(fusion, "GetDescriptionLines", null, twice, old.Hero)!);
        Check(hpDescription.Contains("最大生命值+300") && oldDescription.Contains("最大生命值+3"), "fusion descriptions resolve owner units without mutating raw data");
        string hopper = (string)Call(fusion, "AddDirectEffect", null, "", Canonical(typeof(SmallHopper)).Id)!;
        object dpEntry = EntryValue(fusion: hopper);
        Equal(EntryNumber(dpEntry, "ExtraMaxDp"), 1, "fusion DP metadata remains raw1");
        Equal((decimal)Call(Entry, "GetExtraMaxDp", dpEntry, enabled.Player)!, 100, "fusion DP preview resolves100");
        Creature minion = enabled.Phantom();
        Call(Helper("PhantomHelper"), "ConfigureSummonedMinionHealth", null, minion, 3, enabled.Player);
        Equal(minion.MaxHp, 5000, "summoned minion level3 adds two fixed500HP increments");
        Call(Helper("PhantomHelper"), "ConfigureSummonedMinionHealth", null, minion, 3, enabled.Player);
        Equal(minion.MaxHp, 5000, "repeated level3 minion setup does not multiply bonus or base again");

        MomentOfReign reign = Relic<MomentOfReign>(enabled.Player);
        ((List<RelicModel>)Field(typeof(MegaCrit.Sts2.Core.Entities.Players.Player), "_relics").GetValue(enabled.Player)!).Add(reign);
        Call(typeof(MomentOfReign), "SetRosterEntries", reign, Entries(dpEntry));
        typeof(MomentOfReign).GetProperty("ActiveRosterEntryId", F)!.SetValue(reign, 1);
        Creature shellOwner = enabled.Phantom(40); shellOwner.SetMaxHpInternal(4000);
        AddPower(shellOwner, Blank<PhantomPower>());
        Equal((decimal)Call(typeof(PhantomHighDimensionalShellPower), "CalculateInitialAmount", null, shellOwner)!, 900,
            "actual phantom shell combines800 proportional DP with fixed100 fusionDP once");
        ((List<Creature>)Field(typeof(MegaCrit.Sts2.Core.Combat.CombatState), "_allies").GetValue(enabled.Combat)!).Remove(shellOwner);
        typeof(MomentOfReign).GetProperty("PhantomBaseMaxHpData", F)!.SetValue(reign, "{\"1\":4000}");
        var health = new Dictionary<int, decimal> { [1] = 1000 };
        Call(typeof(MomentOfReign), "RestoreStoredHpFromRosterGrowth", reign, Entries(initial), Entries(level2), health);
        Equal(health[1], 1500, "off-field stored HP growth adds actual500 rather than raw5");
        object level3 = Levels(level2, 1);
        Call(typeof(MomentOfReign), "RestoreStoredHpFromRosterGrowth", reign, Entries(level2), Entries(level3), health);
        Equal(health[1], 2000, "second off-field level adds another500 without rescaling saved HP");
        Call(typeof(MomentOfReign), "RestoreStoredHpFromRosterGrowth", reign, Entries(level3), Entries(level3), health);
        Equal(health[1], 2000, "same roster metadata does not grant duplicate stored healing");
        health[1] = 0;
        Call(typeof(MomentOfReign), "RestoreStoredHpFromRosterGrowth", reign, Entries(level2), Entries(level3), health);
        Equal(health[1], 0, "fixed level growth does not revive stored defeated phantom");

        // The terminal command work is a controlled leaf. Native setters and
        // production temporary bonus ledger, difference and expiry run intact.
        Harmony harmony = new("hbr.resource.scale.phantom.commands");
        harmony.Patch(typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.GainMaxHp))!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(HpGainLeaf), F)!));
        harmony.Patch(typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.SetMaxHp))!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(HpSetLeaf), F)!));
        harmony.Patch(Helper("PhantomHelper").GetMethod("RestoreDp", F)!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(TaskLeaf), F)!));
        foreach (string name in new[] { "RefreshCreatureIntent", "RefreshOpponentIntents" })
            harmony.Patch(Helper("CombatPreviewRefreshHelper").GetMethod(name, F)!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(TaskLeaf), F)!));
        Creature phantom = enabled.Phantom(40); phantom.SetMaxHpInternal(4000); phantom.SetCurrentHpInternal(3500);
        AddPower(phantom, Blank<PhantomPower>());
        var temporary = Blank<PhantomTemporaryLevelPower>(); AddPower(phantom, temporary); Set(temporary, "_amount", 1);
        ((Task)Call(typeof(PhantomTemporaryLevelPower), "RefreshStats", temporary)!).GetAwaiter().GetResult();
        Equal(phantom.MaxHp, 4500, "one temporary doubling at baselevel1 grants500 actualHP");
        Equal((decimal)typeof(PhantomTemporaryLevelPower).GetProperty("ExactAppliedBonusMaxHp", F)!.GetValue(temporary)!, 500, "temporary saved ledger stores actual500 rather than raw5");
        ((Task)Call(typeof(PhantomTemporaryLevelPower), "RefreshStats", temporary)!).GetAwaiter().GetResult();
        Equal(phantom.MaxHp, 4500, "duplicate temporary refresh does not reapply500");
        Set(temporary, "_amount", 2);
        ((Task)Call(typeof(PhantomTemporaryLevelPower), "RefreshStats", temporary)!).GetAwaiter().GetResult();
        Equal(phantom.MaxHp, 5500, "second doubling applies only additional1000HP");
        temporary.AfterRemoved(phantom).GetAwaiter().GetResult();
        Equal(phantom.MaxHp, 4000, "temporary expiry removes actual1500 without multiplying ledger again");
        temporary.AfterRemoved(phantom).GetAwaiter().GetResult();
        Equal(phantom.MaxHp, 4000, "temporary expiry is idempotent");
    }
}
