using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ArrowRainHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/arrow_rain_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DamageVar[] { new DamageVar(7m, ValueProp.Move) };

    public ArrowRainHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(base.CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
