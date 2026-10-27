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

    public override int MaxUpgradeLevel => UpgradeEffectCount - 1;
    protected override bool HasEnergyCostX => HasUpgradeEffect(4);
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
            if (HasUpgradeEffect(1)) yield return HoverTipFactory.FromPower<MindsEyePower>();
            if (HasUpgradeEffect(3)) yield return HoverTipFactory.FromPower<DestructionRatePower>();
            if (HasUpgradeEffect(6)) yield return HoverTipFactory.FromPower<CrushYouLikeABugReplayPower>();
        }
    }

    public CrushYouLikeABugHbrgoSaki() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    internal bool HasUpgradeEffect(int id) => UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    private static bool IsEligible(int id, IReadOnlyCollection<int> applied) =>
        id >= 0 && id < UpgradeEffectCount && !applied.Contains(id) &&
        !(id == 0 && applied.Contains(4)) && !(id == 4 && applied.Contains(0));
    internal IReadOnlyList<int> GetAvailableUpgradeEffects() => Enumerable.Range(0, UpgradeEffectCount)
        .Where(id => IsEligible(id, UpgradeEffectOrder.Take(CurrentUpgradeLevel).ToArray())).ToArray();
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _preparedUpgradeEffect = id;
        return true;
    }
    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;
    internal int GetHitCount(int resolvedX) => (int)Math.Min(int.MaxValue,
        1L + (HasUpgradeEffect(4) ? Math.Max(0, (long)resolvedX) : 0L) + (HasUpgradeEffect(5) ? 2L : 0L));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("BugTarget", HasUpgradeEffect(2) ? "所有敌人" : "目标");
        description.Add("BugHits", HasUpgradeEffect(4) ? (HasUpgradeEffect(5) ? "X+3" : "X+1") : GetHitCount(0).ToString());
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel).Where(id => id is 1 or 3 or 5 or 6)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString())).ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool allEnemies = HasUpgradeEffect(2);
        if (!allEnemies) ArgumentNullException.ThrowIfNull(play.Target, nameof(play.Target));
        int resolvedX = 0;
        if (HasUpgradeEffect(4))
        {
            // Keep the inherited X for every free replay in this play series.
            // Refunds use real ResourceInfo payment, never this copy cache.
            EnergyCost.CapturedXValue = DualSwordsCopyHelper.PeekXEnergySpent(this, EnergyCost.CapturedXValue);
            resolvedX = ResolveEnergyXValue();
        }
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .WithHitCount(GetHitCount(resolvedX)).WithHitFx("vfx/vfx_attack_slash");
        if (allEnemies) attack.TargetingAllOpponents(CombatState);
        else attack.Targeting(play.Target);
        await attack.Execute(context);
        if (HasUpgradeEffect(1) && Owner.Creature.IsAlive)
            await PowerCmd.Apply<MindsEyePower>(context, Owner.Creature, 1m, Owner.Creature, this);
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
        if (id == 4)
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
