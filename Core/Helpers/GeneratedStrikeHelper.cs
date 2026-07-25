using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

internal static class GeneratedStrikeHelper
{
    public static async Task Play(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature target,
        decimal damage)
    {
        if (owner?.Creature?.CombatState == null || target == null || !target.IsHittable)
        {
            return;
        }

        StrikeHbrgoSaki strike =
            owner.Creature.CombatState.CreateCard<StrikeHbrgoSaki>(owner);
        strike.DynamicVars.Damage.BaseValue = damage;

        await CardCmd.AutoPlay(
            choiceContext,
            strike,
            target,
            AutoPlayType.Default,
            skipCardPileVisuals: true);
        await CombatFlowHelper.FinishAutoPlayedCard(strike);
    }

    public static async Task PlayAll(
        PlayerChoiceContext choiceContext,
        Player owner,
        decimal damage)
    {
        Creature target = owner?.Creature?.CombatState?
            .GetOpponentsOf(owner.Creature)
            .FirstOrDefault(enemy => enemy.IsHittable);
        if (target == null)
        {
            return;
        }

        StrikeHbrgoSaki strike =
            owner.Creature.CombatState.CreateCard<StrikeHbrgoSaki>(owner);
        strike.DynamicVars.Damage.BaseValue = damage;
        strike.TargetsAllOpponents = true;

        await CardCmd.AutoPlay(
            choiceContext,
            strike,
            target,
            AutoPlayType.Default,
            skipCardPileVisuals: true);
        await CombatFlowHelper.FinishAutoPlayedCard(strike);
    }
}
