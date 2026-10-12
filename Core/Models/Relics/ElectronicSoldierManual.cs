using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Rewards;
using HBRGOSakiMod.Core.Models.Powers;
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
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual : RelicModel
{
    public const int MaxSixSense = 99;

    public const int SixSenseBonusThreshold = 30;

    public const int StartingSixSensePointsPerPair = 5;

    public virtual int SixSenseRewardOptionCount => 3;

    public static readonly IReadOnlyList<SixSenseAttribute> AllSixSenseAttributes = new[]
    {
        SixSenseAttribute.LeadershipCharm,
        SixSenseAttribute.Talent,
        SixSenseAttribute.Gentleness,
        SixSenseAttribute.MentalStrength,
        SixSenseAttribute.Simplicity,
        SixSenseAttribute.Madness
    };

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/electronic_soldier_manual.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LeadershipCharm { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Talent { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Gentleness { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MentalStrength { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Simplicity { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Madness { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool StartingSixSenseGranted { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingLeadershipCharmRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingTalentRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingGentlenessRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingMentalStrengthRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingSimplicityRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingMadnessRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingSixSenseRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingAllSenseIncreases { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SixSensePoints
    {
        // Saved properties can load in either order. Limit the usable balance
        // against the final levels instead of discarding points in the setter.
        get => Math.Clamp(_sixSensePoints, 0, GetAvailableSixSenseIncreaseCount());
        protected set => _sixSensePoints = Math.Max(0, value);
    }

    private int _sixSensePoints;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SixSenseAllocationRevision { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CombatSixSensePointsGranted { get; protected set; }

    internal bool IsAllocationPending { get; set; }

    protected virtual int CombatSixSensePointCount => 1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int JoiningCeremonyRollCount { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool JoiningCeremonyTriggered { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MentalStrengthDpLossProgress { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingMentalStrengthStatRewards { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MadnessThresholdRewardApplied { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MadnessThresholdDpConversionFixed { get; protected set; }

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NormalStatePower>()
    };

    public override async Task BeforeCombatStart()
    {
        CombatSixSensePointsGranted = false;
        ClearPendingSixSenseRewards();
        Flash();
        if (Owner.Character is HbrgoSaki and not HbrgoSaki30A)
        {
            await PowerCmd.Apply<NormalStatePower>(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                1m,
                Owner.Creature,
                null);
        }
    }

    public override decimal ModifyGoldGained(Player player, decimal amount) =>
        player == Owner && amount > 0m
            ? amount * (1m + Math.Max(0m, GetEffectiveSenseAmount(SixSenseAttribute.Talent)) * 0.02m)
            : amount;

    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (Owner.Character is not (HbrgoSaki or HbrgoHotaru) ||
            Owner.RunState.CurrentActIndex != 0 ||
            JoiningCeremonyTriggered ||
            (Owner.RunState.Players.FirstOrDefault(player =>
                player.Character is HbrgoSaki) ??
             Owner.RunState.Players.FirstOrDefault(player =>
                player.Character is HbrgoHotaru)) != Owner)
        {
            return currentEvent;
        }

        EventModel joiningCeremony =
            ModelDb.Event<JoiningCeremonyHbrgoSaki>();
        if (currentEvent.Id == joiningCeremony.Id)
        {
            return currentEvent;
        }

        bool isFirstEvent = JoiningCeremonyRollCount == 0;
        JoiningCeremonyRollCount++;
        // The first ordinary event is guaranteed; keep the fallback for older runs.
        if (!isFirstEvent && Owner.RunState.Rng.Niche.NextFloat() >= 0.5f)
        {
            return currentEvent;
        }

        JoiningCeremonyTriggered = true;
        return joiningCeremony;
    }

    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom room)
    {
        if (player != Owner || room is not CombatRoom)
        {
            return false;
        }

        int restoredRewards = rewards.RemoveAll(reward => reward is SixSenseReward);
        long points = restoredRewards;
        if (!CombatSixSensePointsGranted)
        {
            if (restoredRewards == 0)
            {
                GrantCombatSixSenseIncreases();
                int roomBonus = room.RoomType switch
                {
                    RoomType.Elite => 1,
                    RoomType.Boss => 3,
                    _ => 0
                };
                points += roomBonus + CombatSixSensePointCount + (long)PendingSixSenseRewards;
                points += AllSixSenseAttributes.Sum(attribute => (long)GetPendingRewardCount(attribute));
            }
            GrantPendingAllSenseIncreases();
            CombatSixSensePointsGranted = true;
        }
        GrantSixSensePoints(points);
        ClearPendingSixSenseRewards();
        return restoredRewards > 0;
    }

    private void GrantPendingAllSenseIncreases()
    {
        if (PendingAllSenseIncreases <= 0) return;
        foreach (SixSenseAttribute attribute in AllSixSenseAttributes)
            if (CanIncreaseSense(attribute))
                SetSenseAmount(attribute, (int)Math.Min(MaxSixSense, (long)GetSenseAmount(attribute) + PendingAllSenseIncreases));
        Flash();
    }

    internal void QueueAllSenseIncrease()
    {
        AssertMutable();
        PendingAllSenseIncreases = (int)Math.Min(int.MaxValue, (long)PendingAllSenseIncreases + 1);
        Flash();
    }

    private void GrantCombatSixSenseIncreases()
    {
        AssertMutable();
        bool increased = false;
        foreach (CardType type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
        {
            SixSenseAttribute[] candidates = SixSenseCardCapability.GetCandidates(type)
                .Where(CanIncreaseSense).ToArray();
            if (candidates.Length == 0) continue;
            SixSenseAttribute attribute = candidates[Owner.PlayerRng.Rewards.NextInt(candidates.Length)];
            SetSenseAmount(attribute, GetSenseAmount(attribute) + 1);
            increased = true;
        }
        if (increased) Flash();
    }

    internal void GrantSixSensePoints(long amount)
    {
        AssertMutable();
        if (amount <= 0) return;
        int previous = SixSensePoints;
        int added = (int)Math.Min(amount, GetAvailableSixSenseIncreaseCount() - previous);
        SixSensePoints = previous + added;
        SynchronizeMemoryGardenUpgradePoints();
        if (added > 0) Flash();
    }

    internal bool CanAllocateSixSensePoints(IReadOnlyList<int> increases, int revision)
    {
        if (!IsMutable || revision != SixSenseAllocationRevision ||
            revision == int.MaxValue || increases?.Count != AllSixSenseAttributes.Count) return false;
        long total = 0;
        for (int i = 0; i < increases.Count; i++)
        {
            int amount = increases[i];
            if (amount < 0 || amount > Math.Max(0, MaxSixSense - GetSenseAmount(AllSixSenseAttributes[i]))) return false;
            total += amount;
        }
        return total > 0 && total <= SixSensePoints;
    }

    internal bool ApplySixSenseAllocation(IReadOnlyList<int> increases, int revision)
    {
        if (!CanAllocateSixSensePoints(increases, revision)) return false;
        using var balance = MemoryGardenUpgradePointsHelper.BeginBalanceUpdate(this);
        int total = increases.Sum();
        int remaining = SixSensePoints - total;
        for (int i = 0; i < increases.Count; i++)
        {
            if (increases[i] > 0)
                SetSenseAmount(AllSixSenseAttributes[i], GetSenseAmount(AllSixSenseAttributes[i]) + increases[i]);
        }
        SixSensePoints = remaining;
        SixSenseAllocationRevision++;
        Flash();
        return true;
    }

    public bool HasAvailableSixSenseIncrease()
    {
        return AllSixSenseAttributes.Any(CanIncreaseSense);
    }

    public int GetAvailableSixSenseIncreaseCount()
    {
        return AllSixSenseAttributes.Sum(attribute =>
            Math.Max(0, MaxSixSense - GetSenseAmount(attribute)));
    }

    public virtual IReadOnlyList<SixSenseAttribute> RollSixSenseRewardOptions()
    {
        return ReplenishSixSenseRewardOptions(
            Array.Empty<SixSenseAttribute>());
    }

    internal IReadOnlyList<SixSenseAttribute>
        ReplenishSixSenseRewardOptions(
            IEnumerable<SixSenseAttribute> existingOptions,
            bool canExceedMaximum = false)
    {
        Func<SixSenseAttribute, bool> isAvailable = canExceedMaximum
            ? _ => true
            : CanIncreaseSense;
        List<SixSenseAttribute> availableAttributes = AllSixSenseAttributes
            .Where(isAvailable)
            .ToList();

        int targetCount = Math.Min(
            SixSenseRewardOptionCount,
            availableAttributes.Count);
        List<SixSenseAttribute> options = existingOptions
            .Where(isAvailable)
            .Distinct()
            .Take(targetCount)
            .ToList();
        if (options.Count >= targetCount)
        {
            return options;
        }

        List<SixSenseAttribute> replacements = availableAttributes
            .Where(attribute => !options.Contains(attribute))
            .ToList();
        Owner.PlayerRng.Rewards.Shuffle(replacements);
        options.AddRange(
            replacements.Take(targetCount - options.Count));
        return options;
    }

    public async Task<bool> ChooseAndIncreaseSense(
        IReadOnlyList<SixSenseAttribute> options,
        bool canExceedMaximum = false)
    {
        SixSenseAttribute[] availableOptions = options
            .Where(attribute => canExceedMaximum ||
                CanIncreaseSense(attribute))
            .Distinct()
            .ToArray();
        if (availableOptions.Length == 0)
        {
            return false;
        }

        SixSenseAttribute? selectedAttribute = await SixSenseChoiceHelper.Choose(
            Owner,
            this,
            availableOptions,
            canExceedMaximum);
        if (!selectedAttribute.HasValue)
        {
            return false;
        }

        return await TryIncreaseSense(
            selectedAttribute.Value,
            canExceedMaximum);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        return 1m;
    }

    public int GetSenseAmount(SixSenseAttribute attribute)
    {
        return attribute switch
        {
            SixSenseAttribute.LeadershipCharm => LeadershipCharm,
            SixSenseAttribute.Talent => Talent,
            SixSenseAttribute.Gentleness => Gentleness,
            SixSenseAttribute.MentalStrength => MentalStrength,
            SixSenseAttribute.Simplicity => Simplicity,
            SixSenseAttribute.Madness => Madness,
            _ => 0
        };
    }

    public bool CanIncreaseSense(SixSenseAttribute attribute)
    {
        return GetSenseAmount(attribute) < MaxSixSense;
    }

    internal void GrantStartingSixSense()
    {
        if (!IsMutable || StartingSixSenseGranted || Owner?.PlayerRng == null)
        {
            return;
        }

        foreach (CardType type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
        {
            SixSenseAttribute[] pair = SixSenseCardCapability.GetCandidates(type);
            int first = Owner.PlayerRng.Rewards.NextInt(StartingSixSensePointsPerPair + 1);
            SetSenseAmount(pair[0], GetSenseAmount(pair[0]) + first);
            SetSenseAmount(pair[1], GetSenseAmount(pair[1]) + StartingSixSensePointsPerPair - first);
        }
        StartingSixSenseGranted = true;
    }

    public bool HasSixSenseBonus(SixSenseAttribute attribute)
    {
        return GetSenseAmount(attribute) >= SixSenseBonusThreshold;
    }

    public decimal GetEffectiveSenseAmount(SixSenseAttribute attribute) =>
        GetEffectiveSenseAmount(attribute, GetSenseAmount(attribute));

    internal decimal GetEffectiveSenseAmount(SixSenseAttribute attribute, int level)
    {
        decimal multiplier = Owner?.Creature?.GetPower<AllRounderPower>()?.SenseMultiplier ?? 1m;
        multiplier *= Owner?.Creature?.GetPower<LoliFormPower>()?.GetSenseMultiplier(attribute) ?? 1m;
        return level * multiplier;
    }

    public void QueueSixSenseReward(SixSenseAttribute attribute)
    {
        SetPendingRewardCount(attribute, GetPendingRewardCount(attribute) + 1);
        Flash();
    }

    public void QueueSixSenseReward(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        PendingSixSenseRewards = (int)Math.Min(int.MaxValue, (long)PendingSixSenseRewards + amount);
        Flash();
    }

    // Retained for existing DP callers; six senses no longer change recovery.
    public static decimal GetSimplicityDpRecoveryMultiplier(Player player) => 1m;

    public static string GetSenseTitle(SixSenseAttribute attribute)
    {
        return attribute switch
        {
            SixSenseAttribute.LeadershipCharm => "领袖魅力",
            SixSenseAttribute.Talent => "才能",
            SixSenseAttribute.Gentleness => "温柔",
            SixSenseAttribute.MentalStrength => "精神力",
            SixSenseAttribute.Simplicity => "单纯",
            SixSenseAttribute.Madness => "疯狂",
            _ => string.Empty
        };
    }

    public static string GetSenseShortDescription(SixSenseAttribute attribute)
    {
        string name = GetSenseTitle(attribute);
        return GetSenseChoiceDescription(attribute) +
            $"达到{SixSenseBonusThreshold}级后，其他属性牌也可削减{name}抗性，但不获得该属性的出目加值和出牌效果。上限{MaxSixSense}级。";
    }

    public string GetSenseShortDescriptionForOwner(SixSenseAttribute attribute) =>
        GetSenseShortDescription(attribute);

    public static string GetSenseChoiceDescription(SixSenseAttribute attribute) =>
        $"每级使{GetSenseTitle(attribute)}属性牌的出目+1。" +
        (attribute == SixSenseAttribute.Talent ? "每点才能使获得的金币+2%。" : string.Empty);

    public string GetSenseChoiceDescriptionForOwner(SixSenseAttribute attribute) =>
        GetSenseChoiceDescription(attribute);

    public static string GetSenseRewardDescriptionKey(SixSenseAttribute attribute)
    {
        return attribute switch
        {
            SixSenseAttribute.LeadershipCharm => "SIX_SENSE_LEADERSHIP_CHARM_REWARD.description",
            SixSenseAttribute.Talent => "SIX_SENSE_TALENT_REWARD.description",
            SixSenseAttribute.Gentleness => "SIX_SENSE_GENTLENESS_REWARD.description",
            SixSenseAttribute.MentalStrength => "SIX_SENSE_MENTAL_STRENGTH_REWARD.description",
            SixSenseAttribute.Simplicity => "SIX_SENSE_SIMPLICITY_REWARD.description",
            SixSenseAttribute.Madness => "SIX_SENSE_MADNESS_REWARD.description",
            _ => "SIX_SENSE_REWARD.description"
        };
    }

    public Task<bool> TryIncreaseSense(SixSenseAttribute attribute)
    {
        return TryIncreaseSense(attribute, canExceedMaximum: false);
    }

    internal async Task<bool> TryIncreaseSense(
        SixSenseAttribute attribute,
        bool canExceedMaximum)
    {
        if (!canExceedMaximum && !CanIncreaseSense(attribute))
        {
            return false;
        }

        if (!LocalContext.IsMe(Owner) ||
            RunManager.Instance.NetService.Type == NetGameType.Replay)
        {
            return true;
        }

        if (SixSenseNetActions.RequestIncrease(
                Owner,
                attribute,
                canExceedMaximum))
        {
            return true;
        }

        return await ApplySenseIncrease(
            attribute,
            canExceedMaximum);
    }

    internal Task<bool> ApplySenseIncrease(
        SixSenseAttribute attribute,
        bool canExceedMaximum = false)
    {
        if (!canExceedMaximum && !CanIncreaseSense(attribute))
        {
            return Task.FromResult(false);
        }

        SetSenseAmount(attribute, GetSenseAmount(attribute) + 1, canExceedMaximum);
        Flash();
        return Task.FromResult(true);
    }

    // Compatibility entry points do not grant the removed legacy DP-loss bonuses.
    internal void RegisterMentalStrengthDpLoss(int amount) { }

    internal Task ResolveMentalStrengthDpLossRewards(
        PlayerChoiceContext choiceContext, CardModel cardSource)
    {
        if (IsMutable)
        {
            PendingMentalStrengthStatRewards = 0;
            MentalStrengthDpLossProgress = 0;
        }
        return Task.CompletedTask;
    }

    private void SetSenseAmount(
        SixSenseAttribute attribute,
        int amount,
        bool canExceedMaximum = false)
    {
        int clampedAmount = canExceedMaximum
            ? Math.Max(0, amount)
            : Math.Clamp(amount, 0, MaxSixSense);
        switch (attribute)
        {
            case SixSenseAttribute.LeadershipCharm:
                LeadershipCharm = clampedAmount;
                break;
            case SixSenseAttribute.Talent:
                Talent = clampedAmount;
                break;
            case SixSenseAttribute.Gentleness:
                Gentleness = clampedAmount;
                break;
            case SixSenseAttribute.MentalStrength:
                MentalStrength = clampedAmount;
                break;
            case SixSenseAttribute.Simplicity:
                Simplicity = clampedAmount;
                break;
            case SixSenseAttribute.Madness:
                Madness = clampedAmount;
                break;
        }
        // A direct level reward can use space previously covered by saved
        // points. Keep only enough unallocated points to reach all six caps.
        _sixSensePoints = SixSensePoints;
        SynchronizeMemoryGardenUpgradePoints();
    }

    private int GetPendingRewardCount(SixSenseAttribute attribute)
    {
        return attribute switch
        {
            SixSenseAttribute.LeadershipCharm => PendingLeadershipCharmRewards,
            SixSenseAttribute.Talent => PendingTalentRewards,
            SixSenseAttribute.Gentleness => PendingGentlenessRewards,
            SixSenseAttribute.MentalStrength => PendingMentalStrengthRewards,
            SixSenseAttribute.Simplicity => PendingSimplicityRewards,
            SixSenseAttribute.Madness => PendingMadnessRewards,
            _ => 0
        };
    }

    private void SetPendingRewardCount(SixSenseAttribute attribute, int amount)
    {
        int clampedAmount = Math.Max(0, amount);
        switch (attribute)
        {
            case SixSenseAttribute.LeadershipCharm:
                PendingLeadershipCharmRewards = clampedAmount;
                break;
            case SixSenseAttribute.Talent:
                PendingTalentRewards = clampedAmount;
                break;
            case SixSenseAttribute.Gentleness:
                PendingGentlenessRewards = clampedAmount;
                break;
            case SixSenseAttribute.MentalStrength:
                PendingMentalStrengthRewards = clampedAmount;
                break;
            case SixSenseAttribute.Simplicity:
                PendingSimplicityRewards = clampedAmount;
                break;
            case SixSenseAttribute.Madness:
                PendingMadnessRewards = clampedAmount;
                break;
        }
    }

    private void ClearPendingSixSenseRewards()
    {
        PendingLeadershipCharmRewards = 0;
        PendingTalentRewards = 0;
        PendingGentlenessRewards = 0;
        PendingMentalStrengthRewards = 0;
        PendingSimplicityRewards = 0;
        PendingMadnessRewards = 0;
        PendingSixSenseRewards = 0;
        PendingAllSenseIncreases = 0;
    }

}
