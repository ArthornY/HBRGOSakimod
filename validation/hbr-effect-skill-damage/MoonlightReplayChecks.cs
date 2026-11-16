using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

// Real native GeneratePlayCount calls the production modifiers and then only
// the models which changed the count. We never simulate that by calling every
// AfterModifying hook, which would incorrectly spend the legacy fallback too.
static partial class EffectChecks
{
    static readonly AsyncLocal<int> MoonlightNativeCountDepth = new();

    static bool MoonlightLiveHookGate(ref bool __result)
    {
        if (MoonlightNativeCountDepth.Value <= 0) return true;
        __result = false;
        return false;
    }

    static void MoonlightReset()
    {
        Reset(); As31O(true); CombatInProgress(false);
        Set(Player.PlayerCombatState, "<Hand>k__BackingField", new CardPile(PileType.Hand));
        Set(Player.PlayerCombatState, "<PlayPile>k__BackingField", new CardPile(PileType.Play));
        // AllPiles caches the five pile objects. Replacing a fixture pile must
        // invalidate that cache, otherwise CardModel.Pile still sees the old hand.
        Set(Player.PlayerCombatState, "_piles", null);
        Set(Player.PlayerCombatState, "_pets", new List<Creature>());
        Set(Player.PlayerCombatState, "_energy", 10);
    }

    static T MoonlightHand<T>() where T : CardModel, new()
    {
        T card = Owned<T>();
        Player.PlayerCombatState.Hand.AddInternal(card, -1, true);
        return card;
    }

    static int MoonlightDisplayed(CardModel card) =>
        (int)Call("MoonlightReplayDisplayHelper", "GetDisplayedReplayCount", card)!;
    static int MoonlightBonus(CardModel card) =>
        (int)Invoke(typeof(MoonlightReplayPower).GetMethod("GetReplayCountBonus", F)!, null, card)!;

    static async Task<int> MoonlightGenerate(CardModel card, bool automatic)
    {
        object scope = Call("CardPlayContextHelper", "Begin", card, automatic)!;
        try
        {
            // Native IterateCombatHookListeners suppresses all hooks while the
            // headless manager is idle. Open only this lifecycle gate for the
            // real count-generation call; listener enumeration is unchanged.
            MoonlightNativeCountDepth.Value++;
            await Task.Yield();
            int bonus = MoonlightBonus(card);
            var listeners = (IEnumerable<AbstractModel>)Invoke(typeof(Hook).GetMethod("IterateCombatHookListeners", F)!, null, Combat)!;
            Check(bonus == 0 || listeners.Any(model => model is MoonlightReplayPower or CrushYouLikeABugReplayPower),
                "the shared replay predicate and native active-combat listener enumeration see the same eligible source");
            return await (Task<int>)Invoke(typeof(CardModel).GetMethod("GeneratePlayCount", F)!, card, Combat, Enemy)!;
        }
        finally
        {
            MoonlightNativeCountDepth.Value--;
            Call("CardPlayContextHelper", "Close", scope);
        }
    }

    static async Task MoonlightReplayCases()
    {
        Localization();
        CardPile oldHand = Player.PlayerCombatState.Hand, oldPlay = Player.PlayerCombatState.PlayPile;
        object? oldPets = typeof(PlayerCombatState).GetField("_pets", F)!.GetValue(Player.PlayerCombatState);
        int oldEnergy = Player.PlayerCombatState.Energy;
        var harmony = new Harmony(ProductionId + ".moonlight-native-display");
        Type displayPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.MoonlightReplayDisplayPatch", true)!;
        MethodBase[] targets = ((IEnumerable<MethodBase>)Invoke(displayPatch.GetMethod("TargetMethods", F)!, null)!).ToArray();
        MethodInfo[] waits = typeof(Cmd).GetMethods(F).Where(method =>
            (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task)).ToArray();
        MethodInfo waitPrefix = typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!;
        MethodInfo ending = AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding");
        MethodInfo gatePrefix = typeof(EffectChecks).GetMethod(nameof(MoonlightLiveHookGate), F)!;
        try
        {
            harmony.Patch(ending, prefix: new HarmonyMethod(gatePrefix));
            foreach (MethodInfo wait in waits) harmony.Patch(wait, prefix: new HarmonyMethod(waitPrefix));
            foreach (Type type in new[] { typeof(MoonlightReplayPower), typeof(CrushYouLikeABugReplayPower) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            MoonlightDisplayStructureCases(displayPatch, targets);
            Check(harmony.CreateClassProcessor(displayPatch).Patch().Count == 2,
                "installs the Moonlight replay display transpiler on both native display leaves");
            await MoonlightNativeCountCases();
            await MoonlightAutomaticCases();
            await MoonlightLegacyCases();
            await MoonlightPersistenceAndGrantCases();
            await MoonlightDisplayCases();
        }
        finally
        {
            foreach (MethodBase target in targets) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
            foreach (MethodInfo wait in waits) harmony.Unpatch(wait, waitPrefix);
            harmony.Unpatch(ending, gatePrefix);
            MoonlightNativeCountDepth.Value = 0;
            Set(Player.PlayerCombatState, "<Hand>k__BackingField", oldHand);
            Set(Player.PlayerCombatState, "<PlayPile>k__BackingField", oldPlay);
            Set(Player.PlayerCombatState, "_piles", null);
            Set(Player.PlayerCombatState, "_pets", oldPets);
            Set(Player.PlayerCombatState, "_energy", oldEnergy);
            As31O(false); Reset();
        }
    }

    static void MoonlightDisplayStructureCases(Type patch, MethodBase[] targets)
    {
        MethodInfo original = typeof(CardModel).GetMethod(nameof(CardModel.GetEnchantedReplayCount), F)!;
        MethodInfo displayed = Helper("MoonlightReplayDisplayHelper").GetMethod("GetDisplayedReplayCount", F)!;
        Check(targets.Length == 2 && targets.Any(method => method.Name == "get_HoverTips") &&
            targets.Any(method => method.Name == "GetDescriptionForPile"),
            "Moonlight display targets only the native hover and description leaves");
        foreach (MethodBase target in targets)
        {
            CodeInstruction[] before = PatchProcessor.GetOriginalInstructions(target).ToArray();
            var after = ((IEnumerable<CodeInstruction>)Invoke(patch.GetMethod("Transpiler", F)!, null, before, target)!).ToArray();
            Check(before.Count(instruction => instruction.Calls(original)) == 1 &&
                after.All(instruction => !instruction.Calls(original)) && after.Count(instruction => instruction.Calls(displayed)) == 1,
                "Moonlight replaces exactly one native displayed replay read in " + target.Name);
        }
        MethodInfo generate = typeof(CardModel).GetMethod("GeneratePlayCount", F)!;
        Type state = generate.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        CodeInstruction[] gameplay = PatchProcessor.GetOriginalInstructions(state.GetMethod("MoveNext", F)!).ToArray();
        Check(gameplay.Count(instruction => instruction.Calls(original)) == 1 &&
            gameplay.All(instruction => !instruction.Calls(displayed)),
            "native replay generation retains its real base getter and never reads the display-only bonus");
    }

    static async Task MoonlightNativeCountCases()
    {
        MoonlightReset(); var moonlight = Power<MoonlightReplayPower>(Hero, 3);
        var card = MoonlightHand<StrikeIronclad>();
        Check(ReferenceEquals(card.Pile, Player.PlayerCombatState.Hand),
            "the native pile getter resolves the replacement fixture hand after AllPiles cache invalidation");
        bool playable = card.CanPlay(out var reason, out var preventer);
        Check(playable, $"the native hand card is playable: reason {reason}, preventer {preventer?.GetType().Name ?? "none"}");
        for (int index = 0; index < 3; index++)
        {
            Equal(MoonlightDisplayed(card), 1m, "hand preview shows one pending replay without spending it");
            Equal(moonlight.Amount, 3m, "repeated Moonlight preview leaves all layers intact");
        }
        Equal(await MoonlightGenerate(card, false), 2m,
            "native GeneratePlayCount adds one Moonlight play to a manually played card");
        Equal(moonlight.Amount, 2m, "the native after-modifier callback spends one layer for the whole replay series");
        Equal(card.BaseReplayCount, 0m, "Moonlight never writes the native base replay field");
        card.BaseReplayCount = 1;
        Equal(MoonlightDisplayed(card), 2m, "a native one-replay card displays two replays with Moonlight");
        Equal(await MoonlightGenerate(card, false), 3m, "native one-replay card actually plays three times with Moonlight");
        Equal(moonlight.Amount, 1m, "a three-play series still spends only one Moonlight layer");
        Equal(card.BaseReplayCount, 1m, "the native one-replay field remains one after count generation");
        Equal(await MoonlightGenerate(card, false), 3m, "the final Moonlight layer supplies one more complete replay series");
        Check(Hero.GetPower<MoonlightReplayPower>() == null || moonlight.Amount == 0,
            "Moonlight disappears only when its remaining layers are spent");
        Equal(MoonlightDisplayed(card), 1m, "exhausted Moonlight restores the card's original one-replay display");
        Equal(await MoonlightGenerate(card, false), 2m, "exhausted Moonlight restores the native play count");

        MoonlightReset(); moonlight = Power<MoonlightReplayPower>(Hero, 2);
        card = MoonlightHand<StrikeIronclad>(); Set(card, "_isDupe", true);
        Check(card.IsDupe, "manual-copy fixture is an actual native duplicate card");
        Equal(await MoonlightGenerate(card, false), 2m, "a manually played duplicate is eligible for Moonlight");
        Equal(moonlight.Amount, 1m, "a manually played duplicate spends exactly one layer");
    }

    static async Task MoonlightAutomaticCases()
    {
        MoonlightReset(); var moonlight = Power<MoonlightReplayPower>(Hero, 4);
        CardModel[] cards = { MoonlightHand<StrikeIronclad>(), MoonlightHand<FragileHbrgoSaki>(),
            MoonlightHand<BeyondProtectionHbrgoSaki>(), MoonlightHand<MagicSwordLuckySevenOpeningHbrgoSaki>(),
            MoonlightHand<MagicSwordLuckySevenHbrgoSaki>() };
        foreach (CardModel card in cards)
        {
            int baseline = card.GetEnchantedReplayCount() + 1;
            Equal(await MoonlightGenerate(card, true), baseline,
                "automatic " + card.GetType().Name + " retains its native play count");
            Equal(moonlight.Amount, 4m, "automatic cards do not spend a Moonlight layer");
        }
        var copy = MoonlightHand<StrikeIronclad>(); Set(copy, "_isDupe", true);
        Call("CardCopyContextHelper", "RecordCopy", copy);
        try
        {
            Equal(await MoonlightGenerate(copy, true), 1m, "the automatic replay-created copy receives no Moonlight replay");
            Equal(moonlight.Amount, 4m, "the automatic replay-created copy spends no Moonlight layer");
        }
        finally { Call("CardCopyContextHelper", "Clear", copy); }
        var foreign = Owned<StrikeIronclad>();
        var other = Blank<Player>(); Set(other, "<Creature>k__BackingField", Enemy); Set(other, "_runState", RunState);
        Set(other, "_relics", new List<RelicModel>()); Set(foreign, "_owner", other);
        Equal(MoonlightBonus(foreign), 0m, "a different owner's card cannot use the hero's Moonlight");
    }

    static async Task MoonlightLegacyCases()
    {
        MoonlightReset(); var current = Power<MoonlightReplayPower>(Hero, 1);
        var old = Power<CrushYouLikeABugReplayPower>(Hero, 2);
        var card = MoonlightHand<StrikeIronclad>();
        Equal(await MoonlightGenerate(card, false), 2m,
            "new and legacy replay powers together add only one play through native hook enumeration");
        Equal(old.Amount, 2m,
            "native AfterModifying visits only the actual new modifier and does not spend the legacy fallback");
        Check(current.Amount == 0 || !Hero.Powers.Contains(current), "the new replay source's last layer was spent");
        Equal(await MoonlightGenerate(card, true), 1m, "legacy replay fallback also excludes automatic cards");
        Equal(old.Amount, 2m, "automatic cards do not consume legacy replay layers");
        Set(card, "_isDupe", true);
        Equal(await MoonlightGenerate(card, false), 2m, "legacy fallback permits a manually played duplicate");
        Equal(old.Amount, 1m, "legacy fallback spends one layer only after the new source is exhausted");
        Equal(await MoonlightGenerate(card, false), 2m, "the last legacy layer still adds only one play");
        Equal(await MoonlightGenerate(card, false), 1m, "both exhausted sources restore the original native play count");
    }

    static async Task MoonlightPersistenceAndGrantCases()
    {
        MoonlightReset(); var moonlight = Power<MoonlightReplayPower>(Hero, 3);
        var context = new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext();
        await moonlight.AfterSideTurnEnd(context, CombatSide.Player, new[] { Hero });
        await moonlight.AfterSideTurnEnd(context, CombatSide.Enemy, new[] { Enemy });
        Equal(moonlight.Amount, 3m, "Moonlight survives both player and enemy turn ends");
        Check(Hero.Powers.Contains(moonlight), "Moonlight remains attached across turns");
        await UnifiedPlayCard(Owned<DoublePowersHbrgoSaki>(), false);
        Equal(moonlight.Amount, 6m, "Double Powers doubles the unified replay counter normally");
        Equal(await MoonlightGenerate(MoonlightHand<StrikeIronclad>(), false), 2m,
            "six Moonlight layers mean six eligible cards, not six extra plays on one card");
        Equal(moonlight.Amount, 5m, "one card consumes one layer after doubling");

        MoonlightReset();
        await UnifiedPlayCard(Owned<MimicMoonlightHbrgoSaki>(), false);
        await UnifiedPlayCard(Owned<MimicMoonlightHbrgoSaki>(), true);
        Equal(Hero.GetPower<MoonlightReplayPower>()!.Amount, 4m,
            "both base and upgraded Mimic Moonlight grant two layers of the same unified power");
        var bug = Owned<CrushYouLikeABugHbrgoSaki>();
        Check((bool)Invoke(typeof(CrushYouLikeABugHbrgoSaki).GetMethod("PrepareUpgradeEffect", F)!, bug, 6)!,
            "the bug card can select its replacement Best Position upgrade explicitly");
        bug.UpgradeInternal(); bug.FinalizeUpgradeInternal();
        await (Task)Call("CrushYouLikeABugCombatHelper", "FinishUpgradeEffects", context, bug, Play(bug))!;
        Equal(Hero.GetPower<MoonlightReplayPower>()!.Amount, 4m,
            "the bug's replacement sixth upgrade no longer grants the deleted replay effect");
        Check(Hero.GetPower<CrushYouLikeABugReplayPower>() == null,
            "current card sources no longer generate the retired replay power");
    }

    static async Task MoonlightDisplayCases()
    {
        MoonlightReset(); var moonlight = Power<MoonlightReplayPower>(Hero, 2);
        var card = MoonlightHand<StrikeIronclad>();
        Check(card.CanPlay(), "Moonlight hand fixture satisfies the real native playability checks");
        Equal(MoonlightDisplayed(card), 1m, "the display helper reports one replay on a playable hand card");
        string face = card.GetDescriptionForPile(PileType.Hand);
        Check(face.Contains("[gold]重放[/gold]1"), "the real native hand description contains its replay-one row");
        Check(MoonlightReplayTip(card).Contains("[blue]1[/blue]"),
            "the real native ReplayDynamic hover tip says the card plays one extra time");
        card.BaseReplayCount = 1;
        face = card.GetDescriptionForPile(PileType.Hand);
        Check(face.Contains("[gold]重放[/gold]2") && MoonlightReplayTip(card).Contains("[blue]2[/blue]"),
            "native description and ReplayDynamic both display two when one base replay and Moonlight coexist");
        Equal(card.BaseReplayCount, 1m, "rendering both native leaves leaves BaseReplayCount untouched");
        Set(Player.PlayerCombatState, "_energy", 0);
        Check(!card.CanPlay(), "zero-energy card fixture is genuinely unplayable");
        Equal(MoonlightDisplayed(card), 1m, "an unplayable hand card displays only its authored replay count");
        Set(Player.PlayerCombatState, "_energy", 10);
        var unplayable = MoonlightHand<FragileHbrgoSaki>();
        Check(!unplayable.CanPlay(), "the auto-triggered Fragile card cannot be played manually");
        Equal(MoonlightDisplayed(unplayable), unplayable.GetEnchantedReplayCount(),
            "an automatic-only card receives no display-only Moonlight replay");
        object scope = Call("CardPlayContextHelper", "Begin", card, true)!;
        try { Equal(MoonlightDisplayed(card), 1m, "the display shares the actual automatic-play exclusion"); }
        finally { Call("CardPlayContextHelper", "Close", scope); }
        Player.PlayerCombatState.Hand.RemoveInternal(card, true);
        Player.PlayerCombatState.PlayPile.AddInternal(card, -1, true);
        Equal(MoonlightDisplayed(card), 1m, "a card outside the hand does not receive the display-only replay");
        Equal(moonlight.Amount, 2m, "description, hover, playability and pile previews consume no Moonlight layers");
    }

    static string MoonlightReplayTip(CardModel card)
    {
        foreach (IHoverTip tip in card.HoverTips)
        {
            string? title = tip.GetType().GetProperty("Title", F)?.GetValue(tip) as string;
            if (title == "重放") return tip.GetType().GetProperty("Description", F)?.GetValue(tip) as string ?? "";
        }
        return "";
    }
}
