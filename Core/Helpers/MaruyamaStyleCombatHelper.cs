using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MaruyamaStyleCombatHelper
{
    private sealed class Battle { internal readonly HashSet<Player> ReturnedMion = new(); }
    private sealed class Receipt : IDisposable
    {
        internal LetMeShowYouClassHbrgoSaki Card;
        internal AttackCommand Command;
        internal Receipt Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<object, Battle> Battles = new();
    private static readonly AsyncLocal<Receipt> Current = new();
    private static bool Applies(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (source is not LetMeShowYouClassHbrgoSaki || source.IsMutable != true || dealer == null || source.Owner?.Creature != dealer ||
            target == null || target.Side == dealer.Side || !props.IsPoweredAttack() || HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return false;
        if (HotaruCannonStatsHelper.IsDamagePreview) return true;
        if (Current.Value is { Active: true } receipt && ReferenceEquals(receipt.Card, source) && receipt.Command != null)
            return HighHitDamageVisualHelper.IsDirectAttackDamage(receipt.Command);
        return !StrongestShieldDamagePatch.IsResolvingActualDamage;
    }
    internal static decimal GetDpDamageMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, target, dealer, props) && ((LetMeShowYouClassHbrgoSaki)source).HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.LittleHighClass) &&
        RanaByakkoCombatHelper.HasDp(target) ? 1.3m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, target, dealer, props) ? 24m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    private static Creature[] Team(Creature owner) => HbrPlayerTeamTargets.Get(owner);
    internal static async Task Play(PlayerChoiceContext context, LetMeShowYouClassHbrgoSaki card, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(card.Owner)) return;
        Creature owner = card.Owner.Creature;
        Creature[] enemies = owner.CombatState.GetOpponentsOf(owner).Where(enemy => enemy.IsHittable).ToArray();
        using (HbrDebuffApplicationScope.BeginBatch(owner))
            foreach (Creature enemy in enemies)
            {
                var profile = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
                if (card.HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.UpsideDown))
                    await HbrPercentDebuffApplication.ApplyBasis<HbrDpDefenseDownPower>(context, enemy, 50m, owner, card, profile);
                if (card.HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.PhotonBullet))
                    await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 30m, owner, card, profile);
            }
        if (card.HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.RadiantAdvance))
            foreach (Creature ally in Team(owner)) await HbrEffectApplication.ApplyMindsEye(context, ally, owner, card);
        if (card.HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.MaruyamaCaptain))
            foreach (Creature ally in Team(owner)) await PowerCmd.Apply<HbrMoralePower>(context, ally, 2m, owner, null);
        using (Receipt receipt = new() { Card = card, Previous = Current.Value })
        {
            Current.Value = receipt;
            receipt.Command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
                .TargetingAllOpponents(owner.CombatState).WithHitCount(4).WithHitFx("vfx/vfx_attack_slash");
            await receipt.Command.Execute(context);
        }
        // This new combo must never enlarge the four base hits that granted it.
        if (card.HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.RadiantAdvance))
            foreach (Creature ally in Team(owner)) await HbrComboPower.Grant<HbrComboMediumPower>(context, ally, owner, card);
        if (card.HasUpgradeEffect(LetMeShowYouClassHbrgoSaki.MionComeHere)) await ReturnMion(context, card);
    }
    private static async Task ReturnMion(PlayerChoiceContext context, LetMeShowYouClassHbrgoSaki source)
    {
        Player owner = source.Owner;
        CardModel[] cards = owner.PlayerCombatState.ExhaustPile.Cards.Where(card => card.Owner == owner &&
            !card.HasBeenRemovedFromState && HbrMemoryGarden.CharacterIdOf(card) == "mion").ToArray();
        if (cards.Length == 0 || !Battles.GetOrCreateValue(source.CombatState).ReturnedMion.Add(owner)) return;
        CardModel selected = cards.Length == 1 ? cards[0] : (await CardSelectCmd.FromSimpleGrid(context, cards, owner,
            new CardSelectorPrefs(new LocString("cards", source.Id.Entry + ".selectionPrompt"), 1)))
            .FirstOrDefault(card => cards.Contains(card) && card.Owner == owner && card.Pile == owner.PlayerCombatState.ExhaustPile);
        if (selected != null && selected.Pile == owner.PlayerCombatState.ExhaustPile) await CardPileCmd.Add(selected, MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand);
    }
}
