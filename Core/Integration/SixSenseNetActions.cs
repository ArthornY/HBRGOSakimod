using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

internal static class SixSenseNetActions
{
    private const string ModuleId = "HBRGOSakiMod";
    private const string IncreaseSenseActionKey = "increase_six_sense";

    private static readonly RitsuLibManagedNetActionDescriptor<SixSenseAttribute>
        IncreaseSenseDescriptor = new(
            ModuleId,
            IncreaseSenseActionKey,
            SerializeAttribute,
            DeserializeAttribute,
            ExecuteIncreaseSense,
            GameActionType.NonCombat);

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        RitsuLibManagedNetActions.Register(IncreaseSenseDescriptor);
        _initialized = true;
    }

    public static bool RequestIncrease(Player player, SixSenseAttribute attribute)
    {
        Initialize();
        return RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            IncreaseSenseDescriptor,
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
}
