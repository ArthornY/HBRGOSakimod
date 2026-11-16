using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class SmilingPanKeychain : HotaruClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    [SavedProperty]
    public string LastSideTurn { get; private set; } = "";
    private ConditionalWeakTable<object, object> _extraTurns = new();
    internal void ResetBattle() { LastSideTurn = ""; _extraTurns = new(); }
    internal bool ClaimBreak(string sideTurn, object extraTurn)
    {
        if (extraTurn != null)
        {
            if (_extraTurns.TryGetValue(extraTurn, out _)) return false;
            _extraTurns.Add(extraTurn, new object());
            return true;
        }
        if (LastSideTurn == sideTurn) return false;
        LastSideTurn = sideTurn;
        return true;
    }
    protected override void AfterCloned() { base.AfterCloned(); _extraTurns = new(); }
}
