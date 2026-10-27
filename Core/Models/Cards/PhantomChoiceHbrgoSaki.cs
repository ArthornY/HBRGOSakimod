using System.Collections.Generic;
using System.Globalization;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PhantomChoiceHbrgoSaki : CardModel
{
    private string _descriptionOverride = string.Empty;

    [SavedProperty]
    public int RosterEntryId { get; private set; }

    [SavedProperty]
    public ModelId MonsterId { get; private set; } = ModelId.none;

    [SavedProperty]
    public int PhantomUpgradeLevel { get; private set; }

    [SavedProperty]
    public int PhantomExperience { get; private set; }

    [SavedProperty]
    public int PhantomExperienceValue { get; private set; } = 1;

    [SavedProperty]
    public decimal PhantomInheritedLevelDamageBonusPercent { get; private set; }

    [SavedProperty]
    public decimal PhantomInheritedLevelDamageReductionPercent { get; private set; }

    [SavedProperty]
    public decimal PhantomInheritedLevelBonusMaxHp { get; private set; }

    [SavedProperty]
    public decimal PhantomInheritedLevelSummonerRollBonus { get; private set; }

    [SavedProperty]
    public string PhantomFusionEffectData { get; private set; } = string.Empty;

    internal PhantomRosterEntry RosterEntry => new(
        RosterEntryId,
        MonsterId,
        PhantomUpgradeLevel,
        PhantomExperience,
        PhantomExperienceValue,
        PhantomInheritedLevelDamageBonusPercent,
        PhantomInheritedLevelDamageReductionPercent,
        PhantomInheritedLevelBonusMaxHp,
        PhantomFusionEffectData,
        PhantomInheritedLevelSummonerRollBonus);

    internal decimal PhantomLevel => GetCurrentPhantom()?.GetPower<PhantomTemporaryLevelPower>()?
        .GetEffectiveLevel(RosterEntry.Level) ?? RosterEntry.Level;

    internal string LevelDescription => PhantomLevel == RosterEntry.Level
        ? $"Lv.{PhantomLevel}"
        : $"Lv.{PhantomLevel}（本场战斗；基础Lv.{RosterEntry.Level}）";

    private Creature GetCurrentPhantom() =>
        RosterEntryId > 0 && Owner?.GetRelic<MomentOfReign>()?.ActiveRosterEntryId == RosterEntryId
            ? PhantomHelper.GetLivingPhantom(Owner)
            : null;

    public override string Title =>
        $"{GetMonsterTitle()} Lv.{PhantomLevel}" +
        (PhantomUpgradeLevel > 0
            ? $" +{PhantomUpgradeLevel}"
            : string.Empty);

    public override string PortraitPath => PhantomPortraitHelper.FallbackPath;

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public PhantomChoiceHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    internal void SetRosterEntry(PhantomRosterEntry entry)
    {
        AssertMutable();
        RosterEntryId = entry.InstanceId;
        MonsterId = entry.MonsterId;
        PhantomUpgradeLevel = entry.UpgradeLevel;
        PhantomExperience = entry.Experience;
        PhantomExperienceValue = entry.ExperienceValue;
        PhantomInheritedLevelDamageBonusPercent =
            entry.InheritedLevelDamageBonusPercent;
        PhantomInheritedLevelDamageReductionPercent =
            entry.InheritedLevelDamageReductionPercent;
        PhantomInheritedLevelBonusMaxHp =
            entry.InheritedLevelBonusMaxHp;
        PhantomInheritedLevelSummonerRollBonus =
            entry.InheritedLevelSummonerRollBonus;
        PhantomFusionEffectData = entry.FusionEffectData;
    }

    internal void SetDescriptionOverride(string description)
    {
        AssertMutable();
        _descriptionOverride = description ?? string.Empty;
    }

    internal PhantomRosterEntry ToRosterEntry() => RosterEntry;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "CardDescription",
            string.IsNullOrEmpty(_descriptionOverride)
                ? GetDefaultDescriptionText()
                : _descriptionOverride);
        description.Add("MonsterName", GetMonsterTitle());
        description.Add(
            "UpgradeStrength",
            PhantomUpgradeLevel);
        description.Add(
            "UpgradeEffect",
            GetUpgradeEffectText());
    }

    private string GetDefaultDescriptionText()
    {
        return $"召唤[gold]{GetMonsterTitle()}[/gold]作为自己的幻影。" +
            GetUpgradeEffectText();
    }

    internal string GetBaseStatsRichText(
        ElectronicSoldierManual manual)
    {
        MonsterModel monster =
            ModelDb.GetByIdOrNull<MonsterModel>(MonsterId);
        if (monster == null)
        {
            return "基础属性不可用";
        }

        PhantomRosterEntry entry = RosterEntry;
        Creature currentPhantom = GetCurrentPhantom();
        decimal summonedMinHp = entry.GetBasePhantomHp(monster.MinInitialHp, owner: Owner);
        decimal summonedMaxHp = entry.GetBasePhantomHp(monster.MaxInitialHp, owner: Owner);
        decimal appliedBonusHp = System.Math.Min(
            BigCombatNumbers.MaximumCreatureStat - summonedMaxHp,
            decimal.Floor(System.Math.Max(0m, entry.BonusMaxHp)));
        decimal finalMinHp = summonedMinHp + appliedBonusHp;
        decimal finalMaxHp = summonedMaxHp + appliedBonusHp;
        if (Owner?.GetRelic<MomentOfReign>() is { } reign)
        {
            finalMinHp = reign.GetAdjustedPhantomMaxHp(finalMinHp);
            finalMaxHp = reign.GetAdjustedPhantomMaxHp(finalMaxHp);
        }
        string finalHp = finalMinHp == finalMaxHp
            ? finalMinHp.ToString()
            : $"{finalMinHp}-{finalMaxHp}";
        if (currentPhantom != null)
        {
            finalMinHp = finalMaxHp = LargeCreatureHealthHelper.GetMaxHp(currentPhantom);
            finalHp = $"{LargeCreatureHealthHelper.GetCurrentHp(currentPhantom)}/{LargeCreatureHealthHelper.GetMaxHp(currentPhantom)}";
        }
        else if (Owner?.GetRelic<MomentOfReign>()?.GetStoredPhantomHp(RosterEntryId) is { } remainingHp)
        {
            finalHp = $"{System.Math.Min(remainingHp, finalMaxHp)}/{finalHp}";
        }
        decimal shellMin = HighDimensionalShellPower
            .CalculatePlayerAmount(finalMinHp);
        decimal shellMax = HighDimensionalShellPower
            .CalculatePlayerAmount(finalMaxHp);
        decimal extraMaxDp = BigCombatNumbers.ClampStat(
            entry.ExtraMaxDp);
        shellMin = BigCombatNumbers.ClampStat(shellMin + extraMaxDp);
        shellMax = BigCombatNumbers.ClampStat(shellMax + extraMaxDp);
        string shell = shellMin == shellMax
            ? shellMin.ToString()
            : $"{shellMin}-{shellMax}";
        decimal damageBonus = currentPhantom != null
            ? PhantomHelper.GetDamageBonusPercent(currentPhantom) : entry.DamageBonusPercent;
        decimal damageReduction = currentPhantom != null
            ? PhantomHelper.GetDamageReductionPercent(currentPhantom) : entry.DamageReductionPercent;
        decimal rollBonus = entry.SummonerRollBonus +
            (currentPhantom?.GetPower<PhantomTemporaryLevelPower>()?.GetSummonerRollBonus() ?? 0m);
        return $"等级：{LevelDescription}\n" +
            $"生命值：{finalHp}\n" +
            $"伤害增幅：{FormatSignedNumber(damageBonus)}%\n" +
            $"伤害增幅倍率：{FormatNumber(entry.DamageBonusMultiplier * 100m)}%（最终伤害增幅乘以此倍率）\n" +
            $"融合槽位：{PhantomFusionEffectHelper.GetEffectTypes(entry.FusionEffectData).Count}/{entry.FusionSlotCount}\n" +
            $"伤害减免：{FormatNumber(damageReduction)}%（上限50%）\n" +
            $"召唤者掷骰出目：+{FormatNumber(decimal.Floor(rollBonus))}（在场时）\n" +
            $"融合素材经验：{entry.ExperienceValue}\n" +
            $"偏导护盾：{shell}";
    }

    private static string FormatGoldValue(string value) =>
        $"[color=#F2C15F]{value}[/color]";

    private string GetUpgradeEffectText()
    {
        PhantomRosterEntry entry = RosterEntry;
        List<string> effects = new();
        if (entry.EarnedLevelDamageBonusPercent > 0m ||
            entry.EarnedLevelDamageReductionPercent > 0m ||
            entry.EarnedLevelBonusMaxHp > 0m ||
            entry.SummonerRollBonus > 0m)
        {
            effects.Add(
                $"升级与继承：伤害增幅+{FormatNumber(entry.EarnedLevelDamageBonusPercent)}%，" +
                $"伤害减免+{FormatNumber(entry.EarnedLevelDamageReductionPercent)}%，" +
                $"最大生命值+{FormatNumber(entry.EarnedLevelBonusMaxHp)}，" +
                $"召唤者掷骰出目+{FormatNumber(entry.SummonerRollBonus)}。伤害减免上限50%。");
        }

        effects.AddRange(
            PhantomFusionEffectHelper.GetDescriptionLines(
                PhantomFusionEffectData));
        if (effects.Count == 0)
        {
            return string.Empty;
        }

        string heading = PhantomUpgradeLevel > 0
            ? $"融合 +{PhantomUpgradeLevel}"
            : "升级效果";
        return $"\n[gold]{heading}[/gold]：" +
            string.Join("\n", effects);
    }

    internal static string FormatNumber(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedNumber(decimal value)
    {
        return value > 0m
            ? $"+{FormatNumber(value)}"
            : FormatNumber(value);
    }

    private string GetMonsterTitle() =>
        ModelDb.GetByIdOrNull<MonsterModel>(MonsterId)?
            .Title.GetFormattedText() ?? base.Title;

    protected override System.Threading.Tasks.Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
    }
}
