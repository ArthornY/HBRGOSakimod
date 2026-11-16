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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SaviorGrandEntranceHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 8;
    private int? _preparedUpgradeEffect;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    public override int MaxUpgradeLevel => 7;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/savior_grand_entrance_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<SaviorGrandEntrancePower>(),
        HoverTipFactory.FromPower<HbrDefenseDownPower>(),
        HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<BreakPower>()
    };

    public SaviorGrandEntranceHbrgoSaki()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    internal bool HasUpgradeEffect(int id) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => !HasUpgradeEffect(id)).ToArray();
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _preparedUpgradeEffect = id;
        return true;
    }
    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel).Where(id => id != 2)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString())).ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        SaviorGrandEntrancePower power = await PowerCmd.Apply<SaviorGrandEntrancePower>(
            context, Owner.Creature, 1m, Owner.Creature, this);
        power?.RegisterCardSource(this);
        if (HasUpgradeEffect(6) && HbrBattleStatsHelper.IsEnabled(Owner.Creature))
        {
            var profile = new HbrEffectProfile(HbrEffectLimit.None, HbrEffectExit.Eternal, 1);
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
            {
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m,
                    Owner.Creature, this, profile);
                await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, enemy, 20m,
                    Owner.Creature, this, profile);
            }
        }
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= MaxUpgradeLevel) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool Eligible(int id) => id >= 0 && id < UpgradeEffectCount && !applied.Contains(id);
        int id = index < UpgradeEffectOrder.Length && Eligible(UpgradeEffectOrder[index])
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int selected && Eligible(selected)
                ? selected : Enumerable.Range(0, UpgradeEffectCount).First(Eligible);
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = id;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(id).ToArray();
        _preparedUpgradeEffect = null;
        if (id == 2) EnergyCost.UpgradeBy(-1);
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>()).Where(id => id >= 0 && id < UpgradeEffectCount)
            .Distinct().Take(MaxUpgradeLevel).ToArray();
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
    }
}
