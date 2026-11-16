using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

// One upgrade ledger per physical card. All seven choices can be acquired once.
public abstract class ThirtyOneBGardenCard : CardModel
{
    private int? _prepared;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; protected set; } = Array.Empty<int>();
    protected abstract int FlashEffectId { get; }
    public override int MaxUpgradeLevel => 7;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => HbrMemoryGardenPortraits.For(this);
    public override string BetaPortraitPath => PortraitPath;
    protected ThirtyOneBGardenCard(int cost, CardType type, TargetType target) : base(cost, type, CardRarity.Rare, target) { }
    internal bool HasUpgradeEffect(int id) => UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    internal IReadOnlyList<int> GetAvailableUpgradeEffects() => Enumerable.Range(0, 7).Where(id => !HasUpgradeEffect(id)).ToArray();
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _prepared = id; return true;
    }
    internal void CancelPreparedUpgradeEffect() => _prepared = null;
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel).Where(id => id != FlashEffectId)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetCardEffectDescription(this, id.ToString()))
            .Where(effect => !string.IsNullOrEmpty(effect)).ToArray();
        text.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }
    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= 7) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool Eligible(int id) => id is >= 0 and < 7 && !applied.Contains(id);
        int id = index < UpgradeEffectOrder.Length && Eligible(UpgradeEffectOrder[index]) ? UpgradeEffectOrder[index]
            : _prepared is int prepared && Eligible(prepared) ? prepared : Enumerable.Range(0, 7).First(Eligible);
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = id;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(id).ToArray();
        _prepared = null;
        if (id == FlashEffectId) EnergyCost.UpgradeBy(-1);
    }
    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>()).Where(id => id is >= 0 and < 7).Distinct().Take(7).ToArray();
        _prepared = null;
    }
    protected override void AfterCloned() { base.AfterCloned(); UpgradeEffectOrder = UpgradeEffectOrder.ToArray(); _prepared = null; }
    protected override void AfterDowngraded() { base.AfterDowngraded(); UpgradeEffectOrder = Array.Empty<int>(); _prepared = null; }
}
