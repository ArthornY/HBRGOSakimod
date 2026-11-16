using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

// Partial of the existing managed native host. Only the coalesced preview
// notification leaf is isolated here. All Remove/Apply/AfterApplied methods
// and Chihaya's real clear-old-mode -> apply-selected-mode method execute.
static partial class EffectChecks
{
    static readonly Type[] ChihayaSelectedModes =
        [typeof(ReptureBowPower), typeof(ReptureDualSwordsPower), typeof(RaptureScythePower)];

    static CardModel ChihayaChosenCard(Type mode) => mode == typeof(ReptureBowPower)
        ? ClarityCard<ReptureBowChoiceHbrgoSaki>()
        : mode == typeof(ReptureDualSwordsPower)
            ? ClarityCard<ReptureDualSwordsChoiceHbrgoSaki>()
            : ClarityCard<RaptureScytheChoiceHbrgoSaki>();

    static void ChihayaReset(bool hbr, Type? current = null)
    {
        FormReset(hbr, current);
        Check(FormModes.All(type => type == current || !Hero.Powers.Any(power => power.GetType() == type)),
            "Chihaya fixture begins with the other authored modes genuinely absent");
    }

    static int ChihayaSuppressionDepth() => (int)Helper("ModeTransitionHelper")
        .GetField("_suppressDualSwordsOnModeExitDepth", F)!.GetValue(null)!;

    static void ChihayaAssertMode(Type expected, string label)
    {
        Check(Hero.Powers.Count(power => FormModes.Contains(power.GetType())) == 1 &&
              Hero.Powers.Single(power => FormModes.Contains(power.GetType())).GetType() == expected,
            label + " leaves exactly the selected real mode and no obsolete mode");
        Check(ChihayaSuppressionDepth() == 0,
            label + " restores the mode-exit suppression scope after real async removals");
    }

    static async Task ChihayaRemovalCases()
    {
        foreach (Type type in FormModes.Concat(new[] { typeof(ChihayaClassmateHbrgoSaki),
                     typeof(ReptureBowChoiceHbrgoSaki), typeof(ReptureDualSwordsChoiceHbrgoSaki),
                     typeof(RaptureScytheChoiceHbrgoSaki) }))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var visual = new Harmony("hbr.power-removal-null.chihaya-preview-leaf");
        MethodInfo preview = Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!;
        visual.Patch(preview, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
        try
        {
            foreach (bool hbr in new[] { true, false })
            foreach (Type mode in ChihayaSelectedModes)
            {
                ChihayaReset(hbr);
                if (mode == typeof(RaptureScythePower)) Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki30A>());
                PowerModel power = FormPower(mode);
                string label = (hbr ? "HBR" : "legacy") + "/" + mode.Name + ".AfterApplied";
                Check(FormModes.Where(type => type != mode).All(type => !Hero.Powers.Any(power => power.GetType() == type)),
                    label + " calls native Remove on three absent powers, not fake stand-ins");
                await power.AfterApplied(Hero, null!);
                ChihayaAssertMode(mode, label);
                Check(ReferenceEquals(power.Owner, Hero) && Hero.Powers.Contains(power),
                    label + " retains its real owner and active power after null cleanups complete");
            }

            foreach (bool hbr in new[] { true, false })
            foreach (Type? previous in new Type?[] { null, typeof(NormalStatePower), typeof(ReptureBowPower),
                         typeof(ReptureDualSwordsPower), typeof(RaptureScythePower) })
            foreach (Type selected in ChihayaSelectedModes)
            {
                ChihayaReset(hbr, previous);
                if (selected == typeof(RaptureScythePower)) Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki30A>());
                var source = ClarityCard<ChihayaClassmateHbrgoSaki>();
                CardModel choice = ChihayaChosenCard(selected);
                PowerModel? old = previous == null ? null : Hero.Powers.Single(power => power.GetType() == previous);
                string label = (hbr ? "HBR" : "legacy") + "/Chihaya/" + (previous?.Name ?? "no-mode") + "->" + selected.Name;
                await (Task)Native(source, "EnterSelectedRepture", new ThrowingPlayerChoiceContext(), choice)!;
                ChihayaAssertMode(selected, label);
                PowerModel actual = Hero.Powers.Single(power => power.GetType() == selected);
                Check(actual.Amount == 1 && ReferenceEquals(actual.Owner, Hero),
                    label + " applies the selected one-layer power through real native PowerCmd.Apply");
                Check(old == null || !Hero.Powers.Contains(old),
                    label + " actually removes the prior power instance before new-mode application");
                Check(actual.GetType() == (choice is ReptureBowChoiceHbrgoSaki ? typeof(ReptureBowPower) :
                        choice is ReptureDualSwordsChoiceHbrgoSaki ? typeof(ReptureDualSwordsPower) : typeof(RaptureScythePower)),
                    label + " preserves the chosen option's authored mode semantics");
            }

            // Corrected user screenshot: this skill enters Dual Swords before
            // its own Combo/Draw body, so test that exact required-mode path.
            ChihayaReset(true);
            var activation = ClarityCard<MimicWillActivationHbrgoSaki>();
            activation.UpgradeInternal(); activation.FinalizeUpgradeInternal();
            object required = Call("ModeTransitionHelper", "GetOffModeTransition", activation)!;
            Check(activation.IsUpgraded && required.ToString() == "DualSwords",
                "reported MimicWillActivation+ uses the real restricted-card lookup and requires Dual Swords");
            bool transitioned = await (Task<bool>)Call("ModeTransitionHelper", "EnterRequiredModeBeforePlay",
                new ThrowingPlayerChoiceContext(), activation, required)!;
            Check(transitioned, "reported MimicWillActivation+ actual pre-play mode transition completes and returns true with old modes absent");
            ChihayaAssertMode(typeof(ReptureDualSwordsPower), "MimicWillActivation+ pre-play");
            var activeDual = Hero.GetPower<ReptureDualSwordsPower>();
            Check(activeDual is { Amount: 1 } && ReferenceEquals(activeDual.Owner, Hero),
                "reported MimicWillActivation+ activates its real one-layer owned Dual Swords through native Apply/AfterApplied");
        }
        finally
        {
            visual.Unpatch(preview, HarmonyPatchType.All, visual.Id);
            FormReset();
        }
    }

    // Only for the released-bug negative control. A real native Apply adds the
    // new power before calling its AfterApplied; the old null-removal prefix
    // then aborts before the prior weapon mode can be removed.
    static async Task ChihayaPartialApplyNegativeCases()
    {
        if (!RemovalNegative) throw new InvalidOperationException("Partial mode-apply reproduction is a negative-control case only.");
        foreach (bool bowToDual in new[] { true, false })
        {
            Type previous = bowToDual ? typeof(ReptureBowPower) : typeof(ReptureDualSwordsPower);
            Type next = bowToDual ? typeof(ReptureDualSwordsPower) : typeof(ReptureBowPower);
            ChihayaReset(true, previous);
            PowerModel old = Hero.Powers.Single(power => power.GetType() == previous);
            var source = ClarityCard<ChihayaClassmateHbrgoSaki>();
            Exception? failure = null;
            try
            {
                if (bowToDual)
                    await PowerCmd.Apply<ReptureDualSwordsPower>(new ThrowingPlayerChoiceContext(), Hero, 1m, Hero, source);
                else
                    await PowerCmd.Apply<ReptureBowPower>(new ThrowingPlayerChoiceContext(), Hero, 1m, Hero, source);
            }
            catch (Exception exception) { failure = exception; }
            string label = "released partial native Apply " + previous.Name + "->" + next.Name;
            Check(failure is NullReferenceException,
                label + " reproduces the original absent NormalState removal failure inside AfterApplied");
            Check(Hero.Powers.Contains(old) && Hero.Powers.Count(power => FormModes.Contains(power.GetType())) == 2 &&
                  Hero.Powers.Any(power => power.GetType() == next),
                label + " leaves both the old and already-added new real weapon powers after that exception");
            Check(Hero.Powers.Single(power => power.GetType() == next).Amount == 1 &&
                  ReferenceEquals(Hero.Powers.Single(power => power.GetType() == next).Owner, Hero),
                label + " proves native ApplyInternal completed before the failed cleanup continuation");
            Check(ChihayaSuppressionDepth() == 0,
                label + " still unwinds the suppression scope despite retaining the partially-applied modes");
        }
        FormReset();
    }
}
