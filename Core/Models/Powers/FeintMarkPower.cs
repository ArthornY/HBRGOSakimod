using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FeintMarkPower : PowerModel
{
    private static readonly object _dualSwordsEligibleStrikesLock = new();
    private static readonly HashSet<CardModel> _dualSwordsEligibleStrikes = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Godot.Color AmountLabelColor => new("F8D36A");

    public static bool IsDualSwordsEligibleStrike(CardModel card)
    {
        lock (_dualSwordsEligibleStrikesLock)
        {
            return _dualSwordsEligibleStrikes.Contains(card);
        }
    }

    public override async Task BeforeAttack(AttackCommand command)
    {
        Creature attacker = command.Attacker;
        if (Amount <= 0 || attacker == null || attacker.Side == Owner.Side || command.TargetSide != Owner.Side || attacker.IsDead || !command.DamageProps.IsPoweredAttack())
        {
            return;
        }

        if (Owner.Player == null || Owner.IsDead)
        {
            return;
        }

        PlayerChoiceContext choiceContext = new BlockingPlayerChoiceContext();
        await PowerCmd.ModifyAmount(choiceContext, this, -1m, Owner, null);

        if (Owner.IsDead || attacker.IsDead)
        {
            return;
        }

        CardModel strike = Owner.CombatState.CreateCard<StrikeHbrgoSaki>(Owner.Player);
        strike.ExhaustOnNextPlay = true;
        AddDualSwordsEligibleStrike(strike);
        try
        {
            await CardCmd.AutoPlay(choiceContext, strike, attacker, AutoPlayType.Default, skipCardPileVisuals: true);
            await CombatFlowHelper.FinishAutoPlayedCard(strike, checkWinCondition: false);
        }
        finally
        {
            RemoveDualSwordsEligibleStrike(strike);
        }
    }

    private static void AddDualSwordsEligibleStrike(CardModel card)
    {
        lock (_dualSwordsEligibleStrikesLock)
        {
            _dualSwordsEligibleStrikes.Add(card);
        }
    }

    private static void RemoveDualSwordsEligibleStrike(CardModel card)
    {
        lock (_dualSwordsEligibleStrikesLock)
        {
            _dualSwordsEligibleStrikes.Remove(card);
        }
    }
}
