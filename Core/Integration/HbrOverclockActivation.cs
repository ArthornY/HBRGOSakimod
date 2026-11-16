using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HBRGOSakiMod.Core.Integration;

// What the OD gauge asks every peer to do (HbrOverclockNetActions): open Overclock at Level for
// the player whose Overclock the gauge shows. In co-op the party shares that OD, so whoever
// presses opens it (user, 2026-11-09: 「联机的od是共用的」「谁按都能开」). Plain .NET, so the
// offline run-upload checks link this file and test the payload and the choice of player.
internal sealed record HbrOverclockActivation
{
    public int Level { get; init; }

    public ulong OwnerNetId { get; init; }

    internal static byte[] Serialize(HbrOverclockActivation request) =>
        JsonSerializer.SerializeToUtf8Bytes(request);

    internal static HbrOverclockActivation Deserialize(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<HbrOverclockActivation>(payload) ??
        throw new ArgumentException("Invalid OD activation.");

    // The player to open OD for: the gauge's player, when they are in the run, on the sender's
    // side and hold Overclock. Otherwise null, and nobody's OD opens. Every peer has the same
    // players in the same state, so every peer makes the same choice.
    internal static TPlayer Target<TPlayer>(IEnumerable<TPlayer> players, TPlayer sender, ulong ownerNetId,
        Func<TPlayer, ulong> netId, Func<TPlayer, TPlayer, bool> sameSide, Func<TPlayer, bool> holdsOverclock)
        where TPlayer : class
    {
        if (sender == null) return null;
        foreach (TPlayer player in players)
        {
            if (netId(player) != ownerNetId) continue;
            return sameSide(player, sender) && holdsOverclock(player) ? player : null;
        }
        return null;
    }
}
