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
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class StarsGuideYouHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneCMemberCard
{
    internal const int Flash = 0, Fortune = 1, StardustRoute = 2, FrozenWonder = 3,
        Provocation = 4, LoveDivination = 5, RadiantNarrator = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneCMember ThirtyOneCMember => ThirtyOneCMember.SakurabaSeira;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    internal decimal BaseRecoveryRatio => .1m + (HasUpgradeEffect(LoveDivination) && IsMutable
        ? StarsGuideCombatHelper.ActiveBondCount(Owner) * .1m : 0m);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<HbrCriticalRateUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(),
      HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrDefenseUpPower>(),
      HoverTipFactory.FromPower<HbrAttackDownPower>() };
    public StarsGuideYouHbrgoSaki() : base(1, CardType.Skill, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("DpRecoveryPercent", BaseRecoveryRatio * 100m);
        text.Add("ActiveBonds", IsMutable ? StarsGuideCombatHelper.ActiveBondCount(Owner) : 0);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        int roll = HasUpgradeEffect(Fortune) ? this.Capability<RollGlossaryCapability>()?.EnsureCurrentRollFor(this) ?? 0 : 0;
        decimal ratio = StarsGuideCombatHelper.RecoveryRatio(this, roll);
        var allies = TeamCombatHelper.GetLivingAllies(Owner.Creature);
        foreach (var target in allies)
        {
            await SeiraDpRecoveryHelper.Restore(context, target, ratio, HasUpgradeEffect(Provocation), this);
            if (HasUpgradeEffect(StardustRoute))
            {
                var duration = new HbrEffectProfile(HbrEffectLimit.None, HbrEffectExit.PlayerTurnEnd, 5);
                await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, target, 50m, Owner.Creature, this, profile: duration);
                await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, target, 50m, Owner.Creature, this, profile: duration);
            }
            if (HasUpgradeEffect(FrozenWonder))
            {
                await HbrEffectApplication.Apply<HbrAttackUpPower>(context, target, 30m, Owner.Creature, this,
                    profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
                await HbrEffectApplication.Apply<HbrDefenseUpPower>(context, target, 30m, Owner.Creature, this,
                    profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
            }
        }
        if (HasUpgradeEffect(RadiantNarrator))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, enemy, 20m, Owner.Creature, this,
                    profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
        }
        if (HasUpgradeEffect(Fortune) && roll >= 48) await CardPileCmd.Draw(context, 1m, Owner);
    }
}
