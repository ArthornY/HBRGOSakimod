using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

// The OD gauge activates OD through a managed action, not a console command: the run upload
// excludes runs changed through the console (RunRecordConsolePatch), and pressing the gauge
// must not count as that. The gauge used to send "hbrgosakimod __activate_overclock <level>"
// as a ConsoleCmdGameAction (user, 2026-11-09: 「bug修一下」). This keeps how that was queued:
//  - its type followed CombatManager.IsInProgress at the request: CombatPlayPhaseOnly in combat
//    (deferred to the play phase, cancelled when combat ends), Any outside combat. A managed
//    action key has a single type, hence two keys;
//  - it was the sender's action, and so is this one. What it opens changed: the party shares OD
//    in co-op, so the request names the player whose Overclock the gauge shows and every peer
//    opens that one, whoever pressed (user, same day: 「谁按都能开」; HbrOverclockActivation).
internal static class HbrOverclockNetActions
{
    private const string ModuleId = "HBRGOSakiMod";
    private static bool _initialized;

    private static readonly RitsuLibManagedNetActionDescriptor<HbrOverclockActivation> CombatDescriptor =
        Descriptor("hbr_overclock_activate_combat", GameActionType.CombatPlayPhaseOnly);

    private static readonly RitsuLibManagedNetActionDescriptor<HbrOverclockActivation> AnyDescriptor =
        Descriptor("hbr_overclock_activate_any", GameActionType.Any);

    public static void Initialize()
    {
        if (_initialized) return;
        RitsuLibManagedNetActions.Register(CombatDescriptor);
        RitsuLibManagedNetActions.Register(AnyDescriptor);
        _initialized = true;
    }

    // The gauge checked CanActivateLevel on the relic it shows when the level was chosen.
    internal static bool Request(Overclock relic, int level)
    {
        if (relic?.Owner == null) return false;
        Initialize();
        RitsuLibManagedNetActionDescriptor<HbrOverclockActivation> descriptor =
            CombatManager.Instance.IsInProgress ? CombatDescriptor : AnyDescriptor;
        var request = new HbrOverclockActivation { Level = level, OwnerNetId = relic.Owner.NetId };
        if (RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, request)) return true;
        Log.Warn($"[HBRGOSakiMod] OD level {level} could not be sent ({RunManager.Instance?.NetService?.Type}).");
        return false;
    }

    private static RitsuLibManagedNetActionDescriptor<HbrOverclockActivation> Descriptor(string key, GameActionType type) => new(
        ModuleId, key, HbrOverclockActivation.Serialize, HbrOverclockActivation.Deserialize, Execute, type);

    // Runs on every peer, for the gauge's player rather than the sender. TryActivate rechecks
    // the combat state and the party's act allowance (not the UI-only input lock): a queued card
    // or another player may have changed combat meanwhile.
    private static Task Execute(RitsuLibManagedNetActionContext<HbrOverclockActivation> context)
    {
        Player sender = context.Player;
        IEnumerable<Player> players = sender?.RunState?.Players ?? [];
        Player owner = HbrOverclockActivation.Target(players, sender, context.Message.OwnerNetId,
            static player => player.NetId,
            static (player, other) => player.Creature?.Side == other.Creature?.Side,
            static player => player.GetRelic<Overclock>() != null);
        owner?.GetRelic<Overclock>().TryActivate(context.Message.Level);
        return Task.CompletedTask;
    }
}
