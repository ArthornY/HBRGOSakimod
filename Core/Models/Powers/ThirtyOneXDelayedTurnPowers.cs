using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public abstract class ThirtyOneXDelayedTurnPower : HbrEnemyStatusPower
{
    [SavedProperty] public int LastOrdinaryTurn { get; protected set; }
    private HashSet<object> _extraTurns = new(ReferenceEqualityComparer.Instance);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override string CustomPackedIconPath => "res://images/powers/hbr_defense_up_power.png";
    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        LastOrdinaryTurn = Owner.Player.PlayerCombatState.TurnNumber;
        if (OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) is { } token) _extraTurns.Add(token);
        return Task.CompletedTask;
    }
    internal async Task Advance(PlayerChoiceContext context)
    {
        Player player = Owner?.Player;
        if (Amount <= 0 || player?.Creature.IsAlive != true || !Owner.Powers.Contains(this) || !HbrBattleStatsHelper.IsEnabled(player)) return;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token != null) { if (!_extraTurns.Add(token)) return; }
        else
        {
            int turn = player.PlayerCombatState.TurnNumber;
            if (turn <= LastOrdinaryTurn) return;
            LastOrdinaryTurn = turn;
        }
        await Resolve(context);
    }
    protected abstract Task Resolve(PlayerChoiceContext context);
    public override Task AfterEnergyReset(Player player) => player == Owner?.Player
        ? Advance(new ThrowingPlayerChoiceContext()) : Task.CompletedTask;
    protected override void AfterCloned() { base.AfterCloned(); _extraTurns = new(ReferenceEqualityComparer.Instance); }
}

[CustomID("YINGXIA_REGENERATION_FORMATION_POWER")]
public sealed class YingxiaRegenerationFormationPower : ThirtyOneXDelayedTurnPower
{
    protected override async Task Resolve(PlayerChoiceContext context)
    {
        foreach (Creature ally in HbrPlayerTeamTargets.Get(Owner))
            await SeiraDpRecoveryHelper.RestoreFromAbility(context, ally, .07m, false);
        await PowerCmd.Decrement(this);
    }
}

[CustomID("CHARLOTTA_ENGAGEMENT_RING_POWER")]
public sealed class CharlottaEngagementRingPower : ThirtyOneXDelayedTurnPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_energy_drain_power.png";
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("EnergyIcons", IsMutable
                ? $"[img]res://images/packed/sprite_fonts/{EnergyIconHelper.GetPrefix(this)}_energy_icon.png[/img]" : "[E]");
            return text;
        }
    }
    protected override async Task Resolve(PlayerChoiceContext context)
    {
        Player player = Owner.Player;
        await PowerCmd.Remove(this);
        await PlayerCmd.GainEnergy(1m, player);
    }
}
