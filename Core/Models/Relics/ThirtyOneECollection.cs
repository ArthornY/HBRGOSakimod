using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneECollection : TeamCollectionRelic
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ArrivedMembers { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool AllArrivedRewardGiven { get; private set; }
    public override string PackedIconPath => "res://images/relics/thirty_one_e.png";
    protected override int CountCards(Player player) => ThirtyOneEKeywordRules.GetPermanentDistinctCount(player);
    public override Task BeforeCombatStart()
    {
        ArrivedMembers = 0; AllArrivedRewardGiven = false;
        ThirtyOneEBondHelper.ResetBattle(Owner);
        return Task.CompletedTask;
    }
    internal void RecordArrival(ThirtyOneEMember member)
    {
        int old = ArrivedMembers;
        ArrivedMembers |= 1 << (int)member;
        if (old != ArrivedMembers) InvokeDisplayAmountChanged();
    }
    internal bool TryReserveAllArrived()
    {
        if (AllArrivedRewardGiven || BitOperations.PopCount((uint)ArrivedMembers & 63u) < 6) return false;
        AllArrivedRewardGiven = true;
        return true;
    }
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play) =>
        ThirtyOneEBondHelper.AfterCardPlayed(context, this, play);
    internal void AddDescriptionVariables(LocString text)
    {
        int kinds = IsMutable && Owner != null ? DisplayAmount : 0;
        IEnumerable<string> Names(bool arrived) => ThirtyOneEKeywordRules.MemberNames.Where((_, index) =>
            ((ArrivedMembers & (1 << index)) != 0) == arrived);
        string arrived = IsMutable ? string.Join("、", Names(true)) : "";
        string missing = IsMutable ? string.Join("、", Names(false)) : string.Join("、", ThirtyOneEKeywordRules.MemberNames);
        text.Add("Kinds", kinds);
        text.Add("Arrived", string.IsNullOrEmpty(arrived) ? "尚无" : arrived);
        text.Add("Missing", string.IsNullOrEmpty(missing) ? "全部到齐" : missing);
        text.Add("ExtraTurnState", IsMutable && AllArrivedRewardGiven ? "本场已获得" : "本场尚未获得");
    }
}
