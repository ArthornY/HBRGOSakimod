using System.Collections;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

static partial class EffectChecks
{
    static async Task PersonalOverclockHandoffCase()
    {
        Player peer = PersonalWorld();
        var original = PersonalCombatCard(PersonalOwned<StrikeIronclad>(), Player.PlayerCombatState!.Hand);
        var relic = Owned<Overclock>(); Set(Player, "_relics", new List<RelicModel> { relic });
        Set(relic, "_activeLevel", 1); Set(relic, "_charge", 200);
        Type od = Helper("OverclockImmediateTurnCoordinator");
        Type sessionType = od.GetNestedType("Session", F)!;
        object session = Activator.CreateInstance(sessionType, F, null, new object[] { relic, Combat, new[] { Player } }, null)!;
        object?[] args = [Player, null];
        Check((bool)Invoke(Helper("OverclockSuspendedHand").GetMethod("TrySuspend", F)!, null, args)!,
            "OD handoff fixture suspends the real owner's original ordinary hand");
        ((IDictionary)sessionType.GetField("Hands", F)!.GetValue(session)!)[Player] = args[1];
        ((IDictionary)sessionType.GetField("Ready", F)!.GetValue(session)!)[Player] = false;
        od.GetField("_current", F)!.SetValue(null, session);
        object queue = Invoke(typeof(ActionQueueSet).GetMethod("GetQueue", F)!, RunManager.Instance.ActionQueueSet, Player.NetId)!;
        var paused = queue.GetType().GetField("isPaused", F)!;
        var cancelling = queue.GetType().GetField("isCancellingPlayerDrivenCombatActions", F)!;
        paused.SetValue(queue, true); cancelling.SetValue(queue, true);
        Player.PlayerCombatState!.Energy = 3;
        PersonalCall("Request", Player, 1);
        var ticket = PersonalIdentity(PersonalTicketObject());
        Check(PersonalCoordinator.GetField("_current", F)!.GetValue(null) == null,
            "a personal request made during true OD queues behind OD without nesting its hand suspension");
        Invoke(od.GetMethod("Restore", F)!, null, session);
        Check(od.GetField("_current", F)!.GetValue(null) == null && original.Pile == Player.PlayerCombatState!.Hand && Player.PlayerCombatState!.Energy == 7,
            "real OD Restore returns its original ordinary cards and energy before personal admission");
        Check(!(bool)paused.GetValue(queue)! && (bool)cancelling.GetValue(queue)!,
            "OD first resumes ordinary queues, then personal admission blocks only new owner manual input");
        Power<MagicSwordModePower>(Hero, 1); Set(Hero, "_block", 123);
        await PersonalNetExecute(true, Player, ticket.Id, ticket.Ordinal);
        Check(Player.PlayerCombatState!.Energy == 3 && Hero.Block == 123,
            "a personal turn entered after OD receives two plus magic-sword energy and preserves existing block");
        peer.PlayerCombatState!.Energy = 2; CombatManager.Instance.SetReadyToEndTurn(peer, true);
        PlayerCmd.EndTurn(Player, true);
        await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
        Check(!(bool)paused.GetValue(queue)! && !(bool)cancelling.GetValue(queue)!,
            "OD-to-personal handoff restores an unlocked ordinary owner queue instead of reviving paused OD flags");
        Check(original.Pile == Player.PlayerCombatState!.Hand && Player.PlayerCombatState!.Energy == 7 && Hero.Block == 123 &&
            peer.PlayerCombatState!.Energy == 2 && CombatManager.Instance.IsPlayerReadyToEndTurn(peer),
            "OD-to-personal finish preserves original owner cards/energy/block and newer teammate energy/ready progress");
    }
}
