using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneCMachinaHelper
{
    internal const int BaseHits = 6;
    internal const decimal OriginalDestructionCoefficient = 6m;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo AttackerField = typeof(AttackCommand).GetField("<Attacker>k__BackingField", Fields)
        ?? throw new MissingFieldException(typeof(AttackCommand).FullName, "Attacker");
    private static readonly FieldInfo SourceField = typeof(AttackCommand).GetField("<ModelSource>k__BackingField", Fields)
        ?? throw new MissingFieldException(typeof(AttackCommand).FullName, "ModelSource");
    private static readonly AsyncLocal<Activation> Current = new();
    private sealed class Activation : IDisposable
    {
        internal Activation Previous;
        internal AttackCommand Command;
        internal Creature Dealer;
        internal int Segment;
        internal decimal Weight = 1m;
        internal bool Active = true;
        internal Task NextSegment()
        {
            // A large combo repeats the complete six-hit distribution. Extra
            // small/medium hits begin the next cycle with its small segments.
            Weight = Segment % BaseHits == BaseHits - 1 ? 5m : 1m;
            Segment++; return Task.CompletedTask;
        }
        public void Dispose()
        {
            Active = false;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }

    internal static int ServantCount(Player player) => ThirtyOneCKeywordRules.GetPermanentDistinctCount(player);
    internal static int RemainingUses(Player player) => player?.GetRelic<ThirtyOneCCollection>()?.RemainingUses ?? 0;
    internal static bool IsVisible(Player player) => player?.GetRelic<ThirtyOneCCollection>() is
        { IsMelted: false, BattleReady: true } && ServantCount(player) >= 2 && player.Creature?.IsAlive == true &&
        HbrBattleStatsHelper.IsEnabled(player) && CombatManager.Instance?.IsInProgress == true;
    private static bool CanExecute(Player player) => IsVisible(player) &&
        CombatManager.Instance.IsOverOrEnding != true && player.PlayerCombatState?.Phase == PlayerTurnPhase.Play &&
        player.GetRelic<ThirtyOneCCollection>() is { IsResolving: false, RemainingUses: > 0 } &&
        player.Creature.CombatState?.GetOpponentsOf(player.Creature).Any(creature => creature.IsAlive) == true;
    internal static bool CanActivate(Player player) => CanExecute(player) && !CombatManager.Instance.PlayerActionsDisabled;
    internal static bool RequestActivate(Player player) => CanActivate(player) && HbrThirtyOneCMachinaNetActions.Request(player);

    internal static string HoverText(Player player)
    {
        int servants = ServantCount(player);
        string fear = servants >= 6
            ? "对魔王的畏惧：每次额外降低30点状态值，可叠加，持续至战斗结束。"
            : "对魔王的畏惧：状态值降低30点，持续至敌方回合结束；重复施加只刷新，不叠加。";
        return $"山胁大人的仆人：{servants}/6。\n对全体敌人基础攻击6次（合计{servants * 100}%，前5次各占10%，最后一次占50%），必定暴击，破坏率系数6。\n{fear}\n本场剩余{RemainingUses(player)}次。只可在自己的出牌阶段使用，不消耗能量，不计为打出牌。";
    }

    internal static async Task<bool> TryActivate(PlayerChoiceContext context, Player player, int expectedUsed)
    {
        ThirtyOneCCollection relic = player?.GetRelic<ThirtyOneCCollection>();
        if (!CanExecute(player) || !relic.TryReserve(expectedUsed)) return false;
        int servants = ServantCount(player);
        Creature actor = player.Creature;
        // No synthetic card and no play wrapper. A single native attack owns
        // all six hits, so attack/count buffs reserve and settle only once.
        AttackCommand command = new(HbrSkillPower.DamageUnits(servants * 100m) / 10m);
        AttackerField.SetValue(command, actor);
        SourceField.SetValue(command, relic);
        command.TargetingAllOpponents(actor.CombatState).WithNoAttackerAnim().WithHitCount(BaseHits);
        using Activation activation = new() { Previous = Current.Value, Command = command, Dealer = actor };
        command.BeforeDamage(activation.NextSegment);
        Current.Value = activation;
        try
        {
            await command.Execute(context);
            foreach (Creature enemy in actor.CombatState.GetOpponentsOf(actor).Where(creature => creature.IsAlive).ToArray())
                await FearOfDevilPower.Apply(context, enemy, actor, servants >= 6);
            return true;
        }
        finally { relic.FinishResolution(); }
    }

    private static bool Matches(Creature dealer, CardModel card, ValueProp props) => card == null &&
        props.IsPoweredAttack() && Current.Value is { Active: true } scope &&
        ReferenceEquals(scope.Dealer, dealer) && HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command);
    internal static decimal ScaleSkillUnits(Creature dealer, CardModel card, ValueProp props, decimal rawUnits) =>
        Matches(dealer, card, props) ? DamageAmplificationLimits.MultiplySaturated(rawUnits, Current.Value.Weight) : rawUnits;
    internal static bool ForcesCritical(Creature dealer, CardModel card, ValueProp props) => Matches(dealer, card, props);
    internal static decimal DestructionMultiplier(Creature dealer, CardModel card, ValueProp props) => Matches(dealer, card, props)
        ? OriginalDestructionCoefficient / HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
}
