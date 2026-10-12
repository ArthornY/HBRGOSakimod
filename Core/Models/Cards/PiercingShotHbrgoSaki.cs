using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PiercingShotHbrgoSaki : CardModel
{
    private int DamagePerFragile =>
        IsUpgraded || DeckVersion?.IsUpgraded == true ? 2 : 1;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/piercing_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DamageVar[]
        {
            new DamageVar(5m, ValueProp.Move)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FragilePower>()
        };

    public PiercingShotHbrgoSaki()
        : base(
            1,
            CardType.Attack,
            CardRarity.Common,
            TargetType.AllEnemies)
    {
    }

    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType)
    {
        return card != this ||
            ModeTransitionHelper.CanPlayBowRestrictedCard(
                Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature[] targets = CombatState
            .GetOpponentsOf(Owner.Creature)
            .Where(target => target.IsHittable)
            .ToArray();
        foreach (Creature target in targets)
        {
            int fragile =
                ReptureBowPower.GetEnemyPowerAmount<FragilePower>(
                    Owner.Creature,
                    target);
            decimal damage =
                DynamicVars.Damage.BaseValue +
                fragile * DamagePerFragile;
            await DamageCmd.Attack(
                    ModeTransitionHelper.GetAdjustedEffectAmount(
                        this,
                        damage))
                .FromCardCompat(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
