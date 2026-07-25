using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCharacters), MethodType.Getter)]
internal static class ModelDbAllCharactersPatch
{
    private static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        __result = __result
            .Append(ModelDb.Character<HbrgoSaki>())
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCardPools), MethodType.Getter)]
internal static class ModelDbAllCardPoolsPatch
{
    private static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        __result = __result
            .Append(ModelDb.CardPool<HBRGOSakiCardPool>())
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCharacterCardPools), MethodType.Getter)]
internal static class ModelDbAllCharacterCardPoolsPatch
{
    private static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        __result = __result
            .Append(ModelDb.CardPool<HBRGOSakiCardPool>())
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCards), MethodType.Getter)]
internal static class ModelDbAllCardsPatch
{
    private static void Postfix(ref IEnumerable<CardModel> __result)
    {
        __result = __result
            .Concat(ModelDb.CardPool<HBRGOSakiCardPool>().AllCards)
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllRelicPools), MethodType.Getter)]
internal static class ModelDbAllRelicPoolsPatch
{
    private static void Postfix(ref IEnumerable<RelicPoolModel> __result)
    {
        __result = __result
            .Append(ModelDb.RelicPool<HBRGOSakiRelicPool>())
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllRelics), MethodType.Getter)]
internal static class ModelDbAllRelicsPatch
{
    private static void Postfix(ref IEnumerable<RelicModel> __result)
    {
        __result = __result
            .Append(ModelDb.Relic<ElectronicSoldierManual>())
            .Append(ModelDb.Relic<SoulOfAnchor>())
            .Append(ModelDb.Relic<FabricatedSoulOfAnchor>())
            .Append(ModelDb.Relic<DeflectorProtection>())
            .Append(ModelDb.Relic<GuideLittleHand>())
            .Append(ModelDb.Relic<FeintLikeWind>())
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllPowers), MethodType.Getter)]
internal static class ModelDbAllPowersPatch
{
    private static void Postfix(ref IEnumerable<PowerModel> __result)
    {
        __result = __result
            .Append(ModelDb.Power<DodgePower>())
            .Append(ModelDb.Power<SwordheartPower>())
            .Append(ModelDb.Power<SwordheartClarityPower>())
            .Append(ModelDb.Power<CrimsonBladeFlowPower>())
            .Append(ModelDb.Power<NotOverYetPower>())
            .Append(ModelDb.Power<IOnlyKnowWhatIKnowPower>())
            .Append(ModelDb.Power<AngryIzumiYukiPower>())
            .Append(ModelDb.Power<AngryIzumiYukiTemporaryStrengthPower>())
            .Append(ModelDb.Power<FragilePower>())
            .Append(ModelDb.Power<MagicSwordModePower>())
            .Append(ModelDb.Power<FeintMarkPower>())
            .Append(ModelDb.Power<FeintDamageRedirectPower>())
            .Append(ModelDb.Power<SakuraRainPower>())
            .Append(ModelDb.Power<MagicSwordEnhancementPower>())
            .Append(ModelDb.Power<NoSelfInDreamPower>())
            .Append(ModelDb.Power<NoSelfInDreamTemporaryStrengthPower>())
            .Append(ModelDb.Power<NormalStatePower>())
            .Append(ModelDb.Power<ReptureBowPower>())
            .Append(ModelDb.Power<ReptureDualSwordsPower>())
            .Append(ModelDb.Power<StrongestShieldPower>())
            .Append(ModelDb.Power<DisabledPower>())
            .Append(ModelDb.Power<SingTogetherPower>())
            .Append(ModelDb.Power<PreparationPower>())
            .Append(ModelDb.Power<HighSongPower>())
            .Append(ModelDb.Power<ShadowDanceTemporaryStrengthPower>())
            .Append(ModelDb.Power<ShadowDanceExtraTurnPower>())
            .Append(ModelDb.Power<BlueSkyPower>())
            .Append(ModelDb.Power<BlueSkyGuardPower>())
            .Append(ModelDb.Power<ChargePower>())
            .Append(ModelDb.Power<TinyMomentsPower>())
            .Append(ModelDb.Power<LostMelodyPower>())
            .Distinct();
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllPotionPools), MethodType.Getter)]
internal static class ModelDbAllPotionPoolsPatch
{
    private static void Postfix(ref IEnumerable<PotionPoolModel> __result)
    {
        __result = __result
            .Append(ModelDb.PotionPool<HBRGOSakiPotionPool>())
            .Distinct();
    }
}
