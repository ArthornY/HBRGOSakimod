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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WorldsFinestHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override int MaxUpgradeLevel => UpgradeEffectCount;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/worlds_finest_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("Recovery", 10m), new PowerVar<MindsEyePower>(1m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<SpiritTalismanPower>(),
        HoverTipFactory.FromPower<MindsEyePower>()
    };

    public WorldsFinestHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    internal bool HasUpgradeEffect(int id) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => IsEligible(id, UpgradeEffectOrder.Take(CurrentUpgradeLevel).ToArray())).ToArray();
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _preparedUpgradeEffect = id;
        return true;
    }
    private static bool IsEligible(int id, IReadOnlyCollection<int> applied) =>
        id >= 0 && id < UpgradeEffectCount && !applied.Contains(id) &&
        (!(id is 1 or 2) || applied.Contains(id - 1));

    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel).Where(id => id != 3 && !(id == 0 && HasUpgradeEffect(1)) && !(id == 1 && HasUpgradeEffect(2)))
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString())).ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await WorldsFinestCombatHelper.RestoreDp(context, this, HbrResourceValueHelper.Fixed(Owner.Creature, DynamicVars["Recovery"].BaseValue));
        if (HasUpgradeEffect(6))
            await PowerCmd.Apply<MindsEyePower>(context, Owner.Creature,
                DynamicVars["MindsEyePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= UpgradeEffectCount) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool Eligible(int id) => IsEligible(id, applied);
        int id = index < UpgradeEffectOrder.Length && Eligible(UpgradeEffectOrder[index])
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int selected && Eligible(selected)
                ? selected : Enumerable.Range(0, UpgradeEffectCount).First(Eligible);
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = id;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(id).ToArray();
        _preparedUpgradeEffect = null;
        if (id == 3) EnergyCost.UpgradeBy(-1);
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>()).Where(id => id >= 0 && id < UpgradeEffectCount)
            .Distinct().Take(UpgradeEffectCount).ToArray();
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
