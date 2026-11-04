using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrgoSakiCombatForm
{
    Normal,
    Bow,
    DualSwords,
    Scythe,
    ThirtyA
}

// The combat portrait and the energy counter switch with the same form.
internal static class HbrgoSakiCombatFormHelper
{
    public static HbrgoSakiCombatForm GetForm(Creature creature)
    {
        if (creature?.Player?.Character is HbrgoSaki30A)
        {
            return HbrgoSakiCombatForm.ThirtyA;
        }

        if (creature == null)
        {
            return HbrgoSakiCombatForm.Normal;
        }

        if (creature.HasPower<RaptureScythePower>())
        {
            return HbrgoSakiCombatForm.Scythe;
        }

        if (creature.HasPower<ReptureBowPower>())
        {
            return HbrgoSakiCombatForm.Bow;
        }

        if (creature.HasPower<ReptureDualSwordsPower>() ||
            creature.HasPower<MagicSwordModePower>() ||
            creature.HasPower<CrimsonBladeFlowPower>())
        {
            return HbrgoSakiCombatForm.DualSwords;
        }

        return HbrgoSakiCombatForm.Normal;
    }
}
