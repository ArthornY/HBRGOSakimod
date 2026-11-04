using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FinalSorrowHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(500m, ValueProp.Move, divisor: 3m)
        };

    public FinalSorrowHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        ModelId monsterId = cardPlay.Target.Monster?.Id ?? default;
        bool canCapture = cardPlay.Target.Monster?
            .ShouldShowInCompendium == true &&
            !cardPlay.Target.HasPower<MinionPower>();
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        bool killed = attack.Results
            .SelectMany(results => results)
            .Any(result => result.WasTargetKilled);
        if (killed &&
            canCapture &&
            PhantomHelper.CountsAsFinalDefeat(cardPlay.Target))
        {
            Owner.GetRelic<MomentOfReign>()?
                .AddMonsterToRoster(monsterId);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(100m);
    }
}

public sealed class PuzzlePieceHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        Owner.GetRelic<MomentOfReign>()?.GetObtainedPhantomMonsterIds().Count > 0;

    public PuzzlePieceHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        MomentOfReign relic = Owner.GetRelic<MomentOfReign>();
        PhantomRosterEntry? selected = await ThirtyARosterSelection.SelectOne(
            choiceContext,
            this,
            relic?.GetPreviouslyObtainedPhantomChoices() ??
                Array.Empty<PhantomRosterEntry>());
        if (selected != null)
        {
            relic?.AddMonsterToRoster(selected.Value.MonsterId);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class AllThingsInNatureHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("StartingLevel", 1m)
        };

    public AllThingsInNatureHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Owner.GetRelic<MomentOfReign>()?.AddRandomCurrentRoomMonster(
            DynamicVars["StartingLevel"].IntValue >= 2);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StartingLevel"].UpgradeValueBy(1m);
    }
}

public sealed class PitchBlackAllureHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<IntangiblePower>(2m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<IntangiblePower>(),
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public PitchBlackAllureHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        foreach (Creature phantom in PhantomHelper
                     .GetLivingPhantoms(Owner)
                     .ToArray())
        {
            await PowerCmd.Apply<IntangiblePower>(
                choiceContext,
                phantom,
                DynamicVars["IntangiblePower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class OverflowingSorrowHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/overflowing_sorrow_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null &&
        Owner.GetRelic<MomentOfReign>()?.GetActiveRosterEntry() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("DamageBonusPerLevel", 3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public OverflowingSorrowHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        MomentOfReign relic = Owner.GetRelic<MomentOfReign>();
        PhantomRosterEntry? entry = relic?.GetActiveRosterEntry();
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (relic == null || entry == null || phantom == null)
        {
            return;
        }

        decimal level = phantom.GetPower<PhantomTemporaryLevelPower>()?
            .GetEffectiveLevel(entry.Value.Level) ?? entry.Value.Level;
        decimal damageBonus = level * DynamicVars["DamageBonusPerLevel"].BaseValue;
        await CreatureCmd.Kill(phantom, force: true);
        if (phantom.IsDead)
        {
            relic.AddPermanentDamageBonus(entry.Value.InstanceId, damageBonus);
            await relic.AdvancePhantomRevive();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamageBonusPerLevel"].UpgradeValueBy(1m);
    }
}

public sealed class OpenDestinyHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/open_destiny_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("RequiredLevels", 3m)
        };

    protected override bool IsPlayable
    {
        get
        {
            if (!IsMutable || Owner == null)
            {
                return false;
            }

            IReadOnlyList<PhantomRosterEntry> entries = Owner
                .GetRelic<MomentOfReign>()?
                .GetConsumableRosterEntries() ??
                Array.Empty<PhantomRosterEntry>();
            return ThirtyARosterSelection.CanReachMinimumLevelTotal(
                entries,
                DynamicVars["RequiredLevels"].IntValue);
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public OpenDestinyHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (!IsMutable || Owner == null)
        {
            return;
        }

        MomentOfReign relic = Owner.GetRelic<MomentOfReign>();
        if (relic == null)
        {
            return;
        }

        int requiredLevels = DynamicVars["RequiredLevels"].IntValue;
        IReadOnlyList<PhantomRosterEntry> entries =
            relic.GetConsumableRosterEntries();
        IReadOnlyList<int> selectedIds =
            await ThirtyARosterSelection.SelectAtLeastLevelTotal(
                choiceContext,
                this,
                entries,
                requiredLevels);
        if (selectedIds.Count == 0 || selectedIds.Distinct().Count() != selectedIds.Count)
        {
            return;
        }

        // Selection can pause; validate the current roster before consuming anything.
        PhantomRosterEntry[] selectedEntries = relic.GetConsumableRosterEntries()
            .Where(entry => selectedIds.Contains(entry.InstanceId))
            .ToArray();
        if (selectedEntries.Length != selectedIds.Count ||
            !ThirtyARosterSelection.CanReachMinimumLevelTotal(selectedEntries, requiredLevels) ||
            !await relic.ConsumeRosterEntries(choiceContext, selectedIds))
        {
            return;
        }

        relic.QueueCardRemovalReward();
    }

    protected override void OnUpgrade()
    {
        DynamicVars["RequiredLevels"].UpgradeValueBy(-1m);
    }
}

public sealed class ShadowOfDeathHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null &&
        Owner.GetRelic<MomentOfReign>()?.GetActiveRosterEntry() != null;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public ShadowOfDeathHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await Owner.GetRelic<MomentOfReign>()
            .ReplaceActivePhantomWithRedCrimson();
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

public sealed class KillKissHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null &&
        Owner.GetRelic<MomentOfReign>()?.GetActiveRosterEntry() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(200m, ValueProp.Move),
            new PowerVar<VulnerablePower>(3m),
            new PowerVar<FragilePower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<VulnerablePower>(),
            HoverTipFactory.FromPower<FragilePower>(),
            HoverTipFactory.FromPower<KillKissFusionPower>(),
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public KillKissHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Rare,
            TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        Creature target = cardPlay.Target;
        ModelId monsterId = target.Monster?.Id ?? ModelId.none;
        bool canFuse = target.Monster?.ShouldShowInCompendium == true &&
            !target.HasPower<MinionPower>();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (target.IsAlive)
        {
            await HbrPercentDebuffApplication.ApplyDefenseDown(
                choiceContext,
                target,
                DynamicVars["VulnerablePower"].BaseValue,
                Owner.Creature,
                this);
            await PowerCmd.Apply<FragilePower>(
                choiceContext,
                target,
                DynamicVars["FragilePower"].BaseValue,
                Owner.Creature,
                this);
        }

        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (!canFuse || phantom == null || monsterId == ModelId.none)
        {
            return;
        }

        KillKissFusionPower pending =
            (KillKissFusionPower)ModelDb
                .Power<KillKissFusionPower>()
                .ToMutable();
        pending.Configure(
            monsterId,
            IsUpgraded ? 2 : 1);
        await PowerCmd.Apply(
            choiceContext,
            pending,
            phantom,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class DeathToYouHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(500m, ValueProp.Move, divisor: 3m),
            new PowerVar<FallingContinuallyPower>(4m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FallingContinuallyPower>(),
            HoverTipFactory.FromPower<DoomPower>()
        };

    public DeathToYouHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Rare,
            TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        Creature target = cardPlay.Target;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (target.IsAlive)
        {
            await PowerCmd.Apply<FallingContinuallyPower>(
                choiceContext,
                target,
                FallingContinuallyHelper.ResolveAmount(
                    DynamicVars["FallingContinuallyPower"].BaseValue,
                    Owner.Creature, target),
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
        DynamicVars["FallingContinuallyPower"].UpgradeValueBy(2m);
    }
}

public sealed class GluttonyHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/gluttony_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null &&
        Owner.GetRelic<MomentOfReign>()?
            .GetConsumableRosterEntries().Count > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("FusionCount", 2m),
            new IntVar("ExperienceMultiplier", 1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public GluttonyHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        MomentOfReign relic = Owner.GetRelic<MomentOfReign>();
        PhantomRosterEntry? selected = await ThirtyARosterSelection.SelectOne(
            choiceContext,
            this,
            relic?.GetConsumableRosterEntries() ??
                Array.Empty<PhantomRosterEntry>());
        if (selected != null)
        {
            await relic.ConsumeRosterEntryAsRepeatedFusion(
                choiceContext,
                selected.Value.InstanceId,
                DynamicVars["FusionCount"].IntValue,
                DynamicVars["ExperienceMultiplier"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ExperienceMultiplier"].UpgradeValueBy(1m);
    }
}

internal static class ThirtyARosterSelection
{
    internal static async Task<PhantomRosterEntry?> SelectOne(
        PlayerChoiceContext choiceContext,
        CardModel source,
        IReadOnlyList<PhantomRosterEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return null;
        }

        List<PhantomChoiceHbrgoSaki> choices = entries
            .Select(entry =>
            {
                PhantomChoiceHbrgoSaki choice = source.CombatState
                    .CreateCard<PhantomChoiceHbrgoSaki>(source.Owner);
                choice.SetRosterEntry(entry);
                return choice;
            })
            .OrderByPhantomGrowth()
            .ToList();
        try
        {
            PhantomChoiceHbrgoSaki selected =
                (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    source.Owner,
                    new CardSelectorPrefs(
                        new LocString(
                            "cards",
                            source.Id.Entry +
                            ".selectionScreenPrompt"),
                        1)))
                .OfType<PhantomChoiceHbrgoSaki>()
                .FirstOrDefault();
            return selected?.RosterEntry;
        }
        finally
        {
            foreach (PhantomChoiceHbrgoSaki choice in choices)
            {
                choice.RemoveFromState();
            }
        }
    }

    internal static bool CanReachMinimumLevelTotal(
        IReadOnlyList<PhantomRosterEntry> entries,
        int target)
    {
        if (target <= 0 || entries == null || entries.Count == 0)
        {
            return false;
        }

        long total = 0;
        HashSet<int> countedIds = new();
        foreach (PhantomRosterEntry entry in entries)
        {
            if (entry.InstanceId <= 0 || !countedIds.Add(entry.InstanceId))
            {
                continue;
            }

            total += Math.Max(0, entry.Level);
            if (total >= target)
            {
                return true;
            }
        }

        return false;
    }

    internal static async Task<IReadOnlyList<int>>
        SelectAtLeastLevelTotal(
            PlayerChoiceContext choiceContext,
            CardModel source,
            IReadOnlyList<PhantomRosterEntry> entries,
            int target)
    {
        if (source?.IsMutable != true || source.Owner == null ||
            !CanReachMinimumLevelTotal(entries, target))
        {
            return Array.Empty<int>();
        }

        List<PhantomRosterEntry> available = entries
            .Where(entry => entry.InstanceId > 0 && entry.Level > 0)
            .GroupBy(entry => entry.InstanceId)
            .Select(group => group.First())
            .ToList();
        List<int> selectedIds = new();
        long remainingLevels = target;
        while (remainingLevels > 0)
        {
            PhantomRosterEntry? selected = await SelectOne(
                choiceContext,
                source,
                available);
            if (selected == null)
            {
                return Array.Empty<int>();
            }

            int index = available.FindIndex(entry => entry.InstanceId == selected.Value.InstanceId);
            if (index < 0)
            {
                return Array.Empty<int>();
            }

            PhantomRosterEntry chosen = available[index];
            selectedIds.Add(chosen.InstanceId);
            remainingLevels -= chosen.Level;
            available.RemoveAt(index);
        }

        return selectedIds;
    }
}
