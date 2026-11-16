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
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

public sealed class HanamuraPlayObserver : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public static readonly List<CardType> Plays = [];
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    { Plays.Add(play.Card.Type); return Task.CompletedTask; }
}
static class HanamuraStandalone { public static void Run(string path) => EffectChecks.RunHanamura(path); }
static partial class EffectChecks
{
    sealed record HanamuraAttack(decimal Raw, int Hits, bool AttackUp, bool Combo, CardType SourceType);
    static readonly List<HanamuraAttack> HanamuraAttacks = [];
    static readonly List<decimal> HanamuraDestruction = [];
    static CardModel? HanamuraChoice;
    static Action? HanamuraSelectionMutation;
    static ExecutionContext? HanamuraCaptured;
    static readonly List<CardModel[]> HanamuraSelections = [];
    static decimal HanamuraBasis(ThisIsMyStageHbrgoSaki card) => (decimal)Call("HanamuraOrchestraCombatHelper", "GetAttackUpBasis", card)!;
    static decimal HanamuraPotency(PowerModel power) => decimal.Parse((string)power.GetType().GetProperty("HbrPotencyData", F)!.GetValue(power)!, System.Globalization.CultureInfo.InvariantCulture);
    static ThisIsMyStageHbrgoSaki HanamuraCard(params int[] upgrades)
    {
        GardenReset(); HanamuraAttacks.Clear(); HanamuraDestruction.Clear(); HanamuraSelections.Clear(); HanamuraChoice = null; HanamuraSelectionMutation = null; HanamuraCaptured = null; HanamuraPlayObserver.Plays.Clear();
        Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        Set(Player.PlayerCombatState!, "_energy", 20); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        return GardenCard<ThisIsMyStageHbrgoSaki>(upgrades);
    }
    static T HanamuraPermanent<T>() where T : CardModel, new()
    { var card = GardenCard<T>(); GardenPermanent(card); return card; }
    static async Task HanamuraPlay(ThisIsMyStageHbrgoSaki card, Creature? target = null)
    {
        var play = Play(card); Set(play, "<Target>k__BackingField", target ?? Hero);
        await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
    }
    static bool HanamuraHandChoice(object[] __args, ref Task<IEnumerable<CardModel>> __result)
    {
        var source = __args.OfType<CardModel>().First();
        var filter = __args.OfType<Func<CardModel, bool>>().Single();
        var choices = source.Owner.PlayerCombatState!.Hand.Cards.Where(filter).ToArray(); HanamuraSelections.Add(choices);
        HanamuraSelectionMutation?.Invoke();
        __result = Task.FromResult<IEnumerable<CardModel>>(HanamuraChoice == null ? choices.Take(1) : new[] { HanamuraChoice }); return false;
    }
    static bool HanamuraDamageLeaf(object[] __args, ref Task<IEnumerable<DamageResult>> __result)
    { __result = HanamuraDamageBody(__args); return false; }
    static async Task<IEnumerable<DamageResult>> HanamuraDamageBody(object[] args)
    {
        var card = args[5] as CardModel; var dealer = args[4] as Creature; var props = (ValueProp)args[3];
        var targets = (IEnumerable<Creature>)Invoke(Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!.GetMethod("ReadTargets", F)!, null, new object?[] { args })!;
        Creature[] snapshot = targets.ToArray();
        if (card is ThisIsMyStageHbrgoSaki)
        {
            HanamuraCaptured ??= ExecutionContext.Capture();
            foreach (Creature target in snapshot)
                HanamuraDestruction.Add((decimal)Call("HanamuraOrchestraCombatHelper", "GetDestructionGainMultiplier", card, target, dealer, props)!);
            Equal((decimal)Call("HanamuraOrchestraCombatHelper", "GetDestructionGainMultiplier", card, snapshot.First(), dealer, ValueProp.Unpowered)!, 1,
                "82 unpowered reaction does not inherit Aria destruction context");
        }
        return await Body(snapshot, (decimal)args[2], props, dealer, card);
    }
    static bool HanamuraAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    { __result = HanamuraResolve(__instance); return false; }
    static async Task<AttackCommand> HanamuraResolve(AttackCommand command)
    {
        var card = (CardModel)command.ModelSource!;
        await Hook.BeforeAttack(Combat, command);
        decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(command)!;
        int hits = (int)Hook.ModifyAttackHitCount(Combat, command, (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(command)!);
        Call("HighHitDamageVisualHelper", "SetResolvedHitCount", command, (decimal)hits);
        HanamuraAttacks.Add(new(raw, hits, Hero.GetPower<HbrAttackUpPower>() != null, Hero.GetPower<HbrComboMediumPower>() != null, card.Type));
        for (int hit = 0; hit < hits; hit++)
        {
            var results = (await Command(command.Attacker, raw, ValueProp.Move, card, Combat.HittableEnemies)).Select(result =>
                new DamageResult(result.Receiver, ValueProp.Move) { BlockedDamage = result.BlockedDamage, UnblockedDamage = result.UnblockedDamage }).ToArray();
            command.AddResultsInternal(results);
            foreach (DamageResult result in results)
            {
                MethodInfo method = typeof(Hook).GetMethods(F).Single(candidate => candidate.Name == "AfterDamageGiven");
                object?[] args = method.GetParameters().Select(p => p.ParameterType.IsInstanceOfType(RunState) ? (object)RunState :
                    p.ParameterType.IsInstanceOfType(Combat) ? Combat : typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType) ? new ThrowingPlayerChoiceContext() :
                    p.ParameterType == typeof(DamageResult) ? result : p.ParameterType == typeof(ValueProp) ? ValueProp.Move :
                    p.ParameterType == typeof(Creature) ? p.Name == "dealer" ? command.Attacker : result.Receiver :
                    p.ParameterType == typeof(CardModel) ? card : throw new Exception(p.ToString())).ToArray();
                await (Task)Invoke(method, null, args)!;
            }
        }
        await Hook.AfterAttack(Combat, new ThrowingPlayerChoiceContext(), command);
        return command;
    }
    static void HanamuraCountCases()
    {
        var source = HanamuraCard(); GardenPermanent(source);
        Equal(HanamuraBasis(source), 50, "82 Hanamura excludes her own permanent character");
        var selfCopy = HanamuraPermanent<ThisIsMyStageHbrgoSaki>(); Set(selfCopy, "<Id>k__BackingField", ModelDb.GetId(typeof(HelloWorldHbrgoSaki)));
        Equal(HanamuraBasis(source), 50, "82 self exclusion uses character identity despite a different model id");
        var ruka = HanamuraPermanent<MyLegendBeginsHbrgoSaki>();
        var rukaAlt = HanamuraPermanent<MyLegendBeginsHbrgoSaki>(); Set(rukaAlt, "<Id>k__BackingField", ModelDb.GetId(typeof(HelloWorldHbrgoSaki)));
        Equal(HanamuraBasis(source), 55, "82 different-named physical copies of the same character still count once");
        HanamuraPermanent<HelloWorldHbrgoSaki>(); HanamuraPermanent<SaviorGrandEntranceHbrgoSaki>();
        Equal(HanamuraBasis(source), 65, "82 three other permanent character identities give65percent");
        var handOnly = GardenCard<InnocentRequiemHbrgoSaki>();
        Equal(HanamuraBasis(source), 65, "82 generated hand-only garden cards do not count even below the Orchestra cap");
        HanamuraPermanent<WorldsFinestHbrgoSaki>(); HanamuraPermanent<MoreThanOneTruthHbrgoSaki>(); HanamuraPermanent<CrushYouLikeABugHbrgoSaki>();
        Equal(HanamuraBasis(source), 75, "82 base Orchestra bonus caps at25percent despite six other characters");
        source = HanamuraCard(1, 5); GardenPermanent(source);
        HanamuraPermanent<MyLegendBeginsHbrgoSaki>(); HanamuraPermanent<HelloWorldHbrgoSaki>(); HanamuraPermanent<SaviorGrandEntranceHbrgoSaki>();
        HanamuraPermanent<WorldsFinestHbrgoSaki>(); var tsukasa = HanamuraPermanent<MoreThanOneTruthHbrgoSaki>();
        Equal(HanamuraBasis(source), 125, "82 baton75 plus Big Sister five-character50 equals125");
        tsukasa.Pile!.RemoveInternal(tsukasa, true);
        Equal(HanamuraBasis(source), 115, "82 permanent removal invalidates the cached character set immediately");
        HanamuraPermanent<CrushYouLikeABugHbrgoSaki>(); Equal(HanamuraBasis(source), 125, "82 same-sized deck replacement also refreshes character identity cache");
        source = HanamuraCard(); var peer = SupportTeammate();
        var foreign = (HelloWorldHbrgoSaki)ModelDb.Card<HelloWorldHbrgoSaki>().ToMutable(); Set(foreign, "_owner", peer); Player.Deck.AddInternal(foreign);
        Equal(HanamuraBasis(source), 50, "82 another owner's card cannot contribute to this player's Orchestra");
    }
    static async Task HanamuraSupportCases()
    {
        var card = HanamuraCard(); Check(card.Type == CardType.Skill && card.EnergyCost.GetWithModifiers(CostModifiers.All) == 1 && !card.Keywords.Contains(CardKeyword.Exhaust), "82 base is a one-energy rare nonexhausting skill");
        Equal((int)card.Rarity, (int)CardRarity.Rare, "82 native rarity is Rare"); Check(card.TargetType == TargetType.Self, "82 solo support uses native self targeting");
        await HanamuraPlay(card); Equal(HanamuraPotency(Hero.GetPower<HbrAttackUpPower>()!), 50, "82 solo base applies actual50percent Attack Up");
        Equal(Hero.GetPower<HbrAttackUpPower>()!.Amount, 1, "82 positive buff has one actual use");
        card = HanamuraCard(0); Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), 0, "82 Flash changes actual native cost to zero");
        card = HanamuraCard(2); await HanamuraPlay(card); Equal(HanamuraPotency(Hero.GetPower<HbrAttackUpPower>()!), 65, "82 Symphony has actual65percent minimum before the stat formula");
        card = HanamuraCard(1, 2, 3); var peer = SupportTeammate();
        Creature phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Power<PhantomPower>(phantom, 1);
        Creature spirit = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Power<KozueSpiritPower>(spirit, 1);
        Creature otherPet = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature, phantom, spirit, otherPet });
        Check(card.TargetType == TargetType.AnyAlly, "82 team buff with Stellar Symphony still requests one actual allied target");
        Check(card.IsValidTarget(peer.Creature) && card.IsValidTarget(phantom) && card.IsValidTarget(spirit) && !card.IsValidTarget(otherPet),
            "82 native targeting accepts the Risa support range while excluding ordinary disposable pets");
        await HanamuraPlay(card, phantom);
        Check(new[] { Hero, peer.Creature, phantom, spirit }.All(target => target.GetPower<HbrAttackUpPower>()?.Amount == 1), "82 all support matches real Risa range: players, phantom and spirit");
        Check(otherPet.GetPower<HbrAttackUpPower>() == null, "82 disposable ordinary pets are excluded from support");
        Check(phantom.GetPower<HbrComboMediumPower>()?.Amount == 3 && Hero.GetPower<HbrComboMediumPower>() == null && peer.Creature.GetPower<HbrComboMediumPower>() == null,
            "82 exactly the selected unit gets one three-use medium combo, never the whole party");
        card = HanamuraCard(); peer = SupportTeammate(); phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Power<PhantomPower>(phantom, 1);
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature, phantom }); await HanamuraPlay(card, phantom);
        Check(phantom.GetPower<HbrAttackUpPower>()?.Amount == 1 && Hero.GetPower<HbrAttackUpPower>() == null && peer.Creature.GetPower<HbrAttackUpPower>() == null,
            "82 base allied selection buffs only the selected phantom, not its summoner or teammate");
        card = HanamuraCard(2); peer = SupportTeammate(); Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        Check(card.TargetType == TargetType.AllAllies, "82 team buff without Stellar Symphony needs no separate unit choice");
        H("Sense", "Talent", 20); await HanamuraPlay(card);
        Check(HanamuraPotency(Hero.GetPower<HbrAttackUpPower>()!) > 65, "82 Orchestra basis goes through the real intelligence buff formula");
        card = HanamuraCard(4); H("Mode", false, 1); await HanamuraPlay(card);
        Check(Hero.GetPower<HbrAttackUpPower>() == null && HanamuraAttacks.Count == 0, "82 HBR-only mechanics remain inactive outside HBR mode");
    }
    static async Task HanamuraAriaCases()
    {
        var card = HanamuraCard(3, 4); var gauge = (Overclock)ModelDb.Relic<Overclock>().ToMutable(); Set(gauge, "_owner", Player); Set(Player, "_relics", Player.Relics.Append(gauge).ToList());
        var observer = Power<HanamuraPlayObserver>(Hero, 1);
        await ChasePlay(card);
        Check(HanamuraAttacks.Count == 1 && HanamuraAttacks[0] is { Raw: 4.62m, Hits: 3, AttackUp: false, Combo: false, SourceType: CardType.Skill },
            "82 Aria requests77percent three times before granting this play's attack or combo buffs");
        Check(HanamuraDestruction.Count == 3 && HanamuraDestruction.All(value => value == 1.6m / 3m), "82 actual direct damage scope uses original destruction1point6 exactly once: " + string.Join(",", HanamuraDestruction));
        Check(Hero.GetPower<HbrAttackUpPower>()?.Amount == 1 && Hero.GetPower<HbrComboMediumPower>()?.Amount == 3,
            "82 the newly granted attack buff and medium combo remain unused after Aria");
        Check(HanamuraPlayObserver.Plays.SequenceEqual(new[] { CardType.Skill }), "82 native wrapper emits one skill play, not an invented attack-card play per Aria hit");
        Equal(gauge.PreciseCharge, 12.5m, "82 native skill charge5 plus three connected segments2point5 each, no extra attack-card charge");
        Check(card.Pile == Player.PlayerCombatState!.DiscardPile && Player.PlayerCombatState.Energy == 19, "82 native skill wrapper pays once and discards rather than exhausting");
        Equal((decimal)Call("HanamuraOrchestraCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!, 1, "82 Aria destruction scope closes after the actual card body");
        ExecutionContext.Run(HanamuraCaptured!, _ => Equal((decimal)Call("HanamuraOrchestraCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!, 1,
            "82 captured but closed Aria execution context cannot retain its direct destruction bonus"), null);
        card = HanamuraCard(4); var old = Power<HbrAttackUpPower>(Hero, 1); Native(old, "ConfigureEffect", 20m, null);
        await HanamuraPlay(card);
        Check(HanamuraAttacks.Single().AttackUp && !Hero.Powers.Contains(old) && Hero.GetPower<HbrAttackUpPower>()?.Amount == 1,
            "82 Aria consumes an existing one-use buff, then grants its own fresh buff afterward");
    }
    static async Task HanamuraRoomCases()
    {
        var source = HanamuraCard(6); var other = GardenCard<NoblesseObligeHbrgoSaki>(); HanamuraChoice = other;
        int before = other.EnergyCost.GetWithModifiers(CostModifiers.All); await HanamuraPlay(source);
        Equal(other.EnergyCost.GetWithModifiers(CostModifiers.All), before - 1, "82 Come To My Room applies one real native relative turn discount");
        Check(HanamuraSelections.Single().Contains(other) && !HanamuraSelections.Single().Contains(source), "82 source physical card cannot select itself");
        other.EnergyCost.EndOfTurnCleanup(); Equal(other.EnergyCost.GetWithModifiers(CostModifiers.All), before, "82 room discount expires through native end-of-turn cleanup");
        source = HanamuraCard(6); var zero = GardenCard<ThisIsMyStageHbrgoSaki>(0); HanamuraChoice = zero; await HanamuraPlay(source);
        Equal(zero.EnergyCost.GetWithModifiers(CostModifiers.All), 0, "82 native zero floor prevents negative energy cost");
        source = HanamuraCard(6); await HanamuraPlay(source); Check(HanamuraSelections.Count == 0, "82 no other garden hand card skips the selector");
        source = HanamuraCard(6); other = GardenCard<NoblesseObligeHbrgoSaki>(); HanamuraChoice = other;
        HanamuraSelectionMutation = () => Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
        await HanamuraPlay(source);
        Equal(other.EnergyCost.GetWithModifiers(CostModifiers.All), 3, "82 a hand selection returning after the owner turn changed cannot discount a stale turn");
    }
    static async Task HanamuraCases()
    {
        foreach (Type type in new[] { typeof(ThisIsMyStageHbrgoSaki), typeof(MyLegendBeginsHbrgoSaki), typeof(HelloWorldHbrgoSaki), typeof(SaviorGrandEntranceHbrgoSaki),
            typeof(WorldsFinestHbrgoSaki), typeof(MoreThanOneTruthHbrgoSaki), typeof(CrushYouLikeABugHbrgoSaki), typeof(InnocentRequiemHbrgoSaki), typeof(NoblesseObligeHbrgoSaki),
            typeof(HbrAttackUpPower), typeof(HbrComboMediumPower), typeof(PhantomPower), typeof(KozueSpiritPower), typeof(Overclock), typeof(HanamuraPlayObserver) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var h = new Harmony(ProductionId + ".hanamura82"); ulong? local = LocalContext.NetId;
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            LocalContext.NetId = Player.NetId;
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent", F)!, nameof(VisualTask));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(add, nameof(CrackPileAdd));
            foreach (MethodInfo select in typeof(CardSelectCmd).GetMethods(F).Where(method => method.Name == "FromHand")) Patch(select, nameof(HanamuraHandChoice));
            h.Patch(typeof(AttackCommand).GetMethod("Execute", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HanamuraAttackLeaf), F)!){ priority = Priority.Last });
            h.Unpatch(Canonical, typeof(EffectChecks).GetMethod(nameof(Terminal), F)!);
            h.Patch(Canonical, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HanamuraDamageLeaf), F)!){ priority = Priority.Last });
            foreach (string name in new[] { "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch", "HighHitDamageAttackScopePatch", "HighHitDamageCommandScopePatch", "HanamuraOrchestraTargetPatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + name) ?? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch();
            HanamuraCountCases(); await HanamuraSupportCases(); await HanamuraAriaCases(); await HanamuraRoomCases();
        }
        finally { h.UnpatchAll(h.Id); LocalContext.NetId = local; CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunHanamura(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); HanamuraCases().GetAwaiter().GetResult(); ok = true; } catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual82 card body, permanent native deck identities, native positive-effect applications and targets, native OnPlayWrapper/skill resources and before/after attack/card/damage hooks. Attack Execute and canonical damage terminals record requests and feed actual shared arithmetic; hand input/visuals/waits are controlled. No Godot renderer, native HP mutation or live network session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Hanamura82 checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
