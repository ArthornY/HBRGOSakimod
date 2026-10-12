using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

public sealed class HerSongIsMyScreamHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;

    // The ordered choices are replayed by CardModel.FromSerializable as it
    // reapplies each saved upgrade. No random draw belongs in OnUpgrade.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasPlayedThisCombat { get; private set; }

    public override int MaxUpgradeLevel => UpgradeEffectCount;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/her_song_is_my_scream_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return HoverTipFactory.Static(StaticHoverTip.Block);
            if (HasUpgradeEffect(1)) yield return HoverTipFactory.FromPower<DexterityPower>();
            if (HasUpgradeEffect(5)) yield return HoverTipFactory.FromPower<ScreamBlockBoostPower>();
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(9m, ValueProp.Move),
        new BlockVar(7m, ValueProp.Move)
    };

    public override bool GainsBlock => true;

    public HerSongIsMyScreamHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
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

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id != 6)
            .Select(id => new LocString("cards", Id.Entry + ".upgrade_effect." + id).GetFormattedText())
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    public override Task BeforeCombatStart()
    {
        HasPlayedThisCombat = false;
        return Task.CompletedTask;
    }

    internal PileType GetFirstPlayResultPile(PileType result)
    {
        // The version-compatible card-result hook reserves the pile before
        // OnPlay. Let vanilla's final move return it after all replays.
        return result == PileType.Discard && !IsDupe && HasUpgradeEffect(2) && !HasPlayedThisCombat
            ? PileType.Hand
            : result;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        HasPlayedThisCombat = true;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay);

        if (HasUpgradeEffect(4))
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (HasUpgradeEffect(0))
            await DamageCmd.Attack(Owner.Creature.Block)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

        if (HasUpgradeEffect(1))
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        if (HasUpgradeEffect(3))
            await CardPileCmd.Draw(choiceContext, 2m, Owner);

        // Applied last, so this play's two block grants only benefit from
        // stacks acquired on earlier plays, and Dexterity applies next time.
        if (HasUpgradeEffect(5))
            await PowerCmd.Apply<ScreamBlockBoostPower>(choiceContext, Owner.Creature, 25m, Owner.Creature, this);
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
            // Non-garden upgrades and legacy saves without an ordered list
            // deterministically receive the first still-unclaimed effect.
            effectId = _preparedUpgradeEffect is int prepared && !UpgradeEffectOrder.Contains(prepared)
                ? prepared
                : Enumerable.Range(0, UpgradeEffectCount).First(id => !UpgradeEffectOrder.Contains(id));
            UpgradeEffectOrder = UpgradeEffectOrder.Append(effectId).ToArray();
        }
        _preparedUpgradeEffect = null;
        if (effectId == 6) EnergyCost.UpgradeBy(-1);
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
