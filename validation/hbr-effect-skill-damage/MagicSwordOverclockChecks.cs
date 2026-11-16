using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

// Run the real OD hand setup through its energy command, then stop at the first
// continuation guard. This covers the inserted energy pool, not OD UI or a full turn.
static partial class EffectChecks
{
    static readonly List<(Player Player, decimal Amount)> MagicSwordOverclockEnergyCalls = [];
    static int MagicSwordOverclockContinuationCalls;

    static bool MagicSwordOverclockSetEnergyLeaf(object[] __args, ref Task __result)
    {
        MagicSwordOverclockEnergyCalls.Add((__args.OfType<Player>().Single(),
            Convert.ToDecimal(__args.First(value => value is int or decimal))));
        __result = Task.CompletedTask;
        return false;
    }

    static bool MagicSwordOverclockStopAfterEnergy(ref Task<bool> __result)
    {
        MagicSwordOverclockContinuationCalls++;
        __result = Task.FromResult(false);
        return false;
    }

    static async Task MagicSwordOverclockCases()
    {
        Type coordinator = Helper("OverclockImmediateTurnCoordinator");
        Type sessionType = coordinator.GetNestedType("Session", F)!;
        MethodInfo deal = coordinator.GetMethod("DealOverclockHand", F)!;
        MethodInfo continuation = coordinator.GetMethod("CanContinue", F)!;
        MethodInfo setEnergyLeaf = typeof(EffectChecks).GetMethod(nameof(MagicSwordOverclockSetEnergyLeaf), F)!;
        MethodInfo stop = typeof(EffectChecks).GetMethod(nameof(MagicSwordOverclockStopAfterEnergy), F)!;
        MethodInfo[] setters = typeof(PlayerCmd).GetMethods(F)
            .Where(method => method.Name == "SetEnergy" && method.ReturnType == typeof(Task)).ToArray();
        Check(setters.Length > 0, "OD energy command is available for magic sword inserted-turn checks");
        var harmony = new Harmony(ProductionId + ".magic-sword-overclock-energy-leaf");

        async Task Deal(object session, Player player, decimal expected, string label)
        {
            int before = MagicSwordOverclockEnergyCalls.Count;
            int checkpoint = MagicSwordOverclockContinuationCalls;
            await (Task)Invoke(deal, null, session, player, null)!;
            Check(MagicSwordOverclockEnergyCalls.Count == before + 1 &&
                  MagicSwordOverclockEnergyCalls[^1] == (player, expected),
                label + " sets this player's OD pool exactly once to " + expected);
            Check(MagicSwordOverclockContinuationCalls == checkpoint + 1,
                label + " reaches the real post-energy guard before the fixture stops further setup");
        }

        MagicSwordOverclockEnergyCalls.Clear();
        MagicSwordOverclockContinuationCalls = 0;
        try
        {
            foreach (MethodInfo setter in setters) harmony.Patch(setter, prefix: new HarmonyMethod(setEnergyLeaf));
            harmony.Patch(continuation, prefix: new HarmonyMethod(stop));
            foreach ((int level, decimal baseline) in new[] { (1, 2m), (2, 3m), (3, 5m) })
            {
                Reset(); As31O(true);
                var peer = Blank<Player>();
                Set(peer, "_runState", RunState);
                var peerHero = (Creature)H("Actor", null, peer, null)!;
                Set(peer, "<Creature>k__BackingField", peerHero);
                var relic = Blank<Overclock>();
                Set(relic, "_activeLevel", level);
                object session = sessionType.GetConstructors(F).Single()
                    .Invoke(new object[] { relic, Combat, new[] { Player, peer } });
                var ownMode = Power<MagicSwordModePower>(Hero, 1);

                await Deal(session, Player, baseline + 1m, $"OD {level} with holder's magic sword");
                await Deal(session, peer, baseline, $"OD {level} teammate without magic sword");
                var peerMode = Power<MagicSwordModePower>(peerHero, 1);
                await Deal(session, peer, baseline + 1m, $"OD {level} teammate with their own magic sword");
                Powers(Hero).Remove(ownMode);
                await Deal(session, Player, baseline, $"OD {level} holder after leaving magic sword");
                await Deal(session, peer, baseline + 1m, $"OD {level} teammate while only their magic sword remains");
                Powers(peerHero).Remove(peerMode);
                await Deal(session, peer, baseline, $"OD {level} teammate after leaving magic sword");
            }
        }
        finally
        {
            foreach (MethodInfo setter in setters) harmony.Unpatch(setter, setEnergyLeaf);
            harmony.Unpatch(continuation, stop);
            MagicSwordOverclockEnergyCalls.Clear();
            MagicSwordOverclockContinuationCalls = 0;
            As31O(false); Reset();
        }
    }
}
