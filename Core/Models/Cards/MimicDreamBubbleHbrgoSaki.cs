using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicDreamBubbleHbrgoSaki : CardModel
{
    private const int _hitCount = 4;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/mimic_dream_bubble_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(100m, ValueProp.Move, divisor: 3m),
        new RepeatVar(_hitCount)
    };

    protected override bool IsPlayable => CanUseDualSwordsCard();

    public MimicDreamBubbleHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this || CanUseDualSwordsCard();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        System.ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        decimal primaryDamageTotal = 0m;
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            base.DynamicVars.Damage.BaseValue);
        for (int i = 0; i < _hitCount && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            var attack = await DamageCmd.Attack(damage)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            primaryDamageTotal += attack.Results
                .SelectMany(results => results)
                .Where(result => result.Receiver == cardPlay.Target)
                .Sum(result => (decimal)result.BlockedDamage + result.UnblockedDamage);
        }

        if (primaryDamageTotal <= 0 ||
            !cardPlay.Target.IsAlive ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        using (PrecalculatedDamageContext.Begin(
            this,
            cardPlay.Target,
            primaryDamageTotal))
        {
            await DamageCmd.Attack(primaryDamageTotal)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Unpowered()
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeDamageByPercent(50m, divisor: 3m);
    }

    private bool CanUseDualSwordsCard()
    {
        return ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);
    }
}
