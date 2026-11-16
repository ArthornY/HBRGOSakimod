using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HisameCloseSlashCombatHelper
{
    static HisameCloseSlashCombatHelper() => ThirtyGProposalState.Changed += card => Invalidate(card?.Owner);
    private sealed class Flags { internal bool Dirty = true, Advice; internal Player Owner; internal int Count = -1; }
    private static readonly ConditionalWeakTable<CardPile, Flags> Decks = new();
    private sealed class Battle
    {
        internal readonly Dictionary<CardModel, decimal> Growth = new();
        internal readonly Dictionary<CardModel, HashSet<(int, object)>> EnergyTurns = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private sealed class Receipt : IDisposable
    {
        internal SlashIfCloseHbrgoSaki Card;
        internal decimal Growth;
        internal AttackCommand Command;
        internal HashSet<Creature> Targets;
        internal bool Gun, Broke, ConnectedGun, DrewSword, Active = true;
        internal Receipt Previous;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<Receipt> Current = new();
    private sealed class PreviewScope : IDisposable
    {
        internal CardModel Card;
        internal bool Gun, Active = true;
        internal PreviewScope Previous;
        public void Dispose() { Active = false; if (ReferenceEquals(Preview.Value, this)) Preview.Value = Previous; }
    }
    private static readonly AsyncLocal<PreviewScope> Preview = new();
    internal static IDisposable BeginPreview(CardModel card, bool gun)
    {
        PreviewScope scope = new() { Card = card, Gun = gun, Previous = Preview.Value };
        Preview.Value = scope; return scope;
    }
    private static bool UsesGun(CardModel card, Creature target) => Preview.Value is { Active: true } scope &&
        ReferenceEquals(scope.Card, card) ? scope.Gun : HbrEnemyCancerHelper.IsCancer(target);
    internal static void Invalidate(Player player)
    { if (player?.Deck is { } deck && Decks.TryGetValue(deck, out var flags)) flags.Dirty = true; }
    internal static bool HasMastersAdvice(Player player)
    {
        if (!HbrBattleStatsHelper.IsEnabled(player) || player.Deck is not { } deck) return false;
        Flags flags = Decks.GetValue(deck, pile => { var entry = new Flags(); pile.ContentsChanged += () => entry.Dirty = true; return entry; });
        if (flags.Dirty || flags.Count != deck.Cards.Count || !ReferenceEquals(flags.Owner, player))
        {
            flags.Advice = deck.Cards.OfType<SlashIfCloseHbrgoSaki>().Any(card => card.Owner == player &&
                !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.MastersAdvice));
            flags.Owner = player; flags.Count = deck.Cards.Count; flags.Dirty = false;
        }
        return flags.Advice;
    }
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
    private static decimal Growth(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return 0m;
        return Battles.GetOrCreateValue(combat).Growth.TryGetValue(Physical(card), out decimal amount) ? amount : 0m;
    }
    private static Receipt Find(CardModel source) => Current.Value is { Active: true } receipt && ReferenceEquals(receipt.Card, source) ? receipt : null;
    internal static decimal GetBranchUnits(CardModel source, bool gun) => HbrSkillPower.DamageUnits(
        DamageAmplificationLimits.AddSaturated(gun ? 60m : 325m, Find(source)?.Growth ?? Growth(source)));
    internal static decimal GetAttackUnits(CardModel source, Creature target) => GetBranchUnits(source, target == null || HbrEnemyCancerHelper.IsCancer(target));
    internal static int GetHitCount(CardModel source, Creature target) => target == null || HbrEnemyCancerHelper.IsCancer(target) ? 2 : 1;
    private static bool OwnCommand(Receipt receipt) => receipt?.Active == true && receipt.Command != null &&
        ReferenceEquals(receipt.Command, HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command);
    private static bool IsOwnDamageOrPreview(CardModel source)
    {
        if (HotaruCannonStatsHelper.IsDamagePreview || Preview.Value is { Active: true } preview && ReferenceEquals(preview.Card, source)) return true;
        // A callback can retain this card/dealer/Move inside the outer attack.
        // Only the primary canonical damage scope belongs to this branch.
        if (Find(source)?.Command is { } command) return HighHitDamageVisualHelper.IsDirectAttackDamage(command);
        return !StrongestShieldDamagePatch.IsResolvingActualDamage;
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is SlashIfCloseHbrgoSaki && source.IsMutable && source.Owner?.Creature == dealer && dealer != null &&
        target != null && target.Side != dealer.Side && props.IsPoweredAttack() &&
        !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source) &&
        IsOwnDamageOrPreview(source);
    internal static bool ForcesCritical(Creature target, Creature dealer, CardModel card, ValueProp props) =>
        Applies(card, dealer, target, props) && !UsesGun(card, target) &&
        ((SlashIfCloseHbrgoSaki)card).HasUpgradeEffect(SlashIfCloseHbrgoSaki.MindsEye);
    private static Creature SourceActor(CardModel source) => source?.IsMutable == true ? source.Owner?.Creature : null;
    internal static decimal GetHpDamageMultiplier(CardModel source, Creature target) =>
        Applies(source, SourceActor(source), target, ValueProp.Move) && !UsesGun(source, target) &&
        !RanaByakkoCombatHelper.HasDp(target) ? ((SlashIfCloseHbrgoSaki)source).HasUpgradeEffect(SlashIfCloseHbrgoSaki.FullForce) ? 2m : 1.5m : 1m;
    internal static decimal GetDpDamageMultiplier(CardModel source, Creature target) =>
        Applies(source, SourceActor(source), target, ValueProp.Move) && UsesGun(source, target) &&
        RanaByakkoCombatHelper.HasDp(target) ? 1.3m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? (UsesGun(source, target) ? 1m : 2.1m) /
            HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static void OnEnemyBroken(Creature dealer, Creature target, CardModel source, ValueProp props, bool alreadyBroken)
    {
        Receipt receipt = Find(source);
        if (!alreadyBroken && OwnCommand(receipt) && HighHitDamageVisualHelper.IsDirectAttackDamage(receipt.Command) &&
            Applies(source, dealer, target, props)) receipt.Broke = true;
    }
    internal static IReadOnlyList<Creature> FilterTargets(AttackCommand command, IReadOnlyList<Creature> native)
    {
        Receipt receipt = Current.Value;
        return receipt?.Active == true && ReferenceEquals(receipt.Command, command) && receipt.Targets != null
            ? native.Where(receipt.Targets.Contains).ToArray() : native;
    }
    internal static async Task Play(PlayerChoiceContext context, SlashIfCloseHbrgoSaki card, CardPlay play)
    {
        Creature actor = card.Owner.Creature;
        Creature[] enemies = actor.CombatState.GetOpponentsOf(actor).Where(enemy => enemy.IsHittable).ToArray();
        Creature[] gun = enemies.Where(HbrEnemyCancerHelper.IsCancer).ToArray();
        Creature[] sword = enemies.Where(enemy => !HbrEnemyCancerHelper.IsCancer(enemy)).ToArray();
        using Receipt receipt = new() { Card = card, Growth = Growth(card), Previous = Current.Value };
        Current.Value = receipt;
        if (gun.Length > 0 && card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.MindsEye))
            await HbrEffectApplication.ApplyMindsEye(context, actor, actor, card);
        // Reserve once for the whole card. Both native species branches share
        // this logical attack, including Count effects and virtual combo hits.
        using var logical = HbrComboHelper.BeginCardAttack(card);
        async Task Attack(Creature[] targets, bool isGun)
        {
            if (!targets.Any(target => target.IsHittable)) return;
            if (!isGun && card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.KillingIntent))
                using (HbrDebuffApplicationScope.BeginBatch(actor))
                    foreach (Creature enemy in actor.CombatState.GetOpponentsOf(actor).Where(enemy => enemy.IsHittable).ToArray())
                        await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, enemy, 20m, actor, card,
                            new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
            receipt.Gun = isGun; receipt.Targets = targets.ToHashSet();
            var command = DamageCmd.Attack(GetBranchUnits(card, isGun)).FromCardCompat(card, play)
                .TargetingAllOpponents(actor.CombatState).WithHitCount(logical?.HitCount(isGun ? 2 : 1) ?? (isGun ? 2 : 1))
                .WithHitFx("vfx/vfx_attack_slash");
            receipt.Command = command;
            if (!isGun) receipt.DrewSword = true;
            try
            {
                await command.Execute(context);
                logical?.Record(command);
                if (isGun && command.Results.SelectMany(results => results).Any(result =>
                        receipt.Targets.Contains(result.Receiver) && CombatAttackHitHelper.IsConnected(result))) receipt.ConnectedGun = true;
            }
            finally { receipt.Command = null; receipt.Targets = null; }
        }
        await Attack(gun, true);
        await Attack(sword, false);
        if (logical != null) await logical.Complete(context);
        if (card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.KillingIntent))
        {
            var duration = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
            using (HbrDebuffApplicationScope.BeginBatch(actor))
                foreach (Creature enemy in gun.Where(enemy => enemy.IsHittable))
                    await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 30m, actor, card, duration);
        }
        if (receipt.ConnectedGun && card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.FullForce)) card.Owner.GetRelic<Overclock>()?.GainLevelProgress(15m);
        Battle battle = Battles.GetOrCreateValue(actor.CombatState);
        CardModel physical = Physical(card);
        if (receipt.Broke && card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.Desire))
        {
            if (!battle.EnergyTurns.TryGetValue(physical, out var turns)) battle.EnergyTurns.Add(physical, turns = new());
            if (turns.Add((card.Owner.PlayerCombatState?.TurnNumber ?? 0, OverclockImmediateTurnCoordinator.GetTurnToken(card.Owner))))
                await PlayerCmd.GainEnergy(1m, card.Owner);
        }
        if (card.HasUpgradeEffect(SlashIfCloseHbrgoSaki.StillGrowing))
            battle.Growth[physical] = DamageAmplificationLimits.AddSaturated(Growth(card), 15m);
    }
}
