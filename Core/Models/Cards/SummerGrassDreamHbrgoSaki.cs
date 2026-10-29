using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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

public sealed class SummerGrassDreamHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;
    private static readonly AccessTools.FieldRef<CardModel, CardEnergyCost> EnergyCostField =
        AccessTools.FieldRefAccess<CardModel, CardEnergyCost>("_energyCost");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    // Fixed-cost reduction and X cost cannot be chosen on the same card.
    public override int MaxUpgradeLevel => UpgradeEffectCount - 1;
    protected override bool HasEnergyCostX => HasUpgradeEffect(3);
    public override TargetType TargetType => HasUpgradeEffect(2) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/summer_grass_dream_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(150m, ValueProp.Move)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (HasUpgradeEffect(1)) yield return HoverTipFactory.FromPower<StrengthPower>();
            if (HasUpgradeEffect(2)) yield return HoverTipFactory.FromPower<FragilePower>();
            if (HasUpgradeEffect(6)) yield return HoverTipFactory.FromPower<SummerGrassGhostPathPower>();
        }
    }

    public SummerGrassDreamHbrgoSaki() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    internal bool HasUpgradeEffect(int effectId) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(effectId);

    private static bool IsEligible(int effectId, IReadOnlyCollection<int> applied) =>
        effectId >= 0 && effectId < UpgradeEffectCount && !applied.Contains(effectId) &&
        !(effectId == 0 && applied.Contains(3)) && !(effectId == 3 && applied.Contains(0));

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() => Enumerable.Range(0, UpgradeEffectCount)
        .Where(id => IsEligible(id, UpgradeEffectOrder.Take(CurrentUpgradeLevel).ToArray())).ToArray();

    internal bool PrepareUpgradeEffect(int effectId)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(effectId)) return false;
        _preparedUpgradeEffect = effectId;
        return true;
    }

    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState) =>
        SummerGrassDreamCombatHelper.BeforeHandDraw(this, player);

    internal int GetHitCount(int resolvedXValue) => (int)Math.Min(int.MaxValue,
        (HasUpgradeEffect(3) ? Math.Max(0, (long)resolvedXValue) : 1L) + (HasUpgradeEffect(2) ? 2L : 0L));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("DreamTarget", HasUpgradeEffect(2) ? "所有敌人" : "目标");
        description.Add("DreamHits", HasUpgradeEffect(3)
            ? (HasUpgradeEffect(2) ? "X+2" : "X") : GetHitCount(0).ToString());
        description.Add("HammerEffect", !HasUpgradeEffect(2) ? string.Empty :
            HbrSkillCardDescription.IsEnabled(this)
                ? $"\n给予所有敌人{HbrFragileCardDescription.Format(this, 2m)}%的[gold]脆弱[/gold]（2回合）。"
                : "\n给予所有敌人2层[gold]脆弱[/gold]。");
        // Native cost and the main sentence already show the cost/target/hit upgrades.
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id is 1 or 4 or 5 or 6)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool startedAtZeroDp = Owner.GetRelic<DeflectorProtection>()?.Dp <= 0;
        bool allEnemies = HasUpgradeEffect(2);
        if (!allEnemies) ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        var targets = allEnemies
            ? CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray()
            : new[] { cardPlay.Target };

        // Replay shares the original payment record. Refund only the first play;
        // copied X values affect hit count, never the amount of energy returned.
        int actualEnergySpent = cardPlay.IsFirstInSeries ? Math.Max(0, cardPlay.Resources.EnergySpent) : 0;
        int resolvedX = 0;
        if (HasUpgradeEffect(3))
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

        foreach (var target in targets.Where(target => target.IsAlive))
        {
            if (HasUpgradeEffect(1))
                await PowerCmd.Apply<StrengthPower>(choiceContext, target, -5m, Owner.Creature, this);
            if (HasUpgradeEffect(2))
                await PowerCmd.Apply<FragilePower>(choiceContext, target, 2m, Owner.Creature, this);
        }

        await SummerGrassDreamCombatHelper.AfterPlay(choiceContext, this, startedAtZeroDp, actualEnergySpent);
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
        if (effectId == 0) EnergyCost.UpgradeBy(-1);
        if (effectId == 3)
        {
            EnergyCostField(this) = new CardEnergyCost(this, 1, true);
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
        EnergyCostField(this) = new CardEnergyCost(this, 1, false);
        InvokeEnergyCostChanged();
    }
}
