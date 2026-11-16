using System.Collections;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

sealed class ClassMerchantEntry : MerchantEntry
{
    internal bool Stocked = true, Succeeds = true;
    public override bool IsStocked => Stocked;
    internal ClassMerchantEntry(Player owner) : base(owner) { _cost = 10; }
    public override void CalcCost() { }
#if HBR_BETA_NATIVE
    protected override Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
#else
    protected override Task<(bool, int)> OnTryPurchase(MerchantInventory? inventory, bool ignoreCost)
#endif
        => Task.FromResult((Succeeds, Succeeds ? 10 : 0));
    protected override void ClearAfterPurchase() => Stocked = false;
    protected override void RestockAfterPurchase(MerchantInventory? inventory) => Stocked = true;
}
static partial class EffectChecks
{
    static Creature? ClassCreated;
    static bool ClassRemovalSucceeds;
    static bool ClassRemovalTerminal(MerchantCardRemovalEntry __instance, ref Task<(bool, int)> __result)
    {
        bool success = ClassRemovalSucceeds && !__instance.Used;
        if (success) Set(__instance, "<Used>k__BackingField", true);
        __result = Task.FromResult((success, success ? 10 : 0)); return false;
    }
    static bool ClassCreateLeaf(MonsterModel monster, CombatSide side, string? slot, ref Creature __result)
    {
        Creature actor = (Creature)H("Actor", monster, null, Player)!;
        ((List<Creature>)typeof(CombatState).GetField("_allies", F)!.GetValue(Combat)!).Add(actor);
        MoveState move = new("CLASS_SUMMON", _ => Task.CompletedTask, new BuffIntent()); move.FollowUpState = move;
        Set(monster, "_moveStateMachine", new MonsterMoveStateMachine(new List<MonsterState> { move }, move));
        Set(monster, "<NextMove>k__BackingField", move); monster.RunRng = RunState.Rng;
        ClassCreated = actor; __result = actor; return false;
    }
    static bool ClassPrayerSelection(ref Task<IReadOnlyList<int>> __result) { __result = Task.FromResult<IReadOnlyList<int>>(new[] { 2 }); return false; }
    static async Task<bool> ClassBuy(ClassMerchantEntry entry) => await (Task<bool>)Invoke(typeof(MerchantEntry).GetMethod(
#if HBR_BETA_NATIVE
        "OnTryPurchaseUnsynchronized",
#else
        "OnTryPurchaseWrapper",
#endif
        F)!, entry, null, false)!;
    static void ClassSetSavedType(Type type)
    {
        var native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } cache)
            cache.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [type]);
        else
        {
            cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
            cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]); cache.GetField("_initialized", F)!.SetValue(null, true);
        }
    }
    static async Task ClassShopCases()
    {
        ClassReset(); var coupon = ClassRelic<ShelterRationCouponThirtyA>(); Set(Player, "_gold", 100);
        Set(RunState, "_currentRooms", new List<AbstractRoom> { Blank<MerchantRoom>() });
        var noPrior = new ClassMerchantEntry(Player); Check(await ClassBuy(noPrior), "actual native merchant purchase completes without a previous phantom");
        Equal(ClassXp(1), 0m, "no phantom ever summoned means no coupon experience or exception");
        ClassCall("RememberSummonedForClassRelics", ClassEntry(2, typeof(MegaCrit.Sts2.Core.Models.Monsters.LeafSlimeS)));
        var failed = new ClassMerchantEntry(Player) { Succeeds = false }; Check(!await ClassBuy(failed), "actual native merchant failure returns false"); Equal(ClassXp(2), 0m, "failed native purchase cannot trigger coupon");
        var insufficient = new ClassMerchantEntry(Player); Set(Player, "_gold", 0); Check(!await ClassBuy(insufficient), "insufficient-gold native precheck rejects before purchase callback"); Equal(ClassXp(2), 0m, "insufficient purchase cannot trigger coupon"); Set(Player, "_gold", 100);
        var bought = new ClassMerchantEntry(Player); Check(await ClassBuy(bought), "actual successful merchant wrapper dispatches purchase hooks"); Equal(ClassXp(2), 1m, "coupon awards ordinary-combat base1 only to exact previously summoned roster instance"); Equal(ClassXp(1), 0m, "same owner active but different roster entry is not the coupon target");
        Check(!await ClassBuy(bought), "second click of sold native entry is rejected"); Equal(ClassXp(2), 1m, "duplicate rejected purchase cannot replay its experience reward");
        bought.Stocked = true; Check(await ClassBuy(bought), "restocked same merchant object can complete a new transaction"); Equal(ClassXp(2), 2m, "new successful transaction on same entry gains experience once again");
        ClassCall("SetRosterEntries", ClassEntries(ClassEntry(1))); Check(await ClassBuy(new ClassMerchantEntry(Player)), "purchase still succeeds after target phantom was consumed"); Equal(ClassXp(1), 0m, "consumed exact instance is not recreated or replaced by another phantom");

        ClassReset(); coupon = ClassRelic<ShelterRationCouponThirtyA>(); Set(Player, "_gold", 100); Set(RunState, "_currentRooms", new List<AbstractRoom> { Blank<MerchantRoom>() });
        ClassCall("RememberSummonedForClassRelics", ClassEntry(0));
        ClassCall("SetStoredPhantomHp", 0, 0m);
        ClassCall("SetPhantomBaseMaxHp", 0, 5000m);
        Check(await ClassBuy(new ClassMerchantEntry(Player)), "real native purchase supports actually summoned temporary fallback phantom");
        int temporaryId = ClassReign.LastSummonedRosterEntryId;
        Check(temporaryId > 2 && ClassXp(temporaryId) == 1, "temporary fallback gets its own fixed persistent identity without selecting an existing same-kind entry");
        Equal((decimal)ClassCall("GetStoredPhantomHp", temporaryId)!, 0m, "adopting a defeated temporary fallback preserves zero HP instead of reviving it");
        Equal((decimal)ClassCall("GetPhantomSummonHp", temporaryId, 5000m, false)!, 0m, "next normal summon still observes the fallback's carried defeated state");
        Check(await ClassBuy(new ClassMerchantEntry(Player)) && ClassXp(temporaryId) == 2, "later purchase grows that fixed fallback identity rather than inserting another copy");
        ClassSetSavedType(typeof(MomentOfReign)); var options = new JsonSerializerOptions { IncludeFields = true };
        string save = JsonSerializer.Serialize(SavedProperties.From(ClassReign), options);
        var restored = (MomentOfReign)ModelDb.Relic<MomentOfReign>().ToMutable(); Set(restored, "_owner", Player); JsonSerializer.Deserialize<SavedProperties>(save, options)!.Fill(restored);
        Check(restored.LastSummonedRosterEntryId == temporaryId && restored.SummonableMonsterIds == ClassReign.SummonableMonsterIds, "native saved-properties JSON round-trip retains exact last-summoned target and already awarded experience");
        ((List<RelicModel>)typeof(Player).GetField("_relics", F)!.GetValue(Player)!).Remove(ClassReign); ClassReign = restored; ((List<RelicModel>)typeof(Player).GetField("_relics", F)!.GetValue(Player)!).Add(restored);
        Equal(ClassXp(temporaryId), 2m, "loading purchase state itself cannot replay coupon reward");
        Check(await ClassBuy(new ClassMerchantEntry(Player)) && ClassXp(temporaryId) == 3, "new purchase after native saved-state restore awards exactly one further point");
        foreach (Type type in new[] { typeof(MerchantCardEntry), typeof(MerchantRelicEntry), typeof(MerchantPotionEntry) })
            Check(type.GetMethod(
#if HBR_BETA_NATIVE
                "OnTryPurchaseUnsynchronized",
#else
                "OnTryPurchaseWrapper",
#endif
                F)!.DeclaringType == typeof(MerchantEntry), "native shared success wrapper covers " + type.Name);
#if HBR_BETA_NATIVE
        Check(typeof(MerchantCardRemovalEntry).GetMethod("OnTryPurchaseUnsynchronized", F)!.DeclaringType == typeof(MerchantEntry), "beta removal service shares actual native purchase wrapper");
#else
        MethodInfo removalWrapper = typeof(MerchantCardRemovalEntry).GetMethods(F).Single(method => method.Name == "OnTryPurchaseWrapper" && method.GetParameters().Length == 3);
        Check(PatchProcessor.GetOriginalInstructions(removalWrapper.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", F)!).Count(instruction => instruction.operand is MethodInfo method && method.Name == "AfterItemPurchased") == 1, "stable removal service has one explicit post-success purchase hook in its actual alternate wrapper");
#endif
        var removal = Blank<MerchantCardRemovalEntry>(); Set(removal, "_player", Player); Set(removal, "_cost", 10);
#if HBR_BETA_NATIVE
        MethodInfo removalBody = typeof(MerchantCardRemovalEntry).GetMethod("OnTryPurchaseInternal", F)!;
        MethodInfo removalEnvelope = typeof(MerchantEntry).GetMethod("OnTryPurchaseUnsynchronized", F)!;
        object?[] removalArgs = [null, false];
#else
        MethodInfo removalBody = typeof(MerchantCardRemovalEntry).GetMethods(F).Single(method => method.Name == "OnTryPurchase" && method.GetParameters().Length == 3);
        MethodInfo removalEnvelope = removalWrapper;
        object?[] removalArgs = [null, false, true];
#endif
        ClassHarmony.Patch(removalBody, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassRemovalTerminal), F)!));
        ClassRemovalSucceeds = false; Check(!await (Task<bool>)Invoke(removalEnvelope, removal, removalArgs)!, "actual native removal-service envelope propagates cancelled selection"); Equal(ClassXp(temporaryId), 3m, "cancelled card-removal service awards no coupon experience");
        ClassRemovalSucceeds = true; Check(await (Task<bool>)Invoke(removalEnvelope, removal, removalArgs)!, "actual native removal-service envelope dispatches successful purchase hook"); Equal(ClassXp(temporaryId), 4m, "successful removal service awards one ordinary-combat experience");
        Check(!await (Task<bool>)Invoke(removalEnvelope, removal, removalArgs)!, "already-used removal service rejects repeated selection"); Equal(ClassXp(temporaryId), 4m, "already-used removal service cannot replay coupon reward");
        ClassHarmony.Unpatch(removalBody, HarmonyPatchType.Prefix, ClassHarmony.Id);
    }
    static async Task ClassSummonRevivalCases()
    {
        ClassHarmony.Patch(typeof(CombatState).GetMethod("CreateCreature", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassCreateLeaf), F)!));
        ClassHarmony.Patch(typeof(CreatureCmd).GetMethods(F).Single(method => method.Name == nameof(CreatureCmd.Add) && method.GetParameters() is var parameters && parameters.Length == 1 && parameters[0].ParameterType == typeof(Creature)), prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!));
        ClassHarmony.Patch(typeof(MomentOfReign).GetMethod("ArrangePhantomVisuals", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
        ClassHarmony.Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
        ClassHarmony.Patch(Mod.GetType("HBRGOSakiMod.Core.Models.Cards.ThirtyARosterSelection", true)!.GetMethod("SelectAtLeastLevelTotal", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ClassPrayerSelection), F)!));
        foreach (string mode in new[] { "initial", "countdown", "madly", "corridor", "prayer", "lingering" })
        {
            ClassReset(); var relic = ClassRelic<PhantomsOfBygoneDaysThirtyA>(); ClassCreated = null;
            if (mode == "initial")
                await (Task)ClassCall("Summon", ClassEntry(1), false, false, 1m)!;
            else
            {
                typeof(MomentOfReign).GetProperty("PendingPhantomReviveMonsterId", F)!.SetValue(ClassReign, ModelDb.GetId(typeof(HBRGOSakiMod.Core.Models.Monsters.SmallHopper)).ToString());
                typeof(MomentOfReign).GetProperty("PhantomReviveTurnNumber", F)!.SetValue(ClassReign, mode == "corridor" ? 2 : 1);
                if (mode == "countdown")
                    await ClassReign.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero], Combat);
                else if (mode == "madly")
                { var card = ClassCard<MadlyInfatuatedHbrgoSaki>(); await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), Play(card))!; }
                else if (mode == "corridor")
                    await PowerCmd.Apply<EcstaticCorridorPower>(new ThrowingPlayerChoiceContext(), Hero, 1m, Hero, null);
                else if (mode == "prayer")
                {
                    object levels = Invoke(ClassEntryType.GetMethod("AddLevels", F)!, ClassEntry(2, typeof(MegaCrit.Sts2.Core.Models.Monsters.LeafSlimeS)), 2)!;
                    ClassCall("SetRosterEntries", ClassEntries(ClassEntry(1), levels));
                    var card = ClassCard<DistantPrayerHbrgoSaki>(); await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), Play(card))!;
                }
                else
                {
                    typeof(MomentOfReign).GetProperty("PhantomReviveTurnNumber", F)!.SetValue(ClassReign, 0);
                    Creature dead = ClassPhantom(); Set(dead, "_currentHp", 0); Power<LingeringLifePower>(Hero, 1);
                    await ClassReign.AfterDeath(new ThrowingPlayerChoiceContext(), dead, false, 0f);
                }
            }
            Check(ClassCreated?.IsAlive == true && ClassCreated.GetPower<PhantomPower>() != null, "actual summon/revive path creates and configures a living main phantom: " + mode);
            // Native countdown revives before energy reset and must wait for it.
            if (mode == "countdown") await relic.AfterEnergyReset(Player);
            Equal(ClassEnergy.Sum(row => row.Count), mode == "initial" ? 0m : 1m, "revival reward grants once while initial summon grants nothing: " + mode);
            if (mode != "initial") Check(ClassReign.LastSummonedRosterEntryId == 1, "actual revive retains precise summon identity: " + mode);
        }
    }
}
