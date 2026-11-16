using System;
using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

// Fate dice changes and the magic sword switch run as managed actions, so every peer
// applies the same command to the same saved state in the same order. The switch is a
// play-phase combat action like a card play; the dice commands may run at any time.
internal static class HbrFateDiceNetActions
{
    private const string ModuleId = "HBRGOSakiMod";
    private static bool _initialized;

    private static readonly RitsuLibManagedNetActionDescriptor<HbrFateDiceCommand> CommandDescriptor = new(
        ModuleId, "hbr_fate_dice_command",
        static command => JsonSerializer.SerializeToUtf8Bytes(command),
        static payload => JsonSerializer.Deserialize<HbrFateDiceCommand>(payload)
            ?? throw new ArgumentException("Invalid fate dice command."),
        ExecuteCommand, GameActionType.Any);

    private static readonly RitsuLibManagedNetActionDescriptor<MagicSwordSwitch> SwitchDescriptor = new(
        ModuleId, "hbr_magic_sword_switch",
        static request => JsonSerializer.SerializeToUtf8Bytes(request),
        static payload => JsonSerializer.Deserialize<MagicSwordSwitch>(payload)
            ?? throw new ArgumentException("Invalid magic sword switch."),
        ExecuteSwitch, GameActionType.CombatPlayPhaseOnly);

    internal sealed record MagicSwordSwitch
    {
        public bool Open { get; init; }
    }

    public static void Initialize()
    {
        if (_initialized) return;
        RitsuLibManagedNetActions.Register(CommandDescriptor);
        RitsuLibManagedNetActions.Register(SwitchDescriptor);
        _initialized = true;
    }

    private static bool IsLocalRequester(ElectronicSoldierManual manual) =>
        manual?.UsesFateDice == true && LocalContext.IsMe(manual.Owner) &&
        RunManager.Instance?.NetService?.Type != NetGameType.Replay;

    internal static bool Request(ElectronicSoldierManual manual, HbrFateDiceCommand command)
    {
        if (command == null || !IsLocalRequester(manual) || manual.IsFateDicePending) return false;
        Initialize();
        manual.IsFateDicePending = true;
        bool sent;
        try { sent = RitsuLibManagedNetActions.Request(RunManager.Instance, CommandDescriptor, command, manual.Owner.NetId); }
        catch
        {
            manual.IsFateDicePending = false;
            throw;
        }
        if (!sent) TaskHelper.RunSafely(Apply(manual, command));
        return true;
    }

    // Reward buttons skip the pending lock: each claim carries its own receipt.
    internal static bool RequestClaim(ElectronicSoldierManual manual, string receipt, string fragment)
    {
        if (manual == null || string.IsNullOrEmpty(receipt) || string.IsNullOrEmpty(fragment)) return false;
        if (!LocalContext.IsMe(manual.Owner) || RunManager.Instance?.NetService?.Type == NetGameType.Replay) return true;
        Initialize();
        var command = new HbrFateDiceCommand { Op = HbrFateDiceOp.Claim, Receipt = receipt, Fragment = fragment };
        if (!RitsuLibManagedNetActions.Request(RunManager.Instance, CommandDescriptor, command, manual.Owner.NetId))
            TaskHelper.RunSafely(manual.ApplyFateDiceCommand(command));
        return true;
    }

    internal static bool RequestSwitch(ElectronicSoldierManual manual, bool open)
    {
        if (!IsLocalRequester(manual)) return false;
        if (open ? !HbrMagicSwordToggle.CanOpen(manual.Owner) : !HbrMagicSwordToggle.CanClose(manual.Owner)) return false;
        Initialize();
        return RitsuLibManagedNetActions.Request(RunManager.Instance, SwitchDescriptor,
            new MagicSwordSwitch { Open = open }, manual.Owner.NetId);
    }

    private static Task ExecuteCommand(RitsuLibManagedNetActionContext<HbrFateDiceCommand> context)
    {
        ElectronicSoldierManual manual = context.Player.GetRelic<ElectronicSoldierManual>();
        return manual == null ? Task.CompletedTask : Apply(manual, context.Message);
    }

    private static async Task Apply(ElectronicSoldierManual manual, HbrFateDiceCommand command)
    {
        try { await manual.ApplyFateDiceCommand(command); }
        finally
        {
            if (command.Op != HbrFateDiceOp.Claim) manual.IsFateDicePending = false;
        }
    }

    // The request rechecked the switch locally; the action rechecks it again on every
    // peer, since a queued card or another player may have changed combat meanwhile.
    private static Task ExecuteSwitch(RitsuLibManagedNetActionContext<MagicSwordSwitch> context) =>
        context.Message.Open
            ? HbrMagicSwordToggle.Open(context.PlayerChoiceContext, context.Player)
            : HbrMagicSwordToggle.Close(context.PlayerChoiceContext, context.Player);
}
