using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

// The shared quote, real native resource check/SpendResources and the mode's
// BeforeCardPlayed execute. Godot rendering is not exercised; event binding
// uses real native model/resource events with only the repaint leaf replaced.
static partial class EffectChecks
{
    static readonly List<int> CostEnergyEvents = [], CostEnergyHistory = [];
    static int CostRepaints;
    static bool CostLive(ref bool __result) { __result = false; return false; }
    static void CostEnergyEvent(CardModel card, int amount) => CostEnergyEvents.Add(amount);
    static void CostHistoryEvent(int amount) => CostEnergyHistory.Add(amount);
    static bool CostRepaintLeaf() { CostRepaints++; return false; }

    static object? CostQuote(CardModel card, bool display = false, PileType pile = PileType.Hand, bool visible = true)
    {
        object?[] args = display ? [card, pile, visible, null] : [card, null];
        MethodInfo method = Helper(display ? "MagicSwordCostDisplayHelper" : "MagicSwordEnergyPaymentHelper")
            .GetMethod(display ? "TryGetQuote" : "TryGetPaymentQuote", F)!;
        return (bool)Invoke(method, null, args)! ? args[^1] : null;
    }
    static T CostValue<T>(object quote, string property) => (T)quote.GetType().GetProperty(property, F)!.GetValue(quote)!;
    static CardModel CostHand(int energyCost = 3, int energy = 1, int stars = 3)
    {
        MoonlightReset(); Fate(FateJson(bosses: 0));
        Set(Player.PlayerCombatState!, "_player", Player);
        Set(Player.PlayerCombatState!, "_energy", energy);
        Set(Player.PlayerCombatState!, "_stars", stars);
        var card = Owned<StrikeIronclad>();
        card.EnergyCost.SetThisCombat(energyCost);
        Player.PlayerCombatState!.Hand.AddInternal(card, -1, true);
        CostEnergyEvents.Clear(); CostEnergyHistory.Clear();
        return card;
    }
    static void CostCheckQuote(object quote, int deficit, int energy, decimal swordheart, bool enough)
    {
        Equal(CostValue<int>(quote, "EnergyDeficit"), deficit, "shared quote preserves the original energy deficit");
        Equal(CostValue<int>(quote, "EnergyToSpend"), energy, "shared quote uses only energy actually available");
        Equal(CostValue<decimal>(quote, "SwordheartToSpend"), swordheart, "shared quote resolves the actual Clarity-adjusted swordheart cost");
        Check(CostValue<bool>(quote, "HasEnoughSwordheart") == enough, "shared quote reports whether its swordheart badge must be red");
    }

    static async Task MagicSwordCostDisplayCases()
    {
        var harmony = new Harmony(ProductionId + ".magic-sword-cost-native-payment");
        var patched = new List<MethodBase>();
        ulong? oldLocal = LocalContext.NetId;
        ExceptionDispatchInfo? failure = null;
        void Patch(MethodInfo method, string prefix) { harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(method); }
        try
        {
            LocalContext.NetId = Player.NetId;
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            foreach (string name in new[] { "PlayerCombatStateHasEnoughResourcesForPatch", "CardModelSpendResourcesPatch" })
            {
                Type type = Mod.GetType("HBRGOSakiMod.Core.Patches." + name, true)!;
                harmony.CreateClassProcessor(type).Patch();
                patched.Add(name == "PlayerCombatStateHasEnoughResourcesForPatch"
                    ? typeof(PlayerCombatState).GetMethod("HasEnoughResourcesFor", F)!
                    : typeof(CardModel).GetMethod("SpendResources", F)!);
            }
            harmony.Patch(typeof(Hook).GetMethod("AfterEnergySpent", F)!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(CostEnergyEvent), F)!));
            patched.Add(typeof(Hook).GetMethod("AfterEnergySpent", F)!);
            MethodInfo history = CombatManager.Instance.History.GetType().GetMethod("EnergySpent", F)!;
            harmony.Patch(history, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(CostHistoryEvent), F)!)); patched.Add(history);

            foreach (bool magic in new[] { false, true })
            foreach (bool clarity in new[] { false, true })
            {
                CardModel card = CostHand(); card.SetStarCostThisTurn(2);
                Power<SwordheartPower>(Hero, 6);
                MagicSwordModePower? mode = magic ? Power<MagicSwordModePower>(Hero, 1) : null;
                if (!magic) Power<MagicSwordUniversalityPower>(Hero, 1);
                if (clarity) Power<SwordheartClarityPower>(Hero, 1);
                decimal hearts = clarity ? 4m : 2m;
                object quote = CostQuote(card, true)!;
                Check(quote != null, "an owned hand card displays a quote for magic sword or universality");
                CostCheckQuote(quote!, 2, 1, hearts, true);
                Equal(CostValue<int>(quote!, "StarsToSpend"), 2m, "a simultaneous native star cost remains in the quote");
                Check(Player.PlayerCombatState!.HasEnoughResourcesFor(card, out UnplayableReason reason) && reason == UnplayableReason.None,
                    "native resource validation accepts the same affordable Swordheart quote");
                int heartBefore = Hero.GetPowerAmount<SwordheartPower>();
                var spent = await card.SpendResources();
                Check(spent == (1, 2) && Player.PlayerCombatState.Energy == 0 && Player.PlayerCombatState.Stars == 1,
                    "real SpendResources spends one energy and the unchanged two-star cost");
                if (mode != null)
                {
                    Equal(Hero.GetPowerAmount<SwordheartPower>(), heartBefore, "magic-mode payment defers its swordheart portion to BeforeCardPlayed");
                    await mode.BeforeCardPlayed(Play(card));
                    Equal(mode.ConsumedSwordheartAmount, hearts, "magic-mode Lucky Seven records the full actual quoted consumption");
                }
                Equal(Hero.GetPowerAmount<SwordheartPower>(), heartBefore - hearts,
                    "real resource payment and the displayed Swordheart cost agree (magic " + magic + ", clarity " + clarity + ")");
                Check(CostEnergyEvents.SequenceEqual(new[] { 1 }) && CostEnergyHistory.SequenceEqual(new[] { 1 }),
                    "energy hooks and native history record actual energy rather than the full three-energy price");
                Equal(card.EnergyCost.GetAmountToSpend(), 3m, "display and mixed payment do not modify the model's energy cost");
                Equal(card.GetStarCostWithModifiers(), 2m, "display and mixed payment do not modify the model's star cost");
            }

            CardModel insufficient = CostHand(); Power<MagicSwordModePower>(Hero, 1); Power<SwordheartPower>(Hero, 1);
            object red = CostQuote(insufficient, true)!;
            CostCheckQuote(red, 2, 1, 2m, false);
            Check(!Player.PlayerCombatState!.HasEnoughResourcesFor(insufficient, out var blocked) && blocked.HasFlag(UnplayableReason.EnergyCostTooHigh),
                "a red Swordheart badge leaves native playability blocked");
            Powers(Hero).Remove(Hero.GetPower<SwordheartPower>()!);
            Check(CostQuote(insufficient, true) is { } empty && !CostValue<bool>(empty, "HasEnoughSwordheart"),
                "zero Swordheart still displays an unaffordable quote");
            Player.PlayerCombatState.GainEnergy(2m);
            Check(CostQuote(insufficient) == null && CostQuote(insufficient, true) == null, "restoring enough energy hides the Swordheart badge");

            CardModel noMode = CostHand(); Power<SwordheartPower>(Hero, 6);
            Check(CostQuote(noMode) == null, "Swordheart alone does not enable energy substitution");
            Power<MagicSwordUniversalityPower>(Hero, 1);
            Check(CostQuote(noMode, true, PileType.Deck) == null && CostQuote(noMode, true, visible: false) == null,
                "library/deck and hidden card presentations retain native cost display");
            LocalContext.NetId = Player.NetId + 1;
            Check(CostQuote(noMode, true) == null, "a teammate's hand does not borrow the local player's cost display");
            LocalContext.NetId = Player.NetId;
            foreach (int cost in new[] { 0, -1 })
            {
                CardModel free = CostHand(cost, 0); Power<MagicSwordModePower>(Hero, 1);
                Check(CostQuote(free, true) == null, "zero or negative energy cost does not display a substitution badge");
            }
            CardModel x = Owned<Whirlwind>(); Player.PlayerCombatState!.Hand.AddInternal(x, -1, true);
            Check(x.EnergyCost.CostsX && CostQuote(x, true) == null, "X-cost cards keep their native X and never show a Swordheart substitution");
            CardModel starsShort = CostHand(stars: 0); starsShort.SetStarCostThisTurn(2);
            Power<MagicSwordUniversalityPower>(Hero, 1); Power<SwordheartPower>(Hero, 6);
            object starQuote = CostQuote(starsShort, true)!;
            Check(CostValue<bool>(starQuote, "HasEnoughSwordheart") && !CostValue<bool>(starQuote, "CanAfford"),
                "sufficient Swordheart does not bypass a separate insufficient native star cost");
            await CostBindingCases(harmony, patched);
            CostDisplayStructureCases();
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase method in patched.Distinct())
            {
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            }
            LocalContext.NetId = oldLocal;
            try { CombatInProgress(false); As31O(false); Reset(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }

    static Task CostBindingCases(Harmony harmony, List<MethodBase> patched)
    {
        CardModel card = CostHand(); Power<MagicSwordModePower>(Hero, 1);
        Type nodeType = Mod.GetType("HBRGOSakiMod.Core.Nodes.Cards.NMagicSwordCostBadge", true)!;
        object node = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(nodeType);
        GC.SuppressFinalize(node);
        NCard view = Blank<NCard>(); Set(view, "_model", card); Set(view, "_visibility", ModelVisibility.Visible);
        Set(node, "_card", view); Set(node, "_displayedPile", PileType.Hand);
        MethodInfo repaint = nodeType.GetMethod("RefreshNative", F)!;
        harmony.Patch(repaint, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(CostRepaintLeaf), F)!)); patched.Add(repaint);
        CostRepaints = 0;
        Invoke(nodeType.GetMethod("Bind", F)!, node);
        Invoke(nodeType.GetMethod("Bind", F)!, node);
        Player.PlayerCombatState!.GainEnergy(1m);
        Check(CostRepaints == 1, "rebinding the same hand card does not duplicate energy subscriptions");
        card.InvokeEnergyCostChanged();
        Check(CostRepaints == 2, "a real card-local cost notification refreshes the extra cost display");
        var heart = Power<SwordheartPower>(Hero, 5);
        Hero.InvokePowerModified(heart, 1, false);
        Check(CostRepaints == 3, "a native Swordheart amount event refreshes the hand cost");
        Invoke(nodeType.GetMethod("Unbind", F)!, node);
        Player.PlayerCombatState.GainEnergy(1m); card.InvokeEnergyCostChanged(); Hero.InvokePowerModified(heart, -1, false);
        Check(CostRepaints == 3, "unbinding removes the old model, energy and power listeners");
        Invoke(nodeType.GetMethod("Bind", F)!, node);
        Invoke(nodeType.GetMethod("_ExitTree", F | BindingFlags.DeclaredOnly)!, node);
        Player.PlayerCombatState.GainEnergy(1m); card.InvokeEnergyCostChanged();
        Check(CostRepaints == 3, "exiting the tree removes event-driven hand cost updates");
        return Task.CompletedTask;
    }

    static void CostDisplayStructureCases()
    {
        Type node = Mod.GetType("HBRGOSakiMod.Core.Nodes.Cards.NMagicSwordCostBadge", true)!;
        Check(node.GetMethod("_Process", F | BindingFlags.DeclaredOnly) == null, "Swordheart cost display introduces no per-frame polling");
        foreach (string name in new[] { "UpdateEnergyCostVisuals", "UpdateStarCostVisuals", "OnFreedToPool", "OnReturnedFromPool" })
            Check(typeof(NCard).GetMethod(name, F) != null, "native card cost presentation boundary exists: " + name);
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Patches.MagicSwordCostPoolPatch", true)!;
        var targets = ((IEnumerable<MethodBase>)Invoke(pool.GetMethod("TargetMethods", F)!, null)!).ToArray();
        Check(targets.Length == 2 && targets.Any(m => m.Name == "OnFreedToPool") && targets.Any(m => m.Name == "OnReturnedFromPool"),
            "both native pool lifecycle boundaries reset the independent resource badge");
    }
}
