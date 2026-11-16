using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

static partial class FrameworkChecks
{
    static bool Undispellable(PowerModel power) => (bool)Call("PowerDispelRules", "IsUndispellable", power)!;
    static void NararaDispelCases()
    {
        Reset();
        foreach (var type in new[] { typeof(HardenedShellPower), typeof(StrengthPower), typeof(NararaSleepingPower),
                     typeof(NararaNullificationPower), typeof(NararaPressurePower), typeof(TourGuidePower),
                     typeof(NararaOverworkPower), typeof(NararaGiveYouPower), typeof(NararaLayeredDpPower) })
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        MonsterModel monster = (MonsterModel)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(NararaShibaBossMonster)), null)!;
        Creature boss = (Creature)FixtureCall("Actor", monster, null, null)!;
        var strength = Raw<StrengthPower>(boss, 5);
        var sleep = Raw<NararaSleepingPower>(boss, 3);
        var cap = Raw<HardenedShellPower>(boss, 25);
        PowerModel[] intrinsic = [sleep, Raw<NararaNullificationPower>(boss, 1), Raw<NararaPressurePower>(boss, 1),
            Raw<TourGuidePower>(boss, 1), Raw<NararaOverworkPower>(boss, 1), Raw<NararaGiveYouPower>(boss, 1),
            Raw<NararaLayeredDpPower>(boss, 1)];
        Check(intrinsic.All(Undispellable), "all seven unique Narara ability classes are protected, including the Type.None layered DP");
        Check(!Undispellable(cap) && !Undispellable(strength), "Hardened Shell and generic Strength remain dispellable on Narara");
        var negative = Raw<StrengthPower>(boss, -2);
        Check(!Undispellable(negative), "native negative Strength is a debuff and does not acquire the buff-only keyword");
        Powers(boss).Remove(negative);
        Set(State<object>("RunState"), "<Rng>k__BackingField", new RunRngSet("NARARA-DISPEL"));
        CardModel card = OwnedCard(typeof(HelloWorldHbrgoSaki));
        ((Task)Call("HelloWorldCombatHelper", "RemoveRandomBuffs", card, boss, 2)!).GetAwaiter().GetResult();
        Check(!Powers(boss).Contains(strength) && !Powers(boss).Contains(cap) && intrinsic.All(power => Powers(boss).Contains(power)),
            "actual Hello World removes two generic buff kinds and preserves every intrinsic Narara mechanism");
        Check(!(bool)((Task<bool>)Call("PowerDispelRules", "TryPlayerDispel", sleep)!).GetAwaiter().GetResult(),
            "shared player-dispel API also refuses direct attempts outside the candidate filter");
        Apply<HbrAttackUpPower>(15m, boss, 1, Profile("Default", "EnemyTurnEnd", 1));
        var timed = Existing<HbrAttackUpPower>(boss).Single();
        Check(!Undispellable(timed), "resentment's generic attack-up remains removable under the class-specific policy");
        Tick(timed, CombatSide.Enemy, boss);
        Check(!Powers(boss).Contains(timed), "natural framework expiration remains available on Narara");
        strength = Raw<StrengthPower>(boss, 5);
        PowerCmd.Remove(strength).GetAwaiter().GetResult();
        Check(!Powers(boss).Contains(strength), "boss mechanism's direct removal remains available");
        Set(boss, "_petOwner", Player); Set(boss, "<Side>k__BackingField", CombatSide.Player);
        Check(!Undispellable(sleep), "a player-owned Narara phantom does not inherit the enemy-only keyword");
        Set(boss, "_petOwner", null); Set(boss, "<Side>k__BackingField", CombatSide.Enemy);
        FixtureCall("Mode", false, 1);
        Check(!Undispellable(sleep), "legacy numeric mode retains its previous dispel behavior");
        Reset();
        Apply<HbrAttackUpPower>(15m, Enemy); Apply<HbrAttackUpPower>(20m, Enemy);
        Raw<StrengthPower>(Enemy, 5);
        ((Task)Call("HelloWorldCombatHelper", "RemoveRandomBuffs", card, Enemy, 2)!).GetAwaiter().GetResult();
        Check(Powers(Enemy).Count == 0,
            "ordinary enemy dispel chooses two ability kinds and removes every independent instance of each selected kind");
        Reset();
    }
}
