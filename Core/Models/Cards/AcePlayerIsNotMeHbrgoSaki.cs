using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AcePlayerIsNotMeHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneDMemberCard
{
    internal const int Flash = 0, Slice = 1, Paranoia = 2, SorrowSnowdrop = 3,
        LateBloomer = 4, DarkFiveBalls = 5, PitchBlackServe = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneDMember ThirtyOneDMember => ThirtyOneDMember.DateAkari;
    public override TargetType TargetType => HasUpgradeEffect(Slice) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    internal int BaseHits => HasUpgradeEffect(DarkFiveBalls) ? 5 : 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(55m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HighDimensionalShellPower>(), HoverTipFactory.FromPower<DestructionRatePower>(),
      HoverTipFactory.FromPower<BreakPower>(), HoverTipFactory.FromPower<HbrAttackUpPower>(),
      HoverTipFactory.FromPower<HbrComboLargePower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
      HoverTipFactory.FromPower<HbrNegativityPower>() }.Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    public AcePlayerIsNotMeHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void OnUpgrade()
    {
        bool before = HasUpgradeEffect(DarkFiveBalls);
        base.OnUpgrade();
        if (!before && HasUpgradeEffect(DarkFiveBalls)) DynamicVars.Damage.UpgradeValueBy(HbrSkillPower.DamageUnits(-30m));
    }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("AkariTarget", HasUpgradeEffect(Slice) ? "所有敌人" : "目标");
        text.Add("AkariHits", BaseHits);
        text.Add("AkariCurrent", HasUpgradeEffect(LateBloomer)
            ? AkariAceCombatHelper.IsLateBloomerActive(this) ? "大器晚成已生效。" : "仍持有[gold]消极[/gold]。" : string.Empty);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        using var receipt = AkariAceCombatHelper.Begin(this);
        if (HasUpgradeEffect(Paranoia))
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, Owner.Creature, 65m, Owner.Creature, this,
                remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        if (HasUpgradeEffect(PitchBlackServe))
        {
            await HbrComboPower.Grant<HbrComboLargePower>(context, Owner.Creature, Owner.Creature, this);
            await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, Owner.Creature, 100m,
                Owner.Creature, this, new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, 1));
        }
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .WithHitCount(BaseHits).WithHitFx("vfx/vfx_attack_slash");
        if (HasUpgradeEffect(Slice)) await attack.TargetingAllOpponents(CombatState).Execute(context);
        else if (play.Target?.IsAlive == true && play.Target.Side != Owner.Creature.Side &&
            ReferenceEquals(play.Target.CombatState, CombatState)) await attack.Targeting(play.Target).Execute(context);
        Owner.GetRelic<Overclock>()?.GainLevelProgress(10m);
    }
}
