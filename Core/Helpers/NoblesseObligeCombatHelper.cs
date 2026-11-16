using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NoblesseObligeCombatHelper
{
    private sealed class Battle
    {
        internal readonly List<WeakReference<NoblesseObligeHbrgoSaki>> Cards = new();
        internal readonly Dictionary<Player, Dictionary<Creature, (PowerModel Rate, PowerModel Damage)>> Azure = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static NoblesseObligeHbrgoSaki Physical(NoblesseObligeHbrgoSaki card)
    {
        if (card.IsDupe || CardCopyContextHelper.IsMarkedCopy(card)) return card;
        return card.DeckVersion is NoblesseObligeHbrgoSaki origin && !origin.HasBeenRemovedFromState &&
            origin.Owner == card.Owner && card.Owner?.Deck?.Cards.Contains(origin) == true ? origin : card;
    }
    internal static void Track(NoblesseObligeHbrgoSaki card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return;
        var cards = Battles.GetOrCreateValue(combat).Cards;
        if (!cards.Any(reference => reference.TryGetTarget(out var item) && ReferenceEquals(item, card))) cards.Add(new(card));
    }
    internal static void Synchronize(NoblesseObligeHbrgoSaki card)
    {
        if (card?.IsMutable != true) return;
        var physical = Physical(card);
        if (!ReferenceEquals(physical, card)) card.SetGrowth(physical.GrowthPercent);
        else card.SyncDamage();
        Track(card);
    }
    internal static bool HasMoonlight(Player player) => HbrBattleStatsHelper.IsEnabled(player) &&
        player.Deck?.Cards.OfType<NoblesseObligeHbrgoSaki>().Any(card => card.Owner == player &&
            !card.HasBeenRemovedFromState && card.HasUpgradeEffect(NoblesseObligeHbrgoSaki.Moonlight)) == true;
    internal static async Task ApplyAzureIris(PlayerChoiceContext context, NoblesseObligeHbrgoSaki source)
    {
        var battle = Battles.GetOrCreateValue(source.Owner.Creature.CombatState);
        if (!battle.Azure.TryGetValue(source.Owner, out var recorded)) battle.Azure.Add(source.Owner, recorded = new());
        foreach (Creature ally in HbrPlayerTeamTargets.Get(source.Owner.Creature))
        {
            if (recorded.TryGetValue(ally, out var previous))
            {
                if (previous.Rate != null && ally.Powers.Contains(previous.Rate)) await PowerCmd.Remove(previous.Rate);
                if (previous.Damage != null && ally.Powers.Contains(previous.Damage)) await PowerCmd.Remove(previous.Damage);
            }
            var before = ally.Powers.ToHashSet();
            var profile = new HbrEffectProfile(HbrEffectLimit.Only, HbrEffectExit.PlayerTurnEnd, 5);
            await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, ally, 100m, source.Owner.Creature, source, profile: profile);
            await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, ally, 130m, source.Owner.Creature, source, profile: profile);
            recorded[ally] = (ally.Powers.OfType<HbrCriticalRateUpPower>().FirstOrDefault(power => !before.Contains(power)),
                ally.Powers.OfType<HbrCriticalDamageUpPower>().FirstOrDefault(power => !before.Contains(power)));
        }
    }
    // Called exactly once by the confirmed 30G proposal transaction. Neither
    // preview, target selection, a rejected proposal nor old proposals call it.
    internal static void OnProposal(Player player, CardModel target)
    {
        if (!HbrBattleStatsHelper.IsEnabled(player) || target?.IsMutable != true || target.Owner != player ||
            target is NoblesseObligeHbrgoSaki) return;
        ICombatState combat = player.Creature?.CombatState;
        var tracked = Enumerable.Empty<NoblesseObligeHbrgoSaki>();
        if (combat != null && Battles.TryGetValue(combat, out Battle battle))
        {
            battle.Cards.RemoveAll(reference => !reference.TryGetTarget(out var card) || card.HasBeenRemovedFromState);
            tracked = battle.Cards.Select(reference => reference.TryGetTarget(out var card) ? card : null);
        }
        var cards = (player.Deck?.Cards ?? Enumerable.Empty<CardModel>())
            .Concat(player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>())
            .OfType<NoblesseObligeHbrgoSaki>().Concat(tracked)
            .Where(card => card?.IsMutable == true && card.Owner == player && !card.HasBeenRemovedFromState &&
                (player.Deck.Cards.Contains(card) || combat?.ContainsCard(card) == true ||
                    !ReferenceEquals(Physical(card), card))).Distinct().ToArray();
        foreach (var group in cards.GroupBy(Physical).Where(group => group.Key.HasUpgradeEffect(NoblesseObligeHbrgoSaki.Courage)))
        {
            var growth = group.Key.GrowthPercent + 40;
            foreach (var card in group.Append(group.Key).Distinct()) card.SetGrowth(growth);
        }
        if (combat != null && CombatManager.Instance.StateTracker != null)
            CombatPreviewRefreshHelper.RefreshCombatState(player.Creature, nameof(OnProposal));
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is NoblesseObligeHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && target != null && target.Side != dealer.Side &&
        !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source);
    internal static decimal GetHpDamageMultiplier(CardModel source, Creature target) =>
        source is NoblesseObligeHbrgoSaki && source.IsMutable && Applies(source, source.Owner?.Creature, target, ValueProp.Move) &&
        !RanaByakkoCombatHelper.HasDp(target) ? 1.5m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? (((NoblesseObligeHbrgoSaki)source).HasUpgradeEffect(NoblesseObligeHbrgoSaki.ThunderJudgment)
            ? 18m : 3.6m) / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
}
