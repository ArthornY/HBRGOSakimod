using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrgoSaki31OCardFilter
{
    public static bool IsExcluded(CardModel card)
    {
        return card is SoulIntimidationHbrgoSaki
            or CollectSoulHbrgoSaki
            or SoulBufferHbrgoSaki
            or SoulHarvestHbrgoSaki
            or SoulExplosionHbrgoSaki
            or MimicCursedScytheHbrgoSaki
            or ExtraGaugeStrikeHbrgoSaki
            or ArmorBreakSoulStrikeHbrgoSaki
            or SoulFinisherHbrgoSaki
            or DpSoulStrikeHbrgoSaki
            or ReapHbrgoSaki
            or RedCrimsonExtraStrikeHbrgoSaki
            or PowerReleaseHbrgoSaki
            or DamageTransferHbrgoSaki
            or RaptureScytheChoiceHbrgoSaki;
    }
}
