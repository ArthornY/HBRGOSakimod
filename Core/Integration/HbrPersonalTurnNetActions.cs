using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace HBRGOSakiMod.Core.Integration;

internal static class HbrPersonalTurnNetActions
{
    internal sealed record Message(long RequestId, int Ordinal, string RoomKey);
    private static bool _registered;
    private static readonly RitsuLibManagedNetActionDescriptor<Message> BeginDescriptor = Create("hbr_personal_turn_begin", true);
    private static readonly RitsuLibManagedNetActionDescriptor<Message> FinishDescriptor = Create("hbr_personal_turn_finish", false);

    private static RitsuLibManagedNetActionDescriptor<Message> Create(string key, bool begin) => new(
        "HBRGOSakiMod", key, message => JsonSerializer.SerializeToUtf8Bytes(message),
        bytes => JsonSerializer.Deserialize<Message>(bytes), context => Execute(context, begin), GameActionType.Any);

    internal static void Initialize()
    {
        if (_registered) return;
        _registered = true;
        RitsuLibManagedNetActions.Register(BeginDescriptor);
        RitsuLibManagedNetActions.Register(FinishDescriptor);
    }

    internal static bool RequestBegin(PersonalTurnTicket<Player> ticket, string roomKey) => Send(ticket, roomKey, BeginDescriptor);
    internal static bool RequestFinish(PersonalTurnTicket<Player> ticket, string roomKey) => Send(ticket, roomKey, FinishDescriptor);

    private static bool Send(PersonalTurnTicket<Player> ticket, string roomKey,
        RitsuLibManagedNetActionDescriptor<Message> descriptor)
    {
        Initialize();
        return RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor,
            new Message(ticket.RequestId, ticket.Ordinal, roomKey), ticket.Owner.NetId);
    }

    private static Task Execute(RitsuLibManagedNetActionContext<Message> context, bool begin) => begin
        ? PersonalImmediateTurnCoordinator.Begin(context.PlayerChoiceContext, context.Player,
            context.Message.RequestId, context.Message.Ordinal, context.Message.RoomKey)
        : PersonalImmediateTurnCoordinator.Finish(context.PlayerChoiceContext, context.Player,
            context.Message.RequestId, context.Message.Ordinal, context.Message.RoomKey);
}
