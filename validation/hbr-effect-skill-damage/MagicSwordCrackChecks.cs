using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

// Real opening/removal/payment/card bodies and native AutoPlay/OnPlayWrapper.
// Only Godot presentation/time, the attack/block terminal, and the eventual
// turn-end request are substituted. Managed pile leaves preserve membership.
static partial class EffectChecks
{
    static readonly List<string> CrackEvents = [];
    static decimal CrackBlock;
    static int CrackEndRequests, CrackAttacks;
    static bool CrackEnding, CrackRejectFinisher;
    static int CrackCount => FateValue<int>("Cracks");
    static bool CrackLive(ref bool __result) { __result = CrackEnding; return false; }
    static bool CrackClock(ref ulong __result) { __result = 1000; return false; }
    static bool CrackShouldPlay(CardModel card, out AbstractModel? preventer, ref bool __result)
    {
        preventer = null;
        if (!CrackRejectFinisher || card is not (MagicSwordLuckySevenHbrgoSaki or MagicSwordLuckySevenOpeningHbrgoSaki)) return true;
        __result = false; return false;
    }
    static bool CrackShow(ref object __result)
    {
        __result = RuntimeHelpers.GetUninitializedObject(Helper("HbrLuckySevenFinisher"));
        return false;
    }
    static bool CrackEnd(Creature owner)
    {
        Check(ReferenceEquals(owner, Hero), "magic-sword exit requests only its holder's turn end");
        CrackEndRequests++; CrackEvents.Add("end:" + CrackCount); return false;
    }
    static bool CrackBlockLeaf(object[] __args, ref Task<decimal> __result)
    {
        decimal amount = Convert.ToDecimal(__args.First(arg => arg is decimal));
        CrackBlock += amount;
        __result = Task.FromResult(amount); return false;
    }
    static bool CrackAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        CrackAttacks++; __result = Task.FromResult(__instance); return false;
    }
    static void CrackAutoPrefix(object[] __args)
    {
        // The last optional flag in both profiles is skipCardPileVisuals.
        __args[^1] = true;
        CrackEvents.Add("start:" + ((CardModel)__args[1]).GetType().Name + ":" + CrackCount);
    }
    static void CrackAutoPostfix(object[] __args, ref Task __result)
    {
        CardModel card = (CardModel)__args[1]; Task original = __result;
        __result = Complete();
        async Task Complete()
        {
            await original;
            CrackEvents.Add("done:" + card.GetType().Name + ":" + CrackCount);
        }
    }
    static bool CrackPileAdd(CardModel card, object[] __args, ref Task<CardPileAddResult> __result)
    {
        CardPile pile = __args[1] is CardPile supplied ? supplied : ((PileType)__args[1]).GetPile(card.Owner);
        card.RemoveFromCurrentPile(); pile.AddInternal(card, -1, true);
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card });
        return false;
    }
    static bool CrackGenerated(CardModel card, PileType newPileType, ref Task<CardPileAddResult> __result)
    {
        card.RemoveFromCurrentPile(); newPileType.GetPile(card.Owner).AddInternal(card, -1, true);
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card }); return false;
    }
    static bool CrackRemove(object[] __args, ref Task __result)
    {
        CardModel[] cards = __args[0] is CardModel card ? [card] : ((IEnumerable<CardModel>)__args[0]).ToArray();
        foreach (CardModel removed in cards) { removed.RemoveFromCurrentPile(); removed.RemoveFromState(); }
        __result = Task.CompletedTask; return false;
    }
    static void CrackReset()
    {
        MoonlightReset(); Fate(FateJson(bosses: 0)); MagicSwordDeck(0);
        foreach ((string name, PileType type) in new[] { ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust) })
            Set(Player.PlayerCombatState, "<" + name + ">k__BackingField", new CardPile(type));
        Set(Player.PlayerCombatState, "_piles", null);
        Set(Combat, "_allCards", new List<CardModel>());
        CrackEvents.Clear(); CrackBlock = 0m; CrackEndRequests = CrackAttacks = 0; CrackEnding = CrackRejectFinisher = false;
    }
    static async Task<MagicSwordModePower> CrackOpen(int swordheart = 5, bool oldCard = false)
    {
        Power<SwordheartPower>(Hero, swordheart);
        if (oldCard) await UnifiedPlayCard(Owned<MagicSwordModeHbrgoSaki>(), false);
        else await (Task)Call("HbrMagicSwordToggle", "Open", new ThrowingPlayerChoiceContext(), Player)!;
        var mode = Hero.GetPower<MagicSwordModePower>();
        Check(mode != null && mode.CrackTimingVersion == 1 && !mode.HasSettledExitCrack && CrackCount == 0,
            (oldCard ? "old mode card" : "HUD open") + " uses the new schema without an opening crack");
        return mode!;
    }
    static async Task CrackClose() => await (Task)Call("HbrMagicSwordToggle", "Close", new ThrowingPlayerChoiceContext(), Player)!;
    static Task CrackPlay(CardModel card) => UnifiedPlayCard(card, false);

    static async Task MagicSwordCrackCases()
    {
        var harmony = new Harmony(ProductionId + ".magic-sword-crack-native-cards");
        var patched = new List<MethodBase>();
        void Patch(MethodBase target, string prefix, string? postfix = null)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(EffectChecks).GetMethod(postfix, F)!));
            patched.Add(target);
        }
        CardPile oldDeck = Player.Deck;
        ulong? oldLocalPlayer = LocalContext.NetId;
        object? oldAllCards = typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat);
        try
        {
            // 0.113 branches card resolution with LocalContext.NetId.Value;
            // this is actual multiplayer context, not a combat/card id.
            LocalContext.NetId = Player.NetId;
            foreach (Type type in new[] { typeof(MagicSwordModePower), typeof(SwordheartPower), typeof(ChargePower),
                         typeof(FeintHbrgoSaki), typeof(MagicSwordLuckySevenHbrgoSaki), typeof(MagicSwordLuckySevenOpeningHbrgoSaki) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CrackLive));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task)))
                Patch(wait, nameof(VisualTask));
            Type show = Helper("HbrLuckySevenFinisher");
            Patch(show.GetMethod("Begin", F)!, nameof(CrackShow));
            Patch(show.GetMethod("BeforeHits", F)!, nameof(VisualTask));
            Patch(show.GetMethod("AfterHits", F)!, nameof(VisualTask));
            Patch(show.GetMethod("Dispose", F)!, nameof(Skip));
            Patch(Helper("MagicSwordTurnEndHelper").GetMethod("Request", F)!, nameof(CrackEnd));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(CrackAttackLeaf));
            foreach (MethodInfo block in typeof(CreatureCmd).GetMethods(F).Where(method => method.Name == "GainBlock" && method.ReturnType == typeof(Task<decimal>) && method.GetParameters()[1].ParameterType == typeof(decimal)))
                Patch(block, nameof(CrackBlockLeaf));
            foreach (MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "Add" && method.GetParameters()[0].ParameterType == typeof(CardModel)))
                Patch(add, nameof(CrackPileAdd));
            foreach (MethodInfo remove in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "RemoveFromCombat"))
                Patch(remove, nameof(CrackRemove));
            foreach (MethodInfo generated in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "AddGeneratedCardToCombat"))
                Patch(generated, nameof(CrackGenerated));
            Patch(typeof(CardCmd).GetMethod("AutoPlay", F)!, nameof(CrackAutoPrefix), nameof(CrackAutoPostfix));
            Patch(typeof(Hook).GetMethod("ShouldPlay", F)!, nameof(CrackShouldPlay));

            foreach (bool oldCard in new[] { false, true })
            {
                CrackReset(); MagicSwordModePower mode = await CrackOpen(oldCard: oldCard);
                await CrackClose();
                Check(!Hero.HasPower<MagicSwordModePower>() && CrackCount == 1 && mode.HasSettledExitCrack,
                    "normal zero-hit exit removes magic sword and settles exactly one crack");
                Check(CrackAttacks == 0 && CrackEndRequests == 1 && CrackEvents.Any(e => e == "done:MagicSwordLuckySevenHbrgoSaki:0") && CrackEvents[^1] == "end:1",
                    "zero-hit Lucky Seven really completes before the crack and turn-end request");
            }
            CrackReset(); var active = await CrackOpen();
            await active.SpendSwordheart(new ThrowingPlayerChoiceContext(), 2m, Owned<MagicSwordModeHbrgoSaki>());
            await CrackClose();
            Check(CrackAttacks == 1 && CrackCount == 1 && CrackEndRequests == 1,
                "manual close resolves the consumed-swordheart attack before one exit crack");

            CrackReset(); active = await CrackOpen();
            var opening = (MagicSwordLuckySevenOpeningHbrgoSaki)ModelDb.Card<MagicSwordLuckySevenOpeningHbrgoSaki>().ToMutable();
            Set(opening, "_owner", Player);
            Player.Deck.AddInternal(opening, -1, true);
            await CrackClose();
            Check(CrackEvents.Count(e => e.StartsWith("done:", StringComparison.Ordinal)) == 3 &&
                CrackEvents.Where(e => e.StartsWith("done:", StringComparison.Ordinal)).All(e => e.EndsWith(":0", StringComparison.Ordinal)) &&
                CrackEvents[^1] == "end:1" && CrackCount == 1,
                "real Opening and both nested/final Lucky Seven bodies all complete before the single exit crack");

            CrackReset(); active = await CrackOpen(1);
            await active.SpendSwordheart(new ThrowingPlayerChoiceContext(), 1m, Owned<MagicSwordModeHbrgoSaki>());
            Check(!Hero.HasPower<MagicSwordModePower>() && CrackCount == 1 && CrackEndRequests == 1,
                "last swordheart consumed normally auto-exits and settles one crack");

            CrackReset(); active = Power<MagicSwordModePower>(Hero, 1); Fate(FateJson(cracks: 1));
            await CrackClose();
            Check(CrackCount == 1 && active.CrackTimingVersion == 0,
                "old schema already charged on entry is not charged again on exit");

            CrackReset(); active = await CrackOpen();
            var room = Blank<CombatRoom>(); Set(room, "<CombatState>k__BackingField", Combat);
            CrackEnding = true;
            await active.AfterCombatEnd(room); await active.AfterCombatEnd(room);
            Check(CrackCount == 1 && CrackEndRequests == 0, "combat ending with magic sword active settles once without requesting another turn");
            Delegate? ended = typeof(CombatManager).GetField("CombatEnded", F)?.GetValue(CombatManager.Instance) as Delegate;
            Delegate[] ownHandlers = ended?.GetInvocationList().Where(handler => handler.Method.DeclaringType?.FullName?.Contains(nameof(MagicSwordModePower), StringComparison.Ordinal) == true).ToArray() ?? [];
            Check(ownHandlers.Length == 1, "one production combat-ended fallback is registered for magic sword");
            foreach (Delegate handler in ownHandlers) handler.DynamicInvoke(room);
            Check(CrackCount == 1, "combat-ended fallback cannot duplicate the model hook's crack");
            CrackReset(); active = await CrackOpen(); CrackEnding = true;
            foreach (Delegate handler in ownHandlers) handler.DynamicInvoke(room);
            Check(CrackCount == 1, "loss fallback alone settles an active opening exactly once");

            await CrackEmergencyCases();

            CrackReset(); active = await CrackOpen();
            CrackRejectFinisher = true;
            await CrackClose();
            Check(CrackAttacks == 0 && CrackEvents.Any(e => e.StartsWith("done:MagicSwordLuckySevenHbrgoSaki:", StringComparison.Ordinal)),
                "native AutoPlay completes its rejected-card branch without executing a finisher attack");
            Check(CrackCount == 0 && !active.HasSettledExitCrack,
                "a finisher rejected by native ShouldPlay does not count as a completed finisher or add a crack");
        }
        finally
        {
            foreach (MethodBase method in patched) harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
            Set(Player, "<Deck>k__BackingField", oldDeck); Set(Combat, "_allCards", oldAllCards);
            LocalContext.NetId = oldLocalPlayer;
            CrackEnding = CrackRejectFinisher = false; CombatInProgress(false); As31O(false); Fate(string.Empty); Reset();
        }
    }

    static async Task CrackEmergencyCases()
    {
        foreach (bool upgraded in new[] { false, true })
        foreach (bool magic in new[] { false, true })
        {
            CrackReset();
            MagicSwordModePower? mode = magic ? await CrackOpen(5) : null;
            if (!magic) Power<SwordheartPower>(Hero, 5);
            var teleport = Owned<TeleportHbrgoSaki>();
            if (upgraded) { teleport.UpgradeInternal(); teleport.FinalizeUpgradeInternal(); }
            decimal blockPerHeart = teleport.DynamicVars.Block.BaseValue;
            await CrackPlay(teleport);
            Check(!Hero.HasPower<MagicSwordModePower>() && CrackCount == 0 && CrackEndRequests == 0 && CrackAttacks == 0 && CrackEvents.Count == 0,
                "Teleport silently removes magic sword without a finisher, crack, or turn-end request (magic " + magic + ", upgraded " + upgraded + ")");
            Equal(CrackBlock, 5m * blockPerHeart, "Teleport preserves its per-remaining-swordheart block command");
            Equal(Hero.GetPowerAmount<ChargePower>(), magic ? 2m : 0m, "Teleport grants charge only when it retreats from magic sword");
            Equal(Hero.GetPowerAmount<SwordheartPower>(), upgraded ? 1m : 0m, "Teleport upgrade grants its swordheart after the mode has been removed");
            Check(mode == null || mode.HasSettledExitCrack, "an emergency retreat marks its opening settled without adding a crack");
        }
        CrackReset(); var active = await CrackOpen(1);
        var lastCost = Owned<TeleportHbrgoSaki>();
        Player.PlayerCombatState.PlayPile.AddInternal(lastCost, -1, true);
        Call("MagicSwordEnergyPaymentHelper", "RecordPendingSwordheartCost", lastCost, 1);
        await active.BeforeCardPlayed(Play(lastCost));
        Check(Hero.HasPower<MagicSwordModePower>() && Hero.GetPowerAmount<SwordheartPower>() == 0 && CrackEndRequests == 0 && CrackEvents.Count == 0,
            "Teleport's final-swordheart energy payment does not prematurely trigger the normal exit");
        await CrackPlay(lastCost);
        Check(!Hero.HasPower<MagicSwordModePower>() && CrackCount == 0 && CrackEndRequests == 0 && CrackBlock == 0m,
            "after final-swordheart payment the real Teleport body still performs only its silent retreat");
    }
}
