using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Potions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using System.Collections;
using System.Text.RegularExpressions;

static partial class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const decimal Maximum = 18_446_744_073_709_551_616m;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static int FixedCalls;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static T Blank<T>()
    {
        var result = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        GC.SuppressFinalize(result!);
        return result;
    }
    static FieldInfo Field(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
            if (current.GetField(name, F) is { } field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object target, string name, object? value) => Field(target.GetType(), name).SetValue(target, value);
    static object? Call(Type type, string name, object? target, params object?[] args) => type.GetMethod(name, F)!.Invoke(target, args);
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Passed.Add(name);
    }
    static void Equal(decimal actual, decimal expected, string name) => Check(actual == expected, $"{name}: {actual} = {expected}");
    static bool Skip() => false;
    static void FixedEntered() => FixedCalls++;
    static AbstractModel Canonical(Type type)
    {
        var content = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
        if (content.Values.FirstOrDefault(model => model.GetType() == type) is { } existing) return existing;
        var model = (AbstractModel)Activator.CreateInstance(type)!;
        content[model.Id] = model;
        return model;
    }
    static T Relic<T>(Player player) where T : RelicModel
    {
        var result = (T)((RelicModel)Canonical(typeof(T))).ToMutable();
        Set(result, "_owner", player);
        return result;
    }
    static void Data(ElectronicSoldierManual manual, string value) =>
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!.SetValue(manual, value);
    static void Initialize(ElectronicSoldierManual manual) => Call(typeof(ElectronicSoldierManual), "InitializeHbrBattleNumerics", manual);
    sealed class Fixture
    {
        internal readonly Player Player = Blank<Player>();
        internal readonly Creature Hero = Blank<Creature>();
        internal readonly RunState Run = Blank<RunState>();
        internal readonly CombatState Combat = Blank<CombatState>();
        internal readonly ElectronicSoldierManual Book;
        internal Fixture(CharacterModel? character = null, bool enabled = true, int? current = null, bool hotaruBook = false)
        {
            character ??= Blank<HbrgoSaki>();
            int maximum = character.StartingHp;
            Set(Player, "<Character>k__BackingField", character); Set(Player, "<Creature>k__BackingField", Hero); Set(Player, "_runState", Run);
            Set(Hero, "<Player>k__BackingField", Player); Set(Hero, "<Side>k__BackingField", CombatSide.Player);
            Set(Hero, "_powers", new List<PowerModel>()); Set(Hero, "_maxHp", maximum); Set(Hero, "_currentHp", current ?? maximum);
            Set(Run, "_players", new List<Player> { Player });
            foreach (string name in new[] { "_mapPointHistory", "_visitedMapCoords", "_currentRooms" })
            {
                FieldInfo field = Field(Run.GetType(), name); field.SetValue(Run, Activator.CreateInstance(field.FieldType));
            }
            Set(Combat, "<RunState>k__BackingField", Run); Set(Combat, "_allies", new List<Creature> { Hero });
            Set(Combat, "_enemies", new List<Creature>()); Hero.CombatState = Combat;
            Book = hotaruBook ? Relic<HotaruElectronicSoldierManual>(Player) : Relic<ElectronicSoldierManual>(Player);
            Set(Player, "_relics", new List<RelicModel> { Book });
            if (enabled) Initialize(Book);
        }
        internal DeflectorProtection Shield()
        {
            var shield = Relic<DeflectorProtection>(Player);
            ((List<RelicModel>)Field(typeof(Player), "_relics").GetValue(Player)!).Add(shield);
            shield.AfterObtained().GetAwaiter().GetResult();
            return shield;
        }
        internal Creature Phantom(int raw = 80)
        {
            var creature = Blank<Creature>(); var monster = Blank<SmallHopper>();
            Set(creature, "_petOwner", Player); Set(creature, "<Side>k__BackingField", CombatSide.Player);
            Set(creature, "_maxHp", raw); Set(creature, "_currentHp", raw); Set(creature, "_powers", new List<PowerModel>());
            Set(creature, "<Monster>k__BackingField", monster); Set(monster, "_creature", creature); creature.CombatState = Combat;
            ((List<Creature>)Field(typeof(CombatState), "_allies").GetValue(Combat)!).Add(creature);
            return creature;
        }
    }
    static void Install()
    {
        var harmony = new Harmony("hbr.mod.fixed.resource.model.checks");
        void Prefix(MethodBase method) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Skip), F)!));
        Prefix(typeof(StringName).GetConstructor([typeof(string)])!);
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor) Prefix(editor);
        foreach (MethodInfo method in logger.GetMethods(F).Where(method => method.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Prefix(method);
        Prefix(typeof(RelicModel).GetMethod("Flash", F, null, Type.EmptyTypes, null)!);
        harmony.Patch(Helper("HbrResourceValueHelper").GetMethod("Fixed", F)!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(FixedEntered), F)!));
        TestMode.IsOn = true;
        foreach (Type type in new[] { typeof(ElectronicSoldierManual), typeof(HotaruElectronicSoldierManual), typeof(DeflectorProtection) })
            SavedPropertiesTypeCache.InjectTypeIntoCache(type);
    }

    static readonly List<decimal> Heals = [], Energy = [];
    static bool CaptureHeal(decimal amount, ref Task __result)
    { Heals.Add(amount); __result = Task.CompletedTask; return false; }
    static bool CaptureEnergy(decimal amount, ref Task __result)
    { Energy.Add(amount); __result = Task.CompletedTask; return false; }
    static bool EmptyCards(ref IEnumerable<CardModel> __result) { __result = Array.Empty<CardModel>(); return false; }
    static async Task Effect(object model, string name, params object?[] args) =>
        await (Task)AccessTools.Method(model.GetType(), name).Invoke(model, args)!;
    static T Card<T>(Fixture fixture) where T : CardModel
    {
        T card = Blank<T>(); Set(card, "<IsMutable>k__BackingField", true); Set(card, "_owner", fixture.Player);
        return card;
    }
    static T Power<T>(Creature owner, decimal amount) where T : PowerModel
    {
        T power = (T)((PowerModel)Canonical(typeof(T))).ToMutable();
        Set(power, "_owner", owner); Set(power, "_amount", (int)amount);
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(owner)!).Add(power);
        if (power is HighDimensionalShellPower) power.SetAmount((int)amount, true);
        return power;
    }
    static object?[] DefaultArgs(MethodInfo method) => method.GetParameters().Select(p => p.HasDefaultValue
        ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
    static CardPlay Play(CardModel card, Creature? target = null)
    {
        var play = Blank<CardPlay>();
        foreach (FieldInfo field in typeof(CardPlay).GetFields(F))
        {
            if (field.Name.Contains("Card") && field.FieldType == typeof(CardModel)) field.SetValue(play, card);
            if (field.Name.Contains("Target") && field.FieldType == typeof(Creature)) field.SetValue(play, target);
        }
        Check(play.Card == card && play.Target == target, "native CardPlay stores real source/target");
        return play;
    }
    static void Leaves()
    {
        var h = new Harmony("hbr.mod.fixed.resource.command.leaves");
        void Prefix(MethodBase method, string name) => h.Patch(method,
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(name, F)!));
        Prefix(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal)), nameof(CaptureHeal));
        Prefix(AccessTools.Method(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy)), nameof(CaptureEnergy));
        Prefix(AccessTools.Method(typeof(PowerModel), "Flash"), nameof(Skip));
        Prefix(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(Skip));
        Prefix(AccessTools.PropertyGetter(typeof(Creature), "CanReceivePowers"), nameof(Skip));
        Prefix(AccessTools.PropertyGetter(typeof(PlayerCombatState), "AllCards"), nameof(EmptyCards));
        Prefix(AccessTools.PropertyGetter(typeof(CardModel), "Pile"), nameof(Skip));
        Type targetTypes = Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiTargetTypes", true)!;
        foreach (PropertyInfo target in targetTypes.GetProperties(F))
            if (target.PropertyType == typeof(TargetType)) Prefix(target.GetMethod!, nameof(Skip));
        foreach (MethodInfo method in typeof(Creature).GetMethods(F).Where(m => m.Name == "InvokePowerModified")) Prefix(method, nameof(Skip));
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighDimensionalShellNativeAmountPatch", true)!).Patch();
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HighDimensionalShellPower));
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PhantomHighDimensionalShellPower));
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(MomentOfReign));
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PhantomPower));
    }
    static async Task DirectCases(bool enabled)
    {
        string mode = enabled ? "HBR" : "legacy";
        Fixture fixture = new(enabled: enabled);
        DeflectorProtection shield = fixture.Shield();
        shield.LoseDp(4000);
        int before = shield.Dp;
        OverdrawMovementHbrgoSaki overdraw = Card<OverdrawMovementHbrgoSaki>(fixture);
        await Effect(overdraw, "OnPlay", new ThrowingPlayerChoiceContext(), null);
        Equal(before - shield.Dp, enabled ? 1500 : 15, mode + " real Overdraw OnPlay consumes fixed DP15");
        Equal(overdraw.DynamicVars["DpLoss"].BaseValue, 15, "Overdraw raw cost stays15 after play");
        Call(typeof(OverdrawMovementHbrgoSaki), "OnUpgrade", overdraw);
        Equal(overdraw.DynamicVars["DpLoss"].BaseValue, 10, "real OnUpgrade changes raw15 to10");
        DynamicVar clone = overdraw.DynamicVars["DpLoss"].Clone(); clone.UpgradeValueBy(-1);
        Equal(clone.BaseValue, 9, "native Clone upgrade retains raw9");
        Equal(overdraw.DynamicVars["DpLoss"].BaseValue, 10, "clone raw changes do not leak to source");
        before = shield.Dp;
        await Effect(overdraw, "OnPlay", new ThrowingPlayerChoiceContext(), null);
        Equal(before - shield.Dp, enabled ? 1000 : 10, mode + " upgraded Overdraw consumes fixed DP10");
        Equal(Energy[^1], 2, "energy command remains2 and is never resource-scaled");
        DpPotionHbrgoSaki potion = Blank<DpPotionHbrgoSaki>(); Set(potion, "<IsMutable>k__BackingField", true); Set(potion, "_owner", fixture.Player);
        before = shield.Dp;
        await Effect(potion, "OnUse", new ThrowingPlayerChoiceContext(), fixture.Hero);
        Equal(shield.Dp - before, enabled ? 2000 : 20, mode + " real DP potion use restores20 once");
        Equal(potion.DynamicVars["Dp"].BaseValue, 20, "DP potion raw amount remains20");
        var potionDescription = new LocString("potions", "unused");
        Call(Helper("PotionDamagePreviewHelper"), "UpdateDescription", null, potion, potionDescription, null);
        var potionArgs = (IDictionary)Field(typeof(LocString), "_variables").GetValue(potionDescription)!;
        Equal((decimal)potionArgs["ResourceDp"]!, enabled ? 2000 : 20, mode + " real potion description binds actual fixed amount");

        Creature phantom = fixture.Phantom(10000); Power<PhantomPower>(phantom, 1);
        var shell = Power<PhantomHighDimensionalShellPower>(phantom, 1800);
        var binding = Card<BindingRedemptionHbrgoSaki>(fixture);
        Heals.Clear(); await Effect(binding, "OnPlay", new ThrowingPlayerChoiceContext(), Play(binding, phantom));
        Equal(shell.CurrentDp, enabled ? 2000 : 1806, mode + " binding spends fixed total on DP first");
        if (enabled) Equal(Heals.Single(), 400, "Binding6 converts to600 once then heals remaining400 after missingDP200");
        else Equal(Heals.Count, 0, "legacy Binding6 is entirely accepted by missingDP200");
        Equal(binding.DynamicVars["Recovery"].BaseValue, 6, "binding raw recovery stays6");
        shell.SetAmount(500, true);
        var reign = Relic<MomentOfReign>(fixture.Player);
        ((List<RelicModel>)Field(typeof(Player), "_relics").GetValue(fixture.Player)!).Add(reign);
        var unbroken = Power<UnbrokenRemnantPower>(fixture.Hero, 10);
        decimal dpBefore = shell.CurrentDp;
        await Effect(unbroken, "AfterPhantomDpBroken", new ThrowingPlayerChoiceContext(), phantom);
        Equal(shell.CurrentDp - dpBefore, enabled ? 1000 : 10, mode + " actual Unbroken trigger restores raw10 once");
        Equal(unbroken.Amount, 10, "Unbroken stored power amount remains10");
        decimal restoredOnce = shell.CurrentDp;
        await Effect(unbroken, "AfterPhantomDpBroken", new ThrowingPlayerChoiceContext(), phantom);
        Equal(shell.CurrentDp, restoredOnce, "Unbroken repeat trigger keeps once-per-combat consumption guard");
        decimal percentBefore = shield.Dp;
        shield.LoseDp(2000);
        Equal(shield.RestoreDpByMaxPercent(0.1m), 700, mode + " final percentage API uses10percent700, never100times");
        Equal(shield.MaxDp, 7000, mode + " DP ratio remains2perHP");
    }
    static async Task OverworkCases(bool thirtyA, bool enabled)
    {
        Fixture fixture = new(thirtyA ? Blank<HbrgoSaki30A>() : Blank<HbrgoSaki>(), enabled);
        var shield = fixture.Shield();
        Type stateType = typeof(Player).GetProperty("PlayerCombatState")!.PropertyType;
        object state = RuntimeHelpers.GetUninitializedObject(stateType);
        for (Type? type = typeof(Player); type != null; type = type.BaseType)
            foreach (FieldInfo field in type.GetFields(F)) if (!field.IsStatic && field.FieldType == stateType) field.SetValue(fixture.Player, state);
        var overwork = new OverworkHbrgoSaki(); Set(overwork, "<IsMutable>k__BackingField", true); Set(overwork, "_owner", fixture.Player);
        var played = Card<OverdrawMovementHbrgoSaki>(fixture);
        CardPlay play = Play(played);
        // This accepted callback receipt is ordinarily recorded by BeforeCardPlayed
        // after the native wrapper observes Overwork in hand. The test executes
        // the full real AfterCardPlayed consumer, not a helper imitation.
        Set(overwork, "_pendingPlays", new Dictionary<CardPlay, ICombatState> { [play] = fixture.Combat });
        Creature? phantom = null; PhantomHighDimensionalShellPower? shell = null; MomentOfReign? reign = null;
        if (thirtyA)
        {
            phantom = fixture.Phantom(10000); Power<PhantomPower>(phantom, 1); shell = Power<PhantomHighDimensionalShellPower>(phantom, 1800);
            reign = Relic<MomentOfReign>(fixture.Player);
            ((List<RelicModel>)Field(typeof(Player), "_relics").GetValue(fixture.Player)!).Add(reign);
        }
        decimal before = thirtyA ? shell!.MaximumDp : shield.MaxDp;
        await overwork.AfterCardPlayed(new ThrowingPlayerChoiceContext(), play);
        decimal after = thirtyA ? shell!.MaximumDp : shield.MaxDp;
        Equal(before - after, thirtyA ? enabled ? 300 : 3 : enabled ? 100 : 1,
            $"{(enabled ? "HBR" : "legacy")} real Overwork AfterCardPlayed {(thirtyA ? "phantom3" : "player1")} maximumDP cost");
    }
    static void DescriptionCases()
    {
        Fixture fixture = new();
        var paths = new[] { "HBRGOSakiMod/localization/zhs/cards.json", "HBRGOSakiMod/localization/zhs/potions.json" };
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        using JsonDocument cardsJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, paths[0])));
        foreach (Type type in new[] { typeof(OverdrawMovementHbrgoSaki), typeof(BindingRedemptionHbrgoSaki), typeof(PressOnThroughPainHbrgoSaki), typeof(PainTransmissionHbrgoSaki), typeof(HolyOneHbrgoSaki), typeof(RapidChargingHbrgoSaki), typeof(BorrowLifeTogetherHbrgoSaki), typeof(UnbrokenRemnantHbrgoSaki), typeof(FateHbrgoSaki), typeof(WorldsFinestHbrgoSaki), typeof(DpSoulStrikeHbrgoSaki), typeof(SelfSacrificeSlashHbrgoSaki), typeof(MercyAndCompassionHbrgoSaki), typeof(BorrowedEdgeHbrgoSaki), typeof(ShelterTheAfterimageHbrgoSaki), typeof(EmberRecoveryHbrgoSaki) })
        {
            var card = (CardModel)Activator.CreateInstance(type)!;
            Set(card, "<IsMutable>k__BackingField", true); Set(card, "_owner", fixture.Player);
            var description = new LocString("cards", "unused");
            Call(Helper("HbrFixedResourceDescriptionHelper"), "AddParameters", null, card, description);
            string key = card.Id.Entry + ".description.hbr";
            string template = cardsJson.RootElement.GetProperty(key).GetString()!;
            string[] resources = Regex.Matches(template, "\\{(Resource[^}:]+)").Select(m => m.Groups[1].Value).Distinct().ToArray();
            Check(resources.Length > 0, "HBR template has explicit resource field " + type.Name);
            var variables = (IDictionary)Field(typeof(LocString), "_variables").GetValue(description)!;
            foreach (string resource in resources) Check(variables.Contains(resource), "actual description helper binds " + type.Name + "." + resource);
            if (card is OverdrawMovementHbrgoSaki) Equal((decimal)variables["ResourceDpLoss"]!, 1500, "actual HBR Overdraw description value is1500");
            if (card is BindingRedemptionHbrgoSaki) Equal((decimal)variables["ResourceRecovery"]!, 600, "actual HBR Binding description total is600");
        }
    }
    public static async Task Run()
    {
        bool ok = false; string? error = null;
        try { Install(); Leaves(); await DirectCases(true); await DirectCases(false); await OverworkCases(false, true); await OverworkCases(true, true); await OverworkCases(false, false); await OverworkCases(true, false); DescriptionCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, cases = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            limits = "Actual mod OnPlay/OnUse/power trigger and native DP ledgers execute. Actual candidate mod card OnPlay/OnUse, Unbroken trigger, Overwork AfterCardPlayed (with a prepared accepted callback receipt) and native exact-DP SetAmount plus production attachment patch execute. Godot StringName/logging/Flash/power notifications, active-combat state, empty host card piles and custom-target registration are controlled; Heal and GainEnergy commands are captured. Unbroken's subsequent Strength application is prevented by the host CanReceivePowers leaf. Descriptions validate real helper-bound numeric parameters and all referenced Resource variables in actual HBR JSON templates; full SmartFormat/LocManager rendering is not executed. No in-game/full-save verification, packaging or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} mod fixed-resource checks");
        if (!ok) throw new Exception("Mod fixed-resource checks failed; inspect report.json");
    }
}
