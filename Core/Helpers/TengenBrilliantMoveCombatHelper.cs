using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class TengenBrilliantMoveCombatHelper
{
    private sealed class Battle
    {
        internal readonly HashSet<Creature> FinalDeaths = new();
        internal readonly List<WeakReference<TengenBrilliantMoveHbrgoSaki>> Cards = new();
    }
    internal sealed record GrowthGroup(TengenBrilliantMoveHbrgoSaki Physical, TengenBrilliantMoveHbrgoSaki[] Mirrors);
    internal sealed record DeathReceipt(ICombatState Combat, Creature Target, bool WasMinion, GrowthGroup[] Groups,
        Player[] DyeOwners, KuraSatomiCombatHelper.Receipt KuraAttack);
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static bool IsReal(TengenBrilliantMoveHbrgoSaki card, ICombatState combat) =>
        card?.IsMutable == true && !card.HasBeenRemovedFromState && card.Owner?.Creature?.CombatState == combat &&
        (card.Owner.Deck?.Cards.Contains(card) == true || combat.ContainsCard(card));
    private static TengenBrilliantMoveHbrgoSaki Physical(TengenBrilliantMoveHbrgoSaki card)
    {
        if (card.IsDupe || CardCopyContextHelper.IsMarkedCopy(card)) return card;
        var origin = card.DeckVersion as TengenBrilliantMoveHbrgoSaki;
        return origin != null && !origin.HasBeenRemovedFromState && origin.Owner == card.Owner &&
            card.Owner?.Deck?.Cards.Contains(origin) == true ? origin : card;
    }
    internal static void Track(TengenBrilliantMoveHbrgoSaki card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return;
        var cards = Battles.GetOrCreateValue(combat).Cards;
        if (!cards.Any(reference => reference.TryGetTarget(out var known) && ReferenceEquals(known, card))) cards.Add(new(card));
    }
    internal static void Synchronize(TengenBrilliantMoveHbrgoSaki card)
    {
        if (card?.IsMutable != true) return;
        var physical = Physical(card);
        if (!ReferenceEquals(physical, card)) card.SetGrowth(physical.GrowthPercent);
        else card.SyncDamage();
        Track(card);
    }
    internal static DeathReceipt CaptureDeath(ICombatState combat, Creature target, bool prevented)
    {
        if (combat == null || target?.Side != CombatSide.Enemy || prevented) return null;
        bool minion = target.HasPower<MinionPower>();
        Battle battle = Battles.GetOrCreateValue(combat);
        if (battle.FinalDeaths.Contains(target)) return null;
        battle.Cards.RemoveAll(reference => !reference.TryGetTarget(out var card) || card.HasBeenRemovedFromState);
        var tracked = battle.Cards.Select(reference => reference.TryGetTarget(out var card) ? card : null);
        var cards = combat.Players.SelectMany(player =>
                (player.Deck?.Cards ?? Enumerable.Empty<CardModel>()).Concat(player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>()))
            .OfType<TengenBrilliantMoveHbrgoSaki>().Concat(tracked).Where(card => IsReal(card, combat)).Distinct().ToArray();
        GrowthGroup[] groups = minion ? Array.Empty<GrowthGroup>() : cards.GroupBy(Physical)
            .Where(group => group.Key.HasUpgradeEffect(TengenBrilliantMoveHbrgoSaki.FiftyWins))
            .Select(group => new GrowthGroup(group.Key, group.Append(group.Key).Distinct().ToArray())).ToArray();
        return new(combat, target, minion, groups, minion ? Array.Empty<Player>() : IrohaDyeCombatHelper.CaptureTombstoneOwners(combat),
            KuraSatomiCombatHelper.CaptureDeath(target));
    }
    internal static void ConfirmDeath(DeathReceipt receipt)
    {
        if (receipt == null || receipt.Target.Side != CombatSide.Enemy ||
            LargeCreatureHealthHelper.GetCurrentHp(receipt.Target) > 0m ||
            !PhantomHelper.CountsAsFinalDefeat(receipt.Target)) return;
        if (!Battles.GetOrCreateValue(receipt.Combat).FinalDeaths.Add(receipt.Target)) return;
        // Kura harvests actual final kills including minions; the existing
        // permanent-growth and dye rewards retain their no-minion rule.
        KuraSatomiCombatHelper.OnFinalDeath(receipt.KuraAttack, receipt.Target);
        if (receipt.WasMinion || receipt.Target.HasPower<MinionPower>()) return;
        foreach (GrowthGroup group in receipt.Groups)
        {
            decimal growth = group.Physical.GrowthPercent + 1m;
            foreach (var card in group.Mirrors) card.SetGrowth(growth);
        }
        IrohaDyeCombatHelper.OnFinalDeath(receipt.DyeOwners);
    }
    internal static bool IsOverdrive(CardModel card) => card?.IsMutable == true &&
        OverclockImmediateTurnCoordinator.IsActiveFor(card.Owner);
    internal static bool IsFirstOrdinaryTurn(Player player) => player?.PlayerCombatState?.TurnNumber == 1 &&
        !OverclockImmediateTurnCoordinator.IsActiveFor(player) && !PersonalImmediateTurnCoordinator.IsActiveFor(player);
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is TengenBrilliantMoveHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && ((TengenBrilliantMoveHbrgoSaki)source).HasUpgradeEffect(TengenBrilliantMoveHbrgoSaki.StoneTower) &&
        IsOverdrive(source) ? 100m : 0m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? 1m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
}
