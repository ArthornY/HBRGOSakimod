using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class AkariAceCombatHelper
{
    private sealed class Battle { internal readonly HashSet<Player> Started = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<Receipt> Current = new();
    internal sealed class Receipt : IDisposable
    {
        internal AcePlayerIsNotMeHbrgoSaki Card;
        internal Receipt Previous;
        internal bool Active = true, BreakAwarded;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    internal static Receipt Begin(AcePlayerIsNotMeHbrgoSaki card)
    { Receipt value = new() { Card = card, Previous = Current.Value }; Current.Value = value; return value; }
    internal static async Task OnCombatStart(ICombatState combat)
    {
        if (combat == null) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        foreach (Player player in combat.Players)
        {
            if (!battle.Started.Add(player)) continue;
            var card = (player.Deck?.Cards ?? Enumerable.Empty<CardModel>())
                .Concat(player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>())
                .OfType<AcePlayerIsNotMeHbrgoSaki>().FirstOrDefault(card => card.Owner == player &&
                    !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(AcePlayerIsNotMeHbrgoSaki.LateBloomer));
            if (card == null) continue;
            await HbrEffectApplication.Apply<HbrNegativityPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                1m, player.Creature, card, remaining: 5);
        }
    }
    internal static bool IsLateBloomerActive(AcePlayerIsNotMeHbrgoSaki card) =>
        card.HasUpgradeEffect(AcePlayerIsNotMeHbrgoSaki.LateBloomer) && card.IsMutable && card.Owner != null &&
        !card.Owner.Creature.Powers.OfType<HbrNegativityPower>().Any(power => power.Amount > 0);
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is AcePlayerIsNotMeHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static decimal GetConditionalDamageMultiplier(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 1m;
        return (RanaByakkoCombatHelper.HasDp(target) ? 1.3m : 1m) *
            (IsLateBloomerActive((AcePlayerIsNotMeHbrgoSaki)source) ? 1.5m : 1m);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? (IsLateBloomerActive((AcePlayerIsNotMeHbrgoSaki)source) ? 3m : 1m) /
            HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static void OnEnemyBroken(Creature dealer, Creature target, CardModel source, ValueProp props, bool alreadyBroken)
    {
        if (alreadyBroken || !Applies(source, dealer, target, props) ||
            !((AcePlayerIsNotMeHbrgoSaki)source).HasUpgradeEffect(AcePlayerIsNotMeHbrgoSaki.SorrowSnowdrop)) return;
        for (Receipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && ReferenceEquals(receipt.Card, source))
            {
                if (!receipt.BreakAwarded)
                { receipt.BreakAwarded = true; source.Owner.GetRelic<Overclock>()?.GainLevelProgress(150m); }
                return;
            }
    }
}
