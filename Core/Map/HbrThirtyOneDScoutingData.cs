using System;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Map;

internal sealed record HbrScoutedRelic
{
    public ulong PlayerId { get; init; }
    public string RelicId { get; init; } = "";
    public RelicRarity Rarity { get; init; }
    public bool Reserved { get; init; }
    public bool IsBonus { get; init; }
}

internal sealed record HbrScoutedPoint
{
    public int ActIndex { get; init; }
    public int Column { get; init; }
    public int Row { get; init; }
    public MapPointType OriginalType { get; init; }
    public RoomType RoomType { get; init; }
    public bool IsMemory { get; init; }
    public string EventId { get; init; } = "";
    public HbrScoutedRelic[] Relics { get; init; } = Array.Empty<HbrScoutedRelic>();
    public bool TreasureOpened { get; init; }
    public bool Released { get; init; }
    public bool Invalidated { get; init; }
}

internal sealed record HbrScoutedAct
{
    public int ActIndex { get; init; }
    public ulong[] Viewers { get; init; } = Array.Empty<ulong>();
    public HbrScoutedPoint[] Points { get; init; } = Array.Empty<HbrScoutedPoint>();
}

internal sealed record HbrScoutingState
{
    public int Schema { get; init; } = 1;
    public int Revision { get; init; }
    public HbrScoutedAct[] Acts { get; init; } = Array.Empty<HbrScoutedAct>();
}
