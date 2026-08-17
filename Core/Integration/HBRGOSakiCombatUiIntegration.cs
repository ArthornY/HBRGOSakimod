using HBRGOSakiMod.Core.Nodes.Combat;
using HBRGOSakiMod.Core.Nodes.Screens;
using HBRGOSakiMod.Core.Patches;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiCombatUiIntegration
{
    internal const string GaugeAttachmentId = "overclock_gauge";
    internal const string ExtraTurnOverlayAttachmentId = "overclock_extra_turn_overlay";
    internal const string RollHistoryWidgetAttachmentId =
        "roll_history_widget";
    internal const string DeflectorProtectionDpBarAttachmentId =
        "deflector_protection_dp_bar";
    internal const string RedCrimsonActionBarAttachmentId =
        "red_crimson_action_bar";
    internal const string ChihayaNumberKeyListenerAttachmentId =
        "chihaya_number_key_listener";
    internal const string GameOverObserverAttachmentId =
        "game_over_observer";

    private const string ModId = "HBRGOSakiMod";

    internal static ModNodeAttachmentRegistry Registry { get; } =
        ModNodeAttachmentRegistry.For(ModId);

    internal static void Register()
    {
        Registry.RegisterReadyChild<NCombatUi, NOverclockGauge>(
            GaugeAttachmentId,
            _ => new NOverclockGauge(),
            CreateOptions("HbrgoSakiOverclockGauge", order: 0));

        Registry.RegisterReadyChild<NCombatUi, NOverclockExtraTurnOverlay>(
            ExtraTurnOverlayAttachmentId,
            _ => new NOverclockExtraTurnOverlay(),
            CreateOptions("HbrgoSakiOverclockExtraTurnOverlay", order: 1));

        Registry.RegisterReadyChild<NCombatUi, NRollHistoryWidget>(
            RollHistoryWidgetAttachmentId,
            _ => new NRollHistoryWidget(),
            CreateOptions("HbrgoSakiRollHistoryWidget", order: 2));

        Registry.RegisterReadyChild<NHealthBar, NDeflectorProtectionDpBar>(
            DeflectorProtectionDpBarAttachmentId,
            _ => new NDeflectorProtectionDpBar(),
            CreateOptions("DeflectorProtectionDpBar", order: 0));

        Registry.RegisterReadyChild<NHealthBar, NRedCrimsonActionBar>(
            RedCrimsonActionBarAttachmentId,
            _ => new NRedCrimsonActionBar(),
            CreateOptions("RedCrimsonActionBar", order: 1));

        Registry.RegisterReadyChild<
            NChooseACardSelectionScreen,
            ChihayaChooseCardNumberKeyListener>(
            ChihayaNumberKeyListenerAttachmentId,
            _ => new ChihayaChooseCardNumberKeyListener(),
            (screen, listener) => listener.Screen = screen,
            CreateOptions(
                "HBRGOChihayaChooseCardNumberKeyListener",
                order: 0));

        Registry.RegisterReadyChild<NGameOverScreen, NHbrgoSakiGameOverObserver>(
            GameOverObserverAttachmentId,
            _ => new NHbrgoSakiGameOverObserver(),
            (screen, observer) => observer.Screen = screen,
            CreateOptions("HbrgoSakiGameOverObserver", order: 0));
    }

    private static NodeAttachmentOptions CreateOptions(string name, int order)
    {
        return new NodeAttachmentOptions
        {
            Name = name,
            Order = order,
            DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            AddMode = NodeAttachmentAddMode.AddChildSafely
        };
    }
}
