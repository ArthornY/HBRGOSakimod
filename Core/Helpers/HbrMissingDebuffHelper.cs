using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// What replaced the six-sense resistances (2026-11-09) for Hotaru's cards and
/// cannons. "Broken resistances" became the number of debuff kinds on the target,
/// counted like Triple Scars, Scars Polyphony and Silence Here; "cutting a
/// resistance" became one stack of attack down, defense down or fragile that the
/// target does not have yet, chosen at random (nothing when it has all three).
/// </summary>
internal static class HbrMissingDebuffHelper
{
    internal enum Kind { AttackDown, DefenseDown, Fragile }

    private static readonly Kind[] AllKinds = { Kind.AttackDown, Kind.DefenseDown, Kind.Fragile };

    // The three debuffs a card using these rules names (attack down, defense down, fragile).
    internal static IEnumerable<IHoverTip> HoverTips() => new IHoverTip[]
    {
        HoverTipFactory.FromPower<WeakPower>(), HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<FragilePower>()
    };

    internal static int DebuffKinds(Creature target) =>
        target == null ? 0 : HotaruRareCombatCardHelper.DebuffKinds(target.Powers);

    // Attack and defense down follow the target's numerics: the HBR percentage
    // powers under HBR, native Weak and Vulnerable otherwise.
    internal static bool Has(Creature target, Kind kind) => kind switch
    {
        Kind.AttackDown => HotaruContestVulnerabilityHelper.HasAttackDown(target),
        Kind.DefenseDown => HotaruContestVulnerabilityHelper.HasDefenseDown(target),
        _ => target?.Powers.OfType<FragilePower>().Any(power => power.Amount > 0) == true
    };

    internal static IReadOnlyList<Kind> Missing(Creature target) =>
        target == null ? System.Array.Empty<Kind>() : AllKinds.Where(kind => !Has(target, kind)).ToArray();

    /// <summary>Gives one missing stack per time; stops once the target has all three.</summary>
    internal static async Task<int> ApplyRandomMissing(PlayerChoiceContext context, Creature target,
        Creature applier, CardModel source, int times = 1)
    {
        int applied = 0;
        for (int i = 0; i < times; i++)
        {
            if (target?.IsAlive != true || target.CombatState == null) break;
            IReadOnlyList<Kind> missing = Missing(target);
            if (missing.Count == 0) break;
            Kind kind = missing.Count == 1 ? missing[0]
                : missing[target.CombatState.RunState.Rng.Niche.NextInt(missing.Count)];
            await Apply(context, target, kind, applier, source);
            applied++;
        }
        return applied;
    }

    /// <summary>One stack of each of the three the target does not have yet.</summary>
    internal static async Task ApplyAllMissing(PlayerChoiceContext context, Creature target,
        Creature applier, CardModel source)
    {
        foreach (Kind kind in Missing(target))
        {
            if (target?.IsAlive != true) return;
            await Apply(context, target, kind, applier, source);
        }
    }

    private static Task Apply(PlayerChoiceContext context, Creature target, Kind kind,
        Creature applier, CardModel source) => kind switch
    {
        Kind.AttackDown => HbrPercentDebuffApplication.ApplyAttackDown(context, target, 1m, applier, source),
        Kind.DefenseDown => HbrPercentDebuffApplication.ApplyDefenseDown(context, target, 1m, applier, source),
        _ => PowerCmd.Apply<FragilePower>(context, target, 1m, applier, source)
    };
}
