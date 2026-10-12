using System;
using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

internal static class SixSenseNetActions
{
    private const string ModuleId = "HBRGOSakiMod";
    private const string IncreaseSenseActionKey = "increase_six_sense";
    private const string IncreaseSenseBeyondMaximumActionKey =
        "increase_six_sense_beyond_maximum";

    private static readonly RitsuLibManagedNetActionDescriptor<SixSenseAttribute>
        IncreaseSenseDescriptor = new(
            ModuleId,
            IncreaseSenseActionKey,
            SerializeAttribute,
            DeserializeAttribute,
        ExecuteIncreaseSense,
        GameActionType.NonCombat);

    private static readonly RitsuLibManagedNetActionDescriptor<SixSenseAttribute>
        IncreaseSenseBeyondMaximumDescriptor = new(
            ModuleId,
            IncreaseSenseBeyondMaximumActionKey,
            SerializeAttribute,
            DeserializeAttribute,
            ExecuteIncreaseSenseBeyondMaximum,
            GameActionType.NonCombat);

    private static bool _initialized;

    internal sealed record Allocation(int Revision, int[] Increases);

    private static readonly RitsuLibManagedNetActionDescriptor<Allocation> AllocateDescriptor = new(
        ModuleId, "allocate_six_sense_points",
        static message => JsonSerializer.SerializeToUtf8Bytes(message),
        static payload => JsonSerializer.Deserialize<Allocation>(payload)
            ?? throw new ArgumentException("Invalid six-sense allocation."),
        // The manual can be opened during combat. NonCombat would queue the
        // allocation until combat ends even though Request already accepted it.
        ExecuteAllocation, GameActionType.Any);

    private static readonly RitsuLibManagedNetActionDescriptor<SixSenseAttribute> LegacyPointDescriptor = new(
        ModuleId, "claim_legacy_six_sense_point", SerializeAttribute, DeserializeAttribute,
        static context => { context.Player.GetRelic<ElectronicSoldierManual>()?.GrantSixSensePoints(1); return Task.CompletedTask; },
        GameActionType.NonCombat);

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        RitsuLibManagedNetActions.Register(IncreaseSenseDescriptor);
        RitsuLibManagedNetActions.Register(
            IncreaseSenseBeyondMaximumDescriptor);
        RitsuLibManagedNetActions.Register(AllocateDescriptor);
        RitsuLibManagedNetActions.Register(LegacyPointDescriptor);
        _initialized = true;
    }

    internal static bool CanAllocate(ElectronicSoldierManual manual) =>
        manual?.IsMutable == true && manual.Owner != null && LocalContext.IsMe(manual.Owner) &&
        RunManager.Instance?.NetService?.Type != NetGameType.Replay;

    internal static bool RequestAllocation(ElectronicSoldierManual manual, int[] increases)
    {
        if (!CanAllocate(manual) || manual.IsAllocationPending ||
            !manual.CanAllocateSixSensePoints(increases, manual.SixSenseAllocationRevision)) return false;
        Initialize();
        var message = new Allocation(manual.SixSenseAllocationRevision, (int[])increases.Clone());
        manual.IsAllocationPending = true;
        try
        {
            if (RitsuLibManagedNetActions.Request(RunManager.Instance, AllocateDescriptor, message, manual.Owner.NetId)) return true;
            manual.IsAllocationPending = false;
            return manual.ApplySixSenseAllocation(message.Increases, message.Revision);
        }
        catch
        {
            manual.IsAllocationPending = false;
            throw;
        }
        finally
        {
            if (manual.SixSenseAllocationRevision != message.Revision) manual.IsAllocationPending = false;
        }
    }

    private static Task ExecuteAllocation(RitsuLibManagedNetActionContext<Allocation> context)
    {
        ElectronicSoldierManual manual = context.Player.GetRelic<ElectronicSoldierManual>();
        if (manual != null)
        {
            try { manual.ApplySixSenseAllocation(context.Message.Increases, context.Message.Revision); }
            finally { manual.IsAllocationPending = false; }
        }
        return Task.CompletedTask;
    }

    internal static bool ClaimLegacyPoint(ElectronicSoldierManual manual)
    {
        if (manual == null) return false;
        if (!LocalContext.IsMe(manual.Owner) || RunManager.Instance?.NetService?.Type == NetGameType.Replay) return true;
        Initialize();
        if (!RitsuLibManagedNetActions.Request(RunManager.Instance, LegacyPointDescriptor,
            SixSenseAttribute.LeadershipCharm, manual.Owner.NetId)) manual.GrantSixSensePoints(1);
        return true;
    }

    public static bool RequestIncrease(
        Player player,
        SixSenseAttribute attribute,
        bool canExceedMaximum = false)
    {
        Initialize();
        return RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            canExceedMaximum
                ? IncreaseSenseBeyondMaximumDescriptor
                : IncreaseSenseDescriptor,
            attribute,
            player.NetId);
    }

    private static byte[] SerializeAttribute(SixSenseAttribute attribute)
    {
        return new[] { checked((byte)attribute) };
    }

    private static SixSenseAttribute DeserializeAttribute(
        ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 1 ||
            !Enum.IsDefined(typeof(SixSenseAttribute), (int)payload[0]))
        {
            throw new ArgumentException("Invalid six-sense action payload.");
        }

        return (SixSenseAttribute)payload[0];
    }

    private static async Task ExecuteIncreaseSense(
        RitsuLibManagedNetActionContext<SixSenseAttribute> context)
    {
        ElectronicSoldierManual manual =
            context.Player.GetRelic<ElectronicSoldierManual>();
        if (manual == null)
        {
            return;
        }

        await manual.ApplySenseIncrease(context.Message);
    }

    private static async Task ExecuteIncreaseSenseBeyondMaximum(
        RitsuLibManagedNetActionContext<SixSenseAttribute> context)
    {
        ElectronicSoldierManual manual =
            context.Player.GetRelic<ElectronicSoldierManual>();
        if (manual == null)
        {
            return;
        }

        await manual.ApplySenseIncrease(
            context.Message,
            canExceedMaximum: true);
    }
}
