using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class TourGuidePower : PowerModel
{
    public const int RollPenalty = -50;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    internal static int GetRollPenalty(Creature roller)
    {
        return roller?.CombatState?.GetOpponentsOf(roller)
            .Any(opponent => opponent.IsAlive &&
                opponent.HasPower<TourGuidePower>() &&
                opponent.GetPower<NararaSleepingPower>()?.Amount > 0) == true
            ? RollPenalty
            : 0;
    }
}
