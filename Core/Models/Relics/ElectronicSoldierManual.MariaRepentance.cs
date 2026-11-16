using System.Globalization;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    // HBR eligibility already requires this persistent run relic. Keep the
    // party receipt here even when every visible Maria ability is dispelled.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MariaLifesaveUsedBattleKey { get; protected set; } = string.Empty;

    private string MariaBattleKey(ICombatState combat) =>
        Owner.RunState.TotalFloor.ToString(CultureInfo.InvariantCulture) + ":" + combat.Encounter?.Id.Entry;

    internal bool HasUsedMariaLifesave(ICombatState combat) => combat != null &&
        !string.IsNullOrEmpty(MariaLifesaveUsedBattleKey) && MariaLifesaveUsedBattleKey == MariaBattleKey(combat);

    internal void MarkMariaLifesaveUsed(ICombatState combat)
    {
        if (combat == null) return;
        AssertMutable();
        MariaLifesaveUsedBattleKey = MariaBattleKey(combat);
    }

    internal void EndMariaRepentanceBattle()
    {
        AssertMutable();
        MariaLifesaveUsedBattleKey = string.Empty;
        MariaRepentanceCombatHelper.EndBattle(Owner?.Creature?.CombatState);
    }
}
