using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed class CannonHitReceipt
{
    internal Creature Cannon { get; init; }
    internal Creature Dealer { get; init; }
    internal Creature Target { get; init; }
    internal decimal ActualHpDamage { get; init; }
    internal decimal ActualDpDamage { get; init; }
    internal decimal ActiveDpMaximum { get; init; }
    internal decimal FinalDamage { get; init; }
    internal CardModel CardSource { get; init; }
    internal CardPlay CardPlay { get; init; }
    internal bool IsReaction { get; init; }
    internal bool IsPrimary => !IsReaction;
    internal decimal ActualDamage => DamageAmplificationLimits.AddSaturated(ActualHpDamage, ActualDpDamage);
}
