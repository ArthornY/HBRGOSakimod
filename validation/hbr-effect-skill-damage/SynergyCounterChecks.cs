using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

// Real Synergy AfterCardPlayed and ModeTransitionHelper select the draw. Only
// native pile relocation/draw leaves are replaced with equivalent in-memory
// pile moves, without a Godot hand or card animation lifecycle.
static partial class EffectChecks
{
    static readonly List<CardModel> SynergyDrawn = new();
    static CardModel? SynergyMovedToTop;

    static void SynergyReset()
    {
        Reset(); As31O(true); CombatInProgress(false);
        Set(Player.PlayerCombatState, "<Hand>k__BackingField", new CardPile(PileType.Hand));
        Set(Player.PlayerCombatState, "<DrawPile>k__BackingField", new CardPile(PileType.Draw));
        Set(Player.PlayerCombatState, "_piles", null);
        SynergyDrawn.Clear(); SynergyMovedToTop = null;
    }

    static void SynergyCount(SynergyPower power, int dual, int bow, string message)
    {
        Check(power.DualSwordsCount == dual && power.BowCount == bow,
            $"{message}: dual {power.DualSwordsCount}/{dual}, bow {power.BowCount}/{bow}");
        Equal(power.DisplayAmount, dual, "Synergy's normal lower-right amount is its dual-swords count");
    }

    static async Task SynergyPlay(SynergyPower power, CardModel card)
    {
        await power.AfterCardPlayed(new ThrowingPlayerChoiceContext(), Play(card));
        Check(Hero.GetPower<DualSwordsCounterPower>() == null && Hero.GetPower<BowCounterPower>() == null,
            "current Synergy does not create separate dual-swords or bow counter powers");
    }

    static T SynergyDeckCard<T>() where T : CardModel, new()
    {
        T card = ModelDb.Contains(typeof(T)) ? (T)ModelDb.Card<T>().ToMutable() : Owned<T>();
        Set(card, "_owner", Player);
        Player.PlayerCombatState.DrawPile.AddInternal(card, -1, true);
        return card;
    }

    static bool SynergyMoveLeaf(object[] __args, ref Task<CardPileAddResult> __result)
    {
        var card = (CardModel)__args[0]; var newPile = (CardPile)__args[1];
        var position = (CardPilePosition)__args[2]; var clonedBy = __args[3] as AbstractModel;
        bool skipVisuals = (bool)__args[4];
        Check(ReferenceEquals(newPile, Player.PlayerCombatState.DrawPile) && position == CardPilePosition.Top &&
            clonedBy is SynergyPower && skipVisuals,
            "real mode draw selects a card and places it on top through the expected native pile command");
        card.Pile?.RemoveInternal(card, true);
        newPile.AddInternal(card, 0, true);
        SynergyMovedToTop = card;
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card });
        return false;
    }

    static bool SynergyDrawLeaf(object[] __args, ref Task<CardModel?> __result)
    {
        var player = (Player)__args[1];
        CardModel card = player.PlayerCombatState.DrawPile.Cards.FirstOrDefault() ??
            throw new Exception("The mode-draw command reached an empty fixture draw pile");
        Check(ReferenceEquals(card, SynergyMovedToTop),
            "mode draw consumes the exact matching card previously selected by production logic");
        card.Pile!.RemoveInternal(card, true);
        player.PlayerCombatState.Hand.AddInternal(card, -1, true);
        SynergyDrawn.Add(card); SynergyMovedToTop = null;
        __result = Task.FromResult<CardModel?>(card);
        return false;
    }

    static async Task SynergyCounterCases()
    {
        Localization();
        CardPile oldHand = Player.PlayerCombatState.Hand, oldDraw = Player.PlayerCombatState.DrawPile;
        var harmony = new Harmony(ProductionId + ".synergy-pile-leaves");
        MethodInfo move = typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Add" &&
            method.GetParameters() is var parameters && parameters.Length == 5 &&
            parameters[0].ParameterType == typeof(CardModel) && parameters[1].ParameterType == typeof(CardPile));
        MethodInfo draw = typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" &&
            method.GetParameters() is var parameters && parameters.Length == 2 && parameters[1].ParameterType == typeof(Player));
        MethodInfo movePrefix = typeof(EffectChecks).GetMethod(nameof(SynergyMoveLeaf), F)!;
        MethodInfo drawPrefix = typeof(EffectChecks).GetMethod(nameof(SynergyDrawLeaf), F)!;
        MethodInfo waitPrefix = typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!;
        MethodInfo[] waits = typeof(Cmd).GetMethods(F).Where(method =>
            (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task)).ToArray();
        try
        {
            harmony.Patch(move, prefix: new HarmonyMethod(movePrefix));
            harmony.Patch(draw, prefix: new HarmonyMethod(drawPrefix));
            foreach (MethodInfo wait in waits) harmony.Patch(wait, prefix: new HarmonyMethod(waitPrefix));
            foreach (Type type in new[] { typeof(SynergyPower), typeof(DualSwordsCounterPower), typeof(BowCounterPower) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            await SynergyIndependentCases();
            await SynergyCopyCases();
            await SynergyDoubleCases();
            await SynergyLegacyCases();
            SynergySaveAndDisplayCases();
        }
        finally
        {
            harmony.Unpatch(move, movePrefix); harmony.Unpatch(draw, drawPrefix);
            foreach (MethodInfo wait in waits) harmony.Unpatch(wait, waitPrefix);
            Set(Player.PlayerCombatState, "<Hand>k__BackingField", oldHand);
            Set(Player.PlayerCombatState, "<DrawPile>k__BackingField", oldDraw);
            Set(Player.PlayerCombatState, "_piles", null);
            SynergyDrawn.Clear(); SynergyMovedToTop = null; As31O(false); Reset();
        }
    }

    static async Task SynergyIndependentCases()
    {
        SynergyReset(); var power = Power<SynergyPower>(Hero, 1);
        SynergyCount(power, 0, 0, "one Synergy power begins with both counters at zero");
        var dualToDraw = SynergyDeckCard<SwallowReversalHbrgoSaki>();
        var bowToDraw = SynergyDeckCard<StrongShotHbrgoSaki>();
        var bow = Owned<StrongShotHbrgoSaki>(); var dual = Owned<ConsecutiveSlashHbrgoSaki>();
        await SynergyPlay(power, bow); await SynergyPlay(power, bow);
        SynergyCount(power, 0, 2, "two bow cards only advance the bow counter");
        await SynergyPlay(power, dual);
        SynergyCount(power, 1, 2, "a dual card advances its independent counter");
        await UnifiedPlayCard(Owned<SynergyHbrgoSaki>(), false);
        Check(ReferenceEquals(Hero.GetPower<SynergyPower>(), power), "reapplying the single Synergy buff retains the same stateful model");
        SynergyCount(power, 1, 2, "reapplying Synergy preserves both accumulated counters");
        await SynergyPlay(power, bow);
        SynergyCount(power, 1, 0, "the third bow card resets only its own counter");
        Check(SynergyDrawn.SequenceEqual(new[] { dualToDraw }), "three bow cards draw the first dual-swords card");
        await SynergyPlay(power, dual); await SynergyPlay(power, dual);
        SynergyCount(power, 0, 0, "the third dual card resets only its own counter");
        Check(SynergyDrawn.SequenceEqual(new CardModel[] { dualToDraw, bowToDraw }),
            "three dual-swords cards draw the first bow card");
        await SynergyPlay(power, Owned<StrikeIronclad>());
        SynergyCount(power, 0, 0, "a card without either weapon form advances neither counter");

        SynergyReset(); power = Power<SynergyPower>(Hero, 1);
        bow = Owned<StrongShotHbrgoSaki>();
        await SynergyPlay(power, bow); await SynergyPlay(power, bow); await SynergyPlay(power, bow);
        SynergyCount(power, 0, 0, "threshold still resets when the draw pile has no opposite-form card");
        Check(SynergyDrawn.Count == 0, "missing opposite-form cards do not become an unrelated normal draw");
    }

    static async Task SynergyCopyCases()
    {
        SynergyReset(); var power = Power<SynergyPower>(Hero, 1);
        var duplicate = Owned<ConsecutiveSlashHbrgoSaki>(); Set(duplicate, "_isDupe", true);
        await SynergyPlay(power, duplicate);
        SynergyCount(power, 0, 0, "a native duplicate dual card does not advance the dual counter");
        var marked = Owned<ConsecutiveSlashHbrgoSaki>();
        Call("CardCopyContextHelper", "RecordCopy", marked);
        try { await SynergyPlay(power, marked); }
        finally { Call("CardCopyContextHelper", "Clear", marked); }
        SynergyCount(power, 0, 0, "a marked copied dual card does not advance the dual counter");
        var bowCopy = Owned<StrongShotHbrgoSaki>(); Set(bowCopy, "_isDupe", true);
        await SynergyPlay(power, bowCopy);
        SynergyCount(power, 0, 1, "bow copies retain the original bow-counting rule");
        var switchingCopy = Owned<SwitchingSlashHbrgoSaki>();
        Call("CardCopyContextHelper", "RecordCopy", switchingCopy);
        try { await SynergyPlay(power, switchingCopy); }
        finally { Call("CardCopyContextHelper", "Clear", switchingCopy); }
        SynergyCount(power, 0, 1, "a copied Switching Slash retains dual classification and does not advance its counter");
        var otherPlayer = Blank<Player>(); Set(otherPlayer, "<Creature>k__BackingField", Enemy);
        var foreign = Owned<StrongShotHbrgoSaki>(); Set(foreign, "_owner", otherPlayer);
        await SynergyPlay(power, foreign);
        SynergyCount(power, 0, 1, "another player's bow card does not advance this Synergy");
    }

    static async Task SynergyDoubleCases()
    {
        SynergyReset(); var power = Power<SynergyPower>(Hero, 1);
        var dual = Owned<ConsecutiveSlashHbrgoSaki>(); var bow = Owned<StrongShotHbrgoSaki>();
        await SynergyPlay(power, dual); await SynergyPlay(power, dual); await SynergyPlay(power, bow);
        var bowToDraw = SynergyDeckCard<StrongShotHbrgoSaki>();
        var dualToDraw = SynergyDeckCard<SwallowReversalHbrgoSaki>();
        await UnifiedPlayCard(Owned<DoublePowersHbrgoSaki>(), false);
        SynergyCount(power, 4, 2, "the real Double Powers effect doubles both internal counters");
        Check(SynergyDrawn.Count == 0, "doubling beyond the threshold does not immediately draw");
        await SynergyPlay(power, dual);
        SynergyCount(power, 0, 2, "the next qualifying dual card triggers once and resets its doubled counter");
        Check(SynergyDrawn.SequenceEqual(new[] { bowToDraw }), "doubled dual count draws one bow card only");
        await SynergyPlay(power, bow);
        SynergyCount(power, 0, 0, "the next qualifying bow card separately reaches its threshold");
        Check(SynergyDrawn.SequenceEqual(new CardModel[] { bowToDraw, dualToDraw }), "both independent doubled counters draw the correct other forms");
    }

    static async Task SynergyLegacyCases()
    {
        SynergyReset(); var power = Power<SynergyPower>(Hero, 1);
        Power<DualSwordsCounterPower>(Hero, 4); Power<BowCounterPower>(Hero, 2);
        await power.AfterApplied(Hero, null!);
        SynergyCount(power, 4, 2, "legacy counters move into the single Synergy state without losing values");
        Check(Hero.GetPower<DualSwordsCounterPower>() == null && Hero.GetPower<BowCounterPower>() == null && SynergyDrawn.Count == 0,
            "legacy counter models disappear without drawing merely because a loaded counter exceeds three");
        await (Task)Invoke(typeof(SynergyPower).GetMethod("AbsorbLegacyCounters", F)!, power)!;
        SynergyCount(power, 4, 2, "legacy absorption is idempotent");
        var bowToDraw = SynergyDeckCard<StrongShotHbrgoSaki>();
        await SynergyPlay(power, Owned<ConsecutiveSlashHbrgoSaki>());
        Check(SynergyDrawn.SequenceEqual(new[] { bowToDraw }), "a loaded high dual count waits for the next dual card before drawing");
        var late = Power<BowCounterPower>(Hero, 1);
        await late.AfterApplied(Hero, null!);
        SynergyCount(power, 0, 3, "a legacy counter applied later joins the unified saved state");
        Check(!Hero.Powers.Contains(late) && SynergyDrawn.Count == 1, "late migration removes its legacy icon without triggering a draw");
    }

    static void SynergySaveAndDisplayCases()
    {
        SynergyReset(); var power = Power<SynergyPower>(Hero, 1);
        typeof(SynergyPower).GetProperty("DualSwordsCount", F)!.SetValue(power, 4);
        typeof(SynergyPower).GetProperty("BowCount", F)!.SetValue(power, 2);
        PropertyInfo[] saved = typeof(SynergyPower).GetProperties(F).Where(property =>
            property.DeclaringType == typeof(SynergyPower) && property.GetCustomAttributes()
                .Any(attribute => attribute.GetType().Name == "SavedPropertyAttribute")).ToArray();
        Check(saved.Select(property => property.Name).Order().SequenceEqual(new[] { "BowCount", "DualSwordsCount" }),
            "both internal counters have independent native saved-property metadata");
        var clone = (SynergyPower)power.ClonePreservingMutability();
        Check(clone.DualSwordsCount == 4 && clone.BowCount == 2, "native model cloning preserves both internal counters");
        string description = power.Description.GetFormattedText();
        Check(description.Contains('4') && description.Contains('2') && !description.Contains("{DualSwordsCount}") && !description.Contains("{BowCount}"),
            "the owned Synergy hover description resolves both saved counter values");
        string source = Source("Core/Models/Powers/SynergyPower.cs"), display = Source("Core/Patches/CountedRollPowerDisplayPatch.cs");
        Check(source.Contains("F8D36A") && display.Contains("synergy.DualSwordsCount") &&
            display.Contains("upperAmount = synergy.BowCount") && display.Contains("6ED7FF"),
            "Synergy displays dual count in native lower-right gold and bow count in upper-right cyan");
        Check(display.Contains("power.AvailableUses") && display.Contains("E8F5FF"),
            "generalizing the upper counter preserves the counted-roll uses and original text color");
    }
}
