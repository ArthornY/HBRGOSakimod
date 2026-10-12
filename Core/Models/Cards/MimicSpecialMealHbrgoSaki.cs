using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicSpecialMealHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/mimic_special_meal_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<ChargePower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            Overclock overclock = ModelDb.Relic<Overclock>();
            return new IHoverTip[]
            {
                HoverTipFactory.FromPower<ChargePower>(),
                // Avoid BaseLib folding the hidden relic's source-only tip list.
                new HoverTip(
                    overclock.Title,
                    overclock.DynamicDescription,
                    overclock.Icon)
            };
        }
    }

    public MimicSpecialMealHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<ChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ChargePower"].BaseValue,
            Owner.Creature,
            this);
        Overclock overclock = Owner.GetRelic<Overclock>();
        overclock?.GainCharge(overclock.ChargePerLevel);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChargePower"].UpgradeValueBy(1m);
    }
}
