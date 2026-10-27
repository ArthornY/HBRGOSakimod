using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrEnemyDefenseReference
{
    Vitality,
    Spirit
}

// Defense is a property of a skill. It must not change with the number of live
// targets, current DP, or whether damage is being previewed. Curve endpoints
// and thresholds below are STS adaptation data, not extracted enemy skills.
internal readonly record struct HbrEnemySkillProfile(
    HbrEnemyDefenseReference DefenseReference,
    decimal MinimumPowerPerUnit,
    decimal MaximumPowerPerUnit,
    decimal ParameterLimit)
{
    internal static readonly HbrEnemySkillProfile Default =
        new(HbrEnemyDefenseReference.Vitality, 100m, 200m, 140m);

    internal HbrSkillPowerCurve CurveFromUnits(decimal units) => new(
        DamageAmplificationLimits.MultiplySaturated(units, MinimumPowerPerUnit),
        DamageAmplificationLimits.MultiplySaturated(units, MaximumPowerPerUnit),
        ParameterLimit);

    internal decimal EvaluateDefense(HbrBattleStats stats) =>
        HbrOriginalNumericFormula.EvaluateWeightedStat(stats,
            DefenseReference == HbrEnemyDefenseReference.Spirit
                ? new(0, 0, 0, 1, 2, 0)
                : new(0, 0, 0, 2, 1, 0));
}

internal static class HbrEnemySkillProfiles
{
    internal static HbrEnemySkillProfile Get(Creature dealer)
    {
        // Explicit assignments for the mod's ray/beam skills. These are local
        // adaptation choices. Unknown native/third-party skills use the fixed
        // vitality-biased fallback until their own data row is authored.
        string move = dealer?.Monster?.NextMove?.Id;
        bool spirit = dealer?.Monster switch
        {
            RedCrimsonBossMonster => move is "DISAPPEAR_RAY_MOVE" or
                "WORLDS_END_SUPER_NOVA_1_MOVE" or "WORLDS_END_SUPER_NOVA_2_MOVE",
            _ => false
        };
        return spirit ? HbrEnemySkillProfile.Default with
            { DefenseReference = HbrEnemyDefenseReference.Spirit } : HbrEnemySkillProfile.Default;
    }
}
