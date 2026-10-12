using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class DeflectorProtection
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasLostDpThisCombat { get; private set; }

    private void RegisterMatchaDpLoss(int amount)
    {
        if (amount <= 0 || HasLostDpThisCombat || Owner.Creature.CombatState == null ||
            Owner.PlayerCombatState == null || CombatManager.Instance.IsOverOrEnding)
            return;

        // The first actual loss belongs to the battle, even if this card is in
        // hand/exhaust or has not been acquired yet. Later losses cannot retry it.
        HasLostDpThisCombat = true;
        foreach (MatchaParfaitHbrgoSaki card in Owner.PlayerCombatState.AllCards.OfType<MatchaParfaitHbrgoSaki>())
        {
            if (!card.IsDupe && card.HasUpgradeEffect(6) &&
                card.Pile?.Type is PileType.Draw or PileType.Discard)
                card.QueueFirstDpLossReturn();
        }
    }

    private async Task ResolveMatchaDpLossReturn()
    {
        if (Owner.PlayerCombatState == null) return;
        MatchaParfaitHbrgoSaki[] pending = Owner.PlayerCombatState.AllCards
            .OfType<MatchaParfaitHbrgoSaki>().Where(card => card.PendingFirstDpLossReturn).ToArray();
        foreach (MatchaParfaitHbrgoSaki card in pending)
        {
            // Consume before moving: pile-change effects may themselves lose DP.
            if (!card.ConsumeFirstDpLossReturn() || card.IsDupe || !card.HasUpgradeEffect(6) ||
                !ReferenceEquals(card.Owner, Owner) || Owner.Creature.IsDead ||
                CombatManager.Instance.IsOverOrEnding ||
                card.Pile?.Type is not (PileType.Draw or PileType.Discard))
                continue;
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature dealer, CardModel cardSource) =>
        target == Owner.Creature ? ResolveMatchaDpLossReturn() : Task.CompletedTask;
}
