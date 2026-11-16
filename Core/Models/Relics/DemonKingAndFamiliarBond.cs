using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class DemonKingAndFamiliarBond : TeamCollectionRelic, IActiveDuoBond
{
    private sealed class TurnLedger
    {
        internal bool YamawakiPlayed;
        internal readonly HashSet<Creature> OpenedTargets = new();
    }
    private TurnLedger _ordinary = new(), _enemy = new();
    private ConditionalWeakTable<object, TurnLedger> _extra = new();
    public bool IsBondActive => IsActive;
    internal bool IsActive => !IsMelted && Owner != null && DisplayAmount == 2 && HbrBattleStatsHelper.IsEnabled(Owner);
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/crychic.png");
    protected override int CountCards(Player player) => DemonKingAndFamiliarBondHelper.GetKinds(player);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<BreakPower>() };

    private TurnLedger CurrentLedger(bool create)
    {
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner);
        if (token != null)
            return create ? _extra.GetOrCreateValue(token) : _extra.TryGetValue(token, out TurnLedger value) ? value : null;
        return Owner.Creature?.CombatState?.CurrentSide == CombatSide.Enemy ? _enemy : _ordinary;
    }
    internal decimal Bonus(bool bungo, Creature target)
    {
        TurnLedger ledger = CurrentLedger(false);
        return ledger != null && (bungo ? ledger.YamawakiPlayed : ledger.OpenedTargets.Contains(target)) ? 50m : 0m;
    }
    internal void RecordOpenedTarget(Creature target) => CurrentLedger(true).OpenedTargets.Add(target);
    public override Task BeforeCombatStart()
    {
        _ordinary = new(); _enemy = new(); _extra = new();
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        // An inserted turn owns a separate receipt; its start/return must never
        // erase the still-suspended ordinary turn's card or target history.
        if (player == Owner && OverclockImmediateTurnCoordinator.GetTurnToken(Owner) == null) _ordinary = new();
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy) _enemy = new();
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        if (IsActive && play?.Card is WorldIsMyLaboratoryHbrgoSaki card && card.Owner == Owner)
            CurrentLedger(true).YamawakiPlayed = true;
        return Task.CompletedTask;
    }
    internal void AddDescriptionVariables(LocString text) => text.Add("ActiveText", IsMutable && IsActive ? "已生效" : "未生效");
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _ordinary = new(); _enemy = new(); _extra = new();
    }
}
