using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

static partial class ActRosterChecks
{
    static bool Protected(PowerModel power) => (bool)Static("HBRGOSakiMod.Core.Helpers.PowerDispelRules", "IsUndispellable", power)!;
    static async Task IvoryAbyssProtectionCases()
    {
        foreach (Type type in new[] { typeof(HbrContinuousDamagePower), typeof(GhostStatePower), typeof(SkullFeatherPower), typeof(PhaseWarpPower), typeof(IntangiblePower) })
            if (!ModelDb.Contains(type)) Inject(type);
        foreach (int asc in new[] { 0, 10 })
        {
            var ivory = FlavorRoom(typeof(IvoryScooper), asc);
            Creature pet = PhantomActor(typeof(SmallHopper));
            Set(Combat, "_allies", new List<Creature> { Hero, pet });
            SimulatePassThrough = false;
            var move = MoveOf(ivory, "DESTROY_BASH_MOVE");
            Check(move.Intents.Any(intent => intent is DebuffIntent), "M16 Destroy Bash forecasts its original poison-burst attachment");
            int attacks = Attacks.Count; await move.PerformMove(new[] { Hero });
            Check(Attacks.Count == attacks + 1 && AttackScopes.Last() == "AllUnits", "M16 poison-burst keeps the original one-hit all-unit attack");
            foreach (Creature target in new[] { Hero, pet })
            {
                var power = target.Powers.OfType<HbrContinuousDamagePower>().Single();
                Near(EffectStrength(power), asc == 0 ? 10m : 15m, "M16 actual all-unit attachment keeps normal10/deadly15percent fixed HP-max potency");
                Check(power.Amount == 4, "M16 actual poison-burst duration is four owner-turn starts even when block absorbed the attack");
            }
            ResetTable(HeroKind.Hbr, asc);
            Creature phantom = PhantomActor(typeof(IvoryScooper)); SetUp(phantom.Monster!);
            await MoveOf(phantom.Monster!, "DESTROY_BASH_MOVE").PerformMove(new[] { Hero });
            Check(!Hero.HasPower<HbrContinuousDamagePower>(), "M16 allied Ivory phantom retains its original attack without new enemy-only poison-burst");

            var abyss = FlavorRoom(typeof(AbyssKnockerKiller), asc);
            pet = PhantomActor(typeof(SmallHopper)); Set(Combat, "_allies", new List<Creature> { Hero, pet });
            move = MoveOf(abyss, "RAGE_WAVE_MOVE");
            Check(move.Intents.Any(intent => intent is DebuffIntent), "M17 Disaster Roar advertises its Calamity attachment while preserving internal RageWave id");
            await move.PerformMove(new[] { Hero });
            foreach (Creature target in new[] { Hero, pet })
            {
                var power = target.Powers.OfType<HbrCalamityPower>().Single();
                Check(power.Amount == (asc == 0 ? 20 : 30) && power.RemainingTurns == 2,
                    "M17 actual Disaster Roar applies twenty/thirty fixed stat reduction and independent two-turn duration to every unit");
            }
            ResetTable(HeroKind.Hbr, asc);
            phantom = PhantomActor(typeof(AbyssKnockerKiller)); SetUp(phantom.Monster!);
            await MoveOf(phantom.Monster!, "RAGE_WAVE_MOVE").PerformMove(new[] { Hero });
            Check(!Hero.HasPower<HbrCalamityPower>(), "M17 allied Abyss phantom never inherits Disaster Roar's enemy-only Calamity");
        }

        // Actual mutable native instances exercise the shared player-dispel
        // policy; removal below remains the real native transition operation.
        foreach ((Type monster, Type marker) in new[]
        {
            (typeof(PillarGuardian), typeof(PillarOverdriveDetectionPower)),
            (typeof(BarrelWalker), typeof(BarrelDemiseCountdownPower)),
            (typeof(DiamondEyeball), typeof(DiamondDpCheckPower)),
            (typeof(BigotryGate), typeof(FaintnessStormCountdownPower)),
            (typeof(ShrimpCrab), typeof(ShrimpRunawayPower)),
            (typeof(TezukaSaki), typeof(GhostStatePower)),
            (typeof(SkullFeather), typeof(SkullFeatherPower)),
            (typeof(FlatHand), typeof(PhaseWarpPower))
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            Creature actor = Actor(monster, 1000);
            PowerModel core = (PowerModel)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(marker), null, actor, 1)!;
            Check(Protected(core), "M18 enemy HBR core cannot be selected by player dispels: " + marker.Name);
            Power<StrengthPower>(actor, 2);
            await (Task)Static("HBRGOSakiMod.Core.Helpers.PowerDispelRules", "RandomRemoveBuffs", HeroPlayer, actor, 1)!;
            Check(actor.Powers.Contains(core) && !actor.HasPower<StrengthPower>(), "M18 protected core does not spend the random dispel's one eligible-buff quota: " + marker.Name);
            await PowerCmd.Remove(core);
            Check(!actor.Powers.Contains(core), "M18 native self-transition removal remains available: " + marker.Name);
            Creature ally = PhantomActor(monster);
            core = (PowerModel)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(marker), null, ally, 1)!;
            Check(!Protected(core), "M18 player-side phantom core remains dispellable: " + marker.Name);
        }
        foreach (Type monster in new[] { typeof(GoldHopper), typeof(RedCrimsonBossMonster) })
        {
            ResetTable(HeroKind.Hbr, 0); Creature actor = Actor(monster, 1000);
            Set(actor.Monster!, "_hasAppliedStartingIntangible", true);
            var intangible = Power<IntangiblePower>(actor, 99); Set(intangible, "_applier", actor);
            Check(Protected(intangible), "M18 self-applied original99 Intangible core is protected for " + monster.Name);
            Set(intangible, "_applier", Hero);
            Check(!Protected(intangible), "M18 an unrelated caster's Intangible remains dispellable for " + monster.Name);
            if (monster == typeof(RedCrimsonBossMonster))
            {
                await PowerCmd.Remove(intangible); SetUp(actor.Monster!);
                await MoveOf(actor.Monster!, "WEAKENING_END_1_MOVE").PerformMove(new[] { Hero });
                Check(actor.GetPower<IntangiblePower>() is { } renewed && Protected(renewed), "M18 Red Crimson's real WeakeningEnd regrant is the same protected self-applied mechanism");
            }
        }
        ResetTable(HeroKind.Hbr, 0); Creature skull = Actor(typeof(SkullFeather), 1000);
        Check(!Protected(Power<IntangiblePower>(skull, 99)), "M18 SkullFeather's separate Intangible grant is intentionally still dispellable");
        Check(!Protected(Power<HbrAttackUpPower>(skull, 1)) && !Protected(Power<HbrDefenseUpPower>(skull, 1)) && !Protected(Power<HbrProvokePower>(skull, 1)),
            "M18 generic attack/defense buffs and Provoke are never blanket protected");
    }
}
