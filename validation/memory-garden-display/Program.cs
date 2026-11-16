using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

static class GardenDisplayStandalone { public static void Run(string path) => EffectChecks.RunGardenDisplay(path); }

static partial class EffectChecks
{
    static readonly List<object> GardenDisplaySamples = [];
    static object GardenDisplayCall(string method, params object?[] args) => Call("HbrMemoryGarden", method, args)!;
    static CardModel[] GardenGhosts(object choices) => ((IEnumerable)GardenDisplayCall("GetUnmetCards", choices)).Cast<CardModel>().ToArray();
    static CardModel GardenPreview(object choice) => (CardModel)choice.GetType().GetProperty("PreviewCard", F)!.GetValue(choice)!;
    static object EmptyGardenChoices() => Array.CreateInstance(Mod.GetType("HBRGOSakiMod.Core.Helpers.MemoryGardenChoice", true)!, 0);
    static string GardenRawState(CardModel card) => JsonSerializer.Serialize(new
    {
        mutable = card.IsMutable,
        variables = card.DynamicVars.Values.Select(value => new { value.Name, value.BaseValue }).ToArray(),
        saved = card.GetType().GetProperties(F).Where(property => property.GetIndexParameters().Length == 0 &&
            property.GetCustomAttributes().Any(attribute => attribute.GetType().Name is "SavedProperty" or "SavedPropertyAttribute"))
            .ToDictionary(property => property.Name, property => property.GetValue(card))
    });
    static string GardenManualState(ElectronicSoldierManual manual) => JsonSerializer.Serialize(new
    {
        manual.MemoryGardenMetCharacters, manual.MemoryGardenUpgradePoints, manual.MemoryGardenSixSenseUpgradeTier,
        manual.MemoryGardenSixSenseRuleVersion, manual.MemoryGardenInitialDeckSize, manual.MemoryGardenDeckHighWater,
        manual.MemoryGardenDeckUpgradeTier, manual.MemoryGardenDeckRuleVersion, manual.MemoryGardenSharedUpgrades,
        manual.MemoryGardenObtainedCardMask, manual.MemoryGardenRepeatRewardClaims, manual.MemoryGardenUpgradeRewardedRooms,
        manual.SixSensePoints, manual.LeadershipCharm, manual.Talent, manual.Gentleness, manual.MentalStrength,
        manual.Simplicity, manual.Madness, manual.HbrBattleProgressData
    });

    static void GardenDisplaySetup()
    {
        H("Setup"); Localization();
        foreach (Type type in Mod.GetTypes().Concat(typeof(CardModel).Assembly.GetTypes()).Where(type => !type.IsAbstract && !type.ContainsGenericParameters &&
            ((typeof(CardModel).IsAssignableFrom(type) && type.Assembly == Mod) || typeof(PowerModel).IsAssignableFrom(type) || typeof(RelicModel).IsAssignableFrom(type) || typeof(CardPoolModel).IsAssignableFrom(type))))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony("hbr.memory-garden.display.native");
        foreach (string patch in new[] { "CardDescriptionLocPatch", "ThirtyOneBCardDescriptionPatch", "HbrCardFaceRulesHoverPatch" })
            harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + patch, true)!).Patch();
        foreach (MethodInfo method in typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!.GetMethods(F).Where(method => method.Name is "GetPrefix" or "GetPath" && method.ReturnType == typeof(string)))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Energy), F)!));
        harmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(CostLive), F)!));
        Reset(); CombatInProgress(false);
        Set(RunState, "_allCards", new List<CardModel>());
        Set(Combat, "_allCards", new List<CardModel>());
        var playerCombat = Blank<PlayerCombatState>();
        Set(playerCombat, "_player", Player);
        Set(playerCombat, "_pets", new List<MegaCrit.Sts2.Core.Entities.Creatures.Creature>());
        Set(playerCombat, "<TurnNumber>k__BackingField", 1);
        Set(Player, "<PlayerCombatState>k__BackingField", playerCombat);
        foreach (var row in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            Set(playerCombat, "<" + row.Item1 + ">k__BackingField", new CardPile(row.Item2));
        Set(Player, "<Deck>k__BackingField", new CardPile(PileType.Deck));
        Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki31O>());
        var manual = Player.GetRelic<ElectronicSoldierManual>()!;
        Set(manual, "<Id>k__BackingField", ModelDb.GetId(manual.GetType()));
        Native(manual, "SetMemoryGardenEncounters", "");
        Native(manual, "SynchronizeMemoryGardenUpgradePoints");
    }

    static void GardenReadVisualInputs(CardModel card, string label)
    {
        Check(card.IsMutable, label + " supplies a mutable display model");
        // Read the actual native NCard visual inputs. Owner is mutable-only;
        // CurrentTarget is also read by both native and display-scope code.
        var target = card.CurrentTarget;
        var owner = card.Owner;
        bool x = card.EnergyCost.CostsX;
        int energy = x ? 0 : card.EnergyCost.GetWithModifiers(CostModifiers.All);
        int stars = card.GetStarCostWithModifiers();
        object? titleValue = typeof(CardModel).GetProperty("Title", F)!.GetValue(card);
        string title = titleValue is LocString loc ? loc.GetFormattedText() : titleValue?.ToString() ?? "";
        Check(title.Length > 0, label + " resolves the native title");
        card.DynamicVars.ClearPreview();
        card.UpdateDynamicVarPreview(CardPreviewMode.Normal, target, card.DynamicVars);
        string description = card.GetDescriptionForPile(PileType.None, target);
        Check(!string.IsNullOrWhiteSpace(description) && !description.Contains("If you can read this"), label + " resolves the actual native description instead of the bug placeholder");
        Check(card.DynamicVars.Values.All(variable => variable != null), label + " resolves its real dynamic variables");
        GardenDisplaySamples.Add(new { label, model = card.GetType().Name, hasOwner = owner != null, x, energy, stars, title, description });
    }

    static void GardenGhostGuards(CardModel[] ghosts)
    {
        Type screenType = Mod.GetType("HBRGOSakiMod.Core.Nodes.Screens.NMemoryGardenScreen", true)!;
        object screen = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(screenType);
        GC.SuppressFinalize(screen);
        Set(screen, "_ghosts", ghosts.ToHashSet()); Set(screen, "_cards", ghosts.ToList());
        // Missing grid/backdrop is deliberate: crossing the real ghost guard
        // would either select the card or touch a forbidden UI terminal.
        foreach (CardModel ghost in ghosts)
        {
            NCard node = Blank<NCard>(); Set(node, "_model", ghost);
            NGridCardHolder holder = Blank<NGridCardHolder>(); Set(holder, "<CardNode>k__BackingField", node); Set(holder, "_baseCard", ghost);
            Invoke(screenType.GetMethod("SelectCard", F)!, screen, holder);
            Invoke(screenType.GetMethod("InspectCard", F)!, screen, holder);
            Check(screenType.GetField("_selected", F)!.GetValue(screen) == null, ghost.GetType().Name + " remains unselectable/uninspectable through the actual garden callbacks");
        }
        string source = File.ReadAllText(Path.Combine(RepositoryRoot, "Core/Nodes/Screens/NMemoryGardenScreen.cs"));
        Check(source.Contains("holder.Hitbox.SetEnabled(_pointerInteracted && !ghost)") && source.Contains("holder.FocusMode = ghost ? FocusModeEnum.None"), "existing ghost pointer/focus seal policy is retained");
    }

    static void GardenDisplayCases()
    {
        Type firstType = typeof(HerSongIsMyScreamHbrgoSaki);
        CardModel firstCanonical = ModelDb.GetById<CardModel>(ModelDb.GetId(firstType));
        Exception? oldFailure = null;
        try { _ = firstCanonical.Owner; } catch (Exception error) { oldFailure = error; }
        Check(oldFailure?.GetType().Name == "CanonicalModelException", "original canonical Taki input genuinely reproduces native mutable-only Owner failure used by visual cost/description paths");
        CardModel[] ghosts = GardenGhosts(EmptyGardenChoices());
        Check(ghosts.Length == 56 && ghosts.Select(card => card.GetType()).Distinct().Count() == 56, "actual GetUnmetCards returns all 56 distinct garden characters when none are met");
        Check(ghosts.All(card => card.IsMutable && card.Owner == null && card.Pile == null && card.DeckVersion == null), "unmet copies are isolated ownerless previews, not game/deck entities");
        var canonicals = ghosts.Select(card => ModelDb.GetById<CardModel>(card.Id)).ToArray();
        var before = canonicals.Select(GardenRawState).ToArray();
        var second = GardenGhosts(EmptyGardenChoices());
        Check(ghosts.All(card => !ReferenceEquals(card, ModelDb.GetById<CardModel>(card.Id))) && ghosts.Zip(second).All(pair => !ReferenceEquals(pair.First, pair.Second)), "each opening receives separate copies while each screen retains its own ghost identities");
        foreach (CardModel card in ghosts) GardenReadVisualInputs(card, "unmet/" + card.GetType().Name);
        GardenGhostGuards(ghosts);
        Check(canonicals.Select(GardenRawState).SequenceEqual(before), "render-input reads and ghost callbacks leave all canonical variables/saved state untouched");

        var manual = Player.GetRelic<ElectronicSoldierManual>()!;
        string[] metIds = ["taki", "rana", "tomori", "anon", "soyo", "mutsumi", "ruka", "yuki"];
        Native(manual, "SetMemoryGardenEncounters", string.Join(',', metIds));
        Native(manual, "AddMemoryGardenUpgradePoints", 3);
        CardModel owned = ModelDb.Card<MyLegendBeginsHbrgoSaki>().ToMutable(); Set(owned, "_owner", Player); Player.Deck.AddInternal(owned); ((List<CardModel>)typeof(MegaCrit.Sts2.Core.Runs.RunState).GetField("_allCards", F)!.GetValue(RunState)!).Add(owned);
        Native(manual, "SynchronizeMemoryGardenUpgradePoints");
        _ = GardenDisplayCall("GetChoices", Player); // settle pre-existing progression synchronization before snapshot
        string savedManual = GardenManualState(manual), savedOwned = GardenRawState(owned);
        var savedDeck = Player.Deck.Cards.ToArray();
        object met = GardenDisplayCall("GetChoices", Player);
        object[] choices = ((IEnumerable)met).Cast<object>().ToArray();
        var hidden = GardenGhosts(met);
        Check(choices.Length == 8 && hidden.Length == 48, "screenshot configuration resolves actual 8 met and 48 unmet entries");
        Check(choices.Select(GardenPreview).All(card => card.IsMutable && ReferenceEquals(card.Owner, Player)), "all met choices retain the existing owner-aware mutable preview path");
        Check(choices.Select(GardenPreview).Concat(hidden).Select(card => card.GetType()).Distinct().Count() == 56, "met and ghost display sets partition the complete catalog without duplicates");
        Check(!choices.Select(GardenPreview).Any(hidden.ToHashSet().Contains), "ghost identity set does not accidentally seal a real met choice");
        foreach (object choice in choices) GardenReadVisualInputs(GardenPreview(choice), "met/" + GardenPreview(choice).GetType().Name);
        foreach (CardModel card in hidden) GardenReadVisualInputs(card, "mixed-ghost/" + card.GetType().Name);
        GardenGhostGuards(hidden);
        Check(Player.Deck.Cards.SequenceEqual(savedDeck) && GardenRawState(owned) == savedOwned, "all repeated preview reads leave the real owned permanent card/deck unchanged");
        Check(GardenManualState(manual) == savedManual, "preview construction/read/ghost callbacks do not grant points, encounter characters or alter progression ledgers");
        Check(canonicals.Select(GardenRawState).SequenceEqual(before), "mixed 8/48 preview generation leaves every canonical model untouched");
    }

    static void GardenPortraitCases()
    {
        Type portraitNode=Mod.GetType("HBRGOSakiMod.Core.Nodes.Cards.NHbrgoStrikePortrait",true)!;
        bool NeedsWhite(CardModel card)=>(bool)portraitNode.GetMethod("NeedsWhiteBackground",F)!.Invoke(null,[card])!;
        string manifestPath=Path.Combine(RepositoryRoot,"validation/memory-garden-display/fixtures/portraits.json");
        using JsonDocument manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));
        Check(manifest.RootElement.GetProperty("mappings").EnumerateArray().Any(row => row.GetProperty("card").GetString() == "PeacefulMemoriesHbrgoSaki" && row.GetProperty("character").GetString() == "aoi"), "Aoi portrait is explicitly covered rather than silently accepting the placeholder");
        foreach(JsonElement row in manifest.RootElement.GetProperty("mappings").EnumerateArray())
        {
            string name=row.GetProperty("card").GetString()!,path="res://"+row.GetProperty("path").GetString();
            Type type=Mod.GetTypes().Single(type=>type.Name==name);
            CardModel preview=ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();
            Check((string)type.GetProperty("PortraitPath",F)!.GetValue(preview)! == path,name+" actual portrait getter uses its assigned character image");
            Check((string)type.GetProperty("BetaPortraitPath",F)!.GetValue(preview)! == path,name+" actual alternate portrait getter uses the same image");
            Check(File.Exists(Path.Combine(RepositoryRoot,path[6..])),name+" portrait asset exists in the source project");
            Check(NeedsWhite(preview),name+" actual portrait presentation places its transparent cut-in frame on white");
        }
        foreach(JsonElement row in manifest.RootElement.GetProperty("assets").EnumerateArray())
        {
            string path=Path.Combine(RepositoryRoot,row.GetProperty("path").GetString()!);
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))==row.GetProperty("sha256").GetString(),"portrait reuses exact accepted bytes: "+row.GetProperty("path").GetString());
        }
        foreach(Type type in new[] { typeof(HalfBodyHbrgoSaki), typeof(MyLegendBeginsHbrgoSaki), typeof(HelloWorldHbrgoSaki),
            typeof(SaviorGrandEntranceHbrgoSaki), typeof(WorldsFinestHbrgoSaki), typeof(MoreThanOneTruthHbrgoSaki),
            typeof(CrushYouLikeABugHbrgoSaki), typeof(SummerGrassDreamHbrgoSaki), typeof(BoostUpHbrgoSaki) })
            Check(NeedsWhite(ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable()),type.Name+" retains its existing white cut-in background");
        foreach(Type type in new[] { typeof(StrikeHbrgoSaki), typeof(DefendHbrgoSaki), typeof(CommunicationMeetingHbrgoSaki), typeof(MatchaParfaitHbrgoSaki) })
            Check(!NeedsWhite(ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable()),type.Name+" retains its existing portrait background");
        Check(!NeedsWhite(null!),"null/pooled portrait model does not request a cut-in white background");
    }

    public static void RunGardenDisplay(string path)
    {
        string? error = null;
        try { GardenDisplaySetup(); GardenDisplayCases(); GardenPortraitCases(); }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(exception); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok = error == null, checks = Passed.Count, passed = Passed, cases = GardenDisplaySamples, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            boundary = "Actual production preview helpers, native CurrentTarget/Owner/cost/title/dynamic-var/description paths, and real ghost callback guards; only native UI scene/texture terminals are absent. No Godot rendering/input test or full NCard node initialization is claimed. Native models, card state and canonical assert behavior are not stubbed." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((error == null ? "PASS " : "FAIL ") + Passed.Count + " memory-garden display checks");
        if (error != null) throw new Exception("See " + path);
    }
}
