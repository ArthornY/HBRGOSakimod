using HBRGOSakiMod.Core.Nodes.Combat;
using HBRGOSakiMod.Core.Nodes.Cards;
using HBRGOSakiMod.Core.Nodes.Screens;
using HBRGOSakiMod.Core.Patches;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Cards;
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
    internal const string CardDamageHistoryWidgetAttachmentId = "card_damage_history_widget";
    internal const string DeflectorProtectionDpBarAttachmentId =
        "deflector_protection_dp_bar";
    internal const string RedCrimsonActionBarAttachmentId =
        "red_crimson_action_bar";
    internal const string ChihayaNumberKeyListenerAttachmentId =
        "chihaya_number_key_listener";
    internal const string GameOverObserverAttachmentId =
        "game_over_observer";
    internal const string StrikePortraitAttachmentId = "strike_portrait";
    internal const string CardDescriptionScrollAttachmentId = "card_description_scroll";
    internal const string PhantomRevivalIntentsAttachmentId = "phantom_revival_intents";
    internal const string PhantomCombatLayoutAttachmentId = "phantom_combat_layout";
    internal const string PhantomMinionPlacementAttachmentId = "phantom_minion_placement";
    internal const string BattleGaugeResizeInputAttachmentId = "battle_gauge_resize_input";
    internal const string CardSelectionPortraitAttachmentId = "card_selection_portrait";
    internal const string CardStyleAttachmentId = "hbr_card_style";
    internal const string MagicSwordButtonAttachmentId = "magic_sword_button";
    internal const string MachinaButtonAttachmentId = "thirty_one_c_machina_button";

    private const string ModId = "HBRGOSakiMod";

    internal static ModNodeAttachmentRegistry Registry { get; } =
        ModNodeAttachmentRegistry.For(ModId);

    internal static void Register()
    {
        Registry.RegisterReadyChild<NCombatRoom, NHbrBattleGaugeResizeInput>(
            BattleGaugeResizeInputAttachmentId,
            _ => new NHbrBattleGaugeResizeInput(),
            CreateOptions("HbrBattleGaugeResizeInput", order: 3));

        Registry.RegisterReadyChild<NCombatRoom, NPhantomMinionPlacement>(
            PhantomMinionPlacementAttachmentId,
            _ => new NPhantomMinionPlacement(),
            CreateOptions("PhantomMinionPlacement", order: 2));

        Registry.RegisterReadyChild<NCombatRoom, NPhantomCombatLayout>(
            PhantomCombatLayoutAttachmentId,
            _ => new NPhantomCombatLayout(),
            CreateOptions("PhantomCombatLayout", order: 1));

        Registry.RegisterReadyChild<NCombatRoom, NPhantomRevivalIntents>(
            PhantomRevivalIntentsAttachmentId,
            _ => new NPhantomRevivalIntents(),
            CreateOptions("PhantomRevivalIntents", order: 0));

        Registry.RegisterReadyChild<NCard, NHbrgoStrikePortrait>(
            StrikePortraitAttachmentId,
            _ => new NHbrgoStrikePortrait(),
            (card, portrait) => portrait.Card = card,
            CreateOptions("HbrgoStrikePortrait", order: 0));

        Registry.RegisterReadyChild<NCard, NHbrCardDescriptionScroll>(
            CardDescriptionScrollAttachmentId,
            _ => new NHbrCardDescriptionScroll(),
            (card, scroll) => scroll.Card = card,
            CreateOptions("HbrCardDescriptionScroll", order: 2));

        Registry.RegisterReadyChild<NCard, NHbrCardStyle>(
            CardStyleAttachmentId,
            _ => new NHbrCardStyle(),
            (card, style) => style.Card = card,
            CreateOptions("HbrCardStyle", order: 3));

        Registry.RegisterReadyChild<NCombatUi, NThirtyOneACutinOverlay>(
            "thirty_one_a_cutin",
            _ => new NThirtyOneACutinOverlay(),
            CreateOptions("ThirtyOneACutinOverlay", order: 6));

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

        Registry.RegisterReadyChild<NCombatUi, NCardDamageHistoryWidget>(
            CardDamageHistoryWidgetAttachmentId,
            _ => new NCardDamageHistoryWidget(),
            CreateOptions("HbrgoSakiCardDamageHistoryWidget", order: 3));

        Registry.RegisterReadyChild<NCombatUi, NSakiCardSelectionPortrait>(
            CardSelectionPortraitAttachmentId,
            _ => new NSakiCardSelectionPortrait(),
            CreateOptions(NSakiCardSelectionPortrait.WidgetName, order: 4));

        // 31O's magic sword switch; it hides itself for every other character.
        Registry.RegisterReadyChild<NCombatUi, NHbrMagicSwordButton>(
            MagicSwordButtonAttachmentId,
            _ => new NHbrMagicSwordButton(),
            CreateOptions(NHbrMagicSwordButton.WidgetName, order: 5));

        Registry.RegisterReadyChild<NCombatUi, NHbrMachinaButton>(
            MachinaButtonAttachmentId,
            _ => new NHbrMachinaButton(),
            CreateOptions(NHbrMachinaButton.WidgetName, order: 7));

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
