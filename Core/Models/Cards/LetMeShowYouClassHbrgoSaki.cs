using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class LetMeShowYouClassHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneFMemberCard, IExtraServingCard
{
    internal const int Flash = 0, LittleHighClass = 1, UpsideDown = 2, PhotonBullet = 3,
        RadiantAdvance = 4, MionComeHere = 5, MaruyamaCaptain = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneFMember ThirtyOneFMember => ThirtyOneFMember.MaruyamaKanata;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(174m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust), HoverTipFactory.FromPower<HbrEnergyDebtPower>(),
        HoverTipFactory.FromPower<HbrDpDefenseDownPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
        HoverTipFactory.FromPower<MindsEyePower>(), HoverTipFactory.FromPower<HbrComboMediumPower>(),
        HoverTipFactory.FromPower<HbrMoralePower>(), HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public LetMeShowYouClassHbrgoSaki() : base(4, CardType.Attack, TargetType.AllEnemies) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => MaruyamaStyleCombatHelper.Play(context, this, play);
}
