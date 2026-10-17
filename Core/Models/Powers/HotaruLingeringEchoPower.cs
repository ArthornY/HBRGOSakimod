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
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_LINGERING_ECHO_POWER")]
public sealed class HotaruLingeringEchoPower : PowerModel, ICustomPower
{
    private const int Maximum = 999_999_999;
    private Dictionary<object, int> _reservations = new();

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F8D36A");
    public override int DisplayAmount => IsMutable ? PendingPoints : 40;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("PendingPoints", IsMutable ? PendingPoints : 40);
            return text;
        }
    }

    private bool CanResolve => IsMutable && Owner?.IsAlive == true &&
        Owner.CombatState != null && Owner.Player?.PlayerCombatState != null &&
        !CombatManager.Instance.IsOverOrEnding;

    // The native power amount is the saved source of truth. Reservations keep
    // a nested card from reusing the outer card's bonus, without turn expiry.
    private int PendingPoints => !CanResolve ? 0 :
        (int)Math.Clamp((long)Amount - _reservations.Values.Sum(value => (long)value), 0L, Maximum);

    internal static int GetProjectedBonus(Player player) =>
        player?.Creature?.GetPower<HotaruLingeringEchoPower>()?.PendingPoints ?? 0;

    internal int GetRollBonus(CardModel card)
    {
        if (card?.Owner?.Creature != Owner || !CanResolve) return 0;
        object token = CardPlayContextHelper.GetCurrentPlayToken(card) ?? card;
        if (_reservations.TryGetValue(token, out int reserved)) return reserved;
        int points = PendingPoints;
        // Reserve zero too: a bonus added during this card's resolution must
        // survive for the next card, even if the current card had no bonus.
        _reservations.Add(token, points);
        if (points > 0) Flash();
        Refresh();
        return points;
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        // Non-rolling and automatic cards still occupy the next-card slot.
        GetRollBonus(play?.Card);
        return Task.CompletedTask;
    }

    internal static async Task Apply(PlayerChoiceContext context, Creature owner, decimal amount, CardModel source)
    {
        int points = (int)Math.Clamp(decimal.Floor(amount), 0m, Maximum);
        if (points <= 0 || owner?.IsAlive != true || owner.Player?.PlayerCombatState == null ||
            owner.CombatState == null || CombatManager.Instance.IsOverOrEnding) return;
        // Let the native command handle blocked/amplified applications. No
        // parallel counter can retain points that the command did not grant.
        await PowerCmd.Apply<HotaruLingeringEchoPower>(context, owner, points, owner, source);
        owner.GetPower<HotaruLingeringEchoPower>()?.Refresh();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play?.Card?.Owner?.Creature != Owner) return;
        object token = CardPlayContextHelper.GetCurrentPlayToken(play.Card) ?? play.Card;
        if (!_reservations.Remove(token, out int points)) return;
        if (points > 0 && Amount > 0)
            await PowerCmd.ModifyAmount(context, this, -Math.Min(points, Amount), Owner, null);
        Refresh();
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source)
    {
        if (ReferenceEquals(power, this)) Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(HotaruLingeringEchoPower));
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservations = new();
    }
}
