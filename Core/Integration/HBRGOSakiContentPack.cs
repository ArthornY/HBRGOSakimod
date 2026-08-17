using System;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Potions;
using HBRGOSakiMod.Core.Models.RelicPools;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Content;
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
        typeof(ArmorBreakSoulStrikeHbrgoSaki),
        typeof(SoulFinisherHbrgoSaki),
        typeof(DpSoulStrikeHbrgoSaki),
        typeof(MimicEvilEyeTidalSlashHbrgoSaki),
        typeof(ChargeStrikeHbrgoSaki),
        typeof(ChargedShotHbrgoSaki),
        typeof(HerSongIsMyScreamHbrgoSaki),
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
        typeof(MagicSwordLuckySevenHbrgoSaki),
        typeof(MagicSwordModeHbrgoSaki),
        typeof(ChihayaClassmateHbrgoSaki),
        typeof(SwitchHbrgoSaki),
        typeof(ReptureBowChoiceHbrgoSaki),
        typeof(ReptureDualSwordsChoiceHbrgoSaki),
        typeof(RaptureScytheChoiceHbrgoSaki),
        typeof(FeintHbrgoSaki),
        typeof(HighSongHbrgoSaki),
        typeof(ShadowDanceSongHbrgoSaki),
        typeof(BlueSkySongHbrgoSaki),
        typeof(LostDaysSongHbrgoSaki)
    };

    private static readonly Type[] RewardRelicTypes =
    {
        typeof(ElectronicSoldierManual),
        typeof(SoulOfAnchor),
        typeof(DeflectorProtection),
        typeof(Overclock),
        typeof(RestorationMoment),
        typeof(GuideLittleHand),
        typeof(FeintLikeWind)
    };

    private static readonly Type[] PotionTypes =
    {
        typeof(DpPotionHbrgoSaki),
        typeof(FragilePotionHbrgoSaki),
        typeof(SoulPotionHbrgoSaki)
    };

    internal static void Register()
    {
        var publicEntries = CardTypes
            .Concat(RewardRelicTypes)
            .Concat(PotionTypes)
            .Append(typeof(FabricatedSoulOfAnchor))
            .Distinct()
            .ToDictionary(type => type, type => ModelDb.GetId(type).Entry);

        ModCardHandOutlineRegistry.Register(
            typeof(CardModel),
            ModCardHandOutlineRules.Fixed(
                MagicSwordEnergyPaymentHelper.CanPlayUsingSwordheart,
                SwordheartSupplementOutlineColor,
                visibleWhenUnplayable: true));

        RitsuLibFramework.CreateContentPack(ModId)
            .SharedEvent<JoiningCeremonyHbrgoSaki>()
            .Custom(context =>
            {
                foreach (Type cardType in CardTypes)
                {
                    context.Content.RegisterCard(
                        typeof(HBRGOSakiCardPool),
                        cardType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[cardType]));
                }

                foreach (Type relicType in RewardRelicTypes)
                {
                    context.Content.RegisterRelic(
                        typeof(HBRGOSakiRelicPool),
                        relicType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[relicType]));
                }

                foreach (Type potionType in PotionTypes)
                {
                    context.Content.RegisterPotion(
                        typeof(HBRGOSakiPotionPool),
                        potionType,
                        ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[potionType]));
                }

                context.Content.RegisterRelic(
                    typeof(HBRGOSakiInternalRelicPool),
                    typeof(FabricatedSoulOfAnchor),
                    ModelPublicEntryOptions.FromFullPublicEntry(publicEntries[typeof(FabricatedSoulOfAnchor)]));
            })
            .Apply();
    }
}
