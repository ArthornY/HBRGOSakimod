using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interactions.RightClick;

namespace HBRGOSakiMod.Core.Models.Relics;

internal readonly record struct PhantomRosterEntry(
    int InstanceId,
    ModelId MonsterId,
    int UpgradeLevel,
    int Experience = 0,
    int ExperienceValue = 1,
    decimal InheritedLevelDamageBonusPercent = 0m,
    decimal InheritedLevelDamageReductionPercent = 0m,
    decimal InheritedLevelBonusMaxHp = 0m,
    string FusionEffectData = "",
    decimal InheritedLevelSummonerRollBonus = 0m)
{
    internal const int InitialExperienceRequirement = 10;
    internal const int ExperienceRequirementIncrease = 5;
    internal const decimal LevelDamageBonusPercent = 10m;
    internal const decimal LevelDamageReductionPercent = 5m;
    internal const decimal LevelBonusMaxHp = 5m;
    internal const decimal LevelSummonerRollBonus = 1m;
    internal const decimal MaximumDamageReductionPercent = 50m;
    internal const int MaximumStoredExperience = 1_000_000_000;

    internal int Level
    {
        get
        {
            GetExperienceProgress(out int level, out _, out _);
            return level;
        }
    }

    internal int ExperienceIntoLevel
    {
        get
        {
            GetExperienceProgress(out _, out int progress, out _);
            return progress;
        }
    }

    internal int ExperienceRequiredForNextLevel
    {
        get
        {
            GetExperienceProgress(out _, out _, out int requirement);
            return requirement;
        }
    }

    internal int LevelUps => Math.Max(0, Level - 1);

    internal decimal EarnedLevelDamageBonusPercent =>
        LevelUps * LevelDamageBonusPercent +
        Math.Max(0m, InheritedLevelDamageBonusPercent);

    internal decimal EarnedLevelDamageReductionPercent =>
        LevelUps * LevelDamageReductionPercent +
        Math.Max(0m, InheritedLevelDamageReductionPercent);

    internal decimal EarnedLevelBonusMaxHp =>
        LevelUps * LevelBonusMaxHp +
        Math.Max(0m, InheritedLevelBonusMaxHp);

    internal decimal SummonerRollBonus =>
        LevelUps * LevelSummonerRollBonus +
        Math.Max(0m, InheritedLevelSummonerRollBonus);

    internal int FusionSlotCount =>
        PhantomFusionEffectHelper.GetFusionSlotCount(ExperienceValue);

    internal decimal DamageBonusMultiplier =>
        PhantomFusionEffectHelper.GetDamageBonusMultiplier(ExperienceValue);

    internal decimal DamageBonusPercent =>
        UnscaledDamageBonusPercent * DamageBonusMultiplier;

    internal decimal UnscaledDamageBonusPercent =>
        PhantomFusionEffectHelper.GetBaseDamageBonusPercent(
            ExperienceValue) +
        EarnedLevelDamageBonusPercent +
        PhantomFusionEffectHelper.GetStaticDamageBonusPercent(
            FusionEffectData);

    internal decimal DamageReductionPercent =>
        Math.Clamp(
            EarnedLevelDamageReductionPercent +
            PhantomFusionEffectHelper.GetStaticDamageReductionPercent(FusionEffectData),
            0m,
            MaximumDamageReductionPercent);

    internal decimal BonusMaxHp =>
        EarnedLevelBonusMaxHp +
        PhantomFusionEffectHelper.GetBonusMaxHp(FusionEffectData);

    internal decimal ExtraMaxDp =>
        PhantomFusionEffectHelper.GetExtraMaxDp(FusionEffectData);

    // Roster/fusion metadata stays in original units across clones and saves.
    // Only readers that apply or display actual HP/DP resolve the fixed scale.
    internal decimal GetBonusMaxHp(Player owner) =>
        BigCombatNumbers.ClampStat(HbrResourceValueHelper.Fixed(owner?.Creature, BonusMaxHp));

    internal decimal GetExtraMaxDp(Player owner) =>
        BigCombatNumbers.ClampStat(HbrResourceValueHelper.Fixed(owner?.Creature, ExtraMaxDp));

    internal decimal GetBasePhantomHp(
        decimal originalHp,
        bool forceBossScaling = false,
        Player owner = null)
    {
        int divisor = ExperienceValue >= 4 || forceBossScaling
            ? 10
            : 2;
        return HbrBattleStatsHelper.GetPhantomBaseHp(owner,
            Math.Max(1m, decimal.Floor(originalHp / divisor)));
    }

    internal bool HasEarnedUpgradeEffects =>
        LevelUps > 0 ||
        InheritedLevelDamageBonusPercent > 0m ||
        InheritedLevelDamageReductionPercent > 0m ||
        InheritedLevelBonusMaxHp > 0m ||
        InheritedLevelSummonerRollBonus > 0m ||
        PhantomFusionEffectHelper.HasEffects(FusionEffectData);

    internal PhantomRosterEntry AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return this;
        }

        long total = (long)Math.Max(0, Experience) + amount;
        return this with
        {
            Experience = (int)Math.Min(
                MaximumStoredExperience,
                total)
        };
    }

    internal PhantomRosterEntry AddLevels(int amount)
    {
        PhantomRosterEntry result = this;
        for (int index = 0; index < Math.Max(0, amount); index++)
        {
            int previousExperience = result.Experience;
            result = result.AddExperience(
                result.ExperienceRequiredForNextLevel);
            if (result.Experience == previousExperience)
            {
                break;
            }
        }

        return result;
    }

    internal PhantomRosterEntry LoseLevel()
    {
        int currentLevel = Level;
        if (currentLevel <= 1)
        {
            return this;
        }

        int targetLevel = currentLevel - 1;
        int targetExperience = 0;
        int targetRequirement = InitialExperienceRequirement;
        for (int level = 1; level < targetLevel; level++)
        {
            targetExperience += targetRequirement;
            targetRequirement += ExperienceRequirementIncrease;
        }

        targetExperience += Math.Min(
            ExperienceIntoLevel,
            targetRequirement - 1);
        return this with { Experience = targetExperience };
    }

    private void GetExperienceProgress(
        out int level,
        out int progress,
        out int requirement)
    {
        int remaining = Math.Clamp(
            Experience,
            0,
            MaximumStoredExperience);
        level = 1;
        requirement = InitialExperienceRequirement;
        while (remaining >= requirement)
        {
            remaining -= requirement;
            level++;
            requirement += ExperienceRequirementIncrease;
        }

        progress = remaining;
    }
}

public partial class MomentOfReign : RelicModel, IModRightClickableRelic
{
    private const int PhantomReviveDelayTurns = 4;

    private string _summonableMonsterIds = string.Empty;
    private string _obtainedPhantomMonsterIds = string.Empty;
    private int _nextSummonableMonsterInstanceId = 1;
    private bool _hasSummonedThisCombat;
    private int _transferredDpThisCombat;
    private int _activeRosterEntryId;
    private int _phantomReviveTurnNumber;
    private string _pendingPhantomReviveMonsterId = string.Empty;
    private bool _hasUsedFullUpgradeInheritance;
    private int _pendingCombatExperience;
    private string _pendingRosterExperienceData = string.Empty;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected virtual int NewlyObtainedPhantomLevelBonus => 0;

    public override bool SpawnsPets => true;

    public override bool AddsPet => true;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("relics/restoration_moment.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    // Protected setters remain visible when the save system reflects on the
    // upgraded BecomeGodHbrgoSaki type. Private inherited setters look read-only
    // and the compatibility loader can consume their saved entries on the first read.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string SummonableMonsterIds
    {
        get => _summonableMonsterIds;
        protected set
        {
            AssertMutable();
            _summonableMonsterIds = value ?? string.Empty;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ObtainedPhantomMonsterIds
    {
        get => _obtainedPhantomMonsterIds;
        protected set
        {
            AssertMutable();
            _obtainedPhantomMonsterIds = value ?? string.Empty;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int NextSummonableMonsterInstanceId
    {
        get => _nextSummonableMonsterInstanceId;
        protected set
        {
            AssertMutable();
            _nextSummonableMonsterInstanceId = Math.Max(1, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasSummonedThisCombat
    {
        get => _hasSummonedThisCombat;
        protected set
        {
            AssertMutable();
            _hasSummonedThisCombat = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TransferredDpThisCombat
    {
        get => _transferredDpThisCombat;
        protected set
        {
            AssertMutable();
            _transferredDpThisCombat = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActiveRosterEntryId
    {
        get => _activeRosterEntryId;
        protected set
        {
            AssertMutable();
            _activeRosterEntryId = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PhantomReviveTurnNumber
    {
        get => _phantomReviveTurnNumber;
        protected set
        {
            AssertMutable();
            _phantomReviveTurnNumber = Math.Max(0, value);
        }
    }

    [SavedProperty]
    public string PendingPhantomReviveMonsterId
    {
        get => _pendingPhantomReviveMonsterId;
        protected set
        {
            AssertMutable();
            _pendingPhantomReviveMonsterId = value ?? string.Empty;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasUsedFullUpgradeInheritance
    {
        get => _hasUsedFullUpgradeInheritance;
        protected set
        {
            AssertMutable();
            _hasUsedFullUpgradeInheritance = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingCombatExperience
    {
        get => _pendingCombatExperience;
        protected set
        {
            AssertMutable();
            _pendingCombatExperience = Math.Clamp(
                value,
                0,
                PhantomRosterEntry.MaximumStoredExperience);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PendingRosterExperienceData
    {
        get => _pendingRosterExperienceData;
        protected set
        {
            AssertMutable();
            _pendingRosterExperienceData = value ?? string.Empty;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasUsedLingeringReviveThisCombat { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PhantomDamageTakenCount { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingCardRemovalRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public List<SerializableCard> PendingThievingHopperCardRewards { get; protected set; } = new();

    public override Task AfterObtained()
    {
        EnsureSummonableMonsterRoster();
        return Task.CompletedTask;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PhantomHealthData { get; protected set; } = string.Empty;

    private Dictionary<int, decimal> ReadPhantomHealth()
    {
        if (string.IsNullOrWhiteSpace(PhantomHealthData)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, decimal>>(PhantomHealthData) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    internal decimal? GetStoredPhantomHp(int instanceId) =>
        ReadPhantomHealth().TryGetValue(instanceId, out decimal hp) ? BigCombatNumbers.ClampStat(hp) : null;

    private void StorePhantomHp(Creature phantom)
    {
        if (!IsMutable || !PhantomHelper.IsOwnedMainPhantom(phantom, Owner)) return;
        StorePhantomBaseMaxHp(phantom);
        decimal permanentMaxHp = Math.Max(1m, LargeCreatureHealthHelper.GetMaxHp(phantom) -
            (phantom.GetPower<PhantomTemporaryLevelPower>()?.ExactAppliedBonusMaxHp ?? 0m));
        SetStoredPhantomHp(ActiveRosterEntryId, Math.Min(LargeCreatureHealthHelper.GetCurrentHp(phantom), permanentMaxHp));
    }

    private void SetStoredPhantomHp(int instanceId, decimal hp)
    {
        AssertMutable();
        Dictionary<int, decimal> health = ReadPhantomHealth();
        hp = BigCombatNumbers.ClampStat(hp);
        if (health.TryGetValue(instanceId, out decimal previous) && previous == hp) return;
        health[instanceId] = hp;
        PhantomHealthData = JsonSerializer.Serialize(health);
    }

    internal decimal GetPhantomSummonHp(int instanceId, decimal maxHp, bool isRevival) =>
        isRevival ? maxHp : Math.Clamp(GetStoredPhantomHp(instanceId) ?? maxHp, 0, maxHp);

    internal void InheritStateFrom(MomentOfReign source)
    {
        if (source == null)
        {
            return;
        }

        SummonableMonsterIds = source.SummonableMonsterIds;
        ObtainedPhantomMonsterIds = source.ObtainedPhantomMonsterIds;
        NextSummonableMonsterInstanceId =
            source.NextSummonableMonsterInstanceId;
        HasSummonedThisCombat = source.HasSummonedThisCombat;
        TransferredDpThisCombat = source.TransferredDpThisCombat;
        ActiveRosterEntryId = source.ActiveRosterEntryId;
        PhantomReviveTurnNumber = source.PhantomReviveTurnNumber;
        PendingPhantomReviveMonsterId =
            source.PendingPhantomReviveMonsterId;
        HasUsedFullUpgradeInheritance =
            source.HasUsedFullUpgradeInheritance;
        PendingCombatExperience = source.PendingCombatExperience;
        PendingRosterExperienceData = source.PendingRosterExperienceData;
        PhantomHealthData = source.PhantomHealthData;
        PhantomMaxHpAdjustment = source.PhantomMaxHpAdjustment;
        PhantomMaxHpAdjustmentData = source.PhantomMaxHpAdjustmentData;
        ExactPhantomLostMaxDp = source.ExactPhantomLostMaxDp;
        ActivePhantomMaxHpAdjustment = source.ActivePhantomMaxHpAdjustment;
        ActivePhantomMaxHpAdjustmentData = source.ActivePhantomMaxHpAdjustmentData;
        PhantomBaseMaxHpData = source.PhantomBaseMaxHpData;
        HasUsedLingeringReviveThisCombat = source.HasUsedLingeringReviveThisCombat;
        PhantomDamageTakenCount = source.PhantomDamageTakenCount;
        PendingCardRemovalRewards = source.PendingCardRemovalRewards;
        PendingThievingHopperCardRewards = PhantomThievingHopperHelper.CopyPendingRewards(
            source.PendingThievingHopperCardRewards);
    }

    public override decimal ModifyMerchantPrice(
        Player player,
        MerchantEntry entry,
        decimal originalPrice)
    {
        if (player != Owner || !LocalContext.IsMe(Owner))
        {
            return originalPrice;
        }

        decimal discount = GetRosterEntries()
            .Select(rosterEntry => PhantomFusionEffectHelper.GetEffect(
                rosterEntry.FusionEffectData,
                PhantomFusionEffectType.FakeMerchant,
                20m,
                15m))
            .DefaultIfEmpty(0m)
            .Max();
        return originalPrice *
            (1m - Math.Clamp(discount, 0m, 100m) / 100m);
    }

    public override async Task BeforeCombatStart()
    {
        HasUsedLingeringReviveThisCombat = false;
        PendingCardRemovalRewards = 0;
        PendingThievingHopperCardRewards.Clear();
        ResetPhantomDamageTakenCount();
        ResetPhantomTurnHistory();
        ResetPhantomCombatHistory();
        HasSummonedThisCombat = false;
        TransferredDpThisCombat = 0;
        ActiveRosterEntryId = 0;
        ClearPendingPhantomRevive();
        PendingCombatExperience = 0;
        PendingRosterExperienceData = string.Empty;
        if (Owner.Character is not HbrgoSaki30A)
        {
            return;
        }

        EnsureSummonableMonsterRoster();
        IReadOnlyList<PhantomRosterEntry> roster = GetRosterEntries();
        if (roster.Count == 0)
        {
            await Summon(new PhantomRosterEntry(
                0,
                ModelDb.Monster<SmallHopper>().Id,
                0).AddLevels(NewlyObtainedPhantomLevelBonus));
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState;
        List<PhantomChoiceHbrgoSaki> choices = roster
            .Select(entry =>
            {
                PhantomChoiceHbrgoSaki card =
                    combatState.CreateCard<PhantomChoiceHbrgoSaki>(Owner);
                card.SetRosterEntry(entry);
                return card;
            })
            .OrderByPhantomGrowth()
            .ToList();
        try
        {
            PhantomChoiceHbrgoSaki selected =
                (await CardSelectCmd.FromSimpleGrid(
                    new BlockingPlayerChoiceContext(),
                    choices,
                    Owner,
                    new CardSelectorPrefs(
                        L10NLookup(Id.Entry + ".selectionScreenPrompt"),
                        1)))
                .OfType<PhantomChoiceHbrgoSaki>()
                .FirstOrDefault();
            if (selected != null)
            {
                await Summon(selected.ToRosterEntry());
            }
        }
        finally
        {
            foreach (PhantomChoiceHbrgoSaki choice in choices)
            {
                choice.RemoveFromState();
            }
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        // Roll back temporary global phantom HP before saving the living phantom
        // and clearing its roster ID. The DP relic also calls this idempotently.
        if (Owner.GetRelic<DeflectorProtection>() is { } protection)
            await protection.FinishBeyondProtection();
        GrantPendingCardRemovalRewards(room);
        PhantomThievingHopperHelper.GrantPendingRewards(room, Owner, PendingThievingHopperCardRewards);
        ResetPhantomDamageTakenCount();
        ResetPhantomTurnHistory();
        ResetPhantomCombatHistory();
        Creature survivingPhantom = room?.CombatState?.Allies.FirstOrDefault(creature =>
            creature.IsAlive &&
            creature.PetOwner == Owner &&
            creature.HasPower<PhantomPower>() &&
            !creature.HasPower<MinionPower>());
        StorePhantomHp(survivingPhantom);
        int remainingTransferredDp = survivingPhantom
            ?.GetPower<HighDimensionalShellPower>() is { } shell
                ? (int)Math.Min(
                    TransferredDpThisCombat,
                    Math.Max(0m, shell.CurrentDp))
                : 0;
        if (remainingTransferredDp > 0)
        {
            Owner.GetRelic<DeflectorProtection>()?
                .RestoreDp(remainingTransferredDp);
        }

        PhantomRosterEntry? activeEntry = GetActiveRosterEntry();
        int bonusGold = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                activeEntry?.FusionEffectData,
                PhantomFusionEffectType.GoldHopper,
                5m,
                3m) +
            PhantomFusionEffectHelper.GetEffect(
                activeEntry?.FusionEffectData,
                PhantomFusionEffectType.GremlinMerc,
                3m,
                1m) +
            PhantomFusionEffectHelper.GetEffect(
                activeEntry?.FusionEffectData,
                PhantomFusionEffectType.FatGremlin,
                5m,
                3m));
        if (bonusGold > 0)
        {
            await PlayerCmd.GainGold(bonusGold, Owner);
        }

        int cardRewardCount = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetEffect(
                activeEntry?.FusionEffectData,
                PhantomFusionEffectType.ThievingHopper,
                1m,
                0m));
        int extraCardOptions = PhantomFusionEffectHelper.ToInt(
            PhantomFusionEffectHelper.GetSecondaryEffect(
                activeEntry?.FusionEffectData,
                PhantomFusionEffectType.ThievingHopper,
                0m,
                1m));
        int optionCount = HBRGOSakiMod.Core.Patches.HBRGOSakiCardRewardPoolHelper
            .GetExtraRewardOptionCount(extraCardOptions);
        if (cardRewardCount > 0)
        {
            room.AddExtraReward(
                Owner,
                new CardReward(
                    CardCreationOptions.ForRoom(Owner, room.RoomType),
                    optionCount,
                    Owner));
        }

        decimal permanentDamageBonus =
            PhantomFusionEffectHelper.GetSecondaryEffect(
                activeEntry?.FusionEffectData,
                PhantomFusionEffectType.GremlinMerc,
                3m,
                0m);
        CommitPendingRosterExperience();
        CommitPostCombatGrowth(permanentDamageBonus);

        HasSummonedThisCombat = false;
        TransferredDpThisCombat = 0;
        ActiveRosterEntryId = 0;
        ClearPendingPhantomRevive();
        PendingCombatExperience = 0;
        PendingRosterExperienceData = string.Empty;
    }

    internal void QueueCardRemovalReward()
    {
        AssertMutable();
        PendingCardRemovalRewards = (int)Math.Min(int.MaxValue,
            (long)Math.Max(0, PendingCardRemovalRewards) + 1);
    }

    internal void QueueThievingHopperCardReward(SerializableCard card)
    {
        AssertMutable();
        if (card != null)
            PendingThievingHopperCardRewards.Add(card);
    }

    internal void GrantPendingCardRemovalRewards(CombatRoom room)
    {
        AssertMutable();
        if (room == null) return;

        int count = PendingCardRemovalRewards;
        PendingCardRemovalRewards = 0;
        for (int index = 0; index < count; index++)
            room.AddExtraReward(Owner, new CardRemovalReward(Owner));
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature target,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (Owner.Character is HbrgoSaki30A &&
            !wasRemovalPrevented &&
            PhantomHelper.CountsAsFinalDefeat(target) &&
            target?.PetOwner == Owner &&
            target.GetPower<PhantomPower>() is
            {
                IsSummonedMinion: false
            })
        {
            decimal damageBonusBeforeDeath = LastPhantomDamageBonusPercent;
            LoseLevelFromActiveRosterEntry();
            SchedulePhantomRevive(target);
            if (Owner.Creature.IsAlive &&
                CombatManager.Instance.IsInProgress &&
                !CombatManager.Instance.IsOverOrEnding &&
                Owner.Creature.GetPower<LingeringLifePower>() is { } lingering)
            {
                bool reviveImmediately = TryUseLingeringRevive();
                await lingering.AfterPhantomDeath(choiceContext, damageBonusBeforeDeath);
                if (reviveImmediately && Owner.Creature.IsAlive &&
                    PhantomHelper.GetLivingPhantom(Owner) == null)
                    await RevivePendingPhantom(0.25m);
            }

            await DistantPrayerHbrgoSaki.ReturnToHandAfterPhantomDeath(Owner);
            if (GetPhantomReviveDelayTurns() == 0)
                await RevivePhantomNow();
        }

        if (Owner.Character is HbrgoSaki30A &&
            !wasRemovalPrevented &&
            PhantomHelper.CountsAsFinalDefeat(target) &&
            target?.Side == CombatSide.Enemy &&
            target.Monster is { ShouldShowInCompendium: true } monster)
        {
            int experienceValue = GetExperienceValueForCurrentRoom();
            QueueCombatExperience(experienceValue);
            if (!target.HasPower<MinionPower>())
            {
                AppendSummonableMonsterIds(
                    new[] { monster.Id },
                    experienceValue);
            }
        }

    }

    internal bool TryUseLingeringRevive()
    {
        AssertMutable();
        if (HasUsedLingeringReviveThisCombat)
            return false;

        // Claim before any draw/summon awaits; another copy cannot reset this flag.
        HasUsedLingeringReviveThisCombat = true;
        return true;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side && participants.Contains(Owner.Creature))
        {
            ResetPhantomTurnHistory();
        }

        if (PhantomReviveTurnNumber <= 0 ||
            Owner.Character is not HbrgoSaki30A ||
            side != Owner.Creature.Side ||
            !participants.Contains(Owner.Creature) ||
            !Owner.Creature.IsAlive ||
            Owner.PlayerCombatState == null)
        {
            return;
        }

        if (PhantomHelper.GetLivingPhantom(Owner) != null)
        {
            ClearPendingPhantomRevive();
            return;
        }

        if (Owner.PlayerCombatState.TurnNumber <
            PhantomReviveTurnNumber)
        {
            return;
        }

        await RevivePendingPhantom();
    }

    internal async Task AdvancePhantomRevive(int turns = 1)
    {
        if (turns <= 0 || !IsMutable || PhantomReviveTurnNumber <= 0 ||
            Owner.Character is not HbrgoSaki30A ||
            !Owner.Creature.IsAlive || Owner.PlayerCombatState == null ||
            PhantomHelper.GetLivingPhantom(Owner) != null ||
            !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        AssertMutable();
        if ((long)PhantomReviveTurnNumber - Owner.PlayerCombatState.TurnNumber > turns)
        {
            PhantomReviveTurnNumber -= turns;
        }
        else
        {
            // Do not write zero to the deadline: it means there is no pending revival.
            await RevivePendingPhantom();
        }
    }

    internal bool CanRevivePhantomNow =>
        IsMutable && PhantomReviveTurnNumber > 0 &&
        Owner?.Character is HbrgoSaki30A && Owner.Creature.IsAlive &&
        Owner.PlayerCombatState != null &&
        CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding &&
        PhantomHelper.GetLivingPhantom(Owner) == null &&
        CreatePendingPhantomReviveEntry() != null;

    internal int? GetPhantomReviveTurnsRemaining()
    {
        if (!IsMutable || PhantomReviveTurnNumber <= 0 ||
            Owner?.Character is not HbrgoSaki30A || Owner.Creature?.IsAlive != true ||
            Owner.PlayerCombatState == null || PhantomHelper.GetLivingPhantom(Owner) != null ||
            (ActiveRosterEntryId > 0 && GetActiveRosterEntry() == null))
        {
            return null;
        }

        // Read the saved deadline directly so loading a battle or shortening the
        // countdown updates the intent without changing when revival is triggered.
        long remaining = (long)PhantomReviveTurnNumber - Owner.PlayerCombatState.TurnNumber;
        return (int)Math.Clamp(remaining, 0L, int.MaxValue);
    }

    internal async Task RevivePhantomNow()
    {
        if (CanRevivePhantomNow)
            await RevivePendingPhantom();
    }

    private async Task RevivePendingPhantom(decimal healthFraction = 1m)
    {
        PhantomRosterEntry? pendingEntry =
            CreatePendingPhantomReviveEntry();
        ClearPendingPhantomRevive();
        if (pendingEntry is not { } reviveEntry ||
            !CombatManager.Instance.IsInProgress ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        HasSummonedThisCombat = false;
        await Summon(reviveEntry, isRevival: true, revivalHealthFraction: healthFraction);
        PhantomPower revivedPower = PhantomHelper.GetLivingPhantom(Owner)?
            .GetPower<PhantomPower>();
        if (revivedPower != null)
        {
            if (Owner.Creature.GetPower<LetMeBearItPower>() is { } burden)
            {
                await burden.AfterPhantomRevived(revivedPower.Owner);
            }
            await revivedPower.BeginPlayerTurn();
        }
    }

    private HashSet<Creature> _phantomAttackTargetsThisTurn = new();

    internal int PhantomDamageThisTurn { get; private set; }

    internal bool HasConsumedRosterThisTurn { get; private set; }

    internal decimal LastPhantomDamageBonusPercent { get; private set; }
    internal decimal LastPhantomDamageReductionPercent { get; private set; }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        StorePhantomHp(creature);
        return Task.CompletedTask;
    }

    public override Task BeforeDeath(Creature creature)
    {
        if (PhantomHelper.IsOwnedMainPhantom(creature, Owner))
        {
            StorePhantomHp(creature);
            AssertMutable();
            LastPhantomDamageBonusPercent = PhantomHelper.GetDamageBonusPercent(creature);
            LastPhantomDamageReductionPercent = PhantomHelper.GetDamageReductionPercent(creature);
        }
        return Task.CompletedTask;
    }

    private bool _hasUsedPhantomDpRecovery;

    internal bool TryUsePhantomDpRecovery()
    {
        if (_hasUsedPhantomDpRecovery) return false;
        AssertMutable();
        _hasUsedPhantomDpRecovery = true;
        return true;
    }

    private void ResetPhantomCombatHistory()
    {
        AssertMutable();
        LastPhantomDamageBonusPercent = 0m;
        LastPhantomDamageReductionPercent = 0m;
        _hasUsedPhantomDpRecovery = false;
    }

    internal bool WasAttackedByPhantomThisTurn(Creature target) =>
        target != null && _phantomAttackTargetsThisTurn.Contains(target);

    internal void ResetPhantomDamageTakenCount()
    {
        AssertMutable();
        PhantomDamageTakenCount = 0;
    }

    internal void RecordPhantomDamage(Creature phantom, ValueProp props)
    {
        if (!IsMutable || !PhantomHelper.IsOwnedMainPhantom(phantom, Owner))
        {
            return;
        }

        PhantomDamageThisTurn = Math.Min(3, PhantomDamageThisTurn + 1);
        if (!props.HasFlag(ValueProp.Unblockable) &&
            CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding)
        {
            // Called once for a real DP hit or actual HP damage, never both.
            PhantomDamageTakenCount = (int)Math.Min(int.MaxValue, (long)PhantomDamageTakenCount + 1);
        }
    }

    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext,
        Creature dealer, DamageResult result, ValueProp props, Creature target, CardModel cardSource)
    {
        if (result.UnblockedDamage > 0)
        {
            RecordPhantomDamage(target, props);
        }

        if (PhantomHelper.IsOwnedMainPhantom(dealer, Owner) &&
            target != null && target.Side != Owner.Creature.Side && props.IsPoweredAttack())
        {
            AssertMutable();
            _phantomAttackTargetsThisTurn.Add(target);
        }
        return Task.CompletedTask;
    }

    private Task AfterRosterConsumed(PlayerChoiceContext choiceContext)
    {
        AssertMutable();
        HasConsumedRosterThisTurn = true;
        return Owner.Creature.GetPower<MemoryRefluxPower>()?.AfterRosterConsumed(choiceContext) ??
            Task.CompletedTask;
    }

    private void ResetPhantomTurnHistory()
    {
        AssertMutable();
        PhantomDamageThisTurn = 0;
        HasConsumedRosterThisTurn = false;
        _phantomAttackTargetsThisTurn.Clear();
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        PendingThievingHopperCardRewards = PhantomThievingHopperHelper.CopyPendingRewards(
            PendingThievingHopperCardRewards);
        ResetPhantomCombatHistory();
        PhantomDamageThisTurn = 0;
        HasConsumedRosterThisTurn = false;
        _phantomAttackTargetsThisTurn = new HashSet<Creature>();
    }

    internal int GetPhantomReviveDelayTurns() =>
        IsMutable
            ? Math.Max(0, PhantomReviveDelayTurns - Math.Max(0,
                Owner?.Creature?.GetPower<EcstaticCorridorPower>()?.Amount ?? 0))
            : PhantomReviveDelayTurns;

    private void SchedulePhantomRevive(Creature phantom)
    {
        if (PhantomReviveTurnNumber > 0 ||
            phantom?.Monster == null ||
            Owner.PlayerCombatState == null)
        {
            return;
        }

        long reviveTurn =
            (long)Owner.PlayerCombatState.TurnNumber +
            GetPhantomReviveDelayTurns();
        // Zero is reserved for no pending revival, including before turn one.
        PhantomReviveTurnNumber = (int)Math.Clamp(reviveTurn, 1L, int.MaxValue);
        PendingPhantomReviveMonsterId = phantom.Monster.Id.ToString();
    }

    private PhantomRosterEntry? CreatePendingPhantomReviveEntry()
    {
        PhantomRosterEntry? activeEntry = GetActiveRosterEntry();
        if (activeEntry is { } rosterEntry)
        {
            return rosterEntry with
            {
                MonsterId = ResolvePendingPhantomReviveMonsterId(
                    rosterEntry.MonsterId)
            };
        }

        if (ActiveRosterEntryId > 0)
        {
            return null;
        }

        ModelId fallbackMonsterId =
            ModelDb.Monster<SmallHopper>().Id;
        return new PhantomRosterEntry(
                0,
                ResolvePendingPhantomReviveMonsterId(fallbackMonsterId),
                0)
            .AddLevels(NewlyObtainedPhantomLevelBonus);
    }

    private ModelId ResolvePendingPhantomReviveMonsterId(
        ModelId fallbackMonsterId)
    {
        if (string.IsNullOrWhiteSpace(PendingPhantomReviveMonsterId))
        {
            return fallbackMonsterId;
        }

        try
        {
            ModelId monsterId = ModelId.Deserialize(
                PendingPhantomReviveMonsterId);
            return ModelDb.GetByIdOrNull<MonsterModel>(monsterId) != null
                ? monsterId
                : fallbackMonsterId;
        }
        catch (JsonException)
        {
            return fallbackMonsterId;
        }
    }

    private void ClearPendingPhantomRevive()
    {
        PhantomReviveTurnNumber = 0;
        PendingPhantomReviveMonsterId = string.Empty;
    }

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        return IsMutable && Owner.Character is HbrgoSaki30A;
    }

    public bool CanExecuteRightClick(ModRightClickExecutionContext context) =>
        IsMutable && Owner.Character is HbrgoSaki30A;

    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (!LocalContext.IsMe(Owner) || NGame.Instance == null)
        {
            return Task.CompletedTask;
        }

        EnsureSummonableMonsterRoster();
        NPhantomRosterScreen.Show(this);
        return Task.CompletedTask;
    }

    internal void EnsureSummonableMonsterRoster()
    {
        if (!IsMutable || Owner.Character is not HbrgoSaki30A)
        {
            return;
        }

        NormalizeRosterData();
    }

    internal IReadOnlyList<PhantomRosterEntry> GetRosterEntries()
    {
        return ParseRosterEntries(out _);
    }

    internal IReadOnlyList<ModelId> GetObtainedPhantomMonsterIds()
    {
        return ParseObtainedPhantomMonsterIds()
            .Concat(GetRosterEntries().Select(entry => entry.MonsterId))
            .Distinct()
            .Where(IsAllowedRosterMonster)
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private List<ModelId> ParseObtainedPhantomMonsterIds()
    {
        List<ModelId> result = new();
        if (string.IsNullOrWhiteSpace(ObtainedPhantomMonsterIds))
        {
            return result;
        }

        string[] serializedIds;
        try
        {
            serializedIds = JsonSerializer.Deserialize<string[]>(
                ObtainedPhantomMonsterIds) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return result;
        }

        foreach (string serializedId in serializedIds)
        {
            if (string.IsNullOrWhiteSpace(serializedId))
            {
                continue;
            }

            try
            {
                result.Add(ModelId.Deserialize(serializedId));
            }
            catch (JsonException)
            {
                // An unavailable or malformed entry must not hide other choices.
            }
        }

        return result;
    }

    private void RememberObtainedPhantoms(IEnumerable<ModelId> monsterIds)
    {
        string[] ids = ParseObtainedPhantomMonsterIds()
            .Concat(monsterIds)
            .Distinct()
            .Select(id => id.ToString())
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        ObtainedPhantomMonsterIds = ids.Length == 0
            ? string.Empty
            : JsonSerializer.Serialize(ids);
    }

    internal PhantomRosterEntry? GetActiveRosterEntry()
    {
        if (ActiveRosterEntryId <= 0)
        {
            return null;
        }

        return GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(entry =>
                entry?.InstanceId == ActiveRosterEntryId);
    }

    internal int GetActivePhantomBaseLevel() =>
        GetActiveRosterEntry()?.Level ?? 1 + Math.Max(0, NewlyObtainedPhantomLevelBonus);

    internal IReadOnlyList<PhantomRosterEntry>
        GetConsumableRosterEntries()
    {
        if (ActiveRosterEntryId <= 0)
        {
            return Array.Empty<PhantomRosterEntry>();
        }

        return GetRosterEntries()
            .Where(entry => entry.InstanceId != ActiveRosterEntryId)
            .ToArray();
    }

    internal void AddExperienceToAllRosterEntries(int amount)
    {
        if (!IsMutable || amount <= 0)
        {
            return;
        }

        foreach (PhantomRosterEntry entry in GetRosterEntries())
        {
            int adjustedAmount = AddCombatExperienceBonus(amount);
            if (entry.InstanceId == ActiveRosterEntryId)
            {
                PendingCombatExperience = (int)Math.Min(
                    PhantomRosterEntry.MaximumStoredExperience,
                    (long)PendingCombatExperience + adjustedAmount);
            }
            else
            {
                QueuePendingRosterExperience(
                    entry.InstanceId,
                    adjustedAmount);
            }
        }
    }

    internal void AddLevelsToAllRosterEntries(int amount)
    {
        if (!IsMutable || amount <= 0)
        {
            return;
        }

        NormalizeRosterData();
        SetRosterEntries(GetRosterEntries()
            .Select(entry => entry.AddLevels(amount))
            .ToArray());
    }

    internal void AddExperienceToActiveRosterEntry(int amount)
    {
        QueueCombatExperience(amount);
    }

    internal void AddPermanentDamageBonus(int rosterEntryId, decimal amount)
    {
        if (!IsMutable || rosterEntryId <= 0 || amount <= 0m)
        {
            return;
        }

        SetRosterEntries(GetRosterEntries().Select(entry => entry.InstanceId == rosterEntryId
            ? entry with { InheritedLevelDamageBonusPercent = entry.InheritedLevelDamageBonusPercent + amount }
            : entry).ToArray());
    }

    internal void AddMinionSacrificeGrowth(int rosterEntryId, int count,
        decimal damageBonusPerMinion, int experiencePerMinion)
    {
        if (!IsMutable || rosterEntryId <= 0 || count <= 0)
        {
            return;
        }

        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int index = entries.FindIndex(entry => entry.InstanceId == rosterEntryId);
        if (index < 0)
        {
            return;
        }

        PhantomRosterEntry entry = entries[index];
        int experience = (int)Math.Min(PhantomRosterEntry.MaximumStoredExperience,
            (long)count * AddCombatExperienceBonus(experiencePerMinion));
        if (ActiveRosterEntryId == rosterEntryId &&
            CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding)
        {
            PendingCombatExperience = (int)Math.Min(PhantomRosterEntry.MaximumStoredExperience,
                (long)PendingCombatExperience + experience);
        }
        else
        {
            // Minion death effects may finish combat before this card resumes.
            entry = entry.AddExperience(experience);
        }

        entries[index] = entry with
        {
            InheritedLevelDamageBonusPercent = entry.InheritedLevelDamageBonusPercent + count * damageBonusPerMinion
        };
        SetRosterEntries(entries);
    }

    internal int LoseLevelFromActiveRosterEntry()
    {
        if (!IsMutable || ActiveRosterEntryId <= 0)
        {
            return 0;
        }

        NormalizeRosterData();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int activeIndex = entries.FindIndex(entry =>
            entry.InstanceId == ActiveRosterEntryId);
        if (activeIndex < 0)
        {
            return 0;
        }

        PhantomRosterEntry current = entries[activeIndex];
        PhantomRosterEntry downgraded = current.LoseLevel();
        if (downgraded.Level >= current.Level)
        {
            return 0;
        }

        entries[activeIndex] = downgraded;
        SetRosterEntries(entries);
        return Math.Max(
            0,
            PhantomFusionEffectHelper.ToInt(
                current.GetBonusMaxHp(Owner) - downgraded.GetBonusMaxHp(Owner)));
    }

    internal IReadOnlyList<PhantomRosterEntry> GetPreviouslyObtainedPhantomChoices()
    {
        int experienceValue = GetExperienceValueForCurrentRoom();
        return GetObtainedPhantomMonsterIds()
            .Select(id => new PhantomRosterEntry(
                    0,
                    id,
                    0,
                    ExperienceValue: experienceValue)
                .AddLevels(NewlyObtainedPhantomLevelBonus))
            .ToArray();
    }

    internal bool AddMonsterToRoster(
        ModelId monsterId,
        bool startsAtLevelTwo = false)
    {
        if (!IsMutable || !IsAllowedRosterMonster(monsterId))
        {
            return false;
        }

        NormalizeRosterData();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        AddObtainedRosterEntry(entries, CreateNewRosterEntry(
            monsterId,
            GetExperienceValueForCurrentRoom(),
            startsAtLevelTwo ? 2 : 1));
        SetRosterEntries(entries);
        return true;
    }

    internal bool AddRandomCurrentRoomMonster(bool startsAtLevelTwo)
    {
        if (!IsMutable || Owner?.RunState?.Act == null)
        {
            return false;
        }

        RoomType roomType =
            Owner.Creature?.CombatState?.Encounter?.RoomType ??
            Owner.RunState.CurrentRoom?.RoomType ??
            RoomType.Monster;
        MonsterModel[] candidates = Owner.RunState.Act.AllEncounters
            .Where(encounter => encounter.RoomType == roomType)
            .SelectMany(encounter => encounter.AllPossibleMonsters)
            .Where(monster =>
                monster != null &&
                monster.ShouldShowInCompendium &&
                !PhantomRosterCandidateHelper.IsMinionOnlyModel(monster) &&
                IsAllowedRosterMonster(monster.Id))
            .GroupBy(monster => monster.Id)
            .Select(group => group.First())
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        MonsterModel selected = candidates[
            Owner.RunState.Rng.Niche.NextInt(candidates.Length)];
        return AddMonsterToRoster(selected.Id, startsAtLevelTwo);
    }

    internal async Task<bool> ConsumeRosterEntries(
        PlayerChoiceContext choiceContext,
        IReadOnlyCollection<int> instanceIds)
    {
        if (!IsMutable || instanceIds == null || instanceIds.Count == 0)
        {
            return false;
        }

        HashSet<int> ids = instanceIds
            .Where(id => id > 0 && id != ActiveRosterEntryId)
            .ToHashSet();
        if (ids.Count != instanceIds.Count)
        {
            return false;
        }

        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        if (!ids.All(id => entries.Any(entry => entry.InstanceId == id)))
        {
            return false;
        }

        entries.RemoveAll(entry => ids.Contains(entry.InstanceId));
        SetRosterEntries(entries);
        RemovePendingRosterExperience(ids);
        await AfterRosterConsumed(choiceContext);
        return true;
    }

    internal async Task<bool> ReplaceActivePhantomWithRedCrimson()
    {
        if (!IsMutable ||
            GetActiveRosterEntry() is not { } activeEntry ||
            PhantomHelper.GetLivingPhantom(Owner) is not { } oldPhantom)
        {
            return false;
        }

        MonsterModel replacementModel = ModelDb.Monster<RedCrimsonBossMonster>();
        PowerTransfer[] inheritedPowers = oldPhantom.Powers
            .Where(power =>
                power.IsVisible &&
                power.Amount != 0 &&
                power is not PhantomPower &&
                power is not HighDimensionalShellPower &&
                power is not MinionPower &&
                PhantomPowerTransferHelper.IsCompatibleWithMonster(
                    power, replacementModel))
            .Select(power => new PowerTransfer(
                power.Id,
                power is PhantomTemporaryLevelPower
                    ? power.Amount
                    : Math.Sign(power.Amount) * Math.Max(
                        1m,
                        Math.Floor(Math.Abs(power.Amount) / 2m))))
            .ToArray();

        StorePhantomHp(oldPhantom);
        await CreatureCmd.Escape(oldPhantom);
        HasSummonedThisCombat = false;
        await Summon(
            activeEntry with
            {
                MonsterId = replacementModel.Id
            },
            isReplacement: true);

        Creature replacement = PhantomHelper.GetLivingPhantom(Owner);
        if (replacement == null)
        {
            return false;
        }

        foreach (PowerTransfer inherited in inheritedPowers)
        {
            PowerModel canonical =
                ModelDb.GetByIdOrNull<PowerModel>(inherited.PowerId);
            if (canonical == null)
            {
                continue;
            }

            await PowerCmd.Apply(
                new ThrowingPlayerChoiceContext(),
                canonical.ToMutable(),
                replacement,
                inherited.Amount,
                Owner.Creature,
                null,
                silent: true);
        }

        return true;
    }

    internal bool CanFuseRosterEntries =>
        !CombatManager.Instance.IsInProgress;

    internal async Task<bool> TryConsumeRosterEntryForActivePhantom(
        PlayerChoiceContext choiceContext,
        int materialInstanceId,
        int experienceMultiplier,
        decimal bonusDamagePercent)
    {
        if (!IsMutable ||
            ActiveRosterEntryId <= 0 ||
            materialInstanceId <= 0 ||
            materialInstanceId == ActiveRosterEntryId)
        {
            return false;
        }

        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int activeIndex = entries.FindIndex(entry =>
            entry.InstanceId == ActiveRosterEntryId);
        int materialIndex = entries.FindIndex(entry =>
            entry.InstanceId == materialInstanceId);
        if (activeIndex < 0 || materialIndex < 0)
        {
            return false;
        }

        PhantomRosterEntry material = entries[materialIndex];
        long requestedExperience =
            (long)Math.Max(1, material.ExperienceValue) *
            Math.Max(0, experienceMultiplier);
        int experience = AddCombatExperienceBonus(
            (int)Math.Min(
                PhantomRosterEntry.MaximumStoredExperience,
                requestedExperience));
        entries[activeIndex] = entries[activeIndex] with
        {
            InheritedLevelDamageBonusPercent =
                entries[activeIndex]
                    .InheritedLevelDamageBonusPercent +
                Math.Max(0m, bonusDamagePercent)
        };
        entries.RemoveAt(materialIndex);
        SetRosterEntries(entries);
        RemovePendingRosterExperience(
            new HashSet<int> { materialInstanceId });
        PendingCombatExperience = (int)Math.Min(
            PhantomRosterEntry.MaximumStoredExperience,
            (long)PendingCombatExperience + experience);
        await AfterRosterConsumed(choiceContext);
        return true;
    }

    internal async Task<bool> ConsumeRosterEntryAsRepeatedFusion(
        PlayerChoiceContext choiceContext,
        int materialInstanceId,
        int fusionCount,
        int experienceMultiplier)
    {
        if (!IsMutable ||
            ActiveRosterEntryId <= 0 ||
            materialInstanceId <= 0 ||
            materialInstanceId == ActiveRosterEntryId ||
            fusionCount <= 0 ||
            experienceMultiplier <= 0)
        {
            return false;
        }

        NormalizeRosterData();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int activeIndex = entries.FindIndex(entry =>
            entry.InstanceId == ActiveRosterEntryId);
        int materialIndex = entries.FindIndex(entry =>
            entry.InstanceId == materialInstanceId);
        if (activeIndex < 0 || materialIndex < 0)
        {
            return false;
        }

        int repetitions = Math.Max(1, fusionCount);
        PhantomRosterEntry target = entries[activeIndex];
        PhantomRosterEntry material = entries[materialIndex];
        bool usesFullInheritance =
            !HasUsedFullUpgradeInheritance &&
            material.HasEarnedUpgradeEffects;
        decimal inheritanceFactor = usesFullInheritance ? 1m : 0.5m;

        string fusionEffectData = target.FusionEffectData;
        for (int index = 0; index < repetitions; index++)
        {
            fusionEffectData = PhantomFusionEffectHelper.AddDirectEffect(
                fusionEffectData,
                material.MonsterId);
        }

        if (material.HasEarnedUpgradeEffects)
        {
            for (int index = 0; index < repetitions; index++)
            {
                fusionEffectData = PhantomFusionEffectHelper.InheritEffects(
                    fusionEffectData,
                    material.FusionEffectData,
                inheritanceFactor);
            }
        }

        fusionEffectData = await ResolveFusionEffectLimit(
            choiceContext,
            target.FusionEffectData,
            fusionEffectData,
            target.FusionSlotCount);

        decimal inheritedFactor = inheritanceFactor * repetitions;
        PhantomRosterEntry upgraded = target with
        {
            UpgradeLevel = (int)Math.Min(
                int.MaxValue,
                (long)target.UpgradeLevel + repetitions),
            InheritedLevelDamageBonusPercent =
                target.InheritedLevelDamageBonusPercent +
                material.EarnedLevelDamageBonusPercent * inheritedFactor,
            InheritedLevelDamageReductionPercent =
                target.InheritedLevelDamageReductionPercent +
                material.EarnedLevelDamageReductionPercent * inheritedFactor,
            InheritedLevelBonusMaxHp =
                target.InheritedLevelBonusMaxHp +
                material.EarnedLevelBonusMaxHp * inheritedFactor,
            InheritedLevelSummonerRollBonus =
                target.InheritedLevelSummonerRollBonus +
                material.SummonerRollBonus * inheritedFactor,
            FusionEffectData = fusionEffectData
        };

        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        decimal previousMaximumDp = phantom == null
            ? 0
            : PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom);
        decimal gainedMaxHp = Math.Max(0m,
            upgraded.GetBonusMaxHp(Owner) - target.GetBonusMaxHp(Owner));

        entries[activeIndex] = upgraded;
        entries.RemoveAt(materialIndex);
        if (usesFullInheritance)
        {
            HasUsedFullUpgradeInheritance = true;
        }

        SetRosterEntries(entries);
        RemovePendingRosterExperience(
            new HashSet<int> { materialInstanceId });
        long requestedExperience =
            (long)NormalizeExperienceValue(material.ExperienceValue) *
            repetitions * experienceMultiplier;
        QueueCombatExperience((int)Math.Min(
            PhantomRosterEntry.MaximumStoredExperience,
            requestedExperience));

        await AfterRosterConsumed(choiceContext);

        if (phantom?.IsAlive != true)
        {
            return true;
        }

        await PhantomHelper.SyncFlatHandFusionPower(phantom);
        if (gainedMaxHp > 0)
        {
            await CreatureCmd.GainMaxHp(phantom, gainedMaxHp);
        }

        PhantomHighDimensionalShellPower shell =
            phantom.GetPower<PhantomHighDimensionalShellPower>();
        decimal currentMaximumDp =
            PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom);
        decimal gainedMaximumDp = Math.Max(0m, currentMaximumDp - previousMaximumDp);
        if (shell != null)
        {
            shell.Restore(gainedMaximumDp);
        }
        else if (gainedMaximumDp > 0 && !phantom.HasPower<HighDimensionalShellPower>())
            await PhantomHelper.RestoreDp(choiceContext, phantom, gainedMaximumDp, null);

        return true;
    }

    internal async Task<bool> FuseMonsterCopyIntoActivePhantom(
        ModelId monsterId,
        int copyLevel)
    {
        if (!IsMutable ||
            ActiveRosterEntryId <= 0 ||
            copyLevel <= 0 ||
            !IsAllowedRosterMonster(monsterId))
        {
            return false;
        }

        NormalizeRosterData();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int activeIndex = entries.FindIndex(entry =>
            entry.InstanceId == ActiveRosterEntryId);
        if (activeIndex < 0)
        {
            return false;
        }

        PhantomRosterEntry target = entries[activeIndex];
        PhantomRosterEntry material = new PhantomRosterEntry(
                0,
                monsterId,
                0,
                ExperienceValue: GetExperienceValueForCurrentRoom())
            .AddLevels(copyLevel - 1);
        bool usesFullInheritance =
            !HasUsedFullUpgradeInheritance &&
            material.HasEarnedUpgradeEffects;
        decimal inheritanceFactor = usesFullInheritance ? 1m : 0.5m;
        string fusionEffectData =
            PhantomFusionEffectHelper.AddDirectEffect(
                target.FusionEffectData,
                material.MonsterId);
        if (material.HasEarnedUpgradeEffects)
        {
            fusionEffectData = PhantomFusionEffectHelper.InheritEffects(
                fusionEffectData,
                material.FusionEffectData,
                inheritanceFactor);
        }

        fusionEffectData = await ResolveFusionEffectLimit(
            new BlockingPlayerChoiceContext(),
            target.FusionEffectData,
            fusionEffectData,
            target.FusionSlotCount);

        PhantomRosterEntry upgraded = target with
        {
            UpgradeLevel = target.UpgradeLevel + 1,
            InheritedLevelDamageBonusPercent =
                target.InheritedLevelDamageBonusPercent +
                material.EarnedLevelDamageBonusPercent *
                inheritanceFactor,
            InheritedLevelDamageReductionPercent =
                target.InheritedLevelDamageReductionPercent +
                material.EarnedLevelDamageReductionPercent *
                inheritanceFactor,
            InheritedLevelBonusMaxHp =
                target.InheritedLevelBonusMaxHp +
                material.EarnedLevelBonusMaxHp * inheritanceFactor,
            InheritedLevelSummonerRollBonus =
                target.InheritedLevelSummonerRollBonus +
                material.SummonerRollBonus * inheritanceFactor,
            FusionEffectData = fusionEffectData
        };
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        decimal previousMaximumDp = phantom == null
            ? 0
            : PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom);
        decimal gainedMaxHp = Math.Max(0m,
            upgraded.GetBonusMaxHp(Owner) - target.GetBonusMaxHp(Owner));

        entries[activeIndex] = upgraded;
        if (usesFullInheritance)
        {
            HasUsedFullUpgradeInheritance = true;
        }

        SetRosterEntries(entries);
        QueueCombatExperience(
            NormalizeExperienceValue(material.ExperienceValue));
        if (phantom?.IsAlive != true)
        {
            return true;
        }

        await PhantomHelper.SyncFlatHandFusionPower(phantom);
        if (gainedMaxHp > 0)
        {
            await CreatureCmd.GainMaxHp(phantom, gainedMaxHp);
        }

        PhantomHighDimensionalShellPower shell =
            phantom.GetPower<PhantomHighDimensionalShellPower>();
        decimal currentMaximumDp =
            PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom);
        decimal gainedMaximumDp = Math.Max(0m, currentMaximumDp - previousMaximumDp);
        if (shell != null)
        {
            shell.Restore(gainedMaximumDp);
        }
        else if (gainedMaximumDp > 0 && !phantom.HasPower<HighDimensionalShellPower>())
            await PhantomHelper.RestoreDp(new ThrowingPlayerChoiceContext(),
                phantom, gainedMaximumDp, null);

        return true;
    }

    private async Task<string> ResolveFusionEffectLimit(
        PlayerChoiceContext choiceContext,
        string currentEffectData,
        string candidateEffectData,
        int maximumEffects)
    {
        IReadOnlyList<PhantomFusionEffectType> originalTypes =
            PhantomFusionEffectHelper.GetEffectTypes(currentEffectData);
        List<PhantomFusionEffectType> retained = originalTypes.ToList();
        while (retained.Count > maximumEffects)
        {
            PhantomFusionEffectType? removed =
                await SelectFusionEffectToRemove(
                    choiceContext,
                    retained,
                    candidateEffectData);
            retained.Remove(removed ?? retained[^1]);
        }

        HashSet<PhantomFusionEffectType> currentTypes =
            originalTypes.ToHashSet();
        foreach (PhantomFusionEffectType incomingType in
                 PhantomFusionEffectHelper
                     .GetEffectTypes(candidateEffectData)
                     .Where(type => !currentTypes.Contains(type)))
        {
            if (retained.Count < maximumEffects)
            {
                retained.Add(incomingType);
                continue;
            }

            PhantomFusionEffectType? replacement =
                await SelectFusionEffectReplacement(
                    choiceContext,
                    retained,
                    incomingType,
                    candidateEffectData);
            if (replacement is not { } replacementType ||
                replacementType == PhantomFusionEffectType.None)
            {
                continue;
            }

            retained.Remove(replacementType);
            retained.Add(incomingType);
        }

        return PhantomFusionEffectHelper.KeepOnlyEffects(
            candidateEffectData,
            retained);
    }

    private async Task<PhantomFusionEffectType?>
        SelectFusionEffectReplacement(
            PlayerChoiceContext choiceContext,
            IReadOnlyList<PhantomFusionEffectType> retained,
            PhantomFusionEffectType incomingType,
            string candidateEffectData)
    {
        string incomingDescription =
            PhantomFusionEffectHelper.GetEffectDescription(
                candidateEffectData,
                incomingType, Owner?.Creature);
        List<PhantomFusionEffectChoiceHbrgoSaki> choices = new();
        PhantomFusionEffectChoiceHbrgoSaki skip = Owner.Creature
            .CombatState
            .CreateCard<PhantomFusionEffectChoiceHbrgoSaki>(Owner);
        skip.ConfigureSkip(incomingDescription);
        choices.Add(skip);
        foreach (PhantomFusionEffectType retainedType in retained)
        {
            PhantomFusionEffectChoiceHbrgoSaki choice = Owner.Creature
                .CombatState
                .CreateCard<PhantomFusionEffectChoiceHbrgoSaki>(Owner);
            choice.ConfigureReplacement(
                retainedType,
                PhantomFusionEffectHelper.GetEffectDescription(
                    candidateEffectData,
                    retainedType, Owner?.Creature),
                incomingDescription);
            choices.Add(choice);
        }

        return await SelectFusionEffectChoice(choiceContext, choices);
    }

    private async Task<PhantomFusionEffectType?> SelectFusionEffectToRemove(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<PhantomFusionEffectType> retained,
        string candidateEffectData)
    {
        List<PhantomFusionEffectChoiceHbrgoSaki> choices = retained
            .Select(type =>
            {
                PhantomFusionEffectChoiceHbrgoSaki choice = Owner.Creature
                    .CombatState
                    .CreateCard<PhantomFusionEffectChoiceHbrgoSaki>(Owner);
                choice.ConfigureRemoval(
                    type,
                    PhantomFusionEffectHelper.GetEffectDescription(
                        candidateEffectData,
                        type, Owner?.Creature));
                return choice;
            })
            .ToList();
        return await SelectFusionEffectChoice(choiceContext, choices);
    }

    private async Task<PhantomFusionEffectType?> SelectFusionEffectChoice(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<PhantomFusionEffectChoiceHbrgoSaki> choices)
    {
        try
        {
            PhantomFusionEffectChoiceHbrgoSaki selected =
                (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(
                        new LocString(
                            "relics",
                            Id.Entry +
                            ".fusionEffectReplacementPrompt"),
                        1)))
                .OfType<PhantomFusionEffectChoiceHbrgoSaki>()
                .FirstOrDefault();
            return selected?.ReplacementEffectType;
        }
        finally
        {
            foreach (PhantomFusionEffectChoiceHbrgoSaki choice in choices)
            {
                choice.RemoveFromState();
            }
        }
    }

    internal void SetSummonableMonsterIds(
        IReadOnlyList<ModelId> monsterIds)
    {
        NextSummonableMonsterInstanceId = 1;
        List<PhantomRosterEntry> entries = monsterIds
            .Where(IsAllowedRosterMonster)
            .Select(id => CreateNewRosterEntry(id, 1, 1))
            .ToList();
        SetRosterEntries(entries);
    }

    internal bool RequestUpgradeRosterEntry(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        IReadOnlyCollection<PhantomFusionEffectType> retainedEffectTypes = null,
        bool consumeFullUpgradeInheritance = false)
    {
        if (!IsMutable ||
            !LocalContext.IsMe(Owner) ||
            !CanFuseRosterEntries)
        {
            return false;
        }

        NormalizeRosterData();
        if (!TryCalculateUpgrade(
                targetInstanceId,
                materialInstanceIds,
                retainedEffectTypes,
                consumeFullUpgradeInheritance,
                enforceEffectLimit: true,
                out PhantomRosterEntry preview,
                out _))
        {
            return false;
        }

        return PhantomRosterNetActions.RequestUpgradeRoster(
            Owner,
            targetInstanceId,
            materialInstanceIds,
            PhantomFusionEffectHelper.GetEffectTypes(
                preview.FusionEffectData),
            consumeFullUpgradeInheritance);
    }

    internal bool TryUpgradeRosterEntry(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        IReadOnlyCollection<PhantomFusionEffectType> retainedEffectTypes = null,
        bool consumeFullUpgradeInheritance = false)
    {
        if (!IsMutable || !CanFuseRosterEntries)
        {
            return false;
        }

        NormalizeRosterData();
        if (!TryCalculateUpgrade(
                targetInstanceId,
                materialInstanceIds,
                retainedEffectTypes,
                consumeFullUpgradeInheritance,
                enforceEffectLimit: true,
                out PhantomRosterEntry upgradedEntry,
                out bool usesFullInheritance))
        {
            return false;
        }

        HashSet<int> materials = materialInstanceIds.ToHashSet();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int targetIndex = entries.FindIndex(entry =>
            entry.InstanceId == targetInstanceId);
        entries[targetIndex] = upgradedEntry;
        entries.RemoveAll(entry => materials.Contains(entry.InstanceId));
        if (usesFullInheritance)
        {
            HasUsedFullUpgradeInheritance = true;
        }

        SetRosterEntries(entries);
        return true;
    }

    internal bool RequestDevourRosterEntries(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds)
    {
        if (!IsMutable ||
            !LocalContext.IsMe(Owner) ||
            !CanFuseRosterEntries)
        {
            return false;
        }

        NormalizeRosterData();
        if (!TryCalculateDevour(
                targetInstanceId,
                materialInstanceIds,
                out _))
        {
            return false;
        }

        return PhantomRosterNetActions.RequestDevourRoster(
            Owner,
            targetInstanceId,
            materialInstanceIds);
    }

    internal bool TryDevourRosterEntries(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds)
    {
        if (!IsMutable || !CanFuseRosterEntries)
        {
            return false;
        }

        NormalizeRosterData();
        if (!TryCalculateDevour(
                targetInstanceId,
                materialInstanceIds,
                out PhantomRosterEntry upgradedEntry))
        {
            return false;
        }

        HashSet<int> materials = materialInstanceIds.ToHashSet();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int targetIndex = entries.FindIndex(entry =>
            entry.InstanceId == targetInstanceId);
        entries[targetIndex] = upgradedEntry;
        entries.RemoveAll(entry => materials.Contains(entry.InstanceId));
        SetRosterEntries(entries);
        RemovePendingRosterExperience(materials);
        return true;
    }

    internal bool TryGetDevourPreview(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        out PhantomRosterEntry upgradedEntry)
    {
        if (!IsMutable || !CanFuseRosterEntries)
        {
            upgradedEntry = default;
            return false;
        }

        NormalizeRosterData();
        return TryCalculateDevour(
            targetInstanceId,
            materialInstanceIds,
            out upgradedEntry);
    }

    internal bool TryGetUpgradePreview(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        bool consumeFullUpgradeInheritance,
        out PhantomRosterEntry upgradedEntry,
        out bool usesFullInheritance)
    {
        if (!IsMutable || !CanFuseRosterEntries)
        {
            upgradedEntry = default;
            usesFullInheritance = false;
            return false;
        }

        NormalizeRosterData();
        return TryCalculateUpgrade(
            targetInstanceId,
            materialInstanceIds,
            retainedEffectTypes: null,
            consumeFullUpgradeInheritance,
            enforceEffectLimit: false,
            out upgradedEntry,
            out usesFullInheritance);
    }

    internal bool TryGetLimitedUpgradePreview(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        IReadOnlyCollection<PhantomFusionEffectType> retainedEffectTypes,
        bool consumeFullUpgradeInheritance,
        out PhantomRosterEntry upgradedEntry,
        out bool usesFullInheritance)
    {
        if (!IsMutable || !CanFuseRosterEntries)
        {
            upgradedEntry = default;
            usesFullInheritance = false;
            return false;
        }

        NormalizeRosterData();
        return TryCalculateUpgrade(
            targetInstanceId,
            materialInstanceIds,
            retainedEffectTypes,
            consumeFullUpgradeInheritance,
            enforceEffectLimit: true,
            out upgradedEntry,
            out usesFullInheritance);
    }

    private bool TryCalculateUpgrade(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        IReadOnlyCollection<PhantomFusionEffectType> retainedEffectTypes,
        bool consumeFullUpgradeInheritance,
        bool enforceEffectLimit,
        out PhantomRosterEntry upgradedEntry,
        out bool usesFullInheritance)
    {
        upgradedEntry = default;
        usesFullInheritance = false;
        if (!CanUpgradeRosterEntry(targetInstanceId, materialInstanceIds))
        {
            return false;
        }

        HashSet<int> materialIds = materialInstanceIds.ToHashSet();
        IReadOnlyList<PhantomRosterEntry> entries = GetRosterEntries();
        PhantomRosterEntry target = entries.First(entry =>
            entry.InstanceId == targetInstanceId);
        PhantomRosterEntry[] materials = entries
            .Where(entry => materialIds.Contains(entry.InstanceId))
            .ToArray();
        PhantomRosterEntry[] inheritableMaterials = materials
            .Where(entry => entry.HasEarnedUpgradeEffects)
            .ToArray();
        if (consumeFullUpgradeInheritance &&
            (HasUsedFullUpgradeInheritance ||
             inheritableMaterials.Length == 0))
        {
            return false;
        }

        usesFullInheritance = consumeFullUpgradeInheritance;
        decimal inheritanceFactor = usesFullInheritance ? 1m : 0.5m;

        string fusionEffectData = target.FusionEffectData;
        foreach (PhantomRosterEntry material in materials)
        {
            fusionEffectData = PhantomFusionEffectHelper.AddDirectEffect(
                fusionEffectData,
                material.MonsterId);
        }

        foreach (PhantomRosterEntry material in inheritableMaterials)
        {
            fusionEffectData = PhantomFusionEffectHelper.InheritEffects(
                fusionEffectData,
                material.FusionEffectData,
                inheritanceFactor);
        }

        IReadOnlyList<PhantomFusionEffectType> availableEffectTypes =
            PhantomFusionEffectHelper.GetEffectTypes(fusionEffectData);
        if (retainedEffectTypes != null)
        {
            IReadOnlyList<PhantomFusionEffectType> currentEffectTypes =
                PhantomFusionEffectHelper.GetEffectTypes(
                    target.FusionEffectData);
            HashSet<PhantomFusionEffectType> retained = retainedEffectTypes
                .Where(type => type != PhantomFusionEffectType.None)
                .ToHashSet();
            if (retained.Count != retainedEffectTypes.Count ||
                retained.Count > target.FusionSlotCount ||
                !retained.IsSubsetOf(availableEffectTypes) ||
                (availableEffectTypes.Count <= target.FusionSlotCount
                    ? !retained.SetEquals(availableEffectTypes)
                    : retained.Count < Math.Min(
                        currentEffectTypes.Count,
                        target.FusionSlotCount)))
            {
                return false;
            }

            fusionEffectData = PhantomFusionEffectHelper.KeepOnlyEffects(
                fusionEffectData,
                retained);
        }
        else if (enforceEffectLimit &&
                 availableEffectTypes.Count > target.FusionSlotCount)
        {
            return false;
        }

        upgradedEntry = target with
        {
            UpgradeLevel = target.UpgradeLevel + materials.Length,
            InheritedLevelDamageBonusPercent =
                target.InheritedLevelDamageBonusPercent +
                inheritableMaterials.Sum(entry =>
                    entry.EarnedLevelDamageBonusPercent *
                    inheritanceFactor),
            InheritedLevelDamageReductionPercent =
                target.InheritedLevelDamageReductionPercent +
                inheritableMaterials.Sum(entry =>
                    entry.EarnedLevelDamageReductionPercent *
                    inheritanceFactor),
            InheritedLevelBonusMaxHp =
                target.InheritedLevelBonusMaxHp +
                inheritableMaterials.Sum(entry =>
                    entry.EarnedLevelBonusMaxHp *
                    inheritanceFactor),
            InheritedLevelSummonerRollBonus =
                target.InheritedLevelSummonerRollBonus +
                inheritableMaterials.Sum(entry =>
                    entry.SummonerRollBonus * inheritanceFactor),
            FusionEffectData = fusionEffectData
        };
        int materialExperience = materials.Sum(entry =>
            NormalizeExperienceValue(entry.ExperienceValue));
        upgradedEntry = upgradedEntry.AddExperience(materialExperience);
        return true;
    }

    private bool TryCalculateDevour(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds,
        out PhantomRosterEntry upgradedEntry)
    {
        upgradedEntry = default;
        if (!CanUpgradeRosterEntry(targetInstanceId, materialInstanceIds))
        {
            return false;
        }

        HashSet<int> materialIds = materialInstanceIds.ToHashSet();
        IReadOnlyList<PhantomRosterEntry> entries = GetRosterEntries();
        PhantomRosterEntry target = entries.First(entry =>
            entry.InstanceId == targetInstanceId);
        long materialExperience = entries
            .Where(entry => materialIds.Contains(entry.InstanceId))
            .Sum(entry => (long)NormalizeExperienceValue(
                entry.ExperienceValue));
        long doubledExperience = Math.Min(
            PhantomRosterEntry.MaximumStoredExperience,
            materialExperience * 2L);
        upgradedEntry = target.AddExperience((int)doubledExperience);
        return true;
    }

    private bool CanUpgradeRosterEntry(
        int targetInstanceId,
        IReadOnlyCollection<int> materialInstanceIds)
    {
        if (!CanFuseRosterEntries)
        {
            return false;
        }

        HashSet<int> materials = materialInstanceIds
            .Where(id => id > 0 && id != targetInstanceId)
            .ToHashSet();
        if (targetInstanceId <= 0 ||
            materials.Count == 0 ||
            materials.Count != materialInstanceIds.Count)
        {
            return false;
        }

        IReadOnlyList<PhantomRosterEntry> entries = GetRosterEntries();
        return entries.Any(entry => entry.InstanceId == targetInstanceId) &&
            materials.All(id => entries.Any(entry => entry.InstanceId == id));
    }

    private void AppendSummonableMonsterIds(
        IEnumerable<ModelId> monsterIds,
        int experienceValue)
    {
        NormalizeRosterData();
        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        foreach (ModelId monsterId in monsterIds.Where(id =>
            IsAllowedRosterMonster(id)))
        {
            AddObtainedRosterEntry(entries, CreateNewRosterEntry(
                monsterId,
                experienceValue,
                1));
        }

        SetRosterEntries(entries);
    }

    private PhantomRosterEntry CreateNewRosterEntry(
        ModelId monsterId,
        int experienceValue,
        int startingLevel)
    {
        return new PhantomRosterEntry(
                AllocateRosterInstanceId(),
                monsterId,
                0,
                ExperienceValue: NormalizeExperienceValue(
                    experienceValue))
            .AddLevels(
                Math.Max(0, startingLevel - 1) +
                NewlyObtainedPhantomLevelBonus);
    }

    private void AddObtainedRosterEntry(List<PhantomRosterEntry> entries, PhantomRosterEntry entry)
    {
        entries.Add(entry);
        if (Owner.Creature.GetPower<DevourRemainingLifePower>()?.Amount > 0)
        {
            // Append directly so bonus acquisitions cannot recursively duplicate themselves.
            entries.Add(entry with { InstanceId = AllocateRosterInstanceId() });
        }
    }

    private void QueueCombatExperience(int amount)
    {
        if (ActiveRosterEntryId <= 0 || amount <= 0)
        {
            return;
        }

        PendingCombatExperience = (int)Math.Min(
            PhantomRosterEntry.MaximumStoredExperience,
            (long)PendingCombatExperience +
            AddCombatExperienceBonus(amount));
    }

    private void QueuePendingRosterExperience(
        int instanceId,
        int amount)
    {
        if (instanceId <= 0 || amount <= 0)
        {
            return;
        }

        Dictionary<int, int> pending = ParsePendingRosterExperience();
        pending.TryGetValue(instanceId, out int current);
        pending[instanceId] = (int)Math.Min(
            PhantomRosterEntry.MaximumStoredExperience,
            (long)current + amount);
        SetPendingRosterExperience(pending);
    }

    private void RemovePendingRosterExperience(
        IReadOnlyCollection<int> instanceIds)
    {
        Dictionary<int, int> pending = ParsePendingRosterExperience();
        bool changed = false;
        foreach (int instanceId in instanceIds)
        {
            changed |= pending.Remove(instanceId);
        }

        if (changed)
        {
            SetPendingRosterExperience(pending);
        }
    }

    private void CommitPendingRosterExperience()
    {
        Dictionary<int, int> pending = ParsePendingRosterExperience();
        if (pending.Count == 0)
        {
            return;
        }

        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        for (int index = 0; index < entries.Count; index++)
        {
            if (pending.TryGetValue(
                    entries[index].InstanceId,
                    out int amount))
            {
                entries[index] = entries[index].AddExperience(amount);
            }
        }

        SetRosterEntries(entries);
        PendingRosterExperienceData = string.Empty;
    }

    private Dictionary<int, int> ParsePendingRosterExperience()
    {
        Dictionary<int, int> result = new();
        foreach (string item in PendingRosterExperienceData.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split(':', 2);
            if (parts.Length == 2 &&
                int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int instanceId) &&
                int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int amount) &&
                instanceId > 0 &&
                amount > 0)
            {
                result[instanceId] = Math.Clamp(
                    amount,
                    0,
                    PhantomRosterEntry.MaximumStoredExperience);
            }
        }

        return result;
    }

    private void SetPendingRosterExperience(
        IReadOnlyDictionary<int, int> pending)
    {
        PendingRosterExperienceData = string.Join(
            ';',
            pending
                .Where(item => item.Key > 0 && item.Value > 0)
                .OrderBy(item => item.Key)
                .Select(item => string.Concat(
                    item.Key.ToString(CultureInfo.InvariantCulture),
                    ":",
                    item.Value.ToString(CultureInfo.InvariantCulture))));
    }

    private void CommitPostCombatGrowth(decimal damageBonusPercent)
    {
        if (ActiveRosterEntryId <= 0 ||
            (PendingCombatExperience <= 0 && damageBonusPercent <= 0m))
        {
            return;
        }

        List<PhantomRosterEntry> entries = GetRosterEntries().ToList();
        int activeIndex = entries.FindIndex(entry =>
            entry.InstanceId == ActiveRosterEntryId);
        if (activeIndex < 0)
        {
            return;
        }

        PhantomRosterEntry entry = entries[activeIndex]
            .AddExperience(PendingCombatExperience);
        entries[activeIndex] = entry with
        {
            InheritedLevelDamageBonusPercent =
                entry.InheritedLevelDamageBonusPercent +
                Math.Max(0m, damageBonusPercent)
        };
        SetRosterEntries(entries);
    }

    private int AddCombatExperienceBonus(int amount)
    {
        int familiarity = Owner.Creature?
            .GetPower<PhantomFamiliarityPower>()?
            .Amount ?? 0;
        return (int)Math.Min(
            PhantomRosterEntry.MaximumStoredExperience,
            (long)Math.Max(0, amount) + Math.Max(0, familiarity));
    }

    private void SetRosterEntries(
        IReadOnlyList<PhantomRosterEntry> entries)
    {
        IReadOnlyList<PhantomRosterEntry> previousEntries = GetRosterEntries();
        PhantomRosterEntry[] validEntries = entries
            .Where(entry =>
                entry.InstanceId > 0 &&
                IsAllowedRosterMonster(entry.MonsterId))
            .Select(entry => entry with
            {
                UpgradeLevel = Math.Max(0, entry.UpgradeLevel),
                Experience = Math.Clamp(
                    entry.Experience,
                    0,
                    PhantomRosterEntry.MaximumStoredExperience),
                ExperienceValue = NormalizeExperienceValue(
                    entry.ExperienceValue),
                InheritedLevelDamageBonusPercent = Math.Max(
                    0m,
                    entry.InheritedLevelDamageBonusPercent),
                InheritedLevelDamageReductionPercent = Math.Max(
                    0m,
                    entry.InheritedLevelDamageReductionPercent),
                InheritedLevelBonusMaxHp = Math.Max(
                    0m,
                    entry.InheritedLevelBonusMaxHp),
                InheritedLevelSummonerRollBonus = Math.Max(
                    0m,
                    entry.InheritedLevelSummonerRollBonus),
                FusionEffectData =
                    PhantomFusionEffectHelper.Normalize(
                        entry.FusionEffectData)
            })
            .ToArray();
        // Include the old roster before consumption, including legacy saves without history.
        RememberObtainedPhantoms(previousEntries
            .Concat(validEntries)
            .Select(entry => entry.MonsterId));
        SummonableMonsterIds = string.Join(
            '\n',
            validEntries.Select(entry => string.Join(
                '|',
                "v4",
                entry.InstanceId.ToString(CultureInfo.InvariantCulture),
                entry.UpgradeLevel.ToString(CultureInfo.InvariantCulture),
                entry.Experience.ToString(CultureInfo.InvariantCulture),
                entry.ExperienceValue.ToString(CultureInfo.InvariantCulture),
                entry.InheritedLevelDamageBonusPercent.ToString(
                    CultureInfo.InvariantCulture),
                entry.InheritedLevelDamageReductionPercent.ToString(
                    CultureInfo.InvariantCulture),
                entry.InheritedLevelBonusMaxHp.ToString(
                    CultureInfo.InvariantCulture),
                entry.InheritedLevelSummonerRollBonus.ToString(
                    CultureInfo.InvariantCulture),
                entry.FusionEffectData,
                entry.MonsterId.ToString())));
        HashSet<int> retainedIds = validEntries.Select(entry => entry.InstanceId).ToHashSet();
        Dictionary<int, decimal> health = ReadPhantomHealth();
        // Live fusion already uses GainMaxHp and saves the resulting HP. Only
        // update the stored value here, including off-field and post-combat growth.
        RestoreStoredHpFromRosterGrowth(previousEntries, validEntries, health);
        foreach (int id in health.Keys.Where(id => id != 0 && !retainedIds.Contains(id)).ToArray())
        {
            health.Remove(id);
        }
        PhantomHealthData = health.Count > 0 ? JsonSerializer.Serialize(health) : string.Empty;
        PrunePhantomBaseMaxHp(retainedIds);
        int nextId = validEntries.Length == 0
            ? 1
            : validEntries.Max(entry => entry.InstanceId) + 1;
        NextSummonableMonsterInstanceId = Math.Max(
            NextSummonableMonsterInstanceId,
            nextId);
    }

    private List<PhantomRosterEntry> ParseRosterEntries(
        out bool requiresRewrite)
    {
        requiresRewrite = false;
        List<PhantomRosterEntry> entries = new();
        HashSet<int> usedIds = new();
        int fallbackId = Math.Max(1, NextSummonableMonsterInstanceId);
        foreach (string line in SummonableMonsterIds.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries))
        {
            bool isV3 = line.StartsWith("v3|", StringComparison.Ordinal);
            bool isV4 = line.StartsWith("v4|", StringComparison.Ordinal);
            bool hasRollBonusField = isV3 || isV4;
            string[] parts = line.Split('|', hasRollBonusField ? 11 : 10);
            int instanceId;
            int upgradeLevel;
            int experience = 0;
            int experienceValue = 1;
            decimal inheritedLevelDamageBonusPercent = 0m;
            decimal inheritedLevelDamageReductionPercent = 0m;
            decimal inheritedLevelBonusMaxHp = 0m;
            decimal inheritedLevelSummonerRollBonus = 0m;
            string fusionEffectData = string.Empty;
            string serializedId;
            if (((parts.Length == 10 && parts[0] == "v2") ||
                 (parts.Length == 11 && hasRollBonusField)) &&
                int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out instanceId) &&
                int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out upgradeLevel) &&
                int.TryParse(
                    parts[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out experience) &&
                int.TryParse(
                    parts[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out experienceValue) &&
                decimal.TryParse(
                    parts[5],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out inheritedLevelDamageBonusPercent) &&
                decimal.TryParse(
                    parts[6],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out inheritedLevelDamageReductionPercent) &&
                decimal.TryParse(
                    parts[7],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out inheritedLevelBonusMaxHp) &&
                (!hasRollBonusField || decimal.TryParse(
                    parts[8],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out inheritedLevelSummonerRollBonus)))
            {
                fusionEffectData = parts[hasRollBonusField ? 9 : 8];
                serializedId = parts[hasRollBonusField ? 10 : 9];
                requiresRewrite |= !isV4;
            }
            else if (parts.Length == 8 &&
                int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out instanceId) &&
                int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out upgradeLevel) &&
                int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out experience) &&
                int.TryParse(
                    parts[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out experienceValue) &&
                decimal.TryParse(
                    parts[5],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal legacyLevelEffectPercent) &&
                decimal.TryParse(
                    parts[6],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal legacyBonusMaxHp))
            {
                inheritedLevelDamageBonusPercent = Math.Max(
                    0m,
                    legacyLevelEffectPercent);
                inheritedLevelDamageReductionPercent = Math.Max(
                    0m,
                    legacyLevelEffectPercent / 2m);
                inheritedLevelBonusMaxHp = Math.Max(
                    0m,
                    legacyBonusMaxHp);
                serializedId = parts[7];
                requiresRewrite = true;
            }
            else if (parts.Length == 3 &&
                int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out instanceId) &&
                int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out upgradeLevel))
            {
                serializedId = parts[2];
                requiresRewrite = true;
            }
            else
            {
                instanceId = 0;
                upgradeLevel = 0;
                serializedId = line;
                requiresRewrite = true;
            }

            if (isV3)
            {
                // V3 persisted inherited roll bonuses at +2 per level.
                inheritedLevelSummonerRollBonus *=
                    PhantomRosterEntry.LevelSummonerRollBonus / 2m;
            }
            else if (!isV4)
            {
                // Older saves recorded inherited upgrades before the roll stat existed.
                inheritedLevelSummonerRollBonus = Math.Max(0m,
                    inheritedLevelDamageReductionPercent) /
                    PhantomRosterEntry.LevelDamageReductionPercent *
                    PhantomRosterEntry.LevelSummonerRollBonus;
            }

            ModelId monsterId;
            try
            {
                monsterId = ModelId.Deserialize(serializedId);
            }
            catch
            {
                requiresRewrite = true;
                continue;
            }

            if (!IsAllowedRosterMonster(monsterId))
            {
                requiresRewrite = true;
                continue;
            }

            if (instanceId <= 0 || !usedIds.Add(instanceId))
            {
                while (usedIds.Contains(fallbackId))
                {
                    fallbackId++;
                }

                instanceId = fallbackId++;
                usedIds.Add(instanceId);
                requiresRewrite = true;
            }

            if (upgradeLevel < 0)
            {
                upgradeLevel = 0;
                requiresRewrite = true;
            }

            entries.Add(new PhantomRosterEntry(
                instanceId,
                monsterId,
                Math.Max(0, upgradeLevel),
                Math.Clamp(
                    experience,
                    0,
                    PhantomRosterEntry.MaximumStoredExperience),
                NormalizeExperienceValue(experienceValue),
                Math.Max(0m, inheritedLevelDamageBonusPercent),
                Math.Max(0m, inheritedLevelDamageReductionPercent),
                Math.Max(0m, inheritedLevelBonusMaxHp),
                PhantomFusionEffectHelper.Normalize(
                    fusionEffectData),
                Math.Max(0m, inheritedLevelSummonerRollBonus)));
            fallbackId = Math.Max(fallbackId, instanceId + 1);
        }

        return entries;
    }

    private void NormalizeRosterData()
    {
        List<PhantomRosterEntry> entries =
            ParseRosterEntries(out bool requiresRewrite);
        RememberObtainedPhantoms(entries.Select(entry => entry.MonsterId));
        int nextId = entries.Count == 0
            ? 1
            : entries.Max(entry => entry.InstanceId) + 1;
        if (requiresRewrite ||
            NextSummonableMonsterInstanceId < nextId)
        {
            NextSummonableMonsterInstanceId = Math.Max(
                NextSummonableMonsterInstanceId,
                nextId);
            SetRosterEntries(entries);
        }
    }

    private int AllocateRosterInstanceId()
    {
        int id = Math.Max(1, NextSummonableMonsterInstanceId);
        NextSummonableMonsterInstanceId = id + 1;
        return id;
    }

    private int GetExperienceValueForCurrentRoom()
    {
        return GetExperienceValue(
            Owner.RunState.CurrentRoom?.RoomType ?? RoomType.Monster);
    }

    private static int GetExperienceValue(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Elite => 2,
            RoomType.Boss => 4,
            _ => 1
        };
    }

    private static int NormalizeExperienceValue(int value)
    {
        if (value >= 4)
        {
            return 4;
        }

        return value >= 2 ? 2 : 1;
    }

    private static bool IsAllowedRosterMonster(ModelId monsterId)
    {
        MonsterModel monster =
            ModelDb.GetByIdOrNull<MonsterModel>(monsterId);
        return monster != null &&
            monster is not BattleFriendV1 &&
            monster is not BattleFriendV2 &&
            monster is not BattleFriendV3;
    }

    private async Task Summon(
        PhantomRosterEntry rosterEntry,
        bool isReplacement = false,
        bool isRevival = false,
        decimal revivalHealthFraction = 1m)
    {
        MonsterModel canonical =
            ModelDb.GetByIdOrNull<MonsterModel>(rosterEntry.MonsterId);
        if (canonical == null || HasSummonedThisCombat)
        {
            return;
        }

        ActiveRosterEntryId = rosterEntry.InstanceId;
        if (!isRevival && GetStoredPhantomHp(rosterEntry.InstanceId) == 0)
        {
            HasSummonedThisCombat = true;
            PendingPhantomReviveMonsterId = rosterEntry.MonsterId.ToString();
            PhantomReviveTurnNumber = (int)Math.Min(int.MaxValue,
                (long)(Owner.PlayerCombatState?.TurnNumber ?? 0) + PhantomReviveDelayTurns);
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState;
        MonsterModel monster = (MonsterModel)canonical.ToMutable();
        Creature phantom;
        using (PhantomAscensionHelper.BeginPhantomEvaluation())
        {
            phantom = combatState.CreateCreature(
                monster,
                Owner.Creature.Side,
                null);
        }
        decimal baseInitialHp = rosterEntry.GetBasePhantomHp(
            LargeCreatureHealthHelper.GetMaxHp(phantom),
            monster is RedCrimsonBossMonster, Owner);
        decimal bonusMaxHp = Math.Min(
            MaximumPhantomHp - baseInitialHp,
            rosterEntry.GetBonusMaxHp(Owner));
        decimal unadjustedInitialHp = baseInitialHp + bonusMaxHp;
        decimal initialHp = GetAdjustedPhantomMaxHp(unadjustedInitialHp);
        ExactActivePhantomMaxHpAdjustment = initialHp - unadjustedInitialHp;
        SetPhantomBaseMaxHp(rosterEntry.InstanceId, baseInitialHp);
        decimal carriedHp = GetPhantomSummonHp(rosterEntry.InstanceId, initialHp, isRevival);
        if (isRevival)
            carriedHp = GetPhantomRevivalHp(initialHp, revivalHealthFraction);
        phantom.SetMaxHpInternal(initialHp);
        phantom.SetCurrentHpInternal(carriedHp);
        using (PhantomAscensionHelper.BeginPhantomEvaluation())
        {
            await PlayerCmd.AddPet(phantom, Owner);
            PhantomPower phantomPower = (PhantomPower)ModelDb.Power<PhantomPower>().ToMutable();
            phantomPower.InitializeSizeBaseMaxHp(baseInitialHp);
            phantomPower.ConfigureAsRosterPhantom();
            await PowerCmd.Apply(
                new ThrowingPlayerChoiceContext(),
                phantomPower,
                phantom,
                1m,
                Owner.Creature,
                null);
            StorePhantomHp(phantom);
            await PhantomHelper.RunPhantomEntryEffects(phantom);
            // Some monster entry hooks initialize HP again; that is not cross-battle healing.
            phantom.SetCurrentHpInternal(Math.Min(carriedHp, LargeCreatureHealthHelper.GetMaxHp(phantom)));
            StorePhantomHp(phantom);
            ArrangePhantomVisuals(phantom);
            await PowerCmd.Apply<PhantomHighDimensionalShellPower>(
                new ThrowingPlayerChoiceContext(),
                phantom,
                PhantomHighDimensionalShellPower.CalculateInitialAmount(
                    phantom),
                Owner.Creature,
                null);

            HasSummonedThisCombat = true;
            if (!isReplacement)
            {
                await PhantomHelper.ApplyOnSummonFusionEffects(phantom);
            }
            StorePhantomHp(phantom);
            phantom.PrepareForNextTurn(
                combatState.GetOpponentsOf(phantom)
                    .Where(PhantomHelper.IsValidPhantomTarget));
        }

        PhantomAscensionHelper.RefreshEnemyAscensionEffects(combatState);
        NCombatRoom.Instance?.SetCreatureIsInteractable(
            phantom,
            on: true);
    }

    internal static decimal GetPhantomRevivalHp(decimal maxHp, decimal fraction) =>
        Math.Max(1m, decimal.Floor(Math.Max(1m, maxHp) * Math.Clamp(fraction, 0m, 1m)));

    private readonly record struct PowerTransfer(
        ModelId PowerId,
        decimal Amount);

    internal static void ArrangePhantomVisuals(Creature phantom)
    {
        NCreature phantomNode = NCombatRoom.Instance?
            .GetCreatureNode(phantom);
        if (phantomNode == null)
        {
            return;
        }

        phantomNode.Body.Scale *= new Vector2(-1f, 1f);
        if (PhantomHelper.IsPhantomMinion(phantom))
        {
            Core.Nodes.Combat.NPhantomMinionPlacement.NotifyCreatureAdded(NCombatRoom.Instance, phantom);
            return;
        }
        if (phantom.PetOwner == null ||
            !LocalContext.IsMe(phantom.PetOwner))
        {
            return;
        }

        NCreature ownerNode = NCombatRoom.Instance?
            .GetCreatureNode(phantom.PetOwner.Creature);
        if (ownerNode != null)
        {
            phantomNode.Position = ownerNode.Position +
                NCreature.GetOstyOffsetFromPlayer(phantom);
        }
    }
}
