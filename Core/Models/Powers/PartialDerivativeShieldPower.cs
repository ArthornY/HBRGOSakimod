using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PartialDerivativeShieldPower :
    HighDimensionalShellPower
{
    // Simulation DP uses full maximum HP, independently of the normal shell
    // percentage setting, but still receives difficulty and per-act DP growth.
    internal static decimal CalculateInitialAmount(Creature creature) =>
        BigCombatNumbers.ScaleHealth(
            LargeCreatureHealthHelper.GetMaxHp(creature), GetEnemyShellMultiplier(creature));

    protected override decimal MaximumAmount => CalculateInitialAmount(Owner);

    protected override async Task ApplyBreakPower(
        PlayerChoiceContext choiceContext,
        int _)
    {
        if (!Owner.HasPower<GhostStatePower>())
        {
            await PowerCmd.Apply<GhostStatePower>(
                choiceContext,
                Owner,
                1,
                Owner,
                null);
        }

        if (!Owner.HasPower<PermanentBreakPower>())
        {
            await PowerCmd.Apply<PermanentBreakPower>(
                choiceContext,
                Owner,
                1 + HelloWorldCombatHelper.GetInitialBreakBonus(Owner),
                Owner,
                null);
        }
    }
}
