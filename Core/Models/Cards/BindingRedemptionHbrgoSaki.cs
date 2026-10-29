using HBRGOSakiMod.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class BindingRedemptionHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("Recovery", 6m),
            new PowerVar<StrengthPower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<
                PhantomHighDimensionalShellPower>(),
            HoverTipFactory.FromPower<StrengthPower>()
        };

    public BindingRedemptionHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Common,
            HBRGOSakiTargetTypes.OwnedPhantomOrEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        if (cardPlay.Target.PetOwner == Owner &&
            cardPlay.Target.HasPower<PhantomPower>())
        {
            decimal remaining = HbrResourceValueHelper.Fixed(Owner.Creature, DynamicVars["Recovery"].BaseValue);
            HighDimensionalShellPower shell =
                cardPlay.Target.GetPower<HighDimensionalShellPower>();
            if (shell != null)
            {
                remaining -= shell.Restore(remaining);
            }

            if (remaining > 0)
            {
                await CreatureCmd.Heal(cardPlay.Target, remaining);
            }

            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            cardPlay.Target,
            -DynamicVars["StrengthPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Recovery"].UpgradeValueBy(3m);
        DynamicVars["StrengthPower"].UpgradeValueBy(1m);
    }
}
