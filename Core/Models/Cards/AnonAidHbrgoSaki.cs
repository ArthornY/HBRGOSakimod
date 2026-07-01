using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AnonAidHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/anon_aid_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<StrongestShieldPower>()
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<StrongestShieldPower>(1m)
    };

    public AnonAidHbrgoSaki()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrongestShieldPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["StrongestShieldPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrongestShieldPower"].UpgradeValueBy(1m);
    }
}
