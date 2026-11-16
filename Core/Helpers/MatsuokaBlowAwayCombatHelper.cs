using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
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

internal static class MatsuokaBlowAwayCombatHelper
{
    private sealed class StrikeScope : IDisposable
    {
        internal CardModel Card;
        internal AttackCommand Command;
        internal StrikeScope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<StrikeScope> Current = new();
    internal static void TurnStart(Player player) => player?.Creature?.GetPower<MatsuokaBlowAwayPower>()?.StartOwnTurn();
    internal static void AddDescriptionParameters(CardModel source, LocString text)
    {
        if (source is not LeaveAnyBattlefieldToMeHbrgoSaki card || text == null) return;
        int count = card.IsMutable ? card.Owner?.Creature?.GetPower<MatsuokaBlowAwayPower>()?.BlowAwayCount ?? 0 : 0;
        decimal basis = 30m + (card.HasUpgradeEffect(LeaveAnyBattlefieldToMeHbrgoSaki.ScarsAreMyPride) ? Math.Min(6, count) * 5m : 0m);
        text.Add("MatsuokaDefense", HbrCardFaceDescription.Percent(card, "DefenseUp", basis));
        text.Add("MatsuokaCardRecipients", new LocString("cards", card.Id.Entry +
            (card.HasUpgradeEffect(LeaveAnyBattlefieldToMeHbrgoSaki.RefreshingCheers) ? ".recipients.all" : ".recipients.self")).GetFormattedText());
        text.Add("MatsuokaCardCombo", new LocString("cards", card.Id.Entry +
            (card.HasUpgradeEffect(LeaveAnyBattlefieldToMeHbrgoSaki.NearAndFar) ? ".combo.medium" : ".combo.small")).GetFormattedText());
    }
    internal static void AddPowerDescriptionParameters(MatsuokaBlowAwayPower power, LocString text)
    {
        if (text == null) return;
        Creature owner = power.IsMutable ? power.Owner : null;
        decimal basis = power.IsMutable ? power.NextDefenseBasis : 30m;
        text.Add("MatsuokaDefense", HbrEffectFormula.Buff(basis, owner, HbrEffectFormula.Intelligence));
        text.Add("MatsuokaRecipients", new LocString("powers", power.Id.Entry +
            (power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.RefreshingCheers) ? ".recipients.all" : ".recipients.self")).GetFormattedText());
        text.Add("MatsuokaCombo", new LocString("powers", power.Id.Entry +
            (power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.NearAndFar) ? ".combo.medium" : ".combo.small")).GetFormattedText());
        string extra = string.Empty;
        if (power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.RotorKick))
        {
            LocString critical = new("powers", power.Id.Entry + ".extra.critical");
            critical.Add("Critical", HbrEffectFormula.Buff(70m, owner, HbrEffectFormula.Intelligence));
            extra += critical.GetFormattedText();
        }
        if (power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.RoadToStardom))
            extra += new LocString("powers", power.Id.Entry + ".extra.recovery").GetFormattedText();
        text.Add("MatsuokaExtra", extra);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        for (StrikeScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, source) && source?.Owner?.Creature == dealer &&
                target != null && target.Side != dealer.Side && props.IsPoweredAttack() &&
                !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source) && HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command))
                return 1m / HbrDestructionGainHelper.DefaultSkillCoefficient;
        return 1m;
    }
    private static async Task Strike(PlayerChoiceContext context, LeaveAnyBattlefieldToMeHbrgoSaki card, CardPlay play)
    {
        Creature[] choices = card.CombatState.GetOpponentsOf(card.Owner.Creature).Where(enemy => enemy.IsHittable).ToArray();
        if (choices.Length == 0) return;
        Creature target = choices[card.Owner.RunState.Rng.CombatTargets.NextInt(choices.Length)];
        AttackCommand command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
            .Targeting(target).WithHitCount(3).WithHitFx("vfx/vfx_attack_slash");
        using StrikeScope scope = new() { Card = card, Command = command, Previous = Current.Value };
        Current.Value = scope;
        await command.Execute(context);
    }
    internal static async Task Play(PlayerChoiceContext context, LeaveAnyBattlefieldToMeHbrgoSaki card, CardPlay play)
    {
        Creature owner = card.Owner?.Creature;
        if (owner?.IsAlive != true || owner.CombatState == null || !HbrBattleStatsHelper.IsEnabled(owner)) return;
        MatsuokaBlowAwayPower ability = owner.GetPower<MatsuokaBlowAwayPower>();
        if (ability == null) await PowerCmd.Apply<MatsuokaBlowAwayPower>(context, owner, 1m, owner, card);
        else ability.Configure(card);
        if (card.HasUpgradeEffect(LeaveAnyBattlefieldToMeHbrgoSaki.ConcentratedStrike)) await Strike(context, card, play);
    }
    internal static async Task AfterHit(PlayerChoiceContext context, MatsuokaBlowAwayPower power,
        Creature target, DamageResult result, ValueProp props, Creature dealer)
    {
        Creature owner = power.IsMutable ? power.Owner : null;
        if (owner?.IsAlive != true || owner.Player == null || target != owner || result?.Receiver != owner ||
            dealer == null || dealer.Side == owner.Side || !props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(owner) ||
            CombatAttackHitHelper.GetPhysicalTarget(result) != owner || CombatAttackHitHelper.WasDodged(result) ||
            !power.TryTakeTrigger(out decimal basis, out bool recover)) return;
        Creature[] recipients = power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.RefreshingCheers)
            ? HbrPlayerTeamTargets.Get(owner) : new[] { owner };
        foreach (Creature recipient in recipients)
            await HbrEffectApplication.Apply<HbrDefenseUpPower>(context, recipient, basis, owner, null,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        if (power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.NearAndFar))
            await HbrComboPower.Grant<HbrComboMediumPower>(context, owner, owner, null);
        else await HbrComboPower.Grant<HbrComboSmallPower>(context, owner, owner, null);
        if (power.Has(LeaveAnyBattlefieldToMeHbrgoSaki.RotorKick))
            await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, owner, 70m, owner, null,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        if (recover) await SeiraDpRecoveryHelper.RestoreFromAbility(context, owner, .1m, false);
    }
}
