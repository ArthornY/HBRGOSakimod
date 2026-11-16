using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Default original AttackSkill references STR/DEX equally. Verified named
// original skills override that profile; DP/HP itself never changes weights.
internal static class HbrSkillAttributeProfiles
{
    internal static readonly HbrStatWeights Physical = new(1, 1, 0, 0, 0, 0);
    private static readonly HbrStatWeights StrengthBiased = new(2, 1, 0, 0, 0, 0);

    internal static HbrStatWeights GetDamageWeights(CardModel card, bool targetsDp) => card switch
    {
        // master.hbr.quest CN skill46001112, 46001106 and 46002511's
        // conditional branches (46002512/13) carry these same parameters.
        MimicThunderGaleHbrgoSaki or MimicDreamBubbleHbrgoSaki or MimicEvilEyeTidalSlashHbrgoSaki
            => StrengthBiased,
        _ => Physical
    };
}
