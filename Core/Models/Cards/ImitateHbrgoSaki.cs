using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ImitateHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/imitate_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust, CardKeyword.Ethereal };

    protected override bool IsPlayable => !HasManualCardPlayedThisTurn();

    public ImitateHbrgoSaki()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        foreach (CardPlayFinishedEntry entry in GetTeammateCardPlaysThisTurn(cardPlay.Target))
        {
            if (CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            CardModel copy = CreateOwnedCopy(entry.CardPlay.Card);
            try
            {
                await CardCmd.AutoPlay(
                    choiceContext,
                    copy,
                    GetCopyTarget(entry.CardPlay),
                    AutoPlayType.Default);
                await CombatFlowHelper.FinishAutoPlayedCard(copy);
            }
            finally
            {
                CardCopyContextHelper.Clear(copy);
                await RemoveCopyFromCombat(copy);
            }
        }

        if (!CombatManager.Instance.IsOverOrEnding)
        {
            PlayerCmd.EndTurn(Owner, canBackOut: false);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }

    private bool HasManualCardPlayedThisTurn()
    {
        ICombatState combatState = CombatState ?? Owner.Creature.CombatState;
        return CombatManager.Instance.History.CardPlaysStarted.Any(entry =>
            entry.HappenedThisTurn(combatState) &&
            entry.CardPlay.Card.Owner == Owner &&
            !entry.CardPlay.IsAutoPlay &&
            entry.CardPlay.IsFirstInSeries);
    }

    private IReadOnlyList<CardPlayFinishedEntry> GetTeammateCardPlaysThisTurn(Creature teammate)
    {
        ICombatState combatState = CombatState ?? Owner.Creature.CombatState;
        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry.HappenedThisTurn(combatState))
            .Where(entry => entry.CardPlay.IsFirstInSeries)
            .Where(entry => entry.CardPlay.Card is not ImitateHbrgoSaki)
            .Where(entry => entry.CardPlay.Card.Owner.Creature == teammate)
            .ToList();
    }

    private CardModel CreateOwnedCopy(CardModel source)
    {
        CardModel copy = CardModel.FromSerializable(source.ToSerializable());
        Owner.Creature.CombatState.AddCard(copy, Owner);
        CardCopyContextHelper.RecordCopy(copy);
        copy.AfterCreated();
        return copy;
    }

    private static async Task RemoveCopyFromCombat(CardModel copy)
    {
        if (copy.HasBeenRemovedFromState || copy.Pile == null || !copy.Pile.IsCombatPile)
        {
            return;
        }

        await CardPileCmd.RemoveFromCombat(copy, skipVisuals: true);
    }

    private Creature GetCopyTarget(CardPlay sourcePlay)
    {
        Creature target = sourcePlay.Target;
        if (target == null || !target.IsAlive)
        {
            return null;
        }

        return sourcePlay.Card.TargetType switch
        {
            TargetType.AnyEnemy when target.Side != Owner.Creature.Side => target,
            TargetType.AnyAlly when target.Side == Owner.Creature.Side => target,
            _ => null
        };
    }

}
