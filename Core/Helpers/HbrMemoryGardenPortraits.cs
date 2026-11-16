using System;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Original HBR battle cut-ins use the existing garden frame. No file
// scans, image decoding or per-card mutable state in a portrait getter.
internal static class HbrMemoryGardenPortraits
{
    private const string Root = "res://images/packed/card_portraits/hbrgo_saki/memory_garden/";
    private const string DefaultPortrait = "res://images/packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png";
    private static readonly Dictionary<Type, string> Paths = new()
    {
        [typeof(AbsolutelyNoEffortHbrgoSaki)] = Root + "yotsuha.png", // yotsuha
        [typeof(AcePlayerIsNotMeHbrgoSaki)] = Root + "akari.png", // akari
        [typeof(AllOrdersConfirmedHbrgoSaki)] = Root + "mari.png", // mari
        [typeof(AlwaysKeepYourChinUpHbrgoSaki)] = Root + "risa.png", // risa
        [typeof(AsTheYoungLadyCommandsHbrgoSaki)] = Root + "mion.png", // mion
        [typeof(ByakkoRoarHbrgoSaki)] = Root + "byakko.png", // byakko
        [typeof(CuteIsInvincibleHbrgoSaki)] = Root + "chie.png", // chie
        [typeof(DiveAsDeepAsIWantHbrgoSaki)] = Root + "aina.png", // aina
        [typeof(EldestOshimaIsHereHbrgoSaki)] = Root + "ichiko.png", // ichiko
        [typeof(ExorcisingStringResoundHbrgoSaki)] = Root + "miya.png", // miya
        [typeof(FollowBehindMeHbrgoSaki)] = Root + "maki.png", // maki
        [typeof(FreshDeathHbrgoSaki)] = Root + "seika.png", // seika
        [typeof(FrozenMistMeowHbrgoSaki)] = Root + "sumomo.png", // sumomo
        [typeof(HarvestBountifulGrainsHbrgoSaki)] = Root + "satomi.png", // satomi
        [typeof(HellfireBurnHbrgoSaki)] = Root + "ichigo.png", // ichigo
        [typeof(IWillNotFallHbrgoSaki)] = Root + "mua.png", // mua
        [typeof(ImmortalRecollectionHbrgoSaki)] = Root + "miko.png", // miko
        [typeof(InnocentRequiemHbrgoSaki)] = Root + "kozue.png", // kozue
        [typeof(ItsNinjaShowtimeHbrgoSaki)] = Root + "adelheid.png", // adelheid
        [typeof(LeaveAnyBattlefieldToMeHbrgoSaki)] = Root + "chiroru.png", // chiroru
        [typeof(LegacyFlashHbrgoSaki)] = Root + "inori.png", // inori
        [typeof(LetMeShowYouClassHbrgoSaki)] = Root + "kanata.png", // kanata
        [typeof(LoveCharlottaHbrgoSaki)] = Root + "charlotta.png", // charlotta
        [typeof(MakeAChangeKillYourselfHbrgoSaki)] = Root + "fubuki.png", // fubuki
        [typeof(NarcissismIsFineHbrgoSaki)] = Root + "nina.png", // nina
        [typeof(NoblesseObligeHbrgoSaki)] = Root + "yuina.png", // yuina
        [typeof(NumbersAreTheSourceHbrgoSaki)] = Root + "vritika.png", // vritika
        [typeof(OpenTheLockHbrgoSaki)] = Root + "isuzu.png", // isuzu
        [typeof(PaintTheWorldMyColorHbrgoSaki)] = Root + "iroha.png", // iroha
        [typeof(PeacefulMemoriesHbrgoSaki)] = Root + "aoi.png", // aoi
        [typeof(ReadySteadyDieHbrgoSaki)] = Root + "carol.png", // carol
        [typeof(RepentanceTimeHbrgoSaki)] = Root + "maria.png", // maria
        [typeof(SlashIfCloseHbrgoSaki)] = Root + "hisame.png", // hisame
        [typeof(StarsGuideYouHbrgoSaki)] = Root + "seira.png", // seira
        [typeof(TengenBrilliantMoveHbrgoSaki)] = Root + "misato.png", // misato
        [typeof(ThereIsOnlyOneTruthHbrgoSaki)] = Root + "irene.png", // irene
        [typeof(ThisIsMyStageHbrgoSaki)] = Root + "shiki.png", // shiki
        [typeof(WindForestFireMountainHbrgoSaki)] = Root + "monaka.png", // monaka
        [typeof(WitnessMySupremacyHbrgoSaki)] = Root + "yingxia.png", // yingxia
        [typeof(WorldConquestHbrgoSaki)] = Root + "yayoi.png", // yayoi
        [typeof(WorldIsMyLaboratoryHbrgoSaki)] = Root + "wakki.png", // wakki
        [typeof(YingxiaFormationChoiceHbrgoSaki)] = Root + "yingxia.png", // yingxia
        [typeof(YourCombatDeliveryIsHereHbrgoSaki)] = Root + "minori.png", // minori
    };

    internal static bool HasCutinFrame(CardModel card) => card != null && Paths.ContainsKey(card.GetType());

    internal static string For(CardModel card) => card != null && Paths.TryGetValue(card.GetType(), out string path)
        ? path : DefaultPortrait;
}
