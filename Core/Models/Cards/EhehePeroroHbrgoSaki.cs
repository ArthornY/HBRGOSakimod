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

public sealed class EhehePeroroHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/ehehe_peroro_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<DodgePower>(),
        HoverTipFactory.FromPower<AngryIzumiYukiPower>()
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<DodgePower>(5m),
        new PowerVar<AngryIzumiYukiPower>(1m)
    };

    public EhehePeroroHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DodgePower>(choiceContext, Owner.Creature, DynamicVars["DodgePower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<AngryIzumiYukiPower>(choiceContext, Owner.Creature, DynamicVars["AngryIzumiYukiPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DodgePower"].UpgradeValueBy(3m);
    }
}
