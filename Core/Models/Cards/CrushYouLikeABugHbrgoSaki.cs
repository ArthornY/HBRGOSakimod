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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class CrushYouLikeABugHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;
    private static readonly AccessTools.FieldRef<CardModel, CardEnergyCost> EnergyCostField =
        AccessTools.FieldRefAccess<CardModel, CardEnergyCost>("_energyCost");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    public override int MaxUpgradeLevel => UpgradeEffectCount;
    public override TargetType TargetType => HasUpgradeEffect(2) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/crush_you_like_a_bug_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.Damage(150m, ValueProp.Move) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (HasUpgradeEffect(1))
            {
                yield return HoverTipFactory.FromPower<HbrCriticalRateUpPower>();
                yield return HoverTipFactory.FromPower<HbrCriticalDamageUpPower>();
            }
            yield return HoverTipFactory.FromPower<BreakPower>();
            if (HasUpgradeEffect(3)) yield return HoverTipFactory.FromPower<DestructionRatePower>();
        }
    }

    public CrushYouLikeABugHbrgoSaki() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    internal bool HasUpgradeEffect(int id) => UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    private static bool IsEligible(int id, IReadOnlyCollection<int> applied) =>
        id >= 0 && id < UpgradeEffectCount && !applied.Contains(id);
    internal IReadOnlyList<int> GetAvailableUpgradeEffects() => Enumerable.Range(0, UpgradeEffectCount)
        .Where(id => IsEligible(id, UpgradeEffectOrder.Take(CurrentUpgradeLevel).ToArray())).ToArray();
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _preparedUpgradeEffect = id;
        return true;
    }
    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;
    internal int GetHitCount(int resolvedX = 0) => 1 + (HasUpgradeEffect(5) ? 2 : 0);

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || !HasUpgradeEffect(6) || originalCost <= 0m ||
            Owner?.Creature?.CombatState?.GetOpponentsOf(Owner.Creature)
                .Any(enemy => enemy.IsAlive && enemy.HasPower<BreakPower>()) != true) return false;
        modifiedCost = Math.Max(0m, originalCost - 1m);
        return true;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("BugTarget", HasUpgradeEffect(2) ? "所有敌人" : "目标");
        description.Add("BugHits", GetHitCount().ToString());
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel).Where(id => id is 1 or 3 or 4 or 5 or 6)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString())).ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool allEnemies = HasUpgradeEffect(2);
        if (!allEnemies) ArgumentNullException.ThrowIfNull(play.Target, nameof(play.Target));
        if (HasUpgradeEffect(1) && HbrBattleStatsHelper.IsEnabled(Owner.Creature))
            foreach (var ally in TeamCombatHelper.GetLivingAllies(Owner.Creature))
                await ThirtyOneAMemoryUpgradeHelper.GrantCriticals(context, ally, Owner.Creature, this);
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .WithHitCount(GetHitCount()).WithHitFx("vfx/vfx_attack_slash");
        if (allEnemies) attack.TargetingAllOpponents(CombatState);
        else attack.Targeting(play.Target);
        await attack.Execute(context);
        await CrushYouLikeABugCombatHelper.FinishUpgradeEffects(context, this, play);
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= MaxUpgradeLevel) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        int id = index < UpgradeEffectOrder.Length && IsEligible(UpgradeEffectOrder[index], applied)
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int selected && IsEligible(selected, applied)
                ? selected : Enumerable.Range(0, UpgradeEffectCount).First(candidate => IsEligible(candidate, applied));
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = id;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(id).ToArray();
        _preparedUpgradeEffect = null;
        if (id == 0) EnergyCost.UpgradeBy(-1);

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
        UpgradeEffectOrder = UpgradeEffectOrder?.ToArray() ?? Array.Empty<int>();
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
