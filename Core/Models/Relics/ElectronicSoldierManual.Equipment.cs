using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Rewards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using PerfectedStrike = MegaCrit.Sts2.Core.Models.Cards.PerfectedStrike;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    private string _hbrEquipmentData = string.Empty;
    private HbrEquipmentState _hbrEquipment;
    private HbrEquipmentBonus? _hbrEquipmentBonus;
    private HbrEquipmentEffectTotals? _hbrEquipmentEffects;

    // One immutable JSON string per player, like the battle progress record.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrEquipmentData
    {
        get => _hbrEquipmentData;
        protected set
        {
            AssertMutable();
            _hbrEquipmentData = value ?? string.Empty;
            _hbrEquipment = null;
            _hbrEquipmentBonus = null;
            _hbrEquipmentEffects = null;
        }
    }

    internal bool IsEquipmentPending { get; set; }
    private bool _isApplyingEquipmentTransaction;

    internal HbrEquipmentState Equipment => _hbrEquipment ??= HbrEquipmentState.Parse(HbrEquipmentData);

    internal int EquipmentTier => HbrEquipmentRules.TierForAct(Owner?.RunState?.CurrentActIndex ?? 0);

    // As in the original game, the loadout is fixed while a battle runs.
    internal static bool IsEquipmentLoadoutUnlocked => CombatManager.Instance?.IsInProgress != true;

    // Stat and DP reads happen every frame, so the summed bonus is cached.
    internal HbrEquipmentBonus GetEquipmentBonus() => !IsHbrNumericsEnabled ? default :
        _hbrEquipmentBonus ??= HbrEquipmentRules.Totals(Equipment);

    internal int GetEquipmentMaxDp() =>
        (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0m, GetEquipmentBonus().Dp)));

    // Pets and phantoms already read their owner's stats, so they share the
    // owner's critical-rate gear in the same way.
    internal static decimal GetEquipmentCriticalRate(Creature creature)
    {
        Player owner = creature?.Player ?? creature?.PetOwner;
        return owner?.GetRelic<ElectronicSoldierManual>()?.GetEquipmentBonus().CriticalRate ?? 0m;
    }

    internal HbrEquipmentEffectTotals GetEquipmentEffects() => !IsHbrNumericsEnabled ? default :
        _hbrEquipmentEffects ??= HbrEquipmentRules.EffectTotals(Equipment);

    // Effects follow the owner and their cannons. Phantoms keep out of the
    // summoner's relic modifiers in the damage pipeline, and so out of these.
    private static ElectronicSoldierManual EffectGearOf(Creature dealer)
    {
        if (dealer == null) return null;
        Player owner = dealer.Player ?? (dealer.HasPower<PhantomPower>() ? null : dealer.PetOwner);
        return owner?.GetRelic<ElectronicSoldierManual>();
    }

    // Element rings: flat roll points for the card type they favour.
    internal static int GetEquipmentRollBonus(Player player, CardType? type)
    {
        HbrEquipmentEffect? effect = type switch
        {
            CardType.Attack => HbrEquipmentEffect.AttackRoll,
            CardType.Skill => HbrEquipmentEffect.SkillRoll,
            CardType.Power => HbrEquipmentEffect.PowerRoll,
            _ => null
        };
        if (effect == null) return 0;
        decimal bonus = player?.GetRelic<ElectronicSoldierManual>()?.GetEquipmentEffects()[effect.Value] ?? 0m;
        return (int)Math.Min(int.MaxValue, decimal.Floor(bonus));
    }

    // Blast earrings and offensive ancient chains, as a percentage added to the
    // destruction gain of one hit.
    internal static decimal GetEquipmentDestructionBonus(Creature dealer, CardModel card)
    {
        if (EffectGearOf(dealer)?.GetEquipmentEffects() is not { } effects) return 0m;
        return effects[HbrEquipmentEffect.DestructionFlat] + HbrEquipmentRules.HitScaledPercent(
            HbrEquipmentEffect.DestructionGain, effects[HbrEquipmentEffect.DestructionGain],
            HbrDestructionGainHelper.GetSkillHits(dealer, card));
    }

    // Drive earrings: the factor on OD gauge gained from this dealer's damage.
    internal static decimal GetEquipmentOverdriveMultiplier(Creature dealer, CardModel card)
    {
        if (EffectGearOf(dealer)?.GetEquipmentEffects() is not { } effects) return 1m;
        return 1m + HbrEquipmentRules.HitScaledPercent(HbrEquipmentEffect.OverdriveGain,
            effects[HbrEquipmentEffect.OverdriveGain], HbrDestructionGainHelper.GetSkillHits(dealer, card)) / 100m;
    }

    // Outgoing gear joins the attack bucket like the original attack buffs.
    // Incoming cuts are a separate factor, like the original defense up.
    private decimal GetEquipmentDamageMultiplier(Creature target, ValueProp props, Creature dealer, CardModel card)
    {
        if (!IsHbrNumericsEnabled || Owner?.Creature == null || target == null || dealer == null ||
            target.Side == dealer.Side || !props.IsPoweredAttack())
            return 1m;
        HbrEquipmentEffectTotals effects = GetEquipmentEffects();
        if (target == Owner.Creature)
        {
            decimal cut = effects[HbrEquipmentEffect.DamageTaken] +
                (IsBossCombat(target) ? effects[HbrEquipmentEffect.BossDamageTaken] : 0m);
            return cut > 0m ? Math.Max(0m, 1m - cut / 100m) : 1m;
        }
        if (EffectGearOf(dealer) != this) return 1m;
        HbrEquipmentEffect side = HasDp(target) ? HbrEquipmentEffect.DpDamage : HbrEquipmentEffect.HpDamage;
        decimal bonus = effects[HbrEquipmentEffect.SkillDamage] + HbrEquipmentRules.HitScaledPercent(side, effects[side],
            HbrDestructionGainHelper.GetSkillHits(dealer, card));
        return bonus > 0m ? HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + bonus / 100m, this) : 1m;
    }

    private static bool HasDp(Creature target) =>
        target.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m ||
        target.Player?.GetRelic<DeflectorProtection>()?.Dp > 0;

    private static bool IsBossCombat(Creature creature) =>
        (creature.CombatState?.Encounter?.RoomType ?? creature.CombatState?.RunState.CurrentRoom?.RoomType) == RoomType.Boss;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner?.Creature is not { IsAlive: true, CombatState: not null }) return;
        decimal energy = GetEquipmentEffects()[HbrEquipmentEffect.EnergyPerTurn];
        if (energy <= 0m) return;
        Flash();
        await PlayerCmd.GainEnergy(energy, Owner);
    }

    // ★6 bracelets: every basic Strike in this combat becomes Perfected Strike
    // with the same upgrades. The deck itself keeps its cards.
    private async Task ConvertStrikesForEquipment()
    {
        if (!GetEquipmentEffects().Has(HbrEquipmentEffect.PerfectStrike) ||
            Owner?.Creature?.CombatState is not { } combat || Owner.PlayerCombatState == null)
            return;
        CardModel[] strikes = Owner.PlayerCombatState.AllCards
            .Where(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike) &&
                card.IsTransformable && card is not PerfectedStrike)
            .ToArray();
        if (strikes.Length == 0) return;
        Flash();
        var transformations = new List<CardTransformation>();
        foreach (CardModel strike in strikes)
        {
            PerfectedStrike replacement = combat.CreateCard<PerfectedStrike>(Owner);
            while (replacement.CurrentUpgradeLevel < strike.CurrentUpgradeLevel && replacement.IsUpgradable)
                CardCmd.Upgrade(replacement, CardPreviewStyle.None);
            transformations.Add(new CardTransformation(strike, replacement));
        }
        await CardCmd.Transform(transformations, null, CardPreviewStyle.None);
    }

    // Every peer derives the same stream from the owner's run seed. Its width
    // differs by runtime, so it is read through the compatibility getter.
    private ulong EquipmentSeed => Equipment.Seed != 0 ? Equipment.Seed :
        (Owner?.PlayerRng is { } rng ? RngCompatibility.GetPlayerSeed(rng) : 0UL) *
        0x9E3779B97F4A7C15UL + 0x48425247UL;

    internal async Task<bool> ApplyEquipmentCommand(HbrEquipmentCommand command)
    {
        if (!IsMutable || !IsHbrNumericsEnabled || Owner == null || _isApplyingEquipmentTransaction) return false;
        _isApplyingEquipmentTransaction = true;
        try
        {
            HbrEquipmentState state = Equipment;
            if (state.Seed == 0) state = state with { Seed = EquipmentSeed };
            HbrEquipmentOutcome outcome = HbrEquipmentCommands.Evaluate(state, command, EquipmentTier,
                IsEquipmentLoadoutUnlocked, Owner.Gold, HbrSatsukiShopPolicy.Get(Owner), Owner.RunState.CurrentActIndex);
            if (!outcome.Succeeded) return false;
            int previousDp = GetEquipmentMaxDp(), previousGold = Owner.Gold;
            string previousData = _hbrEquipmentData;
            HbrEquipmentState previousState = _hbrEquipment;
            HbrEquipmentBonus? previousBonus = _hbrEquipmentBonus;
            HbrEquipmentEffectTotals? previousEffects = _hbrEquipmentEffects;
            try
            {
                // The revision, RNG, first-use receipts and candidates become
                // visible only after native gold hooks finish successfully.
                if (outcome.GoldCost > 0) await PlayerCmd.LoseGold(outcome.GoldCost, Owner, GoldLossType.Spent);
                if (outcome.GoldGain > 0) await PlayerCmd.GainGold(outcome.GoldGain, Owner);
                StoreEquipment(outcome.State);
            }
            catch
            {
                _hbrEquipmentData = previousData;
                _hbrEquipment = previousState;
                _hbrEquipmentBonus = previousBonus;
                _hbrEquipmentEffects = previousEffects;
                // Native Gold assigns its value before emitting GoldChanged.
                // Compensate this owner directly, never through reward hooks.
                try { Owner.Gold = previousGold; } catch { }
                throw;
            }
            if (previousDp != GetEquipmentMaxDp()) Owner.GetRelic<DeflectorProtection>()?.SyncAfterEquipmentDpChanged();
            Flash();
            CombatPreviewRefreshHelper.RefreshCombatState(Owner.Creature, nameof(ApplyEquipmentCommand));
            return true;
        }
        finally { _isApplyingEquipmentTransaction = false; }
    }

    private void StoreEquipment(HbrEquipmentState state)
    {
        AssertMutable();
        HbrEquipmentData = state.Serialize();
        _hbrEquipment = state;
    }

    // Called by the manual's damage hook for every model in the combat.
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer,
        CardModel cardSource) => GetEquipmentDamageMultiplier(target, props, dealer, cardSource);

    // Rolled once per room from a room-keyed stream; the receipt keeps a
    // claimed reward from coming back when the reward screen is restored.
    private bool AddEquipmentDrops(List<Reward> rewards, CombatRoom room)
    {
        if (!IsMutable || !IsHbrNumericsEnabled || rewards.Any(reward => reward is HbrEquipmentReward)) return false;
        string key = MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room);
        if (string.IsNullOrEmpty(key)) return false;
        string receipt = "drop:" + key;
        HbrEquipmentState state = Equipment;
        if (state.HasReceipt(receipt)) return false;
        HbrDropRoom dropRoom = room.RoomType switch
        {
            RoomType.Boss => HbrDropRoom.Boss,
            RoomType.Elite => HbrDropRoom.Elite,
            _ => HbrDropRoom.Normal
        };
        HbrEquipmentDrop drop = HbrEquipmentGenerator.RollCombatDrop(dropRoom, EquipmentTier,
            room.Encounter is GoldOmenWeak, HbrEquipmentRandom.ForKey(EquipmentSeed, key));
        StoreEquipment(state.WithReceipt(receipt));
        foreach (HbrEquipmentClaim claim in HbrEquipmentClaim.FromDrop(key, drop))
            rewards.Add(new HbrEquipmentReward(Owner, this, claim));
        return !drop.IsEmpty;
    }
}
