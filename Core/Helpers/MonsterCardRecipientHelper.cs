using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MonsterCardRecipientHelper
{
    internal static bool IsPlayer(Creature target) => target?.Player != null;

    internal static IEnumerable<Creature> PlayerTargets(IEnumerable<Creature> targets) =>
        targets.Where(IsPlayer);

    internal static Task AddToPlayer<T>(
        Creature target,
        PileType pileType,
        int count,
        Player creator,
        CardPilePosition position) where T : CardModel =>
        PhantomPlayerBenefitContext.RecordStatus(target, count)
            ? Task.CompletedTask
            : IsPlayer(target)
            ? CardPileCmd.AddToCombatAndPreview<T>(target, pileType, count, creator, position)
            : Task.CompletedTask;

    internal static Task AddToPlayers<T>(
        IEnumerable<Creature> targets,
        PileType pileType,
        int count,
        Player creator,
        CardPilePosition position) where T : CardModel =>
        PhantomPlayerBenefitContext.RecordStatuses(targets, count)
            ? Task.CompletedTask
            : CardPileCmd.AddToCombatAndPreview<T>(
            PlayerTargets(targets), pileType, count, creator, position);
}
