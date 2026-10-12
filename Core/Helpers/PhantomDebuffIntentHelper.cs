using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomDebuffIntentHelper
{
    private static readonly Func<SoulFysh, int> ScreamAmount =
        AccessTools.MethodDelegate<Func<SoulFysh, int>>(
            AccessTools.PropertyGetter(typeof(SoulFysh), "ScreamMoveAmount"));
    private static readonly Action<OwlMagistrate, bool> SetFlying =
        AccessTools.MethodDelegate<Action<OwlMagistrate, bool>>(
            AccessTools.PropertySetter(typeof(OwlMagistrate), "IsFlying"));

    internal static void BeforeAttack(Creature phantom, MoveState move)
    {
        // The native dive/reappearance clears its visual state before dealing
        // damage. Intent replay bypasses those native move bodies.
        if (phantom?.Monster is OwlMagistrate owl && move?.Id == "VERDICT")
            SetFlying(owl, false);
        else if (phantom?.Monster is SoulFysh soul && move?.Id == "SCREAM_MOVE")
            soul.IsInvisible = false;
        else if (phantom?.Monster is KnowledgeDemon demon)
        {
            if (move?.Id == "KNOWLEDGE_OVERWHELMING_MOVE")
                demon.IsBurnt = true;
            else if (move?.Id == "PONDER_MOVE")
                demon.IsBurnt = false;
        }
    }

    internal static async Task AfterAttack(Creature phantom, MoveState move,
        IReadOnlyList<Creature> targets)
    {
        if (phantom?.CombatState is not { } combatState || move == null)
            return;

        using IDisposable scope = PhantomAscensionHelper.BeginPhantomEvaluation();
        int draw = phantom.Monster?.GetType().Assembly == typeof(MonsterModel).Assembly
            ? move.Intents.OfType<StatusIntent>().Sum(intent => intent.CardCount)
            : 0;
        if (draw > 0)
            PhantomPlayerBenefitContext.QueueDraw(draw);
        if (phantom.Monster is LivingFog && move.Id == "ADVANCED_GAS_MOVE")
            PhantomPlayerBenefitContext.QueueHandLimit(1);
        else if (phantom.Monster is VineShambler && move.Id == "GRASPING_VINES_MOVE")
            PhantomPlayerBenefitContext.QueueEnergy(1);

        (int weak, int frail, int vulnerable) = phantom.Monster switch
        {
            Axebot when move.Id == "HAMMER_UPPERCUT_MOVE" => (2, 2, 0),
            DecimillipedeSegment when move.Id == "CONSTRICT_MOVE" => (1, 0, 0),
            FakeMerchantMonster when move.Id == "THROW_RELIC_MOVE" => (0, 1, 0),
            Flyconid when move.Id == "FRAIL_SPORES_MOVE" => (0, 2, 0),
            FossilStalker when move.Id == "TACKLE_MOVE" => (0, 1, 0),
            FrogKnight when move.Id == "TONGUE_LASH" => (0, 2, 0),
            GlobeHead when move.Id == "SHOCKING_SLAP" => (0, 2, 0),
            GremlinMerc when move.Id == "DOUBLE_SMASH_MOVE" => (2, 0, 0),
            KinPriest when move.Id == "ORB_OF_FRAILTY_MOVE" => (0, 1, 0),
            KinPriest when move.Id == "ORB_OF_WEAKNESS_MOVE" => (1, 0, 0),
            LouseProgenitor when move.Id == "WEB_CANNON_MOVE" => (0, 2, 0),
            Ovicopter when move.Id == "TENDERIZER_MOVE" => (0, 0, 2),
            OwlMagistrate when move.Id == "VERDICT" => (0, 0, 4),
            PunchConstruct when move.Id == "FAST_PUNCH_MOVE" => (0, 1, 0),
            SludgeSpinner when move.Id == "OIL_SPRAY_MOVE" => (1, 0, 0),
            SoulFysh soul when move.Id == "SCREAM_MOVE" => (0, 0, ScreamAmount(soul)),
            SoulNexus when move.Id == "DRAIN_LIFE_MOVE" => (2, 0, 2),
            Stabbot when move.Id == "STAB_MOVE" => (0, 1, 0),
            TestSubject when move.Id == "SKULL_BASH_MOVE" => (0, 0, 1),
            WaterfallGiant when move.Id == "STOMP_MOVE" => (1, 0, 0),
            // Crusher's BUG_STING_MOVE stays in PhantomKaiserClawHelper.
            // Status-card benefits are queued separately above.
            _ => (0, 0, 0)
        };
        if (weak == 0 && frail == 0 && vulnerable == 0)
            return;

        // Native mixed moves apply powers to the whole move-target list once,
        // after all attack segments. A killed target may still accept powers,
        // so PowerCmd's own CanReceivePowers check cannot replace IsAlive here.
        Creature[] livingTargets = targets.Where(target =>
            target != null && target.IsAlive && target.Side != phantom.Side &&
            target.CombatState == combatState && combatState.ContainsCreature(target) &&
            target.CanReceivePowers).ToArray();
        var context = new ThrowingPlayerChoiceContext();
        if (vulnerable > 0)
            await PowerCmd.Apply<VulnerablePower>(context, livingTargets,
                vulnerable, phantom, null);
        if (weak > 0)
            await PowerCmd.Apply<WeakPower>(context, livingTargets, weak, phantom, null);
        if (frail > 0)
            await PowerCmd.Apply<FrailPower>(context, livingTargets, frail, phantom, null);

        if (phantom.Monster is OwlMagistrate && move.Id == "VERDICT")
            await PowerCmd.Remove<SoarPower>(phantom);
    }
}
