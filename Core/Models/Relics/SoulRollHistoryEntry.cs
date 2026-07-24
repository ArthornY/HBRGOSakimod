namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class SoulRollHistoryEntry
{
    public int Roll { get; }

    public int? DiscardedRoll { get; }

    public string Source { get; }

    public SoulRollHistoryEntry(int roll, int? discardedRoll, string source)
    {
        Roll = roll;
        DiscardedRoll = discardedRoll;
        Source = source;
    }
}
