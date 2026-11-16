using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneCCollection : TeamCollectionRelic
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int UsesSpent { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BattleReady { get; private set; }
    internal bool IsResolving { get; private set; }
    public override string PackedIconPath => "res://images/ui/thirty_one_c/yamawaki_servant_icon.png";
    protected override int CountCards(Player player) => ThirtyOneCKeywordRules.GetPermanentDistinctCount(player);
    internal int MaximumUses => DisplayAmount >= 4 ? 2 : DisplayAmount >= 2 ? 1 : 0;
    internal int RemainingUses => BattleReady ? System.Math.Max(0, MaximumUses - UsesSpent) : 0;

    public override Task BeforeCombatStart()
    { UsesSpent = 0; BattleReady = true; IsResolving = false; InvokeDisplayAmountChanged(); return Task.CompletedTask; }
    public override Task AfterCombatEnd(CombatRoom room)
    { BattleReady = false; IsResolving = false; return Task.CompletedTask; }
    internal bool TryReserve(int expectedUsed)
    {
        if (IsResolving || UsesSpent != expectedUsed || RemainingUses <= 0) return false;
        UsesSpent++; IsResolving = true; InvokeDisplayAmountChanged(); Flash(); return true;
    }
    internal void FinishResolution() => IsResolving = false;
    internal void AddDescriptionVariables(LocString text)
    {
        text.Add("Servants", IsMutable ? DisplayAmount : 0);
        text.Add("RemainingUses", IsMutable ? RemainingUses : 0);
        text.Add("MaximumUses", IsMutable ? MaximumUses : 0);
        text.Add("TotalPercent", (IsMutable ? DisplayAmount : 0) * 100);
    }
    protected override void AfterCloned() { base.AfterCloned(); IsResolving = false; }
}
