using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HighSongPower : PowerModel
{
    public const int NormalMaxAmount = 3;

    public static int GetInitialAmount(Creature creature) =>
        LostStarSongSupportPower.GetInitialHighSongAmount(creature);

    public static int GetMaximumAmount(Creature creature) =>
        Math.Max(NormalMaxAmount, GetInitialAmount(creature));

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ReturnCardAtNextTurnStart { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public PileType ReturnCardPile { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ResetAtNextTurnStart { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public static int GetSongLevel(Creature creature)
    {
        return Math.Clamp(creature?.GetPower<HighSongPower>()?.Amount ?? 1, 1, NormalMaxAmount);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            return;
        }

        if (ReturnCardAtNextTurnStart || ResetAtNextTurnStart)
        {
            bool returnCard = ReturnCardAtNextTurnStart;
            ReturnCardAtNextTurnStart = false;
            ResetAtNextTurnStart = false;
            PileType returnPile = ReturnCardPile is
                PileType.Draw or PileType.Discard
                ? ReturnCardPile
                : PileType.Hand;
            ReturnCardPile = PileType.None;
            SetAmount(GetInitialAmount(Owner));
            Flash();
            if (returnCard) await EnsureCardInPile(Owner.Player, returnPile);
            return;
        }

        if (Amount < GetMaximumAmount(Owner))
        {
            Flash();
            await PowerCmd.ModifyAmount(choiceContext, this, 1m, Owner, null);
        }
    }

    public void ScheduleCardReturn(PileType pileType)
    {
        if (pileType is not (PileType.Draw or PileType.Hand or PileType.Discard))
        {
            throw new ArgumentOutOfRangeException(nameof(pileType), pileType, null);
        }

        AssertMutable();
        ResetAtNextTurnStart = false;
        ReturnCardAtNextTurnStart = true;
        ReturnCardPile = pileType;
    }

    public static async Task EnsureCardInHand(Player player)
    {
        await EnsureCardInPile(player, PileType.Hand);
    }

    private static async Task EnsureCardInPile(Player player, PileType pileType)
    {
        if (player?.PlayerCombatState == null ||
            player.PlayerCombatState.AllCards.Any(card =>
                card is HighSongHbrgoSaki &&
                !card.HasBeenRemovedFromState))
        {
            return;
        }

        HighSongHbrgoSaki card =
            player.Creature.CombatState.CreateCard<HighSongHbrgoSaki>(player);
        await CardPileCmd.AddGeneratedCardToCombat(card, pileType, player);
    }
}
