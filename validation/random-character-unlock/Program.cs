using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils.Attributes;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Timeline;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Unlocks;
using Environment = System.Environment;

[CustomID("RANDOM_CHECK_VISIBLE_EXCLUDED")]
public sealed class VisibleRandomExcluded : HbrgoSaki
{
    public override bool HideFromVanillaCharacterSelect => false;
    public override bool AllowInVanillaRandomCharacterSelect => false;
}
[CustomID("RANDOM_CHECK_HIDDEN_ELIGIBLE")]
public sealed class HiddenRandomEligible : HbrgoSaki
{
    public override bool HideFromVanillaCharacterSelect => true;
    public override bool AllowInVanillaRandomCharacterSelect => true;
}
[CustomID("RANDOM_CHECK_VISIBLE_ELIGIBLE")]
public sealed class VisibleRandomEligible : HbrgoSaki
{
    public override bool HideFromVanillaCharacterSelect => false;
    public override bool AllowInVanillaRandomCharacterSelect => true;
}
[CustomID("RANDOM_CHECK_RITSU_EXCLUDED")]
public sealed class RitsuOnlyRandomExcluded : CharacterModel, IModCharacterVanillaSelectionPolicy
{
    public bool HideFromVanillaCharacterSelect => false;
    public bool AllowInVanillaRandomCharacterSelect => false;
    public bool HideInCardLibraryCompendium => false;
    public override Color NameColor => Colors.White;
    public override CharacterGender Gender => CharacterGender.Neutral;
    protected override CharacterModel? UnlocksAfterRunAs => null;
    public override int StartingHp => 1;
    public override int StartingGold => 0;
    public override CardPoolModel CardPool => null!;
    public override RelicPoolModel RelicPool => null!;
    public override PotionPoolModel PotionPool => null!;
    public override IEnumerable<CardModel> StartingDeck => [];
    public override IReadOnlyList<RelicModel> StartingRelics => [];
    public override float AttackAnimDelay => 0;
    public override float CastAnimDelay => 0;
    public override List<string> GetArchitectAttackVfx() => [];
}

static class RandomCharacterUnlockChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Assembly Native = typeof(ModelDb).Assembly, Mod = typeof(HbrgoSaki).Assembly, BaseLib = typeof(CustomCharacterModel).Assembly;
    static readonly Harmony H = new("hbr.random-unlock.native-checks");
    static readonly List<string> Passed = [];
    static readonly List<object> Rows = [];
    static readonly Dictionary<object, bool> Enabled = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<object, bool> Visible = new(ReferenceEqualityComparer.Instance);
    static readonly List<CharacterModel[]> InitCatalogs = [];
    static readonly List<CharacterModel> ExtraCatalog = [];
    static readonly ICharacterSelectButtonDelegate Delegate = new FixtureDelegate();
    static readonly string[] NativeNames = ["Ironclad", "Silent", "Regent", "Necrobinder", "Defect", "Fool"];
    static UnlockState Unlocks = null!;
    static SaveManager Save = null!;
    static Type RuntimeScope = null!;
    static MethodInfo? InitRandom;
    static string GatedEpoch = "";
    static int InitDepth, EnableCalls, UnlockFaultsRemaining;
    static bool ExpectOld => Environment.GetEnvironmentVariable("HBR_RANDOM_EXPECT_OLD") == "1";

    sealed class FixtureDelegate : ICharacterSelectButtonDelegate
    {
        public StartRunLobby Lobby => null!;
        public void SelectCharacter(NCharacterSelectButton button) { }
        public void SelectCharacter(NCharacterSelectButton button, CharacterModel character) { }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Passed.Add(message); }
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    { try { return method.Invoke(owner, args); } catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; } }
    static FieldInfo Field(Type type, string name)
    { for (Type? current = type; current != null; current = current.BaseType) if (current.GetField(name, F | BindingFlags.DeclaredOnly) is { } field) return field; throw new MissingFieldException(type.FullName, name); }
    static void Set(object owner, string field, object? value) => Field(owner.GetType(), field).SetValue(owner, value);
    static T Blank<T>() { var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static void Patch(MethodBase method, string prefix, int priority = Priority.Normal)
    {
        if (method is MethodInfo info)
            method = info.DeclaringType!.GetMethod(info.Name, F | BindingFlags.DeclaredOnly, null, info.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), null)!;
        H.Patch(method, prefix: new HarmonyMethod(typeof(RandomCharacterUnlockChecks).GetMethod(prefix, F)!) { priority = priority });
    }
    static bool Skip() => false;
    static bool NameCtor(StringName __instance) { GC.SuppressFinalize(__instance); return false; }
    static bool Encounters(ref IEnumerable<EncounterModel> __result) { __result = []; return false; }
    static bool SaveSource(ref SaveManager __result) { __result = Save; return false; }
    static bool UnlockSource(ref UnlockState __result)
    {
        if (UnlockFaultsRemaining > 0) { UnlockFaultsRemaining--; throw new InvalidOperationException("controlled-random-unlock-source-failure"); }
        __result = Unlocks; return false;
    }
    static bool Texture(ref CompressedTexture2D? __result) { __result = null; return false; }
    static bool SetVisible(object __instance, bool value) { Visible[__instance] = value; return false; }
    static bool Enable(object __instance) { Enabled[__instance] = true; EnableCalls++; return false; }
    static bool Disable(object __instance) { Enabled[__instance] = false; return false; }
    static void AppendExtra(ref IEnumerable<CharacterModel> __result) => __result = __result.Concat(ExtraCatalog).ToArray();
    static void ObserveCatalog(IEnumerable<CharacterModel> __result) { if (InitDepth > 0) InitCatalogs.Add(__result.ToArray()); }
    static void EnterInit() => InitDepth++;
    static void ExitInit() => InitDepth--;
    static void InstallClass(Assembly assembly, string name, string owner)
    {
        Type type = assembly.GetType(name, true)!;
        Check(new Harmony(owner).CreateClassProcessor(type).Patch().Count > 0, "installs actual " + name);
    }
    static void InstallRitsuPolicy(Assembly runtime)
    {
        Type policy = runtime.GetType("STS2RitsuLib.Scaffolding.Characters.Patches.CharacterVanillaSelectionPolicyPatches", true)!;
        RuntimeScope = runtime.GetType("STS2RitsuLib.Scaffolding.Characters.Patches.CharacterVanillaSelectionPolicyScope", true)!;
        object[] targets = ((Array)Invoke(policy.GetMethod("GetTargets", F)!, null)!).Cast<object>().ToArray();
        // Resolve the real current targets from the supplied native version;
        // GetTargets is also inspected so InitAsRandom's dependency gap is explicit.
        foreach (MethodBase target in new MethodBase?[] {
            typeof(NCharacterSelectScreen).GetMethod("InitCharacterButtons", F),
            typeof(NCharacterSelectScreen).GetMethod("UpdateRandomCharacterVisibility", F),
            typeof(NCharacterSelectButton).GetMethod("Init", F),
            typeof(StartRunLobby).GetMethod("BeginRunLocally", F) }.OfType<MethodBase>())
            H.Patch(target, prefix: new HarmonyMethod(policy.GetMethod("Prefix", F)!), finalizer: new HarmonyMethod(policy.GetMethod("Finalizer", F)!));
        Check(targets.Length == 4, "actual Ritsu policy still declares its four legacy selection targets");
        Type all = runtime.GetType("STS2RitsuLib.Scaffolding.Characters.Patches.CharacterVanillaSelectionPolicyAllCharactersPatch", true)!;
        new Harmony("com.ritsukage.sts2-RitsuLib.framework-character-policy").Patch(typeof(ModelDb).GetProperty(nameof(ModelDb.AllCharacters))!.GetMethod!, postfix: new HarmonyMethod(all.GetMethod("Postfix", F)!));
        Type unlock = runtime.GetType("STS2RitsuLib.Unlocks.Patches.CharacterUnlockFilterPatch", true)!;
        H.Patch(typeof(UnlockState).GetProperty(nameof(UnlockState.Characters))!.GetMethod!, postfix: new HarmonyMethod(unlock.GetMethod("Postfix", F)!));
    }
    static void Install()
    {
        TestMode.IsOn = true;
        InstallClass(BaseLib, "BaseLib.Patches.Content.PrefixIdPatch", "BaseLib");
        Patch(typeof(StringName).GetConstructor([typeof(string)])!, nameof(NameCtor));
        Type logger = Native.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        foreach (MethodInfo method in logger.GetMethods(F).Where(method => method.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Patch(method, nameof(Skip));
        Patch(typeof(ModelDb).GetProperty("AllEncounters", F)!.GetMethod!, nameof(Encounters));
        Patch(typeof(SaveManager).GetProperty(nameof(SaveManager.Instance))!.GetMethod!, nameof(SaveSource));
        Patch(typeof(SaveManager).GetMethod(nameof(SaveManager.GenerateUnlockStateFromProgress))!, nameof(UnlockSource));
        foreach (string property in new[] { "CharacterSelectIcon", "CharacterSelectLockedIcon" }) Patch(typeof(CharacterModel).GetProperty(property, F)!.GetMethod!, nameof(Texture));
        Patch(typeof(TextureRect).GetProperty(nameof(TextureRect.Texture))!.SetMethod!, nameof(Skip));
        Patch(typeof(CanvasItem).GetProperty(nameof(CanvasItem.Visible))!.SetMethod!, nameof(SetVisible));
        Type button = Native.GetType("MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton", true)!;
        Patch(button.GetMethod("Enable", F)!, nameof(Enable)); Patch(button.GetMethod("Disable", F)!, nameof(Disable));
        Save = Blank<SaveManager>();
        foreach (Type type in NativeNames.Select(name => Native.GetType("MegaCrit.Sts2.Core.Models.Characters." + name)).OfType<Type>()
            .Concat(new[] { typeof(RandomCharacter), typeof(HbrgoSaki), typeof(HbrgoSaki31O), typeof(HbrgoSaki30A), typeof(HbrgoHotaru), typeof(VisibleRandomExcluded), typeof(HiddenRandomEligible), typeof(VisibleRandomEligible), typeof(RitsuOnlyRandomExcluded) }))
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        CustomContentDictionary.CustomCharacters.Clear();
        CustomContentDictionary.CustomCharacters.AddRange(new CustomCharacterModel[] { ModelDb.Character<HbrgoSaki>(), ModelDb.Character<HbrgoSaki31O>(), ModelDb.Character<HbrgoSaki30A>(), ModelDb.Character<HbrgoHotaru>(), ModelDb.Character<HbrgoHotaru>() });
        InstallClass(BaseLib, "BaseLib.Patches.Content.AddCustomCharacters", "BaseLib");
        InstallClass(BaseLib, "BaseLib.Abstracts.HideVanillaCharacterSelectCharactersPatch", "BaseLib");
        InstallClass(BaseLib, "BaseLib.Abstracts.VanillaRandomCharacterEligibilityPatch", "BaseLib");
        Assembly runtime = Assembly.Load("STS2-RitsuLib.Runtime"); InstallRitsuPolicy(runtime);
        new Harmony("com.ritsukage.sts2-RitsuLib.framework-content-registry").Patch(typeof(ModelDb).GetProperty(nameof(ModelDb.AllCharacters))!.GetMethod!, postfix: new HarmonyMethod(typeof(RandomCharacterUnlockChecks).GetMethod(nameof(AppendExtra), F)!));
        InstallClass(Mod, "HBRGOSakiMod.Core.Patches.CharacterCatalogHotaruNormalizationPatch", "HBRGOSakiMod");
        InstallClass(Mod, "HBRGOSakiMod.Core.Patches.HiddenArtificialSaviorUnlockPatch", "HBRGOSakiMod");
        GatedEpoch = Epoch("Ironclad2Epoch");
        foreach (Type type in new[] { typeof(VisibleRandomExcluded), typeof(RitsuOnlyRandomExcluded), typeof(VisibleRandomEligible), typeof(HiddenRandomEligible) })
            ModUnlockRegistry.For("random-character-unlock-checks").RequireEpoch(type, GatedEpoch);
        Unlocks = AllUnlocked();
        InitRandom = typeof(NCharacterSelectButton).GetMethod("InitAsRandom", F);
        MethodInfo init = InitRandom ?? typeof(NCharacterSelectButton).GetMethod("Init", F)!;
        H.Patch(init, prefix: new HarmonyMethod(typeof(RandomCharacterUnlockChecks).GetMethod(nameof(EnterInit), F)!) { priority = Priority.First + 1000 }, finalizer: new HarmonyMethod(typeof(RandomCharacterUnlockChecks).GetMethod(nameof(ExitInit), F)!) { priority = Priority.Last - 1000 });
        H.Patch(typeof(ModelDb).GetProperty(nameof(ModelDb.AllCharacters))!.GetMethod!, postfix: new HarmonyMethod(typeof(RandomCharacterUnlockChecks).GetMethod(nameof(ObserveCatalog), F)!) { priority = Priority.Last - 1000 });
        Type? compatibility = Mod.GetType("HBRGOSakiMod.Core.Patches.RandomCharacterEligibilityCompatibilityPatch");
        Type[] production = Mod.GetTypes().Where(type => type.Name.StartsWith("RandomCharacter", StringComparison.Ordinal) && type.IsDefined(typeof(HarmonyPatch), false)).ToArray();
        Check(ExpectOld ? production.Length == 0 : compatibility != null && production.Length > 0, ExpectOld ? "old candidate has no new random-character compatibility patch" : "candidate contains optional random-character compatibility patches");
        foreach (Type type in production) new Harmony("HBRGOSakiMod").CreateClassProcessor(type).Patch();
    }
    static UnlockState AllUnlocked(string? missing = null) => new(EpochModel.AllEpochIds.Where(id => id != missing), [], 100);
    static string Epoch(string typeName) => EpochModel.GetId(Native.GetTypes().Single(type => type.Name == typeName));
    static NCharacterSelectButton Button()
    {
        var button = Blank<NCharacterSelectButton>(); Set(button, "_icon", Blank<TextureRect>()); Set(button, "_lock", Blank<TextureRect>());
        Set(button, "_remoteSelectedPlayers", new HashSet<ulong>()); Enabled[button] = false; return button;
    }
    static void Init(NCharacterSelectButton button)
    {
        InitCatalogs.Clear();
        if (InitRandom != null) Invoke(InitRandom, button, Delegate);
        else Invoke(typeof(NCharacterSelectButton).GetMethod("Init", F)!, button, ModelDb.Character<RandomCharacter>(), Delegate);
    }
    static void Scope(string method, Action body)
    {
        MethodBase token = typeof(NCharacterSelectScreen).GetMethod(method, F)!;
        Invoke(RuntimeScope.GetMethod("Enter", F)!, null, token);
        try { body(); } finally { Invoke(RuntimeScope.GetMethod("Exit", F)!, null, token); }
    }
    static void Reset()
    {
        CustomContentDictionary.CustomCharacters.RemoveAll(character => character is VisibleRandomExcluded or HiddenRandomEligible or VisibleRandomEligible);
        ExtraCatalog.Clear(); Unlocks = AllUnlocked(); EnableCalls = UnlockFaultsRemaining = 0;
    }
    static void CatalogCases()
    {
        Reset(); CharacterModel[] catalog = ModelDb.AllCharacters.ToArray();
        Check(ExpectOld ? catalog.Any(character => character.GetType() == typeof(HbrgoSaki)) : catalog.All(character => character.GetType() != typeof(HbrgoSaki)),
            ExpectOld ? "old unscoped global catalog still exposes retired artificial savior" : "global discovery catalog excludes only the exact retired artificial savior");
        Check(catalog.Contains(ModelDb.Character<HbrgoSaki31O>()) && catalog.Contains(ModelDb.Character<HbrgoSaki30A>()), "both real derived Saki variants remain registered in the global discovery catalog");
        Check(catalog.Count(character => character is HbrgoHotaru) == 1, "global discovery keeps one canonical Hotaru despite duplicate BaseLib discovery");
        Check(ModelDb.GetId<HbrgoSaki>().Entry == "HBRGO_SAKI" && ReferenceEquals(ModelDb.GetById<CharacterModel>(ModelDb.GetId<HbrgoSaki>()), ModelDb.Character<HbrgoSaki>()), "old HBRGO_SAKI direct ID still resolves its original canonical model");
        Check(!Unlocks.Characters.Any(character => character.GetType() == typeof(HbrgoSaki)), "actual hidden-unlock postfix never grants the retired base as an unlocked character");
        Check(!Unlocks.Characters.Contains(ModelDb.Character<RandomCharacter>()), "native random meta-character itself is outside the unlock catalog");
        Rows.Add(new { scenario = "global-catalog", types = catalog.Select(character => character.GetType().Name), old = ExpectOld });
    }
    static void RandomCases()
    {
        Reset(); var button = Button(); Init(button);
        bool oldGap = ExpectOld && InitRandom != null;
        Check(button.IsLocked == oldGap, oldGap ? "negative: independent beta InitAsRandom locks on retired catalog pollution" : "all eligible native and mod characters unlock the actual random button");
        Check(ReferenceEquals(button.Character, ModelDb.Character<RandomCharacter>()) && button.IsRandom && ReferenceEquals(Field(button.GetType(), "_delegate").GetValue(button), Delegate), "random initialization preserves the native meta-character and exact delegate");
        Scope("InitCharacterButtons", () =>
        {
            Init(Button());
            Type visible = BaseLib.GetType("BaseLib.Abstracts.HideVanillaCharacterSelectCharactersPatch", true)!;
            var menuCatalog = (IEnumerable<CharacterModel>)Invoke(visible.GetMethod("GetVisibleCharacters", F)!, null)!;
            Check(menuCatalog.All(character => character.GetType() != typeof(HbrgoSaki)), "actual BaseLib visible-menu catalog excludes the retired base during ordinary menu construction");
        });
        CustomContentDictionary.CustomCharacters.Add(ModelDb.Character<VisibleRandomExcluded>());
        ExtraCatalog.Add(ModelDb.Character<RitsuOnlyRandomExcluded>()); Unlocks = AllUnlocked(GatedEpoch);
        Check(!Unlocks.Characters.Contains(ModelDb.Character<VisibleRandomExcluded>()) && !Unlocks.Characters.Contains(ModelDb.Character<RitsuOnlyRandomExcluded>()), "actual Ritsu epoch requirements keep both visible excluded fixtures locked");
        Scope("InitCharacterButtons", () =>
        {
            var visible = ModelDb.AllCharacters.ToArray();
            Check(visible.Contains(ModelDb.Character<VisibleRandomExcluded>()) && visible.Contains(ModelDb.Character<RitsuOnlyRandomExcluded>()), "outer visible policy retains visible-but-random-excluded characters");
            var random = Button(); Init(random);
            Check(random.IsLocked == oldGap, oldGap ? "negative: outer-visible beta random init wrongly requires visible random-excluded characters" : "random init honors both BaseLib and Ritsu random exclusions inside an outer visible scope");
            Check(ModelDb.AllCharacters.Contains(ModelDb.Character<VisibleRandomExcluded>()) && ModelDb.AllCharacters.Contains(ModelDb.Character<RitsuOnlyRandomExcluded>()), "random init restores the complete surrounding visible scope");
        });
        Check(ModelDb.AllCharacters.Contains(ModelDb.Character<VisibleRandomExcluded>()), "random init leaves no random-policy scope in the caller");
        Rows.Add(new { scenario = "native-random-initialization", nativeNewEntry = InitRandom != null, expectedOldGap = oldGap });
    }
    static void NativeLockCases()
    {
        foreach (string character in NativeNames.Skip(1).Where(name => Native.GetType("MegaCrit.Sts2.Core.Models.Characters." + name) != null))
        {
            Reset(); string epoch = Epoch(character + "1Epoch");
            Unlocks = AllUnlocked(epoch); var button = Button(); Init(button);
            Check(button.IsLocked, "a genuinely locked native " + character + " keeps random selection locked");
        }
        Reset(); CustomContentDictionary.CustomCharacters.Add(ModelDb.Character<VisibleRandomEligible>()); Unlocks = AllUnlocked(GatedEpoch);
        var lockedMod = Button(); Init(lockedMod); Check(lockedMod.IsLocked, "a genuinely locked random-eligible mod character keeps random selection locked");
    }
    static void HiddenRandomCases()
    {
        // Use Ritsu's real ref-postfix catalog source for this fixture. BaseLib's
        // pass-through append executes after ref postfixes and is independently
        // filtered by its menu helper, rather than by the Ritsu scope alone.
        Reset(); ExtraCatalog.Add(ModelDb.Character<HiddenRandomEligible>());
        Scope("InitCharacterButtons", () =>
        {
            Check(!ModelDb.AllCharacters.Contains(ModelDb.Character<HiddenRandomEligible>()), "outer visible scope hides a character that explicitly allows random selection");
            Unlocks = AllUnlocked(GatedEpoch); var locked = Button(); Init(locked);
            if (!ExpectOld && InitRandom != null)
            {
                Check(locked.IsLocked, "random scope counts a hidden-but-random-eligible locked character");
                Check(InitCatalogs.Any(catalog => catalog.Contains(ModelDb.Character<HiddenRandomEligible>())), "actual random initialization sees the hidden eligible character in its source catalogs");
            }
            Unlocks = AllUnlocked(); var unlocked = Button(); Init(unlocked);
            Check(unlocked.IsLocked == (ExpectOld && InitRandom != null), ExpectOld && InitRandom != null ? "negative: old beta random remains locked by BaseLib-appended retired base" : "unlocking the hidden eligible character permits random selection inside the visible scope");
            Check(!ModelDb.AllCharacters.Contains(ModelDb.Character<HiddenRandomEligible>()), "hidden eligible random lookup restores the enclosing visible policy");
        });
        Check(ModelDb.AllCharacters.Contains(ModelDb.Character<HiddenRandomEligible>()), "hidden eligible source remains discoverable outside selection scopes");
    }
    static void CachedCases()
    {
        Reset(); string silentEpoch = Epoch("Silent1Epoch");
        Unlocks = AllUnlocked(silentEpoch); var button = Button(); Init(button); Check(button.IsLocked, "cached random button initially respects a real locked epoch");
        Unlocks = AllUnlocked(); button.UnlockIfPossible();
        Check(button.IsLocked == (ExpectOld || InitRandom == null), ExpectOld || InitRandom == null ? "old/legacy native cached random unlock cannot find its random meta-character in the unlock list" : "cached beta random button recomputes eligible unlocks after progress changes");
        if (!ExpectOld && InitRandom != null) Check(Enabled[button] && EnableCalls == 1 && ReferenceEquals(Field(button.GetType(), "_delegate").GetValue(button), Delegate), "cached random refresh enables the native button once and preserves its original delegate");
        Reset(); Unlocks = AllUnlocked(silentEpoch); var ordinary = Button();
        Invoke(typeof(NCharacterSelectButton).GetMethod("Init", F)!, ordinary, ModelDb.Character<Silent>(), Delegate);
        Check(ordinary.IsLocked && !ordinary.IsRandom, "ordinary locked character button is separate from random handling");
        Unlocks = AllUnlocked(); ordinary.UnlockIfPossible(); Check(!ordinary.IsLocked, "ordinary character still uses its native unlock refresh");
        Type? rotating = Native.GetType("MegaCrit.Sts2.Core.Models.Characters.RotatingCharacter");
        if (rotating != null)
        {
            if (!ModelDb.Contains(rotating)) ModelDb.Inject(rotating);
            var rotation = Button(); Set(rotation, "<IsRandom>k__BackingField", true); Set(rotation, "_character", ModelDb.GetById<CharacterModel>(ModelDb.GetId(rotating))); Set(rotation, "_isLocked", true);
            rotation.UnlockIfPossible(); Check(rotation.IsLocked, "beta rotating meta-button is not treated as the exact RandomCharacter");
        }
    }
    static void FailureScopeCases()
    {
        if (ExpectOld || InitRandom == null) return;
        Reset(); ExtraCatalog.Add(ModelDb.Character<HiddenRandomEligible>());
        CustomContentDictionary.CustomCharacters.Add(ModelDb.Character<VisibleRandomExcluded>());
        Scope("InitCharacterButtons", () =>
        {
            UnlockFaultsRemaining = 1; bool failed = false;
            try { Init(Button()); }
            catch (InvalidOperationException error) when (error.Message == "controlled-random-unlock-source-failure") { failed = true; }
            Check(failed && InitDepth == 0, "native random initialization propagates its original source failure and balances its fixture depth");
            CharacterModel[] after = ModelDb.AllCharacters.ToArray();
            Check(after.Contains(ModelDb.Character<VisibleRandomExcluded>()) && !after.Contains(ModelDb.Character<HiddenRandomEligible>()), "production exception finalizer restores the enclosing visible scope");
        });
        Check(ModelDb.AllCharacters.Contains(ModelDb.Character<HiddenRandomEligible>()), "failed initialization leaves no random or visible scope in its caller");
        Reset(); Unlocks = AllUnlocked(Epoch("Silent1Epoch")); var cached = Button(); Init(cached);
        Unlocks = AllUnlocked(); UnlockFaultsRemaining = 1; cached.UnlockIfPossible();
        Check(cached.IsLocked && EnableCalls == 0 && InitDepth == 0, "failed cached random refresh falls back without unlocking, enabling or leaking a scope");
        cached.UnlockIfPossible();
        Check(!cached.IsLocked && Enabled[cached] && EnableCalls == 1, "a later valid cached refresh succeeds after the failed attempt");
    }
    static void PoolCases()
    {
        Reset();
        Type[] modelTypes = Native.GetTypes().Concat(Mod.GetTypes()).Where(type => !type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) != null &&
            (typeof(CardModel).IsAssignableFrom(type) || typeof(CardPoolModel).IsAssignableFrom(type) || typeof(PotionPoolModel).IsAssignableFrom(type) || typeof(RelicPoolModel).IsAssignableFrom(type))).ToArray();
        foreach (Type type in modelTypes) if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        // A bounded real native registration mapping proves the catalog chain;
        // the full production content pack is deliberately not initialized here.
        foreach (Type pool in new[] { typeof(HBRGOSakiCardPool), typeof(HBRGOSaki31OCardPool), typeof(HBRGOSaki30ACardPool) })
            foreach (Type card in new[] { typeof(StrikeHbrgoSaki), typeof(DefendHbrgoSaki) }) ModHelper.AddModelToPool(pool, card);
        foreach (Type card in new[] { typeof(HotaruStrike), typeof(HotaruDefend) }) ModHelper.AddModelToPool(typeof(HbrgoHotaruCardPool), card);
        CardPoolModel[] cards = ModelDb.AllCharacterCardPools.ToArray();
        PotionPoolModel[] potions = ModelDb.AllCharacterPotionPools.ToArray();
        RelicPoolModel[] relics = ModelDb.AllCharacterRelicPools.ToArray();
        foreach (CharacterModel character in new CharacterModel[] { ModelDb.Character<HbrgoSaki31O>(), ModelDb.Character<HbrgoSaki30A>(), ModelDb.Character<HbrgoHotaru>() })
        {
            Check(cards.Contains(character.CardPool), "native AllCharacterCardPools retains " + character.GetType().Name + " pool");
            Check(potions.Contains(character.PotionPool), "native AllCharacterPotionPools retains " + character.GetType().Name + " pool");
            Check(relics.Contains(character.RelicPool), "native AllCharacterRelicPools retains " + character.GetType().Name + " pool");
        }
        foreach (AbstractModel pool in new AbstractModel[] { ModelDb.CardPool<HBRGOSakiCardPool>(), ModelDb.PotionPool<HBRGOSakiPotionPool>(), ModelDb.RelicPool<HBRGOSakiRelicPool>() })
            Check(ReferenceEquals(ModelDb.GetById<AbstractModel>(pool.Id), pool), "old base pool remains reachable through its exact native ID: " + pool.GetType().Name);
        Check(ExpectOld ? cards.Contains(ModelDb.CardPool<HBRGOSakiCardPool>()) : !cards.Contains(ModelDb.CardPool<HBRGOSakiCardPool>()),
            ExpectOld ? "old discovery still includes the retired base character's card-pool slot" : "retired base loses its standalone discovery card-pool slot without registry deletion");
        CardModel[] allCards = ModelDb.AllCards.ToArray();
        foreach (CardModel card in new CardModel[] { ModelDb.Card<StrikeHbrgoSaki>(), ModelDb.Card<DefendHbrgoSaki>(), ModelDb.Card<HotaruStrike>(), ModelDb.Card<HotaruDefend>() })
            Check(allCards.Contains(card), "native global AllCards preserves the shared or live starter card " + card.GetType().Name);
        Check(ModelDb.CardPool<HBRGOSaki31OCardPool>().AllCards.Contains(ModelDb.Card<StrikeHbrgoSaki>()) && ModelDb.CardPool<HBRGOSaki30ACardPool>().AllCards.Contains(ModelDb.Card<StrikeHbrgoSaki>()),
            "real native pool registration keeps the same shared Strike canonical object in both live Saki pools");
        Rows.Add(new { scenario = "native-pool-catalogs", registeredModelTypes = modelTypes.Length, cardPools = cards.Select(pool => pool.Id.Entry), potionPools = potions.Select(pool => pool.Id.Entry), relicPools = relics.Select(pool => pool.Id.Entry), allCardCount = allCards.Length,
            registrationBoundary = "Actual native ModHelper.AddModelToPool with a bounded shared/starter mapping; no fake AllCards or pool getters and no full content-pack initialization." });
    }
    public static void Run(string report)
    {
        bool ok = false; string? error = null;
        try { Install(); CatalogCases(); RandomCases(); NativeLockCases(); HiddenRandomCases(); CachedCases(); FailureScopeCases(); PoolCases(); Check(InitDepth == 0, "native random initialization scopes balance after every case"); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error, expectOldDefect = ExpectOld, betaInitAsRandom = InitRandom != null,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Native.Location))),
            sourceNativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"), rows = Rows,
            boundary = "Actual native ModelDb character catalog, BaseLib PrefixId/AddCustomCharacters/visibility/random transpilers, Ritsu scope/catalog/unlock filtering, actual native UnlockState epochs and button Init/InitAsRandom/UnlockIfPossible. BaseLib custom discovery list and fixture-only Ritsu extra catalog/epoch registrations controlled; only SaveManager unlock source, logging, textures, Godot property setters and Enable/Disable leaves isolated. No save/config reads or writes, Godot rendering, live network or packaging." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " random-character unlock checks");
        if (!ok) throw new Exception("Inspect " + report);
    }
}
