using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;

// Actual card bodies, native power application/removal, native deck cloning and
// production setup filtering run here. Only animation-facing damage, block loss,
// turn-end and pile-removal leaves are replaced with managed equivalents.
static partial class EffectChecks
{
    static AttackCommand? TemplateAttack;
    static int TemplateEndRequests;
    static decimal TemplateLostBlock;
    static int TemplateRequestedHits() => (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(TemplateAttack)!;
    static bool TemplateAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    { TemplateAttack = __instance; __result = Task.FromResult(__instance); return false; }
    static bool TemplateEndLeaf(Player player, bool canBackOut)
    { Check(ReferenceEquals(player, Player) && !canBackOut, "Training Time ends only its owner without permitting cancellation"); TemplateEndRequests++; return false; }
    static bool TemplateLoseBlockLeaf(object[] __args, ref Task __result)
    {
        TemplateLostBlock += __args.OfType<decimal>().Single(); SetNativeBlock(0);
        __result = Task.CompletedTask; return false;
    }
    static bool TemplateNestedAutoLeaf(CardModel card, ref Task __result)
    { Check(card is MagicSwordLuckySevenHbrgoSaki, "opening retains its explicit Lucky Seven follow-up"); __result = Task.CompletedTask; return false; }
    static bool TemplateAllListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result = Combat.Creatures.SelectMany(creature => creature.Powers).Cast<AbstractModel>()
            .Concat(Player.Relics).Concat(Player.PlayerCombatState!.AllCards).ToArray();
        return false;
    }
    static bool TemplateDrawOneLeaf(Player player, ref Task<CardModel> __result)
    {
        CardModel card = player.PlayerCombatState!.DrawPile.Cards.First();
        card.RemoveFromCurrentPile(); player.PlayerCombatState.Hand.AddInternal(card, -1, true);
        __result = Task.FromResult(card); return false;
    }
    static bool TemplatePileTopLeaf(CardModel card, object[] __args, ref Task<CardPileAddResult> __result)
    {
        CardPile pile = __args[1] is CardPile supplied ? supplied : ((PileType)__args[1]).GetPile(card.Owner);
        bool top = __args.OfType<CardPilePosition>().Any(position => position == CardPilePosition.Top);
        card.RemoveFromCurrentPile(); pile.AddInternal(card, top ? 0 : -1, true);
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card }); return false;
    }

    static void TemplateReset(bool hbr = true)
    {
        CrimsonReset(hbr); SetNativeBlock(0); MagicSwordDeck(0);
        Set(Player.PlayerCombatState!, "_pets", new List<MegaCrit.Sts2.Core.Entities.Creatures.Creature>());
        Set(Player, "_runPiles", null); TemplateAttack = null; TemplateEndRequests = 0; TemplateLostBlock = 0;
    }
    static async Task TemplatePlay(CardModel card, int energy = 0)
    {
        Player.PlayerCombatState!.PlayPile.AddInternal(card, -1, true);
        CardPlay play = Play(card); Set(play, "<Target>k__BackingField", Enemy);
        Set(play, "<Resources>k__BackingField", new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = 0, StarValue = 0 });
        await (Task)Invoke(card.GetType().GetMethod("OnPlay", F)!, card, new ThrowingPlayerChoiceContext(), play)!;
    }

    static async Task CardTemplateBaselineCases()
    {
        var harmony = new Harmony(ProductionId + ".card-template-frozen-baseline");
        MethodInfo attack = typeof(AttackCommand).GetMethod("Execute", F)!;
        MethodInfo ending = AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding");
        harmony.Patch(attack, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(TemplateAttackLeaf), F)!));
        harmony.Patch(ending, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(CostLive), F)!));
        try
        {
            foreach (int energy in new[] { 1, 2, 3 })
            {
                TemplateReset(); var card = ClarityCard<BladeDanceHbrgoSaki>();
                Power<ReptureDualSwordsPower>(Hero, 1);
                await TemplatePlay(card, energy);
                decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(TemplateAttack)!;
                Equal(raw, energy, "frozen before-change Blade Dance sends raw X into native AttackCommand");
                decimal percent = (decimal)Call("HbrSkillPower", "DamagePercent", raw)!;
                Equal(percent, energy / .06m, "frozen before-change coefficient is X times one-sixth of a hundred percent");
                decimal actual = Actual(Enemy, Hero, raw, ValueProp.Move, card);
                Equal(actual, decimal.Floor(Base(energy) * .75m), "frozen before-change HBR damage includes the live attributes and old dual-sword seventy-five percent factor");
                Equal(Preview(Enemy, Hero, raw, ValueProp.Move, card), actual, "frozen before-change preview equals actual HBR damage");
                Cases.Add(new { scenario = "before-change-blade-dance", energy, raw, percent, actual, requestedHits = TemplateRequestedHits() });
            }
        }
        finally
        {
            harmony.Unpatch(attack, HarmonyPatchType.All, harmony.Id);
            harmony.Unpatch(ending, HarmonyPatchType.All, harmony.Id);
            Reset(); As31O(false);
        }
    }

    static async Task CardTemplateBalanceCases()
    {
        var harmony = new Harmony(ProductionId + ".card-template-balances");
        var patched = new List<MethodBase>(); ExceptionDispatchInfo? failure = null;
        CardPile oldDeck = Player.Deck; ulong? local = LocalContext.NetId;
        void Patch(MethodBase target, string prefix)
        { harmony.Patch(target, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(target); }
        try
        {
            LocalContext.NetId = Player.NetId; CombatInProgressWithTurn();
            foreach (Type type in new[] { typeof(BladeDanceHbrgoSaki), typeof(NoSelfInDreamHbrgoSaki), typeof(TrainingTimeHbrgoSaki),
                typeof(TrainingTimePower), typeof(NoSelfInDreamPower), typeof(NoSelfInDreamTemporaryStrengthPower), typeof(StrengthPower),
                typeof(DexterityPower), typeof(DrawCardsNextTurnPower), typeof(FragilePower), typeof(VulnerablePower), typeof(HbrDefenseDownPower),
                typeof(MagicSwordLuckySevenHbrgoSaki), typeof(MagicSwordLuckySevenOpeningHbrgoSaki), typeof(SwordheartPower),
                typeof(ConsecutiveSlashHbrgoSaki), typeof(LoliFormHbrgoSaki), typeof(StrikeIronclad), typeof(MimicThunderGaleHbrgoSaki) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(TemplateAttackLeaf));
            Patch(typeof(PlayerCmd).GetMethod("EndTurn", F)!, nameof(TemplateEndLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            foreach (MethodInfo remove in typeof(CardPileCmd).GetMethods(F).Where(m => m.Name == "RemoveFromCombat")) Patch(remove, nameof(CrackRemove));
            foreach (MethodInfo lose in typeof(CreatureCmd).GetMethods(F).Where(m => m.Name == "LoseBlock")) Patch(lose, nameof(TemplateLoseBlockLeaf));
            foreach (MethodInfo sound in typeof(SfxCmd).GetMethods(F).Where(m => m.Name == "Play")) Patch(sound, nameof(Skip));
            foreach (MethodInfo get in typeof(PowerModel).GetMethods(F).Where(m => m.Name == "get_Icon")) Patch(get, nameof(FormPowerIconLeaf));
            foreach (string name in new[] { "MagicSwordLuckySevenPopulateCombatStatePatch" })
            {
                Type type = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
                harmony.CreateClassProcessor(type).Patch();
                patched.AddRange(typeof(Player).GetMethods(F).Where(m => m.Name == "PopulateCombatState"));
            }
            await TemplateDamageCases();
            await TemplateResourceCases();
            TemplateDescriptionCases(harmony, patched);
            await TemplateDeckCases(harmony, patched);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase target in patched.Distinct())
                try { harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            LocalContext.NetId = local; Set(Player, "<Deck>k__BackingField", oldDeck); Set(Player, "_runPiles", null);
            try { CombatInProgress(false); Reset(); As31O(false); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }

    static async Task TemplateDamageCases()
    {
        foreach (bool hbr in new[] { true, false })
        foreach (bool upgraded in new[] { false, true })
        foreach (int energy in new[] { 1, 2, 3 })
        {
            TemplateReset(hbr); var card = ClarityCard<BladeDanceHbrgoSaki>();
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await TemplatePlay(card, energy);
            decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(TemplateAttack)!;
            Equal(raw, energy * (hbr ? 1.2m : 1m), "Blade Dance submits X times 20-percent units only in HBR (X " + energy + ", upgraded " + upgraded + ", HBR " + hbr + ")");
            Equal(TemplateRequestedHits(), upgraded ? 6m : 4m, "Blade Dance preserves four/six authored hits before combo effects");
            Equal(Preview(Enemy, Hero, raw, ValueProp.Move, card), Actual(Enemy, Hero, raw, ValueProp.Move, card), "Blade Dance submitted damage shares preview and actual calculation");
            if (hbr) Equal((decimal)Call("HbrSkillPower", "DamagePercent", raw)!, energy * 20m, "HBR Blade Dance coefficient is exactly twenty percent per energy");
        }
        var harmony = new Harmony(ProductionId + ".opening-followup-only");
        MethodInfo auto = typeof(CardCmd).GetMethod("AutoPlay", F)!;
        harmony.Patch(auto, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(TemplateNestedAutoLeaf), F)!));
        try
        {
            foreach (bool hbr in new[] { true, false })
            foreach (int hearts in new[] { 1, 7, 14 })
            {
                TemplateReset(hbr); var card = ClarityCard<MagicSwordLuckySevenOpeningHbrgoSaki>();
                Invoke(card.GetType().GetMethod("SetConsumedSwordheartAmount", F)!, card, hearts);
                await TemplatePlay(card);
                decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(TemplateAttack)!;
                Equal(raw, hearts * (hbr ? 1.2m : 1m), "opening uses consumed hearts times twenty percent only in HBR (hearts " + hearts + ", HBR " + hbr + ")");
                Equal(Preview(Enemy, Hero, raw, ValueProp.Move, card), Actual(Enemy, Hero, raw, ValueProp.Move, card), "opening submitted skill retains common preview/actual formula");
                Check(Enemy.HasPower<FragilePower>(), "opening retains its post-damage Fragile application");
            }
        }
        finally { harmony.Unpatch(auto, HarmonyPatchType.All, harmony.Id); }
    }

    static async Task TemplateResourceCases()
    {
        foreach (bool hbr in new[] { true, false })
        foreach (int block in new[] { 0, 299, 300, 1500 })
        {
            TemplateReset(hbr); SetNativeBlock(block);
            await TemplatePlay(ClarityCard<NoSelfInDreamHbrgoSaki>());
            Equal(Hero.GetPowerAmount<NoSelfInDreamTemporaryStrengthPower>(), block / (hbr ? 300 : 3), "No Self in Dream floors lost-block conversion in its numeric mode (block " + block + ", HBR " + hbr + ")");
            Check(Hero.Block == 0 && TemplateLostBlock == block && Hero.GetPowerAmount<NoSelfInDreamPower>() == 1,
                "No Self in Dream still loses all block and grants its original one-turn power");
        }
        foreach (bool hbr in new[] { true, false })
        foreach (bool upgraded in new[] { false, true })
        {
            TemplateReset(hbr); var card = ClarityCard<TrainingTimeHbrgoSaki>();
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await TemplatePlay(card);
            Check(TemplateEndRequests == 1 && Hero.GetPowerAmount<TrainingTimePower>() == 5 && !Hero.HasPower<StrengthPower>() && !Hero.HasPower<DexterityPower>(),
                "Training Time queues five and ends the current turn without granting immediate stats");
            Equal(Hero.GetPowerAmount<DrawCardsNextTurnPower>(), upgraded ? 1m : 0m, "Training Time preserves its upgrade-only extra draw");
            TrainingTimePower power = Hero.GetPower<TrainingTimePower>()!;
            await power.AfterSideTurnStart(Hero.Side, new[] { Hero }, Combat);
            Check(Hero.GetPowerAmount<StrengthPower>() == 5 && Hero.GetPowerAmount<DexterityPower>() == 5 && !Hero.HasPower<TrainingTimePower>(),
                "the real next-turn hook grants five Strength and Dexterity and removes the pending power");
        }
    }

    static void TemplateDescriptionCases(Harmony harmony, List<MethodBase> patched)
    {
        Localization();
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!;
        MethodInfo getter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
        MethodInfo postfix = patch.GetMethod("Postfix", F)!;
        if (Harmony.GetPatchInfo(getter)?.Postfixes.Any(item => item.PatchMethod == postfix) != true)
        { harmony.CreateClassProcessor(patch).Patch(); patched.Add(getter); }
        string Face(CardModel card, bool detail, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target = null)
        {
            using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F)
                .Single(method => method.Name == "Begin" && method.GetParameters().Length == 4), null, card, detail, false, target)!;
            return card.GetDescriptionForPile(PileType.Hand, target);
        }
        foreach (int energy in new[] { 1, 2, 3 })
        {
            TemplateReset(); var card = ClarityCard<BladeDanceHbrgoSaki>();
            Player.PlayerCombatState!.Hand.AddInternal(card, -1, true); Set(Player.PlayerCombatState, "_energy", energy);
            decimal actual = Actual(Enemy, Hero, energy * 1.2m, ValueProp.Move, card);
            string face = Face(card, false, Enemy), detail = Face(card, true, Enemy);
            Check(face.Contains(actual.ToString("0")) && !face.Contains("（") && !face.Contains("倍率"),
                "Blade Dance selected-target face shows its actual damage rather than a skill coefficient at X " + energy);
            Check(detail.Contains("（" + energy * 20 + "%）"), "Blade Dance inspect adds only the exact parenthesized X coefficient");
        }
        TemplateReset(); var opening = ClarityCard<MagicSwordLuckySevenOpeningHbrgoSaki>();
        Player.PlayerCombatState!.Hand.AddInternal(opening, -1, true);
        Invoke(opening.GetType().GetMethod("SetConsumedSwordheartAmount", F)!, opening, 7);
        decimal damage = Actual(Enemy, Hero, 8.4m, ValueProp.Move, opening);
        Check(Face(opening, false, Enemy).Contains(damage.ToString("0")) && Face(opening, true, Enemy).Contains("（140%）"),
            "configured opening face and inspect agree with the full damage pipeline and seven-heart coefficient");
        var openingTemplate = ClarityCard<MagicSwordLuckySevenOpeningHbrgoSaki>();
        Check(Face(openingTemplate, true).Contains("（20%）"),
            "an unset permanent opening template shows the per-heart twenty-percent basis rather than a misleading zero");
        Invoke(openingTemplate.GetType().GetMethod("SetConsumedSwordheartAmount", F)!, openingTemplate, 0);
        Check(Face(openingTemplate, true).Contains("（0%）"), "an explicitly configured zero-heart opening still previews a true zero");
        foreach (bool hbr in new[] { true, false })
        {
            TemplateReset(hbr); var dream = ClarityCard<NoSelfInDreamHbrgoSaki>();
            string face = Face(dream, false);
            Check(face.Contains(hbr ? "每300点" : "每3点"), "No Self in Dream formats only the selected numeric mode's block conversion");
            string lucky = Face(ClarityCard<MagicSwordLuckySevenHbrgoSaki>(), false);
            Check(lucky.Contains("不会被抽到") && lucky.Contains("自动打出"), "Lucky Seven states its template-only combat role in both numeric modes");
        }
    }

    static async Task TemplateDeckCases(Harmony harmony, List<MethodBase> patched)
    {
        TemplateReset();
        MethodInfo listeners = typeof(CombatState).GetMethod("IterateHookListeners", F)!;
        harmony.Patch(listeners, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(TemplateAllListeners), F)!) { priority = Priority.First + 100 });
        patched.Add(listeners);
        var template = ClarityCard<MagicSwordLuckySevenHbrgoSaki>(); template.UpgradeInternal(); template.FinalizeUpgradeInternal();
        var opening = ClarityCard<MagicSwordLuckySevenOpeningHbrgoSaki>();
        var strike = ClarityCard<StrikeIronclad>();
        Player.Deck.AddInternal(template); Player.Deck.AddInternal(opening); Player.Deck.AddInternal(strike);
        Type compat = Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeCombatApiCompatibility", true)!;
        Invoke(compat.GetMethod("PopulateCombatStateCompat", F)!, null, Player, RunState.Rng.Shuffle, Combat);
        Check(Player.PlayerCombatState!.AllCards.Count() == 1 && Player.PlayerCombatState.AllCards.Single().DeckVersion == strike,
            "real native initial deck cloning plus production setup filtering excludes both finisher templates");
        CardModel[] allClones = ((List<CardModel>)typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat)!).ToArray();
        CardModel[] templateClones = allClones.Where(card => card is MagicSwordLuckySevenHbrgoSaki or MagicSwordLuckySevenOpeningHbrgoSaki).ToArray();
        Check(templateClones.Length == 2 && templateClones.All(card => card.HasBeenRemovedFromState),
            "native initial-shuffle hooks leave no unmarked finisher template clones in the combat registry");
        Check(Player.Deck.Cards.Count == 3 && template.IsUpgraded && template.Pile == Player.Deck,
            "battle setup keeps the upgraded permanent template and complete permanent deck untouched");
        CardModel originalStrike = Player.PlayerCombatState.AllCards.Single();
        Type suspendedType = Helper("OverclockSuspendedHand");
        object?[] suspendArguments = { Player, null };
        Check((bool)Invoke(suspendedType.GetMethod("TrySuspend", F)!, null, suspendArguments)!,
            "OD/personal deck isolation accepts the live ordinary combat piles");
        object suspended = suspendArguments[1]!;
        CardModel[] borrowed = (CardModel[])Invoke(suspendedType.GetMethod("PrepareOverclockDeck", F)!, suspended)!;
        Check(borrowed.Length == 1 && borrowed[0].DeckVersion == strike && borrowed[0] != originalStrike,
            "the actual shared OD/personal deck preparation also excludes finisher templates while cloning an independent ordinary attack");
        Check((bool)Invoke(suspendedType.GetMethod("TryRestore", F)!, suspended)! &&
            Player.PlayerCombatState.AllCards.Single() == originalStrike && borrowed[0].HasBeenRemovedFromState,
            "finishing the borrowed deck restores the original attack identity and disposes its temporary clone");
        Check(!(bool)Invoke(AccessTools.PropertyGetter(typeof(MagicSwordLuckySevenHbrgoSaki), "IsPlayable"), template)! && !template.ShouldPlay(template, AutoPlayType.Default),
            "the permanent Lucky Seven template rejects manual play and unsolicited autoplay");
        foreach (bool initial in new[] { true, false })
        {
            var cards = new List<CardModel> { template, opening, strike };
            template.ModifyShuffleOrder(Player, cards, initial); opening.ModifyShuffleOrder(Player, cards, initial);
            Check(cards.SequenceEqual(new[] { strike }), "both initial and later shuffle lists exclude finisher templates");
        }
        foreach (bool initial in new[] { true, false })
        {
            var clone = (MagicSwordLuckySevenHbrgoSaki)Combat.CloneCard(template);
            Player.PlayerCombatState.DrawPile.AddInternal(clone, -1, true);
            var cards = new List<CardModel> { clone, strike };
            clone.ModifyShuffleOrder(Player, cards, initial);
            Check(clone.HasBeenRemovedFromState && clone.Pile == null && !cards.Contains(clone),
                "shuffle-time template filtering marks the combat clone removed rather than leaving an unmarked orphan");
        }
        foreach (CardModel retired in new CardModel[] { ClarityCard<ConsecutiveSlashHbrgoSaki>(), ClarityCard<LoliFormHbrgoSaki>(), template, opening })
            Check(!(bool)Call("MagicSwordCombatTemplateHelper", "CanGenerateFromDeck", retired)!, "random permanent-deck selectors exclude " + retired.GetType().Name);
        Check((bool)Call("MagicSwordCombatTemplateHelper", "CanGenerateFromDeck", strike)!, "random deck filters retain an ordinary attack");
        Type rewards = Mod.GetType("HBRGOSakiMod.Core.Patches.HBRGOSakiCardRewardPoolHelper", true)!;
        foreach (CardModel retired in new CardModel[] { ClarityCard<ConsecutiveSlashHbrgoSaki>(), ClarityCard<LoliFormHbrgoSaki>() })
        {
            Check(!(bool)Invoke(rewards.GetMethod("CanOfferCard", F)!, null, Player, retired)!,
                "retired " + retired.GetType().Name + " cannot return through an explicit shop candidate");
            Check(!(bool)Invoke(rewards.GetMethod("IsRewardCard", F)!, null, Player, retired, false)!,
                "retired " + retired.GetType().Name + " cannot return through an explicit reward candidate");
        }
        foreach (PileType type in new[] { PileType.Hand, PileType.Draw, PileType.Discard })
        {
            var bad = Combat.CloneCard(template); type.GetPile(Player).AddInternal(bad, -1, true);
            await bad.AfterCardChangedPiles(bad, PileType.None, null!);
            Check(bad.HasBeenRemovedFromState && bad.Pile == null, "a template routed into " + type + " is removed from combat without removing its permanent origin");
        }
        Powers(Hero).Clear();
        var finisher = (MagicSwordLuckySevenHbrgoSaki)Combat.CloneCard(template); finisher.DeckVersion = template;
        finisher.SetMagicSwordModeHitCount(1);
        Check(finisher.ShouldPlay(finisher, AutoPlayType.Default), "an explicitly configured upgraded finisher is eligible for scripted autoplay");
        await TemplatePlay(finisher);
        Check(finisher.IsUpgraded && Hero.GetPowerAmount<SwordheartPower>() == 1, "the upgraded permanent template's clone still awards its extra Swordheart on actual resolution");

        TemplateReset();
        MethodInfo move = typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Add" &&
            method.GetParameters() is var parameters && parameters.Length == 5 &&
            parameters[0].ParameterType == typeof(CardModel) && parameters[1].ParameterType == typeof(CardPile));
        MethodInfo draw = typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" &&
            method.GetParameters() is var parameters && parameters.Length == 2 && parameters[1].ParameterType == typeof(Player));
        harmony.Patch(move, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(TemplatePileTopLeaf), F)!)); patched.Add(move);
        harmony.Patch(draw, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(TemplateDrawOneLeaf), F)!)); patched.Add(draw);
        var retiredSlash = ClarityCard<ConsecutiveSlashHbrgoSaki>();
        var thunderGale = ClarityCard<MimicThunderGaleHbrgoSaki>();
        Player.PlayerCombatState!.DrawPile.AddInternal(retiredSlash, -1, true);
        Player.PlayerCombatState.DrawPile.AddInternal(thunderGale, -1, true);
        var synergy = Power<SynergyPower>(Hero, 1);
        object dual = Enum.Parse(Helper("ReptureCardMode"), "DualSwords");
        CardModel? selected = await (Task<CardModel?>)Call("ModeTransitionHelper", "DrawModeRestrictedCard",
            new ThrowingPlayerChoiceContext(), Player, dual, synergy)!;
        Check(selected == thunderGale && thunderGale.Pile == Player.PlayerCombatState.Hand &&
            retiredSlash.Pile == Player.PlayerCombatState.DrawPile,
            "Synergy's real mode selector skips a retired slash at the top and draws the available live dual-sword attack");
        selected = await (Task<CardModel?>)Call("ModeTransitionHelper", "DrawModeRestrictedCard",
            new ThrowingPlayerChoiceContext(), Player, dual, synergy)!;
        Check(selected == null && retiredSlash.Pile == Player.PlayerCombatState.DrawPile,
            "a mode-only draw with no live eligible attack returns no selection and keeps the old-save retired card intact");
    }
}
