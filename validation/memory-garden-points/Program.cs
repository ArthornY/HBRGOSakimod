using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using Environment = System.Environment;

// A deliberately different real StartingDeck getter proves the implementation
// does not use the three current characters' coincidental ten-card baseline.
public sealed class SevenCardCharacter : HbrgoSaki
{
    public static int Reads;
    public override IEnumerable<CardModel> StartingDeck
    {
        get { Interlocked.Increment(ref Reads); return Enumerable.Repeat(ModelDb.Card<StrikeHbrgoSaki>(), 7); }
    }
}

static class GardenPointsStandalone
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> Cases = [];
    static readonly string[] Senses = ["LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity", "Madness"];
    static Type Helper => Mod.GetType("HBRGOSakiMod.Core.Helpers.MemoryGardenUpgradePointsHelper", true)!;
    static FieldInfo Field(Type type, string name)
    {
        for (Type? next = type; next != null; next = next.BaseType)
            if (next.GetField(name, F) is { } result) return result;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object value, string field, object? data) => Field(value.GetType(), field).SetValue(value, data);
    static void Property(ElectronicSoldierManual value, string name, object? data) => value.GetType().GetProperty(name, F)!.SetValue(value, data);
    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? Call(ElectronicSoldierManual owner, string name, params object?[] args) => Invoke(typeof(ElectronicSoldierManual).GetMethod(name, F)!, owner, args);
    static void Sync(ElectronicSoldierManual manual) => Call(manual, "SynchronizeMemoryGardenUpgradePoints");
    static void Check(bool success, string message)
    { if (!success) throw new Exception(message); Passed.Add(message); }
    static void Equal(int actual, int expected, string message) => Check(actual == expected, $"{message}: {actual} = {expected}");
    static bool Skip() => false;
    static bool EmptyTask(ref Task __result) { __result = Task.CompletedTask; return false; }
    static void SavedType(Type type)
    {
        Assembly native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } cache)
            cache.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [type]);
        else
        {
            Type latest = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
            latest.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]);
            latest.GetField("_initialized", F)!.SetValue(null, true);
        }
    }
    static void Inject(Type type)
    {
        if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
    }
    static void Setup()
    {
        TestMode.IsOn = true;
        var harmony = new Harmony("hbr.garden-points.native-checks");
        harmony.Patch(typeof(StringName).GetConstructor([typeof(string)])!, prefix: new HarmonyMethod(typeof(GardenPointsStandalone).GetMethod(nameof(Skip), F)!));
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        foreach (MethodInfo method in logger.GetMethods(F).Where(m => m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(GardenPointsStandalone).GetMethod(nameof(Skip), F)!));
        foreach (MethodInfo method in typeof(RelicModel).GetMethods(F).Where(m => m.Name == "Flash"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(GardenPointsStandalone).GetMethod(nameof(Skip), F)!));
        // The production permanent-pile hook itself remains real. Its unrelated
        // shared-upgrade, team-marker and story refresh leaves are out of scope.
        foreach ((string type, string method) in new[] { ("HbrMemoryGardenProgression", "Synchronize"), ("HbrMemoryGardenRestStory", "RefreshPermanentCards") })
            foreach (MethodInfo entry in Mod.GetType("HBRGOSakiMod.Core.Helpers." + type, true)!.GetMethods(F).Where(m => m.Name == method))
                harmony.Patch(entry, prefix: new HarmonyMethod(typeof(GardenPointsStandalone).GetMethod(nameof(Skip), F)!));
        foreach ((string type, string method) in new[] { ("TeamCollectionRelics", "EnsureObtained"), ("ThirtyACollectionUpgrades", "Resolve") })
            foreach (MethodInfo entry in Mod.GetType("HBRGOSakiMod.Core.Helpers." + type, true)!.GetMethods(F).Where(m => m.Name == method))
                harmony.Patch(entry, prefix: new HarmonyMethod(typeof(GardenPointsStandalone).GetMethod(nameof(EmptyTask), F)!));
        foreach (Type type in new[] { typeof(ElectronicSoldierManual), typeof(ElectronicSoldierManual31O), typeof(HotaruElectronicSoldierManual),
            typeof(StrikeHbrgoSaki), typeof(DefendHbrgoSaki), typeof(MagicSwordLuckySevenHbrgoSaki), typeof(ChihayaClassmateHbrgoSaki),
            typeof(StrengthAmplificationHbrgoSaki), typeof(CommandStrikeHbrgoSaki), typeof(HotaruStrike), typeof(HotaruDefend), typeof(HotaruStimulate), typeof(HotaruCharge) }) Inject(type);
        foreach (Type type in new[] { typeof(ElectronicSoldierManual), typeof(ElectronicSoldierManual31O), typeof(HotaruElectronicSoldierManual) }) SavedType(type);
    }
    static ElectronicSoldierManual Manual(Type? type = null, Player? owner = null)
    {
        type ??= typeof(ElectronicSoldierManual);
        var value = (ElectronicSoldierManual)ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();
        if (owner != null) { Set(value, "_owner", owner); Set(owner, "_relics", new List<RelicModel> { value }); }
        return value;
    }
    static Player Owner(CharacterModel character, int size)
    {
        var owner = Blank<Player>(); Set(owner, "<Character>k__BackingField", character);
        Set(owner, "<Deck>k__BackingField", new CardPile(PileType.Deck)); Set(owner, "_relics", new List<RelicModel>());
        Resize(owner, size); return owner;
    }
    static CardModel Card(Player player)
    {
        var value = (CardModel)ModelDb.Card<StrikeHbrgoSaki>().ToMutable(); Set(value, "_owner", player); return value;
    }
    static void Resize(Player player, int count)
    {
        while (player.Deck.Cards.Count > count) player.Deck.RemoveInternal(player.Deck.Cards.Last());
        while (player.Deck.Cards.Count < count) player.Deck.AddInternal(Card(player));
    }
    static void Total(ElectronicSoldierManual manual, int total)
    {
        foreach (string property in Senses) Property(manual, property, 0);
        Property(manual, "SixSensePoints", total);
    }
    static void SixSenseCases()
    {
        var manual = Manual();
        foreach (int total in new[] { 0, 15, 29, 30, 59, 60, 61, 119, 120, 179, 180, 594 })
        {
            Total(manual, total); Sync(manual);
            Equal(manual.MemoryGardenSixSenseUpgradeTier, total / 60, "new six-sense milestone " + total);
            Equal(manual.MemoryGardenUpgradePoints, total / 60, "new six-sense grant " + total);
            Sync(manual); Equal(manual.MemoryGardenUpgradePoints, total / 60, "idempotent synchronize " + total);
        }
        Equal(manual.MemoryGardenSixSenseRuleVersion, 1, "fresh manual records current six-sense rule");
        manual = Manual(); Total(manual, 60); Sync(manual);
        using ((IDisposable)Invoke(Helper.GetMethod("BeginBalanceUpdate", F)!, null, manual)!)
        {
            Property(manual, "Madness", 60); Sync(manual);
            Equal(manual.MemoryGardenUpgradePoints, 1, "allocation suppresses transient levels plus old pool double count");
            Property(manual, "SixSensePoints", 0); Sync(manual);
        }
        Equal((int)(long)Invoke(Helper.GetMethod("GetTotalSixSensePoints", F)!, null, manual)!, 60, "allocation moves points without changing combined total");
        Equal(manual.MemoryGardenUpgradePoints, 1, "allocation earns no duplicate point");
        Property(manual, "SixSensePoints", 60); Sync(manual);
        Equal(manual.MemoryGardenUpgradePoints, 2, "allocated and unallocated points both count");
        Property(manual, "SixSensePoints", 0); Sync(manual);
        Property(manual, "SixSensePoints", 60); Sync(manual);
        Equal(manual.MemoryGardenUpgradePoints, 2, "decrease and restoration below sense high water cannot farm points");
    }
    static void MigrationCases()
    {
        foreach ((int oldTier, int balance) in new[] { (0, 0), (1, 0), (2, 1), (9, 0), (10, 3), (11, 7), (19, 23) })
        {
            var manual = Manual(); Property(manual, "MemoryGardenSixSenseUpgradeTier", oldTier); Property(manual, "MemoryGardenUpgradePoints", balance);
            int converted = oldTier / 2; Total(manual, converted * 60); Sync(manual);
            Equal(manual.MemoryGardenSixSenseUpgradeTier, converted, "legacy receipt converts 30-tier to60-tier " + oldTier);
            Equal(manual.MemoryGardenUpgradePoints, balance, "migration preserves earned and spent balance " + oldTier);
            for (int i = 0; i < 5; i++) Sync(manual);
            Equal(manual.MemoryGardenSixSenseUpgradeTier, converted, "conversion occurs exactly once " + oldTier);
            Equal(manual.MemoryGardenUpgradePoints, balance, "repeat sync does not grant migration points " + oldTier);
            if (converted < 9)
            {
                Total(manual, (converted + 1) * 60); Sync(manual);
                Equal(manual.MemoryGardenUpgradePoints, balance + 1, "next new sixty-point boundary grants normally " + oldTier);
            }
        }
        var old = Manual(); Property(old, "MemoryGardenSixSenseUpgradeTier", 10); Property(old, "MemoryGardenUpgradePoints", 0); Total(old, 300);
        var loadedOld = Roundtrip(old, typeof(HotaruElectronicSoldierManual)); Sync(loadedOld);
        Equal(loadedOld.MemoryGardenSixSenseUpgradeTier, 5, "absent version in real old saved payload migrates after load");
        Equal(loadedOld.MemoryGardenUpgradePoints, 0, "loading spent old balance never refunds old spending");
        var fresh = Roundtrip(loadedOld, typeof(HotaruElectronicSoldierManual)); Sync(fresh);
        Equal(fresh.MemoryGardenSixSenseUpgradeTier, 5, "saved version prevents second conversion after reload");
        Equal(fresh.MemoryGardenSixSenseRuleVersion, 1, "derived native save roundtrip retains version");
    }
    static void DeckCases()
    {
        foreach (CharacterModel character in new CharacterModel[] { Blank<HbrgoSaki31O>(), Blank<HbrgoSaki30A>(), Blank<HbrgoHotaru>() })
        {
            Equal(character.StartingDeck.Count(), 10, "actual current character starting deck count " + character.GetType().Name);
            Player player = Owner(character, 10); var manual = Manual(owner: player); Sync(manual);
            Equal(manual.MemoryGardenInitialDeckSize, character.StartingDeck.Count(), "saved baseline comes from actual character " + character.GetType().Name);
            foreach (int size in new[] { 0, 9, 10, 11, 12, 13, 14, 16, 10, 13, 16, 18, 19 })
            {
                Resize(player, size); Sync(manual);
                int expected = Math.Max(0, (manual.MemoryGardenDeckHighWater - 10) / 3);
                Equal(manual.MemoryGardenUpgradePoints, expected, character.GetType().Name + " permanent deck high-water grant size" + size);
                Equal(manual.MemoryGardenDeckUpgradeTier, expected, "deck reward receipt matches highest net growth " + size);
            }
            Equal(manual.MemoryGardenUpgradePoints, 3, "delete and re-add below19 produces no extra entitlement " + character.GetType().Name);
            Equal(manual.MemoryGardenDeckHighWater, 19, "deck high water persists " + character.GetType().Name);
            var temporary = new CardPile(PileType.Hand); temporary.AddInternal(Card(player));
            Sync(manual); Equal(manual.MemoryGardenUpgradePoints, 3, "combat-only temporary card does not increase permanent deck entitlement");
            Resize(player, 28); Sync(manual); Equal(manual.MemoryGardenUpgradePoints, 6, "bulk permanent additions grant every newly crossed milestone");
        }
        SevenCardCharacter.Reads = 0;
        var seven = Owner(Blank<SevenCardCharacter>(), 7); var special = Manual(owner: seven);
        for (int i = 0; i < 10; i++) Sync(special);
        Equal(special.MemoryGardenInitialDeckSize, 7, "non-ten actual StartingDeck controls baseline");
        Equal(SevenCardCharacter.Reads, 1, "real StartingDeck getter is read once across repeated synchronize");
        Resize(seven, 10); Sync(special); Equal(special.MemoryGardenUpgradePoints, 1, "custom starting baseline still grants net three");
        Equal(SevenCardCharacter.Reads, 1, "new growth uses saved baseline rather than rebuilding StartingDeck");
        var existing = Owner(Blank<HbrgoSaki31O>(), 19); var legacy = Manual(owner: existing); Sync(legacy);
        Equal(legacy.MemoryGardenInitialDeckSize, 10, "old run baseline comes from original starting deck, not current size");
        Equal(legacy.MemoryGardenUpgradePoints, 3, "old run receives its current genuine net-growth entitlement once");
        Sync(legacy); Equal(legacy.MemoryGardenUpgradePoints, 3, "old run deck initialization is idempotent");
    }
    static ElectronicSoldierManual Roundtrip(ElectronicSoldierManual source, Type type, Player? owner = null)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize(SavedProperties.From(source), options);
        var result = Manual(type, owner); JsonSerializer.Deserialize<SavedProperties>(json, options)!.Fill(result);
        Cases.Add(new { kind = "native SavedProperties From/JSON/Fill", source = source.GetType().Name, target = type.Name, payload = json });
        return result;
    }
    static void SaveAndInheritanceCases()
    {
        foreach (Type type in new[] { typeof(ElectronicSoldierManual), typeof(ElectronicSoldierManual31O), typeof(HotaruElectronicSoldierManual) })
        {
            Player player = Owner(Blank<SevenCardCharacter>(), 16); var manual = Manual(type, player); Total(manual, 300); Sync(manual);
            Call(manual, "TrySpendMemoryGardenUpgradePoint"); Call(manual, "TrySpendMemoryGardenUpgradePoint");
            int balance = manual.MemoryGardenUpgradePoints;
            Resize(player, 10); var loaded = Roundtrip(manual, type, player); SevenCardCharacter.Reads = 0; Sync(loaded);
            Equal(loaded.MemoryGardenUpgradePoints, balance, "native derived save preserves spend and cannot reclaim current deck " + type.Name);
            Equal(loaded.MemoryGardenInitialDeckSize, 7, "native save restores original baseline " + type.Name);
            Equal(loaded.MemoryGardenDeckHighWater, 16, "native save restores historical highest permanent size " + type.Name);
            Equal(loaded.MemoryGardenDeckUpgradeTier, 3, "native save restores claimed deck tiers " + type.Name);
            Equal(loaded.MemoryGardenDeckRuleVersion, 1, "native save restores initialized-deck receipt " + type.Name);
            Equal(loaded.MemoryGardenSixSenseUpgradeTier, 5, "native save restores converted-six-sense receipt " + type.Name);
            Equal(SevenCardCharacter.Reads, 0, "reload does not query StartingDeck again " + type.Name);
            var replacement = Manual(type, player); Total(replacement, 0);
            Call(replacement, "InheritMemoryProgressFrom", loaded); Sync(replacement);
            Equal(replacement.MemoryGardenUpgradePoints, balance, "manual replacement keeps independent point balance " + type.Name);
            Equal(replacement.MemoryGardenDeckHighWater, 16, "manual replacement inherits deck high water " + type.Name);
            Equal(replacement.MemoryGardenSixSenseRuleVersion, 1, "manual replacement cannot repeat migration " + type.Name);
            Resize(player, 16); Sync(replacement); Equal(replacement.MemoryGardenUpgradePoints, balance, "replacement plus removed cards restored cannot farm " + type.Name);
            Resize(player, 19); Sync(replacement); Equal(replacement.MemoryGardenUpgradePoints, balance + 1, "replacement can earn next genuinely new net-three tier " + type.Name);
        }
    }
    static async Task NativePileHookCases()
    {
        Player player = Owner(Blank<HbrgoSaki31O>(), 12); var manual = Manual(owner: player); Sync(manual);
        CardModel card = Card(player); player.Deck.AddInternal(card);
        await manual.AfterCardChangedPiles(card, PileType.None, null!);
        Equal(manual.MemoryGardenUpgradePoints, 1, "real owner permanent-pile hook immediately awards net-third added card");
        player.Deck.RemoveInternal(card); await manual.AfterCardChangedPiles(card, PileType.Deck, null!);
        player.Deck.AddInternal(card); await manual.AfterCardChangedPiles(card, PileType.None, null!);
        Equal(manual.MemoryGardenUpgradePoints, 1, "real removal/re-add callbacks cannot duplicate award");
        Resize(player, 16);
        var other = Owner(Blank<HbrgoSaki31O>(), 10); var otherCard = Card(other); other.Deck.AddInternal(otherCard);
        await manual.AfterCardChangedPiles(otherCard, PileType.None, null!);
        Equal(manual.MemoryGardenUpgradePoints, 1, "other owner's permanent-pile callback cannot credit this manual");
        var hand = new CardPile(PileType.Hand); var handCard = Card(player); hand.AddInternal(handCard);
        await manual.AfterCardChangedPiles(handCard, PileType.Draw, null!);
        Equal(manual.MemoryGardenUpgradePoints, 1, "nonpermanent hand/draw callback cannot opportunistically claim deck growth");
        Sync(manual); Equal(manual.MemoryGardenUpgradePoints, 2, "fallback synchronization awards pending genuine deck changes");
    }
    static void ReentryConcurrencyCases()
    {
        var manual = Manual(); Total(manual, 360); Property(manual, "MemoryGardenSixSenseUpgradeTier", 11); Property(manual, "MemoryGardenUpgradePoints", 9);
        Parallel.For(0, 128, _ => Sync(manual));
        Equal(manual.MemoryGardenSixSenseRuleVersion, 1, "parallel old-save migration publishes one current-rule marker");
        Equal(manual.MemoryGardenSixSenseUpgradeTier, 6, "parallel old-save migration converts once then claims one new tier");
        Equal(manual.MemoryGardenUpgradePoints, 10, "parallel migration and six-sense claim grants delta only once");
        var owner = Owner(Blank<SevenCardCharacter>(), 19); manual = Manual(owner: owner); SevenCardCharacter.Reads = 0;
        Parallel.For(0, 128, _ => Sync(manual));
        Equal(SevenCardCharacter.Reads, 1, "parallel first sync reads actual starting deck exactly once");
        Equal(manual.MemoryGardenUpgradePoints, 4, "parallel deck growth claims all net-three tiers once");
        int notifications = 0; bool reenter = false;
        Action<ElectronicSoldierManual> observer = changed =>
        {
            if (!ReferenceEquals(changed, manual)) return;
            Interlocked.Increment(ref notifications);
            if (reenter) return; reenter = true; try { Sync(changed); } finally { reenter = false; }
        };
        EventInfo ev = Helper.GetEvent("UpgradePointsChanged", F)!; ev.GetAddMethod(true)!.Invoke(null, [observer]);
        try
        {
            Resize(owner, 22); Sync(manual); Equal(manual.MemoryGardenUpgradePoints, 5, "synchronous UI observer reentry cannot duplicate deck reward");
            Total(manual, 60); Sync(manual); Equal(manual.MemoryGardenUpgradePoints, 6, "synchronous UI observer reentry cannot duplicate sense reward");
            Equal(notifications, 2, "each committed source grant emits one observer notification");
        }
        finally { ev.GetRemoveMethod(true)!.Invoke(null, [observer]); }
        manual = Manual();
        Check((bool)Call(manual, "ClaimMemoryGardenRoomPoints", "same-fire", 1)!, "existing fire receipt still awards once");
        Check(!(bool)Call(manual, "ClaimMemoryGardenRoomPoints", "same-fire", 1)!, "same physical fire receipt remains idempotent");
        Check((bool)Call(manual, "TrySpendMemoryGardenUpgradePoint")!, "existing balance can still be spent");
        Check(!(bool)Call(manual, "TrySpendMemoryGardenUpgradePoint")!, "spent empty balance cannot go negative");
        Call(manual, "RefundMemoryGardenUpgradePoint"); Equal(manual.MemoryGardenUpgradePoints, 1, "failed-purchase refund remains intact");
        Property(manual, "MemoryGardenUpgradePoints", int.MaxValue - 1); Call(manual, "AddMemoryGardenUpgradePoints", int.MaxValue);
        Equal(manual.MemoryGardenUpgradePoints, int.MaxValue, "new and existing rewards preserve saturating balance semantics");
    }
    public static void Run(string report)
    {
        Exception? failed = null;
        try
        {
            Setup(); SixSenseCases(); MigrationCases(); DeckCases(); SaveAndInheritanceCases();
            NativePileHookCases().GetAwaiter().GetResult(); ReentryConcurrencyCases();
        }
        catch (Exception error) { failed = error; }
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(new { success = failed == null, count = Passed.Count,
            mod = Mod.Location, modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            native = typeof(ModelDb).Assembly.Location, nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"), passed = Passed, cases = Cases,
            boundaries = "Actual native models/StartingDeck/CardPile/production sync, migration, claims, inheritance, callback and SavedProperties. Controlled StringName/log/Flash plus unrelated team/garden continuation leaves. No Godot render, gameplay session, network or distributable build.",
            error = failed?.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Garden points: {Passed.Count} checks, success={failed == null}");
        if (failed != null) ExceptionDispatchInfo.Capture(failed).Throw();
    }
}
