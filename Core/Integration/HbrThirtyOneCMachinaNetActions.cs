using System;
using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

internal static class HbrThirtyOneCMachinaNetActions
{
    internal sealed record ActivationRequest
    {
        public int ExpectedUsed { get; init; }
    }
    private static bool _initialized;
    private static readonly RitsuLibManagedNetActionDescriptor<ActivationRequest> Descriptor = new(
        "HBRGOSakiMod", "hbr_thirty_one_c_machina",
        static request => JsonSerializer.SerializeToUtf8Bytes(request),
        static payload => JsonSerializer.Deserialize<ActivationRequest>(payload)
            ?? throw new ArgumentException("Invalid31C activation request."),
        Execute, GameActionType.CombatPlayPhaseOnly);

    public static void Initialize()
    {
        if (_initialized) return;
        RitsuLibManagedNetActions.Register(Descriptor); _initialized = true;
    }
    internal static bool Request(Player player)
    {
        if (!ThirtyOneCMachinaHelper.CanActivate(player) || !LocalContext.IsMe(player) ||
            RunManager.Instance?.NetService?.Type == NetGameType.Replay) return false;
        Initialize();
        return RitsuLibManagedNetActions.Request(RunManager.Instance, Descriptor,
            new ActivationRequest { ExpectedUsed = player.GetRelic<ThirtyOneCCollection>().UsesSpent }, player.NetId);
    }
    private static async Task Execute(RitsuLibManagedNetActionContext<ActivationRequest> context)
    {
        // The authoritative sender is the owner. The payload cannot select a
        // teammate's relic, and a repeated queued receipt spends no second use.
        if (context.Message == null || context.Message.ExpectedUsed < 0) return;
        await ThirtyOneCMachinaHelper.TryActivate(context.PlayerChoiceContext, context.Player, context.Message.ExpectedUsed);
    }
}
