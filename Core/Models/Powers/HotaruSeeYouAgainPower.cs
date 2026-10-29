using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_SEE_YOU_AGAIN_POWER")]
public sealed class HotaruSeeYouAgainPower : PowerModel, ICustomPower
{
    private object _overclockToken;
    private int _overclockCopies;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryCopies { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryTurnNumber { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override int DisplayAmount => IsMutable ? ActiveCopies : 1;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal int ActiveCopies
    {
        get
        {
            if (!IsMutable || Amount <= 0 || Owner?.IsAlive != true || Owner.CombatState == null) return 0;
            object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
            int copies = token == null
                ? Owner.Player?.PlayerCombatState?.TurnNumber == OrdinaryTurnNumber ? OrdinaryCopies : 0
                : ReferenceEquals(token, _overclockToken) ? _overclockCopies : 0;
            return Math.Clamp(copies, 0, Amount);
        }
    }

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Amount", IsMutable ? ActiveCopies : 1);
            text.Add("Multiplier", IsMutable ? HotaruSeeYouAgainHelper.Pow2(ActiveCopies) : 2m);
            text.Add("ExtraDexterity", IsMutable ? HotaruSeeYouAgainHelper.GetExtraDexterity(Owner?.Player) : 0m);
            return text;
        }
    }

    public static async Task Apply(PlayerChoiceContext context, Creature owner, CardModel source)
    {
        if (owner?.IsAlive != true || owner.Player?.PlayerCombatState == null || owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding) return;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner.Player);
        int turn = owner.Player.PlayerCombatState.TurnNumber;
        HotaruSeeYouAgainPower power = owner.GetPower<HotaruSeeYouAgainPower>();
        if (power == null)
        {
            power = (HotaruSeeYouAgainPower)ModelDb.Power<HotaruSeeYouAgainPower>().ToMutable();
            power.AddCopies(token, 1, turn);
            try { await PowerCmd.Apply(context, power, owner, 1m, owner, source); }
            finally { power.AlignCopies(token, turn); }
        }
        else
        {
            power.AddCopies(token, 1, turn);
            try { await PowerCmd.ModifyAmount(context, power, 1m, owner, source); }
            finally { power.AlignCopies(token, turn); }
        }
        await power.Refresh();
    }

    private void AddCopies(object token, int copies, int turn)
    {
        if (copies <= 0) return;
        if (token == null)
        {
            OrdinaryTurnNumber = turn;
            OrdinaryCopies = (int)Math.Min(999_999_999L, (long)OrdinaryCopies + copies);
        }
        else
        {
            if (!ReferenceEquals(token, _overclockToken)) _overclockCopies = 0;
            _overclockToken = token;
            _overclockCopies = (int)Math.Min(999_999_999L, (long)_overclockCopies + copies);
        }
    }

    private void AlignCopies(object token, int turn)
    {
        long difference = Math.Max(0, Amount) - ((long)OrdinaryCopies + _overclockCopies);
        if (difference > 0)
        {
            AddCopies(token, (int)Math.Min(int.MaxValue, difference), turn);
            return;
        }
        int remove = (int)Math.Min(int.MaxValue, -difference);
        if (token != null && ReferenceEquals(token, _overclockToken))
        {
            int fromOd = Math.Min(remove, _overclockCopies);
            _overclockCopies -= fromOd;
            remove -= fromOd;
        }
        int fromOrdinary = Math.Min(remove, OrdinaryCopies);
        OrdinaryCopies -= fromOrdinary;
        remove -= fromOrdinary;
        _overclockCopies = Math.Max(0, _overclockCopies - remove);
    }

    internal async Task OnTurnStart(PlayerChoiceContext context, object token)
    {
        if (_overclockToken != null && !ReferenceEquals(_overclockToken, token))
            await OnTurnEnd(context, _overclockToken);
        // Inserted sessions themselves are not serialized. Discard any native
        // counter left by a saved OD bucket whose token no longer exists.
        int tracked = (int)Math.Min(999_999_999L, (long)OrdinaryCopies + _overclockCopies);
        if (Amount > tracked)
            await PowerCmd.ModifyAmount(context, this, tracked - Amount, Owner, null);
        if (token == null && OrdinaryCopies > 0 && OrdinaryTurnNumber != Owner.Player.PlayerCombatState.TurnNumber)
            await OnTurnEnd(context, null);
        await Refresh();
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this) && Owner?.Player?.PlayerCombatState != null)
            AlignCopies(OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player), Owner.Player.PlayerCombatState.TurnNumber);
        return Task.CompletedTask;
    }

    internal async Task OnTurnEnd(PlayerChoiceContext context, object token)
    {
        int expired;
        if (token == null)
        {
            expired = OrdinaryCopies;
            OrdinaryCopies = 0;
        }
        else
        {
            if (!ReferenceEquals(token, _overclockToken)) return;
            expired = _overclockCopies;
            _overclockCopies = 0;
            _overclockToken = null;
        }
        if (expired > 0 && Amount > 0)
            await PowerCmd.ModifyAmount(context, this, -Math.Min(expired, Amount), Owner, null);
        await Refresh();
    }

    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Player != null && side == Owner.Side && participants.Contains(Owner) &&
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) == null
            ? OnTurnEnd(context, null) : Task.CompletedTask;

    // Match native Dexterity's powered-block eligibility, but keep the
    // temporary contribution scoped to the active ordinary/inserted turn.
    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props,
        CardModel cardSource, CardPlay cardPlay)
    {
        if (HbrBattleStatsHelper.IsEnabled(Owner)) return 0m;
        if (cardSource != null ? cardSource.Owner?.Creature != Owner : target != Owner) return 0m;
        return props.IsPoweredCardOrMonsterMoveBlock() ? HotaruSeeYouAgainHelper.GetExtraDexterity(Owner?.Player) : 0m;
    }

    private async Task Refresh()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(HotaruSeeYouAgainPower));
        if (Owner?.CombatState == null) return;
        foreach (Creature ally in Owner.CombatState.Allies.ToArray())
            if (ally?.PetOwner == Owner.Player)
                await CombatPreviewRefreshHelper.RefreshCreatureIntent(ally);
    }
}
