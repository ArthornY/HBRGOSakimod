using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
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

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MatchaParfaitHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PendingFirstDpLossReturn { get; private set; }

    public override int MaxUpgradeLevel => UpgradeEffectCount;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/matcha_parfait_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedByModifiers => false;

    internal bool UsesThirtyAEffects => IsMutable && Owner != null
        ? Owner.Character is HbrgoSaki30A
        : HbrgoSakiCardPortraitContext.LibraryVariant == HbrgoSakiVariant.ThirtyA;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (HasUpgradeEffect(1)) yield return HoverTipFactory.FromPower<MatchaDeferredDpRecoveryPower>();
            if (HasUpgradeEffect(2)) yield return HoverTipFactory.Static(StaticHoverTip.Block);
            if (HasUpgradeEffect(3))
            {
                yield return HoverTipFactory.FromPower<StrengthPower>();
                yield return HoverTipFactory.FromPower<DexterityPower>();
            }
        }
    }

    public override bool GainsBlock => HasUpgradeEffect(2);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("DpPercent", 10m),
            new CardsVar(1)
        };

    public MatchaParfaitHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => !HasUpgradeEffect(id)).ToArray();

    internal bool HasUpgradeEffect(int effectId) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(effectId);

    internal bool PrepareUpgradeEffect(int effectId)
    {
        if (!IsMutable || !IsUpgradable || effectId < 0 || effectId >= UpgradeEffectCount ||
            HasUpgradeEffect(effectId)) return false;
        _preparedUpgradeEffect = effectId;
        return true;
    }

    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    internal void QueueFirstDpLossReturn()
    {
        AssertMutable();
        PendingFirstDpLossReturn = true;
    }

    internal bool ConsumeFirstDpLossReturn()
    {
        AssertMutable();
        bool pending = PendingFirstDpLossReturn;
        PendingFirstDpLossReturn = false;
        return pending;
    }

    public override Task BeforeCombatStart()
    {
        PendingFirstDpLossReturn = false;
        return Task.CompletedTask;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = UsesThirtyAEffects ? ".30a" : string.Empty;
        description.Add("RecoveryPrefix", new LocString("cards", Id.Entry + ".recovery_prefix" + suffix));
        description.Add("RecoveryFallback", new LocString("cards", Id.Entry + ".recovery_fallback" + suffix));
        description.Add("DrawPrefix", new LocString("cards", Id.Entry + ".draw_prefix" + suffix));
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id != 4)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await HbrMatchaDpHelper.Restore(choiceContext, Owner,
            DynamicVars["DpPercent"].BaseValue / 100m, HasUpgradeEffect(2), this);
        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);

        if (HasUpgradeEffect(0))
        {
            int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
            if (handCount > 0)
            {
                CardModel[] discarded = (await CardSelectCmd.FromHandForDiscard(
                        choiceContext, Owner,
                        new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, handCount),
                        null, this))
                    .Where(card => card.Owner == Owner && card.Pile?.Type == PileType.Hand)
                    .Distinct().ToArray();
                // Keep vanilla's discard/draw ordering, including Sly cards.
                await CardCmd.DiscardAndDraw(choiceContext, discarded, discarded.Length);
            }
        }

        if (HasUpgradeEffect(1))
            await MatchaDeferredDpRecoveryPower.Queue(choiceContext, Owner, HasUpgradeEffect(2), this);

        if (HasUpgradeEffect(3))
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        if (HasUpgradeEffect(5)) await PlayerCmd.GainEnergy(1m, Owner);
    }

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index >= UpgradeEffectCount) return;

        int effectId;
        if (index < UpgradeEffectOrder.Length)
        {
            effectId = UpgradeEffectOrder[index];
        }
        else
        {
            // Native upgrades and old saves without chosen effects remain deterministic.
            effectId = _preparedUpgradeEffect is int prepared && !UpgradeEffectOrder.Contains(prepared)
                ? prepared
                : Enumerable.Range(0, UpgradeEffectCount).First(id => !UpgradeEffectOrder.Contains(id));
            UpgradeEffectOrder = UpgradeEffectOrder.Append(effectId).ToArray();
        }
        _preparedUpgradeEffect = null;
        if (effectId == 4) EnergyCost.UpgradeBy(-1);
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
        PendingFirstDpLossReturn = false;
        _preparedUpgradeEffect = null;
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        UpgradeEffectOrder = Array.Empty<int>();
        if (!HbrMemoryGardenProgression.IsRebuilding) PendingFirstDpLossReturn = false;
        _preparedUpgradeEffect = null;
    }
}
