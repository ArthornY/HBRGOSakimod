using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class NarcissismIsFineHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneEMemberCard
{
    internal const int Flash = 0, Sakura = 1, Innovation = 2, PureLove = 3,
        Opportunity = 4, Makeup = 5, Narcissism = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneEMember ThirtyOneEMember => ThirtyOneEMember.OshimaNiina;
    public override TargetType TargetType => HasUpgradeEffect(Sakura) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(27m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<DestructionRatePower>(), HoverTipFactory.FromPower<HbrComboLargePower>(),
      HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HighDimensionalShellPower>(),
      HoverTipFactory.FromPower<NiinaMakeupPower>(), HoverTipFactory.FromPower<NiinaNextDestructionPower>() };
    public NarcissismIsFineHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("NiinaTarget", HasUpgradeEffect(Sakura) ? "所有敌人" : "目标");
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (HasUpgradeEffect(Innovation))
            await HbrComboPower.Grant<HbrComboLargePower>(context, Owner.Creature, Owner.Creature, this);
        using (var receipt = NiinaNarcissismCombatHelper.Begin(this))
        {
            var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
                .WithHitCount(4).WithHitFx("vfx/vfx_attack_slash");
            if (HasUpgradeEffect(Sakura)) await attack.TargetingAllOpponents(CombatState).Execute(context);
            else if (play.Target?.IsAlive == true && play.Target.Side != Owner.Creature.Side &&
                ReferenceEquals(play.Target.CombatState, CombatState)) await attack.Targeting(play.Target).Execute(context);
            await NiinaNarcissismCombatHelper.CompleteAttack(context, receipt);
        }
        if (HasUpgradeEffect(PureLove))
            await HbrEffectApplication.Apply<NiinaNextDestructionPower>(context, Owner.Creature, 50m,
                Owner.Creature, this, remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
    }
}
