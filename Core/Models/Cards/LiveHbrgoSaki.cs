using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class LiveHbrgoSaki : CardModel
{
    private const string GoldVar = "Gold";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/live_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<ChargePower>(2m),
            new IntVar(GoldVar, 5m),
            new IntVar("AttackUpPercent", 30m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[]
        {
            HoverTipFactory.FromPower<ChargePower>(),
            HoverTipFactory.FromPower<HbrAttackUpPower>()
        };

    public LiveHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Uncommon,
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
        await PlayerCmd.GainGold(
            DynamicVars[GoldVar].BaseValue,
            Owner);
        foreach (var ally in TeamCombatHelper.GetLivingAllies(Owner.Creature))
            await HbrEffectApplication.Apply<HbrAttackUpPower>(choiceContext, ally,
                DynamicVars["AttackUpPercent"].BaseValue, Owner.Creature, this, 2,
                new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.PlayerTurnEnd, 2));
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChargePower"].UpgradeValueBy(1m);
        DynamicVars[GoldVar].UpgradeValueBy(2m);
    }
}
