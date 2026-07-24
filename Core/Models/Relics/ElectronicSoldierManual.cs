using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Rewards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ElectronicSoldierManual : RelicModel
{
    public const int MaxSixSense = 20;

    public const decimal TalentGoldMultiplierPerLayer = 0.05m;

    public const decimal MentalStrengthMaxHpPerLayer = 2m;

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

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NormalStatePower>()
    };

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<NormalStatePower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null);

        int strengthAmount = LeadershipCharm / 2;
        if (strengthAmount > 0)
        {
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                GetAliveAllies(Owner.Creature.CombatState),
                strengthAmount,
                Owner.Creature,
                null);
        }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        int dodgeAmount = Gentleness / 2;
        if (dodgeAmount <= 0 || side != Owner.Creature.Side)
        {
            return;
        }

        List<Creature> targets = participants.Where(creature => creature.IsAlive).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<DodgePower>(choiceContext, targets, dodgeAmount, Owner.Creature, null);
    }

    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom room)
    {
        if (player != Owner || room is not CombatRoom || !HasAvailableSixSenseIncrease() || rewards.Any(reward => reward is SixSenseReward))
        {
            return false;
        }

        rewards.Add(new SixSenseReward(player, this));
        return true;
    }

    public bool HasAvailableSixSenseIncrease()
    {
        return AllSixSenseAttributes.Any(CanIncreaseSense);
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

        await IncreaseSense(selectedAttribute.Value);
        return true;
    }

    public override decimal ModifyGoldGained(Player player, decimal amount)
    {
        if (player != Owner || Talent <= 0)
        {
            return amount;
        }

        return amount * (1m + Talent * TalentGoldMultiplierPerLayer);
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
        if (Madness <= 0)
        {
            return 1m;
        }

        decimal multiplier = 1m;
        if (dealer == Owner.Creature)
        {
            multiplier *= 1m + Madness * MadnessDamageDealtMultiplierPerLayer;
        }

        if (target == Owner.Creature)
        {
            multiplier *= 1m + Madness * MadnessDamageTakenMultiplierPerLayer;
        }

        return multiplier;
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

    public static decimal GetSimplicityDpRecoveryMultiplier(Player player)
    {
        ElectronicSoldierManual manual = player?.GetRelic<ElectronicSoldierManual>();
        return manual == null ? 1m : 1m + manual.Simplicity * SimplicityDpRecoveryMultiplierPerLayer;
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
            SixSenseAttribute.LeadershipCharm => "每2点使我方开战获得1点力量。",
            SixSenseAttribute.Talent => "获得金钱增加5%*层数。",
            SixSenseAttribute.Gentleness => "每2点使我方每回合开始获得1层闪避。",
            SixSenseAttribute.MentalStrength => "每层使生命上限增加2。",
            SixSenseAttribute.Simplicity => "每层使DP恢复增加4%。",
            SixSenseAttribute.Madness => "每层使受到伤害+1%，造成伤害+2%。",
            _ => string.Empty
        };
    }

    private async Task IncreaseSense(SixSenseAttribute attribute)
    {
        if (!CanIncreaseSense(attribute))
        {
            return;
        }

        SetSenseAmount(attribute, GetSenseAmount(attribute) + 1);
        Flash();

        if (attribute == SixSenseAttribute.MentalStrength)
        {
            await CreatureCmd.GainMaxHp(Owner.Creature, MentalStrengthMaxHpPerLayer);
        }
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

    private IEnumerable<Creature> GetAliveAllies(ICombatState combatState)
    {
        if (combatState == null)
        {
            return Array.Empty<Creature>();
        }

        return combatState.Creatures.Where(creature => creature.Side == Owner.Creature.Side && creature.IsAlive);
    }
}
