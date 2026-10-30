using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

static partial class PercentDebuffChecks
{
    const string FocusMove = "FOCUSED_BREAKTHROUGH_MOVE";
    static int GhostBonusCalls;
    static void ObserveGhostBonus() => GhostBonusCalls++;
    static void GhostMove(Creature ghost, string id) =>
        Set(ghost.Monster!, "<NextMove>k__BackingField", new MoveState(id, _ => Task.CompletedTask));
    static decimal GhostPreview(Creature target, Creature dealer, decimal units = 9m, ValueProp props = ValueProp.Move) =>
        (decimal)FixtureCall("Preview", target, dealer, units, props, null)!;
    static decimal GhostActual(Creature target, Creature dealer, decimal units = 9m, ValueProp props = ValueProp.Move) =>
        (decimal)FixtureCall("Actual", target, dealer, units, props)!;

    // Independent arithmetic for the authored test profile, without calling
    // the production coefficient helper or base-damage adapter as the oracle.
    static decimal ExpectedEnemyGhost(decimal units, decimal coefficientPercent, decimal defensePercent,
        decimal defense = 100m, decimal enemyParameter = 150m)
    {
        decimal skill = units + coefficientPercent * .036m;
        decimal difference = enemyParameter - defense;
        decimal basis = difference >= 140m ? skill * 200m : difference >= 0m
            ? skill * 100m + skill * 100m * difference / 140m
            : difference <= -140m ? 1m : Math.Max(1m, skill * 100m * (1m + difference / 140m));
        return decimal.Floor(basis * (1m + defensePercent / 100m));
    }
    static decimal ExpectedPhantomGhost(decimal units, decimal coefficientPercent, decimal defensePercent,
        decimal stat = 100m, decimal enemyParameter = 150m)
    {
        decimal basis = Math.Max(1m, (units + coefficientPercent * .036m) * 1000m *
            Math.Max(0m, 1m + (stat - enemyParameter) / 140m));
        basis = decimal.Floor(basis * 100m) / 100m;
        return decimal.Floor(basis * (1m + defensePercent / 100m));
    }

    static void WithGhost(bool phantom, Action<Creature, GhostStatePower> test)
    {
        Reset();
        var combat = State<MegaCrit.Sts2.Core.Combat.CombatState>("Combat");
        var ghost = (Creature)FixtureCall("Actor", Blank<TezukaSaki>(), null, phantom ? Player : null)!;
        GhostMove(ghost, FocusMove);
        var state = Power<GhostStatePower>(ghost);
        if (phantom)
        {
            Power<PhantomPower>(ghost);
            Set(combat, "_allies", new List<Creature> { Hero, ghost });
        }
        else Set(combat, "_enemies", new List<Creature> { Enemy, ghost });
        try { test(ghost, state); }
        finally
        {
            Set(combat, "_allies", new List<Creature> { Hero });
            Set(combat, "_enemies", new List<Creature> { Enemy }); Reset();
        }
    }

    static void GhostShotCases()
    {
        var bonus = typeof(GhostStatePower).GetMethod("GetBonusUnits", F)
            ?? throw new MissingMethodException(typeof(GhostStatePower).FullName, "GetBonusUnits");
        // Observe the real method, preserving its result and every argument.
        var harmony = new Harmony("hbr.percent-debuff.ghost-shot-observer");
        harmony.Patch(bonus, postfix: new HarmonyMethod(typeof(PercentDebuffChecks).GetMethod(nameof(ObserveGhostBonus), F)!));
        try
        {
            EnemyGhostCoefficient(); PhantomGhostCoefficient(); GhostBypasses(); GhostLegacy();
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    static void EnemyGhostCoefficient()
    {
        WithGhost(false, (ghost, state) =>
        {
            GhostBonusCalls = 0;
            Equal(GhostPreview(Hero, ghost), ExpectedEnemyGhost(9m, 0m, 0m), "zero defense down gives no focused skill coefficient bonus");
            Check(GhostBonusCalls == 1, "full enemy focus preview evaluates the real coefficient once");
            var first = Apply<VulnerablePower>(1, ghost, Hero)!;
            var second = Apply<VulnerablePower>(2, ghost, Hero)!;
            Tick(first);
            Equal(Percent(first) + Percent(second), 67.5m, "enemy focused shot fixture has two independent30 and37.5 defense-down snapshots");
            decimal expected = ExpectedEnemyGhost(9m, 67.5m, 67.5m);
            GhostBonusCalls = 0;
            Equal(GhostPreview(Hero, ghost), expected, "enemy focused shot adds summed percentages times0.036 units before incoming attribute curve");
            Check(GhostBonusCalls == 1, "multi-instance focused shot does not evaluate its base bonus recursively");
            GhostBonusCalls = 0;
            Equal(GhostActual(Hero, ghost), expected, "actual focused-shot wrapper matches full native intent preview and independent arithmetic");
            Check(GhostBonusCalls == 1, "actual focused-shot wrapper applies the coefficient exactly once");
            Check(first.Amount == 1 && second.Amount == 2 && Raw(first) == 1 && Raw(second) == 2,
                "focused-shot damage never changes original raw counters or remaining durations");
            Call("Configure", first, 30m, 999);
            Equal(GhostPreview(Hero, ghost), expected, "focused-shot base bonus uses percentage snapshot rather than authored raw counter999");
            Sense("Gentleness", 75); Sense("MentalStrength", 75);
            Equal(GhostPreview(Hero, ghost), ExpectedEnemyGhost(9m, 67.5m, 67.5m, 250m),
                "enemy focused-shot extra coefficient receives the same negative attribute curve as its ordinary base");
            Sense("Gentleness", 0); Sense("MentalStrength", 0);
            var other = Apply<VulnerablePower>(6, Hero, Enemy)!; Call("Configure", other, 999m, 6);
            Equal(GhostPreview(Hero, ghost), expected, "a different enemy with higher defense down never changes this target's focused bonus");
            GhostMove(ghost, "LINGXIAO_HAMMER_MOVE");
            Equal(GhostPreview(Hero, ghost), ExpectedEnemyGhost(9m, 0m, 67.5m), "non-focused Tezuka move receives defense-down multiplier without skill coefficient bonus");
            GhostMove(ghost, FocusMove);
            Equal(GhostPreview(Hero, Enemy), ExpectedEnemyGhost(9m, 0m, 67.5m), "a different damage dealer cannot use another creature's Ghost coefficient bonus");
            Equal(GhostPreview(Hero, ghost, 9m, ValueProp.Move | ValueProp.Unpowered), 9m,
                "unpowered damage receives neither focused skill coefficient conversion nor powered defense down");
            Tick(first);
            Equal(GhostPreview(Hero, ghost), ExpectedEnemyGhost(9m, 37.5m, 37.5m),
                "only expiration removes an independent defense-down percentage from the focused coefficient");
            Call("Configure", second, 0m, 50);
            Equal(GhostPreview(Hero, ghost), ExpectedEnemyGhost(9m, 0m, 0m),
                "an active zero-percent snapshot adds no coefficient despite positive raw counter and duration");
            Equal((decimal)Invoke(Helper("DamageModifierCompatibility").GetMethod("ModifyDamageAdditive", F)!, null,
                state, Hero, 1234m, ValueProp.Move, ghost, null, null)!, 0m,
                "HBR Ghost additive hook contributes no second world-space flat bonus");
        });
    }

    static void PhantomGhostCoefficient()
    {
        WithGhost(true, (ghost, state) =>
        {
            var vulnerable = Apply<VulnerablePower>(1, Hero)!;
            Equal(GhostPreview(Enemy, ghost), ExpectedPhantomGhost(9m, 30m, 30m),
                "allied Tezuka phantom shares the focused coefficient before outgoing attribute conversion");
            Equal(GhostActual(Enemy, ghost), ExpectedPhantomGhost(9m, 30m, 30m),
                "allied phantom actual wrapper matches its focused intent preview");
            Scars(25);
            Equal(GhostPreview(Enemy, ghost), ExpectedPhantomGhost(9m, 37.5m, 37.5m),
                "Scars enhances focused coefficient snapshot and later native defense-down effect once each");
            Equal(Percent(vulnerable), 30m, "Scars enhanced focused shot never overwrites the saved original snapshot");
            Powers(Hero).Remove(Hero.GetPower<HotaruUnfadingScarsPower>()!);
            SeaBreeze();
            Equal(GhostPreview(Enemy, ghost), ExpectedPhantomGhost(9m, 30m, 30m),
                "new Sea Breeze does not retroactively boost focused-shot coefficient from an older snapshot");
            var boosted = Apply<VulnerablePower>(1, Hero)!;
            Equal(Percent(boosted), 60m, "focused-shot fixture receives actual Sea Breeze base-boosted defense-down snapshot");
            Equal(GhostPreview(Enemy, ghost), ExpectedPhantomGhost(9m, 90m, 90m),
                "focused shot adds old30 plus new Sea Breeze60 snapshots without doubling either again");
            Power<CrueltyPower>(ghost, 20);
            Equal(GhostPreview(Enemy, ghost), ExpectedPhantomGhost(9m, 90m, 130m),
                "Cruelty applies only in each native defense-down multiplier and never in the focused skill coefficient");
            Powers(ghost).Remove(ghost.GetPower<CrueltyPower>()!);
            Powers(Enemy).Remove(vulnerable); Powers(Enemy).Remove(boosted);
            WithRelic<HBRGOSakiMod.Core.Models.Relics.Millsage>(() =>
            {
                Set(Player.GetRelic<HBRGOSakiMod.Core.Models.Relics.Millsage>()!, "_contestUpgradeLevel", 6);
                var fragile = Apply<FragilePower>(1, Hero)!;
                Equal(GhostPreview(Enemy, ghost), ExpectedPhantomGhost(9m, 60m, 60m),
                    "focused shot reads Contest virtual defense down including Sea Breeze base boost");
                Equal(Percent(fragile), 30m, "focused Contest view does not transform its actual Fragile snapshot");
                Check(Active<VulnerablePower>().Length == 0, "focused Contest query creates no real native defense-down instance");
            });
        });
    }

    static void GhostBypasses()
    {
        WithGhost(false, (ghost, _) =>
        {
            Apply<VulnerablePower>(1, ghost, Hero);
            GhostBonusCalls = 0;
            Equal((decimal)FixtureCall("NativeDamage", Hero, ghost, 1000m, ValueProp.Move, null, ModifyDamageHookType.Multiplicative)!, 1300m,
                "multiplicative-only hook does not reinterpret resolved damage as a focused skill coefficient");
            Check(GhostBonusCalls == 0, "multiplicative-only focused query skips base bonus evaluation");
            decimal expected = ExpectedEnemyGhost(9m, 30m, 30m);
            using ((IDisposable)Invoke(Helper("PrecalculatedDamageContext").GetMethod("Begin", F)!, null, null, Hero, expected)!)
            {
                Equal(GhostActual(Hero, ghost, expected), expected, "precalculated focused hit does not receive its coefficient or defense down a second time");
                Check(GhostBonusCalls == 0, "precalculated focused hit bypasses the coefficient method entirely");
            }
            using ((IDisposable)Invoke(Helper("HbrAbsoluteBaseDamageContext").GetMethod("Begin", F)!, null, ghost, null, Hero, 1234m)!)
            {
                Equal(GhostPreview(Hero, ghost, 1234m), 1604m,
                    "absolute world-space focused hit skips coefficient conversion but keeps native defense-down multiplier");
                Check(GhostBonusCalls == 0, "absolute focused hit cannot pick up a legacy-unit base bonus");
            }
            GhostBonusCalls = 0;
            for (int i = 0; i < 3; i++) Equal(GhostPreview(Hero, ghost), expected, "repeated focused preview is stable without cached bonus accumulation " + i);
            Check(GhostBonusCalls == 3, "three repeated previews evaluate three independent coefficients without recursion");
        });
    }

    static void GhostLegacy()
    {
        WithGhost(false, (ghost, _) =>
        {
            Mode(false); var vulnerable = Apply<VulnerablePower>(3, ghost, Hero)!;
            Equal(GhostPreview(Hero, ghost), 22.5m,
                "legacy focused shot retains old raw3 times flat2 then native1.5 multiplier");
            Equal(GhostActual(Hero, ghost), 22.5m, "legacy actual wrapper retains old focused-shot formula without HBR flooring");
            Tick(vulnerable);
            Check(vulnerable.Amount == 3 && !vulnerable.SkipNextDurationTick,
                "legacy player-side application retains native first-tick skip");
            Tick(vulnerable);
            Equal(GhostPreview(Hero, ghost), 19.5m, "legacy focused flat bonus still follows native remaining counter2");
            GhostMove(ghost, "LINGXIAO_HAMMER_MOVE");
            Equal(GhostPreview(Hero, ghost), 13.5m, "legacy non-focused Tezuka move never gains the old additive bonus");
        });
    }

    static void GhostDescriptionCases()
    {
        WithGhost(false, (_, state) =>
        {
            string hbr = Clean(state.Description.GetFormattedText());
            Check(hbr.Contains("基础技能系数增加6‰") && hbr.Contains("目标每有1%的防御力下降") && !hbr.Contains('{'),
                "actual HBR Ghost description binds exact6 permille and percentage coefficient wording");
            Mode(false);
            string legacy = Clean(state.Description.GetFormattedText());
            Check(legacy.Contains("受到的伤害+50%") && legacy.Contains("获得2点力量") && !legacy.Contains("6‰"),
                "legacy Ghost description retains its original template");
        });
        ModelDb.Inject(typeof(GhostStatePower));
        var canonical = ModelDb.Power<GhostStatePower>();
        Set(canonical, "<Id>k__BackingField", ModelDb.GetId(typeof(GhostStatePower)));
        Check(!Clean(canonical.Description.GetFormattedText()).Contains("6‰"),
            "unowned canonical Ghost description retains legacy template without HBR context");
    }
}
