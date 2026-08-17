using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DoublePowersHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/double_powers_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public DoublePowersHbrgoSaki()
        : base(
            3,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        PowerModel[] powers = Owner.Creature.CombatState
            .Allies
            .Concat(Owner.Creature.CombatState.Enemies)
            .SelectMany(creature => creature.Powers)
            .Where(power => power.Amount != 0)
            .ToArray();
        foreach (PowerModel power in powers)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                power,
                power.Amount,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
