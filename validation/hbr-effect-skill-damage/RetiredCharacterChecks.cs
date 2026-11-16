using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Saves;

// The base 人造救世主 (HBRGO_SAKI) left the game on 2026-11-09 (user: 「话说游戏里还保留着的
// 人造救世主角色删掉吧一起」). It stays registered for old saves and run history; 31O and 30A
// keep inheriting from it. The call sites that still listed or counted it now skip it.
static partial class EffectChecks
{
    static Type RetiredHelper => Mod.GetType("HBRGOSakiMod.Core.Patches.HbrgoRetiredCharacters", true)!;

    static object? Retired(string method, params object[] args) =>
        Invoke(RetiredHelper.GetMethods(F).Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
            m.GetParameters().Zip(args).All(pair => pair.First.ParameterType.IsInstanceOfType(pair.Second))), null, args);

    static IEnumerable<CharacterModel>? _retiredStubCharacters;

    static bool RetiredStubAllCharacters(ref IEnumerable<CharacterModel> __result)
    {
        if (_retiredStubCharacters == null) return true;
        __result = _retiredStubCharacters;
        return false;
    }

    static void RetiredCharacterCases()
    {
        // Who is retired: only the base class itself, not 31O and 30A that inherit from it.
        HbrgoSaki saviour = Blank<HbrgoSaki>();
        HbrgoSaki31O thirtyOneO = Blank<HbrgoSaki31O>();
        HbrgoSaki30A thirtyA = Blank<HbrgoSaki30A>();
        HbrgoHotaru hotaru = Blank<HbrgoHotaru>();
        Ironclad ironclad = Blank<Ironclad>();
        Check((bool)Retired("IsRetired", saviour)! && !(bool)Retired("IsRetired", thirtyOneO)! &&
              !(bool)Retired("IsRetired", thirtyA)! && !(bool)Retired("IsRetired", hotaru)! &&
              !(bool)Retired("IsRetired", ironclad)!,
            "only the base 人造救世主 is retired; 31O and 30A, which inherit from it, Hotaru and Ironclad are not");
        ModelId saviourId = ModelDb.GetId(typeof(HbrgoSaki));
        Check(saviourId.Entry == "HBRGO_SAKI" && (bool)Retired("IsRetired", saviourId)! &&
              !(bool)Retired("IsRetired", ModelDb.GetId(typeof(HbrgoSaki31O)))! &&
              !(bool)Retired("IsRetired", ModelDb.GetId(typeof(HbrgoSaki30A)))!,
            "the retired id is HBRGO_SAKI; HBRGO_SAKI_31O and HBRGO_SAKI_30A are not retired");

        // It stays registered and hidden as before, and keeps its texts for old saves.
        Check(!typeof(HbrgoSaki).IsAbstract && saviour.HideFromVanillaCharacterSelect &&
              !saviour.AllowInVanillaRandomCharacterSelect && saviour.HideInCompendium && saviour.HideInCardLibraryCompendium &&
              !thirtyOneO.HideFromVanillaCharacterSelect && thirtyOneO.AllowInVanillaRandomCharacterSelect &&
              !thirtyOneO.HideInCompendium && !thirtyOneO.HideInCardLibraryCompendium,
            "人造救世主 stays a concrete, hidden character class; 31O stays selectable and in the library");
        using (JsonDocument characters = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,
                   "HBRGOSakiMod/localization/zhs/characters.json"))))
        {
            JsonElement root = characters.RootElement;
            Check(root.TryGetProperty("HBRGO_SAKI.title", out JsonElement title) && title.GetString() == "人造救世主" &&
                  root.TryGetProperty("HBRGO_SAKI.cardsModifierTitle", out _) &&
                  root.TryGetProperty("HBRGO_SAKI.cardsModifierDescription", out _),
                "the HBRGO_SAKI texts stay, so old saves and an old 「人造救世主卡牌」 modifier still read");
        }

        // The filtered list and the stats count.
        var list = new CharacterModel[] { ironclad, saviour, thirtyOneO, thirtyA, hotaru };
        CharacterModel[] kept = ((IEnumerable<CharacterModel>)Retired("WithoutRetired", (object)list)!).ToArray();
        Check(kept.SequenceEqual(new CharacterModel[] { ironclad, thirtyOneO, thirtyA, hotaru }),
            "the filtered character list drops 人造救世主 and keeps the others in order");
        var stats = new Dictionary<ModelId, CharacterStats>
        {
            [saviourId] = null!, [ModelDb.GetId(typeof(HbrgoSaki31O))] = null!, [ModelDb.GetId(typeof(Ironclad))] = null!
        };
        Check((int)Retired("CountStatsWithoutRetired", stats)! == 2 &&
              (int)Retired("CountStatsWithoutRetired", new Dictionary<ModelId, CharacterStats>())! == 0,
            "stats left by old 人造救世主 runs do not count as a character played (3 entries count as 2)");

        // SelectableCharacters reads the real ModelDb.AllCharacters (stubbed here).
        var harmony = new Harmony(ProductionId + ".retired-character");
        MethodInfo allCharacters = AccessTools.PropertyGetter(typeof(ModelDb), nameof(ModelDb.AllCharacters));
        harmony.Patch(allCharacters, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RetiredStubAllCharacters), F)!)
            { priority = Priority.First });
        try
        {
            _retiredStubCharacters = list;
            CharacterModel[] selectable = ((IEnumerable<CharacterModel>)Retired("SelectableCharacters")!).ToArray();
            Check(ModelDb.AllCharacters.Count() == 5 && selectable.Length == 4 && !selectable.Contains(saviour),
                "SelectableCharacters is ModelDb.AllCharacters without 人造救世主 (5 characters give 4)");
        }
        finally
        {
            _retiredStubCharacters = null;
            harmony.Unpatch(allCharacters, HarmonyPatchType.Prefix, harmony.Id);
        }

        // The call sites: every read of ModelDb.AllCharacters there now reads the filtered list.
        Type patchType = Mod.GetType("HBRGOSakiMod.Core.Patches.RetiredArtificialSaviorCallSitePatch", true)!;
        MethodBase[] targets = ((IEnumerable<MethodBase>)Invoke(patchType.GetMethod("TargetMethods", F)!, null)!).ToArray();
        bool stableDaily = AccessTools.DeclaredMethod(typeof(NDailyRunScreen), "RollModifiers") != null;
        bool betaPick = AccessTools.DeclaredMethod(typeof(ModifierModel), "Pick2Good1Bad") != null;
        Check(stableDaily != betaPick,
            "the game has exactly one of the daily modifier rolls: NDailyRunScreen.RollModifiers (stable) or ModifierModel.Pick2Good1Bad (v0.111.0)");
        string[] expected =
        {
            "NCustomRunScreen.InitCharacterButtons", "NCustomRunModifiersList.GetAllModifiers.MoveNext",
            "NDailyRunScreen.SetupLobbyParams", stableDaily ? "NDailyRunScreen.RollModifiers" : "ModifierModel.Pick2Good1Bad",
            "Orobas.get_SeaGlassOptions", "Kaleidoscope.IsAllowedAtNeow", "ProgressSaveManager.UpdateEpochsPostRun"
        };
        string Name(MethodBase method) =>
            method.Name == "MoveNext" && method.DeclaringType?.DeclaringType is { } outer
                ? $"{outer.Name}.{(method.DeclaringType.Name.Split('>')[0].TrimStart('<'))}.MoveNext"
                : $"{method.DeclaringType?.Name}.{method.Name}";
        string[] actual = targets.Select(Name).ToArray();
        Check(actual.SequenceEqual(expected), "the call-site patch targets: " + string.Join(", ", actual));

        MethodInfo selectableCharacters = RetiredHelper.GetMethod("SelectableCharacters", F)!;
        MethodInfo countStats = RetiredHelper.GetMethod("CountStatsWithoutRetired", F)!;
        MethodInfo statsCount = AccessTools.PropertyGetter(typeof(IReadOnlyCollection<KeyValuePair<ModelId, CharacterStats>>), "Count");
        var originals = targets.ToDictionary(target => target,
            target => PatchProcessor.GetOriginalInstructions(target).Count(instruction => instruction.Calls(allCharacters)));
        var originalStatsCounts = targets.ToDictionary(target => target,
            target => PatchProcessor.GetOriginalInstructions(target).Count(instruction => instruction.Calls(statsCount)));
        var installer = new Harmony(ProductionId + ".retired-character-sites");
        try
        {
            Check(installer.CreateClassProcessor(patchType).Patch().Count == expected.Length,
                $"installs the production call-site patch on all {expected.Length} targets");
            foreach (MethodBase target in targets)
            {
                List<CodeInstruction> code = PatchProcessor.GetCurrentInstructions(target);
                int reads = code.Count(instruction => instruction.Calls(allCharacters));
                int filtered = code.Count(instruction => instruction.Calls(selectableCharacters));
                int counts = code.Count(instruction => instruction.Calls(countStats));
                int rawCounts = code.Count(instruction => instruction.Calls(statsCount));
                Cases.Add(new { source = "Retired character call site", target = Name(target), originalReads = originals[target], originalStatsCounts = originalStatsCounts[target], reads, filtered, counts, rawCounts });
                Check(originals[target] > 0 && reads == 0 && filtered == originals[target],
                    $"{Name(target)}: its {originals[target]} read(s) of ModelDb.AllCharacters now skip 人造救世主");
                if (target.Name == "UpdateEpochsPostRun")
                    // 0.113 filters a materialized character-ID set first; its
                    // epoch method no longer has the two old dictionary Count calls.
                    Check(counts == originalStatsCounts[target] && rawCounts == 0,
                        $"all {originalStatsCounts[target]} native CharacterStats.Count gate reads exclude 人造救世主's stats");
            }
        }
        finally
        {
            installer.UnpatchAll(installer.Id);
        }

        // Cards only 人造救世主 could get: in neither the 31O nor the 30A pool; the classes stay.
        Type filter = Helper("HbrgoSaki31OCardFilter");
        bool Filter(string method, Type card) => (bool)Invoke(filter.GetMethod(method, F, new[] { typeof(Type) })!, null, card)!;
        Type[] onlySaviour =
        {
            typeof(SoulIntimidationHbrgoSaki), typeof(CollectSoulHbrgoSaki), typeof(SoulBufferHbrgoSaki),
            typeof(RedCrimsonExtraStrikeHbrgoSaki), typeof(PowerReleaseHbrgoSaki), typeof(SoulHarvestHbrgoSaki),
            typeof(SoulExplosionHbrgoSaki), typeof(ExtraGaugeStrikeHbrgoSaki), typeof(ArmorBreakSoulStrikeHbrgoSaki),
            typeof(SoulFinisherHbrgoSaki), typeof(DpSoulStrikeHbrgoSaki), typeof(MimicEvilEyeTidalSlashHbrgoSaki),
            typeof(ReapHbrgoSaki), typeof(DamageTransferHbrgoSaki)
        };
        using JsonDocument cards = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs/cards.json")));
        foreach (Type card in onlySaviour)
        {
            string entry = ModelDb.GetId(card).Entry;
            Check(!card.IsAbstract && Filter("IsExcluded", card) && !Filter("IsThirtyAAllowed", card) &&
                  !Filter("IsThirtyOneOExclusive", card) && cards.RootElement.TryGetProperty(entry + ".title", out _),
                $"{entry} stays (class and text) and is in neither the 31O nor the 30A card pool");
        }
        var retiredCards = (IReadOnlySet<Type>)Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack", true)!
            .GetField("RetiredCardTypes", F)!.GetValue(null)!;
        Check(retiredCards.Contains(typeof(MimicEvilEyeTidalSlashHbrgoSaki)),
            "模仿-邪眼海潮斬 was already retired to the deprecated pool with the six-sense resistances");

        // Old 人造救世主 runs: the card library shows them the 31O row instead of failing its lookup.
        Type variants = Mod.GetType("HBRGOSakiMod.Core.Integration.HbrgoSakiVariantPreference", true)!;
        string Variant(CharacterModel character) => Invoke(variants.GetMethod("FromCharacter", F)!, null, character)!.ToString()!;
        Check(Variant(saviour) == "ThirtyOneO" && Variant(thirtyOneO) == "ThirtyOneO" && Variant(thirtyA) == "ThirtyA",
            "an old 人造救世主 run opens the library on 31O; 31O and 30A map to themselves");
        string library = File.ReadAllText(Path.Combine(RepositoryRoot, "Core/Patches/NCardLibraryPatch.cs"));
        Check(library.Contains("cardPoolFilters.TryAdd(ModelDb.Character<HbrgoSaki>(), visibleSakiFilter);"),
            "the card library maps 人造救世主, which has no filter of its own, to the shared Saki filter");
        string unlock = File.ReadAllText(Path.Combine(RepositoryRoot, "Core/Patches/HiddenArtificialSaviorUnlockPatch.cs"));
        Check(unlock.Contains("HbrgoRetiredCharacters.WithoutRetired(__result)"),
            "the unlocked characters still leave out 人造救世主, through the same filter");
    }
}
