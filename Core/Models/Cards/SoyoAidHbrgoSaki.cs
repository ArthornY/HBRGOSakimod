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

public sealed class SoyoAidHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;
    private bool _legacyUpgradeReplay;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    public override int MaxUpgradeLevel => UpgradeEffectCount;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/soyo_aid_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NotOverYetPower>()
    };

    public SoyoAidHbrgoSaki()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount)
            .Where(id => !HasUpgradeEffect(id) && (id != 1 || HasUpgradeEffect(0))).ToArray();

    internal bool HasUpgradeEffect(int effectId) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(effectId);

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
        // Native cost/keyword displays cover IDs 2–4. The fifth-attack effect
        // supersedes the fourth-attack description, while both remain earned.
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => (id == 0 && !HasUpgradeEffect(1)) || id == 1 || id >= 5)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        NotOverYetPower power = await PowerCmd.Apply<NotOverYetPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        power?.EnableUpgrades(HasUpgradeEffect(0), HasUpgradeEffect(1),
            HasUpgradeEffect(5), HasUpgradeEffect(6));
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index >= UpgradeEffectCount) return;

        // Only effects already replayed can satisfy the fifth-attack prerequisite.
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool IsEligible(int id) => id >= 0 && id < UpgradeEffectCount && !applied.Contains(id) &&
                                   (id != 1 || applied.Contains(0));
        int effectId;
        if (index < UpgradeEffectOrder.Length && IsEligible(UpgradeEffectOrder[index]))
            effectId = UpgradeEffectOrder[index];
        else if (_preparedUpgradeEffect is int prepared && IsEligible(prepared))
            effectId = prepared;
        else if (_legacyUpgradeReplay && index == 0)
            effectId = 3;
        else
            effectId = Enumerable.Range(0, UpgradeEffectCount).First(IsEligible);

        if (index < UpgradeEffectOrder.Length)
            UpgradeEffectOrder[index] = effectId;
        else
            UpgradeEffectOrder = UpgradeEffectOrder.Append(effectId).ToArray();
        _preparedUpgradeEffect = null;
        _legacyUpgradeReplay = false;
        switch (effectId)
        {
            case 2: EnergyCost.UpgradeBy(-1); break;
            case 3: AddKeyword(CardKeyword.Innate); break;
            case 4: AddKeyword(CardKeyword.Retain); break;
        }
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        _legacyUpgradeReplay = UpgradeEffectOrder == null || UpgradeEffectOrder.Length == 0;
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>())
            .Where(id => id >= 0 && id < UpgradeEffectCount).Distinct().Take(UpgradeEffectCount).ToArray();
        int dependent = Array.IndexOf(UpgradeEffectOrder, 1);
        int prerequisite = Array.IndexOf(UpgradeEffectOrder, 0);
        if (dependent >= 0 && prerequisite >= 0 && dependent < prerequisite)
        {
            List<int> repaired = UpgradeEffectOrder.ToList();
            repaired.Remove(1);
            repaired.Insert(repaired.IndexOf(0) + 1, 1);
            UpgradeEffectOrder = repaired.ToArray();
        }
        else if (dependent >= 0 && prerequisite < 0)
        {
            UpgradeEffectOrder = UpgradeEffectOrder.Where(id => id != 1).ToArray();
        }
        _preparedUpgradeEffect = null;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        UpgradeEffectOrder = (int[])UpgradeEffectOrder.Clone();
        _preparedUpgradeEffect = null;
        _legacyUpgradeReplay = false;
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        UpgradeEffectOrder = Array.Empty<int>();
        _preparedUpgradeEffect = null;
        _legacyUpgradeReplay = false;
    }
}
