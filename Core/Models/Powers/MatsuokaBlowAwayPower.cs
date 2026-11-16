using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

// One ability per hero. Additional physical power cards add upgrade flags, not
// another trigger or a fresh once-per-battle resource receipt.
public sealed class MatsuokaBlowAwayPower : PowerModel, ICustomPower
{
    [SavedProperty] public int UpgradeFlags { get; private set; }
    [SavedProperty] public bool UsedThisRound { get; private set; }
    [SavedProperty] public bool RecoveredThisBattle { get; private set; }
    [SavedProperty] public int BlowAwayCount { get; private set; }
    [SavedProperty] public int LastNativeTurn { get; private set; } = -1;
    private object _lastExtraTurn;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public string CustomPackedIconPath => "res://images/powers/hbr_defense_up_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    internal bool Has(int id) => (UpgradeFlags & (1 << id)) != 0;
    internal decimal NextDefenseBasis => 30m + (Has(LeaveAnyBattlefieldToMeHbrgoSaki.ScarsAreMyPride)
        ? Math.Min(6, Math.Max(0, BlowAwayCount)) * 5m : 0m);
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            MatsuokaBlowAwayCombatHelper.AddPowerDescriptionParameters(this, text);
            return text;
        }
    }
    internal void Configure(LeaveAnyBattlefieldToMeHbrgoSaki card)
    {
        AssertMutable();
        for (int id = 0; id < 7; id++) if (card.HasUpgradeEffect(id)) UpgradeFlags |= 1 << id;
        if (LastNativeTurn < 0)
        {
            LastNativeTurn = Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;
            _lastExtraTurn = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        }
        Owner.InvokePowerModified(this, 0, true);
    }
    internal void StartOwnTurn()
    {
        if (Owner?.Player is not { PlayerCombatState: { } state } player) return;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (LastNativeTurn == state.TurnNumber && ReferenceEquals(_lastExtraTurn, token)) return;
        LastNativeTurn = state.TurnNumber; _lastExtraTurn = token;
        UsedThisRound = false;
        Owner.InvokePowerModified(this, 0, true);
    }
    internal bool TryTakeTrigger(out decimal basis, out bool recover)
    {
        basis = NextDefenseBasis; recover = false;
        if (UsedThisRound || Amount <= 0) return false;
        UsedThisRound = true;
        if (BlowAwayCount == 0 && Has(LeaveAnyBattlefieldToMeHbrgoSaki.RoadToStardom) && !RecoveredThisBattle)
        { RecoveredThisBattle = true; recover = true; }
        BlowAwayCount = (int)Math.Min(int.MaxValue, (long)Math.Max(0, BlowAwayCount) + 1L);
        Flash();
        Owner.InvokePowerModified(this, 0, true);
        return true;
    }
    public override Task AfterApplied(Creature applier, CardModel source)
    {
        if (source is LeaveAnyBattlefieldToMeHbrgoSaki card) Configure(card);
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (IsMutable && player == Owner.Player) StartOwnTurn();
        return Task.CompletedTask;
    }
    public override Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature dealer, CardModel source) =>
        MatsuokaBlowAwayCombatHelper.AfterHit(context, this, target, result, props, dealer);
    protected override void AfterCloned() { base.AfterCloned(); _lastExtraTurn = null; }
}
