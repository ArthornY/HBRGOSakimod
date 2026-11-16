using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Rewards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

// 31O's kept swordheart and fate dice (design v3, sections 2 and 4). The record is
// one JSON string per player, changed only by synchronized commands and by hooks
// that every peer runs in the same order.
public partial class ElectronicSoldierManual
{
    private string _hbrFateDiceData = string.Empty;
    private HbrFateDiceState _hbrFateDice;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrFateDiceData
    {
        get => _hbrFateDiceData;
        protected set
        {
            AssertMutable();
            _hbrFateDiceData = value ?? string.Empty;
            _hbrFateDice = null;
        }
    }

    internal bool IsFateDicePending { get; set; }

    internal bool UsesFateDice => IsMutable && Owner?.Character is HbrgoSaki31O;

    // A run from before the fate dice reads as two empty slots plus the bosses
    // already beaten (one per finished act).
    internal HbrFateDiceState FateDice => HbrFateDiceState.Effective(
        _hbrFateDice ??= HbrFateDiceState.Parse(HbrFateDiceData),
        Owner?.RunState?.CurrentActIndex ?? 0);

    internal bool HasFateFragment(string id) => UsesFateDice && FateDice.IsEquipped(id);

    internal int FateDiceRawPenalty => UsesFateDice ? FateDice.RawPenalty : 0;

    // As with equipment, the loadout is fixed while a battle runs.
    internal static bool IsFateDiceLoadoutUnlocked => CombatManager.Instance?.IsInProgress != true;

    private ulong FateDiceSeed =>
        (Owner?.PlayerRng is { } rng ? RngCompatibility.GetPlayerSeed(rng) : 0UL) * 0x9E3779B97F4A7C15UL + 0x46415445UL;

    // The merchant room the owner stands in, or null.
    internal string CurrentFateShopKey =>
        Owner?.RunState?.CurrentRoom is MerchantRoom room
            ? MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room)
            : null;

    internal IReadOnlyList<HbrFateShopOffer> CurrentFateShop =>
        UsesFateDice && CurrentFateShopKey is { Length: > 0 } key
            ? HbrFateDiceOffers.Shop(FateDice, key, HbrFateDiceRandom.ForKey(FateDiceSeed, "shop:" + key))
            : Array.Empty<HbrFateShopOffer>();

    private void StoreFateDice(HbrFateDiceState state)
    {
        AssertMutable();
        HbrFateDiceData = state.Serialize();
        _hbrFateDice = state;
    }

    // Room hooks run on every peer. This extends the parasol's native merchant
    // haul to our separate shelf without sending a second purchase request.
    internal void StealFateShop(AbstractRoom room)
    {
        if (!UsesFateDice || room is not MerchantRoom ||
            Owner.GetRelic<MegaCrit.Sts2.Core.Models.Relics.LordsParasol>() == null) return;
        HbrFateDiceState state = FateDice;
        HbrFateDiceState taken = HbrFateDiceCommands.StealShop(state, CurrentFateShopKey, CurrentFateShop);
        if (ReferenceEquals(taken, state)) return;
        StoreFateDice(taken);
        Flash();
    }

    internal async Task<bool> ApplyFateDiceCommand(HbrFateDiceCommand command)
    {
        if (!UsesFateDice || command == null) return false;
        string shopKey = CurrentFateShopKey;
        HbrFateDiceOutcome outcome = HbrFateDiceCommands.Evaluate(FateDice, command, IsFateDiceLoadoutUnlocked,
            Owner.Gold, shopKey, CurrentFateShop);
        if (!outcome.Succeeded) return false;
        StoreFateDice(outcome.State);
        if (outcome.GoldCost > 0) await PlayerCmd.LoseGold(outcome.GoldCost, Owner, GoldLossType.Spent);
        Flash();
        return true;
    }

    // ---- swordheart kept between combats ---------------------------------------

    // Stored at the end of every combat, cut to the kept maximum: raises that last
    // only this combat (剑心通明) are lost (「战斗结束时超过 7 层的部分作废」).
    internal void KeepSwordheartAfterCombat()
    {
        if (!UsesFateDice || Owner.Creature == null) return;
        int amount = Owner.Creature.GetPower<SwordheartPower>()?.Amount ?? 0;
        HbrFateDiceState state = FateDice;
        HbrFateDiceState kept = state.WithSwordheart(amount, SwordheartPower.GetKeptMaximum(Owner.Creature));
        if (!ReferenceEquals(kept, state)) StoreFateDice(kept);
    }

    internal async Task RestoreKeptSwordheart()
    {
        if (!UsesFateDice || Owner.Creature == null) return;
        int kept = FateDice.Swordheart;
        if (kept <= 0 || Owner.Creature.HasPower<SwordheartPower>()) return;
        await PowerCmd.Apply<SwordheartPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, kept, Owner.Creature, null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        EndMariaRepentanceBattle();
        KeepSwordheartAfterCombat();
        // Each boss beaten opens one more slot (「每打过一个首领 +1，最多 5 个」).
        if (UsesFateDice && room?.RoomType == RoomType.Boss) StoreFateDice(FateDice.WithBossVictory());
        return Task.CompletedTask;
    }

    // ---- cracks ------------------------------------------------------------------

    // Settled after a normal finisher, or when combat ends with the mode open.
    internal void AddFateDiceCrack()
    {
        if (!UsesFateDice) return;
        StoreFateDice(FateDice.WithCrack());
        Flash();
    }

    internal bool CanRepairFateDice => UsesFateDice && FateDice.Cracks > 0;

    internal bool RepairFateDiceCrack()
    {
        if (!CanRepairFateDice) return false;
        StoreFateDice(FateDice.Repaired());
        Flash();
        return true;
    }

    // ---- fragment rewards ----------------------------------------------------------

    // Elites offer three common or uncommon fragments, bosses three rare ones; taking
    // one discards the others, and the set may be skipped. Rewards are rebuilt when a
    // saved reward screen is restored, so only a claimed set stops a second roll; the
    // stream is keyed by the room, so the rebuilt set is the same one.
    private bool AddFateFragmentRewards(List<Reward> rewards, CombatRoom room)
    {
        if (!UsesFateDice || room == null || room.RoomType is not (RoomType.Elite or RoomType.Boss) ||
            rewards.Any(IsFateFragmentReward))
            return false;
        string key = MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room);
        if (string.IsNullOrEmpty(key)) return false;
        string receipt = "fate:" + key;
        HbrFateDiceState state = FateDice;
        if (state.HasClaimed(receipt)) return false;
        HbrFateOfferKind kind = room.RoomType == RoomType.Boss ? HbrFateOfferKind.Boss : HbrFateOfferKind.Elite;
        string[] offers = HbrFateDiceOffers.Roll(state, kind, HbrFateDiceRandom.ForKey(FateDiceSeed, receipt));
        HbrFateDiceState rolled = state.WithReceipt(receipt);
        if (!ReferenceEquals(rolled, state)) StoreFateDice(rolled);
        if (offers.Length == 0) return false;
        List<Reward> choices = offers
            .Select(fragment => (Reward)new HbrFateFragmentReward(Owner, this, receipt, fragment))
            .ToList();
        rewards.Add(choices.Count == 1 ? choices[0] : new LinkedRewardSet(choices, Owner));
        return true;
    }

    private static bool IsFateFragmentReward(Reward reward) =>
        reward is HbrFateFragmentReward ||
        reward is LinkedRewardSet linked && linked.Rewards.Any(inner => inner is HbrFateFragmentReward);
}
