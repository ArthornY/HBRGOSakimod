using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

public sealed class LaboratoryMemberFixture : CardModel, IThirtyOneCMemberCard
{
    public ThirtyOneCMember ThirtyOneCMember { get; set; }
    public LaboratoryMemberFixture() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
}

static partial class EffectChecks
{
    static bool WProbeCannon;
    static TaskCompletionSource<bool>? WActionGate;
    static int WActionSpent;
    static bool WLogNameLeaf(ref string __result) { __result = "test-enemy"; return false; }
    static bool WPlayWrapperLeaf(CardModel __instance, Creature target, ResourceInfo resources, ref Task __result)
    {
        if (__instance is not WorldIsMyLaboratoryHbrgoSaki || WActionGate == null) return true;
        WActionSpent = resources.EnergySpent;
        Check(target == Enemy && __instance.CurrentTarget == null, "real PlayCardAction reaches its selected Break target before CurrentTarget is assigned");
        Equal(__instance.EnergyCost.GetAmountToSpend(), 0, "authoritative action scope still owns the selected-target discount inside the asynchronous play boundary");
        __result = WActionGate.Task; return false;
    }
    static async Task WActualPlayActionCase(Harmony h)
    {
        BReset(); var card = GardenCard<WorldIsMyLaboratoryHbrgoSaki>(1); Power<BreakPower>(Enemy, 1);
        Set(Player.PlayerCombatState!, "_energy", 0); Enemy.CombatId = 789u;
        Invoke(typeof(NetCombatCardDb).GetMethod("IdCardIfNecessary", F)!, NetCombatCardDb.Instance, card);
        MethodInfo wrapper = typeof(CardModel).GetMethod(nameof(CardModel.OnPlayWrapper))!;
        MethodInfo leaf = typeof(EffectChecks).GetMethod(nameof(WPlayWrapperLeaf), F)!;
        h.Patch(wrapper, prefix: new HarmonyMethod(leaf));
        MethodInfo logName = AccessTools.PropertyGetter(typeof(Creature), "LogName");
        MethodInfo logLeaf = typeof(EffectChecks).GetMethod(nameof(WLogNameLeaf), F)!;
        h.Patch(logName, prefix: new HarmonyMethod(logLeaf));
        WActionGate = new(TaskCreationOptions.RunContinuationsAsynchronously); WActionSpent = -1;
        try
        {
            var action = new PlayCardAction(card, Enemy);
            Task task = (Task)Native(action, "ExecuteAction")!;
            if (task.IsFaulted) await task;
            Check(!task.IsCompleted && WActionSpent == 0, $"state={task.Status},spent={WActionSpent}; actual native PlayCardAction validates and spends zero before its real asynchronous wrapper boundary");
            Equal(card.EnergyCost.GetAmountToSpend(), 1, "a suspended network action restores caller target context instead of leaking its free-card quote");
            WActionGate.SetResult(true); await task;
            Equal(Player.PlayerCombatState.Energy, 0, "actual zero-energy selected-target action neither spends nor grants energy");
            Equal(card.EnergyCost.GetAmountToSpend(), 1, "completed native action closes its selected-target cost scope");
        }
        finally { WActionGate?.TrySetResult(true); WActionGate = null; h.Unpatch(wrapper, leaf); h.Unpatch(logName, logLeaf); }
    }
    static void WActualCannonProbe(object[] __args)
    {
        if (!WProbeCannon || __args[5] is not CardModel card) return;
        Creature target = ((IEnumerable<Creature>)Invoke(Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!.GetMethod("ReadTargets", F)!, null, new object?[] { __args })!).Single();
        object?[] query = [target, Hero, card, ValueProp.Move, null];
        Check((bool)Call("HotaruCannonStatsHelper", "TryGetCurrentCannon", query)!, "real owner-attributed cannon command is recognized despite retaining a card source: " + card.GetType().Name);
        Equal(BFactor(Hero, target, card), 1m, "card-specific31B HP/DP/solo multipliers do not leak into its triggered cannon");
        Equal((decimal)Call("ThirtyOneBCardCombatHelper", "GetDestructionGainMultiplier", card, target, Hero, ValueProp.Move)!, 1m, "Li's destruction upgrade does not leak into its triggered cannon");
        Equal(WNormal(card, target), 0m, "Yamawaki's normal-amplification upgrades do not leak into her triggered cannon");
        Equal(WHp(card, target), 1m, "Yamawaki's HP multiplier does not leak into her triggered cannon");
        Equal((decimal)Call("YamawakiLaboratoryCombatHelper", "GetDestructionGainMultiplier", card, target)!, 1m, "Yamawaki's Break destruction multiplier does not leak into her triggered cannon");
        Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetDamageAmplificationPercent", card, Hero, target, ValueProp.Move)!, 0m, "Ninja's retained damage bonus does not leak into her triggered cannon");
        Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetDestructionGainMultiplier", card, target, Hero, ValueProp.Move)!, 1m, "Ninja's authored destruction coefficient does not leak into her triggered cannon");
    }
    static async Task WActualCannonCases(Harmony h)
    {
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HotaruCannonDamageContextPatch", true)!).Patch();
        MethodInfo probe = typeof(EffectChecks).GetMethod(nameof(WActualCannonProbe), F)!;
        h.Patch(Canonical, prefix: new HarmonyMethod(probe) { priority = 0 });
        try
        {
            for (int i = 0; i < 4; i++)
            {
                BReset();
                var relic = new Millsage(); Set(relic, "<IsMutable>k__BackingField", true); Set(relic, "_owner", Player);
                Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual"), relic });
                var cannon = (Creature)H("Actor", Blank<HotaruContestCannon>(), null, Player)!;
                Set(Combat, "_allies", new List<Creature> { Hero, cannon });
                CardModel source = i == 0 ? GardenCard<HellfireBurnHbrgoSaki>(1, 3) : i == 1 ? GardenCard<FrozenMistMeowHbrgoSaki>(5) : i == 2 ? GardenCard<WorldIsMyLaboratoryHbrgoSaki>(1, 2, 5) : GardenCard<ItsNinjaShowtimeHbrgoSaki>(2, 5, 6);
                if (i == 1) Power<HighDimensionalShellPower>(Enemy, 100);
                if (i == 2) Power<BreakPower>(Enemy, 1);
                if (i == 3) Call("KanzakiShowtimeCombatHelper", "RecordRetained", source, null);
                WProbeCannon = true;
                await (Task)Call("HotaruCannonStatsHelper", "ReactionDamage", new ThrowingPlayerChoiceContext(), cannon, Enemy, 6m, source, null, false, false)!;
                WProbeCannon = false;
            }
        }
        finally { WProbeCannon = false; h.Unpatch(Canonical, probe); }
    }
    static decimal WNormal(CardModel card, Creature? target = null) => (decimal)Call("YamawakiLaboratoryCombatHelper", "GetDamageAmplificationPercent", card, Hero, target ?? Enemy, ValueProp.Move)!;
    static decimal WHp(CardModel card, Creature target) => (decimal)Call("YamawakiLaboratoryCombatHelper", "GetHpDamageMultiplier", card, target)!;
    static void WMembers(int count)
    {
        for (int i = 1; i < count; i++)
        {
            var member = (LaboratoryMemberFixture)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(LaboratoryMemberFixture))).ToMutable();
            Set(member, "_owner", Player); member.ThirtyOneCMember = (ThirtyOneCMember)i; Player.Deck.AddInternal(member);
        }
    }
    static async Task BWorldCases(Harmony h)
    {
        foreach (Type type in new[] { typeof(WorldIsMyLaboratoryHbrgoSaki), typeof(ItsNinjaShowtimeHbrgoSaki), typeof(LaboratoryMemberFixture), typeof(FearOfDevilPower), typeof(BreakPower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        foreach (string patch in new[] { "YamawakiBreakCostPatch", "YamawakiPlayActionTargetPatch", "YamawakiCanPlayTargetPatch", "YamawakiCanBeginTargetingPatch", "YamawakiShuffleReceiptPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + patch, true)!).Patch().Count > 0, "installs real selected-target/physical-shuffle patch " + patch);
        BReset(); var card = GardenCard<WorldIsMyLaboratoryHbrgoSaki>();
        var play = Play(card); Set(play, "<Target>k__BackingField", Enemy);
        await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
        Check(BAttacks.Single().Raw == 3m && BAttacks.Single().Hits == 4, "Yamawaki base is one selected enemy with four50% hits");
        Equal(WHp(card, Enemy), 1.3m, "Yamawaki noDP target has30%HP bonus");
        Power<HighDimensionalShellPower>(Enemy, 100);
        Equal(WHp(card, Enemy), 1m, "Yamawaki HP bonus does not apply against liveDP");
        BReset(); card = GardenCard<WorldIsMyLaboratoryHbrgoSaki>(1, 2);
        Power<BreakPower>(Enemy, 1);
        Set(Player.PlayerCombatState!, "_energy", 0);
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), 1, "unselected header retains the printed cost despite another broken enemy");
        Check(card.CanPlay(), "zero-energy header validation permits selecting a broken target");
        Check(card.CanPlayTargeting(Enemy), "real native target validation allows a zero-energy Break attack");
        Creature healthy = (Creature)H("Actor", Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, healthy });
        Check(!card.CanPlayTargeting(healthy), "zero-energy target validation rejects the nonBreak enemy even when a different enemy is broken");
        using ((IDisposable)Invoke(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.YamawakiTargetCostContext", true)!.GetMethod("Begin", F)!, null, card, Enemy)!)
        {
            Equal(card.EnergyCost.GetAmountToSpend(), 0, "actual selected Break target costs zero before native CurrentTarget is assigned");
            var spent = await card.SpendResources(); Equal(spent.Item1, 0, "real native SpendResources spends zero for the selected Break target");
        }
        using ((IDisposable)Invoke(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.YamawakiTargetCostContext", true)!.GetMethod("Begin", F)!, null, card, healthy)!)
            Equal(card.EnergyCost.GetAmountToSpend(), 1, "authoritative nonBreak target scope suppresses the header fallback");
        Equal(WNormal(card), 25m, "Mad Scientist joins normal amplification by25 when any living enemy is broken");
        Equal((decimal)Call("YamawakiLaboratoryCombatHelper", "GetDestructionGainMultiplier", card, Enemy)!, 2m, "Will Scythe doubles destruction only on its broken target");
        Equal((decimal)Call("YamawakiLaboratoryCombatHelper", "GetDestructionGainMultiplier", card, healthy)!, 1m, "Will Scythe does not double destruction against a nonBreak target");
        BReset(); card = GardenCard<WorldIsMyLaboratoryHbrgoSaki>(5); GardenPermanent(card);
        var ordinary = (WorldIsMyLaboratoryHbrgoSaki)card.MutableClone(); Set(ordinary, "_owner", Player); Set(ordinary, "_deckVersion", card); Player.PlayerCombatState!.Hand.AddInternal(ordinary);
        var od = (WorldIsMyLaboratoryHbrgoSaki)card.MutableClone(); Set(od, "_owner", Player); Set(od, "_deckVersion", card); Player.PlayerCombatState.Hand.AddInternal(od);
        Equal(WNormal(ordinary), 50m, "initial draw pile counts as freshly shuffled");
        using ((IDisposable)Call("YamawakiLaboratoryCombatHelper", "BeginPlay", ordinary)!)
        {
            Equal(WNormal(ordinary), 50m, "first-play receipt retains its50% for all hits after marking the physical card spent");
            Equal(WNormal(od), 0m, "a different OD mirror cannot reuse the reserved first-play bonus");
            using ((IDisposable)Call("YamawakiLaboratoryCombatHelper", "BeginPlay", ordinary)!)
                Equal(WNormal(ordinary), 0m, "nested replay of the same card does not inherit its parent's consumed receipt");
            Equal(WNormal(ordinary), 50m, "nested replay restores the outer attack's own receipt");
        }
        Equal(WNormal(ordinary), 0m, "second play in the same shuffle has no Devil Returns bonus");
        Equal(WNormal(od), 0m, "OD mirror shares the ordinary copy's spent physical ledger");
        var independent = GardenCard<WorldIsMyLaboratoryHbrgoSaki>(5);
        Equal(WNormal(independent), 50m, "a separate actual card has its own first-play receipt");
        var staleReceipt = (IDisposable)Call("YamawakiLaboratoryCombatHelper", "BeginPlay", independent)!;
        ExecutionContext captured = ExecutionContext.Capture()!; staleReceipt.Dispose();
        decimal staleBonus = -1m; ExecutionContext.Run(captured, _ => staleBonus = WNormal(independent), null);
        Equal(staleBonus, 0m, "an asynchronous continuation holding a disposed first-play receipt cannot reuse its bonus");
        await Hook.AfterShuffle(Combat, new ThrowingPlayerChoiceContext(), Player);
        Equal(WNormal(od), 50m, "real Hook.AfterShuffle refreshes the physical origin for its next ordinary or OD play");
        BReset(); card = GardenCard<WorldIsMyLaboratoryHbrgoSaki>(3, 4, 6); GardenPermanent(card); WMembers(3);
        Equal(WHp(card, Enemy), 1.5m, "three distinct31C permanent kinds upgrade HP bonus to50%");
        Equal(card.TargetType == TargetType.AnyEnemy ? 1 : 0, 1m, "three kinds retain the selected-target four-hit attack");
        WMembers(6);
        var origin = card; card = (WorldIsMyLaboratoryHbrgoSaki)origin.MutableClone(); Set(card, "_owner", Player); Set(card, "_deckVersion", origin); Player.PlayerCombatState!.Hand.AddInternal(card);
        Equal(card.TargetType == TargetType.AllEnemies ? 1 : 0, 1m, "six distinct31C kinds enable the live AoE target type");
        await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), Play(card))!;
        Check(BAttacks.Single().Raw == 3m && BAttacks.Single().Hits == 10, "Big Bang replaces base4 with ten hits without changing each50% coefficient");
        Check(Enemy.GetPower<FearOfDevilPower>() is { Permanent: true, Amount: 1 }, "six-kind Yamawaki Fear uses the same permanent receiving pipeline as the31C bond");
        Check(!DesuwaFace(card, false).Contains('{'), "Yamawaki upgraded face renders all target/hit/member placeholders");
        foreach (int id in new[] { 0, 1, 2, 5 }) GardenUpgrade(card, id);
        Equal(card.CurrentUpgradeLevel, 7, "Yamawaki reuses the seven-choice native upgrade ledger");
        await WActualPlayActionCase(h);
        await WActualCannonCases(h);
    }
}

