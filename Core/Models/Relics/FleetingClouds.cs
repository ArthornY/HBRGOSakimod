using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class FleetingClouds : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("relics/fleeting_clouds.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory
            .FromCardWithCardHoverTips<RecallHbrgoSaki>()
            .Concat(HoverTipFactory
                .FromCardWithCardHoverTips<AmnesiaHbrgoSaki>());

    public override async Task AfterObtained()
    {
        CardModel[] cards =
        {
            Owner.RunState.CreateCard<RecallHbrgoSaki>(Owner),
            Owner.RunState.CreateCard<AmnesiaHbrgoSaki>(Owner)
        };
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.Add(cards, PileType.Deck),
            2f);
    }
}
