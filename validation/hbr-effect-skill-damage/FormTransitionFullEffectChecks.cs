using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Real switch adapter, PowerCmd.Apply/AfterApplied removal, card OnPlay and
// native damage/block hooks. The unavailable native card-wrapper presentation
// tail and AttackCommand execution leaf alone are isolated explicitly.
static partial class EffectChecks
{
    sealed record FormHit(CardModel Card, decimal Raw, decimal Final);
    static readonly List<FormHit> FormHits = [];
    static readonly List<(Type Power, decimal Amount)> FormPowerRequests = [];
    static readonly List<decimal> FormDrawRequests = [];
    static bool FormReachedAfterSwitch;
    static readonly Type[] FormModes = [typeof(NormalStatePower), typeof(ReptureBowPower), typeof(ReptureDualSwordsPower), typeof(RaptureScythePower)];

    static bool FormNotEnding(ref bool __result) { __result = false; return false; }
    static bool FormPowerIconLeaf(ref Godot.Texture2D? __result) { __result = null; return false; }
    static bool FormAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!;
        var card = (CardModel)__instance.ModelSource!;
        // Execute's VFX/engine continuation is outside this host. Its raw
        // request goes through the actual shared calculation used by damage.
        FormHits.Add(new(card, raw, Actual(Enemy, __instance.Attacker, raw, __instance.DamageProps, card)));
        __result = Task.FromResult(__instance); return false;
    }
    static bool FormDrawLeaf(decimal count, ref Task<IEnumerable<CardModel>> __result)
    { FormDrawRequests.Add(count); __result = Task.FromResult(Enumerable.Empty<CardModel>()); return false; }
    static void FormObservePower(PowerModel power, decimal amount) => FormPowerRequests.Add((power.GetType(), amount));
    static bool FormOriginalPlayLeaf(CardModel instance, PlayerChoiceContext choiceContext, Creature target, bool isAutoPlay, ref Task __result)
    {
        FormReachedAfterSwitch = Call("ModeTransitionHelper", "GetOffModeTransition", instance)!.ToString() == "None";
        CardPlay play = Play(instance); Set(play, "<Target>k__BackingField", target); Set(play, "<IsAutoPlay>k__BackingField", isAutoPlay);
        __result = (Task)Native(instance, "OnPlay", choiceContext, play)!; return false;
    }
    static T FormCard<T>(bool upgraded = false) where T : CardModel, new()
    {
        var card = (T)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(T))).MutableClone();
        Set(card, "_owner", Player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        Player.PlayerCombatState!.Hand.AddInternal(card);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat)!).Add(card);
        return card;
    }
    static PowerModel FormPower(Type type, int amount = 1)
    {
        var power = (PowerModel)ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).MutableClone();
        Set(power, "_owner", Hero); Set(power, "_amount", amount); Powers(Hero).Add(power); return power;
    }
    static void FormReset(bool hbr = true, Type? currentMode = null, bool thirtyOneO = true)
    {
        Reset(hbr); As31O(thirtyOneO); CombatInProgressWithTurn();
        Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Player);
        Set(Combat, "_allCards", new List<CardModel>());
        var state = Player.PlayerCombatState!; Set(state, "_player", Player); Set(state, "_pets", new List<Creature>());
        Set(state, "_piles", null);
        foreach (var row in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            Set(state, "<" + row.Item1 + ">k__BackingField", new CardPile(row.Item2));
        Set(Player, "<Deck>k__BackingField", new CardPile(PileType.Deck)); Set(Player, "_runPiles", null);
        if (currentMode != null) FormPower(currentMode);
        FormHits.Clear(); FormPowerRequests.Clear(); FormDrawRequests.Clear(); FormReachedAfterSwitch = false;
    }
    static async Task FormPlay(CardModel card, bool automatic = false)
    {
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.ModeRestrictedCardPlayPatch", true)!;
        object?[] args = [card, new ThrowingPlayerChoiceContext(), Enemy, automatic, default(ResourceInfo), false, null];
        bool native = (bool)Invoke(patch.GetMethod("Prefix", F)!, null, args)!;
        if (native)
        {
            FormReachedAfterSwitch = true;
            var play = Play(card); Set(play, "<Target>k__BackingField", Enemy); Set(play, "<IsAutoPlay>k__BackingField", automatic);
            await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
        }
        else await (Task)args[^1]!;
    }
    static decimal FormDamagePreview(CardModel card)
    {
        Invoke(typeof(DamageVar).GetMethod(nameof(DamageVar.UpdateCardPreview), F)!, card.DynamicVars.Damage,
            card, CardPreviewMode.None, Enemy, true);
        return Convert.ToDecimal(typeof(DamageVar).GetProperty("PreviewValue", F)!.GetValue(card.DynamicVars.Damage));
    }

    static async Task FormTransitionFullEffectCases()
    {
        Type[] cards = [typeof(SlideStepHbrgoSaki), typeof(SwallowReversalHbrgoSaki), typeof(SoulHarvestHbrgoSaki),
            typeof(NextTurnEnergyStrikeHbrgoSaki), typeof(ArmorBreakSoulStrikeHbrgoSaki), typeof(MimicThunderGaleHbrgoSaki), typeof(StrikeIronclad)];
        foreach (Type type in cards.Concat(FormModes).Concat(new[] { typeof(QuickSwitchPower), typeof(EnergyNextTurnPower), typeof(FragilePower) }))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony(ProductionId + ".form-full-effects");
        var installed = new List<MethodBase>();
        void Leaf(MethodInfo target, string prefix)
        { harmony.Patch(target, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); installed.Add(target); }
        Type modePatch = Mod.GetType("HBRGOSakiMod.Core.Patches.ModeRestrictedCardPlayPatch", true)!;
        Type previewPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.ModeTransitionDamagePreviewPatch", true)!;
        try
        {
            Leaf(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(FormNotEnding));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Leaf(wait, nameof(VisualTask));
            Leaf(AccessTools.Method(typeof(AttackCommand), "Execute"), nameof(FormAttackLeaf));
            Leaf(typeof(CardPileCmd).GetMethods(F).Single(m => m.Name == "Draw" && m.GetParameters().Length == 4), nameof(FormDrawLeaf));
            Leaf(typeof(PowerCmd).GetMethods(F).Single(m => m.Name == "Apply" && !m.IsGenericMethod && m.GetParameters().Length == 7 && m.GetParameters()[1].ParameterType == typeof(PowerModel)), nameof(FormObservePower));
            Leaf(modePatch.GetMethod("OnPlayWrapperOriginal", F)!, nameof(FormOriginalPlayLeaf));
            harmony.CreateClassProcessor(previewPatch).Patch();
            installed.Add(typeof(DamageVar).GetMethod(nameof(DamageVar.UpdateCardPreview), F)!);
            await FormModeMatrixCases();
            await FormPowerAndLegacyCases();
            FormIntrinsicBalanceCases();
            FormChihayaDescriptionCases();
        }
        finally
        {
            foreach (MethodBase target in installed.Distinct()) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
            As31O(false); CombatInProgress(false); Reset();
        }
    }

    static async Task FormModeMatrixCases()
    {
        foreach (bool hbr in new[] { true, false })
        foreach (bool thirtyOneO in new[] { true, false })
        foreach (Type? current in new Type?[] { null, typeof(NormalStatePower), typeof(ReptureBowPower), typeof(ReptureDualSwordsPower), typeof(RaptureScythePower) })
        foreach (string required in new[] { "Bow", "DualSwords", "Scythe" })
        {
            FormReset(hbr, current, thirtyOneO);
            CardModel card = required switch { "Bow" => FormCard<SlideStepHbrgoSaki>(), "DualSwords" => FormCard<SwallowReversalHbrgoSaki>(), _ => FormCard<SoulHarvestHbrgoSaki>() };
            string label = $"{(hbr ? "HBR" : "native")}/{(thirtyOneO ? "31O" : "other")}/{current?.Name ?? "no-mode"}->{required}";
            decimal raw = card.DynamicVars.Damage.BaseValue;
            decimal preview = FormDamagePreview(card);
            bool initiallyMatching = (bool)Call("ModeTransitionHelper", "IsCardCompatibleWithCurrentMode", Hero, card)!;
            foreach (decimal amount in new[] { raw, 5m, 3.25m, -7m, 0m })
                Equal((decimal)Call("ModeTransitionHelper", "GetAdjustedEffectAmount", card, amount)!, amount,
                    label + " keeps the full authored effect amount including fractional and negative values");
            await FormPlay(card, automatic: !thirtyOneO);
            Check(FormReachedAfterSwitch && (bool)Call("ModeTransitionHelper", "IsCardCompatibleWithCurrentMode", Hero, card)!,
                label + " enters the required form before the actual card effect, for manual and automatic plays");
            Check(Hero.Powers.Count(power => FormModes.Contains(power.GetType())) == 1,
                label + " leaves one real weapon form after native AfterApplied removes the prior form");
            Equal(FormHits.Single().Raw, raw, label + " real OnPlay sends its complete raw damage request");
            Equal(preview, FormHits.Single().Final, label + " native targeted preview equals actual shared damage calculation after switching");
            Equal((decimal)Call("ModeTransitionHelper", "GetAdjustedEffectAmount", card, 5m)!, 5m,
                label + " preserves full raw block after switching");
            decimal block = Hook.ModifyBlock(Combat, Hero, 5m, ValueProp.Move, card, null, out _);
            decimal baseBlock = hbr ? 500m : 5m;
            Equal(block, baseBlock,
                label + " real block hooks retain the target form's intrinsic modifier without a transition penalty");
            if (!initiallyMatching)
                Check(FormPowerRequests.Any(request => request.Power == (required == "Bow" ? typeof(ReptureBowPower) : required == "DualSwords" ? typeof(ReptureDualSwordsPower) : typeof(RaptureScythePower))),
                    label + " uses native PowerCmd.Apply to perform its automatic form change");
            if (required == "Bow") Equal(FormDrawRequests.Single(), 1m, label + " preserves the card's authored draw effect");
        }
    }

    static async Task FormPowerAndLegacyCases()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            FormReset(currentMode: typeof(NormalStatePower));
            CardModel energy = FormCard<NextTurnEnergyStrikeHbrgoSaki>(upgraded);
            var quick = FormPower(typeof(QuickSwitchPower), 3);
            decimal raw = energy.DynamicVars.Damage.BaseValue;
            await FormPlay(energy);
            Equal(FormHits.Single().Raw, raw, "off-form energy attack keeps its full " + (upgraded ? "upgraded" : "base") + " damage");
            Check(Hero.GetPower<EnergyNextTurnPower>()?.Amount == 1 && FormPowerRequests.Any(request => request.Power == typeof(EnergyNextTurnPower) && request.Amount == 1m),
                "off-form actual OnPlay gives its complete one next-turn energy layer without transition rounding");
            Equal(quick.Amount, 3, "legacy QuickSwitch stacks are never reserved or spent by full-effect transitions");

            FormReset(currentMode: typeof(ReptureDualSwordsPower));
            CardModel scythe = FormCard<ArmorBreakSoulStrikeHbrgoSaki>(upgraded);
            await FormPlay(scythe);
            Equal(FormHits.Single().Raw, scythe.DynamicVars.Damage.BaseValue, "scythe transition retains full raw base damage");
            decimal grant = scythe.DynamicVars["FragilePower"].BaseValue;
            Check(grant == 0m ? FormPowerRequests.All(request => request.Power != typeof(FragilePower)) :
                    FormPowerRequests.Any(request => request.Power == typeof(FragilePower) && request.Amount == grant),
                "scythe transition preserves zero/base and full upgraded debuff grant amounts");
        }
        FormReset(currentMode: typeof(NormalStatePower));
        CardModel suppressed = FormCard<SwallowReversalHbrgoSaki>();
        using ((IDisposable)Call("ModeTransitionHelper", "SuppressModeTransition", suppressed)!)
        {
            await FormPlay(suppressed);
            Check(Hero.HasPower<NormalStatePower>() && !Hero.HasPower<ReptureDualSwordsPower>(),
                "explicit transition suppression still prevents only the automatic switch");
            Equal(FormHits.Single().Raw, suppressed.DynamicVars.Damage.BaseValue,
                "explicitly suppressed switch also preserves the card's full raw effect");
        }
        Check(Call("ModeTransitionHelper", "BeginCardPlay", suppressed)!.GetType().GetProperty("RequiredMode")!.GetValue(Call("ModeTransitionHelper", "BeginCardPlay", suppressed))!.ToString() == "DualSwords",
            "disposing explicit suppression restores the ordinary automatic-switch rule");
    }

    static void FormIntrinsicBalanceCases()
    {
        foreach (bool hbr in new[] { true, false })
        {
            FormReset(hbr); var strike = FormCard<StrikeIronclad>();
            decimal raw = strike.DynamicVars.Damage.BaseValue;
            decimal baseline = Actual(Enemy, Hero, raw, ValueProp.Move, strike);
            FormPower(typeof(NormalStatePower));
            decimal normal = Actual(Enemy, Hero, raw, ValueProp.Move, strike);
            Equal(normal, hbr ? decimal.Floor(baseline * .5m) : baseline * .5m,
                "NormalState keeps its own 50 percent outgoing damage balance in " + (hbr ? "HBR" : "native") + " mode");
            Equal(Hook.ModifyBlock(Combat, Hero, 5m, ValueProp.Move, strike, null, out _), hbr ? 375m : 3.75m,
                "NormalState keeps its own 75 percent block balance in " + (hbr ? "HBR" : "native") + " mode");
        }
        Check(Helper("ModeTransitionHelper").GetMethod("TryGetCurrentEffectHalving", F) == null &&
            Helper("ModeTransitionHelper").GetMethod("PushEffectHalvingContext", F) == null &&
            typeof(QuickSwitchPower).GetMethod("TryReserve", F) == null,
            "removed transition-halving/reservation machinery cannot allocate or consume obsolete per-play state");
    }

    static void FormChihayaDescriptionCases()
    {
        Type type = typeof(ChihayaClassmateHbrgoSaki);
        if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        Set(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Models.CardPools.HBRGOSaki31OCardPool", true)!;
        if (!ModelDb.Contains(pool)) ModelDb.Inject(pool);
        Type context = Mod.GetType("HBRGOSakiMod.Core.Integration.HbrgoSakiCardPortraitContext", true)!;
        FieldInfo libraryField = context.GetField("_library", F)!, variantField = context.GetField("_variant", F)!;
        object? savedLibrary = libraryField.GetValue(null), savedVariant = variantField.GetValue(null);
        var harmony = new Harmony(ProductionId + ".form-chihaya-description");
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!;
        MethodInfo getter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
        MethodInfo postfix = patch.GetMethod("Postfix", F)!;
        bool install = Harmony.GetPatchInfo(getter)?.Postfixes.Any(item => item.PatchMethod == postfix) != true;
        if (install) harmony.CreateClassProcessor(patch).Patch();
        MethodInfo icon = AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Icon));
        MethodInfo iconLeaf = typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf), F)!;
        harmony.Patch(icon, prefix: new HarmonyMethod(iconLeaf));
        void Description(CardModel card, bool scythe, string label)
        {
            string formatted = card.Description.GetFormattedText();
            Check(formatted.Contains("Rapture-弓") && formatted.Contains("Rapture-双剑") && formatted.Contains("镰刀") == scythe,
                label + " real native Description getter and production localization patch format exactly its available forms");
            Check(card.HoverTips.Any(tip => tip.Id == ModelDb.GetId(typeof(RaptureScythePower)).ToString()) == scythe,
                label + " native HoverTips shares the live/library form predicate for the scythe entry");
        }
        try
        {
            FormReset(thirtyOneO: true); Description(FormCard<ChihayaClassmateHbrgoSaki>(), false, "live31O Chihaya");
            FormReset(thirtyOneO: false); Description(FormCard<ChihayaClassmateHbrgoSaki>(), true, "live other-character Chihaya");
            Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki30A>());
            Description(FormCard<ChihayaClassmateHbrgoSaki>(), true, "live30A Chihaya");
            CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            libraryField.SetValue(null, null); Description(canonical, false, "canonical default Chihaya");
            object library = new();
            Invoke(context.GetMethod("SetLibraryVariant", F)!, null, library, HbrgoSakiVariant.ThirtyA);
            Description(canonical, true, "canonical30A library Chihaya");
            Invoke(context.GetMethod("SetLibraryVariant", F)!, null, library, HbrgoSakiVariant.ThirtyOneO);
            Description(canonical, false, "canonical31O library Chihaya");
        }
        finally
        {
            libraryField.SetValue(null, savedLibrary); variantField.SetValue(null, savedVariant);
            if (install) harmony.Unpatch(getter, postfix);
            harmony.Unpatch(icon, iconLeaf);
        }
    }
}
