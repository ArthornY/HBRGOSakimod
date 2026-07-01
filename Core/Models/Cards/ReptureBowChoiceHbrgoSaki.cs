using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ReptureBowChoiceHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/chihaya_classmate_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<ReptureBowPower>()
    };

    public ReptureBowChoiceHbrgoSaki()
        : base(-1, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
    {
    }
}
