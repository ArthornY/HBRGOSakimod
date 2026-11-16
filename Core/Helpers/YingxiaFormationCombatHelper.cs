using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class YingxiaFormationCombatHelper
{
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        source is WitnessMySupremacyHbrgoSaki && ThirtyOneXAuthoredAttackScope.Applies(source, target, dealer, props)
            ? 1.5m / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static decimal Bonus(WitnessMySupremacyHbrgoSaki card) => card?.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.Amplify) == true ? 10m : 0m;
    internal static void AddDescriptionParameters(WitnessMySupremacyHbrgoSaki card, LocString text)
    {
        if (text == null) return;
        decimal bonus = Bonus(card);
        string Percent(string kind, decimal basis) => card == null ? basis.ToString("0.#", CultureInfo.InvariantCulture)
            : HbrCardFaceDescription.Percent(card, kind, basis, true);
        text.Add("CraneAttack", Percent("AttackUp", 50m + bonus));
        text.Add("ScaleCritical", Percent("CriticalRate", 50m + bonus));
        text.Add("ScaleCriticalDamage", Percent("CriticalDamage", 45m + bonus));
        text.Add("SquareDefense", Percent("DefenseUp", 30m + bonus));
        text.Add("FormationCount", card?.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.DualFormation) == true ? 2 : 1);
        text.Add("FormationRegeneration", card?.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.RegenerationFormation) == true
            ? new LocString("card_keywords", "HBRGO_SAKI_MOD_KEYWORD_FORMATION.regeneration").GetFormattedText() : string.Empty);
        text.Add("FormationCombo", card?.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.SomethingFromNothing) == true
            ? new LocString("card_keywords", "HBRGO_SAKI_MOD_KEYWORD_FORMATION.combo").GetFormattedText() : string.Empty);
    }
    internal static async Task Play(PlayerChoiceContext context, WitnessMySupremacyHbrgoSaki card, CardPlay play)
    {
        var player = card.Owner;
        if (player?.Creature is not { IsAlive: true, CombatState: { } combat } || !HbrBattleStatsHelper.IsEnabled(player)) return;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(player);
        int turn = player.PlayerCombatState.TurnNumber;
        int count = card.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.RegenerationFormation) ? 4 : 3;
        int selections = card.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.DualFormation) ? 2 : 1;
        var options = new YingxiaFormationChoiceHbrgoSaki[count];
        int[] chosen;
        try
        {
            for (int index = 0; index < count; index++)
            {
                options[index] = (YingxiaFormationChoiceHbrgoSaki)ModelDb.Card<YingxiaFormationChoiceHbrgoSaki>().ToMutable();
                options[index].Configure(card, index);
            }
            chosen = (await CardSelectCmd.FromSimpleGrid(context, options, player,
                new CardSelectorPrefs(new LocString("cards", card.Id.Entry + ".selectionPrompt"), selections)))
                .OfType<YingxiaFormationChoiceHbrgoSaki>().Where(option => options.Contains(option))
                .Select(option => option.Formation).Distinct().Take(selections).ToArray();
        }
        finally
        { foreach (var option in options) if (option is { HasBeenRemovedFromState: false }) option.RemoveFromState(); }
        if (chosen.Length != selections || player.Creature.IsAlive != true || !ReferenceEquals(combat, player.Creature.CombatState) ||
            player.PlayerCombatState.TurnNumber != turn || !ReferenceEquals(token, OverclockImmediateTurnCoordinator.GetTurnToken(player))) return;
        foreach (int formation in chosen) await ApplyFormation(context, card, formation);
        Creature owner = player.Creature;
        if (card.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.MantisAgainstChariot))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(owner);
            foreach (Creature enemy in combat.GetOpponentsOf(owner).Where(enemy => enemy.IsHittable).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m, owner, card,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        }
        if (card.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.SleepingDragonDance))
        {
            Creature[] enemies = combat.GetOpponentsOf(owner).Where(enemy => enemy.IsHittable).ToArray();
            if (enemies.Length == 0) return;
            Creature target = enemies[player.RunState.Rng.CombatTargets.NextInt(enemies.Length)];
            var command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card, play)
                .Targeting(target).WithHitCount(4).WithHitFx("vfx/vfx_attack_slash");
            using (new ThirtyOneXAuthoredAttackScope(card, command)) await command.Execute(context);
            if (target.IsHittable)
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, target, 30m, owner, card,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        }
    }
    private static async Task ApplyFormation(PlayerChoiceContext context, WitnessMySupremacyHbrgoSaki card, int formation)
    {
        Creature owner = card.Owner.Creature;
        decimal bonus = Bonus(card);
        foreach (Creature ally in HbrPlayerTeamTargets.Get(owner))
            switch (formation)
            {
                case 0:
                    await HbrEffectApplication.Apply<HbrAttackUpPower>(context, ally, 50m + bonus, owner, card,
                        profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
                    if (card.HasUpgradeEffect(WitnessMySupremacyHbrgoSaki.SomethingFromNothing))
                        await HbrComboPower.Grant<HbrComboSmallPower>(context, ally, owner, card);
                    break;
                case 1:
                    await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, ally, 50m + bonus, owner, card,
                        profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
                    await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, ally, 45m + bonus, owner, card,
                        profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
                    break;
                case 2:
                    await HbrEffectApplication.Apply<HbrDefenseUpPower>(context, ally, 30m + bonus, owner, card,
                        profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
                    break;
                case 3:
                    // A single owner clock heals the entire current team on each of its next three turns.
                    if (ReferenceEquals(ally, owner))
                        await PowerCmd.Apply<YingxiaRegenerationFormationPower>(context, owner, 3m, owner, null);
                    break;
            }
    }
}
