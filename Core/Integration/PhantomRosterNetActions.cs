using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

internal static class PhantomRosterNetActions
{
    private const string ModuleId = "HBRGOSakiMod";
    private const string SetRosterActionKey = "set_phantom_roster";
    private const string UpgradeRosterActionKey = "upgrade_phantom_roster";
    private const string DevourRosterActionKey = "devour_phantom_roster";

    private static readonly RitsuLibManagedNetActionDescriptor<string>
        SetRosterDescriptor = new(
            ModuleId,
            SetRosterActionKey,
            static message => Encoding.UTF8.GetBytes(message),
            static payload => Encoding.UTF8.GetString(payload),
        ExecuteSetRoster,
        GameActionType.NonCombat);

    private static readonly RitsuLibManagedNetActionDescriptor<string>
        UpgradeRosterDescriptor = new(
            ModuleId,
            UpgradeRosterActionKey,
            static message => Encoding.UTF8.GetBytes(message),
            static payload => Encoding.UTF8.GetString(payload),
            ExecuteUpgradeRoster,
            GameActionType.NonCombat);

    private static readonly RitsuLibManagedNetActionDescriptor<string>
        DevourRosterDescriptor = new(
            ModuleId,
            DevourRosterActionKey,
            static message => Encoding.UTF8.GetBytes(message),
            static payload => Encoding.UTF8.GetString(payload),
            ExecuteDevourRoster,
            GameActionType.NonCombat);

    private static bool _initialized;

    internal static bool RequestSetRoster(
        Player player,
        IReadOnlyList<ModelId> monsterIds)
    {
        Initialize();
        string message = string.Join(
            '\n',
            monsterIds.Select(id => id.ToString()));
        return RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            SetRosterDescriptor,
            message,
            player.NetId);
    }

    internal static bool RequestUpgradeRoster(
        Player player,
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        IReadOnlyCollection<PhantomFusionEffectType> retainedEffectTypes,
        bool consumeFullUpgradeInheritance)
    {
        Initialize();
        string message = targetInstanceId + "|" +
            string.Join(',', materialInstanceIds.OrderBy(id => id)) + "|" +
            string.Join(',', retainedEffectTypes
                .Where(type => type != PhantomFusionEffectType.None)
                .Distinct()
                .OrderBy(type => (int)type)
                .Select(type => ((int)type).ToString())) + "|" +
            (consumeFullUpgradeInheritance ? "1" : "0");
        return RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            UpgradeRosterDescriptor,
            message,
            player.NetId);
    }

    internal static bool RequestDevourRoster(
        Player player,
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds)
    {
        Initialize();
        string message = targetInstanceId + "|" + string.Join(
            ',',
            materialInstanceIds.OrderBy(id => id));
        return RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            DevourRosterDescriptor,
            message,
            player.NetId);
    }

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        RitsuLibManagedNetActions.Register(SetRosterDescriptor);
        RitsuLibManagedNetActions.Register(UpgradeRosterDescriptor);
        RitsuLibManagedNetActions.Register(DevourRosterDescriptor);
        _initialized = true;
    }

    private static Task ExecuteSetRoster(
        RitsuLibManagedNetActionContext<string> context)
    {
        ModelId[] monsterIds = context.Message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ModelId.Deserialize)
            .ToArray();
        context.Player.GetRelic<MomentOfReign>()?
            .SetSummonableMonsterIds(monsterIds);
        return Task.CompletedTask;
    }

    private static Task ExecuteUpgradeRoster(
        RitsuLibManagedNetActionContext<string> context)
    {
        string[] parts = context.Message.Split('|');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out int targetInstanceId))
        {
            int[] materialInstanceIds = ParsePositiveIds(parts[1]);
            PhantomFusionEffectType[] retainedEffectTypes =
                parts.Length >= 3
                    ? ParseEffectTypes(parts[2])
                    : null;
            bool consumeFullUpgradeInheritance =
                parts.Length >= 4 && parts[3] == "1";
            context.Player.GetRelic<MomentOfReign>()?
                .TryUpgradeRosterEntry(
                    targetInstanceId,
                    materialInstanceIds,
                    retainedEffectTypes,
                    consumeFullUpgradeInheritance);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteDevourRoster(
        RitsuLibManagedNetActionContext<string> context)
    {
        string[] parts = context.Message.Split('|', 2);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int targetInstanceId))
        {
            context.Player.GetRelic<MomentOfReign>()?
                .TryDevourRosterEntries(
                    targetInstanceId,
                    ParsePositiveIds(parts[1]));
        }

        return Task.CompletedTask;
    }

    private static int[] ParsePositiveIds(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => int.TryParse(item, out int id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

    private static PhantomFusionEffectType[] ParseEffectTypes(
        string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => int.TryParse(item, out int type) ? type : 0)
            .Where(type =>
                type > 0 &&
                Enum.IsDefined(typeof(PhantomFusionEffectType), type))
            .Select(type => (PhantomFusionEffectType)type)
            .Distinct()
            .ToArray();
}
