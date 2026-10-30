using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class HelloWorldHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    public override int MaxUpgradeLevel => UpgradeEffectCount;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/hello_world_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<FragilePower>(3m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromPower<FragilePower>();
            if (HasUpgradeEffect(0) || HasUpgradeEffect(2))
                yield return HoverTipFactory.FromPower<BreakPower>();
            if (HasUpgradeEffect(0) || HasUpgradeEffect(3))
            {
                yield return HoverTipFactory.FromPower<DestructionRatePower>();
                yield return HoverTipFactory.FromPower<DestructionRateCapIncreasePower>();
            }
            if (HasUpgradeEffect(4))
            {
                yield return HoverTipFactory.Static(StaticHoverTip.Block);
                yield return HoverTipFactory.FromPower<HotaruDamageAmplificationPower>();
            }
            if (HasUpgradeEffect(6)) yield return HoverTipFactory.FromPower<MyLegendNextAttackPower>();
        }
    }

    public HelloWorldHbrgoSaki() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies) { }

    internal bool HasUpgradeEffect(int effectId) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(effectId);

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => !HasUpgradeEffect(id)).ToArray();

    internal bool PrepareUpgradeEffect(int effectId)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(effectId)) return false;
        _preparedUpgradeEffect = effectId;
        return true;
    }

    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        // The native energy cost already displays the discount.
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id != 5)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray();
        foreach (var enemy in enemies)
            await PowerCmd.Apply<FragilePower>(choiceContext, enemy,
                DynamicVars["FragilePower"].BaseValue, Owner.Creature, this);

        // Snapshot after the base debuff has resolved on every enemy.
        await HelloWorldCombatHelper.ApplyUpgradeEffects(choiceContext, this, enemies);
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= UpgradeEffectCount) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool IsEligible(int id) => id >= 0 && id < UpgradeEffectCount && !applied.Contains(id);
        int effectId = index < UpgradeEffectOrder.Length && IsEligible(UpgradeEffectOrder[index])
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int prepared && IsEligible(prepared)
                ? prepared : Enumerable.Range(0, UpgradeEffectCount).First(IsEligible);
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = effectId;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(effectId).ToArray();
        _preparedUpgradeEffect = null;
        if (effectId == 5) EnergyCost.UpgradeBy(-1);
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>())
            .Where(id => id >= 0 && id < UpgradeEffectCount).Distinct().Take(UpgradeEffectCount).ToArray();
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
    }
}
