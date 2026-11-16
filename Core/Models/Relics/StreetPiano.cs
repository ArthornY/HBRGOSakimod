using System.Globalization;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class StreetPiano : HotaruClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    [SavedProperty]
    public int SelectedKind { get; private set; } = -1;
    [SavedProperty]
    public string SelectedCombatId { get; private set; } = "";
    [SavedProperty]
    public int OwnTurns { get; private set; }
    [SavedProperty]
    public bool FollowupPending { get; private set; }
    private Creature _selectedCannon;
    internal void ResetBattle() { SelectedKind = -1; SelectedCombatId = ""; OwnTurns = 0; FollowupPending = false; _selectedCannon = null; }
    internal void Select(Creature cannon, HotaruCannonKind kind)
    {
        _selectedCannon = cannon; SelectedKind = (int)kind;
        SelectedCombatId = cannon.CombatId?.ToString(CultureInfo.InvariantCulture) ?? "";
        FollowupPending = true;
    }
    internal Creature AdvanceTurn(Millsage millsage)
    {
        if (OwnTurns < 2) OwnTurns++;
        if (OwnTurns != 2 || !FollowupPending) return null;
        FollowupPending = false;
        // A loaded battle reconstructs creature objects. Once selected in this
        // live battle, require the exact entity so replacement cannot revive it.
        Creature cannon = _selectedCannon;
        if (cannon == null && SelectedKind >= 0 && !string.IsNullOrEmpty(SelectedCombatId))
        {
            Creature loaded = millsage.GetCannon((HotaruCannonKind)SelectedKind);
            if (loaded?.CombatId?.ToString(CultureInfo.InvariantCulture) == SelectedCombatId) cannon = loaded;
        }
        return Millsage.IsOwnedCannon(cannon, Owner) ? cannon : null;
    }
    protected override void AfterCloned() { base.AfterCloned(); _selectedCannon = null; }
}
