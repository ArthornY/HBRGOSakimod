using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class LostStarSongHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;
    private bool _legacyUpgradeReplay;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasAutoPlayedThisCombat { get; private set; }

    public override int MaxUpgradeLevel => UpgradeEffectCount;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/lost_star_song_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<SingTogetherPower>(),
        HoverTipFactory.FromPower<PreparationPower>(),
        HoverTipFactory.FromPower<HighSongPower>()
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SingTogetherPower>(1m)
    };

    public LostStarSongHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => !HasUpgradeEffect(id)).ToArray();

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
        // The SingTogether variable already displays effect 2's extra stacks.
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id != 2)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    public override Task BeforeCombatStart()
    {
        HasAutoPlayedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterAutoPrePlayPhaseEnteredEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!HasUpgradeEffect(0) || player != Owner || HasAutoPlayedThisCombat ||
            player.PlayerCombatState?.TurnNumber != 1 ||
            Pile == null || Pile.Type is not (PileType.Draw or PileType.Hand or PileType.Discard)) return;
        // Mark before awaiting: card movement and generated copies can re-enter
        // hooks. The native first-turn phase never runs for later generated cards.
        HasAutoPlayedThisCombat = true;
        await CardCmd.AutoPlay(choiceContext, this, null);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await LostStarSongSupportPower.Enable(choiceContext, Owner, this,
            HasUpgradeEffect(3), HasUpgradeEffect(4), HasUpgradeEffect(5), HasUpgradeEffect(6));
        await PowerCmd.Apply<SingTogetherPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SingTogetherPower"].BaseValue,
            Owner.Creature,
            this);

        if (HasUpgradeEffect(1)) await PlayerCmd.GainEnergy(1m, Owner);
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index >= UpgradeEffectCount) return;

        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool IsEligible(int id) => id >= 0 && id < UpgradeEffectCount && !applied.Contains(id);
        int effectId;
        if (index < UpgradeEffectOrder.Length && IsEligible(UpgradeEffectOrder[index]))
            effectId = UpgradeEffectOrder[index];
        else if (_preparedUpgradeEffect is int prepared && IsEligible(prepared))
            effectId = prepared;
        else if (_legacyUpgradeReplay && index == 0)
            effectId = 0;
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
            case 2: DynamicVars["SingTogetherPower"].UpgradeValueBy(2m); break;
        }
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        _legacyUpgradeReplay = UpgradeEffectOrder == null || UpgradeEffectOrder.Length == 0;
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>())
            .Where(id => id >= 0 && id < UpgradeEffectCount).Distinct().Take(UpgradeEffectCount).ToArray();
        _preparedUpgradeEffect = null;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        UpgradeEffectOrder = (int[])UpgradeEffectOrder.Clone();
        _preparedUpgradeEffect = null;
        _legacyUpgradeReplay = false;
        HasAutoPlayedThisCombat = false;
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        UpgradeEffectOrder = Array.Empty<int>();
        _preparedUpgradeEffect = null;
        _legacyUpgradeReplay = false;
        if (!HbrMemoryGardenProgression.IsRebuilding) HasAutoPlayedThisCombat = false;
    }
}
