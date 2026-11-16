using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CarolHeroCombatHelper
{
    private sealed class Receipt : IDisposable
    {
        internal ReadySteadyDieHbrgoSaki Card;
        internal AttackCommand Command;
        internal bool Mask, Active = true;
        internal Receipt Previous;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<Receipt> Current = new();
    private static Receipt Find(CardModel card) => Current.Value is { Active: true } receipt && ReferenceEquals(receipt.Card, card) ? receipt : null;
    private static bool HasMaskDp(Creature actor) => RanaByakkoCombatHelper.HasDp(FubukiChangeCombatHelper.DpTarget(actor?.Player) ?? actor);
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (source is not ReadySteadyDieHbrgoSaki { IsMutable: true } || dealer == null || source.Owner?.Creature != dealer ||
            !HbrBattleStatsHelper.IsEnabled(dealer) || !props.IsPoweredAttack() || target != null && target.Side == dealer.Side ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source)) return false;
        if (Find(source)?.Command is { } command) return HighHitDamageVisualHelper.IsDirectAttackDamage(command);
        return HotaruCannonStatsHelper.IsDamagePreview || !StrongestShieldDamagePatch.IsResolvingActualDamage;
    }
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && ((ReadySteadyDieHbrgoSaki)source).HasUpgradeEffect(ReadySteadyDieHbrgoSaki.SistersMask) &&
        (Find(source)?.Mask ?? HasMaskDp(dealer)) ? 30m : 0m;
    internal static decimal GetHpDamageMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        target != null && Applies(source, dealer, target, props) && !RanaByakkoCombatHelper.HasDp(target) ?
            ((ReadySteadyDieHbrgoSaki)source).HasUpgradeEffect(ReadySteadyDieHbrgoSaki.ThunderOfJustice) ? 1.5m : 1.3m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? 2.375m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static async Task Play(PlayerChoiceContext context, ReadySteadyDieHbrgoSaki card, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(card.Owner)) return;
        Creature actor = card.Owner.Creature;
        using Receipt receipt = new() { Card = card, Mask = HasMaskDp(actor), Previous = Current.Value };
        Current.Value = receipt;
        bool goodChance = actor.CombatState.GetOpponentsOf(actor).Any(enemy => enemy.IsAlive && enemy.HasPower<BreakPower>());
        if (card.HasUpgradeEffect(ReadySteadyDieHbrgoSaki.LegendaryMode))
            await HbrEffectApplication.ApplyMindsEye(context, actor, actor, card);
        if (goodChance && card.HasUpgradeEffect(ReadySteadyDieHbrgoSaki.GoodChance))
            await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, actor, 25m, actor, card,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.PlayerTurnEnd, 1));
        AttackCommand command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play).WithHitCount(4)
            .WithHitFx("vfx/vfx_attack_slash");
        receipt.Command = card.HasUpgradeEffect(ReadySteadyDieHbrgoSaki.LightningStorm)
            ? command.TargetingAllOpponents(actor.CombatState) : command.Targeting(play.Target);
        await receipt.Command.Execute(context);
        receipt.Command = null;
        if (card.HasUpgradeEffect(ReadySteadyDieHbrgoSaki.ThunderRoar))
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, actor, 40m, actor, card,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
    }
}
