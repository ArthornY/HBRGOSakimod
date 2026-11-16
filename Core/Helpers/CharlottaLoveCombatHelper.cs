using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CharlottaLoveCombatHelper
{
    private sealed class Turn
    {
        internal readonly HashSet<object> Tokens = new(ReferenceEqualityComparer.Instance);
        internal readonly List<CardModel> GardenPlays = new();
    }
    private sealed class Battle
    {
        internal readonly Dictionary<(Player, LeadingChargeCombatHelper.TurnKey), Turn> Turns = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static CardModel Physical(CardModel card) => ReferenceEquals(card.Pile, card.Owner?.Deck) ? card :
        !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) && card.DeckVersion is LoveCharlottaHbrgoSaki ? card.DeckVersion : card;
    private static bool Active(Player owner) => owner?.Creature is { IsAlive: true, CombatState: not null } && HbrBattleStatsHelper.IsEnabled(owner);
    internal static void BeforeCombatStart(LoveCharlottaHbrgoSaki card)
    {
        // Native combat listeners contain the populated combat copies, not
        // their permanent deck models. Reset the same physical receipt that
        // Play will consult, as well as this freshly populated copy.
        card.ClearBattleReceipts();
        if (Physical(card) is LoveCharlottaHbrgoSaki physical) physical.ClearBattleReceipts();
    }
    internal static void BeforeCardPlayed(CardPlay play)
    {
        if (play?.PlayIndex != 0 || play.Card is not { } card || !Active(card.Owner) || !HbrMemoryGardenCardCatalog.IsGardenCard(card.GetType())) return;
        var state = Battles.GetOrCreateValue(card.Owner.Creature.CombatState);
        var key = (card.Owner, LeadingChargeCombatHelper.Key(card.Owner));
        if (!state.Turns.TryGetValue(key, out Turn turn)) state.Turns.Add(key, turn = new());
        if (turn.Tokens.Add(CardPlayContextHelper.GetCurrentPlayToken(card) ?? play)) turn.GardenPlays.Add(Physical(card));
    }
    internal static int JealousyCount(LoveCharlottaHbrgoSaki card)
    {
        if (card?.IsMutable != true || !card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.Jealousy) || !Active(card.Owner) ||
            !Battles.TryGetValue(card.Owner.Creature.CombatState, out Battle battle) ||
            !battle.Turns.TryGetValue((card.Owner, LeadingChargeCombatHelper.Key(card.Owner)), out Turn turn)) return 0;
        CardModel physical = Physical(card);
        return Math.Min(2, turn.GardenPlays.Count(other => !ReferenceEquals(other, physical)));
    }
    internal static int EffectiveX(LoveCharlottaHbrgoSaki card, int paid) => (int)Math.Clamp((long)Math.Max(0, paid) +
        (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.Flash) ? 1 : 0) + JealousyCount(card), 0, int.MaxValue);
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        source is LoveCharlottaHbrgoSaki && ThirtyOneXAuthoredAttackScope.Applies(source, target, dealer, props)
            ? 4.25m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static async Task Play(PlayerChoiceContext context, LoveCharlottaHbrgoSaki card, CardPlay play)
    {
        Player player = card.Owner;
        if (!Active(player)) return;
        Creature owner = player.Creature;
        var physical = (LoveCharlottaHbrgoSaki)Physical(card);
        if (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.EternalVow) && physical.TakeVow())
        {
            await HbrEffectApplication.Apply<HbrEternalVowPower>(context, owner, 25m, owner, card,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.Eternal, 1));
        }
        Creature[] targets = card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.GrenadeForYou)
            ? card.CombatState.GetOpponentsOf(owner).Where(enemy => enemy.IsHittable).ToArray()
            : play.Target?.IsHittable == true ? new[] { play.Target } : Array.Empty<Creature>();
        if (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.GrenadeForYou))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(owner);
            foreach (Creature target in targets)
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, target, 20m, owner, card,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        }
        int x = EffectiveX(card, CardCopyContextHelper.TakeXEnergySpent(card, play.Resources.EnergySpent));
        if (x > 0 && targets.Length > 0)
        {
            var command = DamageCmd.Attack(BigCombatNumbers.MultiplyPositive(card.DynamicVars.Damage.BaseValue, x))
                .FromCardCompat(card, play).WithHitCount(3).WithHitFx("vfx/vfx_attack_slash");
            if (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.GrenadeForYou)) command.TargetingAllOpponents(card.CombatState);
            else command.Targeting(targets[0]);
            using (new ThirtyOneXAuthoredAttackScope(card, command)) await command.Execute(context);
            if (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.PureKiss))
            {
                Creature[] hit = command.Results.SelectMany(results => results).Where(CombatAttackHitHelper.IsConnected)
                    .Select(CombatAttackHitHelper.GetPhysicalTarget).Where(target => target?.IsHittable == true).Distinct().ToArray();
                using var batch = HbrDebuffApplicationScope.BeginBatch(owner);
                foreach (Creature target in hit)
                    await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, target, 42m, owner, card,
                        new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
            }
        }
        if (!Active(player)) return;
        if (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.PallasCatDoll) && physical.TakeDoll())
        {
            CardModel doll = player.Creature.CombatState.CreateCard<PallasCatDollHbrgoSaki>(player);
            await CardPileCmd.Add(doll, PileType.Hand);
        }
        if (card.HasUpgradeEffect(LoveCharlottaHbrgoSaki.EngagementRing))
            await PowerCmd.Apply<CharlottaEngagementRingPower>(context, owner, 1m, owner, null);
    }
}
