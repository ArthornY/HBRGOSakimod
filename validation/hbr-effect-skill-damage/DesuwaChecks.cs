using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

// Full card body and native block commands run; only the raw die, absent Roll
// keyword registration, capability attachment and presentation/history leaves
// are fixture boundaries. The actual capability selection and modifiers run.
static partial class EffectChecks
{
    static RollGlossaryCapability? DesuwaPipeline;
    static int DesuwaListenerReads;
    static readonly List<decimal> DesuwaBlockEvents = [];
    sealed class DesuwaBlockObserver : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? card)
        {
            if (creature == Owner && card is DesuwaHbrgoSaki) DesuwaBlockEvents.Add(amount);
            return Task.CompletedTask;
        }
    }
    static bool DesuwaListeners(ref IEnumerable<AbstractModel> __result)
    {
        DesuwaListenerReads++;
        __result = Combat.Creatures.SelectMany(creature => creature.Powers).Cast<AbstractModel>()
            .Concat(Player.Relics).Concat(DesuwaPipeline == null ? [] : new AbstractModel[] { DesuwaPipeline }).ToArray();
        return false;
    }
    static bool DesuwaKeyword(CardModel card, ref bool __result)
    {
        if (!ReferenceEquals(card, DesuwaPipeline?.Owner)) return true;
        __result = true; return false;
    }
    static void DesuwaReset()
    {
        FormReset(); Fate(FateJson(bosses: 0)); SetNativeBlock(0);
        Set(RunState, "_players", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player });
        DesuwaPipeline = null; DesuwaListenerReads = 0; DesuwaBlockEvents.Clear(); UnifiedDice();
    }
    static RollGlossaryCapability DesuwaAttach(CardModel card)
    {
        var pipeline = RollPipeline(card);
        // The fixture does not execute mod initialization. Attach the same
        // capability directly to the real Ritsu collection, so OnPlay's normal
        // Capability<T>() lookup still selects this physical card's instance.
        ModelCapabilitySet set = card.Capabilities();
        ((IList<IModelCapability>)typeof(ModelCapabilitySet).GetField("_capabilities", F)!.GetValue(set)!).Add(pipeline);
        Check(ReferenceEquals(card.Capability<RollGlossaryCapability>(), pipeline),
            "native Ritsu capability lookup returns Desuwa's own attached roll pipeline");
        return DesuwaPipeline = pipeline;
    }
    static decimal DesuwaPreview(CardModel card, CardPlay play) => Hook.ModifyBlock(
        Combat, Hero, card.DynamicVars.Block.BaseValue, card.DynamicVars.Block.Props, card, play, out _);
    static string DesuwaFace(CardModel card, bool detail)
    {
        using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F)
            .Single(method => method.Name == "Begin" && method.GetParameters().Length == 4), null, card, detail, false, null)!;
        return card.GetDescriptionForPile(PileType.Hand, null);
    }
    static async Task DesuwaCases()
    {
        foreach (Type type in new[] { typeof(DesuwaHbrgoSaki), typeof(QuickThinkingHbrgoSaki),
                     typeof(MagicSwordModePower), typeof(MagicSwordSharingPower), typeof(ReptureBowPower), typeof(FocusedShotPower), typeof(StrengthPower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony(ProductionId + ".desuwa");
        var patched = new List<MethodBase>(); ExceptionDispatchInfo? failure = null;
        void Patch(MethodBase method, string prefix)
        {
            // The linked host already has a false-returning listener prefix at
            // Priority.First. This fixture's augmented list must execute first
            // so that the attached real roll capability participates in Hook.
            int priority = prefix == nameof(DesuwaListeners) ? Priority.First + 1 : Priority.First;
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!) { priority = priority });
            patched.Add(method);
        }
        try
        {
            Patch(typeof(CombatState).GetMethod("IterateHookListeners", F)!, nameof(DesuwaListeners));
            Patch(typeof(RollGlossaryCapability).GetMethod("HasEffectiveRollKeyword", F)!, nameof(DesuwaKeyword));
            Patch(typeof(RollGlossaryCapability).GetMethod("NextRawRoll", F)!, nameof(UnifiedFixedRaw));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (var method in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(method, nameof(VisualTask));
            foreach (var method in typeof(SfxCmd).GetMethods(F).Where(method => method.Name == "Play")) Patch(method, nameof(Skip));
            foreach (var method in typeof(VfxCmd).GetMethods(F).Where(method => method.Name == "PlayOnCreatureCenter")) Patch(method, nameof(Skip));
            Type history = typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory", true)!;
            Patch(history.GetMethod("BlockGained", F)!, nameof(Skip));
            Type descriptionPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!;
            MethodInfo getter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
            if (Harmony.GetPatchInfo(getter)?.Postfixes.Any(item => item.PatchMethod == descriptionPatch.GetMethod("Postfix", F)) != true)
            { harmony.CreateClassProcessor(descriptionPatch).Patch(); patched.Add(getter); }
            await DesuwaThresholdCases();
            await DesuwaModifiedRollCases();
            await DesuwaIndependentBlockCases();
            DesuwaRegistrationCases();
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase method in patched.Distinct())
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            DesuwaPipeline = null;
            try { CombatInProgress(false); As31O(false); Reset(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }
    static async Task DesuwaThresholdCases()
    {
        foreach (bool upgraded in new[] { false, true })
        foreach (int die in new[] { 1, 75, 76, 100 })
        {
            DesuwaReset(); var card = FormCard<DesuwaHbrgoSaki>(upgraded);
            var pipeline = DesuwaAttach(card); var play = Play(card);
            Power<DesuwaBlockObserver>(Hero, 1);
            decimal percent = upgraded ? 177m : 133m, basis = upgraded ? 885m : 665m;
            Equal((decimal)Call("HbrSkillPower", "BlockPercent", card.DynamicVars.Block.BaseValue)!, percent,
                "Desuwa keeps its exact authored block coefficient at upgrade=" + upgraded);
            Equal(DesuwaPreview(card, play), basis, "INT100 preview before dice is exactly the authored base block");
            Check(card.Type == CardType.Skill && card.Rarity == CardRarity.Common && card.EnergyCost.Canonical == 1 &&
                card.TargetType == TargetType.Self && card.GainsBlock, "Desuwa is an ordinary one-energy self-block skill");
            Check((bool)StaticCall(RollRules, "IsRollEnabled", card)!, "Desuwa is a registered Roll-enabled card");
            string face = DesuwaFace(card, false), detail = DesuwaFace(card, true);
            Check(face.Contains(basis.ToString("0")) && !face.Contains("倍率") && !face.Contains("（"),
                "Desuwa's face shows calculated block without coefficient text");
            Check(detail.Contains("（" + percent + "%）") && detail.Contains("大于75"),
                "inspect adds the exact parenthesized coefficient and strict final-roll condition");
            UnifiedDice(die);
            Equal(pipeline.EnsureCurrentRollFor(card), die, "the normal card roll pipeline lands the supplied raw die");
            decimal preview = DesuwaPreview(card, play);
            decimal tier = die switch { 75 => 1.15m, 76 => 1.2m, 100 => 1.5m, _ => 1m };
            Equal(preview, decimal.Floor(basis * tier), "native block preview includes the actual current-die value multiplier");
            Check(DesuwaListenerReads > 0, "native Hook enumerates this fixture's augmented capability listener list");
            int reads = UnifiedRawReads;
            await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
            int count = die > 75 ? 2 : 1;
            Check(DesuwaBlockEvents.Count == count && DesuwaBlockEvents.All(amount => amount == preview),
                "final roll " + die + " produces " + count + " independent native AfterBlockGained callbacks matching per-gain preview");
            Equal(Hero.Block, preview * count, "actual native block writer grants each independently calculated block amount");
            Check(reads == 1 && UnifiedRawReads == reads && UnifiedRawDice.Count == 0,
                "the conditional repeat reuses the current roll and requests no second die");
            Check(DesuwaFace(card, false).Contains(preview.ToString("0")),
                "current-die card preview shows the same individual block that native GainBlock grants");
            Equal(card.DynamicVars.Block.BaseValue, upgraded ? 8.85m : 6.65m,
                "previews and two native gains never overwrite authored native units with resolved block");
        }
    }
    static async Task DesuwaModifiedRollCases()
    {
        foreach (bool magic in new[] { false, true })
        {
            DesuwaReset(); var card = FormCard<DesuwaHbrgoSaki>(); var pipeline = DesuwaAttach(card);
            Power<DesuwaBlockObserver>(Hero, 1);
            int raw;
            if (magic)
            {
                // Self magic's +77 is attack-only in the existing rules. Its
                // teammate sharing effect also applies to skills, so exercise
                // the supported +77 path without widening magic's eligibility.
                var teammate = SupportTeammate();
                Set(Combat, "_allies", new List<Creature> { Hero, teammate.Creature });
                Set(RunState, "_players", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player, teammate });
                Power<MagicSwordModePower>(teammate.Creature, 1);
                Power<MagicSwordSharingPower>(teammate.Creature, 77);
                raw = 1;
            }
            else { await UnifiedApply(false, 100, 2); raw = 40; }
            UnifiedDice(raw); var play = Play(card);
            int expected = magic ? 78 : 80;
            Equal(pipeline.EnsureCurrentRollFor(card), expected,
                "the threshold uses " + (magic ? "the teammate Magic Sword sharing plus77" : "the percentage roll bonus") + " in the final result");
            Check(pipeline.GetCurrentRollDetailsFor(card).BaseRoll == raw && raw <= 75,
                "the modified result retains a low raw die, so checking raw instead of final would fail");
            decimal preview = DesuwaPreview(card, play);
            await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
            Check(DesuwaBlockEvents.Count == 2 && DesuwaBlockEvents.All(amount => amount == preview) && UnifiedRawReads == 1,
                "a low raw die crossing75 after modifiers grants two separately modified blocks without rerolling");
            Equal(Hero.Block, preview * 2m, "modified-roll actual block equals two current-die previews");
        }
    }
    static async Task DesuwaIndependentBlockCases()
    {
        DesuwaReset(); var card = FormCard<DesuwaHbrgoSaki>(true); var pipeline = DesuwaAttach(card);
        Power<DesuwaBlockObserver>(Hero, 1); Power<ReptureBowPower>(Hero, 1); Power<FocusedShotPower>(Hero, 1);
        UnifiedDice(76); pipeline.EnsureCurrentRollFor(card); var play = Play(card);
        decimal preview = DesuwaPreview(card, play);
        await (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
        Equal(preview, 796m, "the shared preview includes both current die and Focused Shot's effective block modifier");
        Equal(Hero.Block, 1592m, "two independently floored native block gains keep their per-gain rounding");
        Equal(Hero.GetPowerAmount<StrengthPower>(), 32m,
            "real Focused Shot reacts separately to two796 gains (16 plus16), rather than treating combined block as one33-layer trigger");
        Check(DesuwaBlockEvents.SequenceEqual(new[] { 796m, 796m }),
            "each actual gain reaches native block listeners independently after effective modifiers");
    }
    static void DesuwaRegistrationCases()
    {
        Type content = Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack", true)!;
        var retired = (IReadOnlySet<Type>)content.GetField("RetiredCardTypes", F)!.GetValue(null)!;
        var types = (Type[])content.GetField("CardTypes", F)!.GetValue(null)!;
        Check(retired.Contains(typeof(QuickThinkingHbrgoSaki)) && !retired.Contains(typeof(DesuwaHbrgoSaki)),
            "Quick Thinking uses the existing retired/deprecated registration path while Desuwa remains offered");
        Check(types.Contains(typeof(QuickThinkingHbrgoSaki)) && types.Contains(typeof(DesuwaHbrgoSaki)),
            "both classes remain source-registered, retaining the old model for saved-card loading");
        Check(SupportTypePredicate("HbrgoSaki31OCardFilter", "IsExcluded", typeof(QuickThinkingHbrgoSaki)) &&
            !SupportTypePredicate("HbrgoSaki31OCardFilter", "IsThirtyOneOExclusive", typeof(QuickThinkingHbrgoSaki)),
            "retired Quick Thinking is excluded from31O selection and removed from the exclusive list");
        Check(SupportTypePredicate("HbrgoSaki31OCardFilter", "IsThirtyOneOExclusive", typeof(DesuwaHbrgoSaki)) &&
            !SupportTypePredicate("HbrgoSaki31OCardFilter", "IsExcluded", typeof(DesuwaHbrgoSaki)) &&
            !SupportTypePredicate("HbrgoSaki31OCardFilter", "IsThirtyAAllowed", typeof(DesuwaHbrgoSaki)),
            "Desuwa is available in31O and absent from30A selection");
        var old = (QuickThinkingHbrgoSaki)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(QuickThinkingHbrgoSaki))).MutableClone();
        Check(old.Id.Entry == "QUICK_THINKING_HBRGO_SAKI" && old.DynamicVars.Cards.BaseValue == 2m,
            "the old canonical model ID resolves and native mutable cloning preserves its original draw-two effect");
        old.UpgradeInternal(); old.FinalizeUpgradeInternal();
        Equal(old.DynamicVars.Cards.BaseValue, 3m, "a saved Quick Thinking can still upgrade by its unchanged legacy rule");
        var card = ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(DesuwaHbrgoSaki)));
        Check(card.Title == "desuwa" && card.PortraitPath.EndsWith("memory_corridor_hbrgo_saki.png", StringComparison.Ordinal) &&
            card.BetaPortraitPath == card.PortraitPath, "Desuwa keeps the user's lowercase title and authorized default portrait on both profiles");
        SupportPoolCases();
    }
}
