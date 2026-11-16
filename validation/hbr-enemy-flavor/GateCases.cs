using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

static partial class ActRosterChecks
{
    static async Task GateFlavorCases()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var gate = (BigotryGate)FlavorRoom(typeof(BigotryGate), 0, variant);
            Creature actor = gate.Creature;
            Check(actor.GetPower<FaintnessStormCountdownPower>()?.Amount == 4,
                $"M15 gate variant{variant} initializes its real countdown at4");
            for (int turn = 1; turn <= 12; turn++)
            {
                bool storm = gate.NextMove.Id == "FAINTNESS_STORM_MOVE";
                Check(storm == (turn % 4 == 0), $"M15 gate variant{variant} turn{turn} has exactly the promised fourth-turn intent");
                int cursor = gate.MoveTableStep, mask = gate.DpOnceUsedMask, attacks = Attacks.Count;
                if (storm)
                    Check(gate.NextMove.Intents.Any(intent => intent is DebuffIntent) && gate.NextMove.Intents.Any(intent => intent is BuffIntent) &&
                        !gate.NextMove.Intents.Any(intent => intent is AttackIntent), "M15 Faintness Storm forecasts only debuff+buff, never damage");
                await FlavorTurn(gate);
                if (storm)
                {
                    Check(Attacks.Count == attacks, "M15 actual fourth-turn storm performs no attack command");
                    Check(FlavorBeforeRollStep == cursor && FlavorBeforeRollMask == mask && gate.NextMove.Id == gate.FlavorQueuedTableMoveId && gate.NextMove.Id != "FAINTNESS_STORM_MOVE",
                        "M15 storm preserves the already selected table step and resumes its held ordinary intent");
                    Check(actor.GetPower<FaintnessStormCountdownPower>()!.Amount == 4,
                        "M15 storm resets to4 without immediately ticking down again at the same enemy-turn end");
                    var defense = actor.Powers.OfType<HbrDefenseUpPower>().Last();
                    Near(EffectStrength(defense), ExpectedEnemyBuff(50m, actor, HeroKind.Hbr), "M15 real defense buff uses basis50 through the existing enemy-parameter formula");
                    Check(Hero.HasPower<HbrStunPower>(), "M15 actual storm installs the existing player stun status");
                    int losses = EnergyLosses.Count;
                    foreach (HbrStunPower stun in Hero.Powers.OfType<HbrStunPower>().ToArray()) await stun.AfterEnergyReset(HeroPlayer);
                    Check(EnergyLosses.Count == losses + 1 && EnergyLosses.Last().Amount == 2m, "M15 next player energy reset loses exactly two energy once");
                }
            }
        }

        var broken = (BigotryGate)FlavorRoom(typeof(BigotryGate));
        for (int turn = 1; turn <= 3; turn++) await FlavorTurn(broken);
        FlavorDpBreak(broken);
        Check(broken.NextMove.Id == "FAINTNESS_STORM_MOVE", "M15 breaking the gate's DP cannot cancel the already announced fourth-turn storm");
        await FlavorTurn(broken);
        Check(Hero.HasPower<HbrStunPower>() && broken.Creature.HasPower<HbrDefenseUpPower>(), "M15 a gate in Break still performs both storm effects");

        var reference = (BigotryGate)FlavorRoom(typeof(BigotryGate));
        decimal referenceE = (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyProfileHelper", "GetEnemyParameter", reference.Creature)!;
        Power<HbrCalamityPower>(reference.Creature, (int)(referenceE - 100m));
        Set(reference.Creature.GetPower<FaintnessStormCountdownPower>()!, "_amount", 1); RefreshFlavor(reference);
        await MoveOf(reference, "FAINTNESS_STORM_MOVE").PerformMove(new[] { Hero });
        Near(EffectStrength(reference.Creature.Powers.OfType<HbrDefenseUpPower>().Last()), 50m,
            "M15 at enemy state100 the real Faintness Storm's basis50 resolves to exactly fifty-percent defense-up");

        var first = (BigotryGate)FlavorRoom(typeof(BigotryGate));
        var second = (BigotryGate)TableActor(typeof(BigotryGate), 1, out Creature other);
        SetUp(second); await second.AfterAddedToRoom();
        Creature phantom = PhantomActor(typeof(SmallHopper));
        Set(first.Creature.GetPower<FaintnessStormCountdownPower>()!, "_amount", 1);
        Set(second.Creature.GetPower<FaintnessStormCountdownPower>()!, "_amount", 1);
        Static("HBRGOSakiMod.Core.Helpers.HbrEnemyFlavorSchedulingHelper", "RefreshOwner", first.Creature);
        Static("HBRGOSakiMod.Core.Helpers.HbrEnemyFlavorSchedulingHelper", "RefreshOwner", second.Creature);
        MoveState firstStorm = MoveOf(first, "FAINTNESS_STORM_MOVE"), secondStorm = MoveOf(second, "FAINTNESS_STORM_MOVE");
        await firstStorm.PerformMove(new[] { Hero, phantom }); await secondStorm.PerformMove(new[] { Hero, phantom });
        Check(Hero.Powers.OfType<HbrStunPower>().Any() && !phantom.HasPower<HbrStunPower>(),
            "M15 multiple gates apply stun to actual players only, never their summons");
        int beforeLosses = EnergyLosses.Count;
        foreach (HbrStunPower stun in Hero.Powers.OfType<HbrStunPower>().ToArray()) await stun.AfterEnergyReset(HeroPlayer);
        Check(EnergyLosses.Count == beforeLosses + 1 && EnergyLosses.Last().Amount == 2m,
            "M15 simultaneous storms share the native single action-lock loss instead of stacking energy penalties");

        ResetTable(HeroKind.Hbr, 0);
        Creature allied = PhantomActor(typeof(BigotryGate)); var replay = allied.Monster!;
        SetUp(replay); await replay.AfterAddedToRoom();
        Check(!allied.HasPower<FaintnessStormCountdownPower>(), "M15 an allied phantom gate never receives the enemy-only countdown");
    }
}
