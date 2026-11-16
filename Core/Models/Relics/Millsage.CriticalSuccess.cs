using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    private Queue<(Creature[] Cannons, int HandbookCount)> _pendingCriticalCannons = new();
    private bool _resolvingCriticalCannons;

    internal void QueueCriticalCannonReward()
    {
        if (_resolvingCriticalCannons || Owner?.Character is not HbrgoHotaru || !CanAct()) return;
        var available = new List<Creature>(5);
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
            if (GetCannon(kind) is { } cannon) available.Add(cannon);
        int count = Math.Min(2, available.Count);
        if (count == 0) return;
        var selected = new Creature[count];
        for (int index = 0; index < count; index++)
        {
            int pick = Owner.RunState.Rng.Niche.NextInt(available.Count);
            selected[index] = available[pick];
            available.RemoveAt(pick);
        }
        _pendingCriticalCannons.Enqueue((HotaruClassRelicCombatHelper.AddGiftCannon(Owner, selected,
            Enum.GetValues<HotaruCannonKind>().Select(GetCannon).Where(cannon => cannon != null).ToArray()), count));
    }

    // Actual roll callbacks can be synchronous. Their reward is completed at
    // the surrounding awaited card, damage or cannon-phase boundary.
    internal async Task ResolveCriticalCannonRewards(PlayerChoiceContext context)
    {
        if (_resolvingCriticalCannons) return;
        if (_pendingCriticalCannons.Count == 0)
        {
            await RanaByakkoCombatHelper.ResolvePendingRawCritical(context, Owner);
            return;
        }
        _resolvingCriticalCannons = true;
        try
        {
            while (_pendingCriticalCannons.TryDequeue(out var reward))
            {
                Creature[] cannons = reward.Cannons;
                using var vigorBatch = HotaruCannonVigorHelper.BeginCompleteActionBatch(this, cannons);
                for (int index = 0; index < cannons.Length; index++)
                {
                    Creature cannon = cannons[index];
                    await TriggerAllEffects(cannon, context, reuseVigorBatch: true, suppressCritical: true);
                    // Only the handbook's two selected cannons gain soul power.
                    if (index < reward.HandbookCount) GainCannonSoulPower(((HotaruCannonModel)cannon.Monster).Kind);
                    await RefreshIntent(cannon);
                }
            }
        }
        finally { _resolvingCriticalCannons = false; }
        await RanaByakkoCombatHelper.ResolvePendingRawCritical(context, Owner);
    }

    private void ResetCriticalCannonRewards()
    {
        _pendingCriticalCannons = new();
        _resolvingCriticalCannons = false;
    }

    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature dealer,
        DamageResult result, ValueProp props, Creature target, CardModel cardSource) =>
        dealer == Owner?.Creature ? ResolveCriticalCannonRewards(context) : Task.CompletedTask;

    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target,
        DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner?.Creature) return;
        await HotaruClassRelicCombatHelper.ResolveDpBreak(context, Owner);
        await ResolveCriticalCannonRewards(context);
    }
}
