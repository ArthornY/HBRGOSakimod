using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BaseLib.Config;
using BaseLib.Config.UI;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

static class ForcedNumericsChecks
{
    public static void Run(string path) => Checks.RunForcedNumerics(path);
}

static partial class Checks
{
    sealed class ForcedBinding(string modId) : IModSettingsValueBinding<bool>
    {
        public string ModId => modId;
        public string DataKey => nameof(HBRGOSakiConfig.HbrNumerics);
        public SaveScope Scope => default;
        public void Save() { }
        public bool Read() => HBRGOSakiConfig.HbrNumerics;
        public void Write(bool value) => HBRGOSakiConfig.HbrNumerics = value;
    }
    static int ForcedInfrastructureCalls, ForcedDisableCalls;
    static NClickableControl? ForcedDisabledControl;
    static bool ForcedInfrastructureLeaf() { ForcedInfrastructureCalls++; return false; }
    static bool ForcedDisableLeaf(NClickableControl __instance)
    {
        ForcedDisableCalls++; ForcedDisabledControl = __instance;
        Set(__instance, "_isEnabled", false); return false;
    }
    static Type ForcedPatch(string name) => Mod.GetType("HBRGOSakiMod.Core.Patches." + name, true)!;
    static Type ForcedLifecycle => Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiRunLifecycle", true)!;
    static bool ForcedEnabled(Player player) => (bool)Helper("HbrBattleStatsHelper")
        .GetMethod("IsEnabled", F, null, new[] { typeof(Player) }, null)!.Invoke(null, new object[] { player })!;
    static void ForcedStart(Fixture world)
    {
        Call(ForcedLifecycle, "OnRunStarted", null, new RunStartedEvent(world.Run, false, false, DateTimeOffset.UnixEpoch));
    }
    static void ForcedLoad(Fixture world)
    {
        Call(ForcedLifecycle, "OnRunLoaded", null, new RunLoadedEvent(world.Run, false, false, DateTimeOffset.UnixEpoch));
    }
    static void ForcedModeCases()
    {
        PropertyInfo setting = typeof(HBRGOSakiConfig).GetProperty(nameof(HBRGOSakiConfig.HbrNumerics))!;
        foreach (bool saved in new[] { false, true, false })
        {
            setting.SetValue(null, saved);
            Check(HBRGOSakiConfig.HbrNumerics && (bool)setting.GetValue(null)!,
                "normal reflected config reload ignores a saved " + saved + " and reads forced-on");
        }
        foreach (CharacterModel character in new CharacterModel[] { Blank<HbrgoSaki31O>(), Blank<HbrgoSaki30A>(), Blank<HbrgoHotaru>() })
        {
            HBRGOSakiConfig.HbrNumerics = false;
            Fixture fresh = new(character, enabled: false, current: 117, hotaruBook: character is HbrgoHotaru);
            Check(!fresh.Book.IsHbrNumericsEnabled, "fresh manual is not mistaken for an initialized old-run profile before its explicit start event");
            int maximum = fresh.Hero.MaxHp;
            ForcedStart(fresh);
            Check(fresh.Book.IsHbrNumericsEnabled && fresh.Book.HbrBattleLevel == 1,
                character.GetType().Name + " new-run event starts HBR even after a saved-off config assignment");
            Check(fresh.Hero.MaxHp == maximum && fresh.Hero.CurrentHp == (character is HbrgoHotaru ? 1 : 117),
                character.GetType().Name + " fresh initialization preserves its already-scaled maximum and original current-HP rule");
            Check(ForcedEnabled(fresh.Player),
                character.GetType().Name + " enters the same saved-profile damage/stat calculation route");
            var shield = fresh.Shield();
            Equal(shield.MaxDp, maximum * 2m, "new character DP retains its existing ratio to scaled maximum HP");
            string profile = fresh.Book.HbrBattleProgressData;
            fresh.Hero.SetCurrentHpInternal(73); ForcedStart(fresh);
            Check(fresh.Hero.CurrentHp == 73 && fresh.Hero.MaxHp == maximum && fresh.Book.HbrBattleProgressData == profile,
                "duplicate fresh-run notifications never multiply HP, reset damage or overwrite the chosen HBR profile");
        }
        foreach (bool explicitFalse in new[] { false, true })
        foreach (CharacterModel character in new CharacterModel[] { Blank<HbrgoSaki31O>(), Blank<HbrgoSaki30A>(), Blank<HbrgoHotaru>() })
        {
            Fixture old = new(character, enabled: false, current: 17, hotaruBook: character is HbrgoHotaru);
            old.Hero.SetMaxHpInternal(35); old.Hero.SetCurrentHpInternal(17);
            if (explicitFalse) Data(old.Book, "{\"Schema\":1,\"Enabled\":false,\"HealthScaleApplied\":false,\"Level\":7}");
            var restored = (ElectronicSoldierManual)RelicModel.FromSerializable(old.Book.ToSerializable());
            Set(restored, "_owner", old.Player); Set(old.Player, "_relics", new List<RelicModel> { restored });
            string data = restored.HbrBattleProgressData;
            HBRGOSakiConfig.HbrNumerics = false;
            ForcedLoad(old);
            Check(!restored.IsHbrNumericsEnabled && restored.HbrBattleLevel == 0 && restored.HbrBattleProgressData == data,
                character.GetType().Name + " old-run load preserves " + (explicitFalse ? "explicit disabled" : "missing legacy") + " profile through native relic serialization");
            Check(old.Hero.MaxHp == 35 && old.Hero.CurrentHp == 17,
                "forcing the new-run setting never rescales or heals an in-progress legacy run");
            Check(!ForcedEnabled(old.Player),
                "existing legacy profiles retain the old damage/stat route despite the forced config getter");
        }
        Fixture enabled = new(Blank<HbrgoSaki31O>());
        enabled.Hero.SetMaxHpInternal(1750); enabled.Hero.SetCurrentHpInternal(555);
        string enabledProfile = enabled.Book.HbrBattleProgressData;
        ForcedLoad(enabled);
        Check(enabled.Book.IsHbrNumericsEnabled && enabled.Book.HbrBattleProgressData == enabledProfile &&
            enabled.Hero.MaxHp == 1750 && enabled.Hero.CurrentHp == 555,
            "loading an existing HBR run also retains its saved mode and stored historical HP scale");
        Fixture party = new(Blank<HbrgoSaki31O>(), enabled: false);
        Fixture native = new(Blank<Ironclad>(), enabled: false, current: 37);
        ((List<Player>)Field(party.Run.GetType(), "_players").GetValue(party.Run)!).Add(native.Player);
        ForcedStart(party);
        Check(native.Book.IsHbrNumericsEnabled && native.Hero.MaxHp == 8000 && native.Hero.CurrentHp == 3700,
            "a new mixed party gives its native teammate matching HBR numerics and scales native HP once");
        ForcedStart(party);
        Check(native.Hero.MaxHp == 8000 && native.Hero.CurrentHp == 3700,
            "repeated mixed-party starts do not scale native teammates again");
    }

    static void ForcedSettingsCases(Harmony harmony, List<MethodBase> patched)
    {
        var config = Blank<HBRGOSakiConfig>();
        PropertyInfo forced = typeof(HBRGOSakiConfig).GetProperty(nameof(HBRGOSakiConfig.HbrNumerics))!;
        PropertyInfo ordinary = typeof(HBRGOSakiConfig).GetProperty(nameof(HBRGOSakiConfig.ModUI))!;
        Type policy = ForcedPatch("HbrNumericsSettingsLock");
        Check((bool)Call(policy, "IsForcedProperty", null, config, forced)! &&
            !(bool)Call(policy, "IsForcedProperty", null, config, ordinary)!,
            "BaseLib lock policy selects only this mod's forced numerics property");
        MethodInfo readyPostfix = ForcedPatch("HbrNumericsBaseLibSettingsLockPatch").GetMethod("Postfix", F)!;
        CodeInstruction[] readyBody = PatchProcessor.GetOriginalInstructions(readyPostfix).ToArray();
        Check(readyBody.Any(instruction => instruction.operand is MethodInfo method && method.Name == "IsForcedProperty") &&
            readyBody.Any(instruction => instruction.operand is MethodInfo method && method.DeclaringType == typeof(NClickableControl) && method.Name == "Disable"),
            "the production BaseLib Ready postfix gates the native Disable call through the exact forced-property policy");

        Type ritsuPatch = ForcedPatch("HbrNumericsRitsuSettingsLockPatch");
        harmony.CreateClassProcessor(ritsuPatch).Patch();
        patched.Add(AccessTools.PropertyGetter(typeof(ModSettingsEntryDefinition), nameof(ModSettingsEntryDefinition.EnabledPredicate)));
        foreach ((string modId, string id, bool locked) in new[]
        { ("HBRGOSakiMod", "bl_HBR_NUMERICS", true), ("hbrgosakimod", "bl_HBR_NUMERICS", true),
          ("AnotherMod", "bl_HBR_NUMERICS", false), ("HBRGOSakiMod", "bl_MOD_U_I", false) })
        {
            var entry = new ToggleModSettingsEntryDefinition(id, ModSettingsText.Literal("numeric setting"), new ForcedBinding(modId), null);
            Check(entry.EnabledPredicate?.Invoke() == (locked ? (bool?)false : null),
                "native Ritsu mirrored entry enabled predicate locks only the matching mod binding and key (" + modId + "/" + id + ")");
            entry.Binding.Write(false);
            Check(entry.Binding.Read(), "mirrored binding writes cannot switch the forced new-run policy off");
        }
    }

    public static void RunForcedNumerics(string path)
    {
        bool ok = false; string? error = null;
        var harmony = new Harmony("hbr.forced.numerics.native.checks"); var patched = new List<MethodBase>();
        try
        {
            Install();
            MethodInfo infrastructure = ForcedLifecycle.GetMethod("EnsureRollInfrastructure", F)!;
            harmony.Patch(infrastructure, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(ForcedInfrastructureLeaf), F)!)); patched.Add(infrastructure);
            ForcedModeCases(); ForcedSettingsCases(harmony, patched); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        finally
        {
            foreach (MethodBase method in patched.Distinct()) harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error, infrastructureEvents = ForcedInfrastructureCalls,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual config getter/setter, real fresh/load lifecycle callback bodies, actual model HP/DP initialization and native relic serialization, saved-profile damage/stat selection, BaseLib lock-policy and production IL wiring to Disable, and actual native Ritsu enabled-predicate patch. Existing manuals supply unrelated run infrastructure. Godot-facing NClickableControl/Disable is statically inspected rather than instantiated. No Godot, rendered settings menu, full save file/config disk IO, networking or packaging."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " forced-numerics checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
