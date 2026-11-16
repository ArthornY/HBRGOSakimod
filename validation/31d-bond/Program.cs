using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

public sealed class ThirtyOneDMemberFixture : CardModel, IThirtyOneDMemberCard
{
    public ThirtyOneDMember ThirtyOneDMember { get; set; }
    public ThirtyOneDMemberFixture() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }
}
static class ThirtyOneDStandalone { public static void Run(string report) => EffectChecks.RunThirtyOneD(report); }
static partial class EffectChecks
{
    static readonly List<(Player Player, decimal Count)> IntelDraws = [];
    static bool IntelDrawLeaf(decimal count, Player player, ref Task<IEnumerable<CardModel>> __result)
    { IntelDraws.Add((player, count)); __result = Task.FromResult<IEnumerable<CardModel>>([]); return false; }
    static ThirtyOneDCollection DReset(int kinds)
    {
        GardenReset(); IntelDraws.Clear();
        Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        var relic = (ThirtyOneDCollection)ModelDb.Relic<ThirtyOneDCollection>().MutableClone(); Set(relic, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Concat(new RelicModel[] { relic }).ToList());
        for (int i = 0; i < kinds; i++) DMember((ThirtyOneDMember)i);
        relic.BeforeCombatStart().GetAwaiter().GetResult(); return relic;
    }
    static ThirtyOneDMemberFixture DMember(ThirtyOneDMember member)
    {
        var card = (ThirtyOneDMemberFixture)ModelDb.Card<ThirtyOneDMemberFixture>().MutableClone();
        Set(card, "_owner", Player); card.ThirtyOneDMember = member; Player.Deck.AddInternal(card); return card;
    }
    static Task DTurn(ThirtyOneDCollection relic, int turn)
    { Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", turn); return relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player); }
    static decimal DFactor(Creature dealer, Creature target) => (decimal)Call("ThirtyOneDBondHelper", "GetDamageMultiplier", dealer, target)!;
    static async Task DIntelCases()
    {
        foreach (int kinds in new[] { 0, 1, 2, 4, 6 })
        {
            var relic = DReset(kinds);
            Equal(relic.DisplayAmount, kinds, "31D permanent member count " + kinds);
            if (kinds > 0) { DMember(ThirtyOneDMember.NikaidoMisato); Equal(relic.DisplayAmount, kinds, "duplicate member never increases the31D counter"); }
            for (int turn = 1; turn <= Math.Max(3, kinds); turn++)
            {
                await DTurn(relic, turn);
                int expected = kinds < 2 ? 0 : Math.Min(kinds, turn);
                Equal(Hero.GetPowerAmount<HbrIntelPower>(), expected, "real turn callback accumulates/caps Intel for " + kinds + " kinds turn" + turn);
                await DTurn(relic, turn);
                Equal(Hero.GetPowerAmount<HbrIntelPower>(), expected, "repeated same-turn callback is idempotent");
                if (kinds < 6 || turn < 6) Check(!relic.IntelRewardGiven && IntelDraws.Count == 0, "no early six-Intel reward");
            }
            if (kinds < 2) Check(!Hero.HasPower<HbrIntelPower>(), "one/zero31D kind installs no useless Intel power");
            Equal(DFactor(Hero, Enemy), 1m + (kinds < 2 ? 0 : kinds) * .05m, "Intel has one independent linear damage factor");
            if (kinds == 6)
            {
                Check(relic.IntelRewardGiven && Hero.GetPowerAmount<HbrMoralePower>() == 4 && IntelDraws.SequenceEqual(new[] { (Player, 2m) }), "sixth turn grants actual Morale4 and ownerDraw2 once");
                await DTurn(relic, 7); await DTurn(relic, 8);
                Check(Hero.GetPowerAmount<HbrMoralePower>() == 4 && IntelDraws.Count == 1, "capped Intel never replays the six-kind reward");
            }
            await relic.BeforeCombatStart();
            Check(!Hero.HasPower<HbrIntelPower>() && !relic.IntelRewardGiven, "new combat resets Intel0 and the per-combat reward receipt");
        }
        var dynamic = DReset(4); for (int turn = 1; turn <= 4; turn++) await DTurn(dynamic, turn);
        foreach (CardModel card in Player.Deck.Cards.Where(card => card is ThirtyOneDMemberFixture member && (int)member.ThirtyOneDMember >= 2).ToArray()) Player.Deck.RemoveInternal(card);
        await DTurn(dynamic, 5); Equal(Hero.GetPowerAmount<HbrIntelPower>(), 2m, "turn start rereads a reduced permanent roster and clamps Intel to its current cap");
        foreach (CardModel card in Player.Deck.Cards.Where(card => card is ThirtyOneDMemberFixture member && member.ThirtyOneDMember != ThirtyOneDMember.NikaidoMisato).ToArray()) Player.Deck.RemoveInternal(card);
        await DTurn(dynamic, 6); Check(!Hero.HasPower<HbrIntelPower>(), "dropping to one member removes Intel at the next own turn");
        var midturn = DReset(6); Power<HbrIntelPower>(Hero, 6);
        foreach (CardModel card in Player.Deck.Cards.Where(card => card is ThirtyOneDMemberFixture member && (int)member.ThirtyOneDMember >= 2).ToArray()) Player.Deck.RemoveInternal(card);
        Equal(DFactor(Hero, Enemy), 1.1m, "midturn roster6to2 immediately uses only two effective Intel without deleting stored layers");
        Check(Hero.GetPower<HbrIntelPower>()!.Amount == 6 && Hero.GetPower<HbrIntelPower>()!.DisplayAmount == 2,
            "midturn Intel counter shows effective2 while stored layers await the next scheduled turn");
        string intelText = Hero.GetPower<HbrIntelPower>()!.Description.GetFormattedText();
        string relicText = midturn.DynamicDescription.GetFormattedText();
        Check(intelText.Contains("当前2份") && intelText.Contains("+10%") && relicText.Contains("情报2/2"),
            "actual Intel and Collection tooltips agree with the midturn ten-percent damage factor and current2cap");
    }
    static async Task DTeamCases()
    {
        var relic = DReset(6); Player peer = SupportTeammate(); Set(RunState, "_players", new List<Player> { Player, peer });
        Creature phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Creature peerPhantom = (Creature)H("Actor", Blank<SmallHopper>(), null, peer)!;
        Creature unrelatedPet = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Power<PhantomPower>(phantom, 1); Power<PhantomPower>(peerPhantom, 1);
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature, phantom, peerPhantom, unrelatedPet });
        await PowerCmd.Apply<HbrMoralePower>(new ThrowingPlayerChoiceContext(), peer.Creature, 8m, Hero, null);
        for (int turn = 1; turn <= 6; turn++) await DTurn(relic, turn);
        Check(Hero.GetPowerAmount<HbrMoralePower>() == 4 && peer.Creature.GetPowerAmount<HbrMoralePower>() == 10 &&
            phantom.GetPowerAmount<HbrMoralePower>() == 4 && peerPhantom.GetPowerAmount<HbrMoralePower>() == 4,
            "six Intel gives actual Morale to both players and their independent phantoms, respecting level10");
        Check(!unrelatedPet.HasPower<HbrMoralePower>() && IntelDraws.Count == 1 && IntelDraws[0].Player == Player,
            "nonphantom pets do not receive a second owner-scaled Morale; only the collecting player draws");
        Equal(DFactor(phantom, Enemy), 1.3m, "real pet dealer inherits its own summoner's Intel");
        Equal(DFactor(peerPhantom, Enemy), 1m, "another player's phantom cannot borrow the collector's Intel");
        Equal(DFactor(Hero, peer.Creature), 1m, "Intel never amplifies friendly damage");
        Equal(DFactor(Enemy, Hero), 1m, "Intel never amplifies enemy damage");
        await relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), peer);
        Check(!peer.Creature.HasPower<HbrIntelPower>(), "teammate turn callback cannot collect the owner's Intel");
    }
    static async Task DExtraTurnCases()
    {
        var relic = DReset(6); await DTurn(relic, 1);
        Type personal = Helper("PersonalImmediateTurnCoordinator");
        FieldInfo current = personal.GetField("_current", F)!, battle = personal.GetField("_combat", F)!;
        object? before = current.GetValue(null), oldCombat = battle.GetValue(null);
        try
        {
            object session = Activator.CreateInstance(personal.GetNestedType("Session", F)!, true)!;
            Set(session, "Owner", Player); Set(session, "Combat", Combat); current.SetValue(null, session); battle.SetValue(null, Combat);
            await DTurn(relic, 1); Equal(Hero.GetPowerAmount<HbrIntelPower>(), 2m, "own personal extra turn collects its distinct Intel");
            current.SetValue(null, null); await DTurn(relic, 1); Equal(Hero.GetPowerAmount<HbrIntelPower>(), 2m, "resume ordinary turn cannot collect twice after a personal turn");
            await DTurn(relic, 2); Equal(Hero.GetPowerAmount<HbrIntelPower>(), 3m, "native extra turn with a new ownTurnNumber in the same combat round collects Intel");
        }
        finally { current.SetValue(null, before); battle.SetValue(null, oldCombat); }
    }
    static async Task DDamageCases()
    {
        var relic = DReset(2); decimal before = Actual(Enemy, Hero, 10000m, ValueProp.Unpowered, null);
        await DTurn(relic, 1); await DTurn(relic, 2);
        Equal(Actual(Enemy, Hero, 10000m, ValueProp.Unpowered, null), decimal.Floor(before * 1.1m), "real shared damage pipeline applies Intel once as a separate final multiplier");
        Creature phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Power<PhantomPower>(phantom, 1);
        decimal powered = Actual(Enemy, phantom, 10000m, ValueProp.Unpowered, null);
        Powers(Hero).Remove(Hero.GetPower<HbrIntelPower>()!);
        decimal unboosted = Actual(Enemy, phantom, 10000m, ValueProp.Unpowered, null);
        Equal(powered, decimal.Floor(unboosted * 1.1m), "real phantom damage pipeline inherits exactly its summoner's independent Intel factor");
    }
    static void DSaveAndPresentationCases()
    {
        var relic = DReset(2);
        Native(relic, "SetScoutingData", "{\"act\":1,\"known\":[\"event\",\"宝箱\"],\"viewers\":[1,2]}");
        Check((bool)Native(relic, "TryReserveIntelReward")!, "per-combat reward receipt reserves once before asynchronous grants");
        Check(!(bool)Native(relic, "TryReserveIntelReward")!, "a nested reward reservation is rejected");
        Assembly native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } legacy)
            legacy.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [typeof(ThirtyOneDCollection)]);
        else
        {
            Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
            cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [typeof(ThirtyOneDCollection)]);
            cache.GetField("_initialized", F)!.SetValue(null, true);
        }
        var options = new JsonSerializerOptions { IncludeFields = true };
        string payload = JsonSerializer.Serialize(SavedProperties.From(relic), options);
        var restored = (ThirtyOneDCollection)ModelDb.Relic<ThirtyOneDCollection>().MutableClone();
        JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(restored);
        Check(restored.ScoutingData == relic.ScoutingData && restored.IntelRewardGiven,
            "real native SavedProperties From/JSON/Fill preserves shared scouting ledger and per-combat reward receipt");
        Type ui = Mod.GetType("HBRGOSakiMod.Core.Patches.ThirtyOneDScoutingMapUi", true)!;
        foreach (var row in new[] { (RoomType.Monster, "map_monster"), (RoomType.Shop, "map_shop"), (RoomType.Treasure, "map_chest"), (RoomType.Event, "map_unknown") })
            Check((string)Invoke(ui.GetMethod("IconName", F)!, null, row.Item1)! == row.Item2,
                "scouting uses the real native room icon name for " + row.Item1);
        foreach (var size in new[] { (16f, 16f), (32f, 32f), (64f, 64f), (96f, 72f), (128f, 128f), (0f, 0f) })
        {
            var placement = ((float Diameter, float X, float Y))Invoke(ui.GetMethod("BadgePlacement", F)!, null, size.Item1, size.Item2)!;
            float nativeSize = Math.Min(size.Item1, size.Item2);
            Check(Math.Abs(placement.Diameter * 3f - (nativeSize > 0f ? nativeSize : 64f)) < .001f &&
                placement.X < 0f && placement.Y < 0f && placement.X + placement.Diameter > 0f,
                "real badge geometry stays one-third size at the upper-right corner for " + size);
        }
        Type rewardArray = Mod.GetType("HBRGOSakiMod.Core.Map.HbrScoutedRelic", true)!.MakeArrayType();
        object rewards = JsonSerializer.Deserialize("[{\"PlayerId\":11},{\"IsBonus\":true},{\"IsBonus\":true}]", rewardArray)!;
        Check((string)Invoke(ui.GetMethod("RewardRecipient", F)!, null, rewards, new ulong[] { 11, 22 })! == "玩家1、额外遗物×2：",
            "real treasure label retains both a normal owner and two same-item bonus slots");
        object onlyBonus = JsonSerializer.Deserialize("[{\"IsBonus\":true}]", rewardArray)!;
        Check((string)Invoke(ui.GetMethod("RewardRecipient", F)!, null, onlyBonus, new ulong[] { 11 })! == "额外遗物：",
            "a singleplayer bonus relic is labeled as extra instead of assigned to a fictitious player0");
        string source = File.ReadAllText(Path.Combine(RepositoryRoot, "Core/Patches/ThirtyOneDScoutingMapUiPatch.cs"));
        Check(!source.Contains(".PointType =") && !source.Contains("_Process("), "map presentation never mutates a real point type or scans every frame");
        Check(source.Contains("HbrThirtyOneDScoutingHelper.CanView(LocalContext.GetMe(run))") && source.Contains("HbrThirtyOneDScoutingHelper.GetKnownPoint(run, point)"),
            "local map visibility and current content come only from the shared saved scouting service");
        Check(source.Contains("HoverTipCompatibility.Copy(withIcon") && source.Contains("tip.Description, relic.Icon") && source.Contains("reward.PlayerId"),
            "treasure hover preserves actual relic icon/description and labels each player's assigned item");
        Check(source.Contains("definition.Pages.SelectMany(page => page.MetCharacters)") && source.Contains("HbrMemoryGarden.CharacterIdOf(card)"),
            "memory hover names real encountered characters and their actual implemented garden cards");
        Check(source.Contains("Priority.Last") && source.Contains("Priority.First") && source.Contains("Control.MouseFilterEnum.Ignore") && source.Contains("/ 3f"),
            "scouting icon/hover runs after memory visuals and its noninteractive badge is one third of the icon");
    }
    static async Task DCases()
    {
        foreach (Type type in new[] { typeof(ThirtyOneDCollection), typeof(HbrIntelPower), typeof(HbrMoralePower), typeof(ThirtyOneDMemberFixture), typeof(PhantomPower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var harmony = new Harmony(ProductionId + ".31d");
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" && method.GetParameters().Length == 4), nameof(IntelDrawLeaf));
            harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.ThirtyOneDDescriptionPatch", true)!).Patch();
            await DIntelCases(); await DTeamCases(); await DExtraTurnCases(); await DDamageCases(); DSaveAndPresentationCases();
        }
        finally { harmony.UnpatchAll(harmony.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunThirtyOneD(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); DCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real permanent member counting, native PowerCmd applications, real turn callbacks and shared hero/pet damage pipeline. Draw and presentation/wait leaves are isolated; no Godot process or rendered map/input session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " 31D checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
