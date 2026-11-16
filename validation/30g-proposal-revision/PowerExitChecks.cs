using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Models.Capabilities;

public sealed class ThirtyGPowerExitProbe : ThirtyOneBGardenCard
{
    protected override int FlashEffectId => -1;
    public static int Effects;
    public override string Title => "Power exit probe";
    public ThirtyGPowerExitProbe() : base(1, CardType.Power, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { Effects++; return Task.CompletedTask; }
}

static partial class EffectChecks
{
    static readonly Dictionary<CardModel, NCard> RTable = new(ReferenceEqualityComparer.Instance);
    static readonly HashSet<NCard> RClaimed = new(ReferenceEqualityComparer.Instance);
    static readonly List<CardModel> RFlights = [];
    static readonly List<object> RPowerExitResults = [];
    static readonly List<string> RPowerExitGuardAudit = [];
    static Type? RPowerExitHelper;
    static bool RExpectOldPowerExit => Environment.GetEnvironmentVariable("HBR_POWER_EXIT_EXPECT_OLD") == "1";
    static CardModel? RGenerateFailure;
    static bool RFlightFailure;
    static int RLostStarEffects;

    // This is the explicit whole UI leaf: no constructor or native Godot method
    // is called on these NCard tokens. The production guard IL is audited below.
    static bool RUnclaimedTableLeaf(CardModel card, ref NCard? __result)
    {
        __result = card?.Type == CardType.Power && RTable.TryGetValue(card, out NCard? token) && !RClaimed.Contains(token) ? token : null;
        return false;
    }
    static bool RPowerFlightLeaf(CardModel __instance, ref Task __result)
    {
        RFlights.Add(__instance);
        if (RFlightFailure) __result = Task.FromException(new InvalidOperationException("controlled-power-flight-ui-failure"));
        else { RTable.Remove(__instance); __result = Task.CompletedTask; }
        return false;
    }
    static void RGenerateFailureLeaf(CardModel __instance, ref Task<int> __result)
    {
        if (ReferenceEquals(__instance, RGenerateFailure))
            __result = Task.FromException<int>(new InvalidOperationException("controlled-native-generate-failure"));
    }
    static void RObserveLostStarEffect() => RLostStarEffects++;
    static bool ROwnerManualPile(CardModel card, ref Task __result)
    {
        card.RemoveFromCurrentPile();
        card.Owner.PlayerCombatState!.PlayPile.AddInternal(card, -1, true);
        __result = Task.CompletedTask;
        return false;
    }
    static void RInstallPowerExitChecks(Harmony harmony)
    {
        foreach (Type type in new[] { typeof(LostStarSongHbrgoSaki), typeof(ThirtyGPowerExitProbe), typeof(SingTogetherPower), typeof(PreparationPower), typeof(HighSongPower), typeof(LostStarSongSupportPower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            GSaveType(type);
        }
        RPowerExitHelper = Mod.GetType("HBRGOSakiMod.Core.Helpers.PowerCardPlayVisualsHelper");
        if (RPowerExitHelper != null && Environment.GetEnvironmentVariable("HBR_POWER_EXIT_HELPER_INIT") == "before-flight-patch")
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(RPowerExitHelper.TypeHandle);
        harmony.Patch(typeof(CardModel).GetMethod("PlayPowerCardFlyVfx", F)!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RPowerFlightLeaf), F)!) { priority = Priority.First + 500 });
        if (RPowerExitHelper != null)
        {
            // Run this before patching any method on the helper itself: Harmony
            // must not incidentally choose its static-initialization timing.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(RPowerExitHelper.TypeHandle);
            RCheckHelperGuards();
            harmony.Patch(RPowerExitHelper.GetMethod("GetUnclaimedTableCard", F)!,
                prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RUnclaimedTableLeaf), F)!));
        }
        harmony.Patch(typeof(CardModel).GetMethod("GeneratePlayCount", F)!,
            postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RGenerateFailureLeaf), F)!) { priority = Priority.First });
        harmony.Patch(typeof(LostStarSongHbrgoSaki).GetMethod("OnPlay", F)!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RObserveLostStarEffect), F)!));
        harmony.Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ROwnerManualPile), F)!) { priority = Priority.First + 500 });
    }
    static void RCheckHelperGuards()
    {
        var lookup = PatchProcessor.GetOriginalInstructions(RPowerExitHelper!.GetMethod("GetUnclaimedTableCard", F)!);
        var calls = lookup.Select(instruction => instruction.operand).OfType<MethodBase>().ToArray();
        void HasCall(string declaring, string name)
        {
            Check(calls.Any(method => method.DeclaringType?.Name == declaring && method.Name == name), "power exit lookup IL retains " + declaring + "." + name);
            RPowerExitGuardAudit.Add(declaring + "." + name);
        }
        HasCall(nameof(CardModel), "get_Type"); HasCall("Creature", "get_IsAlive");
        HasCall("CombatManager", "get_IsOverOrEnding"); HasCall(nameof(NCard), "FindOnTable");
        HasCall("NCardFlyPowerVfx", "get_CardNode");
        int claimIndex = lookup.FindIndex(instruction => instruction.operand is MethodBase method && method.DeclaringType?.Name == "NCardFlyPowerVfx" && method.Name == "get_CardNode");
        Check(lookup.Skip(claimIndex + 1).Take(3).Any(instruction => instruction.opcode == OpCodes.Bne_Un || instruction.opcode == OpCodes.Bne_Un_S) || calls.Any(method => method.DeclaringType == typeof(object) && method.Name == "ReferenceEquals"),
            "power exit lookup IL compares the flight's exact CardNode reference");
        RPowerExitGuardAudit.Add("exact CardNode reference comparison");
        Check(lookup.Any(instruction => instruction.opcode == OpCodes.Isinst && instruction.operand is Type type && type.Name == "NCardFlyPowerVfx"), "power exit lookup IL only treats native power flights as claims");
        var usability = PatchProcessor.GetOriginalInstructions(RPowerExitHelper.GetMethod("IsUsable", F)!).Select(instruction => instruction.operand).OfType<MethodBase>().ToArray();
        foreach (string method in new[] { "IsInstanceValid", "IsInsideTree", "IsQueuedForDeletion" })
        {
            Check(usability.Any(call => call.Name == method), "power exit usability IL retains " + method);
            RPowerExitGuardAudit.Add(method);
        }
    }
    static async Task RPowerReset()
    {
        RToken = null; RExternalRepeats = 0; RFlights.Clear(); RTable.Clear(); RClaimed.Clear();
        RGenerateFailure = null; RFlightFailure = false; RLostStarEffects = ThirtyGPowerExitProbe.Effects = 0;
        GReset(2); await GPrepare();
    }
    static LostStarSongHbrgoSaki RLostStar(bool inHand = true, bool openingUpgrade = false)
    {
        var physical = GOrigin<LostStarSongHbrgoSaki>();
        RAttachLostStarCapability(physical);
        if (openingUpgrade)
        {
            Check((bool)Native(physical, "PrepareUpgradeEffect", 0)!, "Lost Star prepares its actual opening autoplay upgrade");
            physical.UpgradeInternal(); physical.FinalizeUpgradeInternal();
        }
        var card = (LostStarSongHbrgoSaki)GMirror(physical, inHand);
        RAttachLostStarCapability(card);
        Call("ThirtyGProposalState", "EnsureIdentities", Player);
        return card;
    }
    static void RAttachLostStarCapability(CardModel card)
    {
        // The shared host does not run content-pack registration. Supply the
        // same 30A capability that production attaches to real Lost Star cards,
        // so native pile hooks can synchronize genuine collection claims.
        ModelCapabilitySet capabilities = card.Capabilities();
        var attached = (IList<IModelCapability>)typeof(ModelCapabilitySet).GetField("_capabilities", F)!.GetValue(capabilities)!;
        if (attached.OfType<ThirtyAKeywordCapability>().Any()) return;
        var capability = new ThirtyAKeywordCapability(); Set(capability, "<IsMutable>k__BackingField", true);
        capability.Attach(card, true); attached.Add(capability);
    }
    static void RMarkNullified(CardModel card)
    {
        string id = GId(card.DeckVersion ?? card);
        Check(!string.IsNullOrEmpty(id), "power exit target has a real physical identity");
        GUpdateData(data => data["Cards"]![id] = new System.Text.Json.Nodes.JsonObject { ["GraduatedRemaining"] = 1 });
        Check(GGraduated(card), "power exit target is actually nullified before the native wrapper");
    }
    static NCard RShow(CardModel card)
    {
        NCard token = Blank<NCard>(); RTable.Add(card, token); return token;
    }
    static async Task RPlayPower(CardModel card, bool automatic)
    {
        if (automatic) await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null);
        else
        {
            var (energy, stars) = await card.SpendResources();
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, false,
                new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars });
        }
    }
    static async Task RNullifiedPowerCase(bool automatic)
    {
        await RPowerReset(); var card = RLostStar(); RMarkNullified(card); RShow(card);
        var sameOwnerOther = RLostStar(); NCard sameOwnerToken = RShow(sameOwnerOther);
        Player peer = SupportTeammate(); peer.Creature.CombatState = Combat;
        var otherOwner = (LostStarSongHbrgoSaki)ModelDb.Card<LostStarSongHbrgoSaki>().ToMutable(); Set(otherOwner, "_owner", peer);
        RAttachLostStarCapability(otherOwner);
        peer.PlayerCombatState!.Hand.AddInternal(otherOwner); NCard peerToken = RShow(otherOwner);
        Check(card.Id == sameOwnerOther.Id && card.Id == otherOwner.Id && !ReferenceEquals(card, sameOwnerOther), "power exit isolation uses the same real model ID on distinct cards and owners");
        int energy = card.Owner.PlayerCombatState!.Energy; RExternalRepeats = 3;
        await RPlayPower(card, automatic);
        Check(RLostStarEffects == 0 && Hero.GetPower<SingTogetherPower>() == null && Hero.GetPower<PreparationPower>() == null, "nullified Lost Star suppresses its actual OnPlay and power effects, including external replays: " + automatic);
        Check(card.HasBeenRemovedFromState && card.Pile == null && !Player.PlayerCombatState!.PlayPile.Cards.Contains(card), "nullified Lost Star completes native Power removal from combat and the play pile: " + automatic);
        Check(!GGraduated(card) && GHeart(card) == 1, "nullified Lost Star advances to exactly one heartfelt play after native cleanup: " + automatic);
        Equal(card.Owner.PlayerCombatState.Energy, automatic ? energy : energy, "zero-cost nullified power preserves native payment semantics: " + automatic);
        Check(RTable.TryGetValue(sameOwnerOther, out NCard? otherToken) && ReferenceEquals(otherToken, sameOwnerToken) && sameOwnerOther.Pile?.Type == PileType.Hand && GHeart(sameOwnerOther) == 0,
            "completing a nullified power leaves the same owner's same-ID other model and visual untouched: " + automatic);
        Check(RTable.TryGetValue(otherOwner, out NCard? remoteToken) && ReferenceEquals(remoteToken, peerToken) && otherOwner.Pile?.Type == PileType.Hand,
            "completing a nullified power leaves another owner's same-ID model and visual untouched: " + automatic);
        if (RExpectOldPowerExit)
            Check(RPowerExitHelper == null && RFlights.Count == 0 && RTable.ContainsKey(card), "negative control observes the exact old zero-play/no-flight/remaining-table-node defect: " + automatic);
        else
            Check(RFlights.Count == 1 && ReferenceEquals(RFlights[0], card) && !RTable.ContainsKey(card), "nullified Lost Star starts exactly one exit for its existing unclaimed visual: " + automatic);
        RPowerExitResults.Add(new { scenario = automatic ? "nullified-external-autoplay" : "nullified-manual", effects = RLostStarEffects, flights = RFlights.Count, remainingTargetVisual = RTable.ContainsKey(card), remainingOtherVisuals = RTable.Count - (RTable.ContainsKey(card) ? 1 : 0), heart = GHeart(card), removed = card.HasBeenRemovedFromState, expectedOldDefect = RExpectOldPowerExit });
    }
    static async Task ROpeningPowerCase()
    {
        await RPowerReset(); var card = RLostStar(openingUpgrade: true); RShow(card);
        Check(!GGraduated(card), "ordinary opening Lost Star has no nullification state");
        Check(!(bool)Call("HighHitDamageVisualHelper", "ShouldSkipPresentationWait")!, "ordinary first-turn Lost Star has no live fast presentation batch");
        await card.AfterAutoPrePlayPhaseEnteredEarly(new ThrowingPlayerChoiceContext(), Player);
        Check(card.HasAutoPlayedThisCombat && RLostStarEffects == 1 && RFlights.Count == 1 && !RTable.ContainsKey(card), "ordinary first-turn Lost Star follows its native autoplay/flight exactly once");
        Check(Hero.GetPower<SingTogetherPower>()?.Amount == 1 && Hero.HasPower<PreparationPower>() && GHeart(card) == 0,
            "ordinary first-turn Lost Star retains its actual singing powers without consuming proposal state");
        await card.AfterAutoPrePlayPhaseEnteredEarly(new ThrowingPlayerChoiceContext(), Player);
        Equal(RFlights.Count, 1, "repeated first-turn callback does not replay the consumed opening Lost Star");
        RPowerExitResults.Add(new { scenario = "ordinary-first-turn-autoplay", effects = RLostStarEffects, flights = RFlights.Count, remainingVisual = RTable.ContainsKey(card) });
    }
    static bool RInvokeFastFlight(CardModel card, out Task? task)
    {
        MethodInfo prefix = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighHitDamagePowerFlightPatch", true)!.GetMethod("Prefix", F)!;
        object?[] args = prefix.GetParameters().Length == 1 ? [null] : [card, null];
        bool nativeRuns = (bool)Invoke(prefix, null, args)!; task = args[^1] as Task; return nativeRuns;
    }
    static async Task RFastPowerCases()
    {
        await RPowerReset(); var card = RLostStar();
        object scope = Invoke(Helper("HighHitDamageVisualHelper").GetMethod("BeginCardBatch", F, [typeof(CardModel), typeof(long)])!, null, card, 11L)!;
        try
        {
            Check((bool)Call("HighHitDamageVisualHelper", "ShouldSkipPresentationWait")!, "fast power tests use a real enabled live presentation batch");
            for (int index = 0; index < 3; index++)
                Check(!RInvokeFastFlight(card, out Task? skipped) && skipped?.IsCompletedSuccessfully == true, "fast power without a table node does not manufacture a flight: " + index);
            Equal(RFlights.Count, 0, "repeated no-node fast presentation creates no flight owners");
            NCard token = RShow(card);
            bool nativeRuns = RInvokeFastFlight(card, out _);
            if (RExpectOldPowerExit)
                Check(!nativeRuns && RTable.ContainsKey(card), "negative control observes old fast suppression of an existing power visual");
            else
            {
                Check(nativeRuns, "fast presentation preserves native flight for an existing unclaimed power node");
                await (Task)Invoke(typeof(CardModel).GetMethod("PlayPowerCardFlyVfx", F)!, card)!;
                Check(RFlights.Count == 1 && !RTable.ContainsKey(card), "allowed fast native flight owns the exact existing power node");
            }
            RTable[card] = token; RClaimed.Add(token);
            Check(!RInvokeFastFlight(card, out Task? alreadyFlying) && alreadyFlying?.IsCompletedSuccessfully == true, "fast presentation does not create another card or flight for an already claimed node");
            if (!RExpectOldPowerExit)
            {
                int before = RFlights.Count;
                await (Task)Invoke(RPowerExitHelper!.GetMethod("FinishNullifiedPowerPlay", F)!, null, card)!;
                Equal(RFlights.Count, before, "nullified cleanup leaves an existing flight owner's node alone");
                RClaimed.Clear(); RTable.Clear();
                await (Task)Invoke(RPowerExitHelper.GetMethod("FinishNullifiedPowerPlay", F)!, null, card)!;
                await (Task)Invoke(RPowerExitHelper.GetMethod("FinishNullifiedPowerPlay", F)!, null, card)!;
                Equal(RFlights.Count, before, "nullified cleanup without a visible table card never creates replacement cards");
            }
            RPowerExitResults.Add(new { scenario = "live-fast-batch", flights = RFlights.Count, existingNodeAllowed = nativeRuns, expectedOldDefect = RExpectOldPowerExit });
        }
        finally { Call("HighHitDamageVisualHelper", "CloseCard", scope); }
        Check(!(bool)Call("HighHitDamageVisualHelper", "ShouldSkipPresentationWait")!, "fast power test restores its live batch to avoid cross-case contamination");
    }
    static async Task RPowerFailureAndTypeCases()
    {
        await RPowerReset(); var card = RLostStar(); RMarkNullified(card); RShow(card); RGenerateFailure = card;
        bool failed = false;
        try { await RPlayPower(card, false); }
        catch (InvalidOperationException error) when (error.Message == "controlled-native-generate-failure") { failed = true; }
        finally { RGenerateFailure = null; }
        Check(failed && RFlights.Count == 0 && GGraduated(card) && GHeart(card) == 0,
            "failed native GeneratePlayCount propagates unchanged and cannot start flight or advance proposal state");
        RPowerExitResults.Add(new { scenario = "native-generate-failure", propagated = failed, flights = RFlights.Count, heart = GHeart(card) });
        if (!RExpectOldPowerExit)
        {
            await RPowerReset(); card = RLostStar(); RMarkNullified(card); RShow(card); RFlightFailure = true;
            await RPlayPower(card, false); RFlightFailure = false;
            Check(RFlights.Count == 1 && RLostStarEffects == 0 && card.HasBeenRemovedFromState && card.Pile == null && GHeart(card) == 1,
                "a failed UI flight does not block synchronized zero-effect model cleanup and heart transition");
            await RPowerReset(); CardModel skill = GMirror(GOrigin<ThirtyGRevisionProbe>(), true); Call("ThirtyGProposalState", "EnsureIdentities", Player); RMarkNullified(skill); RShow(skill);
            await RPlayPower(skill, false);
            Check(RFlights.Count == 0 && skill.Pile?.Type == PileType.Discard && GHeart(skill) == 1,
                "nullified ordinary non-Power card keeps native discard behavior and starts no power flight");
            await RPowerReset(); CardModel power = GMirror(GOrigin<ThirtyGPowerExitProbe>(), true); Call("ThirtyGProposalState", "EnsureIdentities", Player); RMarkNullified(power); RShow(power);
            await RPlayPower(power, false);
            Check(ThirtyGPowerExitProbe.Effects == 0 && power.HasBeenRemovedFromState && power.Pile == null && GHeart(power) == 1 && RFlights.Count == 1 && !RTable.ContainsKey(power),
                "the same zero-effect/exit/model-removal/heart rule covers another actual Power model");
        }
    }
    static async Task RPowerExitCases()
    {
        Check(RExpectOldPowerExit ? RPowerExitHelper == null : RPowerExitHelper != null,
            RExpectOldPowerExit ? "negative-control candidate has no new power-exit helper" : "candidate contains the production power-exit helper");
        await RNullifiedPowerCase(false); await RNullifiedPowerCase(true);
        await ROpeningPowerCase(); await RFastPowerCases(); await RPowerFailureAndTypeCases();
    }
}
