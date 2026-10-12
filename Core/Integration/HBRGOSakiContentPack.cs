using System;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Acts;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Potions;
using HBRGOSakiMod.Core.Models.RelicPools;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Cards.HandOutline;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiContentPack
{
    private const string ModId = "HBRGOSakiMod";
    private static readonly Color SwordheartSupplementOutlineColor = new(1f, 0.86f, 0.32f, 0.98f);

    private static readonly Type[] CardTypes =
    {
        typeof(StrikeHbrgoSaki),
        typeof(DefendHbrgoSaki),
        typeof(SwallowReversalHbrgoSaki),
        typeof(SlideStepHbrgoSaki),
        typeof(DrinkTeaHbrgoSaki),
        typeof(StrengthenHbrgoSaki),
        typeof(QuickSwitchHbrgoSaki),
        typeof(ExtremeDodgeHbrgoSaki),
        typeof(SoulIntimidationHbrgoSaki),
        typeof(RestTimeHbrgoSaki),
        typeof(CollectSoulHbrgoSaki),
        typeof(SoulBufferHbrgoSaki),
        typeof(DoubleArmorBreakHbrgoSaki),
        typeof(CoveringFireHbrgoSaki),
        typeof(RapidChargeHbrgoSaki),
        typeof(RedCrimsonExtraStrikeHbrgoSaki),
        typeof(MemoryCorridorHbrgoSaki),
        typeof(OverdrawMovementHbrgoSaki),
        typeof(MatchaParfaitHbrgoSaki),
        typeof(PerTurnChargeHbrgoSaki),
        typeof(FocusedShotHbrgoSaki),
        typeof(PowerReleaseHbrgoSaki),
        typeof(MagicSwordSharingHbrgoSaki),
        typeof(TeleportHbrgoSaki),
        typeof(YoHbrgoSaki),
        typeof(MimicExorcismArrowHbrgoSaki),
        typeof(NextTurnEnergyStrikeHbrgoSaki),
        typeof(RollBoostStrikeHbrgoSaki),
        typeof(MimicThunderGaleHbrgoSaki),
        typeof(FragileDoubleStrikeHbrgoSaki),
        typeof(HangingShotHbrgoSaki),
        typeof(PiercingShotHbrgoSaki),
        typeof(SoulHarvestHbrgoSaki),
        typeof(SoulExplosionHbrgoSaki),
        typeof(MimicCursedScytheHbrgoSaki),
        typeof(ExtraGaugeStrikeHbrgoSaki),
        typeof(BladeDanceHbrgoSaki),
        typeof(StrongShotHbrgoSaki),
        typeof(MimicSaviorStrikeHbrgoSaki),
        typeof(SelfSacrificeSlashHbrgoSaki),
        typeof(MimicMaidCultivationHbrgoSaki),
        typeof(MimicFullBodyShotHbrgoSaki),
        typeof(MimicReminiscenceHbrgoSaki),
        typeof(QuickThinkingHbrgoSaki),
        typeof(SwitchingSlashHbrgoSaki),
        typeof(ConsecutiveSlashHbrgoSaki),
        typeof(AmnesiaHbrgoSaki),
        typeof(OverworkHbrgoSaki),
        typeof(ArmorBreakSoulStrikeHbrgoSaki),
        typeof(SoulFinisherHbrgoSaki),
        typeof(DpSoulStrikeHbrgoSaki),
        typeof(MimicEvilEyeTidalSlashHbrgoSaki),
        typeof(RuinStrikeHbrgoSaki),
        typeof(ResistanceBulwarkHbrgoSaki),
        typeof(BreakInsightHbrgoSaki),
        typeof(SenseShatterHbrgoSaki),
        typeof(BeyondProtectionHbrgoSaki),
        typeof(MagicSwordUniversalityHbrgoSaki),
        typeof(ChargeStrikeHbrgoSaki),
        typeof(ChargedShotHbrgoSaki),
        typeof(DestinedArrowHbrgoSaki),
        typeof(SniperModeHbrgoSaki),
        typeof(SynergyHbrgoSaki),
        typeof(GodspeedHbrgoSaki),
        typeof(WithMeHbrgoSaki),
        typeof(LiveHbrgoSaki),
        typeof(TrainingTimeHbrgoSaki),
        typeof(HasteHbrgoSaki),
        typeof(FateHbrgoSaki),
        typeof(NotAsItShouldBeHbrgoSaki),
        typeof(HerSongIsMyScreamHbrgoSaki),
        typeof(HalfBodyHbrgoSaki),
        typeof(MyLegendBeginsHbrgoSaki),
        typeof(HelloWorldHbrgoSaki),
        typeof(SaviorGrandEntranceHbrgoSaki),
        typeof(WorldsFinestHbrgoSaki),
        typeof(MoreThanOneTruthHbrgoSaki),
        typeof(CrushYouLikeABugHbrgoSaki),
        typeof(SummerGrassDreamHbrgoSaki),
        typeof(BoostUpHbrgoSaki),
        typeof(MimicDreamBubbleHbrgoSaki),
        typeof(EhehePeroroHbrgoSaki),
        typeof(NoSelfInDreamHbrgoSaki),
        typeof(EatPassersbyHbrgoSaki),
        typeof(AccelerationHbrgoSaki),
        typeof(CommunicationMeetingHbrgoSaki),
        typeof(AnonAidHbrgoSaki),
        typeof(SwordheartClarityHbrgoSaki),
        typeof(CrimsonBladeFlowHbrgoSaki),
        typeof(SoyoAidHbrgoSaki),
        typeof(IOnlyKnowWhatIKnowHbrgoSaki),
        typeof(AllRounderHbrgoSaki),
        typeof(LoliFormHbrgoSaki),
        typeof(FatePointsHbrgoSaki),
        typeof(ChargeBurstHbrgoSaki),
        typeof(ReapHbrgoSaki),
        typeof(RemoveDpHbrgoSaki),
        typeof(LastStandHbrgoSaki),
        typeof(MimicSpecialMealHbrgoSaki),
        typeof(DamageTransferHbrgoSaki),
        typeof(DoublePowersHbrgoSaki),
        typeof(RecallHbrgoSaki),
        typeof(EnergyDrainHbrgoSaki),
        typeof(MeditationHbrgoSaki),
        typeof(OverclockUpgradeHbrgoSaki),
        typeof(SwiftHbrgoSaki),
        typeof(ProtagonistPrivilegeHbrgoSaki),
        typeof(DualSwordsEnhancementHbrgoSaki),
        typeof(MimicIceFlowerThousandArrowsHbrgoSaki),
        typeof(MimicMoonlightHbrgoSaki),
        typeof(ImitateHbrgoSaki),
        typeof(SakuraRainHbrgoSaki),
        typeof(MagicSwordEnhancementHbrgoSaki),
        typeof(FragileHbrgoSaki),
        typeof(LostStarSongHbrgoSaki),
        typeof(MagicSwordLuckySevenOpeningHbrgoSaki),
        typeof(UnfetteredHbrgoSaki),
        typeof(MagicSwordLuckySevenHbrgoSaki),
        typeof(MagicSwordModeHbrgoSaki),
        typeof(ChihayaClassmateHbrgoSaki),
        typeof(AnokoHbrgoSaki),
        typeof(ReptureBowChoiceHbrgoSaki),
        typeof(ReptureDualSwordsChoiceHbrgoSaki),
        typeof(RaptureScytheChoiceHbrgoSaki),
        typeof(FeintHbrgoSaki),
        typeof(HighSongHbrgoSaki),
        typeof(ShadowDanceSongHbrgoSaki),
        typeof(BlueSkySongHbrgoSaki),
        typeof(LostDaysSongHbrgoSaki),
        typeof(StrengthAmplificationHbrgoSaki),
        typeof(CommandStrikeHbrgoSaki),
        typeof(ObsessiveHbrgoSaki),
        typeof(ReturnToMeHbrgoSaki),
        typeof(AlreadyWeightlessHbrgoSaki),
        typeof(EmbraceHbrgoSaki),
        typeof(BindingRedemptionHbrgoSaki),
        typeof(AsIfAtoningHbrgoSaki),
        typeof(JoyfulCaptiveHbrgoSaki),
        typeof(ReadyToGoHbrgoSaki),
        typeof(DeliberationHbrgoSaki),
        typeof(SoulImpactHbrgoSaki),
        typeof(ChargeUpSlashHbrgoSaki),
        typeof(SacrificialAttackHbrgoSaki),
        typeof(ProofThroughPainHbrgoSaki),
        typeof(TogetherAtTheEndHbrgoSaki),
        typeof(MemoryRefluxHbrgoSaki),
        typeof(AwaitingEchoHbrgoSaki),
        typeof(EmberRecoveryHbrgoSaki),
        typeof(EchoSlashHbrgoSaki),
        typeof(StandAloneHbrgoSaki),
        typeof(ShelterTheAfterimageHbrgoSaki),
        typeof(PainTransmissionHbrgoSaki),
        typeof(StandInFrontHbrgoSaki),
        typeof(ActInYourSteadHbrgoSaki),
        typeof(ArcSweepHbrgoSaki),
        typeof(LoneAssaultHbrgoSaki),
        typeof(GatherWarmthHbrgoSaki),
        typeof(PressOnThroughPainHbrgoSaki),
        typeof(ChargeInReserveHbrgoSaki),
        typeof(PierceTheSilenceHbrgoSaki),
        typeof(BorrowedEdgeHbrgoSaki),
        typeof(RefuseToLeaveHbrgoSaki),
        typeof(SilentOfferingHbrgoSaki),
        typeof(AwaitingReturnHbrgoSaki),
        typeof(InMyNameHbrgoSaki),
        typeof(UnbrokenRemnantHbrgoSaki),
        typeof(BorrowLifeTogetherHbrgoSaki),
        typeof(ScarLedgerHbrgoSaki),
        typeof(BuryYesterdayHbrgoSaki),
        typeof(DefyFateStrikeHbrgoSaki),
        typeof(EnduringAfterimageHbrgoSaki),
        typeof(DevourRemainingLifeHbrgoSaki),
        typeof(PainResonanceHbrgoSaki),
        typeof(LetMeBearItHbrgoSaki),
        typeof(MadlyInfatuatedHbrgoSaki),
        typeof(ShakeTheAbyssHbrgoSaki),
        typeof(ScorchingImpatienceHbrgoSaki),
        typeof(CoordinatedAttackHbrgoSaki),
        typeof(SweepHbrgoSaki),
        typeof(PuttingOnAirsHbrgoSaki),
        typeof(SoundOfSilenceHbrgoSaki),
        typeof(EntrustToMeHbrgoSaki),
        typeof(CastEverythingAsideHbrgoSaki),
        typeof(HowRidiculousHbrgoSaki),
        typeof(MercyAndCompassionHbrgoSaki),
        typeof(AtTheBeginningHbrgoSaki),
        typeof(FallingContinuallyHbrgoSaki),
        typeof(HolyOneHbrgoSaki),
        typeof(RestlessHbrgoSaki),
        typeof(VisitingPainHbrgoSaki),
        typeof(RapidChargingHbrgoSaki),
        typeof(NeverEndingHbrgoSaki),
        typeof(TranscendSelfHbrgoSaki),
        typeof(LingeringOnHbrgoSaki),
        typeof(FamiliarityHbrgoSaki),
        typeof(ImmaculateDressHbrgoSaki),
        typeof(PainOfHellfireHbrgoSaki),
        typeof(UglyEndingHbrgoSaki),
        typeof(TowardTheEndHbrgoSaki),
        typeof(FinalSorrowHbrgoSaki),
        typeof(KillKissHbrgoSaki),
        typeof(DeathToYouHbrgoSaki),
        typeof(WorldThatWillComeHbrgoSaki),
        typeof(DevoutPrayerHbrgoSaki),
        typeof(PuzzlePieceHbrgoSaki),
        typeof(AllThingsInNatureHbrgoSaki),
        typeof(PitchBlackAllureHbrgoSaki),
        typeof(OverflowingSorrowHbrgoSaki),
        typeof(OpenDestinyHbrgoSaki),
        typeof(GluttonyHbrgoSaki),
        typeof(NowIsTheTimeToReignHbrgoSaki),
        typeof(ShadowOfDeathHbrgoSaki),
        typeof(DistantPrayerHbrgoSaki),
        typeof(EcstaticCorridorHbrgoSaki),
        typeof(PhantomChoiceHbrgoSaki),
        typeof(PhantomFusionEffectChoiceHbrgoSaki),
        typeof(PhantomKnowledgeChoiceHbrgoSaki)
    };

    private static readonly Type[] HotaruCardTypes =
    {
        typeof(HotaruStrike),
        typeof(HotaruDefend),
        typeof(HotaruStimulate),
        typeof(HotaruCharge),
        typeof(HotaruEcho),
        typeof(HotaruSalvo),
        typeof(HotaruOvercharge)
    };

    // Permanent choices unlocked by a level-seven cannon. They are loadable
    // and localized like other cards, but never enter ordinary reward pools.
    private static readonly Type[] HotaruDerivedCardTypes =
    {
        typeof(HotaruSacrifice),
        typeof(HotaruRedemption)
    };

    private static readonly Type[] RewardRelicTypes =
    {
        typeof(ElectronicSoldierManual),
        typeof(Ketsu),
        typeof(SoulOfAnchor),
        typeof(DeflectorProtection),
        typeof(Overclock),
        typeof(RestorationMoment),
        typeof(GuideLittleHand),
        typeof(FeintLikeWind),
        typeof(FleetingClouds),
        typeof(ShieldLock),
        typeof(ElectronicSoldierManual31O),
        typeof(MomentOfReign),
        typeof(BecomeGodHbrgoSaki),
        typeof(HotaruElectronicSoldierManual),
        typeof(Millsage),
        typeof(HotaruIridescence)
    };

    private static readonly Type[] InternalRelicTypes =
    {
        typeof(FabricatedSoulOfAnchor),
        typeof(ThirtyACollection),
        typeof(ThirtyOneACollection),
        typeof(CryChicCollection)
    };

    private static readonly Type[] PotionTypes =
    {
        typeof(DpPotionHbrgoSaki),
        typeof(FragilePotionHbrgoSaki),
        typeof(SoulPotionHbrgoSaki)
    };

    internal static bool IsRegisteredCardType(Type cardType)
    {
        return cardType != null &&
            (CardTypes.Contains(cardType) || HotaruCardTypes.Contains(cardType) ||
                HotaruDerivedCardTypes.Contains(cardType));
    }

    internal static void Register()
    {
        var publicEntries = CardTypes
            .Concat(HotaruCardTypes)
            .Concat(HotaruDerivedCardTypes)
            .Concat(RewardRelicTypes)
            .Concat(PotionTypes)
            .Concat(InternalRelicTypes)
            .Distinct()
            .ToDictionary(type => type, type => ModelDb.GetId(type).Entry);

        ModCardHandOutlineRegistry.Register(
            typeof(CardModel),
            ModCardHandOutlineRules.Fixed(
                MagicSwordEnergyPaymentHelper.CanPlayUsingSwordheart,
                SwordheartSupplementOutlineColor,
                visibleWhenUnplayable: true));

        RitsuLibFramework.CreateContentPack(ModId)
            .CardKeywordOwnedByLocNamespace(
                "ROLL",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .CardKeywordOwnedByLocNamespace(
                "CRITICAL_SUCCESS",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .CardKeywordOwnedByLocNamespace(
                "ABSOLUTE_CRITICAL_SUCCESS",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .CardKeywordOwnedByLocNamespace(
                "30A",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .CardKeywordOwnedByLocNamespace(
                "31A",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .CardKeywordOwnedByLocNamespace(
                "HEADQUARTERS",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .CardKeywordOwnedByLocNamespace(
                "CRYCHIC",
                null,
                ModKeywordCardDescriptionPlacement.None,
                includeInCardHoverTip: false)
            .SharedEvent<JoiningCeremonyHbrgoSaki>()
            .SharedEvent<DoorAjarHbrgoSaki>()
            .ActAncient<HbrgoActFour, ChihayaAnonTogawaSakiko>()
            .Custom(context =>
            {
                context.Content.RegisterCharacter(typeof(HbrgoHotaru));
                context.Content.RegisterMonster(typeof(HotaruHappinessCannon));
                context.Content.RegisterMonster(typeof(HotaruCoffinCannon));
                context.Content.RegisterMonster(typeof(HotaruContestCannon));
                context.Content.RegisterMonster(typeof(HotaruLongingCannon));
                context.Content.RegisterMonster(typeof(HotaruAbsolutionCannon));
                context.Content.RegisterMonster(typeof(HotaruFinalCannon));

                context.Content.RegisterCardLibraryCompendiumSharedPoolFilter<HBRGOSakiMemoryGardenCardPool>(
                    "memory_garden", HbrMemoryGardenIconHelper.IconPath);

                // Ritsu registration also establishes model ownership, localization
                // and presentation assets. Memory stories need that registration too;
                // HbrMemoryFragmentLegacyQueuePatch removes them before ordinary draws.
                foreach (Type storyType in HbrMemoryFragmentPool.StoryTypes)
                {
                    context.Content.RegisterSharedEvent(storyType);
                }

                context.Content.RegisterModelCapability<
                    RollGlossaryCapability>();
                context.Content.RegisterModelCapability<
                    ThirtyAKeywordCapability>();
                context.Content.RegisterModelCapability<ThirtyOneAKeywordCapability>();
                context.Content.RegisterModelCapability<HalfBodyAffiliationCapability>();
                context.Content.RegisterModelCapability<SixSenseCardCapability>();
                context.Content.ConfigureDefaultModelCapabilities<CardModel>(
                    "six_sense_attribute",
                    static (_, capabilities) => capabilities.Add<SixSenseCardCapability>());
                context.Content.ConfigureDefaultModelCapabilities<CardModel>(
                    "roll_glossary",
                    static (_, capabilities) =>
                        capabilities.Add<RollGlossaryCapability>());
                context.Content.ConfigureDefaultModelCapabilities<CardModel>(
                    "thirty_a_keyword",
                    static (_, capabilities) =>
                        capabilities.Add<ThirtyAKeywordCapability>());
                context.Content.ConfigureDefaultModelCapabilities<CardModel>(
                    "thirty_one_a_keyword",
                    static (_, capabilities) => capabilities.Add<ThirtyOneAKeywordCapability>());
                context.Content.ConfigureDefaultModelCapabilities<CardModel>(
                    "half_body_affiliation",
                    static (_, capabilities) => capabilities.Add<HalfBodyAffiliationCapability>());

                foreach (Type cardType in HotaruCardTypes)
                {
                    context.Content.RegisterCard(
                        typeof(HbrgoHotaruCardPool),
                        cardType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                }

                foreach (Type cardType in HotaruDerivedCardTypes)
                {
                    context.Content.RegisterCard(typeof(DeprecatedCardPool), cardType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                }

                foreach (Type cardType in CardTypes)
                {
                    if (HbrMemoryGardenCardCatalog.IsGardenCard(cardType))
                    {
                        context.Content.RegisterCard(
                            typeof(HBRGOSakiMemoryGardenCardPool),
                            cardType,
                            ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                        continue;
                    }
                    if (cardType == typeof(MimicEvilEyeTidalSlashHbrgoSaki))
                    {
                        // Retired cards remain loadable in existing saves.
                        context.Content.RegisterCard(typeof(DeprecatedCardPool), cardType,
                            ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                        continue;
                    }
                    if (cardType == typeof(AmnesiaHbrgoSaki))
                    {
                        context.Content.RegisterCard(
                            typeof(CurseCardPool),
                            cardType,
                            ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                        continue;
                    }
                    if (cardType == typeof(OverworkHbrgoSaki))
                    {
                        context.Content.RegisterCard(
                            typeof(StatusCardPool),
                            cardType,
                            ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                        continue;
                    }

                    bool isThirtyOneOExclusive = HbrgoSaki31OCardFilter
                        .IsThirtyOneOExclusive(cardType);
                    bool isThirtyA = HbrgoSaki31OCardFilter
                        .IsThirtyAAllowed(cardType);
                    bool isSharedStarter = cardType == typeof(StrikeHbrgoSaki) ||
                        cardType == typeof(DefendHbrgoSaki);
                    Type primaryPoolType = isThirtyOneOExclusive
                        ? typeof(HBRGOSaki31OCardPool)
                        : isThirtyA && !isSharedStarter
                            ? typeof(HBRGOSaki30ACardPool)
                            : typeof(HBRGOSakiCardPool);
                    context.Content.RegisterCard(
                        primaryPoolType,
                        cardType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));

                    if (isThirtyA && primaryPoolType != typeof(HBRGOSaki30ACardPool))
                    {
                        ModHelper.AddModelToPool(
                            typeof(HBRGOSaki30ACardPool),
                            cardType);
                    }

                    if (!isThirtyOneOExclusive &&
                        !HbrgoSaki31OCardFilter.IsExcluded(cardType))
                    {
                        ModHelper.AddModelToPool(
                            typeof(HBRGOSaki31OCardPool),
                            cardType);
                    }
                }

                foreach (Type relicType in RewardRelicTypes)
                {
                    context.Content.RegisterRelic(
                        relicType == typeof(HotaruElectronicSoldierManual) ||
                        relicType == typeof(Millsage) ||
                        relicType == typeof(HotaruIridescence)
                            ? typeof(HbrgoHotaruRelicPool)
                            : typeof(HBRGOSakiRelicPool),
                        relicType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[relicType]));
                }

                foreach (Type potionType in PotionTypes)
                {
                    // Keep old saves readable, without offering the retired potion in rewards/shops.
                    Type poolType = potionType == typeof(SoulPotionHbrgoSaki)
                        ? typeof(DeprecatedPotionPool)
                        : typeof(HBRGOSakiPotionPool);
                    context.Content.RegisterPotion(
                        poolType,
                        potionType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[potionType]));
                }

                foreach (Type relicType in InternalRelicTypes)
                {
                    context.Content.RegisterRelic(
                        typeof(HBRGOSakiInternalRelicPool),
                        relicType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[relicType]));
                }
            })
            .Apply();
    }
}
