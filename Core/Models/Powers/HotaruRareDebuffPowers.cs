using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_INCOMPLETE_CHORD_POWER")]
public sealed class HotaruIncompleteChordPower : HotaruUncommonCardPower { }

[CustomID("HOTARU_TRIPLE_SILENCE_POWER")]
public sealed class HotaruTripleSilencePower : HotaruUncommonCardPower
{
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (dealer != Owner || amount <= 0m ||
            HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out _)) return 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(1m + HotaruRareDebuffHelper.GetDamageBonus(dealer, target), this);
    }
}
