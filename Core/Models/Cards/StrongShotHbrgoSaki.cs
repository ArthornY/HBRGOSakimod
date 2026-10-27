using System.Collections.Generic;
using System.Linq;
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

public sealed class StrongShotHbrgoSaki : CardModel
{
    private const int _hitCount = 3;
    private const string _fragileBonusVar = "FragileBonus";

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/strong_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(50m, ValueProp.Move),
        new RepeatVar(_hitCount),
        HbrSkillVars.DamageBonus(_fragileBonusVar, 50m, divisor: 3m)
    };

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);

    public StrongShotHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this ||
               ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int highestFragile = base.CombatState
            .GetOpponentsOf(Owner.Creature)
            .Where(target => target.IsHittable)
            .Select(target =>
                ReptureBowPower.GetEnemyPowerAmount<FragilePower>(
                    Owner.Creature,
                    target))
            .DefaultIfEmpty(0)
            .Max();
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            base.DynamicVars.Damage.BaseValue +
            highestFragile *
            DynamicVars[_fragileBonusVar].BaseValue);
        for (int i = 0; i < _hitCount && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            await DamageCmd.Attack(damage)
                .FromCardCompat(this, cardPlay)
                .TargetingAllOpponents(base.CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeDamageByPercent(50m, divisor: 3m);
        DynamicVars[_fragileBonusVar].UpgradeDamageByPercent(50m, divisor: 3m);
    }
}
