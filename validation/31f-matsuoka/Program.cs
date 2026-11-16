using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class MatsuokaStandalone { public static void Run(string path) => EffectChecks.RunMatsuoka(path); }
public sealed class MatsuokaPlayObserver : PowerModel
{
    public static readonly List<CardType> Plays = [];
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    { Plays.Add(play.Card.Type); return Task.CompletedTask; }
}
static partial class EffectChecks
{
    static bool MZero, MDodge;
    static readonly List<(decimal Raw, int Hits, Creature Target)> MStrikes = [];
    static readonly List<decimal> MCoefficients = [];
    static ExecutionContext? MCaptured;
    static decimal MPotency(PowerModel power) => decimal.Parse((string)power.GetType().GetProperty("HbrPotencyData", F)!.GetValue(power)!, System.Globalization.CultureInfo.InvariantCulture);
    static LeaveAnyBattlefieldToMeHbrgoSaki MCard(params int[] ids)
    {
        var card = (LeaveAnyBattlefieldToMeHbrgoSaki)FCard(typeof(LeaveAnyBattlefieldToMeHbrgoSaki));
        foreach (int id in ids)
        { Check((bool)Native(card, "PrepareUpgradeEffect", id)!, "83 accepts independent upgrade " + id); card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        return card;
    }
    static LeaveAnyBattlefieldToMeHbrgoSaki MReset(int[]? ids = null, int kinds = 0, bool hbr = true)
    {
        FReset(kinds, hbr); MZero = MDodge = false; MStrikes.Clear(); MCoefficients.Clear(); MCaptured = null; MatsuokaPlayObserver.Plays.Clear();
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1); Set(Player.PlayerCombatState!, "_energy", 20);
        Set(Hero, "_block", 0); Set(Enemy, "_block", 0);
        Set(Hero, "_currentHp", 100000000); Set(Hero, "_maxHp", 100000000);
        return MCard(ids ?? []);
    }
    static Task MPlay(LeaveAnyBattlefieldToMeHbrgoSaki card) => NewPlay(card);
    static MatsuokaBlowAwayPower MAbility => Hero.GetPower<MatsuokaBlowAwayPower>()!;
    static async Task MStart(Player? player = null)
    {
        player ??= Player; Set(player.PlayerCombatState!, "<TurnNumber>k__BackingField", player.PlayerCombatState!.TurnNumber + 1);
        foreach (var power in player.Creature.Powers.ToArray()) await power.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), player);
    }
    static async Task MAfterReceive(Creature target, DamageResult result, Creature? dealer, ValueProp props, CardModel? source)
    {
        MethodInfo method = typeof(Hook).GetMethods(F).Single(m => m.Name == "AfterDamageReceived");
        object?[] args = method.GetParameters().Select(p => p.ParameterType.IsInstanceOfType(RunState) ? (object)RunState :
            p.ParameterType.IsInstanceOfType(Combat) ? Combat : typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType) ? new ThrowingPlayerChoiceContext() :
            p.ParameterType == typeof(Creature) ? p.Name == "dealer" ? dealer : target : p.ParameterType == typeof(DamageResult) ? result :
            p.ParameterType == typeof(ValueProp) ? props : p.ParameterType == typeof(CardModel) ? source : throw new Exception(p.ToString())).ToArray();
        await (Task)Invoke(method, null, args)!;
    }
    static async Task MAfterGiven(Creature target, DamageResult result, Creature? dealer, ValueProp props, CardModel? source)
    {
        MethodInfo method = typeof(Hook).GetMethods(F).Single(m => m.Name == "AfterDamageGiven");
        object?[] args = method.GetParameters().Select(p => p.ParameterType.IsInstanceOfType(Combat) ? (object)Combat :
            typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType) ? new ThrowingPlayerChoiceContext() : p.ParameterType == typeof(Creature) ? p.Name == "dealer" ? dealer : target :
            p.ParameterType == typeof(DamageResult) ? result : p.ParameterType == typeof(ValueProp) ? props : p.ParameterType == typeof(CardModel) ? source : throw new Exception(p.ToString())).ToArray();
        await (Task)Invoke(method, null, args)!;
    }
    static bool MDamageLeaf(object[] __args, ref Task<IEnumerable<DamageResult>> __result)
    { __result = MDamageBody(__args); return false; }
    static async Task<IEnumerable<DamageResult>> MDamageBody(object[] args)
    {
        Type api = Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!;
        var targets = (IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets", F)!, null, new object?[] { args })!;
        Creature? dealer = args[4] as Creature; ValueProp props = (ValueProp)args[3]; decimal raw = (decimal)args[2]; CardModel? card = args[5] as CardModel;
        var result = new List<DamageResult>();
        foreach (Creature target in targets)
        {
            decimal final = MZero || MDodge ? 0m : Actual(target, dealer, raw, props, card);
            await (Task)NativeHookBeforeReceive(target, dealer, final, props)!;
            if (MDodge) Call("HpOnlyDamageContext", "RecordDodge", target);
            decimal blocked = (target.PetOwner?.Creature ?? target).DamageBlockInternal(final, props);
            decimal lost = Math.Max(0m, final - blocked);
            DamageResult hit = target.LoseHpInternal(lost, props); Set(hit, "<BlockedDamage>k__BackingField", (int)blocked);
            Call("ItsMygoIncomingDamageCapture", "AppendResults", result, new[] { hit }); FDamage.Add((dealer, target, final, blocked, lost));
            if (card is LeaveAnyBattlefieldToMeHbrgoSaki)
            {
                MCaptured ??= ExecutionContext.Capture();
                MCoefficients.Add((decimal)Call("MatsuokaBlowAwayCombatHelper", "GetDestructionGainMultiplier", card, target, dealer, props)!);
            }
            await MAfterGiven(target, hit, dealer, props, card);
            if (target.IsAlive) await MAfterReceive(target, hit, dealer, props, card);
        }
        return result;
    }
    static bool MAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        if (__instance.ModelSource is not LeaveAnyBattlefieldToMeHbrgoSaki) return FCounterLeaf(__instance, ref __result);
        __result = MAttackBody(__instance); return false;
    }
    static async Task<AttackCommand> MAttackBody(AttackCommand command)
    {
        var card = (CardModel)command.ModelSource!;
        var target = (Creature)typeof(AttackCommand).GetField("_singleTarget", F)!.GetValue(command)!;
        await Hook.BeforeAttack(Combat, command);
        decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(command)!;
        int hits = (int)Hook.ModifyAttackHitCount(Combat, command, (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(command)!);
        Call("HighHitDamageVisualHelper", "SetResolvedHitCount", command, (decimal)hits); MStrikes.Add((raw, hits, target));
        for (int i = 0; i < hits; i++) command.AddResultsInternal(await Command(command.Attacker, raw, ValueProp.Move, card, new[] { target }));
        await Hook.AfterAttack(Combat, new ThrowingPlayerChoiceContext(), command); return command;
    }
    static async Task MBasicCases()
    {
        var card = MReset(); Check(card.Type == CardType.Power && card.Rarity == CardRarity.Rare && card.EnergyCost.GetWithModifiers(CostModifiers.All) == 2, "83 base is a native two-energy rare power");
        await MPlay(card); Hero.GainBlockInternal(100000000); await FMove(6, 6, 6);
        Equal(MAbility.BlowAwayCount, 1, "83 real fully-blocked multi-command enemy move triggers only once this round");
        Equal(MPotency(Hero.GetPower<HbrDefenseUpPower>()!), 30m, "83 first hit grants actual30percent defense");
        Check(FDamage[1].Final < FDamage[0].Final && FDamage[2].Final == FDamage[1].Final, "83 later segments immediately use defense granted after the first segment");
        Equal(Hero.GetPower<HbrComboSmallPower>()!.Amount, 5, "83 one existing small-combo application has five uses");
        await Hero.GetPower<HbrDefenseUpPower>()!.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, new[] { Enemy });
        Check(!Hero.HasPower<HbrDefenseUpPower>(), "83 defense expires through its real enemy-turn-end callback");
        var second = MCard(); await MPlay(second); await FMove(6); Equal(MAbility.BlowAwayCount, 1, "83 a second physical power cannot reset this round's receipt");
        Equal(Hero.Powers.OfType<MatsuokaBlowAwayPower>().Count(), 1, "83 duplicate casts share exactly one ability");
        await MStart(); await FMove(6); Equal(MAbility.BlowAwayCount, 2, "83 actual next player-turn callback resets one use");
        card = MReset([0]); Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), 1, "83 Flash lowers actual cost to one");
        card = MReset(); await MPlay(card); MZero = true; await FMove(6); Equal(MAbility.BlowAwayCount, 1, "83 actual zero-loss result still triggers Blow Away");
        card = MReset(); await MPlay(card); MDodge = true; await FMove(6); Equal(MAbility.BlowAwayCount, 0, "83 canonical dodge receipt excludes a true dodge");
        MDodge = false; MZero = true; await FMove(6); Equal(MAbility.BlowAwayCount, 1, "83 earlier dodge did not spend the round's real hit receipt");
        card = MReset(); await MPlay(card); var dp = NewDp(5000); await FMove(.06m); Check(dp.Dp < 5000 && MAbility.BlowAwayCount == 1, "83 real DP absorption triggers even with no HP loss: dp=" + dp.Dp + " count=" + MAbility.BlowAwayCount + " final=" + FDamage.Last().Final + " blocked=" + FDamage.Last().Blocked + " lost=" + FDamage.Last().Lost);
        card = MReset(kinds: 6); await MPlay(card); dp = NewDp(20); await FMove(6); Equal(dp.Dp, 20, "83 requirement79 still preserves lethal DP"); Equal(MAbility.BlowAwayCount, 1, "83 requirement79's cancelled native hit also triggers Blow Away");
        card = MReset(); await MPlay(card); await FMoveTo([6m], [Hero], ValueProp.Unpowered); Equal(MAbility.BlowAwayCount, 0, "83 unpowered effect does not count as an enemy attack");
        card = MReset(hbr: false); await MPlay(card); Check(Hero.GetPower<MatsuokaBlowAwayPower>() == null, "83 HBR-only ability is inactive in legacy mode");
    }
    static async Task MUpgradeCases()
    {
        var card = MReset([2, 6]); await MPlay(card); Hero.GainBlockInternal(100000000); await FMove(6);
        Equal(MPotency(Hero.GetPower<HbrCriticalRateUpPower>()!), 70, "83 Rotor Kick grants actual70percent critical rate"); Equal(Hero.GetPower<HbrCriticalRateUpPower>()!.Amount, 1, "83 critical rate has one counted attack");
        Equal(Hero.GetPower<HbrComboMediumPower>()!.Amount, 3, "83 Near and Far grants one medium-combo application"); Check(Hero.GetPower<HbrComboSmallPower>() == null, "83 upgraded combo replaces rather than adds small combo");
        card = MReset([5]); await MPlay(card); Hero.GainBlockInternal(100000000);
        for (int i = 0; i < 8; i++)
        {
            foreach (PowerModel p in Hero.Powers.Where(p => p is HbrDefenseUpPower or HbrComboSmallPower).ToArray()) await PowerCmd.Remove(p);
            if (i != 0) await MStart(); await FMove(6); Equal(MPotency(Hero.GetPower<HbrDefenseUpPower>()!), Math.Min(60, 30 + i * 5), "83 Scars base grows after prior hits and caps at60, trigger" + (i + 1));
        }
        card = MReset([5]); await MPlay(card); Equal(MAbility.BlowAwayCount, 0, "83 a new combat starts with no Scars growth");
        card = MReset([4]); await MPlay(card); var dp = NewDp(0); MZero = true; await FMove(6); int recovered = dp.Dp; Check(recovered > 0, "83 first Blow Away restores actual DP through the ordinary pool");
        await MStart(); await FMove(6); Equal(dp.Dp, recovered, "83 Road to Stardom restores only once per battle");
        await MPlay(MCard(4)); await MStart(); await FMove(6); Equal(dp.Dp, recovered, "83 duplicate casts cannot renew the battle recovery receipt");
        card = MReset([4]); await MPlay(card); dp = NewDp(); int maximum = dp.MaxDp; Set(dp, "_dp", maximum - 1); MZero = true; await FMove(6); Equal(dp.Dp, maximum, "83 recovery caps at ordinary maximum DP");
    }
    static async Task MTargetCases()
    {
        var card = MReset([3]); await MPlay(card); Player peer = SupportTeammate(); Set(peer, "<NetId>k__BackingField", 2UL); Set(RunState, "_players", new List<Player> { Player, peer });
        Creature phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, peer)!; Power<PhantomPower>(phantom, 1);
        Creature pet = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature, phantom, pet });
        foreach (Creature ally in new[] { peer.Creature, phantom, pet }) { Set(ally, "_currentHp", 100000000); Set(ally, "_maxHp", 100000000); Set(ally, "_block", 0); }
        Hero.GainBlockInternal(100000000); await FMoveTo([.01m], [pet], ValueProp.Move); Equal(MAbility.BlowAwayCount, 0, "83 ordinary pet's use of hero block cannot count as a hero hit");
        await FMoveTo([.01m], [phantom], ValueProp.Move); Equal(MAbility.BlowAwayCount, 0, "83 physical phantom hit does not trigger the hero's ability");
        await FMoveTo([.01m], [peer.Creature], ValueProp.Move); Equal(MAbility.BlowAwayCount, 0, "83 teammate hit does not trigger the owner's ability");
        await FMove(6); Check(new[] { Hero, peer.Creature, phantom }.All(c => c.HasPower<HbrDefenseUpPower>()) && !pet.HasPower<HbrDefenseUpPower>(), "83 Cheers gives all real player/main-phantom support targets defense, excluding pets: " + string.Join(",", new[] { Hero, peer.Creature, phantom, pet }.Select(c => c.IsAlive + ":" + c.HasPower<HbrDefenseUpPower>())));
        Check(Hero.HasPower<HbrComboSmallPower>() && !peer.Creature.HasPower<HbrComboSmallPower>() && !phantom.HasPower<HbrComboSmallPower>(), "83 Cheers never spreads the owner's combo");
        await MStart(); var fake = new DamageResult(Hero, ValueProp.Move) { UnblockedDamage = 1 }; Call("CombatAttackHitHelper", "BindPhysical", new[] { fake }, Enemy, null, null, 0m, null, phantom);
        await MAfterReceive(Hero, fake, Enemy, ValueProp.Move, null); Equal(MAbility.BlowAwayCount, 1, "83 a phantom overflow result naming hero retains original physical identity");
        card = MReset(); await MPlay(card); peer = SupportTeammate(); Set(RunState, "_players", new List<Player> { Player, peer }); Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        Set(peer.Creature, "_currentHp", 100000000); Set(peer.Creature, "_maxHp", 100000000);
        var peerCard = (LeaveAnyBattlefieldToMeHbrgoSaki)FCard(typeof(LeaveAnyBattlefieldToMeHbrgoSaki), peer);
        await (Task)Native(peerCard, "OnPlay", new ThrowingPlayerChoiceContext(), Play(peerCard))!;
        await FMoveTo([6m], [Hero, peer.Creature], ValueProp.Move);
        Check(MAbility.BlowAwayCount == 1 && peer.Creature.GetPower<MatsuokaBlowAwayPower>()!.BlowAwayCount == 1, "83 cooperative owners each take only their own actual hit receipt");
    }
    static async Task MStrikeCases()
    {
        var card = MReset([1]); var gauge = NewGauge(); Power<MatsuokaPlayObserver>(Hero, 1); int rng = RngCount(RunState.Rng.CombatTargets);
        await ChasePlay(card); Check(MStrikes.Single() is { Raw: 2.16m, Hits: 3 } && MStrikes.Single().Target == Enemy, "83 Concentrated Strike requests36percent three times against one actual random enemy");
        Check(MCoefficients.Count == 3 && MCoefficients.All(v => v == 1m / 3m), "83 direct strike uses coefficient1 normalized exactly once");
        Equal(RngCount(RunState.Rng.CombatTargets), rng + 1, "83 random enemy selection consumes exactly one combat-target RNG draw");
        Check(MatsuokaPlayObserver.Plays.SequenceEqual(new[] { CardType.Power }), "83 native wrapper emits one power-card play without invented attack-card events");
        Equal(gauge.PreciseCharge, 17.5m, "83 normal power charge10 plus three connected hit charges2point5"); Equal(Player.PlayerCombatState!.Energy, 18, "83 native power wrapper pays two energy only once");
        Equal((decimal)Call("MatsuokaBlowAwayCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!, 1, "83 coefficient scope is closed after actual play");
        ExecutionContext.Run(MCaptured!, _ => Equal((decimal)Call("MatsuokaBlowAwayCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!, 1, "83 captured closed strike context does not leak coefficient"), null);
    }
    static async Task MSavedAndTurnCases()
    {
        var card = MReset([4, 5]); await MPlay(card); MZero = true; await FMove(6);
        var options = new JsonSerializerOptions { IncludeFields = true }; GSaveType(typeof(MatsuokaBlowAwayPower));
        string json = JsonSerializer.Serialize(SavedProperties.From(MAbility), options); var clone = (MatsuokaBlowAwayPower)ModelDb.Power<MatsuokaBlowAwayPower>().ToMutable();
        JsonSerializer.Deserialize<SavedProperties>(json, options)!.Fill(clone); Check(clone.UsedThisRound && clone.RecoveredThisBattle && clone.BlowAwayCount == 1 && clone.UpgradeFlags == MAbility.UpgradeFlags, "83 native SavedProperties JSON Fill preserves flags, hit and battle recovery receipts");
        Type coordinator = Helper("PersonalImmediateTurnCoordinator"); FieldInfo current = coordinator.GetField("_current", F)!, combat = coordinator.GetField("_combat", F)!;
        object? old = current.GetValue(null), oldCombat = combat.GetValue(null); Type sessionType = coordinator.GetNestedType("Session", F)!;
        object session = Activator.CreateInstance(sessionType, true)!; sessionType.GetField("Owner", F)!.SetValue(session, Player); sessionType.GetField("Combat", F)!.SetValue(session, Combat);
        try { current.SetValue(null, session); combat.SetValue(null, Combat); Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Player);
            Check((bool)Call("PersonalImmediateTurnCoordinator", "IsActiveFor", Player)!, "83 personal-turn fixture owns a real live player-side session");
            Call("MatsuokaBlowAwayCombatHelper", "TurnStart", Player); await FMove(6); Equal(MAbility.BlowAwayCount, 2, "83 actual personal-turn identity gives a new round receipt"); }
        finally { current.SetValue(null, old); combat.SetValue(null, oldCombat); }
        await FMove(6); Equal(MAbility.BlowAwayCount, 2, "83 resuming ordinary turn does not create a fresh receipt");
    }
    public static void RunMatsuoka(string path)
    {
        bool ok = false; string? error = null; ulong? previousLocal = LocalContext.NetId;
        try
        {
            Install(); Localization(); LocalContext.NetId = 1UL; var h = new Harmony(ProductionId + ".matsuoka83");
            foreach (Type type in FMembers.Concat(new[] { typeof(LeaveAnyBattlefieldToMeHbrgoSaki), typeof(MatsuokaBlowAwayPower), typeof(HbrDefenseUpPower), typeof(HbrComboSmallPower), typeof(HbrComboMediumPower), typeof(HbrCriticalRateUpPower), typeof(MatsuokaPlayObserver), typeof(ThirtyOneFCollection), typeof(Overclock), typeof(PhantomPower), typeof(MomentOfReign) }))
            { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
            foreach (string name in new[] { "CreatureDamageBlockInternalPatch", "ThirtyOneBEnemyMoveScopePatch", "ThirtyOneBEnemyActualTargetPatch", "PhantomDamageTargetPatch", "PhantomBeforeDamageReceivedPatch", "HighHitDamageAttackScopePatch", "HighHitDamageCommandScopePatch", "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch" })
                Check(h.CreateClassProcessor(Mod.GetTypes().Single(t => t.Name == name)).Patch().Count > 0, "83 installs production " + name);
            void Patch(MethodBase m, string name) => h.Patch(m, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(name, F)!));
            foreach (Type type in new[] { typeof(RunState), typeof(CombatState) }) h.Patch(type.GetMethod("IterateHookListeners", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FListeners), F)!) { priority = Priority.First + 200 });
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(FormNotEnding)); Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip)); Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent", F)!, nameof(VisualTask));
            foreach (var method in typeof(Cmd).GetMethods(F).Where(m => m.Name is "Wait" or "CustomScaledWait" && m.ReturnType == typeof(Task))) Patch(method, nameof(VisualTask));
            foreach (var method in typeof(SfxCmd).GetMethods(F).Where(m => m.Name == "Play")) Patch(method, nameof(Skip)); foreach (var method in typeof(ThinkCmd).GetMethods(F).Where(m => m.Name == "Play")) Patch(method, nameof(Skip));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock)); Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (var method in typeof(CardPileCmd).GetMethods(F).Where(m => m.Name == "Add" && m.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(method, nameof(CrackPileAdd));
            h.Unpatch(Canonical, typeof(EffectChecks).GetMethod(nameof(Terminal), F)!); h.Patch(Canonical, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MDamageLeaf), F)!) { priority = Priority.Last });
            h.Patch(typeof(AttackCommand).GetMethod("Execute", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MAttackLeaf), F)!) { priority = Priority.Last });
            try { MBasicCases().GetAwaiter().GetResult(); MUpgradeCases().GetAwaiter().GetResult(); MTargetCases().GetAwaiter().GetResult(); MStrikeCases().GetAwaiter().GetResult(); MSavedAndTurnCases().GetAwaiter().GetResult(); ok = true; }
            finally { h.UnpatchAll(h.Id); }
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        finally { LocalContext.NetId = previousLocal; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual card/power bodies, native Monster.PerformMove, canonical command scopes, physical receipts, block/HP/DP mutation, hook dispatch, common arithmetic, and wrapper resources. Canonical terminal, animation/wait/UI leaves are controlled; no Godot rendering or live network." }, new JsonSerializerOptions { WriteIndented = true }));
        if (!ok) throw new Exception("Inspect " + path); Console.WriteLine("PASS " + Passed.Count + " Matsuoka83 checks");
    }
}
