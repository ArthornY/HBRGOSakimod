using System;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrMemoryGardenCardCatalog
{
    internal static IReadOnlyList<Type> CardTypes { get; } = Array.AsReadOnly(new[]
    {
        typeof(HerSongIsMyScreamHbrgoSaki),
        typeof(MatchaParfaitHbrgoSaki),
        typeof(LostStarSongHbrgoSaki),
        typeof(AnonAidHbrgoSaki),
        typeof(SoyoAidHbrgoSaki),
        typeof(HalfBodyHbrgoSaki),
        typeof(MyLegendBeginsHbrgoSaki),
        typeof(HelloWorldHbrgoSaki),
        typeof(SaviorGrandEntranceHbrgoSaki),
        typeof(WorldsFinestHbrgoSaki),
        typeof(MoreThanOneTruthHbrgoSaki),
        typeof(CrushYouLikeABugHbrgoSaki),
        typeof(SummerGrassDreamHbrgoSaki),
        typeof(ByakkoRoarHbrgoSaki),
        typeof(PeacefulMemoriesHbrgoSaki),
        typeof(HellfireBurnHbrgoSaki),
        typeof(FrozenMistMeowHbrgoSaki),
        typeof(InnocentRequiemHbrgoSaki),
        typeof(FreshDeathHbrgoSaki),
        typeof(WorldIsMyLaboratoryHbrgoSaki),
        typeof(WorldConquestHbrgoSaki),
        typeof(ItsNinjaShowtimeHbrgoSaki),
        typeof(ImmortalRecollectionHbrgoSaki),
        typeof(StarsGuideYouHbrgoSaki),
        typeof(AllOrdersConfirmedHbrgoSaki),
        typeof(TengenBrilliantMoveHbrgoSaki),
        typeof(PaintTheWorldMyColorHbrgoSaki),
        typeof(MakeAChangeKillYourselfHbrgoSaki),
        typeof(AlwaysKeepYourChinUpHbrgoSaki),
        typeof(AcePlayerIsNotMeHbrgoSaki),
        typeof(DiveAsDeepAsIWantHbrgoSaki),
        typeof(EldestOshimaIsHereHbrgoSaki),
        typeof(NarcissismIsFineHbrgoSaki),
        typeof(YourCombatDeliveryIsHereHbrgoSaki),
        typeof(AbsolutelyNoEffortHbrgoSaki),
        typeof(OpenTheLockHbrgoSaki),
        typeof(IWillNotFallHbrgoSaki),
        typeof(NoblesseObligeHbrgoSaki),
        typeof(ExorcisingStringResoundHbrgoSaki),
        typeof(WindForestFireMountainHbrgoSaki),
        typeof(HarvestBountifulGrainsHbrgoSaki),
        typeof(CuteIsInvincibleHbrgoSaki),
        typeof(SlashIfCloseHbrgoSaki),
        typeof(AsTheYoungLadyCommandsHbrgoSaki),
        typeof(LetMeShowYouClassHbrgoSaki),
        typeof(ThisIsMyStageHbrgoSaki),
        typeof(LeaveAnyBattlefieldToMeHbrgoSaki),
        typeof(LegacyFlashHbrgoSaki),
        typeof(FollowBehindMeHbrgoSaki),
        typeof(ReadySteadyDieHbrgoSaki),
        typeof(ThereIsOnlyOneTruthHbrgoSaki),
        typeof(RepentanceTimeHbrgoSaki),
        typeof(NumbersAreTheSourceHbrgoSaki),
        typeof(WitnessMySupremacyHbrgoSaki),
        typeof(LoveCharlottaHbrgoSaki),
        typeof(BoostUpHbrgoSaki)
    });

    internal static bool IsGardenCard(CardModel card) => card != null && IsGardenCard(card.GetType());

    internal static bool IsOrdinaryUpgradeCandidate(CardModel card) =>
        card != null && !IsGardenCard(card) && card.IsUpgradable;

    internal static bool IsGardenCard(Type type) =>
        type == typeof(HerSongIsMyScreamHbrgoSaki) ||
        type == typeof(MatchaParfaitHbrgoSaki) ||
        type == typeof(LostStarSongHbrgoSaki) ||
        type == typeof(AnonAidHbrgoSaki) ||
        type == typeof(SoyoAidHbrgoSaki) ||
        type == typeof(HalfBodyHbrgoSaki) ||
        type == typeof(MyLegendBeginsHbrgoSaki) ||
        type == typeof(HelloWorldHbrgoSaki) ||
        type == typeof(SaviorGrandEntranceHbrgoSaki) ||
        type == typeof(WorldsFinestHbrgoSaki) ||
        type == typeof(MoreThanOneTruthHbrgoSaki) ||
        type == typeof(CrushYouLikeABugHbrgoSaki) ||
        type == typeof(SummerGrassDreamHbrgoSaki) ||
        type == typeof(ByakkoRoarHbrgoSaki) ||
        typeof(ThirtyOneBGardenCard).IsAssignableFrom(type) ||
        type == typeof(BoostUpHbrgoSaki);
}
