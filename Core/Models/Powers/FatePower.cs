using HBRGOSakiMod.Core.Helpers;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FatePower : PowerModel
{
    private const int MaxHpLossPerDraw = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("ResourceLossPerStack", IsMutable
                ? HbrResourceValueHelper.Fixed(Owner, MaxHpLossPerDraw)
                : MaxHpLossPerDraw * HbrResourceValueHelper.FixedResourceScale);
            return text;
        }
    }


    public override Color AmountLabelColor => new("F8D36A");

    public override decimal ModifyHandDraw(
        Player player,
        decimal count)
    {
        return player?.Creature == Owner
            ? count + Amount
            : count;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay?.Card?.Owner?.Creature != Owner)
        {
            return;
        }

        Creature target = cardPlay.Target;
        if (target?.Side == Owner.Side ||
            target?.IsHittable != true)
        {
            target = Owner.CombatState
                .GetOpponentsOf(Owner)
                .FirstOrDefault(creature => creature.IsHittable);
        }

        if (target == null)
        {
            return;
        }

        Flash();
        await CreatureCmd.LoseMaxHp(
            choiceContext,
            target,
            HbrResourceValueHelper.Fixed(Owner, Amount * MaxHpLossPerDraw),
            isFromCard: false);
    }
}
