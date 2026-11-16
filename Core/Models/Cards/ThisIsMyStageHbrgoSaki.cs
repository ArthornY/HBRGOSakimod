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

public sealed class ThisIsMyStageHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneFMemberCard, IOrchestraCard
{
    internal const int Flash = 0, GuidingBaton = 1, SymphonyForLittleBirds = 2, StellarSymphony = 3,
        EvilAria = 4, BigSister = 5, ComeToMyRoom = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneFMember ThirtyOneFMember => ThirtyOneFMember.HanamuraShiki;
    public string OrchestraExcludedCharacterId => "shiki";
    public decimal OrchestraStepPercent => HasUpgradeEffect(BigSister) ? 10m : 5m;
    public decimal OrchestraMaximumBonusPercent => HasUpgradeEffect(BigSister) ? 50m : 25m;
    public override TargetType TargetType => HasUpgradeEffect(SymphonyForLittleBirds) && !HasUpgradeEffect(StellarSymphony)
        ? TargetType.AllAllies : IsMutable && Owner?.Creature != null && RisaRecoveryCombatHelper.Targets(Owner).Length <= 1
            ? TargetType.Self : TargetType.AnyAlly;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(77m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrComboMediumPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public ThisIsMyStageHbrgoSaki() : base(1, CardType.Skill, TargetType.AnyAlly) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        HanamuraOrchestraCombatHelper.AddDescriptionParameters(this, text);
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => HanamuraOrchestraCombatHelper.Play(context, this, play);
}
