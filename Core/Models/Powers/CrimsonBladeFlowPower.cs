using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class CrimsonBladeFlowPower : PowerModel
{
    public const int SoulOfAnchorEffectMultiplier = 2;

    private bool _putSwitchInHandAfterRemoved;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F85C5C");

    public static int GetSoulOfAnchorEffectMultiplier(Creature creature)
    {
        return creature?.HasPower<CrimsonBladeFlowPower>() == true ? SoulOfAnchorEffectMultiplier : 1;
    }

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
        await PowerCmd.Remove(Owner.GetPower<ReptureBowPower>());
        await PowerCmd.Remove(Owner.GetPower<ReptureDualSwordsPower>());
        await ModeTransitionHelper.ReplaceChihayaInLibraryWithSwitch(Owner, cardSource);
        await ModeTransitionHelper.MoveSwitchesOutOfHandWhileBlocked(Owner);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await ModeTransitionHelper.EnterDualSwordsAfterModeExit(oldOwner);
        if (_putSwitchInHandAfterRemoved)
        {
            await PutSwitchIntoHand(oldOwner, oldOwner.Player);
        }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card is SwitchHbrgoSaki && card.Owner?.Creature == Owner)
        {
            await ModeTransitionHelper.MoveSwitchesOutOfHandWhileBlocked(Owner);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Amount <= 0)
        {
            return;
        }

        bool willExpire = Amount <= 1;
        Creature owner = Owner;
        _putSwitchInHandAfterRemoved |= willExpire;
        await PowerCmd.ModifyAmount(choiceContext, this, -1m, owner, null);
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (amount <= 0m || target == null)
        {
            return 1m;
        }

        return dealer == Owner || target == Owner ? 2m : 1m;
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not DodgePower || target != Owner || amount <= 0m)
        {
            return false;
        }

        modifiedAmount = amount * 2m;
        return true;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _putSwitchInHandAfterRemoved = false;
    }

    private static async Task PutSwitchIntoHand(Creature owner, Player player)
    {
        if (ModeTransitionHelper.ShouldKeepSwitchOutOfHand(owner) ||
            player?.PlayerCombatState == null ||
            owner.CombatState == null)
        {
            return;
        }

        SwitchHbrgoSaki source = player.PlayerCombatState.DrawPile.Cards
            .OfType<SwitchHbrgoSaki>()
            .FirstOrDefault();
        if (source == null)
        {
            source = player.PlayerCombatState.AllCards
                .OfType<SwitchHbrgoSaki>()
                .FirstOrDefault(card => card.Pile?.Type != PileType.Hand);
        }

        if (source == null)
        {
            return;
        }

        SwitchHbrgoSaki switchCard = owner.CombatState.CreateCard<SwitchHbrgoSaki>(player);
        SyncUpgradeLevel(switchCard, source);
        switchCard.MakeNextPlayFree();

        await CardPileCmd.AddGeneratedCardToCombat(switchCard, PileType.Hand, player);
        if (!source.HasBeenRemovedFromState && source.Pile?.IsCombatPile == true)
        {
            await CardPileCmd.RemoveFromCombat(source, skipVisuals: true);
        }
    }

    private static void SyncUpgradeLevel(CardModel target, CardModel source)
    {
        while (target.CurrentUpgradeLevel < source.CurrentUpgradeLevel && target.IsUpgradable)
        {
            target.UpgradeInternal();
            target.FinalizeUpgradeInternal();
        }
    }
}
