using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomMixedIntentHelper
{
    private static readonly Func<DecimillipedeSegment, int> BulkStrength = Getter<DecimillipedeSegment>("BulkStrength");
    private static readonly Func<LivingShield, int> EnrageStrength = Getter<LivingShield>("EnrageStr");
    private static readonly Func<KnowledgeDemon, int> PonderStrength = Getter<KnowledgeDemon>("PonderStrength");
    private static readonly Func<Myte, int> SuckStrength = Getter<Myte>("SuckStrength");
    private static readonly Func<SkulkingColony, int> InertiaStrength = Getter<SkulkingColony>("InertiaStrengthGain");
    private static readonly Func<InfestedPrism, int> PulsateBlock = Getter<InfestedPrism>("PulsateBlock");
    private static readonly Func<InfestedPrism, int> VitalSparkAmount = Getter<InfestedPrism>("VitalSparkAmount");
    private static readonly Func<WaterfallGiant, int> PressureGunDamage = Getter<WaterfallGiant>("CurrentPressureGunDamage");
    private static readonly Action<WaterfallGiant, int> SetPressureGunDamage = Setter<WaterfallGiant>("CurrentPressureGunDamage");
    private static readonly Func<WaterfallGiant, int> PressureGunIncrease = Getter<WaterfallGiant>("PressureGunIncrease");
    private static readonly Func<WaterfallGiant, int> PressureBuildup = Getter<WaterfallGiant>("PressureBuildupIdx");
    private static readonly Action<WaterfallGiant, int> SetPressureBuildup = Setter<WaterfallGiant>("PressureBuildupIdx");

    internal static async Task AfterAttack(Creature phantom, MoveState move,
        IReadOnlyList<Creature> targets)
    {
        // Damage has already followed the phantom targeting/segment rules. Replay
        // only the mixed move's effects, never its attack or enemy-only UI actions.
        using IDisposable scope = PhantomAscensionHelper.BeginPhantomEvaluation();
        var context = new ThrowingPlayerChoiceContext();
        switch (phantom.Monster)
        {
            case KnowledgeDemon demon when move.Id == "PONDER_MOVE":
                await CreatureCmd.Heal(phantom, 30 * phantom.CombatState.Players.Count);
                break;
            case InfestedPrism prism when move.Id == "PULSATE_MOVE":
                await CreatureCmd.GainBlock(phantom, PulsateBlock(prism), ValueProp.Move, null);
                await PowerCmd.Apply<VitalSparkPower>(context, phantom,
                    VitalSparkAmount(prism), phantom, null);
                return;
            case TerrorEel when move.Id == "THRASH_MOVE":
                await PowerCmd.Apply<VigorPower>(context, phantom, 6m, phantom, null);
                return;
            case WaterfallGiant giant when move.Id is "STOMP_MOVE" or "RAM_MOVE" or
                "PRESSURE_GUN_MOVE" or "PRESSURE_UP_MOVE":
                if (move.Id == "PRESSURE_GUN_MOVE")
                    SetPressureGunDamage(giant, PressureGunDamage(giant) + PressureGunIncrease(giant));
                await PowerCmd.Apply<SteamEruptionPower>(context, phantom, 3m, phantom, null);
                // This count also controls ShouldFadeAfterDeath. Keep it in sync
                // without the native method's enemy music and Spine-track updates.
                SetPressureBuildup(giant, PressureBuildup(giant) + 1);
                return;
        }

        int strength = phantom.Monster switch
        {
            CubexConstruct when move.Id is "REPEATER_BLAST_MOVE" or "REPEATER_BLAST_MOVE_2" => 2,
            DecimillipedeSegment segment when move.Id == "BULK_MOVE" => BulkStrength(segment),
            // Native HEHE also steals from players; phantoms retain only its own buff.
            GremlinMerc when move.Id == "HEHE_MOVE" => 2,
            LivingShield shield when move.Id == "SMASH_MOVE" => EnrageStrength(shield),
            GlobeHead when move.Id == "GALVANIC_BURST" => 2,
            KnowledgeDemon demon when move.Id == "PONDER_MOVE" => PonderStrength(demon),
            Fogmog when move.Id is "SWIPE_MOVE" or "SWIPE_RANDOM_MOVE" => 1,
            Myte myte when move.Id == "SUCK_MOVE" => SuckStrength(myte),
            SkulkingColony colony when move.Id == "INERTIA_MOVE" => InertiaStrength(colony),
            SludgeSpinner when move.Id == "RAGE_MOVE" => 3,
            SlumberingBeetle when move.Id == "ROLL_OUT_MOVE" => 2,
            SnappingJaxfruit when move.Id == "ENERGY_ORB_MOVE" => 2,
            TheAdversaryMkOne when move.Id == "BARRAGE_MOVE" => 2,
            TheAdversaryMkTwo when move.Id == "BARRAGE_MOVE" => 3,
            TheAdversaryMkThree when move.Id == "BARRAGE_MOVE" => 4,
            // Its intent's base damage already includes this move's pre-attack +2.
            // Apply the lasting gain afterward so the current attack is not boosted twice.
            DeathSlug when move.Id == "P2_DEATH_BURST_MOVE" => 2,
            _ => 0
        };
        if (strength > 0)
            await PowerCmd.Apply<StrengthPower>(context, phantom, strength, phantom, null);
        // CeremonialBeast remains in its existing helper and must not gain twice.
    }

    private static Func<T, int> Getter<T>(string property) =>
        AccessTools.MethodDelegate<Func<T, int>>(AccessTools.PropertyGetter(typeof(T), property));

    private static Action<T, int> Setter<T>(string property) =>
        AccessTools.MethodDelegate<Action<T, int>>(AccessTools.PropertySetter(typeof(T), property));
}
