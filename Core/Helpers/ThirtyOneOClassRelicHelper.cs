using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneOClassRelicHelper
{
    internal static bool IsEnabled(Player player) => player?.Character is HbrgoSaki31O &&
        HbrBattleStatsHelper.IsEnabled(player);

    private static bool IsInCombat(Player player) => IsEnabled(player) &&
        player.Creature is { IsAlive: true, CombatState: not null } && player.PlayerCombatState != null;

    // Called exactly once for the selected real roll, before any queued reward
    // changes Charge and therefore the critical range of a later roll.
    internal static void QueueResolvedRoll(Player player, SoulRollBreakdown roll)
    {
        if (!IsInCombat(player) || roll == null) return;
        bool critical = RollKeywordRules.IsCriticalRoll(player, roll);
        if (roll.BaseRoll is > 0 and <= RollKeywordRules.RollMaximum && roll.BaseRoll % 7 == 0)
            player.GetRelic<MarkOfSeven>()?.QueueCharge();
        if (critical) player.GetRelic<ScoreOfHaruhikage>()?.QueueCriticalReward();
    }

    internal static async Task ResolvePendingRollRewards(PlayerChoiceContext context, Player player)
    {
        if (!IsInCombat(player)) return;
        if (player.GetRelic<MarkOfSeven>() is { } mark) await mark.ResolvePending(context);
        if (player.GetRelic<ScoreOfHaruhikage>() is { } score) await score.ResolvePending(context);
    }

    internal static bool IsFormCard(CardModel card) => card != null &&
        (ModeTransitionHelper.IsBowRestrictedCard(card) || ModeTransitionHelper.IsDualSwordsRestrictedCard(card));

    internal static bool HasLuckySevenBonus(Player player) => IsEnabled(player) &&
        player.GetRelic<SakuraSwordTassel>() is { IsMelted: false };

    internal static bool HasLuckySevenBonus(AttackCommand command) =>
        command?.ModelSource is MagicSwordLuckySevenHbrgoSaki card && card.IsMutable &&
        command.Attacker == card.Owner?.Creature && command.DamageProps.HasFlag(ValueProp.Move) &&
        !command.DamageProps.HasFlag(ValueProp.Unpowered) && HasLuckySevenBonus(card.Owner);
}
