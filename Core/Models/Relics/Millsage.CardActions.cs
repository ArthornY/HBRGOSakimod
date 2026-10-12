using System;
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
            foreach (HotaruCannonKind kind in new[] { HotaruCannonKind.Coffin, HotaruCannonKind.Longing })
            {
                Creature cannon = GetCannon(kind);
                if (!CanAct() || cannon == null) continue;
                int actions = StateFor(kind).TakeActionCount();
                for (int action = 0; action < actions && CanAct() && IsOwnedCannon(cannon, Owner); action++)
                    await ExecutePhase(cannon, HotaruCannonPhase.Start, context, null,
                        baseValueBonus: request.BaseValueBonus);
            }
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
            }
        }
    }

    private async Task GrowHappiness(PlayerChoiceContext context, Creature cannon, decimal baseValueBonus,
        CardModel cardSource, CardPlay cardPlay)
    {
        HotaruCannonState state = StateFor(HotaruCannonKind.Happiness);
        decimal before = state.HappinessDamage;
        state.GrowHappiness(baseValueBonus);
        decimal increase = state.HappinessDamage - before;
        if (increase > 0m && Owner.Creature.GetPower<HotaruOverchargePower>() is { } power)
            await power.OnHappinessGrowth(context, this, cannon, increase, cardSource, cardPlay);
    }

    private Task DealPhaseDamage(Creature cannon, Creature fixedTarget, decimal amount,
        PlayerChoiceContext context, CardModel cardSource, CardPlay cardPlay) =>
        fixedTarget == null ? DealRandomDamage(cannon, amount, context, cardSource, cardPlay)
            : IsEnemy(fixedTarget, Owner) ? DealDamage(cannon, fixedTarget, amount, context, cardSource, cardPlay)
            : Task.CompletedTask;
}
