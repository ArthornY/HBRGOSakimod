using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    internal bool IsCannonRecalledThisCombat(HotaruCannonKind kind) =>
        HotaruCannonKinds.IsOriginal(kind) && WasCannonRetired(kind) &&
        StateFor(kind).RecalledUpgradeLevel > 0;

    internal bool IsCannonAvailableInCombat(HotaruCannonKind kind) =>
        !WasCannonRetired(kind) || IsCannonRecalledThisCombat(kind);

    internal async Task<bool> RecallRandomRetiredCannon(PlayerChoiceContext choiceContext,
        int level, CardModel source = null)
    {
        if (!IsMutable || !CanAct()) return false;
        var combat = Owner.Creature.CombatState;
        HotaruCannonCombatState snapshot = _combat;
        HotaruCannonKind[] candidates = HotaruCannonKinds.Original.Where(kind =>
            WasCannonRetired(kind) && !StateFor(kind).RecallPending &&
            !combat.Allies.Any(creature => creature.IsAlive && creature.PetOwner == Owner &&
                creature.Monster is HotaruCannonModel model && model.Kind == kind)).ToArray();
        if (candidates.Length == 0) return false;
        HotaruCannonKind selected = candidates[Owner.RunState.Rng.Niche.NextInt(candidates.Length)];
        level = Math.Clamp(level, 1, MaximumCannonUpgradeLevel);
        HotaruCannonState previous = StateFor(selected);
        HotaruCannonState recalled = new() { RecallPending = true };
        recalled.InitializeRecall(level, InitialCannonDamagePercent + CannonDamagePercentPerUpgrade * level);
        if (selected == HotaruCannonKind.Happiness)
            recalled.InitializeHappinessIncrease(3m);
        // Publish availability before AddPet's hooks. A nested card cannot
        // select this same identity while its entity is being registered.
        snapshot.Cannons[(int)selected] = recalled;
        Creature cannon = null;
        try
        {
            MonsterModel canonical = selected switch
            {
                HotaruCannonKind.Happiness => ModelDb.Monster<HotaruHappinessCannon>(),
                HotaruCannonKind.Coffin => ModelDb.Monster<HotaruCoffinCannon>(),
                HotaruCannonKind.Contest => ModelDb.Monster<HotaruContestCannon>(),
                HotaruCannonKind.Longing => ModelDb.Monster<HotaruLongingCannon>(),
                HotaruCannonKind.Absolution => ModelDb.Monster<HotaruAbsolutionCannon>(),
                _ => throw new ArgumentOutOfRangeException(nameof(selected))
            };
            cannon = combat.CreateCreature((MonsterModel)canonical.ToMutable(), Owner.Creature.Side, null);
            await PlayerCmd.AddPet(cannon, Owner);
            if (!ReferenceEquals(snapshot, _combat) || Owner.Creature.CombatState != combat || !CanAct()) return false;
            cannon.PrepareForNextTurn(combat.Enemies);
            // Combat-start rewards are past events. Only the recalled level's
            // persistent/action abilities and fresh per-cannon state take effect.
            foreach (HotaruCannonPhase phase in Enum.GetValues<HotaruCannonPhase>())
                if (HotaruCannonState.HasEffect(selected, phase))
                    await HotaruCannonSupportUpgradeHelper.PreparePhase(choiceContext, cannon, phase);
            await RefreshIntent(cannon);
            CombatPreviewRefreshHelper.RefreshCombatState(Owner.Creature, nameof(RecallRandomRetiredCannon));
            return true;
        }
        finally
        {
            recalled.RecallPending = false;
            // AddPet may throw from an after-add hook. Keep the temporary
            // identity if registration happened before that hook failed.
            bool registered = cannon?.PetOwner == Owner &&
                (combat.Allies.Contains(cannon) || Owner.PlayerCombatState?.Pets.Contains(cannon) == true);
            if (!registered && ReferenceEquals(snapshot, _combat)) snapshot.Cannons[(int)selected] = previous;
        }
    }
}
