using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MyLegendBeginsHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;
    // CostsX is immutable on CardEnergyCost; switching the upgrade's cost mode
    // must replace that object, including when its fixed cost was already read.
    private static readonly AccessTools.FieldRef<CardModel, CardEnergyCost> EnergyCostField =
        AccessTools.FieldRefAccess<CardModel, CardEnergyCost>("_energyCost");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    // The fixed-cost discount and X cost are mutually exclusive.
    public override int MaxUpgradeLevel => UpgradeEffectCount - 1;
    protected override bool HasEnergyCostX => HasUpgradeEffect(4);
    public override TargetType TargetType => HasUpgradeEffect(0) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/my_legend_begins_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(350m, ValueProp.Move, divisor: 3m), new PowerVar<VigorPower>(5m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (HasUpgradeEffect(1) || HasUpgradeEffect(2)) yield return HoverTipFactory.FromPower<VigorPower>();
            if (HasUpgradeEffect(2))
            {
                yield return HoverTipFactory.FromPower<MyLegendNextAttackPower>();
                yield return HoverTipFactory.FromPower<MyLegendNextAttackDoublePower>();
            }
            if (HasUpgradeEffect(6)) yield return HoverTipFactory.FromPower<DexterityPower>();
        }
    }

    public MyLegendBeginsHbrgoSaki() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    internal bool HasUpgradeEffect(int effectId) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(effectId);

    private static bool IsEligible(int effectId, IReadOnlyCollection<int> applied) =>
        effectId >= 0 && effectId < UpgradeEffectCount && !applied.Contains(effectId) &&
        !(effectId == 3 && applied.Contains(4)) && !(effectId == 4 && applied.Contains(3));

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() => Enumerable.Range(0, UpgradeEffectCount)
        .Where(id => IsEligible(id, UpgradeEffectOrder.Take(CurrentUpgradeLevel).ToArray())).ToArray();

    internal bool PrepareUpgradeEffect(int effectId)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(effectId)) return false;
        _preparedUpgradeEffect = effectId;
        return true;
    }

    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    internal static int CalculateHitCount(int baseHits, int dexterity, bool scalesWithDexterity, bool doublesHits)
    {
        long hits = Math.Max(0, baseHits);
        if (scalesWithDexterity) hits += Math.Max(0, dexterity) / 2;
        if (doublesHits) hits *= 2;
        return (int)Math.Min(int.MaxValue, hits);
    }

    private int CurrentDexterity => IsMutable ? Owner?.Creature?.GetPowerAmount<DexterityPower>() ?? 0 : 0;

    internal int GetHitCount(int resolvedXValue) => CalculateHitCount(
        HasUpgradeEffect(4) ? resolvedXValue : 2, CurrentDexterity, HasUpgradeEffect(6), HasUpgradeEffect(5));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("LegendTarget", HasUpgradeEffect(0) ? "所有敌人" : "目标");
        int dexterityHits = CalculateHitCount(0, CurrentDexterity, HasUpgradeEffect(6), HasUpgradeEffect(5));
        string hits = HasUpgradeEffect(4)
            ? (HasUpgradeEffect(5) ? "2X" : "X") + (dexterityHits > 0 ? "+" + dexterityHits : string.Empty)
            : GetHitCount(0).ToString();
        description.Add("LegendHits", hits);
        // Cost, target and total hit count are already reflected by their native
        // fields/body. Keep only the additional effects in the appended text.
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id is 1 or 2 or 6)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool hadVigorBeforeAttack = Owner.Creature.GetPowerAmount<VigorPower>() > 0;
        bool allEnemies = HasUpgradeEffect(0);
        if (!allEnemies) ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        int resolvedX = 0;
        if (HasUpgradeEffect(4))
        {
            EnergyCost.CapturedXValue = DualSwordsCopyHelper.TakeXEnergySpent(this, EnergyCost.CapturedXValue);
            resolvedX = ResolveEnergyXValue();
        }
        int hits = GetHitCount(resolvedX);
        if (hits > 0)
        {
            var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay)
                .WithHitCount(hits).WithHitFx("vfx/vfx_attack_slash");
            if (allEnemies) attack.TargetingAllOpponents(CombatState);
            else attack.Targeting(cardPlay.Target);
            await attack.Execute(choiceContext);
        }

        // The attack resolves before granting these buffs. This play cannot
        // consume its own next-attack effect, and its Vigor grant qualifies the
        // source for the doubled version when both upgrades are owned.
        var allies = CombatState.GetTeammatesOf(Owner.Creature).Where(creature => creature.IsAlive).ToArray();
        if (HasUpgradeEffect(1))
            foreach (var ally in allies)
                await PowerCmd.Apply<VigorPower>(choiceContext, ally, DynamicVars["VigorPower"].BaseValue,
                    Owner.Creature, this);
        if (HasUpgradeEffect(2))
        {
            bool doubleHits = hadVigorBeforeAttack || Owner.Creature.GetPowerAmount<VigorPower>() > 0;
            foreach (var ally in allies)
                await LegendNextAttackHelper.Grant(choiceContext, ally, Owner.Creature, this, doubleHits);
        }
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= MaxUpgradeLevel) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        int effectId = index < UpgradeEffectOrder.Length && IsEligible(UpgradeEffectOrder[index], applied)
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int prepared && IsEligible(prepared, applied)
                ? prepared : Enumerable.Range(0, UpgradeEffectCount).First(id => IsEligible(id, applied));
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = effectId;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(effectId).ToArray();
        _preparedUpgradeEffect = null;
        if (effectId == 3) EnergyCost.UpgradeBy(-1);
        if (effectId == 4)
        {
            EnergyCostField(this) = new CardEnergyCost(this, 2, true);
            InvokeEnergyCostChanged();
        }
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        List<int> valid = new();
        foreach (int id in UpgradeEffectOrder ?? Array.Empty<int>())
            if (IsEligible(id, valid) && valid.Count < MaxUpgradeLevel) valid.Add(id);
        UpgradeEffectOrder = valid.ToArray();
        _preparedUpgradeEffect = null;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        UpgradeEffectOrder = (int[])UpgradeEffectOrder.Clone();
        _preparedUpgradeEffect = null;
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        UpgradeEffectOrder = Array.Empty<int>();
        _preparedUpgradeEffect = null;
        EnergyCostField(this) = new CardEnergyCost(this, 2, false);
        InvokeEnergyCostChanged();
    }
}
