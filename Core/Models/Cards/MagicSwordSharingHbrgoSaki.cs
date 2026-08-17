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

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MagicSwordSharingHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/magic_sword_sharing_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    protected override bool IsPlayable =>
        Owner?.Creature?.HasPower<MagicSwordModePower>() == true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<MagicSwordSharingPower>(
                MagicSwordSharingPower.RollBonus)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<MagicSwordSharingPower>(),
            HoverTipFactory.FromPower<MagicSwordModePower>()
        };

    public MagicSwordSharingHbrgoSaki()
        : base(
            1,
            CardType.Power,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature[] teammates =
            TeamCombatHelper.GetLivingPlayerCreatures(Owner.Creature)
                .Where(creature => creature != Owner.Creature)
                .ToArray();
        foreach (Creature teammate in teammates)
        {
            await PowerCmd.Apply<MagicSwordSharingPower>(
                choiceContext,
                teammate,
                DynamicVars["MagicSwordSharingPower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
