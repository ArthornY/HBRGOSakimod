using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ReadySteadyDieHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneXMemberCard, IExtraServingCard
{
    internal const int Flash = 0, ThunderRoar = 1, LightningStorm = 2, LegendaryMode = 3,
        ThunderOfJustice = 4, GoodChance = 5, SistersMask = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.CarolReaper;
    public override TargetType TargetType => IsMutable && HasUpgradeEffect(LightningStorm) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(99m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrEnergyDebtPower>(), HoverTipFactory.FromPower<HbrAttackUpPower>(),
        HoverTipFactory.FromPower<MindsEyePower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public ReadySteadyDieHbrgoSaki() : base(2, CardType.Attack, TargetType.AnyEnemy) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => CarolHeroCombatHelper.Play(context, this, play);
}
