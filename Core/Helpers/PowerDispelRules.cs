using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Player dispels share this policy. Natural expiry, amount changes, slot
// replacement, DP break and the boss's own transitions use their native
// removal commands directly, and therefore retain their normal lifecycle.
internal static class PowerDispelRules
{
    internal static bool IsUndispellable(PowerModel power)
    {
        if (power?.IsMutable != true || !EnemyStatScalingHelper.IsEnemy(power.Owner)) return false;
        // The four HP bars are an intrinsic enemy-boss rule in every mode.
        if (power.Owner.Monster is NararaShibaBossMonster && power is NararaLayeredHpPower) return true;
        if (!HbrBattleStatsHelper.IsEnabled(power.Owner)) return false;
        if (power.Owner.Monster is NararaShibaBossMonster &&
            power is NararaNullificationPower or NararaPressurePower or NararaSleepingPower or
                TourGuidePower or NararaOverworkPower or NararaGiveYouPower or NararaLayeredDpPower) return true;
        return (power.Owner.Monster, power) switch
        {
            (PillarGuardian, PillarOverdriveDetectionPower) => true,
            (BarrelWalker, BarrelDemiseCountdownPower) => true,
            (DiamondEyeball, DiamondDpCheckPower) => true,
            (BigotryGate, FaintnessStormCountdownPower) => true,
            (ShrimpCrab, ShrimpRunawayPower) => true,
            (TezukaSaki, GhostStatePower) => true,
            (SkullFeather, SkullFeatherPower) => true,
            (FlatHand, PhaseWarpPower) => true,
            (RedCrimsonBossMonster red, IntangiblePower) => red.HasAppliedStartingIntangible && ReferenceEquals(power.Applier, power.Owner),
            // The enemy's own starting grant has itself as the applier. Other
            // monsters' and player-side phantoms' Intangible remain dispellable.
            (GoldHopper gold, IntangiblePower) => gold.HasAppliedStartingIntangible && ReferenceEquals(power.Applier, power.Owner),
            _ => false
        };
    }

    internal static bool CanPlayerDispel(PowerModel power) => power != null && !IsUndispellable(power);

    internal static async Task<bool> TryPlayerDispel(PowerModel power)
    {
        if (!CanPlayerDispel(power) || power.Owner is not { } owner || !owner.Powers.Contains(power)) return false;
        await PowerCmd.Remove(power);
        return !owner.Powers.Contains(power);
    }

    // All authored random player dispels share the same visible-buff policy,
    // RNG stream and ability-kind grouping, regardless of the source card.
    internal static async Task RandomRemoveBuffs(Player source, Creature enemy, int count)
        => await RandomRemoveBuffKinds(source, enemy, count);

    internal static async Task<int> RandomRemoveBuffKinds(Player source, Creature enemy, int count)
    {
        if (source?.RunState == null || enemy == null || count <= 0) return 0;
        List<PowerModel[]> candidates = enemy.Powers.Where(power =>
            power.IsVisible && power.Type == PowerType.Buff && CanPlayerDispel(power) &&
            power is not (AdaptablePower or SteamEruptionPower or ReattachPower or
                InfestedPower or StockPower or SurprisePower or IllusionPower or MinionPower or SandpitPower) &&
            (power is not (StrengthPower or DexterityPower) || power.Amount > 0))
            .GroupBy(power => power.Id).Select(group => group.ToArray()).ToList();
        List<PowerModel[]> selected = new();
        while (selected.Count < count && candidates.Count > 0)
        {
            int index = source.RunState.Rng.CombatCardGeneration.NextInt(0, candidates.Count);
            selected.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        int removedKinds = 0;
        foreach (PowerModel[] group in selected)
        {
            // The receipt describes original instances actually removed, even
            // when an AfterRemoved callback creates a new same-kind instance.
            PowerModel[] originals = group.Where(enemy.Powers.Contains).ToArray();
            try
            {
                foreach (PowerModel power in originals)
                    if (enemy.Powers.Contains(power)) await TryPlayerDispel(power);
            }
            finally
            {
                if (originals.Any(power => !enemy.Powers.Contains(power)))
                {
                    removedKinds++;
                    IreneExposeCombatHelper.OnBuffKindDispelled(source, enemy, group[0].Id);
                }
            }
        }
        return removedKinds;
    }
}
