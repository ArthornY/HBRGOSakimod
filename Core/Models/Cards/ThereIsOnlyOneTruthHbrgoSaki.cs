using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ThereIsOnlyOneTruthHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneXMemberCard, IExposeCard
{
    internal const int Flash = 0, StayAwake = 1, Insight = 2, PhantomThief = 3,
        CutTheKnot = 4, NoUnsolvableMystery = 5, Qed = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.IreneRedmayne;
    public int ExposeKindCount => IsMutable && HasUpgradeEffect(PhantomThief) ? 3 : 1;
    public override TargetType TargetType => IsMutable && HasUpgradeEffect(Insight) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(109m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrCriticalRateUpPower>(), HoverTipFactory.FromPower<HbrComboLargePower>(),
        HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public ThereIsOnlyOneTruthHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("ExposeCount", ExposeKindCount);
        text.Add("IreneQED", IreneExposeCombatHelper.GetDispelledKindCount(IsMutable ? Owner : null));
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => IreneExposeCombatHelper.Play(context, this, play);
}
