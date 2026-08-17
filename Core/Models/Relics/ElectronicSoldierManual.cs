using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Rewards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

public sealed class ElectronicSoldierManual : RelicModel
{
    public const int MaxSixSense = 20;

    public const decimal TalentGoldMultiplierPerLayer = 0.05m;

    public const decimal MentalStrengthDamageReductionPerLayer = 0.01m;

    public const decimal NonSakiSimplicityMaxHpPerLayer = 2m;

    public const decimal SimplicityDpRecoveryMultiplierPerLayer = 0.04m;

    public const decimal MadnessDamageTakenMultiplierPerLayer = 0.01m;

    public const decimal MadnessDamageDealtMultiplierPerLayer = 0.02m;

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
    public int LeadershipCharm { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Talent { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Gentleness { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MentalStrength { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Simplicity { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Madness { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingLeadershipCharmRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingTalentRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingGentlenessRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingMentalStrengthRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingSimplicityRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingMadnessRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingSixSenseRewards { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int JoiningCeremonyRollCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool JoiningCeremonyTriggered { get; private set; }

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NormalStatePower>()
    };

    public override async Task BeforeCombatStart()
    {
        ClearPendingSixSenseRewards();
        Flash();
        if (Owner.Character is HbrgoSaki)
        {
            await PowerCmd.Apply<NormalStatePower>(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                1m,
                Owner.Creature,
                null);
        }

        await ApplyEntrySenseBonuses(
            GetAliveAllies(Owner.Creature.CombatState),
            flash: false);
    }

    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (Owner.Character is not HbrgoSaki ||
            Owner.RunState.CurrentActIndex != 0 ||
            JoiningCeremonyTriggered ||
            Owner.RunState.Players.FirstOrDefault(player =>
                player.Character is HbrgoSaki) != Owner)
        {
            return currentEvent;
        }

        EventModel joiningCeremony =
            ModelDb.Event<JoiningCeremonyHbrgoSaki>();
        if (currentEvent.Id == joiningCeremony.Id)
        {
            return currentEvent;
        }

        float chance = JoiningCeremonyRollCount == 0 ? 0.8f : 0.5f;
        JoiningCeremonyRollCount++;
        if (Owner.RunState.Rng.Niche.NextFloat() >= chance)
        {
            return currentEvent;
        }

        JoiningCeremonyTriggered = true;
        return joiningCeremony;
    }

    public override Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.CombatState != Owner.Creature.CombatState ||
            creature.Side != Owner.Creature.Side ||
            !creature.IsAlive)
        {
            return Task.CompletedTask;
        }

        return ApplyEntrySenseBonuses(
            new[] { creature },
            flash: true);
    }

    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom room)
    {
        if (player != Owner || room is not CombatRoom)
        {
            return false;
        }

        bool modified = false;
        if (HasAvailableSixSenseIncrease() &&
            !rewards.OfType<SixSenseReward>().Any(reward => reward.IsDefaultReward))
        {
            rewards.Add(new SixSenseReward(player, this));
            modified = true;
        }

        foreach (SixSenseAttribute attribute in AllSixSenseAttributes)
        {
            int pendingCount = GetPendingRewardCount(attribute);
            int availableCount = Math.Max(0, MaxSixSense - GetSenseAmount(attribute));
            int rewardCount = Math.Min(pendingCount, availableCount);
            for (int i = 0; i < rewardCount; i++)
            {
                rewards.Add(new SixSenseReward(player, this, attribute));
                modified = true;
            }
        }

        int remainingIncreaseCount = Math.Max(
            0,
            GetAvailableSixSenseIncreaseCount() -
            rewards.OfType<SixSenseReward>().Count());
        int genericRewardCount = Math.Min(
            PendingSixSenseRewards,
            remainingIncreaseCount);
        for (int i = 0; i < genericRewardCount; i++)
        {
            rewards.Add(new SixSenseReward(player, this));
            modified = true;
        }

        ClearPendingSixSenseRewards();
        return modified;
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

    public IReadOnlyList<SixSenseAttribute> RollSixSenseRewardOptions()
    {
        List<SixSenseAttribute> options = AllSixSenseAttributes
            .Where(CanIncreaseSense)
            .ToList();

        Owner.PlayerRng.Rewards.Shuffle(options);
        return options.Take(3).ToList();
    }

    public async Task<bool> ChooseAndIncreaseSense(IReadOnlyList<SixSenseAttribute> options)
    {
        if (options.Count == 0)
        {
            return false;
        }

        SixSenseAttribute? selectedAttribute = await SixSenseChoiceHelper.Choose(Owner, this, options);
        if (!selectedAttribute.HasValue)
        {
            return false;
        }

        return await TryIncreaseSense(selectedAttribute.Value);
    }

    public override decimal ModifyGoldGained(Player player, decimal amount)
    {
        decimal effectiveTalent = GetEffectiveSenseAmount(SixSenseAttribute.Talent);
        if (player != Owner || effectiveTalent <= 0m)
        {
            return amount;
        }

        return amount * (1m + effectiveTalent * TalentGoldMultiplierPerLayer);
    }

    public override Task AfterModifyingGoldGained(Player player, decimal amount)
    {
        if (player == Owner && Talent > 0)
        {
            Flash();
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        decimal effectiveMadness = GetEffectiveSenseAmount(SixSenseAttribute.Madness);
        decimal effectiveMentalStrength =
            GetEffectiveSenseAmount(SixSenseAttribute.MentalStrength);

        decimal multiplierOffset = 0m;
        if (dealer == Owner.Creature)
        {
            multiplierOffset +=
                effectiveMadness * MadnessDamageDealtMultiplierPerLayer;
        }

        if (target == Owner.Creature)
        {
            multiplierOffset +=
                effectiveMadness * MadnessDamageTakenMultiplierPerLayer;
            multiplierOffset -=
                effectiveMentalStrength *
                MentalStrengthDamageReductionPerLayer;
        }

        return Math.Max(0m, 1m + multiplierOffset);
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

    public decimal GetEffectiveSenseAmount(SixSenseAttribute attribute)
    {
        decimal multiplier = Owner?.Creature?.GetPower<AllRounderPower>()?.SenseMultiplier ?? 1m;
        multiplier *= Owner?.Creature?.GetPower<LoliFormPower>()?.GetSenseMultiplier(attribute) ?? 1m;
        return GetSenseAmount(attribute) * multiplier;
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

        PendingSixSenseRewards += amount;
        Flash();
    }

    public static decimal GetSimplicityDpRecoveryMultiplier(Player player)
    {
        ElectronicSoldierManual manual = player?.GetRelic<ElectronicSoldierManual>();
        return manual == null || player.Character is not HbrgoSaki
            ? 1m
            : 1m + manual.GetEffectiveSenseAmount(SixSenseAttribute.Simplicity) *
                SimplicityDpRecoveryMultiplierPerLayer;
    }

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
        return attribute switch
        {
            SixSenseAttribute.LeadershipCharm => "每2点使我方单位入场时获得1点力量。",
            SixSenseAttribute.Talent => "获得金钱增加5%*层数。",
            SixSenseAttribute.Gentleness => "每2点使我方单位入场时获得1点敏捷。",
            SixSenseAttribute.MentalStrength => "每层使受到的伤害减少1%。",
            SixSenseAttribute.Simplicity => "每层使DP恢复增加4%。",
            SixSenseAttribute.Madness => "每层使受到伤害+1%，造成伤害+2%。",
            _ => string.Empty
        };
    }

    public string GetSenseShortDescriptionForOwner(SixSenseAttribute attribute)
    {
        if (attribute == SixSenseAttribute.Simplicity &&
            Owner.Character is not HbrgoSaki)
        {
            return "每层使生命上限增加2。";
        }

        return GetSenseShortDescription(attribute);
    }

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

    public async Task<bool> TryIncreaseSense(SixSenseAttribute attribute)
    {
        if (!CanIncreaseSense(attribute))
        {
            return false;
        }

        if (!LocalContext.IsMe(Owner) ||
            RunManager.Instance.NetService.Type == NetGameType.Replay)
        {
            return true;
        }

        if (SixSenseNetActions.RequestIncrease(Owner, attribute))
        {
            return true;
        }

        return await ApplySenseIncrease(attribute);
    }

    internal async Task<bool> ApplySenseIncrease(
        SixSenseAttribute attribute)
    {
        if (!CanIncreaseSense(attribute))
        {
            return false;
        }

        SetSenseAmount(attribute, GetSenseAmount(attribute) + 1);
        Flash();
        if (attribute == SixSenseAttribute.Simplicity &&
            Owner.Character is not HbrgoSaki)
        {
            await CreatureCmd.GainMaxHp(
                Owner.Creature,
                NonSakiSimplicityMaxHpPerLayer);
        }

        return true;
    }

    private void SetSenseAmount(SixSenseAttribute attribute, int amount)
    {
        int clampedAmount = Math.Clamp(amount, 0, MaxSixSense);
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
    }

    private async Task ApplyEntrySenseBonuses(
        IEnumerable<Creature> targets,
        bool flash)
    {
        int strengthAmount = (int)(
            GetEffectiveSenseAmount(SixSenseAttribute.LeadershipCharm) / 2m);
        int dexterityAmount = (int)(
            GetEffectiveSenseAmount(SixSenseAttribute.Gentleness) / 2m);
        if (strengthAmount <= 0 &&
            dexterityAmount <= 0)
        {
            return;
        }

        List<Creature> validTargets = targets
            .Where(creature =>
                creature.CombatState == Owner.Creature.CombatState &&
                creature.Side == Owner.Creature.Side &&
                creature.IsAlive)
            .ToList();
        if (validTargets.Count == 0)
        {
            return;
        }

        if (flash)
        {
            Flash(validTargets);
        }

        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        if (strengthAmount > 0)
        {
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                choiceContext,
                validTargets,
                strengthAmount,
                Owner.Creature,
                null);
        }

        if (dexterityAmount > 0)
        {
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.DexterityPower>(
                choiceContext,
                validTargets,
                dexterityAmount,
                Owner.Creature,
                null);
        }
    }

    private IEnumerable<Creature> GetAliveAllies(ICombatState combatState)
    {
        if (combatState == null)
        {
            return Array.Empty<Creature>();
        }

        return combatState.Creatures.Where(creature => creature.Side == Owner.Creature.Side && creature.IsAlive);
    }
}
