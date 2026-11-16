using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HBR_CONTINUOUS_DAMAGE_POWER")]
public sealed class HbrContinuousDamagePower : HbrInstancedEnemyStatusPower
{
    internal override int StatusDuration => 4;
    internal override decimal StatusPotency => 15m;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.OwnerTurnStart, 4);
    public override string CustomPackedIconPath => "res://images/powers/hbr_continuous_damage_power.png";
    internal override decimal NativeApplicationBasis(decimal amount) => amount;
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner == null || Amount <= 0 || side != Owner.Side || !participants.Contains(Owner) ||
            !ReferenceEquals(Owner.CombatState, combatState)) return;
        Flash();
        await HbrEnemyStatusRuntime.PercentHpDamage(context, Owner, EffectPotency, Applier);
        if (Owner?.Powers.Contains(this) == true)
            await HbrEffectRules.TickStart(this, CurrentEffectProfile, side, participants);
    }
}

[CustomID("HBR_ENERGY_DRAIN_POWER")]
public sealed class HbrEnergyDrainPower : HbrInstancedEnemyStatusPower
{
    internal override int StatusDuration => 4;
    internal override decimal StatusPotency => 2m;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.OwnerTurnStart, 4);
    internal override bool CanApplyTo(Creature target) => target?.IsPlayer == true;
    internal override decimal NativeApplicationBasis(decimal amount) => amount;
    public override string CustomPackedIconPath => "res://images/powers/hbr_energy_drain_power.png";
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            int energy = (int)System.Math.Clamp(decimal.Floor(EffectPotency), 0m, int.MaxValue);
            string prefix = MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPrefix(this);
            string icon = $"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]";
            text.Add("EnergyIcons", string.Concat(Enumerable.Repeat(icon, energy)));
            return text;
        }
    }
    // Energy must be spent after the player's native reset, rather than just
    // before it. Every independent application contributes its own amount.
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState) => Task.CompletedTask;
    public override async Task AfterEnergyReset(Player player)
    {
        if (Amount <= 0 || Owner?.Player != player) return;
        Flash();
        await PlayerCmd.LoseEnergy(decimal.Floor(EffectPotency), player);
        if (Owner?.Powers.Contains(this) == true)
            await HbrEffectRules.TickStart(this, CurrentEffectProfile, Owner.Side, new[] { Owner });
    }
}
