using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static class MonakaStandalone { public static void Run(string path) => EffectChecks.RunMonaka(path); }
static partial class EffectChecks
{
    sealed record MonakaAttack(decimal Units, decimal Multiplier, decimal Destruction, int Hits, decimal Faith);
    static readonly List<MonakaAttack> MonakaAttacks = [];
    static AttackCommand? MonakaCommand;
    static Creature? MonakaSecondEnemy;
    static bool MonakaDirect(AttackCommand command, ref bool __result)
    { __result = ReferenceEquals(command, MonakaCommand); return false; }
    static decimal MonakaFaith() => (decimal)(System.Numerics.BigInteger)Call("HbrFaithResource", "Get", Hero)!;
    static void MonakaSetFaith(int value)
    { Call("HbrFaithResource", "Clear", Hero); Call("HbrFaithResource", "Gain", Hero, new System.Numerics.BigInteger(value)); }
    static bool MonakaAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        MonakaCommand = __instance;
        CardModel card = (CardModel)__instance.ModelSource!;
        MonakaAttacks.Add(new((decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!,
            (decimal)Call("MonakaWindForestCombatHelper", "GetConditionalDamageMultiplier", card, Hero, Enemy, ValueProp.Move)!,
            (decimal)Call("MonakaWindForestCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!,
            (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(__instance)!, MonakaFaith()));
        DamageResult Result(Creature target, int blocked)
        { var result = Blank<DamageResult>(); Set(result, "<Receiver>k__BackingField", target); Set(result, "<BlockedDamage>k__BackingField", blocked); return result; }
        var one = Result(Enemy, 1); Call("MonakaWindForestCombatHelper", "RecordDamage", card, one); Call("MonakaWindForestCombatHelper", "RecordDamage", card, one);
        if (MonakaSecondEnemy != null) Call("MonakaWindForestCombatHelper", "RecordDamage", card, Result(MonakaSecondEnemy, 1));
        MonakaCommand = null;
        Equal((decimal)Call("MonakaWindForestCombatHelper", "GetConditionalDamageMultiplier", card, Hero, Enemy, ValueProp.Move)!, 1m,
            "75 nested unrelated powered damage cannot inherit the active Monaka snapshot multiplier");
        __result = Task.FromResult(__instance); return false;
    }
    static WindForestFireMountainHbrgoSaki MonakaCard(int faith, params int[] upgrades)
    {
        GardenReset(); MonakaAttacks.Clear(); MonakaSecondEnemy = null;
        Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        Set(Player.PlayerCombatState!, "_energy", 20); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        var card = GardenCard<WindForestFireMountainHbrgoSaki>(upgrades); MonakaSetFaith(faith); return card;
    }
    static async Task MonakaPlay(CardModel card, int index = 0)
    {
        var play = Play(card); Set(play, "<Target>k__BackingField", Enemy); Set(play, "<PlayIndex>k__BackingField", index);
        await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
    }
    static async Task MonakaBodyCases()
    {
        foreach (int faith in new[] { 0, 5, 10, 12 })
        {
            var card = MonakaCard(faith);
            Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), 2, "75 base energy cost remains two");
            await MonakaPlay(card);
            var hit = MonakaAttacks.Single();
            Equal(hit.Units, 232m * 6m / 100m, "75 native base request is232percent"); Equal(hit.Hits, 1, "75 base is one all-enemy hit");
            Equal(hit.Multiplier, 1m + Math.Min(faith, 10) * .16m, "75 faith contribution caps at ten points for starting faith " + faith);
            Equal(hit.Destruction, 1.6m / 3m, "75 original destruction1.6 is normalized once");
            Equal(MonakaFaith(), faith + 1, "75 actual card gives one fixed Faith after its attack");
        }
        var flash = MonakaCard(0, 0); Equal(flash.EnergyCost.GetWithModifiers(CostModifiers.All), 1, "75 Flash really changes native cost to one");
        var merit = MonakaCard(0, 1);
        MonakaSecondEnemy = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, MonakaSecondEnemy });
        await MonakaPlay(merit);
        Equal(MonakaFaith(), 3, "75 War Merit counts two actually connected enemy targets, deduplicating repeated results including fully blocked hits");
        var defense = MonakaCard(0, 3, 4); await MonakaPlay(defense);
        Check(Enemy.GetPower<HbrDefenseDownPower>()?.Amount == 1 && Hero.GetPower<HbrDefenseUpPower>()?.Amount == 1,
            "75 real post-attack framework applications use one enemy-turn duration");
        var retained = MonakaCard(0, 5);
        Check(retained.Keywords.Contains(CardKeyword.Retain), "75 Gentle Forest uses native Retain");
        await retained.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        await retained.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        Equal(MonakaFaith(), 1, "75 retained hand end grants Faith once per actual own turn");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
        await retained.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        Equal(MonakaFaith(), 2, "75 a new native own turn permits another retained Faith");
    }
    static async Task MonakaPaymentCases()
    {
        foreach (int faith in new[] { 4, 5, 12 })
        {
            var card = MonakaCard(faith, 2); int before = Player.PlayerCombatState!.Energy;
            Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), faith >= 5 ? 0 : 2, "75 native preview automatically uses Faith at threshold " + faith);
            var spent = await card.SpendResources();
            Equal(spent.Item1, faith >= 5 ? 0 : 2, "75 actual native resource command spends the correct energy at faith " + faith);
            Equal(before - Player.PlayerCombatState.Energy, spent.Item1, "75 displayed payment agrees with the actual energy ledger");
            Equal(MonakaFaith(), faith - (faith >= 5 ? 5 : 0), "75 actual Faith is paid once before card body");
            await MonakaPlay(card);
            Equal(MonakaAttacks.Single().Multiplier, (1m + Math.Min(faith, 10) * .16m) * (faith >= 5 ? 1.5m : 1m),
                "75 actual damage retains pre-payment Faith and HP bonus even after spending five");
        }
        var selfless = MonakaCard(12, 1, 2, 6);
        MonakaSecondEnemy = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, MonakaSecondEnemy });
        await selfless.SpendResources(); await MonakaPlay(selfless);
        Equal(MonakaAttacks[0].Multiplier, 7.8m, "75 twelve-Faith payment combines2.6faith, doubled Selfless and1.5HP exactly once");
        Equal(MonakaFaith(), 0m, "75 Selfless clears even the normal plusone and War Merit gains at the end");
        Check(Hits.Count > 0 && Hits.All(hit => hit.Dealer == null && hit.Props.HasFlag(ValueProp.Unpowered) && hit.Final == hit.Raw),
            "75 actual team damage bridge emits fixed dealerless hits without attack scaling");
        int teamHits = Hits.Count;
        await MonakaPlay(selfless, 1); Equal(MonakaAttacks[1].Multiplier, 1m, "75 first Heartfelt replay snapshots zero Faith after Selfless");
        Equal(MonakaFaith(), 3m, "75 later non-Selfless replay keeps base and two target merits");
        Check(Hits.Count == teamHits, "75 sub-ten Faith replay never repeats friendly fire");
        await MonakaPlay(selfless, 2); Equal(MonakaAttacks[2].Multiplier, 1.48m, "75 next replay takes its new three-Faith snapshot");
        Equal(MonakaFaith(), 6m, "75 extra play series never pays energy a second time");
    }
    static async Task MonakaWrapperCases(Harmony h)
    {
        ulong? local = LocalContext.NetId;
        try
        {
            LocalContext.NetId = Player.NetId;
            void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            foreach (string name in new[] { "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + name, false) ?? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch();
            var card = MonakaCard(12, 1, 2, 6);
            var physical = GardenCard<WindForestFireMountainHbrgoSaki>(1, 2, 6); GardenPermanent(physical); card.DeckVersion = physical;
            var relic = (ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().MutableClone(); Set(relic, "_owner", Player);
            Set(Player, "_relics", Player.Relics.Append(relic).ToList());
            Call("ThirtyGProposalState", "RestoreIdentity", physical, Player.NetId + ":1");
            Native(relic, "SetProposalData", JsonSerializer.Serialize(new { InBattle = true,
                Cards = new Dictionary<string, object> { [Player.NetId + ":1"] = new { Heart = 2 } } }));
            MonakaSecondEnemy = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
            Set(Combat, "_enemies", new List<Creature> { Enemy, MonakaSecondEnemy });
            int energy = Player.PlayerCombatState!.Energy; await ChasePlay(card);
            Check(MonakaAttacks.Count == 3 && MonakaAttacks.Select(hit => hit.Multiplier).SequenceEqual(new[] { 7.8m, 1m, 1.48m }),
                "75 true native wrapper and actual30GHeart modifier execute three bodies with independent pre-payment Faith snapshots");
            Equal(Player.PlayerCombatState.Energy, energy, "75 native Heart series never repeats energy payment");
            Equal(MonakaFaith(), 6, "75 native Heart replays preserve the exact post-Selfless Faith sequence");
            Check(card.Pile == Player.PlayerCombatState.DiscardPile, "75 native wrapper moves the physical played card only after its full replay series");
        }
        finally { LocalContext.NetId = local; }
    }
    static async Task MonakaCases()
    {
        foreach (Type type in new[] { typeof(WindForestFireMountainHbrgoSaki), typeof(HbrFaithPower), typeof(HbrDefenseDownPower), typeof(HbrDefenseUpPower), typeof(ThirtyGCollectionRelic) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var h = new Harmony(ProductionId + ".monaka75");
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(MonakaAttackLeaf));
            Patch(Helper("HighHitDamageVisualHelper").GetMethod("IsDirectAttackDamage", F)!, nameof(MonakaDirect));
            foreach (string name in new[] { "MonakaFaithEnergyCostPatch", "MonakaFaithPaymentPatch" }) h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch();
            await MonakaBodyCases(); await MonakaPaymentCases(); await MonakaWrapperCases(h);
        }
        finally { h.UnpatchAll(h.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunMonaka(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); MonakaCases().GetAwaiter().GetResult(); ok = true; } catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual card bodies, native SpendResources/cost getter and Faith mutations, real framework powers. Attack requests recorded at Execute leaf; actual fixed team command routes through native canonical damage terminal and shared damage pipeline, without simulating native block/DP mutation. Replay invocations explicitly use native CardPlay indexes. No Godot or live network session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + "Monaka checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
