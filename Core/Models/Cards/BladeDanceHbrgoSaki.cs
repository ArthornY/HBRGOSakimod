using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class BladeDanceHbrgoSaki : CardModel
{
    private const int _baseHitCount = 4;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/blade_dance_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new RepeatVar(_baseHitCount)
    };

    protected override bool IsPlayable => CanUseDualSwordsCard();

    public BladeDanceHbrgoSaki()
        : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this || CanUseDualSwordsCard();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        int energySpent = CardCopyContextHelper.TakeXEnergySpent(this, cardPlay.Resources.EnergySpent);
        int hitCount = (int)base.DynamicVars["Repeat"].BaseValue;
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            GetDamageUnits(energySpent));
        if (damage <= 0m || hitCount <= 0)
        {
            return;
        }

        if (CombatManager.Instance.IsOverOrEnding) return;
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(hitCount)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["Repeat"].UpgradeValueBy(2m);
    }

    internal decimal GetDamageUnits(int energySpent) =>
        HbrBattleStatsHelper.IsEnabled(Owner)
            ? HbrSkillPower.DamageUnits(Math.Max(0, energySpent) * 20m)
            : Math.Max(0, energySpent);

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if (!HbrSkillCardDescription.IsEnabled(this)) return;
        int energy = IsMutable && Owner?.PlayerCombatState != null
            ? Math.Max(0, EnergyCost.GetAmountToSpend()) : 1;
        HbrSkillCardDescription.AddDamageParameters(this, description, "BladeDanceDamage",
            HbrSkillPower.DamageUnits(energy * 20m));
        HbrSkillCardDescription.AddDamageParameters(this, description, "XDamage",
            HbrSkillPower.DamageUnits(20m));
    }

    private bool CanUseDualSwordsCard()
    {
        return ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);
    }
}
