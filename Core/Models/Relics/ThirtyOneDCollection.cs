using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneDCollection : TeamCollectionRelic
{
    private int _lastRosterCount = -1;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ScoutingData { get; private set; } = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IntelRewardGiven { get; private set; }
    public override string PackedIconPath => "res://images/relics/thirty_one_d.png";
    protected override int CountCards(Player player) => ThirtyOneDKeywordRules.GetPermanentDistinctCount(player);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<HbrIntelPower>(), HoverTipFactory.FromPower<HbrMoralePower>() };
    internal override void RefreshCounter()
    {
        base.RefreshCounter();
        int count = DisplayAmount;
        if (count == _lastRosterCount) return;
        _lastRosterCount = count;
        Owner?.Creature?.GetPower<HbrIntelPower>()?.RefreshRoster();
    }

    internal void SetScoutingData(string data) { AssertMutable(); ScoutingData = data ?? ""; }
    internal bool TryReserveIntelReward()
    {
        AssertMutable();
        if (IntelRewardGiven) return false;
        IntelRewardGiven = true;
        return true;
    }
    public override Task BeforeCombatStart()
    {
        IntelRewardGiven = false;
        ThirtyOneDBondHelper.ResetBattle(Owner);
        return ThirtyOneDBondHelper.ClearIntel(Owner);
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        player == Owner ? ThirtyOneDBondHelper.TurnStart(context, this) : Task.CompletedTask;
    internal void AddDescriptionVariables(LocString text)
    {
        int count = IsMutable && Owner != null ? DisplayAmount : 0;
        text.Add("Kinds", count);
        text.Add("IntelCap", count >= 2 ? count : 0);
        text.Add("CurrentIntel", IsMutable ? Owner?.Creature?.GetPower<HbrIntelPower>()?.DisplayAmount ?? 0 : 0);
        text.Add("NextTier", count >= 6 ? "全部档位已生效" : count >= 4 ? "6种：情报满6份时全队士气+4级，你抽2张" : count >= 2 ? "4种：提前侦察本幕地图" : "2种：每回合收集情报并获得独立伤害加成");
    }
}
