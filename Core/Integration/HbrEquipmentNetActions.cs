using System;
using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

// Every loadout, shop and reward change runs as one managed action, so all
// peers apply the same command to the same saved state in the same order.
internal static class HbrEquipmentNetActions
{
    private const string ModuleId = "HBRGOSakiMod";
    private static bool _initialized;

    private static readonly RitsuLibManagedNetActionDescriptor<HbrEquipmentCommand> CommandDescriptor = new(
        ModuleId, "hbr_equipment_command",
        static command => JsonSerializer.SerializeToUtf8Bytes(command),
        static payload => JsonSerializer.Deserialize<HbrEquipmentCommand>(payload)
            ?? throw new ArgumentException("Invalid equipment command."),
        // The manual and its shop stay usable during combat.
        Execute, GameActionType.Any);

    public static void Initialize()
    {
        if (_initialized) return;
        RitsuLibManagedNetActions.Register(CommandDescriptor);
        _initialized = true;
    }

    internal static bool CanRequest(ElectronicSoldierManual manual) =>
        manual?.IsMutable == true && manual.Owner != null && manual.IsHbrNumericsEnabled &&
        LocalContext.IsMe(manual.Owner) && RunManager.Instance?.NetService?.Type != NetGameType.Replay;

    internal static bool Request(ElectronicSoldierManual manual, HbrEquipmentCommand command)
    {
        if (command == null || !CanRequest(manual) || manual.IsEquipmentPending) return false;
        Initialize();
        manual.IsEquipmentPending = true;
        bool sent;
        try { sent = RitsuLibManagedNetActions.Request(RunManager.Instance, CommandDescriptor, command, manual.Owner.NetId); }
        catch
        {
            manual.IsEquipmentPending = false;
            throw;
        }
        if (!sent) TaskHelper.RunSafely(Apply(manual, command));
        return true;
    }

    // Reward buttons may be pressed in quick succession; each claim carries
    // its own receipt instead of the revision, so it skips the pending lock.
    internal static bool RequestClaim(ElectronicSoldierManual manual, HbrEquipmentClaim claim)
    {
        if (manual == null || claim == null) return false;
        if (!LocalContext.IsMe(manual.Owner) || RunManager.Instance?.NetService?.Type == NetGameType.Replay) return true;
        Initialize();
        var command = new HbrEquipmentCommand { Op = HbrEquipmentOp.Claim, Claim = claim };
        if (!RitsuLibManagedNetActions.Request(RunManager.Instance, CommandDescriptor, command, manual.Owner.NetId))
            TaskHelper.RunSafely(manual.ApplyEquipmentCommand(command));
        return true;
    }

    private static Task Execute(RitsuLibManagedNetActionContext<HbrEquipmentCommand> context)
    {
        ElectronicSoldierManual manual = context.Player.GetRelic<ElectronicSoldierManual>();
        return manual == null ? Task.CompletedTask : Apply(manual, context.Message);
    }

    private static async Task Apply(ElectronicSoldierManual manual, HbrEquipmentCommand command)
    {
        try { await manual.ApplyEquipmentCommand(command); }
        finally
        {
            if (command.Op != HbrEquipmentOp.Claim) manual.IsEquipmentPending = false;
        }
    }
}
