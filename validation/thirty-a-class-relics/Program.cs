using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using LeafSlimeS = MegaCrit.Sts2.Core.Models.Monsters.LeafSlimeS;

static class ThirtyAClassStandalone { public static void Run(string path) => EffectChecks.RunThirtyAClasses(path); }
static partial class EffectChecks
{
    static readonly Harmony ClassHarmony = new("hbr.thirty-a.class-relics.native-checks");
    static readonly List<(Player Owner, decimal Count)> ClassDraws = [], ClassEnergy = [];
    static readonly List<(int Depth, decimal Percent)> ClassAmplifications = [];
    static MomentOfReign ClassReign = null!;
    static CombatRoom ClassRoom = null!;
    static RoomType ClassRoomType;
    static object? ClassExtraToken;
    static int ClassNativeMoves;
    static Type ClassEntryType => Mod.GetType("HBRGOSakiMod.Core.Models.Relics.PhantomRosterEntry", true)!;
    static object ClassEntry(int id, Type? type = null, int experience = 0, int value = 1) => ClassEntryType.GetConstructors(F).Single(m => m.GetParameters().Length == 10).Invoke([id, ModelDb.GetId(type ?? typeof(SmallHopper)), 0, experience, value, 0m, 0m, 0m, "", 0m]);
    static Array ClassEntries(params object[] entries) { Array result = Array.CreateInstance(ClassEntryType, entries.Length); for (int i = 0; i < entries.Length; i++) result.SetValue(entries[i], i); return result; }
    static object? ClassCall(string method, params object?[] args) => Native(ClassReign, method, args);
    static int ClassXp(int id) => ((IEnumerable)ClassCall("GetRosterEntries")!).Cast<object>().Where(row => (int)ClassEntryType.GetProperty("InstanceId")!.GetValue(row)! == id).Select(row => (int)ClassEntryType.GetProperty("Experience")!.GetValue(row)!).Single();
    static T ClassRelic<T>() where T : RelicModel
    {
        if (!ModelDb.Contains(typeof(T))) ModelDb.Inject(typeof(T));
        var result = (T)ModelDb.Relic<T>().ToMutable(); Set(result, "_owner", Player);
        ((List<RelicModel>)typeof(Player).GetField("_relics", F)!.GetValue(Player)!).Add(result); return result;
    }
    static T ClassCard<T>() where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().ToMutable(); Set(card, "_owner", Player);
        Player.PlayerCombatState!.Hand.AddInternal(card); return card;
    }
    static void ClassReset(bool hbr = true)
    {
        FormReset(hbr); Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki30A>());
        ClassDraws.Clear(); ClassEnergy.Clear(); ClassAmplifications.Clear(); ClassNativeMoves = 0; ClassExtraToken = null;
        ClassReign = ClassRelic<MomentOfReign>(); ClassCall("SetRosterEntries", ClassEntries(ClassEntry(1), ClassEntry(2, typeof(LeafSlimeS))));
        typeof(MomentOfReign).GetProperty("ActiveRosterEntryId", F)!.SetValue(ClassReign, 1);
        ClassRoom = Blank<CombatRoom>(); ClassRoomType = RoomType.Monster;
        Set(RunState, "_currentRooms", new List<AbstractRoom> { ClassRoom });
        Set(Player, "<NetId>k__BackingField", 101UL);
    }
    static bool ClassDrawLeaf(decimal count, Player player, ref Task<IEnumerable<CardModel>> __result) { ClassDraws.Add((player, count)); __result = Task.FromResult(Enumerable.Empty<CardModel>()); return false; }
    static bool ClassEnergyLeaf(decimal amount, Player player, ref Task __result) { ClassEnergy.Add((player, amount)); __result = Task.CompletedTask; return false; }
    static bool ClassRoomLeaf(CombatRoom __instance, ref RoomType __result) { if (!ReferenceEquals(__instance, ClassRoom)) return true; __result = ClassRoomType; return false; }
    static bool ClassTokenLeaf(Player player, ref object? __result) { __result = player == Player ? ClassExtraToken : null; return false; }
    static bool ClassListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result = Combat.Creatures.SelectMany(c => c.Powers).Cast<AbstractModel>().Concat(Player.Relics.Where(relic => relic is ThirtyAClassRelic or MomentOfReign)).ToArray(); return false;
    }
    static void ClassObserveAmp(decimal __result) => ClassAmplifications.Add((Depth, __result));
    static Creature ClassPhantom(bool attack = false, int hits = 1, bool minion = false)
    {
        var model = (MonsterModel)ModelDb.Monster<SmallHopper>().ToMutable();
        var actor = (Creature)H("Actor", model, null, Player)!;
        Set(actor, "_currentHp", 5000); Set(actor, "_maxHp", 5000);
        Set(Combat, "_allies", new List<Creature> { Hero, actor });
        var power = Power<PhantomPower>(actor, 1); Set(power, "<IsSummonedMinion>k__BackingField", minion);
        if (minion) Power<MinionPower>(actor, 1);
        MoveState move = attack ? new("CLASS_ATTACK", _ => { ClassNativeMoves++; return Task.CompletedTask; }, new MultiAttackIntent(6, hits))
            : new("CLASS_BUFF", _ => { ClassNativeMoves++; return Task.CompletedTask; }, new BuffIntent());
        move.FollowUpState = move;
        Set(model, "_moveStateMachine", new MonsterMoveStateMachine(new List<MonsterState> { move }, move));
        Set(model, "<NextMove>k__BackingField", move); model.RunRng = RunState.Rng;
        return actor;
    }
    static Task ClassAct(Creature phantom, bool immediate = true) => (Task)Call("PhantomHelper", "PerformCurrentIntent", phantom, Enemy, 0, immediate)!;
    static Task ClassAfterPlay(CheckmateThirtyA relic, CardModel card, bool automatic = false)
    {
        CardPlay play = Play(card); Set(play, "<IsAutoPlay>k__BackingField", automatic);
        return relic.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(), play);
    }
    static async Task ClassLetterAndAttackCases()
    {
        ClassReset(); var letter = ClassRelic<LetterArrivesThirtyA>(); Creature pet = ClassPhantom();
        await ClassAct(pet); Check(ClassNativeMoves == 1 && ClassDraws.Sum(row => row.Count) == 1, "actual non-attack phantom intent completes before first-action draw");
        await ClassAct(pet, false); Equal(ClassDraws.Sum(row => row.Count), 1m, "natural turn-end and immediate actions share one letter combat receipt");
        await letter.BeforeCombatStart(); await ClassAct(pet); Equal(ClassDraws.Sum(row => row.Count), 2m, "next combat resets the first-action letter receipt");
        ClassReset(); letter = ClassRelic<LetterArrivesThirtyA>(); pet = ClassPhantom(minion: true); await ClassAct(pet);
        Equal(ClassDraws.Count, 0, "summoned minion action does not consume its owner's main-phantom letter");

        ClassReset(); var checkmate = ClassRelic<CheckmateThirtyA>(); pet = ClassPhantom(true, 3); await ClassAct(pet); decimal[] baseline = Hits.Select(hit => hit.Final).ToArray();
        Check(baseline.Length == 3, "actual phantom multi-attack produces three native damage commands");
        var attack = ClassCard<StrikeHbrgoSaki>(); await ClassAfterPlay(checkmate, attack); Hits.Clear(); ClassAmplifications.Clear(); await ClassAct(pet);
        Check(Hits.Count == 3 && ClassAmplifications.Any(row => row.Percent == 25m), "Checkmate reaches the actual shared damage pipeline throughout a real three-hit intent");
        Check(Hits.Select(hit => hit.Final).Zip(baseline).All(pair => pair.First > pair.Second), "every real damage segment receives Checkmate amplification");
        Hits.Clear(); ClassAmplifications.Clear(); await ClassAfterPlay(checkmate, attack, true); await ClassAct(pet);
        Check(ClassAmplifications.All(row => row.Percent == 0m), "second attack card including automatic play cannot rearm the same turn");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); await ClassAfterPlay(checkmate, attack, true); Hits.Clear(); ClassAmplifications.Clear(); DoNested = true; await ClassAct(pet); DoNested = false;
        Check(ClassAmplifications.Any(row => row.Depth == 1 && row.Percent == 25m) && ClassAmplifications.Where(row => row.Depth > 1).All(row => row.Percent == 0m), "next-turn automatic attack arms one scope while nested same-dealer reaction cannot inherit it");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 3); await ClassAfterPlay(checkmate, attack); await checkmate.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [Enemy], Combat);
        Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Enemy); Hits.Clear(); ClassAmplifications.Clear(); await ClassAct(pet);
        Check(ClassAmplifications.All(row => row.Percent == 0m), "unused Checkmate expires when that player turn has ended");

        ClassReset(); checkmate = ClassRelic<CheckmateThirtyA>(); attack = ClassCard<StrikeHbrgoSaki>(); await ClassAfterPlay(checkmate, attack);
        await ClassAct(ClassPhantom()); Check(checkmate.OrdinaryArmed, "an actual non-attack phantom action leaves the next-attack Checkmate receipt armed");
        Hits.Clear(); ClassAmplifications.Clear(); await ClassAct(ClassPhantom(true));
        Check(ClassAmplifications.Any(row => row.Percent == 25m) && !checkmate.OrdinaryArmed, "the subsequent real attack consumes the preserved Checkmate receipt");

        ClassReset(); checkmate = ClassRelic<CheckmateThirtyA>(); pet = ClassPhantom(true); attack = ClassCard<StrikeHbrgoSaki>(); await ClassAfterPlay(checkmate, attack);
        ClassExtraToken = new object(); await ClassAfterPlay(checkmate, attack); await ClassAct(pet); ClassExtraToken = null;
        Check(checkmate.OrdinaryArmed, "inserted turn uses its own first-attack receipt without erasing suspended normal-turn benefit");
        Hits.Clear(); ClassAmplifications.Clear(); await ClassAct(pet); Check(ClassAmplifications.Any(row => row.Percent == 25m), "returning to the ordinary turn restores that turn's unused Checkmate benefit");
    }
    static async Task ClassExperienceAndCaptureCases()
    {
        ClassReset(); ClassRelic<TicketStubThirtyA>(); Power<PhantomFamiliarityPower>(Hero, 1);
        Equal((int)ClassCall("AddCombatExperienceBonus", 1)!, 3, "raw1 times Familiarity2 times Ticket1point5 floors only after composition to3");
        ClassCall("AddExperienceToAllRosterEntries", 3); Equal(ClassReign.PendingCombatExperience, 9, "real active roster acquisition queues multiplicative experience once");
        await ClassReign.AfterCombatEnd(null!); Equal(ClassXp(1), 9, "actual post-combat commit does not multiply queued experience again"); Equal(ClassXp(2), 9, "inactive roster receives its own resolved award once");
        await ClassReign.AfterCombatEnd(null!); Equal(ClassXp(1), 9, "repeated combat-end commit cannot replay resolved Ticket experience");
        Equal((int)ClassCall("AddCombatExperienceBonus", int.MaxValue)!, 1000000000, "composed Ticket multiplier saturates existing experience cap without overflow");
        ClassReset(); ClassRelic<TicketStubThirtyA>(); Power<PhantomFamiliarityPower>(Hero, 8);
        ClassCall("SetRosterEntries", ClassEntries(ClassEntry(1), ClassEntry(2, typeof(LeafSlimeS), value: 4)));
        CombatInProgress(false);
        Check((bool)ClassCall("TryUpgradeRosterEntry", 1, new[] { 2 }, null, false)!, "actual out-of-combat fusion is accepted"); Equal(ClassXp(1), 6, "permanent Ticket applies to manual fusion without reusing combat Familiarity");
        ClassReset(false); ClassRelic<TicketStubThirtyA>(); Equal((int)ClassCall("AddCombatExperienceBonus", 4)!, 4, "new class relic does not change legacy numeric mode");

        foreach (RoomType room in new[] { RoomType.Monster, RoomType.Elite, RoomType.Boss })
        {
            ClassReset(); ClassRelic<FormerFoesThirtyA>(); ClassRoomType = room; ClassCall("SetRosterEntries", ClassEntries());
            var victim = (Creature)H("Actor", ModelDb.Monster<SmallHopper>().ToMutable(), null, null)!; Set(victim, "_currentHp", 0);
            await ClassReign.AfterDeath(new ThrowingPlayerChoiceContext(), victim, false, 0f);
            object first = ((IEnumerable)ClassCall("GetRosterEntries")!).Cast<object>().Single();
            Equal((int)ClassEntryType.GetProperty("Level", F)!.GetValue(first)!, room == RoomType.Elite ? 2 : 1, "actual enemy death capture grants one starting level only in Elite room: " + room);
            await ClassReign.AfterDeath(new ThrowingPlayerChoiceContext(), victim, false, 0f);
            var rows = ((IEnumerable)ClassCall("GetRosterEntries")!).Cast<object>().ToArray();
            Equal((int)ClassEntryType.GetProperty("Level", F)!.GetValue(rows.Last())!, 1, "same monster already in roster keeps the preexisting acquisition rule: " + room);
        }
    }
    static async Task ClassNativeRevivalCases()
    {
        ClassReset(); var relic = ClassRelic<PhantomsOfBygoneDaysThirtyA>(); Creature pet = ClassPhantom();
        await ClassReign.AfterCurrentHpChanged(pet, 10m); Equal(ClassEnergy.Count, 0, "ordinary healing of a living phantom is not a revival");
        Set(pet, "_currentHp", 10); await ClassReign.AfterCurrentHpChanged(pet, 10m);
        Check(ClassEnergy.Sum(row => row.Count) == 1m && ClassDraws.Sum(row => row.Count) == 1m, "native phantom zero-to-positive HP notification grants one energy and card");
        Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Enemy); Set(pet, "_currentHp", 10); await ClassReign.AfterCurrentHpChanged(pet, 10m);
        Check(relic.PendingRewards == 1 && ClassEnergy.Sum(row => row.Count) == 1m, "enemy-turn revival queues rather than changing a suspended energy pool");
        await relic.AfterEnergyReset(Player); await relic.AfterEnergyReset(Player);
        Check(relic.PendingRewards == 0 && ClassEnergy.Sum(row => row.Count) == 2m && ClassDraws.Sum(row => row.Count) == 2m, "actual after-energy-reset relic hook drains delayed rewards exactly once");
        Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Player); await ClassReign.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero], Combat);
        Set(pet, "_currentHp", 10); await ClassReign.AfterCurrentHpChanged(pet, 10m); Check(relic.PendingRewards == 1, "revival before native turn energy reset also queues safely");
        await (Task)Invoke(typeof(PhantomsOfBygoneDaysThirtyA).GetMethod("AfterInsertedEnergyReset", F)!, null, Player)!;
        Equal(ClassEnergy.Sum(row => row.Count), 3m, "inserted-turn post-reset integration drains the pending award once");
    }
    public static void RunThirtyAClasses(string path)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Localization();
            foreach (Type type in new[] { typeof(MomentOfReign), typeof(SmallHopper), typeof(LeafSlimeS), typeof(PhantomPower), typeof(PhantomHighDimensionalShellPower), typeof(PhantomFamiliarityPower), typeof(StrikeHbrgoSaki), typeof(LetterArrivesThirtyA), typeof(FormerFoesThirtyA), typeof(CheckmateThirtyA), typeof(TicketStubThirtyA), typeof(PhantomsOfBygoneDaysThirtyA), typeof(ShelterRationCouponThirtyA), typeof(EcstaticCorridorPower), typeof(LingeringLifePower), typeof(MadlyInfatuatedHbrgoSaki), typeof(DistantPrayerHbrgoSaki) })
            { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
            foreach (Type t in new[] { typeof(CombatState), RunState.GetType() }) ClassHarmony.Patch(t.GetMethod("IterateHookListeners", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassListeners), F)!){ priority = Priority.First + 100 });
            ClassHarmony.Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" && method.GetParameters().Length == 4), prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassDrawLeaf), F)!));
            ClassHarmony.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy))!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassEnergyLeaf), F)!));
            ClassHarmony.Patch(typeof(CombatRoom).GetProperty(nameof(CombatRoom.RoomType))!.GetMethod!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassRoomLeaf), F)!));
            ClassHarmony.Patch(Helper("OverclockImmediateTurnCoordinator").GetMethod("GetTurnToken", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassTokenLeaf), F)!));
            ClassHarmony.Patch(Helper("ThirtyAClassRelicsHelper").GetMethod("GetDamageAmplificationPercent", F)!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassObserveAmp), F)!));
            Check(ClassHarmony.CreateClassProcessor(Mod.GetTypes().Single(type => type.Name == "HighHitDamageCommandScopePatch")).Patch().Count > 0, "installs real canonical direct-damage batch scope");
            foreach (MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(method => method.Name == "TriggerAnim" && method.ReturnType == typeof(Task))) ClassHarmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!));
            foreach (MethodInfo method in typeof(Cmd).GetMethods(F).Where(method => method.Name is "Wait" or "CustomScaledWait")) ClassHarmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!));
            ClassLetterAndAttackCases().GetAwaiter().GetResult(); ClassExperienceAndCaptureCases().GetAwaiter().GetResult(); ClassNativeRevivalCases().GetAwaiter().GetResult(); ClassShopCases().GetAwaiter().GetResult(); ClassSummonRevivalCases().GetAwaiter().GetResult(); ClassIsolationCases().GetAwaiter().GetResult(); ok = true;
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual phantom intent/action, canonical damage command scopes and common damage arithmetic; native power/roster/death/XP commits, successful merchant/removal envelopes, native SavedProperties round-trip, and countdown/card/power revival paths. Scene animation, selection, draw/energy gain and creature-scene insertion terminals are controlled. Extra-turn token lookup is controlled while real relic ledgers and after-reset hooks execute. No Godot rendering, live multiplayer, or shop/card selection UI is exercised." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " 30A class relic checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
