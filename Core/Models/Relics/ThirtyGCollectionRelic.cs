using System.Threading.Tasks;
using System;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyGCollectionRelic : TeamCollectionRelic
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int San { get; private set; } = 100;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int Popularity { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int NextRoundStart { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public string ProposalData { get; private set; } = "";
    // Registers the shared native network property name used by card identity.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public string ThirtyGPhysicalId { get; private set; } = "";
    public override string PackedIconPath => "res://images/relics/thirty_g.png";
    public override int DisplayAmount => IsMutable ? San : 100;
    protected override int CountCards(Player player) => ThirtyGKeywordRules.GetPermanentDistinctCount(player);
    internal int Kinds => IsMutable ? CountCards(Owner) : 0;
    internal void SetProposalData(string value) { AssertMutable(); ProposalData = value ?? ""; }
    internal void SetSan(int value) { AssertMutable(); San = System.Math.Clamp(value, 0, 100); InvokeDisplayAmountChanged(); }
    internal void GainPopularity() { AssertMutable(); Popularity = (int)System.Math.Min(int.MaxValue, (long)Popularity + 1); InvokeDisplayAmountChanged(); }
    internal void FinishVote() { AssertMutable(); NextRoundStart = System.Math.Min(9, NextRoundStart + 1); Popularity = NextRoundStart; InvokeDisplayAmountChanged(); }
    public override int ModifyCardPlayCount(CardModel card, Creature target, int count) =>
        card?.Owner == Owner ? count + ThirtyGProposalState.ReplayBonus(card) + ChieLitaState.ReplayBonus(card) : count;
    public override Task BeforeCardPlayed(CardPlay play) => play?.Card?.Owner == Owner ? ChieLitaState.BeforeCardPlayed(play) : Task.CompletedTask;
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    { if (play?.Card?.Owner == Owner) ChieLitaState.AfterCardPlayed(play); return Task.CompletedTask; }
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card?.Owner != Owner || card.EnergyCost.CostsX || !ThirtyGProposalState.HasNextPlayDiscount(card) || originalCost <= 0m) return false;
        modifiedCost = Math.Max(0m, originalCost - 1m); return true;
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        player == Owner ? ThirtyGBondHelper.TurnStart(context, player) : Task.CompletedTask;
    public override Task AfterCombatEnd(CombatRoom room) { ThirtyGBondHelper.EndBattle(Owner); return Task.CompletedTask; }
    public override Task AfterRoomEntered(AbstractRoom room) { ThirtyGBondHelper.RoomEntered(Owner, room); return Task.CompletedTask; }
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel clonedBy)
    {
        if (card?.Owner == Owner && (card.Pile?.Type == PileType.Deck || oldPileType == PileType.Deck))
        { ThirtyGProposalState.EnsureIdentities(Owner); ChieLitaState.OnPermanentDeckChanged(Owner); }
        await Task.CompletedTask;
    }
    internal void AddDescriptionVariables(LocString text)
    {
        text.Add("Kinds", Kinds); text.Add("San", IsMutable ? San : 100);
        text.Add("Popularity", IsMutable ? Popularity : 0); text.Add("NextRoundStart", IsMutable ? NextRoundStart : 0);
    }
}
