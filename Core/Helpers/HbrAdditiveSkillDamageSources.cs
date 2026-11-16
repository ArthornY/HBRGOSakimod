using System;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// These native modifiers return raw skill units after the attack's base has
// already crossed the HBR boundary. Keep their own eligibility, counters and
// lifetime intact; only the positive additive result is converted once.
// Do not infer this policy for arbitrary modifiers: several mod hooks already
// return resolved world damage, while Strength/Vigor use separate boundaries.
internal static class HbrAdditiveSkillDamageSources
{
    private static readonly HashSet<Type> Sources = Build();

    internal static IEnumerable<Type> Types => Sources;

    internal static bool Supports(AbstractModel model) =>
        model != null && Sources.Contains(model.GetType());

    private static HashSet<Type> Build()
    {
        var sources = new HashSet<Type> { typeof(PhantomPollutionPower) };
        foreach (string name in new[]
        {
            "Powers.AccuracyPower", "Powers.CalcifyPower", "Powers.LeadershipPower",
            "Powers.PhantomBladesPower", "Powers.TaintedPower",
            "Relics.FakeStrikeDummy", "Relics.MiniatureCannon", "Relics.MysticLighter"
        })
        {
            // Optional lookup keeps a shared candidate compatible with native
            // profiles that do not yet contain a particular named modifier.
            Type type = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + name, false);
            if (type != null) sources.Add(type);
        }
        return sources;
    }
}
