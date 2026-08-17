using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DragonArrowHbrgoSaki : CardModel
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/dragon_arrow_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool HasEnergyCostX => true;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(10m, ValueProp.Move)
    };

    public DragonArrowHbrgoSaki()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this ||
               ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        int energySpent = DualSwordsCopyHelper.TakeXEnergySpent(
            this,
            cardPlay.Resources.EnergySpent);
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            DynamicVars.Damage.BaseValue);
        for (int i = 0;
             i < energySpent && !CombatManager.Instance.IsOverOrEnding;
             i++)
        {
            await DamageCmd.Attack(damage)
                .FromCard(this)
                .TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
