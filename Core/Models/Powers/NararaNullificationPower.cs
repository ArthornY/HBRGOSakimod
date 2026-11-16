using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaNullificationPower : PowerModel, ICustomPower
{
    private HashSet<(Player Player, int Turn, object Extra)> _usedTurns = new();
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public string CustomPackedIconPath => "res://images/powers/six_sense_resistance_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal bool IsActive => Owner?.IsAlive == true &&
        Owner.Monster is NararaShibaBossMonster && Owner.Side == CombatSide.Enemy &&
        HbrBattleStatsHelper.IsEnabled(Owner);

    private (Player Player, int Turn, object Extra) Key(Player player) =>
        (player, player?.PlayerCombatState?.TurnNumber ?? 0, OverclockImmediateTurnCoordinator.GetTurnToken(player));
    internal bool CanClaimTurn(Player player) => IsActive && player?.Creature.IsAlive == true &&
        player.Creature.Side != Owner.Side && ReferenceEquals(player.Creature.CombatState, Owner.CombatState) &&
        !_usedTurns.Contains(Key(player));
    internal bool TryClaimTurn(Player player) => CanClaimTurn(player) && _usedTurns.Add(Key(player));

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combat)
    {
        if (IsActive && side == CombatSide.Player) NararaNullificationHelper.RefreshHands(combat);
        return Task.CompletedTask;
    }
    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (!IsActive) return;
        await NararaNullificationHelper.ResolvePlay(play);
        NararaNullificationHelper.RecordPlay(this, play);
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _usedTurns = new();
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource) =>
        IsActive && target == Owner && !props.HasFlag(ValueProp.Unpowered) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out _) &&
        NararaNullificationHelper.IsSuccessful(target, dealer, cardSource)
            ? HbrgoSakiDamageMultiplierHelper.AddTemporary(1.5m, this) : 1m;

    public decimal ModifyDamageCap(Creature target, ValueProp props, Creature dealer, CardModel cardSource) =>
        GetNullificationDamageCap(target, cardSource);

    // Give the compatibility bridge its own nonvirtual entry point. Calling an
    // override directly still encodes the old AbstractModel signature in IL.
    internal decimal GetNullificationDamageCap(Creature target, CardModel cardSource) =>
        target == Owner && IsActive && NararaNullificationHelper.IsCardNullified(Owner, cardSource)
            ? 0m : decimal.MaxValue;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsActive || !NararaNullificationHelper.ShouldHeal(this, cardPlay)) return;
        Flash();
        await CreatureCmd.Heal(Owner, decimal.Floor(LargeCreatureHealthHelper.GetMaxHp(Owner) * 0.02m));
    }

}
