using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    private int TakeCannonActionCount(HotaruCannonKind kind)
    {
        int actions = StateFor(kind).TakeActionCount();
        if (kind != HotaruCannonKind.Coffin) return actions;
        int extra = Owner.Creature.GetPower<HotaruMyCoffinPower>()?.ExtraActivationCount ?? 0;
        // Plan every original/charged action's repeats once. Extra activations
        // are complete actions, but cannot recursively create more repeats.
        return (int)Math.Min(int.MaxValue, actions * (1L + Math.Max(0, extra)));
    }

    public void QueueDefensiveEcho(decimal baseValueBonus)
    {
        if (!CanAct()) return;
        _combat.PendingDefensiveEchoes.Add(new HotaruDefensiveEcho {
            BaseValueBonus = Math.Clamp(baseValueBonus, 0m, HotaruCannonState.MaximumStoredValue),
            Overclock = OverclockImmediateTurnCoordinator.IsActiveFor(Owner)
        });
    }

    private async Task ResolveDefensiveEchoes(PlayerChoiceContext context, bool overclock)
    {
        // Ordinary-turn instructions remain suspended during OD. Claim this
        // phase's requests before drawing can play another deferred skill.
        HotaruDefensiveEcho[] requests = _combat.PendingDefensiveEchoes
            .Where(request => request.Overclock == overclock).ToArray();
        _combat.PendingDefensiveEchoes.RemoveAll(request => request.Overclock == overclock);
        foreach (HotaruDefensiveEcho request in requests)
            await ExecutePhaseBatch(context, HotaruCannonPhase.Start,
                new[] { HotaruCannonKind.Coffin, HotaruCannonKind.Longing }, request.BaseValueBonus);
    }

    public async Task TriggerAllCannons(PlayerChoiceContext context, CardModel cardSource = null, CardPlay cardPlay = null)
    {
        if (!CanAct()) return;
        Creature[] cannons = Enum.GetValues<HotaruCannonKind>().Select(GetCannon)
            .Where(cannon => cannon != null).ToArray();
        using var vigorBatch = HotaruCannonVigorHelper.BeginCompleteActionBatch(this, cannons);
        foreach (Creature cannon in cannons)
        {
            if (!CanAct()) break;
            await TriggerAllEffects(cannon, context, cardSource, cardPlay, reuseVigorBatch: true);
        }
    }

    public async Task FireAllAttackIntents(Creature target, PlayerChoiceContext context,
        CardModel cardSource = null, CardPlay cardPlay = null)
    {
        if (!CanAct() || !IsEnemy(target, Owner)) return;
        HotaruCannonKind[] kinds = { HotaruCannonKind.Happiness, HotaruCannonKind.Contest,
            HotaruCannonKind.Longing, HotaruCannonKind.Absolution, HotaruCannonKind.Endgame };
        Creature[] cannons = kinds.Select(GetCannon).Where(cannon => cannon != null).ToArray();
        using var vigorBatch = HotaruCannonVigorHelper.BeginCompleteActionBatch(this, cannons, fireHappiness: true);
        foreach (Creature cannon in cannons)
        {
            if (!CanAct() || !IsEnemy(target, Owner)) break;
            HotaruCannonKind kind = ((HBRGOSakiMod.Core.Models.Monsters.HotaruCannonModel)cannon.Monster).Kind;
            if (kind == HotaruCannonKind.Happiness)
            {
                await FireHappiness(target, context, cardSource, cardPlay, reuseVigorBatch: true);
                continue;
            }
            int actions = StateFor(kind).TakeActionCount();
            bool hasStart = kind == HotaruCannonKind.Absolution || kind == HotaruCannonKind.Endgame && EndgameStartHitCount > 0;
            bool hasEnd = kind != HotaruCannonKind.Endgame || EndgameEndHitCount > 0;
            HotaruCannonPhase first = hasStart ? HotaruCannonPhase.Start : HotaruCannonPhase.End;
            for (int action = 0; action < actions && CanAct() && IsOwnedCannon(cannon, Owner) && IsEnemy(target, Owner); action++)
            {
                using var uncommon = HotaruUncommonCannonHelper.BeginAction(cannon, cardSource, cardPlay, isExtraAction: true);
                await HotaruUncommonCannonHelper.BeforeAction(context, uncommon);
                var vigorAttack = HotaruCannonVigorHelper.BeginCompleteAction(cannon, first);
                try
                {
                    if (hasStart)
                        await ExecutePhase(cannon, HotaruCannonPhase.Start, context, cardSource, cardPlay,
                            actionVigor: vigorAttack, completeVigor: false, fixedTarget: target);
                    if (hasEnd && CanAct() && IsEnemy(target, Owner))
                        await ExecutePhase(cannon, HotaruCannonPhase.End, context, cardSource, cardPlay,
                            actionVigor: vigorAttack, fixedTarget: target);
                }
                finally { await vigorAttack.Complete(context); }
                await NotifyCannonActionsCompleted(context, new[] { cannon });
                await HotaruUncommonCannonHelper.AfterAction(context, uncommon);
            }
        }
    }

    private async Task GrowHappiness(PlayerChoiceContext context, Creature cannon, decimal baseValueBonus,
        CardModel cardSource, CardPlay cardPlay)
    {
        HotaruCannonState state = StateFor(HotaruCannonKind.Happiness);
        decimal before = HotaruMyIndulgencePower.ModifyBaseDamage(cannon, state.HappinessDamage);
        state.GrowHappiness(baseValueBonus, HotaruCannonSupportUpgradeHelper.GetCannonRollMultiplier(
            cannon, CardType.Attack, resolve: true));
        decimal increase = HotaruMyIndulgencePower.ModifyBaseDamage(cannon, state.HappinessDamage) - before;
        await NotifyCannonBaseDamageIncreased(context, cannon, increase, cardSource, cardPlay);
    }

    internal async Task NotifyCannonBaseDamageIncreased(PlayerChoiceContext context, Creature cannon,
        decimal actualIncrease, CardModel cardSource = null, CardPlay cardPlay = null)
    {
        if (actualIncrease <= 0m || !CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        if (Owner.Creature.GetPower<HotaruMyHappinessPower>() is { } happiness)
            await happiness.OnCannonBaseDamageGrowth(context, this, cannon, actualIncrease, cardSource, cardPlay);
        if (Owner.Creature.GetPower<HotaruOverchargePower>() is { } overcharge)
            await overcharge.OnCannonBaseDamageGrowth(context, this, cannon, actualIncrease, cardSource, cardPlay);
    }

    private Task NotifyCannonActionsCompleted(PlayerChoiceContext context, IReadOnlyList<Creature> cannons)
    {
        foreach (Creature cannon in cannons)
            HotaruUncommonCannonHelper.RecordCompletedExtraAction(HotaruUncommonCannonHelper.CurrentAction(cannon));
        HotaruRareCannonHelper.RecordActions(Owner, cannons);
        return CanAct() && Owner.Creature.GetPower<HotaruMyHappinessPower>() is { } happiness
            ? happiness.OnCannonActions(context, this, cannons) : Task.CompletedTask;
    }

    private Task DealPhaseDamage(Creature cannon, Creature fixedTarget, decimal amount,
        PlayerChoiceContext context, CardModel cardSource, CardPlay cardPlay) =>
        fixedTarget == null ? DealRandomDamage(cannon, amount, context, cardSource, cardPlay)
            : IsEnemy(fixedTarget, Owner) ? DealDamage(cannon, fixedTarget, amount, context, cardSource, cardPlay)
            : Task.CompletedTask;
}
