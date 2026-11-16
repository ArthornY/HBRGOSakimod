using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

static class ThirtyOneBStandalone { public static void Run(string path) => EffectChecks.RunThirtyOneB(path); }
static partial class EffectChecks
{
    static readonly Type[] New31BCards = [typeof(PeacefulMemoriesHbrgoSaki), typeof(HellfireBurnHbrgoSaki), typeof(FrozenMistMeowHbrgoSaki), typeof(InnocentRequiemHbrgoSaki), typeof(FreshDeathHbrgoSaki)];
    static readonly List<(CardModel Card, decimal Raw, int Hits)> BAttacks = [];
    static readonly List<(CardModel Card, Creature Target)> BAutoPlays = [];
    static readonly Queue<int> ShieldDice = new();
    static int ShieldRolls;
    static bool BKillTargets;
    static bool BAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        CardModel card = (__instance.ModelSource as CardModel)!;
        decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!;
        int hits = (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(__instance)!;
        BAttacks.Add((card, raw, hits));
        Creature[] targets = __instance.IsMultiTargeted ? Combat.HittableEnemies.ToArray() : [(Creature)typeof(AttackCommand).GetField("_singleTarget", F)!.GetValue(__instance)!];
        var results = (List<List<DamageResult>>)typeof(AttackCommand).GetField("_results", F)!.GetValue(__instance)!;
        foreach (Creature target in targets)
        {
            results.Add([new DamageResult(target, ValueProp.Move)]);
            if (BKillTargets && target == Enemy) Set(target, "_currentHp", 0);
        }
        __result = Task.FromResult(__instance); return false;
    }
    static bool BAutoLeaf(CardModel card, Creature target, ref Task __result)
    { BAutoPlays.Add((card, target)); __result = Task.CompletedTask; return false; }
    static bool BRawDice(int maxExclusive, ref int __result)
    { if (maxExclusive != 100 || ShieldDice.Count == 0) return true; ShieldRolls++; __result = ShieldDice.Dequeue(); return false; }
    static void BReset()
    {
        GardenReset(); BAttacks.Clear(); BAutoPlays.Clear(); ShieldDice.Clear(); ShieldRolls = 0; BKillTargets = false;
        Set(Combat, "<RoundNumber>k__BackingField", 1); Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Enemy);
        Set(Hero, "_currentHp", 5000); Set(Hero, "_maxHp", 5000); Set(Enemy, "_currentHp", 5000); Set(Enemy, "_maxHp", 5000);
    }
    static decimal BFactor(Creature? dealer, Creature target, CardModel? source = null) => (decimal)Call("ThirtyOneBCardCombatHelper", "GetDamageMultiplier", dealer, target, source, ValueProp.Move)!;
    static object BBegin() => Call("ThirtyOneBEnemyActionHelper", "Begin", Enemy)!;
    static void BCapture(params Creature[] targets) => Call("ThirtyOneBEnemyActionHelper", "Capture", targets, Enemy, ValueProp.Move);
    static Task BFinish(object scope)
    { Task task = (Task)Call("ThirtyOneBEnemyActionHelper", "Finish", Task.CompletedTask, scope)!; Call("ThirtyOneBEnemyActionHelper", "RestoreCaller", scope); return task; }
    static Task BPending(Creature target) => (Task)Call("ThirtyOneBEnemyActionHelper", "ApplyPending", new ThrowingPlayerChoiceContext(), target)!;
    static Task BBasis<T>(Creature target, decimal basis, Creature applier) where T : PowerModel => (Task)Invoke(Helper("HbrPercentDebuffApplication").GetMethods(F).Single(m => m.Name == "ApplyBasis" && m.IsGenericMethodDefinition).MakeGenericMethod(typeof(T)), null,
        new ThrowingPlayerChoiceContext(), target, basis, applier, null, null, false)!;
    static Task BEffect<T>(Creature target, decimal basis, Creature applier) where T : HbrEffectPower => (Task)Invoke(Helper("HbrEffectApplication").GetMethods(F).Single(m => m.Name == "Apply" && m.IsGenericMethodDefinition).MakeGenericMethod(typeof(T)), null,
        new ThrowingPlayerChoiceContext(), target, basis, applier, null, null, null, false, null)!;
    static void BChoices()
    {
        foreach (Type type in New31BCards)
        {
            BReset(); CardModel card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
            Equal(((IReadOnlyList<int>)Native(card, "GetAvailableUpgradeEffects")!).Count, 7, type.Name + " has exactly seven independent choices");
            foreach (int choice in Enumerable.Range(0, 7).Reverse()) GardenUpgrade(card, choice);
            Equal(card.CurrentUpgradeLevel, 7, type.Name + " reaches seven-upgrade cap in reverse order");
            Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local), type == typeof(PeacefulMemoriesHbrgoSaki) || type == typeof(InnocentRequiemHbrgoSaki) ? 1 : 0, type.Name + " flash changes cost once");
        }
    }
    static async Task BAoiCases()
    {
        BReset(); var aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(1, 2, 3, 5);
        await GardenPlay(aoi);
        Check(Hero.GetPower<AoiInvincibleShieldPower>() != null && Hero.GetPower<AoiMiracleShieldPower>()?.Amount == 2 && Hero.GetPower<ProtectiveCharmPower>()?.Amount == 1,
            "Aoi actual OnPlay installs invincible, miracle two and charm one");
        var cry = Hero.GetPower<HbrCriticalRateUpPower>()!;
        Equal(Convert.ToDecimal(Native(cry, "get_EffectPotency")), 100m, "Aoi Battlecry is original constant100 despite intelligence");
        await cry.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]);
        Check(Hero.Powers.Contains(cry), "Aoi Battlecry does not expire on player turn end");
        // Remove Artifact-like charm so the action shield itself proves rejection.
        await PowerCmd.Remove(Hero.GetPower<ProtectiveCharmPower>()!);
        ShieldDice.Enqueue(0); object first = BBegin(); BCapture(Hero); BCapture(Hero); BCapture(Hero);
        Equal(ShieldRolls, 1m, "three hits of same action roll the raw shield die once");
        Equal(Actual(Hero, Enemy, 8m, ValueProp.Move, null), 0m, "whole-action shield zeroes the first hit");
        Equal(Actual(Hero, Enemy, 8m, ValueProp.Move, null), 0m, "whole-action shield zeroes later hits");
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
        await BBasis<HbrAttackDownPower>(Hero, 20m, Enemy);
        await BBasis<HbrDefenseDownPower>(Hero, 20m, Enemy);
        await BBasis<FragilePower>(Hero, 30m, Enemy);
        await BEffect<HbrVirusPower>(Hero, 1m, Enemy);
        Check(!Hero.HasPower<WeakPower>() && !Hero.HasPower<HbrAttackDownPower>() && !Hero.HasPower<HbrDefenseDownPower>() && !Hero.HasPower<FragilePower>() && !Hero.HasPower<HbrVirusPower>(),
            "invalidated action rejects native Weak and directly-applied HBR attack/defense/fragile/virus effects");
        await BPending(Hero);
        Equal(Hero.GetPower<AoiMiracleShieldPower>()!.Amount, 2m, "invincible nullification preserves both miracle layers");
        Check(Hero.HasPower<HbrAttackUpPower>(), "Essence grants the attack-up only for a successful invincible nullification");
        await BFinish(first);
        Check(AsyncValue("ThirtyOneBEnemyActionHelper", "Current") == null, "action completion restores caller context");
        object second = BBegin(); BCapture(Hero); await BPending(Hero);
        Equal(ShieldRolls, 1m, "default invincible quota exhausted for the second action of the same turn");
        Equal(Hero.GetPower<AoiMiracleShieldPower>()!.Amount, 1m, "second action consumes exactly one miracle layer");
        Equal(Actual(Hero, Enemy, 8m, ValueProp.Move, null), 0m, "miracle applies to every hit of its reserved action");
        await BFinish(second);
        Set(Combat, "<RoundNumber>k__BackingField", 2);
        ShieldDice.Enqueue(99); object third = BBegin(); BCapture(Hero); await BPending(Hero); await BFinish(third);
        Check(Hero.Powers.OfType<RollBonusPower>().Any(power => power.Amount == 50 && power.RemainingUses == 1),
            "raw99 grants +50 roll bonus with exactly one use");
        Equal(ShieldRolls, 2m, "new turn renews invincible's once-action quota");
        // Two checks, a separate owner and a teammate not selected by the action.
        BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(0); await GardenPlay(aoi);
        for (int i = 0; i < 3; i++) { ShieldDice.Enqueue(0); object move = BBegin(); BCapture(Hero); await BFinish(move); }
        Equal(ShieldRolls, 2m, "Battlefield Flowers tests exactly first two actions per turn");
        BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi);
        await (Task)Invoke(Helper("HbrRollPowerHelper").GetMethod("Apply", F)!.MakeGenericMethod(typeof(RollBonusPower)), null, new ThrowingPlayerChoiceContext(), Hero, 30, 3, Hero, null, true)!;
        var existing = Hero.GetPower<RollBonusPower>()!;
        await AdvantageDicePower.Apply(new ThrowingPlayerChoiceContext(), Hero, 2, 4, Hero, null);
        var advantage = Hero.GetPower<AdvantageDicePower>()!;
        ShieldDice.Enqueue(0); object independent = BBegin(); BCapture(Hero); await BFinish(independent);
        Equal(existing.RemainingUses, 3m, "independent shield roll never consumes existing roll bonus uses");
        Equal(existing.Amount, 30m, "independent shield roll never changes existing roll bonus potency");
        Equal(advantage.RemainingUses, 4m, "independent shield die neither uses nor consumes existing advantage dice");
        foreach (int raw in new[] { 50, 51 })
        {
            BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi); ShieldDice.Enqueue(raw - 1);
            object boundary = BBegin(); BCapture(Hero);
            Check((bool)Call("ThirtyOneBEnemyActionHelper", "IsNullified", Hero)! == (raw <= 50), "independent shield exact raw50/51 boundary: " + raw);
            await BFinish(boundary);
        }
        BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi);
        ShieldDice.Enqueue(0);
        Set(Enemy.Monster!, "<NextMove>k__BackingField", new MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState("SHIELD_TEST", async _ =>
        {
            await Command(Enemy, 8m, ValueProp.Move, null, [Hero]);
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
            await Command(Enemy, 8m, ValueProp.Move, null, [Hero]);
        }));
        Set(Enemy.Monster!, "<Id>k__BackingField", ModelDb.GetId(Enemy.Monster!.GetType()));
        await Enemy.Monster!.PerformMove();
        Equal(ShieldRolls, 1m, "real Monster.PerformMove scopes two separate damage commands as one attack action");
        Check(Hits.Count == 2 && Hits.All(hit => hit.Final == 0m) && !Hero.HasPower<WeakPower>(), "real native move rejects both damage segments and its attached native Weak");
        Check(AsyncValue("ThirtyOneBEnemyActionHelper", "Current") == null, "real async PerformMove postfix restores caller scope");
    }
    static async Task BAttackCards()
    {
        BReset(); var ichigo = GardenCard<HellfireBurnHbrgoSaki>(1, 2, 3, 4, 5, 6);
        await PowerCmd.Apply<HbrMoralePower>(new ThrowingPlayerChoiceContext(), Hero, 5m, Hero, null);
        await GardenPlay(ichigo);
        Equal(BAttacks.Single().Raw, 4m, "Ichigo initial per-hit skill is four raw units");
        Equal(BAttacks.Single().Hits, 4m, "Morale5 doubles Ichigo base hit count");
        Check(Hero.HasPower<HbrComboLargePower>() && Hero.HasPower<HbrComboSmallPower>() && Hero.HasPower<HbrAttackUpPower>(), "Ichigo OnPlay applies both combo sizes and attack-up before the attack leaf");
        Equal(Convert.ToDecimal(Native(ichigo, "get_MeritPercent")), 10m, "military merit grows ten coefficient points per distinct target");
        Equal(BFactor(Hero, Enemy, ichigo), 3m, "Ichigo empty-DP50% and sole-enemy100% bonuses multiply");
        Equal(BFactor(Hero, null!, ichigo), 2m, "unknown-target preview never assumes emptyDP while retaining the known sole-enemy bonus");
        Check(Hero.HasPower<IchigoNextTurnEnergyPenaltyPower>(), "Noisy Entrustment schedules its next-turn energy penalty");
        Set(Player.PlayerCombatState!, "_energy", 3);
        await Hero.GetPower<IchigoNextTurnEnergyPenaltyPower>()!.AfterEnergyReset(Player);
        Equal(Player.PlayerCombatState.Energy, 2m, "Noisy Entrustment's real next-energy-reset callback spends exactly one energy");
        Check(!Hero.HasPower<IchigoNextTurnEnergyPenaltyPower>(), "Noisy Entrustment penalty removes itself after one reset");
        var second = GardenCard<HellfireBurnHbrgoSaki>(4);
        Equal(Convert.ToDecimal(Native(second, "get_MeritPercent")), 0m, "another physical Ichigo card has independent merit");
        GardenPermanent(ichigo);
        var ordinary = (HellfireBurnHbrgoSaki)ichigo.MutableClone(); Set(ordinary, "_owner", Player); Set(ordinary, "_deckVersion", ichigo); Player.PlayerCombatState!.Hand.AddInternal(ordinary);
        // Combat-origin and OD copies point at the same permanent physical card.
        var od = (HellfireBurnHbrgoSaki)ichigo.MutableClone(); Set(od, "_owner", Player); Set(od, "_deckVersion", ichigo); Player.PlayerCombatState.Hand.AddInternal(od);
        Call("ThirtyOneBPhysicalCardState", "AddMerit", ordinary, 20m);
        Equal(Convert.ToDecimal(Native(od, "get_MeritPercent")), 30m, "OD mirror retains the physical origin's merit growth");
        BReset(); var li = GardenCard<FrozenMistMeowHbrgoSaki>(0, 1, 2, 4, 5, 6);
        Equal(li.ModifyCardPlayCount(li, Enemy, 1), 2m, "Li first play repeats exactly once");
        Creature spare = (Creature)H("Actor", Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, spare });
        var play = Play(li); Set(play, "<Target>k__BackingField", Enemy); BKillTargets = true;
        await (Task)Native(li, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
        Equal(BAttacks.Single().Hits, 4m, "Li AoE3 replaces single4 then night adds one");
        Equal(li.ModifyCardPlayCount(li, Enemy, 1), 1m, "Li later play cannot recursively repeat its first-play bonus");
        Check(play.ResultPile == PileType.Hand && Hero.GetPowerAmount<HbrMoralePower>() == 1, $"Li actual kill schedules return-to-hand and grants one morale: pile={play.ResultPile}, morale={Hero.GetPowerAmount<HbrMoralePower>()}, dead={Enemy.IsDead},hp={Enemy.CurrentHp}");
        Equal((decimal)Call("ThirtyOneBCardCombatHelper", "GetDestructionGainMultiplier", li, Enemy, Hero, ValueProp.Move)!, 2m, "Halloween Meow Night doubles destruction gain");
        BReset(); li = GardenCard<FrozenMistMeowHbrgoSaki>(0);
        object attack = BBegin(); BCapture(Hero); await BFinish(attack);
        Check(BAutoPlays.Count == 1 && BAutoPlays[0].Card == li && BAutoPlays[0].Target == Enemy, "Li in hand retaliates at the enemy after its action ends");
        object another = BBegin(); BCapture(Hero); await BFinish(another);
        Equal(BAutoPlays.Count, 1m, "Li same physical card retaliates only once per turn");
        Set(Combat, "<RoundNumber>k__BackingField", 2); object later = BBegin(); BCapture(Hero); await BFinish(later);
        Equal(BAutoPlays.Count, 2m, "Li counter renews on the next turn");
    }
    static async Task BHiguchi()
    {
        BReset(); var card = GardenCard<FreshDeathHbrgoSaki>(0, 1, 3, 5, 6);
        var dp = Blank<DeflectorProtection>(); Set(dp, "<IsMutable>k__BackingField", true); Set(dp, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Concat(new RelicModel[] { dp }).ToList());
        dp.EnsureDpInitialized(); dp.LoseDpByCurrentPercent(1m);
        Set(Hero, "_currentHp", 2000); var play = Play(card); Set(play, "<Target>k__BackingField", Hero);
        await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
        Equal(Convert.ToDecimal(Native(Hero.GetPower<HbrAttackUpPower>()!, "get_EffectPotency")), 120m, "Higuchi doubles only this grant below50%HP");
        Equal(dp.Dp, decimal.Floor(dp.MaxDp * .05m), "Higuchi Superb Spectacle restores exactly5% maxDP");
        Check(Hero.HasPower<HbrCriticalRateUpPower>() && Hero.HasPower<HbrCriticalDamageUpPower>() && Hero.GetPowerAmount<HbrMoralePower>() == 2, "Higuchi actual body grants critical pair and two morale");
        Check(Enemy.Powers.Any(power => power is HbrAttackDownPower or HbrDefenseDownPower or FragilePower or HbrCalamityPower), "Trick Cannon independently chooses an actual debuff");
        foreach (PowerModel power in Enemy.Powers.Where(power => power is HbrAttackDownPower or HbrDefenseDownPower or FragilePower)) Equal(power.Amount, 2m, "Trick Cannon percent debuff has exactly two turns");
        GardenPermanent(card); Set(Hero, "_currentHp", 1000);
        Equal(BFactor(null, Enemy), 1.3m, "Die Together covers dealerless damage to enemies below owner's30%HP");
        Equal(BFactor(Enemy, Hero), 1m, "Die Together never amplifies damage received by the owner");
        Set(Hero, "_currentHp", 1500); Equal(BFactor(null, Enemy), 1m, "Die Together turns off at exactly30%HP without refreshing deck cache");
        Set(Hero, "_currentHp", 1000); card.Pile!.RemoveInternal(card);
        Equal(BFactor(null, Enemy), 1m, "Die Together turns off immediately when permanent source is removed");
    }
    static async Task BCases()
    {
        foreach (Type type in New31BCards.Concat(new[] { typeof(AoiInvincibleShieldPower), typeof(AoiMiracleShieldPower), typeof(ProtectiveCharmPower), typeof(IchigoNextTurnEnergyPenaltyPower), typeof(KozueSpiritPower), typeof(KozueSpiritShellPower), typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower), typeof(HbrAttackUpPower), typeof(HbrCriticalRateUpPower), typeof(HbrCriticalDamageUpPower), typeof(HbrMoralePower), typeof(HbrComboSmallPower), typeof(HbrComboLargePower), typeof(FragilePower), typeof(WeakPower), typeof(HbrVirusPower), typeof(RollBonusPower), typeof(AdvantageDicePower), typeof(HbrCalamityPower) }))
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var h = new Harmony(ProductionId + ".31b");
        void Patch(MethodBase m, string prefix) => h.Patch(m, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            // The terminal substitutes only the unavailable engine damage leaf;
            // it must run after every real production prefix/capture boundary.
            new Harmony(ProductionId).Unpatch(Canonical, typeof(EffectChecks).GetMethod(nameof(Terminal), F)!);
            h.Patch(Canonical, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Terminal), F)!) { priority = -100 });
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (var wait in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(BAttackLeaf));
            Patch(typeof(CardCmd).GetMethod("AutoPlay", F)!, nameof(BAutoLeaf));
            Patch(typeof(Rng).GetMethod("NextInt", [typeof(int)])!, nameof(BRawDice));
            foreach (string name in new[] { "HbrEffectApplicationPatch", "HbrFragileStackingPatch", "HbrFragileApplyPatch", "HbrFragileCopyPatch", "ThirtyOneBEnemyMoveScopePatch", "ThirtyOneBEnemyAttackScopePatch", "ThirtyOneBEnemyActualTargetPatch", "ThirtyOneBAoiDamageNullificationPatch", "ThirtyOneBAoiAttachedPowerPatch", "ThirtyOneBAoiAttachedPowerChangePatch", "ThirtyOneBAoiAttachedStunPatch", "ThirtyOneBAoiAttachedStatusCardPatch", "ThirtyOneBAoiAttachedDpLossPatch", "ThirtyOneBAoiAttachedDpBreakPatch", "ThirtyOneBAoiAttachedOdLossPatch" })
                Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0, "installs real action/buff patch " + name);
            if (Environment.GetEnvironmentVariable("HBR_31B_CASE_FILTER") == "overcap-review")
            { await BDpOvercapReview(h); return; }
            if (Environment.GetEnvironmentVariable("HBR_31B_CASE_FILTER") is "ninja-od" or "ninja")
            {
                if (!ModelDb.Contains(typeof(ItsNinjaShowtimeHbrgoSaki))) ModelDb.Inject(typeof(ItsNinjaShowtimeHbrgoSaki));
                Set(ModelDb.Card<ItsNinjaShowtimeHbrgoSaki>(), "<Id>k__BackingField", ModelDb.GetId(typeof(ItsNinjaShowtimeHbrgoSaki)));
                if (Environment.GetEnvironmentVariable("HBR_31B_CASE_FILTER") == "ninja-od") await BNinjaOdCases(); else await BNinjaCases(h);
                return;
            }
            BChoices(); await BAoiCases(); await BAttackCards(); await BHiguchi(); await BKozueCases(h); await BSaveCases(); await BMultiplayerCases(); Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.ThirtyOneBDamageCallsitePatch", true)!).Patch().Count > 0, "installs composed real damage callsite adapter"); await BDeferredCases(); await BBetaTargetCases(h); BLocalizationCases(h); await BSpiritRollCase(h); await BWorldCases(h); await BNinjaCases(h);
        }
        finally { h.UnpatchAll(h.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunThirtyOneB(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); BCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, caseSelection = Environment.GetEnvironmentVariable("HBR_31B_CASE_FILTER") ?? "all", checks = Passed.Count, passed = Passed, error, modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"), boundary = "Production card bodies, native upgrade ledgers/costs, shared numeric hooks and real native PowerCmd applications. Texture/UI/waits, the attack presentation leaf and auto-play scheduling are isolated; action scopes/capture/pending grants execute their real helpers." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " 31B checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}

