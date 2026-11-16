using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("MINORI_NEXT_TURN_ENERGY_DEBT_POWER")]
public sealed class MinoriNextTurnEnergyDebtPower : HbrEnemyStatusPower
{
    [SavedProperty] public int AppliedOwnTurn { get; private set; }
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => "res://images/powers/hbr_energy_drain_power.png";
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            if (!IsMutable)
            {
                text.Add("EnergyIcons", "[E]");
                return text;
            }
            string prefix = EnergyIconHelper.GetPrefix(this);
            text.Add("EnergyIcons", string.Concat(Enumerable.Repeat($"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]", Math.Max(0, Amount))));
            return text;
        }
    }
    public override Task AfterApplied(Creature applier, CardModel cardSource)
    { AppliedOwnTurn = Owner?.Player?.PlayerCombatState?.TurnNumber ?? 0; return Task.CompletedTask; }
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner?.Player || Amount <= 0 || player.PlayerCombatState?.TurnNumber <= AppliedOwnTurn ||
            OverclockImmediateTurnCoordinator.IsActiveFor(player) || PersonalImmediateTurnCoordinator.IsActiveFor(player)) return;
        int loss = Amount;
        await PowerCmd.Remove(this);
        await PlayerCmd.LoseEnergy(loss, player);
    }
}
