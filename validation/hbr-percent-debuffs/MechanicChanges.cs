using System.Reflection;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Models.Capabilities;

static partial class PercentDebuffChecks
{
    static void Replay(HotaruReplayPain? card = null) => FixtureCall("Play", card ?? Card<HotaruReplayPain>(), null);
    static PowerModel[] PercentagePowers(Creature? target = null) => (target ?? Enemy).Powers
        .Where(p => p.Amount > 0 && p is FragilePower or WeakPower or VulnerablePower).ToArray();
    static SaviorGrandEntrancePower SeaBreeze()
    {
        var power = Power<SaviorGrandEntrancePower>(Hero);
        typeof(SaviorGrandEntrancePower).GetProperty("UpgradeEffects", F)!.SetValue(power, new[] { 5 });
        return power;
    }

    static void MechanicChangeCases()
    {
        FragileActualDouble();
        ReplayUnlimited();
        ReplayBlocked<FragilePower>(); ReplayBlocked<WeakPower>(); ReplayBlocked<VulnerablePower>();
        ReplayFrozenCallbacks(); ReplayNarara(); ReplayLegacy();
        SeaBreezeSnapshots(); SeaBreezeContest();
        Reset();
    }

    static void FragileActualDouble()
    {
        Reset();
        var first = Apply<FragilePower>(2, Enemy)!; Tick(first);
        var second = Apply<FragilePower>(3, Hero)!;
        PlayDouble();
        Check(Active<FragilePower>().Length == 2 && Powers(Enemy).Contains(first) && Powers(Enemy).Contains(second),
            "actual Double retains both Fragile identities");
        Equal(Percent(first), 75m, "actual Double doubles first Fragile snapshot");
        Equal(Percent(second), 93.75m, "actual Double doubles second Fragile snapshot");
        Check(first.Amount == 2 && second.Amount == 4 && Raw(first) == 2 && Raw(second) == 3,
            "actual Double doubles Fragile remaining time independently and preserves authored strengths");
        Check(ReferenceEquals(first.Applier, Enemy) && ReferenceEquals(second.Applier, Hero),
            "actual Double preserves original Fragile appliers");
        PlayDouble();
        Equal(Percent(first), 150m, "repeated actual Double uses current Fragile effect");
        Equal(Percent(second), 187.5m, "repeated actual Double does not recompute Fragile raw3");
        Check(first.Amount == 4 && second.Amount == 8 && Active<FragilePower>().Length == 2,
            "repeated actual Double doubles Fragile time without a two-turn clamp");
    }

    static void ReplayUnlimited()
    {
        Reset(); Sense("Talent", 75); Sense("Simplicity", 75);
        var originals = new PowerModel[] { Apply<WeakPower>(3, Hero)!, Apply<VulnerablePower>(3, Hero)!, Apply<FragilePower>(3, Hero)! };
        foreach (PowerModel original in originals) Tick(original);
        Sense("Talent", 0); Sense("Simplicity", 0); FixtureCall("Act", 2);
        Equal(Percent(originals[0]), 51.5625m, "Replay setup has a nontrivial saved attribute snapshot");
        foreach (int count in new[] { 2, 4, 8 })
        {
            Replay();
            foreach (PowerModel original in originals)
            {
                var instances = PercentagePowers().Where(p => p.GetType() == original.GetType()).ToArray();
                Check(instances.Length == count && instances.Distinct(ReferenceEqualityComparer.Instance).Count() == count,
                    $"actual Replay {original.GetType().Name} reaches {count} independent instances");
                Check(instances.All(p => Percent(p) == 51.5625m && Raw(p) == 3),
                    $"actual Replay {original.GetType().Name} preserves every frozen percentage and raw strength at {count}");
                Check(original.Amount == 1 && instances.Where(p => !ReferenceEquals(p, original)).All(p => p.Amount == 2),
                    $"actual Replay {original.GetType().Name} leaves original time and gives each copy exactly two turns at {count}");
                Check(instances.Where(p => !ReferenceEquals(p, original)).All(p => ReferenceEquals(p.Applier, Hero)),
                    $"actual Replay {original.GetType().Name} gives copies the actual card owner as applier at {count}");
            }
        }
        foreach (PowerModel original in originals)
        {
            var before = PercentagePowers().Where(p => p.GetType() == original.GetType()).ToArray();
            var generic = typeof(PercentDebuffChecks).GetMethod(nameof(Apply), F)!.MakeGenericMethod(original.GetType());
            Invoke(generic, null, 1, Hero, Enemy);
            Check(before.All(Powers(Enemy).Contains) && PercentagePowers().Count(p => p.GetType() == original.GetType()) == 8,
                $"ordinary weaker {original.GetType().Name} grant cannot add a ninth Replay instance");
            Invoke(generic, null, 6, Hero, Enemy);
            var after = PercentagePowers().Where(p => p.GetType() == original.GetType()).ToArray();
            Check(after.Length == 8 && !after.Contains(original) && before.Where(p => !ReferenceEquals(p, original)).All(after.Contains),
                $"ordinary stronger {original.GetType().Name} grant replaces only the shortest weakest Replay instance");
            Equal(Percent(after.Single(p => !before.Contains(p))), 91.552734375m,
                $"ordinary replacement {original.GetType().Name} computes its own new strength");
        }
        Reset();
        var weak = Apply<WeakPower>(2, Hero)!;
        PowerCmd.ModifyAmount(null!, weak, 1, Hero, null, true).GetAwaiter().GetResult();
        PowerCmd.ModifyAmount(null!, weak, 1, Hero, null, true).GetAwaiter().GetResult();
        Check(Active<WeakPower>().Length == 2, "ordinary positive ModifyAmount copies remain capped at two");
    }

    static void ReplayBlocked<T>() where T : PowerModel
    {
        Reset(); var original = Apply<T>(3, Hero)!; Tick(original);
        var artifact = Power<ArtifactPower>(Enemy);
        Replay();
        Check(Active<T>().Length == 1 && ReferenceEquals(Active<T>()[0], original),
            $"actual Replay {typeof(T).Name} still respects native Artifact");
        Check(artifact.Amount == 0 && !Powers(Enemy).Contains(artifact) && original.Amount == 1 && Raw(original) == 3,
            $"blocked Replay {typeof(T).Name} consumes Artifact and preserves original identity and counters");
        Equal(Percent(original), 46.875m, $"blocked Replay {typeof(T).Name} preserves original effect");
    }

    static void ReplayFrozenCallbacks()
    {
        Reset(); var weak = Apply<WeakPower>(1, Hero)!;
        var vulnerable = Apply<VulnerablePower>(2, Hero)!;
        var fragile = Apply<FragilePower>(3, Hero)!;
        bool reacted = false;
        void Increased(PowerModel power, int amount, bool silent)
        {
            if (reacted || power is not WeakPower || ReferenceEquals(power, weak)) return;
            reacted = true;
            Call("Configure", vulnerable, 99m, 99);
            PowerCmd.Remove(fragile).GetAwaiter().GetResult();
        }
        Enemy.PowerIncreased += Increased;
        try { Replay(); }
        finally { Enemy.PowerIncreased -= Increased; }
        Check(reacted, "actual native PowerIncreased callback runs during Replay");
        Check(Active<WeakPower>().Length == 2 && Active<VulnerablePower>().Length == 2 && Active<FragilePower>().Length == 1,
            "Replay freezes one global list before callbacks and never recursively copies new instances");
        var copiedVulnerable = Active<VulnerablePower>().Single(p => !ReferenceEquals(p, vulnerable));
        Equal(Percent(vulnerable), 99m, "Replay callback really changes a later original effect");
        Equal(Percent(copiedVulnerable), 37.5m, "Replay copy uses pre-play percent despite callback mutation of original");
        Check(Raw(copiedVulnerable) == 2 && copiedVulnerable.Amount == 2, "Replay copy uses pre-play raw strength and a fresh two-turn clock");
        Check(!Powers(Enemy).Contains(fragile), "Replay callback really removes a later original instance");
        Equal(Percent(Active<FragilePower>().Single()), 46.875m, "Replay still copies frozen Fragile snapshot after original removal");
        Check(Raw(Active<FragilePower>().Single()) == 3 && Active<FragilePower>().Single().Amount == 2,
            "Replay removed-original copy retains original authored strength and two turns");
    }

    static void ReplayNarara()
    {
        Reset();
        var boss = (Creature)FixtureCall("Actor", Blank<NararaShibaBossMonster>(), null, null)!;
        var combat = State<MegaCrit.Sts2.Core.Combat.CombatState>("Combat");
        Set(combat, "_enemies", new List<Creature> { boss });
        try
        {
            var original = Apply<WeakPower>(2, Hero, boss)!;
            Power<NararaNullificationPower>(boss);
            var resistance = Power<SixSenseResistancePower>(boss);
            typeof(SixSenseResistancePower).GetProperty("Resistances", F)!.SetValue(resistance, new[] { 10, 10, 10, 10, 10, 10 });
            var card = Card<HotaruReplayPain>(); ModelCapabilities.EnsureInitialized();
            ModelCapabilityRegistry.Register<SixSenseCardCapability>("fixture.percent-debuff.replay");
            var capability = ModelCapabilityRegistry.Create<SixSenseCardCapability>();
            Set(capability, "_attribute", SixSenseAttribute.Simplicity); card.Capabilities().Add(capability, false, false);
            var scope = Invoke(Helper("NararaNullificationHelper").GetMethod("Begin", F)!, null, card)!;
            try
            {
                var artifact = Power<ArtifactPower>(boss);
                Check((bool)Invoke(Helper("NararaNullificationHelper").GetMethod("IsCardNullified", F)!, null, boss, card)!,
                    "actual Replay card scope captures Narara resistance");
                Replay(card);
                Check(Active<WeakPower>(boss).Length == 1 && ReferenceEquals(Active<WeakPower>(boss)[0], original) && original.Amount == 2,
                    "unlimited Replay does not bypass active Narara immunity");
                Check(artifact.Amount == 1, "Narara blocks Replay before native Artifact is consumed");
            }
            finally { Invoke(Helper("NararaNullificationHelper").GetMethod("Close", F)!, null, scope); }
        }
        finally { Set(combat, "_enemies", new List<Creature> { Enemy }); Reset(); }
    }

    static void ReplayLegacy()
    {
        Reset(); Mode(false);
        var weak = Apply<WeakPower>(2, Hero)!; var vulnerable = Apply<VulnerablePower>(3, Hero)!;
        var fragile = Apply<FragilePower>(2, Hero)!;
        Replay();
        Check(Active<WeakPower>().Length == 1 && Active<VulnerablePower>().Length == 1 && Active<FragilePower>().Length == 1,
            "legacy Replay retains native single-instance debuff stacking");
        Check(weak.Amount == 4 && vulnerable.Amount == 6 && fragile.Amount == 4,
            "legacy Replay doubles all three authored native counters");
        Equal(Multiplier(weak, Enemy, Hero), .75m, "legacy Replay preserves native Weak multiplier");
        Equal(Multiplier(vulnerable, Hero, Enemy), 1.5m, "legacy Replay preserves native Vulnerable multiplier");
        Reset();
    }

    static void SeaBreezeSnapshots()
    {
        Reset(); SeaBreeze();
        var baseVulnerable = Apply<VulnerablePower>(1, Hero)!;
        Equal(Percent(baseVulnerable), 60m, "Sea Breeze doubles defense-down base30 to60 before attributes");
        Equal(Percent(Apply<WeakPower>(1, Hero)!), 30m, "Sea Breeze never doubles attack-down base");
        Equal(Percent(Apply<FragilePower>(1, Hero)!), 30m, "Sea Breeze never changes actual Fragile base");
        Reset(); Sense("Talent", 75); Sense("Simplicity", 75); SeaBreeze();
        var vulnerable = Apply<VulnerablePower>(1, Hero)!;
        Equal(Percent(vulnerable), 66m, "Sea Breeze base60 then positive attribute100 gives66 percent");
        WithRelic<PaperPhrog>(() => Equal(Damage(Enemy, Hero), 1910m,
            "Sea Breeze preserves PaperPhrog additive25 percentage points without doubling them"));
        Power<CrueltyPower>(Hero, 20);
        WithRelic<PaperPhrog>(() => Equal(Damage(Enemy, Hero), 2110m,
            "Sea Breeze preserves Cruelty additive20 percentage points without doubling them"));
        Scars(25);
        WithRelic<PaperPhrog>(() => Equal(Damage(Enemy, Hero), 2387m,
            "Scars enhances the complete Sea Breeze native defense-down offset exactly once before final floor"));
        Powers(Hero).Remove(Hero.GetPower<CrueltyPower>()!); Powers(Hero).Remove(Hero.GetPower<HotaruUnfadingScarsPower>()!);
        Replay();
        Check(Active<VulnerablePower>().Length == 2 && Active<VulnerablePower>().All(p => Percent(p) == 66m),
            "Replay preserves boosted defense-down snapshot without applying Sea Breeze twice");
        PlayDouble();
        Check(Active<VulnerablePower>().All(p => Percent(p) == 132m && p.Amount == 4),
            "actual Double doubles boosted defense-down effect and time once");
        var saved = SavedProperties.From(Active<VulnerablePower>()[0])!;
        var writer = new PacketWriter(); saved.Serialize(writer); var reader = new PacketReader(); reader.Reset(writer.Buffer);
        var bag = new SavedProperties(); bag.Deserialize(reader);
        Reset(); var restored = Power<VulnerablePower>(Enemy, 4); bag.FillInternal(restored);
        Equal(Percent(restored), 132m, "real packet restores exact doubled Sea Breeze snapshot after source upgrade is absent");
        Check(Raw(restored) == 1 && reader.BitPosition == writer.BitPosition,
            "Sea Breeze copied snapshot packet preserves authored strength and exact payload shape");
        Equal(Damage(Enemy, Hero), 2320m, "restored Sea Breeze snapshot receives no source bonus again at damage time");
        Reset(); var old = Apply<VulnerablePower>(1, Hero)!; SeaBreeze();
        Equal(Percent(old), 30m, "gaining Sea Breeze never retroactively rescales an existing defense-down snapshot");
        Equal(Damage(Enemy, Hero), 1300m, "existing defense down never receives Sea Breeze as a late final multiplier");
        Equal(Percent(Apply<VulnerablePower>(1, Hero)!), 60m, "future defense-down grant alone receives newly gained Sea Breeze");
        Reset();
    }

    static void SeaBreezeContest()
    {
        Reset();
        WithRelic<Millsage>(() =>
        {
            Set(Player.GetRelic<Millsage>()!, "_contestUpgradeLevel", 6); SeaBreeze();
            var fragile = Apply<FragilePower>(1, Hero)!;
            var view = (PowerModel)Invoke(Helper("HotaruContestVulnerabilityHelper").GetMethod("GetVulnerable", F)!, null, Enemy)!;
            Equal(Percent(view), 60m, "Contest virtual defense-down applies Sea Breeze to Fragile snapshot base");
            Equal(Percent(fragile), 30m, "Contest conversion leaves the actual Fragile effect unchanged");
            Check(!Powers(Enemy).Contains(view) && Raw(view) == 1 && Active<VulnerablePower>().Length == 0,
                "Sea Breeze Contest view remains outside actual native slots with original authored strength");
            Equal(Damage(Enemy, Hero), 1600m, "Sea Breeze Contest defense-down virtual view is boosted once outside Break");
            Power<BreakPower>(Enemy);
            Equal(Damage(Enemy, Hero), 1900m, "Break combines original Fragile30 and boosted virtual defense-down60 in one bucket");
            Replay();
            view = (PowerModel)Invoke(Helper("HotaruContestVulnerabilityHelper").GetMethod("GetVulnerable", F)!, null, Enemy)!;
            Equal(Percent(view), 120m, "Contest sums both copied Fragile snapshots and doubles only converted defense-down base");
            Check(Active<FragilePower>().All(p => Percent(p) == 30m), "Sea Breeze conversion never mutates Replay Fragile copies");
            Equal(Damage(Enemy, Hero), 2800m, "Replay Fragile60 plus virtual defense-down120 receive no second late boost");
        });
        Reset();
    }
}
