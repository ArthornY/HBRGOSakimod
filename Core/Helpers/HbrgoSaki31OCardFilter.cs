using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrgoSaki31OCardFilter
{
    private static readonly HashSet<Type> ThirtyAAllowedTypes = new()
    {
        typeof(StrikeHbrgoSaki),
        typeof(DefendHbrgoSaki),
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
        typeof(MimicCursedScytheHbrgoSaki),
        typeof(ChargeUpSlashHbrgoSaki),
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
        typeof(EcstaticCorridorHbrgoSaki)
    };

    private static readonly HashSet<Type> ThirtyOneOExclusiveTypes = new()
    {
        typeof(RuinStrikeHbrgoSaki),
        typeof(ResistanceBulwarkHbrgoSaki),
        typeof(BreakInsightHbrgoSaki),
        typeof(SenseShatterHbrgoSaki),
        typeof(BeyondProtectionHbrgoSaki),
        typeof(MagicSwordUniversalityHbrgoSaki),
        typeof(MimicReminiscenceHbrgoSaki),
        typeof(QuickThinkingHbrgoSaki),
        typeof(SwitchingSlashHbrgoSaki),
        typeof(ConsecutiveSlashHbrgoSaki),
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
        typeof(RemoveDpHbrgoSaki),
        typeof(AnonAidHbrgoSaki),
        typeof(SwordheartClarityHbrgoSaki),
        typeof(TeleportHbrgoSaki),
        typeof(MeditationHbrgoSaki),
        typeof(SakuraRainHbrgoSaki),
        typeof(MagicSwordEnhancementHbrgoSaki),
        typeof(MagicSwordSharingHbrgoSaki),
        typeof(MagicSwordLuckySevenHbrgoSaki),
        typeof(MagicSwordLuckySevenOpeningHbrgoSaki),
        typeof(MagicSwordModeHbrgoSaki),
        typeof(FeintHbrgoSaki),
        typeof(UnfetteredHbrgoSaki),
        typeof(AnokoHbrgoSaki)
    };

    private static readonly HashSet<Type> ThirtyOneOExcludedTypes = new()
    {
        typeof(MimicEvilEyeTidalSlashHbrgoSaki),
        typeof(SoulIntimidationHbrgoSaki),
        typeof(CollectSoulHbrgoSaki),
        typeof(SoulBufferHbrgoSaki),
        typeof(SoulHarvestHbrgoSaki),
        typeof(SoulExplosionHbrgoSaki),
        typeof(MimicCursedScytheHbrgoSaki),
        typeof(ExtraGaugeStrikeHbrgoSaki),
        typeof(ArmorBreakSoulStrikeHbrgoSaki),
        typeof(SoulFinisherHbrgoSaki),
        typeof(DpSoulStrikeHbrgoSaki),
        typeof(ReapHbrgoSaki),
        typeof(RedCrimsonExtraStrikeHbrgoSaki),
        typeof(PowerReleaseHbrgoSaki),
        typeof(DamageTransferHbrgoSaki),
        typeof(RaptureScytheChoiceHbrgoSaki),
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
        typeof(StrengthAmplificationHbrgoSaki),
        typeof(CommandStrikeHbrgoSaki),
        typeof(PhantomChoiceHbrgoSaki),
        typeof(PhantomFusionEffectChoiceHbrgoSaki),
        typeof(PhantomKnowledgeChoiceHbrgoSaki),
        typeof(ObsessiveHbrgoSaki),
        typeof(ReturnToMeHbrgoSaki),
        typeof(AlreadyWeightlessHbrgoSaki),
        typeof(EmbraceHbrgoSaki),
        typeof(BindingRedemptionHbrgoSaki),
        typeof(AsIfAtoningHbrgoSaki),
        typeof(JoyfulCaptiveHbrgoSaki),
        typeof(ReadyToGoHbrgoSaki),
        typeof(DeliberationHbrgoSaki),
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
        typeof(EcstaticCorridorHbrgoSaki)
    };

    public static bool IsAvailableTo(
        CharacterModel character,
        CardModel card)
    {
        if (character is not HbrgoSaki)
        {
            return true;
        }

        return IsAvailableToVariant(
            card,
            character is HbrgoSaki31O);
    }

    public static bool IsAvailableToVariant(
        CardModel card,
        bool isThirtyOneO)
    {
        if (card is AmnesiaHbrgoSaki)
        {
            return false;
        }

        return isThirtyOneO
            ? !IsExcluded(card)
            : IsThirtyAAllowed(card);
    }

    public static bool IsThirtyAAllowed(CardModel card)
    {
        return card != null && IsThirtyAAllowed(card.GetType());
    }

    public static bool IsThirtyAAllowed(Type cardType)
    {
        return cardType != null && ThirtyAAllowedTypes.Contains(cardType);
    }

    public static IEnumerable<CardModel> GetThirtyACards()
    {
        return ThirtyAAllowedTypes
            .Where(ModelDb.Contains)
            .Select(type => ModelDb.GetById<CardModel>(ModelDb.GetId(type)))
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal);
    }

    public static bool IsThirtyOneOExclusive(CardModel card)
    {
        return card != null && IsThirtyOneOExclusive(card.GetType());
    }

    public static bool IsThirtyOneOExclusive(Type cardType)
    {
        return cardType != null &&
            ThirtyOneOExclusiveTypes.Contains(cardType);
    }

    public static bool IsExcluded(CardModel card)
    {
        return card != null && IsExcluded(card.GetType());
    }

    public static bool IsExcluded(Type cardType)
    {
        return cardType != null && ThirtyOneOExcludedTypes.Contains(cardType);
    }
}
