using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

internal static class RisaRecoveryCombatHelper
{
    private sealed class Battle { internal readonly ConditionalWeakTable<Player, Receipt> Players = new(); }
    private sealed class Receipt { internal bool Applied; }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    internal static Creature[] Targets(Player player) => TeamCombatHelper.GetLivingAllies(player?.Creature)
        .Where(target => target.Player != null || target.HasPower<PhantomPower>() || target.HasPower<KozueSpiritPower>()).ToArray();
    internal static async Task<int> Cleanse(PlayerChoiceContext context, Creature target)
    {
        if (target?.IsAlive != true) return 0;
        var choices = target.Powers.Where(power => power.IsVisible && power.Amount != 0 &&
            power.GetTypeForAmount(power.Amount) == PowerType.Debuff && PowerDispelRules.CanPlayerDispel(power)).Take(3).ToArray();
        int removed = 0;
        foreach (var power in choices)
            if (await PowerDispelRules.TryPlayerDispel(power)) removed++;
        return removed;
    }
    internal static async Task BeforeCombatStart(Player player)
    {
        if (player?.Creature is not { IsAlive: true, CombatState: { } combat } owner ||
            !HbrBattleStatsHelper.IsEnabled(player)) return;
        AlwaysKeepYourChinUpHbrgoSaki source = player.Deck?.Cards.OfType<AlwaysKeepYourChinUpHbrgoSaki>()
            .FirstOrDefault(card => ReferenceEquals(card.Owner, player) && !card.HasBeenRemovedFromState &&
                !ThirtyGProposalState.IsDisabled(card) &&
                card.HasUpgradeEffect(AlwaysKeepYourChinUpHbrgoSaki.HeartfeltHospitality));
        if (source == null) return;
        Receipt receipt = Battles.GetOrCreateValue(combat).Players.GetOrCreateValue(player);
        if (receipt.Applied) return;
        receipt.Applied = true;
        foreach (Creature target in Targets(player).Where(target => ReferenceEquals(target, owner) || ReferenceEquals(target.PetOwner, player)))
            await SeiraDpRecoveryHelper.Restore(new BlockingPlayerChoiceContext(), target, .1m, true, source);
    }
}
